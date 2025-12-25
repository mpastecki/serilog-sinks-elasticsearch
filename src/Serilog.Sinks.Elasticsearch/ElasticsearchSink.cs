// Copyright © Serilog Contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting;

namespace Serilog.Sinks.Elasticsearch;

/// <summary>
/// A sink that writes log events to Elasticsearch 8.x using the Bulk API.
/// </summary>
sealed class ElasticsearchSink : IBatchedLogEventSink, IDisposable
#if FEATURE_ASYNCDISPOSABLE
    , IAsyncDisposable
#endif
{
    readonly HttpClient _httpClient;
    readonly bool _disposeHttpClient;
    readonly bool _isSharedHttpClient;
    readonly Uri _bulkUri;
    readonly string _indexFormat;
    readonly ITextFormatter _formatter;
    readonly string _apiKey;
    readonly IReadOnlyDictionary<string, string>? _customHeaders;

    /// <summary>
    /// Construct a sink that writes to Elasticsearch.
    /// </summary>
    /// <param name="options">Configuration options for the sink.</param>
    /// <exception cref="ArgumentNullException">When options is null.</exception>
    /// <exception cref="ArgumentException">When required options are missing.</exception>
    public ElasticsearchSink(ElasticsearchSinkOptions options)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        if (options.ServerUrl is null) throw new ArgumentException("ServerUrl is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.ApiKey)) throw new ArgumentException("ApiKey is required.", nameof(options));

        _indexFormat = options.IndexFormat;
        _apiKey = options.ApiKey!; // Already validated non-null/whitespace above
        _customHeaders = options.CustomHeaders is not null
            ? new Dictionary<string, string>(options.CustomHeaders)
            : null;

        // Validate index format at construction time to fail fast
        try
        {
            _ = string.Format(_indexFormat, DateTimeOffset.UtcNow);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException($"IndexFormat '{_indexFormat}' is not a valid format string: {ex.Message}", nameof(options));
        }

        // Use custom formatter with correct timestamp field, or wrap default formatter
        _formatter = options.Formatter ?? new ElasticsearchJsonFormatter(
            timestampFieldName: options.TimestampFieldName,
            renderMessage: options.RenderMessage);

        // Build bulk endpoint URI
        var baseUri = options.ServerUrl.ToString().TrimEnd('/');
        var bulkPath = string.IsNullOrEmpty(options.Pipeline)
            ? "/_bulk"
            : $"/_bulk?pipeline={Uri.EscapeDataString(options.Pipeline)}";
        _bulkUri = new Uri(baseUri + bulkPath);

        // Create or use provided HttpClient
        if (options.HttpClientFactory is not null)
        {
            _httpClient = options.HttpClientFactory()
                ?? throw new ArgumentException("HttpClientFactory returned null.", nameof(options));
            _disposeHttpClient = false;
            _isSharedHttpClient = true;
        }
        else
        {
            _httpClient = new HttpClient { Timeout = options.RequestTimeout };
            _disposeHttpClient = true;
            _isSharedHttpClient = false;

            // Only set default headers on owned HttpClient (thread-safe)
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("ApiKey", _apiKey);
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            if (_customHeaders is not null)
            {
                foreach (var header in _customHeaders)
                {
                    _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
                }
            }
        }
    }

    /// <inheritdoc />
    public async Task EmitBatchAsync(IReadOnlyCollection<LogEvent> batch)
    {
        if (batch.Count == 0)
            return;

        var payload = FormatBulkPayload(batch);

        using var content = new StringContent(payload, Encoding.UTF8, "application/x-ndjson");

        HttpResponseMessage response;
        try
        {
            // For shared HttpClient, use HttpRequestMessage to add headers per-request (thread-safe)
            if (_isSharedHttpClient)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, _bulkUri);
                request.Content = content;
                request.Headers.Authorization = new AuthenticationHeaderValue("ApiKey", _apiKey);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                if (_customHeaders is not null)
                {
                    foreach (var header in _customHeaders)
                    {
                        request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }

                response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            }
            else
            {
                response = await _httpClient.PostAsync(_bulkUri, content).ConfigureAwait(false);
            }
        }
        catch (HttpRequestException ex)
        {
            SelfLog.WriteLine("Elasticsearch sink: HTTP request failed: {0}", ex.Message);
            throw;
        }
        catch (OperationCanceledException ex)
        {
            // Handles both TaskCanceledException (timeout) and user cancellation
            SelfLog.WriteLine("Elasticsearch sink: Request cancelled or timed out: {0}", ex.Message);
            throw;
        }

        // Ensure response is disposed in all code paths
        using (response)
        {
            var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                SelfLog.WriteLine(
                    "Elasticsearch sink: Bulk request failed with status {0}: {1}",
                    (int)response.StatusCode,
                    responseContent);

                // Throw to trigger retry mechanism in BatchingSink
                throw new HttpRequestException(
                    $"Elasticsearch bulk request failed with status {response.StatusCode}");
            }

            // Check for partial failures using proper JSON parsing
            if (HasBulkErrors(responseContent))
            {
                SelfLog.WriteLine(
                    "Elasticsearch sink: Bulk request had partial failures: {0}",
                    responseContent);

                // Throw to trigger retry - partial failures mean data loss
                throw new HttpRequestException(
                    "Elasticsearch bulk request had partial failures. Some documents were not indexed.");
            }
        }
    }

    /// <summary>
    /// Parses the bulk response to check for errors using proper JSON parsing.
    /// </summary>
    static bool HasBulkErrors(string responseContent)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseContent);
            if (doc.RootElement.TryGetProperty("errors", out var errorsElement))
            {
                return errorsElement.ValueKind == JsonValueKind.True;
            }
            return false;
        }
        catch (JsonException)
        {
            // If we can't parse the response, assume no errors rather than throwing
            SelfLog.WriteLine("Elasticsearch sink: Could not parse bulk response as JSON");
            return false;
        }
    }

    /// <inheritdoc />
    public Task OnEmptyBatchAsync() => Task.CompletedTask;

    string FormatBulkPayload(IReadOnlyCollection<LogEvent> batch)
    {
        var sb = new StringBuilder();

        foreach (var logEvent in batch)
        {
            try
            {
                // Compute index name for this event
                var indexName = string.Format(_indexFormat, logEvent.Timestamp);

                // Write action line with explicit \n for cross-platform NDJSON compatibility
                sb.Append("{\"index\":{\"_index\":\"");
                sb.Append(EscapeJsonString(indexName));
                sb.Append("\"}}\n");

                // Write document line
                var docWriter = new StringWriter(sb);
                _formatter.Format(logEvent, docWriter);

                // Normalize to Unix newlines: strip any trailing \r, ensure exactly one \n
                while (sb.Length > 0 && sb[sb.Length - 1] == '\r')
                {
                    sb.Remove(sb.Length - 1, 1);
                }
                if (sb.Length == 0 || sb[sb.Length - 1] != '\n')
                {
                    sb.Append('\n');
                }
            }
            catch (Exception ex)
            {
                // Log and skip problematic events instead of failing the entire batch
                SelfLog.WriteLine(
                    "Elasticsearch sink: Failed to format event at {0}: {1}",
                    logEvent.Timestamp,
                    ex.Message);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Escapes special characters in a string for JSON.
    /// </summary>
    static string EscapeJsonString(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < ' ')
                        sb.Append($"\\u{(int)c:x4}");
                    else
                        sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposeHttpClient)
        {
            _httpClient.Dispose();
        }
    }

#if FEATURE_ASYNCDISPOSABLE
    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Dispose();
        return default;
    }
#endif
}

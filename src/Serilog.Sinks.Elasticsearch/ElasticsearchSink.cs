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
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Json;

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
    readonly Uri _bulkUri;
    readonly string _indexFormat;
    readonly ITextFormatter _formatter;
    readonly string _timestampFieldName;

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
        _timestampFieldName = options.TimestampFieldName;
        _formatter = options.Formatter ?? new JsonFormatter(
            closingDelimiter: null,
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
            _httpClient = options.HttpClientFactory();
            _disposeHttpClient = false;
        }
        else
        {
            _httpClient = new HttpClient { Timeout = options.RequestTimeout };
            _disposeHttpClient = true;
        }

        // Set authorization header
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("ApiKey", options.ApiKey);

        // Set content type for NDJSON (Newline Delimited JSON)
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        // Add custom headers if provided
        if (options.CustomHeaders is not null)
        {
            foreach (var header in options.CustomHeaders)
            {
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
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
            response = await _httpClient.PostAsync(_bulkUri, content).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            SelfLog.WriteLine("Elasticsearch sink: HTTP request failed: {0}", ex.Message);
            throw;
        }
#if FEATURE_ASYNCDISPOSABLE
        catch (TaskCanceledException ex) when (!ex.CancellationToken.IsCancellationRequested)
        {
            SelfLog.WriteLine("Elasticsearch sink: Request timed out: {0}", ex.Message);
            throw;
        }
#endif

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            SelfLog.WriteLine(
                "Elasticsearch sink: Bulk request failed with status {0}: {1}",
                (int)response.StatusCode,
                responseBody);

            // Throw to trigger retry mechanism in BatchingSink
            throw new HttpRequestException(
                $"Elasticsearch bulk request failed with status {response.StatusCode}");
        }

        // Check for partial failures in the bulk response
        var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (responseContent.Contains("\"errors\":true"))
        {
            SelfLog.WriteLine(
                "Elasticsearch sink: Bulk request had partial failures: {0}",
                responseContent);
        }
    }

    /// <inheritdoc />
    public Task OnEmptyBatchAsync() => Task.CompletedTask;

    string FormatBulkPayload(IReadOnlyCollection<LogEvent> batch)
    {
        var sb = new StringBuilder();

        foreach (var logEvent in batch)
        {
            // Compute index name for this event
            var indexName = string.Format(_indexFormat, logEvent.Timestamp);

            // Write action line (Elasticsearch 8.x doesn't use document types)
            sb.Append("{\"index\":{\"_index\":\"");
            sb.Append(indexName);
            sb.Append("\"}}");
            sb.AppendLine();

            // Write document line
            using var docWriter = new StringWriter(sb);
            _formatter.Format(logEvent, docWriter);

            // Ensure the document ends with a newline
            if (sb.Length > 0 && sb[sb.Length - 1] != '\n')
            {
                sb.AppendLine();
            }
        }

        // Replace "Timestamp" with the configured timestamp field name if different
        if (_timestampFieldName != "Timestamp")
        {
            sb.Replace("\"Timestamp\":", $"\"{_timestampFieldName}\":");
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

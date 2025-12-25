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
using Serilog.Configuration;
using Serilog.Formatting;

namespace Serilog.Sinks.Elasticsearch;

/// <summary>
/// Configuration options for the Elasticsearch sink.
/// </summary>
public class ElasticsearchSinkOptions
{
    string _indexFormat = "logs-{0:yyyy.MM.dd}";
    string _timestampFieldName = "@timestamp";
    TimeSpan _requestTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The Elasticsearch server URL. Required.
    /// Example: "https://my-cluster.es.us-east-1.aws.elastic.cloud:443"
    /// </summary>
    public Uri? ServerUrl { get; set; }

    /// <summary>
    /// The Elasticsearch API key for authentication. Required.
    /// This is the Base64-encoded API key (id:api_key format already encoded).
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// The index name or pattern. Supports date-based patterns using string.Format syntax.
    /// Default: "logs-{0:yyyy.MM.dd}"
    /// The timestamp of each event is passed as argument {0}.
    /// </summary>
    /// <example>
    /// "logs" - static index name
    /// "logs-{0:yyyy.MM.dd}" - daily indices
    /// "logs-{0:yyyy.MM}" - monthly indices
    /// "myapp-logs-{0:yyyy.MM.dd}" - prefixed daily indices
    /// </example>
    /// <exception cref="ArgumentNullException">When set to null.</exception>
    public string IndexFormat
    {
        get => _indexFormat;
        set => _indexFormat = value ?? throw new ArgumentNullException(nameof(value), "IndexFormat cannot be null.");
    }

    /// <summary>
    /// The text formatter used to format log events as JSON for Elasticsearch.
    /// Default: <see cref="ElasticsearchJsonFormatter"/> with the configured TimestampFieldName.
    /// </summary>
    public ITextFormatter? Formatter { get; set; }

    /// <summary>
    /// Options controlling batch sizes and buffering.
    /// If null, default <see cref="BatchingOptions"/> values are used.
    /// </summary>
    public BatchingOptions? BatchingOptions { get; set; }

    /// <summary>
    /// HTTP request timeout for Elasticsearch requests.
    /// Default: 30 seconds. Must be positive.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">When set to zero or negative.</exception>
    public TimeSpan RequestTimeout
    {
        get => _requestTimeout;
        set
        {
            if (value <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(value), "RequestTimeout must be positive.");
            _requestTimeout = value;
        }
    }

    /// <summary>
    /// Optional custom HttpClient factory. If provided, the sink will not dispose the HttpClient.
    /// Useful for scenarios where HttpClient lifecycle is managed externally (e.g., IHttpClientFactory).
    /// Note: When using a factory-provided HttpClient, headers are added per-request for thread safety.
    /// </summary>
    public Func<HttpClient>? HttpClientFactory { get; set; }

    /// <summary>
    /// Additional HTTP headers to include in requests to Elasticsearch.
    /// </summary>
    public IDictionary<string, string>? CustomHeaders { get; set; }

    /// <summary>
    /// Whether to render the message template as a "RenderedMessage" property.
    /// Default: true (for better searchability in Elasticsearch).
    /// </summary>
    public bool RenderMessage { get; set; } = true;

    /// <summary>
    /// The name of the timestamp field in the Elasticsearch document.
    /// Default: "@timestamp" (ECS compatible).
    /// </summary>
    /// <exception cref="ArgumentException">When set to null or empty.</exception>
    public string TimestampFieldName
    {
        get => _timestampFieldName;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("TimestampFieldName cannot be null or empty.", nameof(value));
            _timestampFieldName = value;
        }
    }

    /// <summary>
    /// The pipeline to use for ingestion. Optional.
    /// </summary>
    public string? Pipeline { get; set; }

    /// <summary>
    /// Validates the configuration and returns a list of validation errors.
    /// </summary>
    /// <returns>A list of validation error messages, empty if valid.</returns>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (ServerUrl is null)
            errors.Add("ServerUrl is required.");

        if (string.IsNullOrWhiteSpace(ApiKey))
            errors.Add("ApiKey is required.");

        try
        {
            _ = string.Format(_indexFormat, DateTimeOffset.UtcNow);
        }
        catch (FormatException)
        {
            errors.Add($"IndexFormat '{_indexFormat}' is not a valid format string.");
        }

        return errors;
    }

    /// <summary>
    /// Validates the configuration and throws if invalid.
    /// </summary>
    /// <exception cref="InvalidOperationException">When the configuration is invalid.</exception>
    public void ThrowIfInvalid()
    {
        var errors = Validate();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"ElasticsearchSinkOptions is invalid: {string.Join("; ", errors)}");
        }
    }
}

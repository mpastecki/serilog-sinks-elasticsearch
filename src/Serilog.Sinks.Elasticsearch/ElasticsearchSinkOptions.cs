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
    public string IndexFormat { get; set; } = "logs-{0:yyyy.MM.dd}";

    /// <summary>
    /// The text formatter used to format log events as JSON for Elasticsearch.
    /// Default: <see cref="Formatting.Json.JsonFormatter"/> with renderMessage=true.
    /// </summary>
    public ITextFormatter? Formatter { get; set; }

    /// <summary>
    /// Options controlling batch sizes and buffering.
    /// If null, default <see cref="BatchingOptions"/> values are used.
    /// </summary>
    public BatchingOptions? BatchingOptions { get; set; }

    /// <summary>
    /// HTTP request timeout for Elasticsearch requests.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Optional custom HttpClient factory. If provided, the sink will not dispose the HttpClient.
    /// Useful for scenarios where HttpClient lifecycle is managed externally (e.g., IHttpClientFactory).
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
    public string TimestampFieldName { get; set; } = "@timestamp";

    /// <summary>
    /// The pipeline to use for ingestion. Optional.
    /// </summary>
    public string? Pipeline { get; set; }
}

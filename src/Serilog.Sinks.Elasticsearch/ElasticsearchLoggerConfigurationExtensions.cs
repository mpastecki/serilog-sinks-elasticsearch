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

using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.Elasticsearch;

namespace Serilog;

/// <summary>
/// Extends <see cref="LoggerSinkConfiguration"/> with methods to add the Elasticsearch sink.
/// </summary>
public static class ElasticsearchLoggerConfigurationExtensions
{
    /// <summary>
    /// Write log events to Elasticsearch 8.x.
    /// </summary>
    /// <param name="loggerSinkConfiguration">The logger sink configuration.</param>
    /// <param name="serverUrl">The Elasticsearch server URL.</param>
    /// <param name="apiKey">The API key for authentication.</param>
    /// <param name="indexFormat">The index name pattern. Default: "logs-{0:yyyy.MM.dd}"</param>
    /// <param name="restrictedToMinimumLevel">The minimum level for events passed to the sink.</param>
    /// <param name="levelSwitch">A switch allowing the minimum level to be changed at runtime.</param>
    /// <returns>Configuration object allowing method chaining.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="loggerSinkConfiguration"/> is null.</exception>
    /// <exception cref="ArgumentNullException">When <paramref name="serverUrl"/> is null.</exception>
    /// <exception cref="ArgumentNullException">When <paramref name="apiKey"/> is null or empty.</exception>
    public static LoggerConfiguration Elasticsearch(
        this LoggerSinkConfiguration loggerSinkConfiguration,
        string serverUrl,
        string apiKey,
        string indexFormat = "logs-{0:yyyy.MM.dd}",
        LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
        LoggingLevelSwitch? levelSwitch = null)
    {
        if (loggerSinkConfiguration is null) throw new ArgumentNullException(nameof(loggerSinkConfiguration));
        if (serverUrl is null) throw new ArgumentNullException(nameof(serverUrl));
        if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentNullException(nameof(apiKey));

        var options = new ElasticsearchSinkOptions
        {
            ServerUrl = new Uri(serverUrl),
            ApiKey = apiKey,
            IndexFormat = indexFormat
        };

        return Elasticsearch(loggerSinkConfiguration, options, restrictedToMinimumLevel, levelSwitch);
    }

    /// <summary>
    /// Write log events to Elasticsearch 8.x with full configuration options.
    /// </summary>
    /// <param name="loggerSinkConfiguration">The logger sink configuration.</param>
    /// <param name="options">Configuration options for the Elasticsearch sink.</param>
    /// <param name="restrictedToMinimumLevel">The minimum level for events passed to the sink.</param>
    /// <param name="levelSwitch">A switch allowing the minimum level to be changed at runtime.</param>
    /// <returns>Configuration object allowing method chaining.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="loggerSinkConfiguration"/> is null.</exception>
    /// <exception cref="ArgumentNullException">When <paramref name="options"/> is null.</exception>
    public static LoggerConfiguration Elasticsearch(
        this LoggerSinkConfiguration loggerSinkConfiguration,
        ElasticsearchSinkOptions options,
        LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
        LoggingLevelSwitch? levelSwitch = null)
    {
        if (loggerSinkConfiguration is null) throw new ArgumentNullException(nameof(loggerSinkConfiguration));
        if (options is null) throw new ArgumentNullException(nameof(options));

        var batchingOptions = options.BatchingOptions ?? new BatchingOptions();
        var sink = new ElasticsearchSink(options);

        return loggerSinkConfiguration.Sink(sink, batchingOptions, restrictedToMinimumLevel, levelSwitch);
    }
}

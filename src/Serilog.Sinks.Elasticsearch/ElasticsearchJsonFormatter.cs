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

using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Json;

namespace Serilog.Sinks.Elasticsearch;

/// <summary>
/// A JSON formatter optimized for Elasticsearch that correctly handles the timestamp field name
/// without using string replacement on the output.
/// </summary>
sealed class ElasticsearchJsonFormatter : ITextFormatter
{
    readonly string _timestampFieldName;
    readonly bool _renderMessage;
    readonly JsonValueFormatter _valueFormatter;

    /// <summary>
    /// Construct a formatter for Elasticsearch.
    /// </summary>
    /// <param name="timestampFieldName">The name of the timestamp field. Default: "@timestamp"</param>
    /// <param name="renderMessage">Whether to render the message template. Default: true</param>
    public ElasticsearchJsonFormatter(
        string timestampFieldName = "@timestamp",
        bool renderMessage = true)
    {
        _timestampFieldName = timestampFieldName ?? "@timestamp";
        _renderMessage = renderMessage;
        _valueFormatter = new JsonValueFormatter(typeTagName: null);
    }

    /// <inheritdoc />
    public void Format(LogEvent logEvent, TextWriter output)
    {
        if (logEvent is null) throw new ArgumentNullException(nameof(logEvent));
        if (output is null) throw new ArgumentNullException(nameof(output));

        output.Write('{');

        // Write timestamp with configured field name
        WriteQuotedJsonString(_timestampFieldName, output);
        output.Write(':');
        WriteQuotedJsonString(logEvent.Timestamp.ToString("O"), output);

        // Write level
        output.Write(",\"Level\":");
        WriteQuotedJsonString(logEvent.Level.ToString(), output);

        // Write message template
        output.Write(",\"MessageTemplate\":");
        WriteQuotedJsonString(logEvent.MessageTemplate.Text, output);

        // Write rendered message if requested
        if (_renderMessage)
        {
            output.Write(",\"RenderedMessage\":");
            WriteQuotedJsonString(logEvent.MessageTemplate.Render(logEvent.Properties), output);
        }

        // Write exception if present
        if (logEvent.Exception is not null)
        {
            output.Write(",\"Exception\":");
            WriteQuotedJsonString(logEvent.Exception.ToString(), output);
        }

        // Write properties
        if (logEvent.Properties.Count > 0)
        {
            output.Write(",\"Properties\":{");
            var first = true;
            foreach (var property in logEvent.Properties)
            {
                if (!first)
                    output.Write(',');
                first = false;

                WriteQuotedJsonString(property.Key, output);
                output.Write(':');
                try
                {
                    _valueFormatter.Format(property.Value, output);
                }
                catch (Exception ex)
                {
                    // Write a placeholder for the failed property value to maintain valid JSON.
                    // Format: "[Error formatting value: ExceptionTypeName]"
                    output.Write('"');
                    output.Write("[Error formatting value: ");
                    // Write exception type name, escaping any special chars (unlikely but safe)
                    var exTypeName = ex.GetType().Name;
                    foreach (var c in exTypeName)
                    {
                        if (c == '"' || c == '\\')
                            output.Write('\\');
                        output.Write(c);
                    }
                    output.Write(']');
                    output.Write('"');
                    SelfLog.WriteLine(
                        "Elasticsearch formatter: Failed to format property '{0}': {1}",
                        property.Key,
                        ex.Message);
                }
            }
            output.Write('}');
        }

        output.Write('}');
        // Use explicit \n for cross-platform NDJSON compatibility
        output.Write('\n');
    }

    /// <summary>
    /// Writes a JSON-escaped string with quotes.
    /// </summary>
    static void WriteQuotedJsonString(string value, TextWriter output)
    {
        output.Write('"');

        if (!string.IsNullOrEmpty(value))
        {
            foreach (var c in value)
            {
                switch (c)
                {
                    case '"':
                        output.Write("\\\"");
                        break;
                    case '\\':
                        output.Write("\\\\");
                        break;
                    case '\b':
                        output.Write("\\b");
                        break;
                    case '\f':
                        output.Write("\\f");
                        break;
                    case '\n':
                        output.Write("\\n");
                        break;
                    case '\r':
                        output.Write("\\r");
                        break;
                    case '\t':
                        output.Write("\\t");
                        break;
                    default:
                        if (c < ' ')
                        {
                            output.Write("\\u");
                            output.Write(((int)c).ToString("x4"));
                        }
                        else
                        {
                            output.Write(c);
                        }
                        break;
                }
            }
        }

        output.Write('"');
    }
}

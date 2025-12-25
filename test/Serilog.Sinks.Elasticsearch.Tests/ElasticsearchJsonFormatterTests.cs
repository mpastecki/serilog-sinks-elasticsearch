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

using System.Text.Json;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace Serilog.Sinks.Elasticsearch.Tests;

public class ElasticsearchJsonFormatterTests
{
    [Fact]
    public void Format_ProducesValidJson()
    {
        // Arrange
        var formatter = new ElasticsearchJsonFormatter();
        var logEvent = CreateLogEvent(LogEventLevel.Information, "Test message");
        var output = new StringWriter();

        // Act
        formatter.Format(logEvent, output);
        var json = output.ToString().TrimEnd('\n');

        // Assert - should be valid JSON
        var doc = JsonDocument.Parse(json);
        Assert.NotNull(doc);
    }

    [Fact]
    public void Format_IncludesTimestampWithDefaultFieldName()
    {
        // Arrange
        var formatter = new ElasticsearchJsonFormatter();
        var timestamp = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var logEvent = CreateLogEvent(LogEventLevel.Information, "Test", timestamp);
        var output = new StringWriter();

        // Act
        formatter.Format(logEvent, output);
        var json = output.ToString();

        // Assert
        Assert.Contains("\"@timestamp\":", json);
        Assert.Contains("2024-01-15T10:30:00", json);
    }

    [Fact]
    public void Format_UsesCustomTimestampFieldName()
    {
        // Arrange
        var formatter = new ElasticsearchJsonFormatter(timestampFieldName: "custom_time");
        var logEvent = CreateLogEvent(LogEventLevel.Information, "Test");
        var output = new StringWriter();

        // Act
        formatter.Format(logEvent, output);
        var json = output.ToString();

        // Assert
        Assert.Contains("\"custom_time\":", json);
        Assert.DoesNotContain("\"@timestamp\":", json);
    }

    [Fact]
    public void Format_IncludesLevel()
    {
        // Arrange
        var formatter = new ElasticsearchJsonFormatter();
        var logEvent = CreateLogEvent(LogEventLevel.Warning, "Test");
        var output = new StringWriter();

        // Act
        formatter.Format(logEvent, output);
        var json = output.ToString();

        // Assert
        Assert.Contains("\"Level\":\"Warning\"", json);
    }

    [Fact]
    public void Format_IncludesMessageTemplate()
    {
        // Arrange
        var formatter = new ElasticsearchJsonFormatter();
        var logEvent = CreateLogEvent(LogEventLevel.Information, "Hello {Name}");
        var output = new StringWriter();

        // Act
        formatter.Format(logEvent, output);
        var json = output.ToString();

        // Assert
        Assert.Contains("\"MessageTemplate\":\"Hello {Name}\"", json);
    }

    [Fact]
    public void Format_IncludesRenderedMessage_WhenRenderMessageIsTrue()
    {
        // Arrange
        var formatter = new ElasticsearchJsonFormatter(renderMessage: true);
        var logEvent = CreateLogEvent(LogEventLevel.Information, "Test message");
        var output = new StringWriter();

        // Act
        formatter.Format(logEvent, output);
        var json = output.ToString();

        // Assert
        Assert.Contains("\"RenderedMessage\":", json);
    }

    [Fact]
    public void Format_OmitsRenderedMessage_WhenRenderMessageIsFalse()
    {
        // Arrange
        var formatter = new ElasticsearchJsonFormatter(renderMessage: false);
        var logEvent = CreateLogEvent(LogEventLevel.Information, "Test message");
        var output = new StringWriter();

        // Act
        formatter.Format(logEvent, output);
        var json = output.ToString();

        // Assert
        Assert.DoesNotContain("\"RenderedMessage\":", json);
    }

    [Fact]
    public void Format_IncludesException_WhenPresent()
    {
        // Arrange
        var formatter = new ElasticsearchJsonFormatter();
        var exception = new InvalidOperationException("Test error");
        var logEvent = CreateLogEventWithException(LogEventLevel.Error, "Error occurred", exception);
        var output = new StringWriter();

        // Act
        formatter.Format(logEvent, output);
        var json = output.ToString();

        // Assert
        Assert.Contains("\"Exception\":", json);
        Assert.Contains("InvalidOperationException", json);
        Assert.Contains("Test error", json);
    }

    [Fact]
    public void Format_OmitsException_WhenNull()
    {
        // Arrange
        var formatter = new ElasticsearchJsonFormatter();
        var logEvent = CreateLogEvent(LogEventLevel.Information, "Test");
        var output = new StringWriter();

        // Act
        formatter.Format(logEvent, output);
        var json = output.ToString();

        // Assert
        Assert.DoesNotContain("\"Exception\":", json);
    }

    [Fact]
    public void Format_IncludesProperties()
    {
        // Arrange
        var formatter = new ElasticsearchJsonFormatter();
        var properties = new List<LogEventProperty>
        {
            new LogEventProperty("UserId", new ScalarValue(123)),
            new LogEventProperty("Action", new ScalarValue("Login"))
        };
        var logEvent = CreateLogEventWithProperties(LogEventLevel.Information, "User action", properties);
        var output = new StringWriter();

        // Act
        formatter.Format(logEvent, output);
        var json = output.ToString();

        // Assert
        Assert.Contains("\"Properties\":{", json);
        Assert.Contains("\"UserId\":123", json);
        Assert.Contains("\"Action\":\"Login\"", json);
    }

    [Fact]
    public void Format_OmitsProperties_WhenEmpty()
    {
        // Arrange
        var formatter = new ElasticsearchJsonFormatter();
        var logEvent = CreateLogEvent(LogEventLevel.Information, "Test");
        var output = new StringWriter();

        // Act
        formatter.Format(logEvent, output);
        var json = output.ToString();

        // Assert
        Assert.DoesNotContain("\"Properties\":", json);
    }

    [Theory]
    [InlineData("\"", "\\\"")]
    [InlineData("\\", "\\\\")]
    [InlineData("\n", "\\n")]
    [InlineData("\r", "\\r")]
    [InlineData("\t", "\\t")]
    public void Format_EscapesSpecialCharactersInMessage(string input, string expectedEscape)
    {
        // Arrange
        var formatter = new ElasticsearchJsonFormatter();
        var logEvent = CreateLogEvent(LogEventLevel.Information, $"Message with {input} character");
        var output = new StringWriter();

        // Act
        formatter.Format(logEvent, output);
        var json = output.ToString();

        // Assert
        Assert.Contains(expectedEscape, json);
        // Should still be valid JSON
        var doc = JsonDocument.Parse(json.TrimEnd('\n'));
        Assert.NotNull(doc);
    }

    [Fact]
    public void Format_EscapesControlCharacters()
    {
        // Arrange
        var formatter = new ElasticsearchJsonFormatter();
        var logEvent = CreateLogEvent(LogEventLevel.Information, "Message with \x00 control char");
        var output = new StringWriter();

        // Act
        formatter.Format(logEvent, output);
        var json = output.ToString();

        // Assert - control char should be escaped as \u0000
        Assert.Contains("\\u0000", json);
        // Should still be valid JSON
        var doc = JsonDocument.Parse(json.TrimEnd('\n'));
        Assert.NotNull(doc);
    }

    [Fact]
    public void Format_EndsWithNewline()
    {
        // Arrange
        var formatter = new ElasticsearchJsonFormatter();
        var logEvent = CreateLogEvent(LogEventLevel.Information, "Test");
        var output = new StringWriter();

        // Act
        formatter.Format(logEvent, output);
        var result = output.ToString();

        // Assert - should end with \n for NDJSON compatibility
        Assert.EndsWith("\n", result);
        Assert.DoesNotContain("\r\n", result); // Should use Unix newlines
    }

    [Fact]
    public void Format_ThrowsArgumentNullException_WhenLogEventIsNull()
    {
        // Arrange
        var formatter = new ElasticsearchJsonFormatter();
        var output = new StringWriter();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => formatter.Format(null!, output));
    }

    [Fact]
    public void Format_ThrowsArgumentNullException_WhenOutputIsNull()
    {
        // Arrange
        var formatter = new ElasticsearchJsonFormatter();
        var logEvent = CreateLogEvent(LogEventLevel.Information, "Test");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => formatter.Format(logEvent, null!));
    }

    [Fact]
    public void Format_HandlesNullTimestampFieldName()
    {
        // Arrange - null should default to @timestamp
        var formatter = new ElasticsearchJsonFormatter(timestampFieldName: null!);
        var logEvent = CreateLogEvent(LogEventLevel.Information, "Test");
        var output = new StringWriter();

        // Act
        formatter.Format(logEvent, output);
        var json = output.ToString();

        // Assert - should use default @timestamp
        Assert.Contains("\"@timestamp\":", json);
    }

    [Fact]
    public void Format_HandlesNestedExceptionStackTrace()
    {
        // Arrange
        var formatter = new ElasticsearchJsonFormatter();
        Exception? exception = null;
        try
        {
            try
            {
                throw new InvalidOperationException("Inner");
            }
            catch (Exception inner)
            {
                throw new ApplicationException("Outer", inner);
            }
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        var logEvent = CreateLogEventWithException(LogEventLevel.Error, "Error", exception!);
        var output = new StringWriter();

        // Act
        formatter.Format(logEvent, output);
        var json = output.ToString();

        // Assert - should contain both exception messages and be valid JSON
        Assert.Contains("Outer", json);
        Assert.Contains("Inner", json);
        var doc = JsonDocument.Parse(json.TrimEnd('\n'));
        Assert.NotNull(doc);
    }

    // Helper methods

    static LogEvent CreateLogEvent(LogEventLevel level, string messageTemplate, DateTimeOffset? timestamp = null)
    {
        var parser = new MessageTemplateParser();
        var template = parser.Parse(messageTemplate);
        return new LogEvent(
            timestamp ?? DateTimeOffset.UtcNow,
            level,
            null,
            template,
            Enumerable.Empty<LogEventProperty>());
    }

    static LogEvent CreateLogEventWithException(LogEventLevel level, string messageTemplate, Exception exception)
    {
        var parser = new MessageTemplateParser();
        var template = parser.Parse(messageTemplate);
        return new LogEvent(
            DateTimeOffset.UtcNow,
            level,
            exception,
            template,
            Enumerable.Empty<LogEventProperty>());
    }

    static LogEvent CreateLogEventWithProperties(LogEventLevel level, string messageTemplate, IEnumerable<LogEventProperty> properties)
    {
        var parser = new MessageTemplateParser();
        var template = parser.Parse(messageTemplate);
        return new LogEvent(
            DateTimeOffset.UtcNow,
            level,
            null,
            template,
            properties);
    }
}

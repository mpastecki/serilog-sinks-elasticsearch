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

using Serilog.Events;
using Xunit;

namespace Serilog.Sinks.Elasticsearch.Tests.Support;

static class Some
{
    static int Counter;

    public static int Int() => Interlocked.Increment(ref Counter);

    public static string String(string? tag = null) => (tag ?? "") + "__" + Int();

    public static LogEvent LogEvent(
        DateTimeOffset? timestamp = null,
        LogEventLevel level = LogEventLevel.Information,
        string? messageTemplate = null)
    {
        var logger = new LoggerConfiguration().CreateLogger();
        Assert.True(logger.BindMessageTemplate(messageTemplate ?? "Test message " + Int(), Array.Empty<object>(), out var parsedTemplate, out var boundProperties));
        return new LogEvent(
            timestamp ?? DateTimeOffset.UtcNow,
            level,
            null,
            parsedTemplate,
            boundProperties);
    }

    public static LogEvent InformationEvent(DateTimeOffset? timestamp = null)
    {
        return LogEvent(timestamp, LogEventLevel.Information);
    }

    public static LogEvent ErrorEvent(DateTimeOffset? timestamp = null)
    {
        return LogEvent(timestamp, LogEventLevel.Error);
    }
}

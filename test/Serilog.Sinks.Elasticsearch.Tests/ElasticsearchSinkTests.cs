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

using System.Net;
using System.Net.Http;
using Serilog.Events;
using Serilog.Sinks.Elasticsearch.Tests.Support;
using Xunit;

namespace Serilog.Sinks.Elasticsearch.Tests;

public class ElasticsearchSinkTests
{
    [Fact]
    public async Task EmitBatchAsync_SendsCorrectBulkFormat()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{\"errors\":false}");
        var httpClient = new HttpClient(handler);

        var options = new ElasticsearchSinkOptions
        {
            ServerUrl = new Uri("https://localhost:9200"),
            ApiKey = "test-api-key",
            IndexFormat = "logs-{0:yyyy.MM.dd}",
            HttpClientFactory = () => httpClient
        };

        var sink = new ElasticsearchSink(options);

        var timestamp = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var logEvent = Some.LogEvent(timestamp, LogEventLevel.Information, "Test message");

        // Act
        await sink.EmitBatchAsync(new[] { logEvent });

        // Assert
        Assert.Single(handler.RequestBodies);
        var body = handler.RequestBodies[0];

        Assert.Contains("\"_index\":\"logs-2024.01.15\"", body);
        Assert.Contains("\"Level\":\"Information\"", body);
    }

    [Fact]
    public async Task EmitBatchAsync_SetsAuthorizationHeader()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{\"errors\":false}");
        var httpClient = new HttpClient(handler);

        var options = new ElasticsearchSinkOptions
        {
            ServerUrl = new Uri("https://localhost:9200"),
            ApiKey = "my-secret-key",
            HttpClientFactory = () => httpClient
        };

        var sink = new ElasticsearchSink(options);
        var logEvent = Some.InformationEvent();

        // Act
        await sink.EmitBatchAsync(new[] { logEvent });

        // Assert
        var request = handler.Requests[0];
        Assert.Equal("ApiKey", request.Headers.Authorization?.Scheme);
        Assert.Equal("my-secret-key", request.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task EmitBatchAsync_ThrowsOnHttpFailure()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.InternalServerError, "Server error");
        var httpClient = new HttpClient(handler);

        var options = new ElasticsearchSinkOptions
        {
            ServerUrl = new Uri("https://localhost:9200"),
            ApiKey = "test-key",
            HttpClientFactory = () => httpClient
        };

        var sink = new ElasticsearchSink(options);
        var logEvent = Some.ErrorEvent();

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(
            () => sink.EmitBatchAsync(new[] { logEvent }));
    }

    [Fact]
    public async Task EmitBatchAsync_HandlesMultipleEventsInBatch()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{\"errors\":false}");
        var httpClient = new HttpClient(handler);

        var options = new ElasticsearchSinkOptions
        {
            ServerUrl = new Uri("https://localhost:9200"),
            ApiKey = "test-key",
            IndexFormat = "logs-{0:yyyy.MM.dd}",
            HttpClientFactory = () => httpClient
        };

        var sink = new ElasticsearchSink(options);

        var events = new[]
        {
            Some.LogEvent(new DateTimeOffset(2024, 1, 15, 10, 0, 0, TimeSpan.Zero), LogEventLevel.Information, "Event 1"),
            Some.LogEvent(new DateTimeOffset(2024, 1, 16, 10, 0, 0, TimeSpan.Zero), LogEventLevel.Warning, "Event 2"),
        };

        // Act
        await sink.EmitBatchAsync(events);

        // Assert
        var body = handler.RequestBodies[0];
        Assert.Contains("logs-2024.01.15", body);
        Assert.Contains("logs-2024.01.16", body);
        Assert.Contains("\"Level\":\"Information\"", body);
        Assert.Contains("\"Level\":\"Warning\"", body);
    }

    [Fact]
    public async Task EmitBatchAsync_UsesConfiguredTimestampFieldName()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{\"errors\":false}");
        var httpClient = new HttpClient(handler);

        var options = new ElasticsearchSinkOptions
        {
            ServerUrl = new Uri("https://localhost:9200"),
            ApiKey = "test-key",
            TimestampFieldName = "@timestamp",
            HttpClientFactory = () => httpClient
        };

        var sink = new ElasticsearchSink(options);
        var logEvent = Some.InformationEvent();

        // Act
        await sink.EmitBatchAsync(new[] { logEvent });

        // Assert
        var body = handler.RequestBodies[0];
        Assert.Contains("\"@timestamp\":", body);
    }

    [Fact]
    public async Task EmitBatchAsync_IncludesPipelineParameter()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{\"errors\":false}");
        var httpClient = new HttpClient(handler);

        var options = new ElasticsearchSinkOptions
        {
            ServerUrl = new Uri("https://localhost:9200"),
            ApiKey = "test-key",
            Pipeline = "my-pipeline",
            HttpClientFactory = () => httpClient
        };

        var sink = new ElasticsearchSink(options);
        var logEvent = Some.InformationEvent();

        // Act
        await sink.EmitBatchAsync(new[] { logEvent });

        // Assert
        var request = handler.Requests[0];
        Assert.Contains("pipeline=my-pipeline", request.RequestUri?.Query ?? "");
    }

    [Theory]
    [InlineData("logs")]
    [InlineData("logs-{0:yyyy.MM.dd}")]
    [InlineData("myapp-{0:yyyy.MM}")]
    public void IndexFormat_SupportsVariousPatterns(string format)
    {
        // Arrange & Act
        var options = new ElasticsearchSinkOptions
        {
            ServerUrl = new Uri("https://localhost:9200"),
            ApiKey = "test-key",
            IndexFormat = format
        };

        var sink = new ElasticsearchSink(options);

        // Assert - should not throw
        Assert.NotNull(sink);
    }

    [Fact]
    public void Constructor_ThrowsWhenServerUrlMissing()
    {
        var options = new ElasticsearchSinkOptions { ApiKey = "test" };

        Assert.Throws<ArgumentException>(() => new ElasticsearchSink(options));
    }

    [Fact]
    public void Constructor_ThrowsWhenApiKeyMissing()
    {
        var options = new ElasticsearchSinkOptions { ServerUrl = new Uri("https://localhost:9200") };

        Assert.Throws<ArgumentException>(() => new ElasticsearchSink(options));
    }

    [Fact]
    public void Constructor_ThrowsWhenOptionsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new ElasticsearchSink(null!));
    }

    [Fact]
    public async Task EmitBatchAsync_SendsNdjsonContentType()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{\"errors\":false}");
        var httpClient = new HttpClient(handler);

        var options = new ElasticsearchSinkOptions
        {
            ServerUrl = new Uri("https://localhost:9200"),
            ApiKey = "test-key",
            HttpClientFactory = () => httpClient
        };

        var sink = new ElasticsearchSink(options);
        var logEvent = Some.InformationEvent();

        // Act
        await sink.EmitBatchAsync(new[] { logEvent });

        // Assert
        var request = handler.Requests[0];
        Assert.Equal("application/x-ndjson", request.Content?.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task EmitBatchAsync_SkipsEmptyBatch()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{\"errors\":false}");
        var httpClient = new HttpClient(handler);

        var options = new ElasticsearchSinkOptions
        {
            ServerUrl = new Uri("https://localhost:9200"),
            ApiKey = "test-key",
            HttpClientFactory = () => httpClient
        };

        var sink = new ElasticsearchSink(options);

        // Act
        await sink.EmitBatchAsync(Array.Empty<LogEvent>());

        // Assert - no HTTP request should be made
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task EmitBatchAsync_AddsCustomHeaders()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{\"errors\":false}");
        var httpClient = new HttpClient(handler);

        var options = new ElasticsearchSinkOptions
        {
            ServerUrl = new Uri("https://localhost:9200"),
            ApiKey = "test-key",
            CustomHeaders = new Dictionary<string, string>
            {
                ["X-Custom-Header"] = "custom-value"
            },
            HttpClientFactory = () => httpClient
        };

        var sink = new ElasticsearchSink(options);
        var logEvent = Some.InformationEvent();

        // Act
        await sink.EmitBatchAsync(new[] { logEvent });

        // Assert
        var request = handler.Requests[0];
        Assert.True(request.Headers.TryGetValues("X-Custom-Header", out var values));
        Assert.Contains("custom-value", values);
    }
}

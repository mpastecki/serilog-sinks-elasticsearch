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

using Xunit;

namespace Serilog.Sinks.Elasticsearch.Tests;

public class ElasticsearchSinkOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var options = new ElasticsearchSinkOptions();

        Assert.Equal("logs-{0:yyyy.MM.dd}", options.IndexFormat);
        Assert.Equal(TimeSpan.FromSeconds(30), options.RequestTimeout);
        Assert.True(options.RenderMessage);
        Assert.Equal("@timestamp", options.TimestampFieldName);
        Assert.Null(options.ServerUrl);
        Assert.Null(options.ApiKey);
        Assert.Null(options.Pipeline);
        Assert.Null(options.BatchingOptions);
        Assert.Null(options.Formatter);
        Assert.Null(options.HttpClientFactory);
        Assert.Null(options.CustomHeaders);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var serverUrl = new Uri("https://localhost:9200");
        var options = new ElasticsearchSinkOptions
        {
            ServerUrl = serverUrl,
            ApiKey = "my-api-key",
            IndexFormat = "custom-{0:yyyy}",
            RequestTimeout = TimeSpan.FromMinutes(1),
            RenderMessage = false,
            TimestampFieldName = "timestamp",
            Pipeline = "my-pipeline"
        };

        Assert.Equal(serverUrl, options.ServerUrl);
        Assert.Equal("my-api-key", options.ApiKey);
        Assert.Equal("custom-{0:yyyy}", options.IndexFormat);
        Assert.Equal(TimeSpan.FromMinutes(1), options.RequestTimeout);
        Assert.False(options.RenderMessage);
        Assert.Equal("timestamp", options.TimestampFieldName);
        Assert.Equal("my-pipeline", options.Pipeline);
    }

    [Fact]
    public void IndexFormat_ThrowsWhenNull()
    {
        var options = new ElasticsearchSinkOptions();
        Assert.Throws<ArgumentNullException>(() => options.IndexFormat = null!);
    }

    [Fact]
    public void RequestTimeout_ThrowsWhenNonPositive()
    {
        var options = new ElasticsearchSinkOptions();
        Assert.Throws<ArgumentOutOfRangeException>(() => options.RequestTimeout = TimeSpan.Zero);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.RequestTimeout = TimeSpan.FromSeconds(-1));
    }

    [Fact]
    public void TimestampFieldName_ThrowsWhenNullOrEmpty()
    {
        var options = new ElasticsearchSinkOptions();
        Assert.Throws<ArgumentException>(() => options.TimestampFieldName = null!);
        Assert.Throws<ArgumentException>(() => options.TimestampFieldName = "");
        Assert.Throws<ArgumentException>(() => options.TimestampFieldName = "   ");
    }

    [Fact]
    public void Validate_ReturnsErrors_WhenRequiredFieldsMissing()
    {
        var options = new ElasticsearchSinkOptions();
        var errors = options.Validate();

        Assert.Contains(errors, e => e.Contains("ServerUrl"));
        Assert.Contains(errors, e => e.Contains("ApiKey"));
    }

    [Fact]
    public void Validate_ReturnsNoErrors_WhenValid()
    {
        var options = new ElasticsearchSinkOptions
        {
            ServerUrl = new Uri("https://localhost:9200"),
            ApiKey = "test-key"
        };

        var errors = options.Validate();
        Assert.Empty(errors);
    }

    [Fact]
    public void ThrowIfInvalid_ThrowsWhenInvalid()
    {
        var options = new ElasticsearchSinkOptions();
        Assert.Throws<InvalidOperationException>(() => options.ThrowIfInvalid());
    }
}

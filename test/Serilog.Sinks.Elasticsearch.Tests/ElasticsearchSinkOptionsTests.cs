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
}

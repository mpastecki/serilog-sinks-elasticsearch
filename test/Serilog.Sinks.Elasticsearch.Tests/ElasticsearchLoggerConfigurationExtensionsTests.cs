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

public class ElasticsearchLoggerConfigurationExtensionsTests
{
    [Fact]
    public void Elasticsearch_SimpleOverload_CreatesLogger()
    {
        // Act
        var logger = new LoggerConfiguration()
            .WriteTo.Elasticsearch(
                "https://localhost:9200",
                "test-api-key")
            .CreateLogger();

        // Assert
        Assert.NotNull(logger);
        logger.Dispose();
    }

    [Fact]
    public void Elasticsearch_OptionsOverload_CreatesLogger()
    {
        // Act
        var logger = new LoggerConfiguration()
            .WriteTo.Elasticsearch(new ElasticsearchSinkOptions
            {
                ServerUrl = new Uri("https://localhost:9200"),
                ApiKey = "test-api-key",
                IndexFormat = "myapp-{0:yyyy.MM.dd}"
            })
            .CreateLogger();

        // Assert
        Assert.NotNull(logger);
        logger.Dispose();
    }

    [Fact]
    public void Elasticsearch_SimpleOverload_ThrowsWhenServerUrlNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LoggerConfiguration()
                .WriteTo.Elasticsearch(null!, "api-key"));
    }

    [Fact]
    public void Elasticsearch_SimpleOverload_ThrowsWhenApiKeyNullOrEmpty()
    {
        // Null throws ArgumentException (not ArgumentNullException since it's also checked for whitespace)
        Assert.Throws<ArgumentException>(() =>
            new LoggerConfiguration()
                .WriteTo.Elasticsearch("https://localhost:9200", null!));

        // Empty or whitespace also throws ArgumentException
        Assert.Throws<ArgumentException>(() =>
            new LoggerConfiguration()
                .WriteTo.Elasticsearch("https://localhost:9200", ""));
    }

    [Fact]
    public void Elasticsearch_OptionsOverload_ThrowsWhenOptionsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LoggerConfiguration()
                .WriteTo.Elasticsearch((ElasticsearchSinkOptions)null!));
    }
}

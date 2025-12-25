# Serilog.Sinks.Elasticsearch

[![Build Status](https://github.com/mpastecki/serilog-sinks-elasticsearch/workflows/CI/badge.svg)](https://github.com/mpastecki/serilog-sinks-elasticsearch/actions)

A [Serilog](https://serilog.net/) sink that writes log events to [Elasticsearch](https://www.elastic.co/elasticsearch/) 8.x using the Bulk API.

## Installation

This package is not yet published to NuGet. To use it, clone the repository and build from source:

```bash
git clone https://github.com/mpastecki/serilog-sinks-elasticsearch.git
cd serilog-sinks-elasticsearch
dotnet build -c Release
```

Then reference the project directly in your solution, or create a local NuGet package:

```bash
dotnet pack -c Release
```

## Getting Started

Configure the sink in your application:

```csharp
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Elasticsearch(
        serverUrl: "https://my-cluster.es.cloud:443",
        apiKey: "your-base64-encoded-api-key")
    .CreateLogger();

Log.Information("Hello, Elasticsearch!");

Log.CloseAndFlush();
```

## Configuration Options

For more control, use the options overload:

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.Elasticsearch(new ElasticsearchSinkOptions
    {
        ServerUrl = new Uri("https://my-cluster.es.cloud:443"),
        ApiKey = "your-base64-encoded-api-key",
        IndexFormat = "myapp-logs-{0:yyyy.MM.dd}",
        BatchingOptions = new BatchingOptions
        {
            BatchSizeLimit = 500,
            BufferingTimeLimit = TimeSpan.FromSeconds(5)
        },
        Pipeline = "my-ingest-pipeline",
        CustomHeaders = new Dictionary<string, string>
        {
            ["X-Custom-Header"] = "value"
        }
    })
    .CreateLogger();
```

### Available Options

| Option | Description | Default |
|--------|-------------|---------|
| `ServerUrl` | Elasticsearch server URL (required) | - |
| `ApiKey` | API key for authentication (required) | - |
| `IndexFormat` | Index name pattern with date formatting | `logs-{0:yyyy.MM.dd}` |
| `Formatter` | Custom `ITextFormatter` for JSON serialization | `JsonFormatter` |
| `BatchingOptions` | Batching configuration (size, timing, queue) | See below |
| `RequestTimeout` | HTTP request timeout | 30 seconds |
| `TimestampFieldName` | Name of timestamp field in documents | `@timestamp` |
| `Pipeline` | Ingest pipeline name | `null` |
| `CustomHeaders` | Additional HTTP headers | `null` |
| `RenderMessage` | Include rendered message in output | `true` |
| `HttpClientFactory` | Custom HttpClient factory | `null` |

### Batching Options

| Option | Description | Default |
|--------|-------------|---------|
| `BatchSizeLimit` | Maximum events per batch | 1000 |
| `BufferingTimeLimit` | Maximum time between batches | 2 seconds |
| `QueueLimit` | Maximum events in queue | 100,000 |
| `RetryTimeLimit` | Maximum retry duration | 10 minutes |
| `EagerlyEmitFirstEvent` | Send first event immediately | `true` |

## Index Naming

The `IndexFormat` option supports .NET date formatting:

```csharp
// Daily indices (default)
IndexFormat = "logs-{0:yyyy.MM.dd}"      // logs-2025.01.15

// Monthly indices
IndexFormat = "logs-{0:yyyy.MM}"          // logs-2025.01

// Static index name
IndexFormat = "application-logs"          // application-logs

// Application-prefixed
IndexFormat = "myapp-{0:yyyy.MM.dd}"      // myapp-2025.01.15
```

## Authentication

This sink uses Elasticsearch API Key authentication. To create an API key:

1. In Kibana, go to **Stack Management** → **API Keys**
2. Click **Create API key**
3. Configure the key with appropriate privileges
4. Copy the Base64-encoded key

The API key is sent via the `Authorization: ApiKey <key>` header.

## Elasticsearch Document Format

Log events are formatted as JSON using Serilog's `JsonFormatter`:

```json
{
  "@timestamp": "2025-01-15T10:30:00.000+00:00",
  "Level": "Information",
  "MessageTemplate": "User {UserId} logged in",
  "RenderedMessage": "User 12345 logged in",
  "Properties": {
    "UserId": 12345,
    "SourceContext": "MyApp.AuthService"
  }
}
```

## Requirements

- Elasticsearch 8.x
- .NET 6.0+ / .NET Framework 4.6.2+ / .NET Standard 2.0

## License

Apache 2.0 - see [LICENSE](LICENSE) for details.

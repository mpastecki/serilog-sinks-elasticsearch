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
using System.Threading;
using System.Threading.Tasks;

namespace Serilog.Sinks.Elasticsearch.Tests.Support;

/// <summary>
/// A mock HTTP message handler for testing HTTP interactions.
/// </summary>
class MockHttpMessageHandler : HttpMessageHandler
{
    readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;
    readonly List<HttpRequestMessage> _requests = new();

    public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    public MockHttpMessageHandler(HttpStatusCode statusCode, string content = "{}")
        : this(_ => Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content)
        }))
    {
    }

    public IReadOnlyList<HttpRequestMessage> Requests => _requests;

    public List<string> RequestBodies { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        _requests.Add(request);

        if (request.Content is not null)
        {
            var body = await request.Content.ReadAsStringAsync().ConfigureAwait(false);
            RequestBodies.Add(body);
        }

        return await _handler(request).ConfigureAwait(false);
    }
}

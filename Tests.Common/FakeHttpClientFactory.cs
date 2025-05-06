namespace Tests.Common
{
    // simple IHttpClientFactory implementation that returns a preconfigured HttpClient
    public class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;
        public FakeHttpClientFactory(HttpClient client) => _client = client;
        public HttpClient CreateClient(string name) => _client;
    }
}

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net;
using System.Net.Http.Json;
using Tests.Common;

namespace HackerNews.API.IntegrationTests
{
    public class CustomWebApplicationFactory
    : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHttpClientFactory>();
                var fakeHandler = new FakeHttpMessageHandler();

                var ids = new[] { 5, 4, 3, 2, 1 };

                // sample responses used by all tests
                fakeHandler.AddResponse(
                    "https://hacker-news.firebaseio.com/v0/newstories.json",
                    () => new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(ids)
                    });

                // item/{id}
                foreach (var id in ids)
                {
                    fakeHandler.AddResponse(
                        $"https://hacker-news.firebaseio.com/v0/item/{id}.json",
                        () => new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = JsonContent.Create(new { id, url = $"u{id}" })
                        });
                }

                services.AddSingleton<IHttpClientFactory>(_ =>
                    new FakeHttpClientFactory(new HttpClient(fakeHandler)
                    {
                        BaseAddress = new Uri("https://hacker-news.firebaseio.com/v0/")
                    }));
            });
        }
    }
}
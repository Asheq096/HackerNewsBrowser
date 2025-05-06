using HackerNews.API.Services;
using HackerNews.API.DTOs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HackerNews.API.Tests
{
    public class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, Func<HttpResponseMessage>> _responses = new();

        public void AddResponse(string url, Func<HttpResponseMessage> responseFactory) =>
            _responses[url] = responseFactory;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_responses.TryGetValue(request.RequestUri.ToString(), out var responseFactory))
                return Task.FromResult(responseFactory());

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request });
        }
    }

    // simple IHttpClientFactory implementation that returns a preconfigured HttpClient
    public class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;
        public FakeHttpClientFactory(HttpClient client) => _client = client;
        public HttpClient CreateClient(string name) => _client;
    }

    public class HackerNewsServiceTests
    {
        private readonly FakeHttpMessageHandler _handler;
        private readonly HttpClient _httpClient;
        private readonly FakeHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _config;
        private readonly HackerNewsService _service;

        public HackerNewsServiceTests()
        {
            _handler = new FakeHttpMessageHandler();
            _httpClient = new HttpClient(_handler)
            {
                BaseAddress = new Uri("https://hacker-news.firebaseio.com/v0/")
            };

            _httpClientFactory = new FakeHttpClientFactory(_httpClient);

            _cache = new MemoryCache(new MemoryCacheOptions());
            _cache.Remove("newstories");

            var inMemorySettings = new Dictionary<string, string>
            {
                { "HackerNewsApi:BaseUrl", "https://hacker-news.firebaseio.com/v0" },
                { "HackerNewsApi:NewStoriesUrl", "newstories" }
            };
            _config = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            _service = new HackerNewsService(_httpClientFactory, _config, _cache);
        }

        [Fact]
        public async Task FetchNewStoryIdsAsync_ReturnsArray()
        {
            // Arrange
            var ids = new[] { 3, 2, 1 };
            var json = JsonSerializer.Serialize(ids);
            _handler.AddResponse("https://hacker-news.firebaseio.com/v0/newstories.json",
                () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });

            // Act
            var result = await _service.FetchNewStoryIdsAsync();

            // Assert
            Assert.Equal(ids, result);
        }

        [Fact]
        public async Task GetItemAsync_ReturnsDto()
        {
            // Arrange
            var item = new ItemDto { Id = 42, By = "alice", Url = "https://example.com" };
            var json = JsonSerializer.Serialize(item);
            _handler.AddResponse("https://hacker-news.firebaseio.com/v0/item/42.json",
                () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });

            // Act
            var result = await _service.GetItemAsync(42);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(42, result.Id);
            Assert.Equal("alice", result.By);
            Assert.Equal("https://example.com", result.Url);
        }

        [Fact]
        public async Task GetStoriesWithLinksAsync_FiltersAndPaginates()
        {
            // Arrange
            var ids = new[] { 5, 4, 3, 2, 1 };
            _handler.AddResponse("https://hacker-news.firebaseio.com/v0/newstories.json",
                () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(ids)) });

            var items = new Dictionary<int, ItemDto?>
            {
                { 5, new ItemDto { Id = 5, Url = null } },
                { 4, new ItemDto { Id = 4, Url = "u4", Deleted = true } },
                { 3, new ItemDto { Id = 3, Url = "u3" } },
                { 2, new ItemDto { Id = 2, Url = "u2" } },
                { 1, new ItemDto { Id = 1, Url = "u1" } }
            };
            foreach (var kv in items)
            {
                var u = $"https://hacker-news.firebaseio.com/v0/item/{kv.Key}.json";
                var msg = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(kv.Value)) };
                _handler.AddResponse(u, () => msg);
            }

            // Act
            var result = await _service.GetStoriesWithLinksAsync(null, null, null, null, 2);

            // Assert
            Assert.Equal(new[] { 3, 2 }, result.Items.Select(i => i.Id));
            Assert.Equal(5, result.CurrentHead);
            Assert.Equal(5, result.NextHead);
        }

        [Fact]
        public async Task GetStoriesWithLinksAsync_StartAfterIdNotFound_JustBeginsFromTheBeginningAndUpdatesNextHead()
        {
            // Arrange: IDs
            var ids = new[] { 12, 11, 10 };
            _handler.AddResponse("https://hacker-news.firebaseio.com/v0/newstories.json",
                () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(ids)) });

            foreach (var id in ids)
            {
                var item = new ItemDto { Id = id, Url = $"url{id}" };
                _handler.AddResponse($"https://hacker-news.firebaseio.com/v0/item/{id}.json",
                    () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(item)) });
            }

            // Act: startAfterId not in list (e.g. 999)
            var page = await _service.GetStoriesWithLinksAsync(999, 11, 11, null, 2);

            // Assert
            Assert.Equal(new[] { 12 }, page.Items.Select(i => i.Id)); // will not return head again
            Assert.Equal(12, page.CurrentHead); // will update both heads
            Assert.Equal(12, page.NextHead);
        }

        [Fact]
        public async Task GetStoriesWithLinksAsync_PaginationOnMultipleCalls_WorksCorrectly()
        {
            // Arrange
            var storyIds = new[] { 16, 15, 14, 13, 12, 11, 10 };
            _handler.AddResponse("https://hacker-news.firebaseio.com/v0/newstories.json",
                () => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(storyIds))
                });

            foreach (var id in storyIds)
            {
                var item = new ItemDto { Id = id, Url = $"url{id}" };
                _handler.AddResponse($"https://hacker-news.firebaseio.com/v0/item/{id}.json",
                    () => new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(JsonSerializer.Serialize(item))
                    });
            }

            // Act - First call
            var firstPage = await _service.GetStoriesWithLinksAsync(null, null, null, null, 2);

            // Assert - First call
            Assert.Equal(new[] { 16, 15 }, firstPage.Items.Select(i => i.Id));
            Assert.Equal(16, firstPage.CurrentHead);
            Assert.Equal(16, firstPage.NextHead);

            foreach (var id in storyIds)
            {
                var item = new ItemDto { Id = id, Url = $"url{id}" };
                _handler.AddResponse($"https://hacker-news.firebaseio.com/v0/item/{id}.json",
                    () => new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(JsonSerializer.Serialize(item))
                    });
            }

            // Act - Second call
            var secondPage = await _service.GetStoriesWithLinksAsync(firstPage.Items.Min(x => x.Id), firstPage.CurrentHead, firstPage.NextHead, null, 2);

            // Assert - Second call
            Assert.Equal(new[] { 14, 13 }, secondPage.Items.Select(i => i.Id));
            Assert.Equal(16, secondPage.CurrentHead);
            Assert.Equal(16, firstPage.NextHead);
        }

        [Fact]
        public async Task GetStoriesWithLinksAsync_HandlesUpdatedStoryIdsBetweenCalls()
        {
            // Arrange initial story IDs
            var initialIds = new[] { 13, 12, 11, 10 };
            _handler.AddResponse("https://hacker-news.firebaseio.com/v0/newstories.json",
                () => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(initialIds))
                });

            foreach (var id in initialIds)
            {
                var item = new ItemDto { Id = id, Url = $"url{id}" };
                _handler.AddResponse($"https://hacker-news.firebaseio.com/v0/item/{id}.json",
                    () => new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(JsonSerializer.Serialize(item))
                    });
            }

            // Act - First call
            var firstPage = await _service.GetStoriesWithLinksAsync(null, null, null, null, 3);

            // Assert - First call
            Assert.Equal(new[] { 13, 12, 11 }, firstPage.Items.Select(i => i.Id));
            Assert.Equal(13, firstPage.CurrentHead);
            Assert.Equal(13, firstPage.NextHead);

            // update story IDs to simulate new stories being added
            var updatedIds = new[] { 16, 15, 14, 13, 12, 11, 10 };
            _handler.AddResponse("https://hacker-news.firebaseio.com/v0/newstories.json",
                () => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(updatedIds))
                });

            // add responses for new items
            foreach (var id in new[] { 16, 15, 14 })
            {
                var item = new ItemDto { Id = id, Url = $"url{id}" };
                _handler.AddResponse($"https://hacker-news.firebaseio.com/v0/item/{id}.json",
                    () => new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(JsonSerializer.Serialize(item))
                    });
            }

            // remove the cache entry to simulate expiration
            _cache.Remove("newstories");

            // Act
            var secondPage = await _service.GetStoriesWithLinksAsync(firstPage.Items.Min(x => x.Id), firstPage.CurrentHead, firstPage.NextHead, null, 3);

            // Assert - Second call
            Assert.Equal(new[] { 10, 16, 15 }, secondPage.Items.Select(i => i.Id));
            Assert.Equal(13, secondPage.CurrentHead);
            Assert.Equal(16, secondPage.NextHead);
        }

        [Fact]
        public async Task GetStoriesWithLinksAsync_UpdatesNextHeadWhenWrappingAndReturnsProperElements()
        {
            // Arrange: IDs
            var ids = new[] { 9, 8, 7, 6, 5 };
            _handler.AddResponse("https://hacker-news.firebaseio.com/v0/newstories.json",
                () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(ids)) });

            // all items have Url
            foreach (var id in ids)
            {
                var item = new ItemDto { Id = id, Url = $"url{id}" };
                _handler.AddResponse($"https://hacker-news.firebaseio.com/v0/item/{id}.json",
                    () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(item)) });
            }

            // Act
            var result = await _service.GetStoriesWithLinksAsync(6, 7, 7, null, 2);

            // Assert
            Assert.Equal(new[] { 5, 9 }, result.Items.Select(i => i.Id));
            Assert.Equal(7, result.CurrentHead);
            Assert.Equal(9, result.NextHead);
        }

        [Fact]
        public async Task GetStoriesWithLinksAsync_HeadHit_UpdatesBothHeadsAfterOneNewItemComesIn()
        {
            // Arrange
            var ids = new[] { 20, 19, 18, 17, 16 };
            _handler.AddResponse("https://hacker-news.firebaseio.com/v0/newstories.json",
                () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(ids)) });

            foreach (var id in ids)
            {
                var item = new ItemDto { Id = id, Url = $"url{id}" };
                _handler.AddResponse($"https://hacker-news.firebaseio.com/v0/item/{id}.json",
                    () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(item)) });
            }

            // first page to establish initial state
            var firstPage = await _service.GetStoriesWithLinksAsync(null, null, null, null, 2);

            // Assert initial state
            Assert.Equal(new[] { 20, 19 }, firstPage.Items.Select(i => i.Id));
            Assert.Equal(20, firstPage.CurrentHead);
            Assert.Equal(20, firstPage.NextHead);

            var updatedIds = new[] { 21, 20, 19, 18, 17, 16 };
            _handler.AddResponse("https://hacker-news.firebaseio.com/v0/newstories.json",
                () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(updatedIds)) });

            _handler.AddResponse($"https://hacker-news.firebaseio.com/v0/item/{21}.json",
                () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(
                        new ItemDto { Id = 21, Url = $"url21" }
                    )) });

            // remove cache to force refresh
            _cache.Remove("newstories");

            // creating situation where we'll hit the head
            // get a page starting after ID 16, with currentHead=20 and nextHead=20
            var secondPage = await _service.GetStoriesWithLinksAsync(16, 20, 20, null, 2);

            // Assert: We should wrap around once we hit the head (20)
            // page should contain the next items after wrapping
            Assert.Equal(21, secondPage.CurrentHead); // currentHead updates to 21
            Assert.Equal(21, secondPage.NextHead);    // nextHead set to first element (21)
            Assert.Equal(new[] { 21 }, secondPage.Items.Select(i => i.Id));
        }

        [Fact]
        public async Task GetStoriesWithLinksAsync_HeadHit_UpdatesOnlyNextHeadAfterTwoNewItemComesInWithPageSizeTwo()
        {
            // Arrange
            var ids = new[] { 20, 19, 18, 17, 16 };
            _handler.AddResponse("https://hacker-news.firebaseio.com/v0/newstories.json",
                () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(ids)) });

            foreach (var id in ids)
            {
                var item = new ItemDto { Id = id, Url = $"url{id}" };
                _handler.AddResponse($"https://hacker-news.firebaseio.com/v0/item/{id}.json",
                    () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(item)) });
            }

            // first page to establish initial state
            var firstPage = await _service.GetStoriesWithLinksAsync(null, null, null, null, 2);

            // Assert initial state
            Assert.Equal(new[] { 20, 19 }, firstPage.Items.Select(i => i.Id));
            Assert.Equal(20, firstPage.CurrentHead);
            Assert.Equal(20, firstPage.NextHead);

            var updatedIds = new[] { 22, 21, 20, 19, 18, 17, 16 };
            _handler.AddResponse("https://hacker-news.firebaseio.com/v0/newstories.json",
                () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(updatedIds)) });

            foreach (var id in new[] { 22, 21 })
            {
                var item = new ItemDto { Id = id, Url = $"url{id}" };
                _handler.AddResponse($"https://hacker-news.firebaseio.com/v0/item/{id}.json",
                    () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(item)) });
            }

            // remove cache to force refresh
            _cache.Remove("newstories");

            // creating situation where we'll hit the head
            // get a page starting after ID 16, with currentHead=20 and nextHead=20
            var secondPage = await _service.GetStoriesWithLinksAsync(16, 20, 20, null, 2);

            // Assert: We should wrap around once we hit the head (20)
            // page should contain the next items after wrapping
            Assert.Equal(20, secondPage.CurrentHead); // currentHead is unchanged (20)
            Assert.Equal(22, secondPage.NextHead);    // nextHead set to first element (22)
            Assert.Equal(new[] { 22, 21 }, secondPage.Items.Select(i => i.Id));
        }

        [Fact]
        public async Task GetStoriesWithLinksAsync_HeadHit_UpdatesBothHeadsAfterTwoNewItemComesInWithPageSizeThree()
        {
            // Arrange
            var ids = new[] { 20, 19, 18, 17, 16 };
            _handler.AddResponse("https://hacker-news.firebaseio.com/v0/newstories.json",
                () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(ids)) });

            foreach (var id in ids)
            {
                var item = new ItemDto { Id = id, Url = $"url{id}" };
                _handler.AddResponse($"https://hacker-news.firebaseio.com/v0/item/{id}.json",
                    () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(item)) });
            }

            // first page to establish initial state
            var firstPage = await _service.GetStoriesWithLinksAsync(null, null, null, null, 2);

            // Assert initial state
            Assert.Equal(new[] { 20, 19 }, firstPage.Items.Select(i => i.Id));
            Assert.Equal(20, firstPage.CurrentHead);
            Assert.Equal(20, firstPage.NextHead);

            var updatedIds = new[] { 22, 21, 20, 19, 18, 17, 16 };
            _handler.AddResponse("https://hacker-news.firebaseio.com/v0/newstories.json",
                () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(updatedIds)) });

            foreach (var id in new[] { 22, 21 })
            {
                var item = new ItemDto { Id = id, Url = $"url{id}" };
                _handler.AddResponse($"https://hacker-news.firebaseio.com/v0/item/{id}.json",
                    () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(item)) });
            }

            // remove cache to force refresh
            _cache.Remove("newstories");

            // creating situation where we'll hit the head
            // get a page starting after ID 16, with currentHead=20 and nextHead=20
            var secondPage = await _service.GetStoriesWithLinksAsync(16, 20, 20, null, 3);

            // Assert: We should wrap around once we hit the head (20)
            // page should contain the next items after wrapping
            Assert.Equal(22, secondPage.CurrentHead); // currentHead changes to nextHead(22)
            Assert.Equal(22, secondPage.NextHead);    // nextHead set to first element (22)
            Assert.Equal(new[] { 22, 21 }, secondPage.Items.Select(i => i.Id));
        }

        [Fact]
        public async Task GetStoriesWithLinksAsync_HeadFallsOffBeforeReaching_HandlesCorrectly()
        {
            // Arrange: Initial set of IDs
            var initialIds = new[] { 25, 24, 23, 22, 21 };
            _handler.AddResponse("https://hacker-news.firebaseio.com/v0/newstories.json",
                () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(initialIds)) });

            foreach (var id in initialIds)
            {
                var item = new ItemDto { Id = id, Url = $"url{id}" };
                _handler.AddResponse($"https://hacker-news.firebaseio.com/v0/item/{id}.json",
                    () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(item)) });
            }

            // get first page
            var firstPage = await _service.GetStoriesWithLinksAsync(null, null, null, null, 2);
            Assert.Equal(new[] { 25, 24 }, firstPage.Items.Select(i => i.Id));
            Assert.Equal(25, firstPage.CurrentHead);
            Assert.Equal(25, firstPage.NextHead);

            // simulate new stories arriving and old ones falling off
            var updatedIds = new[] { 30, 29, 28, 27, 26 }; // Note: Original head (25) is gone
            _handler.AddResponse("https://hacker-news.firebaseio.com/v0/newstories.json",
                () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(updatedIds)) });

            foreach (var id in updatedIds)
            {
                var item = new ItemDto { Id = id, Url = $"url{id}" };
                _handler.AddResponse($"https://hacker-news.firebaseio.com/v0/item/{id}.json",
                    () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(item)) });
            }

            // remove cache to force refresh
            _cache.Remove("newstories");

            // get next page with startAfterId=24, but the currentHead (25) has fallen off
            var secondPage = await _service.GetStoriesWithLinksAsync(24, 25, 25, null, 2);

            // Assert: should handle the case where head isn't found and proceed correctly
            Assert.Equal(new[] { 30, 29 }, secondPage.Items.Select(i => i.Id));
            Assert.Equal(30, secondPage.CurrentHead); // CurrentHead is updated to new first element
            Assert.Equal(30, secondPage.NextHead);    // NextHead is updated to new first element
        }

        [Fact]
        public async Task GetStoriesWithLinksAsync_AllItemsFiltered_ReturnsEmptyListWithHeads()
        {
            // Arrange: Setup story IDs
            var ids = new[] { 35, 34, 33, 32, 31 };
            _handler.AddResponse("https://hacker-news.firebaseio.com/v0/newstories.json",
                () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(ids)) });

            // set up all items to be filtered out (all deleted or no URLs)
            foreach (var id in ids)
            {
                var item = new ItemDto { Id = id, Url = null, Deleted = id % 2 == 0 }; // alternating deleted and no URL
                _handler.AddResponse($"https://hacker-news.firebaseio.com/v0/item/{id}.json",
                    () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(item)) });
            }

            // Act
            var result = await _service.GetStoriesWithLinksAsync(null, null, null, null, 3);

            // Assert
            Assert.Empty(result.Items);
            Assert.Equal(35, result.CurrentHead); // Should still set the head
            Assert.Equal(35, result.NextHead);    // And the next head
        }

        [Fact]
        public async Task GetStoriesWithLinksAsync_HeadPersistence_MaintainsConsistencyAcrossCalls()
        {
            // Arrange: Setup story IDs
            var ids = new[] { 55, 54, 53, 52, 51, 50 };
            _handler.AddResponse("https://hacker-news.firebaseio.com/v0/newstories.json",
                () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(ids)) });

            foreach (var id in ids)
            {
                var item = new ItemDto { Id = id, Url = $"url{id}" };
                _handler.AddResponse($"https://hacker-news.firebaseio.com/v0/item/{id}.json",
                    () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(item)) });
            }

            // first call to establish heads
            var firstPage = await _service.GetStoriesWithLinksAsync(null, null, null, null, 2);
            Assert.Equal(new[] { 55, 54 }, firstPage.Items.Select(i => i.Id));
            Assert.Equal(55, firstPage.CurrentHead);
            Assert.Equal(55, firstPage.NextHead);

            // verify cache is maintained (don't clear it)
            // make a second call using the previous page's values
            var secondPage = await _service.GetStoriesWithLinksAsync(54, firstPage.CurrentHead, firstPage.NextHead, null, 2);

            // Assert: Should correctly continue from previous position
            Assert.Equal(new[] { 53, 52 }, secondPage.Items.Select(i => i.Id));
            Assert.Equal(55, secondPage.CurrentHead); // Head should remain consistent
            Assert.Equal(55, secondPage.NextHead);    // NextHead should remain consistent

            // third call to verify consistent behavior
            var thirdPage = await _service.GetStoriesWithLinksAsync(52, secondPage.CurrentHead, secondPage.NextHead, null, 2);

            // Assert continued consistency
            Assert.Equal(new[] { 51, 50 }, thirdPage.Items.Select(i => i.Id));
            Assert.Equal(55, thirdPage.CurrentHead);
            Assert.Equal(55, thirdPage.NextHead);
        }

        [Theory]
        [InlineData("U4", new int[] { 4 })] // matches URL
        [InlineData("jDoe", new int[] { 3 })] // matches by
        [InlineData("INTERESTING", new int[] { 5, 2 })] // matches text
        public async Task GetStoriesWithLinksAsync_SearchQueryFiltersCorrectly(string searchQuery, int[] expectedIds)
        {
            // Arrange
            var ids = new[] { 5, 4, 3, 2, 1 };
            _handler.AddResponse("https://hacker-news.firebaseio.com/v0/newstories.json",
                () => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(ids))
                });

            var items = new Dictionary<int, ItemDto?>
            {
                { 5, new ItemDto { Id = 5, Url = "url", Title = "Some interesting title" } },
                { 4, new ItemDto { Id = 4, Url = "u4", Title = "Breaking News" } },
                { 3, new ItemDto { Id = 3, Url = "url", By = "jdoe" } },
                { 2, new ItemDto { Id = 2, Url = "url", Text = "Something interesting here" } },
                { 1, new ItemDto { Id = 1, Url = "url", Title = "No match here" } }
            };
            foreach (var kv in items)
            {
                var url = $"https://hacker-news.firebaseio.com/v0/item/{kv.Key}.json";
                var msg = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(kv.Value))
                };
                _handler.AddResponse(url, () => msg);
            }

            // Act
            var result = await _service.GetStoriesWithLinksAsync(null, null, null, searchQuery, 10);

            // Assert
            Assert.Equal(expectedIds, result.Items.Select(item => item.Id));
            Assert.Equal(5, result.CurrentHead); // Should still set the head
            Assert.Equal(5, result.NextHead);
        }

        [Fact]
        public async Task GetStoriesWithLinksAsync_SearchQuery_NoMatches_ReturnsEmpty()
        {
            // Arrange
            var ids = new[] { 1, 2 };
            _handler.AddResponse("https://hacker-news.firebaseio.com/v0/newstories.json",
                () => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(ids))
                });

            var items = new Dictionary<int, ItemDto?>
            {
                { 1, new ItemDto { Id = 1, Url = "url", Title = "foo" } },
                { 2, new ItemDto { Id = 2, Url = "url", Text = "bar" } }
            };
            foreach (var kv in items)
            {
                var url = $"https://hacker-news.firebaseio.com/v0/item/{kv.Key}.json";
                var msg = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(kv.Value))
                };
                _handler.AddResponse(url, () => msg);
            }

            // Act
            var result = await _service.GetStoriesWithLinksAsync(null, null, null, "notfound", 10);

            // Assert
            Assert.Empty(result.Items);
        }

        [Fact]
        public async Task GetStoriesWithLinksAsync_ReturnsHasMoreStoriesTrueWhenThereAreMoreStories()
        {
            // Arrange: IDs
            var ids = new[] { 9, 8, 7 };
            _handler.AddResponse("https://hacker-news.firebaseio.com/v0/newstories.json",
                () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(ids)) });

            // all items have Url
            foreach (var id in ids)
            {
                var item = new ItemDto { Id = id, Url = $"url{id}" };
                _handler.AddResponse($"https://hacker-news.firebaseio.com/v0/item/{id}.json",
                    () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(item)) });
            }

            // Act
            var result = await _service.GetStoriesWithLinksAsync(null, null, null, null, 2);

            // Assert
            Assert.Equal(new[] { 9, 8 }, result.Items.Select(i => i.Id));
            Assert.Equal(9, result.CurrentHead);
            Assert.Equal(9, result.NextHead);
            Assert.True(result.HasMoreStories);
        }

        [Fact]
        public async Task GetStoriesWithLinksAsync_ReturnsHasMoreStoriesTrueWhenThereAreMoreStoriesWhenWrapping()
        {
            // Arrange: IDs
            var ids = new[] { 9, 8, 7 };
            _handler.AddResponse("https://hacker-news.firebaseio.com/v0/newstories.json",
                () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(ids)) });

            // all items have Url
            foreach (var id in ids)
            {
                var item = new ItemDto { Id = id, Url = $"url{id}" };
                _handler.AddResponse($"https://hacker-news.firebaseio.com/v0/item/{id}.json",
                    () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(item)) });
            }

            // Act
            var result = await _service.GetStoriesWithLinksAsync(null, null, null, null, 2);

            // Assert
            Assert.Equal(new[] { 9, 8 }, result.Items.Select(i => i.Id));
            Assert.Equal(9, result.CurrentHead);
            Assert.Equal(9, result.NextHead);
            Assert.True(result.HasMoreStories);

            // 2nd page with new IDs added
            var updatedIds = new[] { 11, 10, 9, 8, 7 };
            _handler.AddResponse("https://hacker-news.firebaseio.com/v0/newstories.json",
                () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(updatedIds)) });

            foreach (var id in updatedIds)
            {
                var item = new ItemDto { Id = id, Url = $"url{id}" };
                _handler.AddResponse($"https://hacker-news.firebaseio.com/v0/item/{id}.json",
                    () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(item)) });
            }

            // remove cache to force refresh
            _cache.Remove("newstories");

            var secondPage = await _service.GetStoriesWithLinksAsync(result.Items.Last().Id, result.CurrentHead, result.NextHead, null, 2);

            // Assert
            Assert.Equal(new[] { 7, 11 }, secondPage.Items.Select(i => i.Id));
            Assert.Equal(9, secondPage.CurrentHead);
            Assert.Equal(11, secondPage.NextHead); // NextHead is updated to new first element
            Assert.True(secondPage.HasMoreStories);
        }

        [Fact]
        public async Task GetStoriesWithLinksAsync_ReturnsHasMoreStoriesFalseWhenThereAreNoMoreStories()
        {

            // Arrange: IDs
            var ids = new[] { 9, 8, 7 };
            _handler.AddResponse("https://hacker-news.firebaseio.com/v0/newstories.json",
                () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(ids)) });

            // all items have Url
            foreach (var id in ids)
            {
                var item = new ItemDto { Id = id, Url = $"url{id}" };
                _handler.AddResponse($"https://hacker-news.firebaseio.com/v0/item/{id}.json",
                    () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(item)) });
            }

            // Act
            var result = await _service.GetStoriesWithLinksAsync(null, null, null, null, 3);

            // Assert
            Assert.Equal(new[] { 9, 8, 7 }, result.Items.Select(i => i.Id));
            Assert.Equal(9, result.CurrentHead);
            Assert.Equal(9, result.NextHead);
            Assert.False(result.HasMoreStories);
        }

        [Fact]
        public async Task GetStoriesWithLinksAsync_ReturnsHasMoreStoriesFalseWhenThereAreNoMoreStoriesWhenWrapping()
        {
            // Arrange: IDs
            var ids = new[] { 9, 8, 7 };
            _handler.AddResponse("https://hacker-news.firebaseio.com/v0/newstories.json",
                () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(ids)) });

            // all items have Url
            foreach (var id in ids)
            {
                var item = new ItemDto { Id = id, Url = $"url{id}" };
                _handler.AddResponse($"https://hacker-news.firebaseio.com/v0/item/{id}.json",
                    () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(item)) });
            }

            // Act
            var result = await _service.GetStoriesWithLinksAsync(null, null, null, null, 2);

            // Assert
            Assert.Equal(new[] { 9, 8 }, result.Items.Select(i => i.Id));
            Assert.Equal(9, result.CurrentHead);
            Assert.Equal(9, result.NextHead);
            Assert.True(result.HasMoreStories);

            // 2nd page with new IDs added
            var updatedIds = new[] { 10, 9, 8, 7 };
            _handler.AddResponse("https://hacker-news.firebaseio.com/v0/newstories.json",
                () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(updatedIds)) });

            foreach (var id in updatedIds)
            {
                var item = new ItemDto { Id = id, Url = $"url{id}" };
                _handler.AddResponse($"https://hacker-news.firebaseio.com/v0/item/{id}.json",
                    () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(item)) });
            }

            // remove cache to force refresh
            _cache.Remove("newstories");

            var secondPage = await _service.GetStoriesWithLinksAsync(result.Items.Last().Id, result.CurrentHead, result.NextHead, null, 2);

            // Assert
            Assert.Equal(new[] { 7, 10 }, secondPage.Items.Select(i => i.Id));
            Assert.Equal(9, secondPage.CurrentHead);
            Assert.Equal(10, secondPage.NextHead); // NextHead is updated to new first element
            Assert.False(secondPage.HasMoreStories);
        }

        [Fact]
        public async Task GetStoriesWithLinksAsync_ReturnsHasMoreStoriesFalseWhenThereAreNoMoreStoriesWhenWrapping2()
        {
            // Arrange: IDs
            var ids = new[] { 9, 8, 7 };
            _handler.AddResponse("https://hacker-news.firebaseio.com/v0/newstories.json",
                () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(ids)) });

            // all items have Url
            foreach (var id in ids)
            {
                var item = new ItemDto { Id = id, Url = $"url{id}" };
                _handler.AddResponse($"https://hacker-news.firebaseio.com/v0/item/{id}.json",
                    () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(item)) });
            }

            // Act
            var result = await _service.GetStoriesWithLinksAsync(null, null, null, null, 2);

            // Assert
            Assert.Equal(new[] { 9, 8 }, result.Items.Select(i => i.Id));
            Assert.Equal(9, result.CurrentHead);
            Assert.Equal(9, result.NextHead);
            Assert.True(result.HasMoreStories);

            // 2nd page with new IDs added
            var updatedIds = new[] { 11, 10, 9, 8, 7 };
            _handler.AddResponse("https://hacker-news.firebaseio.com/v0/newstories.json",
                () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(updatedIds)) });

            foreach (var id in updatedIds)
            {
                var item = new ItemDto { Id = id, Url = $"url{id}" };
                _handler.AddResponse($"https://hacker-news.firebaseio.com/v0/item/{id}.json",
                    () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(item)) });
            }

            // remove cache to force refresh
            _cache.Remove("newstories");

            // get next page with startAfterId=24, but the currentHead (25) has fallen off
            var secondPage = await _service.GetStoriesWithLinksAsync(result.Items.Last().Id, result.CurrentHead, result.NextHead, null, 2);

            // Assert: should handle the case where head isn't found and proceed correctly
            Assert.Equal(new[] { 7, 11 }, secondPage.Items.Select(i => i.Id));
            Assert.Equal(9, secondPage.CurrentHead);
            Assert.Equal(11, secondPage.NextHead); // NextHead is updated to new first element
            Assert.True(secondPage.HasMoreStories);

            // 3nd page with new IDs added
            var updatedIds2 = new[] { 12, 11, 10, 9, 8, 7 };
            _handler.AddResponse("https://hacker-news.firebaseio.com/v0/newstories.json",
                () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(updatedIds2)) });

            foreach (var id in updatedIds2)
            {
                var item = new ItemDto { Id = id, Url = $"url{id}" };
                _handler.AddResponse($"https://hacker-news.firebaseio.com/v0/item/{id}.json",
                    () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(item)) });
            }

            // remove cache to force refresh
            _cache.Remove("newstories");

            var thirdPage = await _service.GetStoriesWithLinksAsync(secondPage.Items.Last().Id, secondPage.CurrentHead, secondPage.NextHead, null, 2);

            // Assert
            Assert.Equal(new[] { 10, 12 }, thirdPage.Items.Select(i => i.Id));
            Assert.Equal(11, thirdPage.CurrentHead);
            Assert.Equal(12, thirdPage.NextHead);
            Assert.False(thirdPage.HasMoreStories);
        }
    }
}

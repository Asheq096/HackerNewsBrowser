using HackerNews.API.DTOs;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace HackerNews.API.Services
{
    public class HackerNewsService : IHackerNewsService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _baseUrl;
        private readonly string _newStoriesUrl;
        private readonly IMemoryCache _cache;

        public HackerNewsService(IHttpClientFactory httpClientFactory, IConfiguration configuration, IMemoryCache cache)
        {
            _httpClientFactory = httpClientFactory;
            _baseUrl = configuration["HackerNewsApi:BaseUrl"]
                ?? throw new ArgumentNullException(nameof(configuration), "BaseUrl is required"); ;
            _newStoriesUrl = configuration["HackerNewsApi:NewStoriesUrl"]
                ?? throw new ArgumentNullException(nameof(configuration), "NewStoriesUrl is required");
            _cache = cache;
        }

        public async Task<StoriesPageDto> GetStoriesWithLinksAsync(int? startAfterId, int? currentHead, int? nextHead, string? searchQuery, int pageSize = 20)
        {
            int[] allIds = await _cache.GetOrCreateAsync("newstories", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
                return FetchNewStoryIdsAsync();
            }) ?? [];

            if (allIds.Length == 0)
            {
                return new StoriesPageDto { Items = [], CurrentHead = null };
            }

            int index = 0;
            if (startAfterId.HasValue)
            {
                int startAfterIdIndex = Array.IndexOf(allIds, startAfterId.Value);
                if (startAfterIdIndex >= 0)
                    index = (startAfterIdIndex + 1);
                else // index fell off, wrap to the first element
                    nextHead = allIds[0];
                // wrap if at the end
                if (index >= allIds.Length)
                { // wrap but also set the nextHead to index 0
                    index = 0;
                    nextHead = allIds[0];
                }
            }

            var items = new List<ItemDto>();
            int count = 0;
            var hasMoreStories = false;

            while (count < pageSize + 1) // +1 to load an extra story to figure out the value of HasMoreStories
            {
                var id = allIds[index];

                // check if we hit the head OR at last element and we never hit head (new stories coming in pushed it out)
                if (currentHead == allIds[index] || (index == allIds.Length - 1 && currentHead.HasValue && !allIds.Contains(currentHead.Value)))
                {
                    if (count != pageSize) // if false, then we are just setting HasMoreStories, not messing with heads
                    {
                        currentHead = nextHead;
                        nextHead = allIds[0];
                        index = 0; // wrap to the beginning now and start processing new items (if there are, if not will break out below)
                        id = allIds[index];
                    }
                    if (currentHead == allIds[index]) // there are no new items, we basically hit the head already
                        break;
                }

                if (!currentHead.HasValue || (currentHead.HasValue && !allIds.Contains(currentHead.Value)))
                    currentHead = allIds[0];
                if (!nextHead.HasValue || (nextHead.HasValue && !allIds.Contains(nextHead.Value)))
                    nextHead = allIds[0];

                var item = await _cache.GetOrCreateAsync($"story:{id}", entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                    return GetItemAsync(id);
                });
                if (
                    item != null && 
                    !(item.Deleted ?? false) && 
                    !string.IsNullOrWhiteSpace(item.Url) &&
                    (
                        string.IsNullOrWhiteSpace(searchQuery) ||
                        (item.Title?.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (item.By?.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (item.Text?.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (item.Url?.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ?? false)
                    )
                )
                {
                    if (count == pageSize) // last item, don't add, just set HasMoreStories = true
                        hasMoreStories = true;
                    else
                        items.Add(item);

                    count++;
                }

                // move to next index
                index++;
                if (index >= allIds.Length)
                { // wrap but also set the nextHead to index 0
                    index = 0;
                    nextHead = allIds[0];
                }
            }

            return new StoriesPageDto
            {
                Items = items,
                CurrentHead = currentHead,
                NextHead = nextHead,
                HasMoreStories = hasMoreStories
            };
        }

        public async Task<int[]> FetchNewStoryIdsAsync()
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"{_baseUrl}/{_newStoriesUrl}.json";
            using var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            return await JsonSerializer.DeserializeAsync<int[]>(stream) ?? [];
        }

        public async Task<ItemDto?> GetItemAsync(int id)
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"{_baseUrl}/item/{id}.json";
            using var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            return await JsonSerializer.DeserializeAsync<ItemDto>(stream);
        }
    }
}
using HackerNews.API.DTOs;

namespace HackerNews.API.Services
{
    public interface IHackerNewsService
    {
        public Task<StoriesPageDto> GetStoriesWithLinksAsync(int? startAfterId, int? currentHead, int? nextHead, string? searchQuery, int pageSize = 20);
    }
}
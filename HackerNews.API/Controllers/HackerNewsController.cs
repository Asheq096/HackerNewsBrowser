using HackerNews.API.DTOs;
using HackerNews.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace HackerNews.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HackerNewsController : Controller
    {
        private IHackerNewsService _hackerNewsService;
        public HackerNewsController(IHackerNewsService hackerNewsService)
        { 
            _hackerNewsService = hackerNewsService;
        }

        [HttpGet("GetStoriesWithLinks")]
        public async Task<StoriesPageDto> GetStoriesWithLinksAsync(int? startAfterId, int? currentHead, int? nextHead, string? searchQuery, int pageSize = 20)
        {
            return await _hackerNewsService.GetStoriesWithLinksAsync(startAfterId, currentHead, nextHead, searchQuery, pageSize);
        }
    }
}

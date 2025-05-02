namespace HackerNews.API.DTOs
{
    public class StoriesPageDto
    {
        public List<ItemDto> Items { get; set; } = new();
        public int? CurrentHead { get; set; }  // the ID where we stop. It's the "head" of the current sliding window
        public int? NextHead { get; set; }  // tracks the next head when we eventually wrap around and encounter newer stories
    }
}

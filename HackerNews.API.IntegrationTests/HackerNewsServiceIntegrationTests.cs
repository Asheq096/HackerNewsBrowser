using FluentAssertions;
using HackerNews.API.DTOs;
using System.Net.Http.Json;
using System.Text.Json;

namespace HackerNews.API.IntegrationTests
{
    [CollectionDefinition("API")]
    public class ApiCollection : ICollectionFixture<CustomWebApplicationFactory> { }

    [Collection("API")]
    public class HackerNewsServiceIntegrationTests
    {
        private readonly HttpClient _client;

        public HackerNewsServiceIntegrationTests(CustomWebApplicationFactory factory)
            => _client = factory.CreateClient();

        [Fact]
        public async Task GetStories_FirstPage_ReturnsExpectedItems()
        {
            // Arrange: nothing—Fake handler already primed in factory

            // Act
            var response = await _client.GetAsync("/api/HackerNews/GetStoriesWithLinks?pageSize=2");
            response.EnsureSuccessStatusCode();

            // Assert
            var payload = JsonSerializer.Deserialize<StoriesPageDto>(
                await response.Content.ReadAsStringAsync(),
                new JsonSerializerOptions(JsonSerializerDefaults.Web));

            payload!.Items.Select(i => i.Id)
                    .Should().Equal(5, 4);
            payload.CurrentHead.Should().Be(5);
            payload.NextHead.Should().Be(5);
        }

        [Fact]
        public async Task Pagination_WorksAcrossMultipleCalls()
        {
            // 1st request
            var first = await _client.GetFromJsonAsync<StoriesPageDto>("/api/HackerNews/GetStoriesWithLinks?pageSize=2");
            // 2nd request uses query parameters returned from first call
            var url = $"/api/HackerNews/GetStoriesWithLinks?startAfterId={first!.Items.Min(i => i.Id)}" +
                        $"&currentHead={first.CurrentHead}&nextHead={first.NextHead}&pageSize=2";

            var second = await _client.GetFromJsonAsync<StoriesPageDto>(url);

            second!.Items.Select(i => i.Id).Should().Equal(3, 2);
            second.CurrentHead.Should().Be(5);
            second.NextHead.Should().Be(5);
        }
    }

}

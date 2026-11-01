using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace SampleApi.Tests;

// Отдельный сервер на каждый случай: тесты не зависят от чужих заметок.
public class SmokeTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory = CreateFactory();

    private static WebApplicationFactory<Program> CreateFactory(string? label = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["APP_LABEL"] = label
                })));

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Health_Returns_Ok()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        // S08_BREAK_POINT: для эксперимента замените только OK на NotFound.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthDto>();
        Assert.NotNull(body);
        Assert.Equal("ok", body.Status);
    }

    [Fact]
    public async Task Info_Uses_Default_Label()
    {
        using var client = _factory.CreateClient();
        var info = await client.GetFromJsonAsync<InfoDto>("/info");
        Assert.NotNull(info);
        Assert.Equal("s08-local", info.Label);
    }

    [Fact]
    public async Task Info_Uses_Configured_Label()
    {
        using var configured = CreateFactory("s08-test");
        using var client = configured.CreateClient();
        var info = await client.GetFromJsonAsync<InfoDto>("/info");
        Assert.NotNull(info);
        Assert.Equal("s08-test", info.Label);
    }

    [Fact]
    public async Task New_Store_Is_Empty()
    {
        using var client = _factory.CreateClient();
        var list = await client.GetFromJsonAsync<List<Note>>("/notes");
        Assert.NotNull(list);
        Assert.Empty(list);
    }

    [Fact]
    public async Task Post_Then_Get_Note_Roundtrips()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/notes", new { text = "  hello  " });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<Note>();
        Assert.NotNull(created);
        Assert.Equal("hello", created.Text);
        Assert.NotNull(response.Headers.Location);
        Assert.Equal(created, await client.GetFromJsonAsync<Note>(response.Headers.Location));
        var list = await client.GetFromJsonAsync<List<Note>>("/notes");
        Assert.NotNull(list);
        Assert.Equal(created, Assert.Single(list));
    }

    [Fact]
    public async Task Post_Empty_Text_Is_Rejected()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/notes", new { text = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var list = await client.GetFromJsonAsync<List<Note>>("/notes");
        Assert.NotNull(list);
        Assert.Empty(list);
    }

    [Fact]
    public async Task Post_Whitespace_Is_Rejected()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/notes", new { text = "   " });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Two_Notes_Have_Different_Ids()
    {
        using var client = _factory.CreateClient();
        var first = await (await client.PostAsJsonAsync("/notes", new { text = "first" })).Content.ReadFromJsonAsync<Note>();
        var second = await (await client.PostAsJsonAsync("/notes", new { text = "second" })).Content.ReadFromJsonAsync<Note>();
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first.Id, second.Id);
        var list = await client.GetFromJsonAsync<List<Note>>("/notes");
        Assert.NotNull(list);
        Assert.Equal(2, list.Count);
    }

    private record HealthDto(string Status);
    private record InfoDto(string Label);
}

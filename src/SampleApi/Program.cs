using System.Collections.Concurrent;

// Самопроверка контейнера: тот же бинарный файл делает HTTP-запрос к API.
// В runtime-образ не нужно дополнительно устанавливать curl.
if (args.Length == 1 && args[0] == "--healthcheck")
{
    try
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var url = Environment.GetEnvironmentVariable("S08_HEALTH_URL")
            ?? "http://127.0.0.1:8080/health";
        using var response = await client.GetAsync(url);
        Environment.ExitCode = response.IsSuccessStatusCode ? 0 : 1;
    }
    catch (HttpRequestException) { Environment.ExitCode = 1; }
    catch (TaskCanceledException) { Environment.ExitCode = 1; }
    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<NoteStore>();
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// APP_LABEL — публичная настройка, не секрет. Она реально меняет ответ.
app.MapGet("/info", (IConfiguration configuration) =>
    Results.Ok(new { label = configuration["APP_LABEL"] ?? "s08-local" }));

app.MapGet("/notes", (NoteStore store) => Results.Ok(store.All()));

app.MapGet("/notes/{id:int}", (int id, NoteStore store) =>
    store.Find(id) is Note note ? Results.Ok(note) : Results.NotFound());

app.MapPost("/notes", (NoteInput input, NoteStore store) =>
{
    if (string.IsNullOrWhiteSpace(input.Text))
        return Results.BadRequest(new { error = "text is required" });

    var note = store.Add(input.Text.Trim());
    return Results.Created($"/notes/{note.Id}", note);
});

await app.RunAsync();

public record Note(int Id, string Text);
public record NoteInput(string? Text);

// Учебные данные только в памяти. База данных и внешние секреты не нужны.
public sealed class NoteStore
{
    private readonly ConcurrentDictionary<int, Note> _notes = new();
    private int _nextId;
    public Note[] All() => _notes.Values.OrderBy(note => note.Id).ToArray();
    public Note? Find(int id) => _notes.GetValueOrDefault(id);
    public Note Add(string text)
    {
        var note = new Note(Interlocked.Increment(ref _nextId), text);
        _notes[note.Id] = note;
        return note;
    }
}

public partial class Program;

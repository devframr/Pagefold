using System.IO;
using System.Text.Json;

namespace Pagefold.App;

public sealed class AppState
{
    public List<string> LibraryFolders { get; set; } = [];

    public List<string> LibraryFiles { get; set; } = [];

    public List<string> RecentFiles { get; set; } = [];

    public List<BookmarkState> Bookmarks { get; set; } = [];

    public Dictionary<string, int> LastPages { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public PagefoldUserSettings Settings { get; set; } = new();
}

public sealed class BookmarkState
{
    public string DocumentPath { get; set; } = string.Empty;

    public int PageIndex { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}

public sealed class PagefoldUserSettings
{
    public string DefaultMode { get; set; } = "Single Page";

    public bool ResumeLastPage { get; set; } = true;

    public bool RememberWindowSize { get; set; } = true;

    public bool WheelZooms { get; set; }

    public int CacheRadius { get; set; } = 2;

    public bool PreloadLargeBooks { get; set; } = true;

    public double WindowWidth { get; set; } = 1320;

    public double WindowHeight { get; set; } = 820;
}

public sealed record DocumentListItem(string Path)
{
    public string DisplayName => System.IO.Path.GetFileNameWithoutExtension(Path);

    public override string ToString() => DisplayName;
}

public static class AppStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string AppDataFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Pagefold");

    public static string StatePath { get; } = Path.Combine(AppDataFolder, "state.json");

    public static AppState Load()
    {
        try
        {
            if (!File.Exists(StatePath))
            {
                return new AppState();
            }

            var json = File.ReadAllText(StatePath);
            return JsonSerializer.Deserialize<AppState>(json, JsonOptions) ?? new AppState();
        }
        catch
        {
            return new AppState();
        }
    }

    public static void Save(AppState state)
    {
        Directory.CreateDirectory(AppDataFolder);
        File.WriteAllText(StatePath, JsonSerializer.Serialize(state, JsonOptions));
    }
}

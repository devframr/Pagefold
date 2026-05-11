using Pagefold.Core.Documents;

namespace Pagefold.Core.Services;

public sealed class LibraryScanner
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cbz",
        ".cbr",
        ".cb7",
        ".cbt",
        ".pdf"
    };

    public Task<IReadOnlyList<string>> ScanAsync(string folder, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        return Task.Run<IReadOnlyList<string>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)
                .Where(file => SupportedExtensions.Contains(Path.GetExtension(file)))
                .OrderBy(file => file, NaturalPathComparer.Instance)
                .ToArray();
        }, cancellationToken);
    }

    public static bool IsSupported(ComicBookFormat format) => format is not ComicBookFormat.Unknown;
}

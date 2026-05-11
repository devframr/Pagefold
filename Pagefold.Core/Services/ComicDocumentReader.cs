using Pagefold.Core.Documents;
using SharpCompress.Readers;

namespace Pagefold.Core.Services;

public sealed class ComicDocumentReader : IComicDocumentReader
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".gif",
        ".bmp",
        ".webp",
        ".tif",
        ".tiff"
    };

    public async Task<ComicDocument> OpenAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var format = FormatDetector.Detect(path);
        if (format is ComicBookFormat.Unknown)
        {
            throw new NotSupportedException($"Unsupported document format: {Path.GetExtension(path)}");
        }

        if (format is ComicBookFormat.Pdf)
        {
            return new ComicDocument(path, format, Array.Empty<ComicPage>(), ComicMetadata.Empty);
        }

        return await Task.Run(() => OpenArchive(path, format), cancellationToken);
    }

    public Task<Stream> OpenPageStreamAsync(
        ComicDocument document,
        ComicPage page,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(page);

        if (document.Format is ComicBookFormat.Pdf)
        {
            throw new NotSupportedException("PDF rendering adapter is not wired yet.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<Stream>(ReadEntryToMemory(document.Path, page.Key));
    }

    private static ComicDocument OpenArchive(string path, ComicBookFormat format)
    {
        var entries = ReadEntryCatalog(path);

        var metadata = ReadMetadata(entries);

        var pages = entries
            .Where(e => ImageExtensions.Contains(Path.GetExtension(e.Key)))
            .OrderBy(e => e.Key, NaturalPathComparer.Instance)
            .Select((e, index) => new ComicPage(
                index,
                e.Key,
                Path.GetFileName(e.Key),
                e.CompressedSize,
                e.Size))
            .ToArray();

        return new ComicDocument(path, format, pages, metadata);
    }

    private static ComicMetadata ReadMetadata(IReadOnlyList<ArchiveEntryInfo> entries)
    {
        var comicInfo = entries.FirstOrDefault(e =>
            string.Equals(Path.GetFileName(e.Key), "ComicInfo.xml", StringComparison.OrdinalIgnoreCase));

        if (comicInfo is null)
        {
            return ComicMetadata.Empty;
        }

        using var stream = ReadEntryToMemory(comicInfo.ArchivePath, comicInfo.Key);
        return ComicInfoReader.Read(stream);
    }

    private static IReadOnlyList<ArchiveEntryInfo> ReadEntryCatalog(string path)
    {
        using var file = File.OpenRead(path);
        using var reader = ReaderFactory.OpenReader(file, new ReaderOptions());
        var entries = new List<ArchiveEntryInfo>();

        while (reader.MoveToNextEntry())
        {
            var entry = reader.Entry;
            if (entry.IsDirectory || string.IsNullOrWhiteSpace(entry.Key) || IsSystemEntry(entry.Key))
            {
                continue;
            }

            entries.Add(new ArchiveEntryInfo(
                path,
                entry.Key,
                entry.CompressedSize,
                entry.Size));
        }

        return entries;
    }

    private static MemoryStream ReadEntryToMemory(string path, string key)
    {
        using var file = File.OpenRead(path);
        using var reader = ReaderFactory.OpenReader(file, new ReaderOptions());

        while (reader.MoveToNextEntry())
        {
            var entry = reader.Entry;
            if (entry.IsDirectory || entry.Key != key)
            {
                continue;
            }

            using var entryStream = reader.OpenEntryStream();
            var output = new MemoryStream();
            entryStream.CopyTo(output);
            output.Position = 0;
            return output;
        }

        throw new FileNotFoundException($"Page entry was not found in archive: {key}");
    }

    private static bool IsSystemEntry(string key)
    {
        return key.StartsWith("__MACOSX/", StringComparison.OrdinalIgnoreCase)
            || key.EndsWith(".DS_Store", StringComparison.OrdinalIgnoreCase)
            || key.EndsWith("Thumbs.db", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record ArchiveEntryInfo(
        string ArchivePath,
        string Key,
        long CompressedSize,
        long Size);
}

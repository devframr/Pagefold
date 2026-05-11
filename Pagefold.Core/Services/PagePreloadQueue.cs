using System.Collections.Concurrent;
using Pagefold.Core.Documents;

namespace Pagefold.Core.Services;

public sealed class PagePreloadQueue
{
    private readonly IComicDocumentReader _reader;
    private readonly ConcurrentDictionary<string, byte[]> _cache = new();

    public PagePreloadQueue(IComicDocumentReader reader)
    {
        _reader = reader;
    }

    public bool TryGet(ComicDocument document, ComicPage page, out byte[] bytes)
    {
        return _cache.TryGetValue(CacheKey(document, page), out bytes!);
    }

    public async Task PreloadAroundAsync(
        ComicDocument document,
        int pageIndex,
        int radius,
        CancellationToken cancellationToken = default)
    {
        var start = Math.Max(0, pageIndex - radius);
        var end = Math.Min(document.Pages.Count - 1, pageIndex + radius);

        for (var i = start; i <= end; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var page = document.Pages[i];
            var key = CacheKey(document, page);
            if (_cache.ContainsKey(key))
            {
                continue;
            }

            await using var stream = await _reader.OpenPageStreamAsync(document, page, cancellationToken);
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken);
            _cache[key] = memory.ToArray();
        }
    }

    private static string CacheKey(ComicDocument document, ComicPage page) => $"{document.Path}|{page.Key}";
}

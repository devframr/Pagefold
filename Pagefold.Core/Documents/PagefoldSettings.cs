namespace Pagefold.Core.Documents;

public sealed record PagefoldSettings(
    ReadingMode ReadingMode,
    int PageCacheRadius,
    bool PreloadLargeBooks,
    bool RememberLastPage)
{
    public static PagefoldSettings Default { get; } = new(ReadingMode.SinglePage, 2, true, true);
}

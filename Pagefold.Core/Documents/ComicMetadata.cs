namespace Pagefold.Core.Documents;

public sealed record ComicMetadata(
    string? Title,
    string? Series,
    string? Number,
    string? Writer,
    string? Publisher,
    bool MangaMode)
{
    public static ComicMetadata Empty { get; } = new(null, null, null, null, null, false);
}

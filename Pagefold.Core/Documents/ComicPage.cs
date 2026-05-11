namespace Pagefold.Core.Documents;

public sealed record ComicPage(
    int Index,
    string Key,
    string DisplayName,
    long? CompressedSize,
    long? UncompressedSize);

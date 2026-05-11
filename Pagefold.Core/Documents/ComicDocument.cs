namespace Pagefold.Core.Documents;

public sealed record ComicDocument(
    string Path,
    ComicBookFormat Format,
    IReadOnlyList<ComicPage> Pages,
    ComicMetadata Metadata);

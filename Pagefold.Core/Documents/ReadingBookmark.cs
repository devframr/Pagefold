namespace Pagefold.Core.Documents;

public sealed record ReadingBookmark(
    string DocumentPath,
    int PageIndex,
    string Note,
    DateTimeOffset CreatedAt);

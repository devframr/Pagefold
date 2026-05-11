namespace Pagefold.Core.Documents;

public sealed record ReadingProgress(
    string DocumentPath,
    int PageIndex,
    int PageCount,
    DateTimeOffset LastOpenedAt);

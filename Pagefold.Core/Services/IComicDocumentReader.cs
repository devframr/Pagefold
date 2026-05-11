using Pagefold.Core.Documents;

namespace Pagefold.Core.Services;

public interface IComicDocumentReader
{
    Task<ComicDocument> OpenAsync(string path, CancellationToken cancellationToken = default);

    Task<Stream> OpenPageStreamAsync(
        ComicDocument document,
        ComicPage page,
        CancellationToken cancellationToken = default);
}

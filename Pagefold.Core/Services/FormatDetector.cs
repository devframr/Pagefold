using Pagefold.Core.Documents;

namespace Pagefold.Core.Services;

public static class FormatDetector
{
    public static ComicBookFormat Detect(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".cbz" => ComicBookFormat.Cbz,
            ".cbr" => ComicBookFormat.Cbr,
            ".cb7" => ComicBookFormat.Cb7,
            ".cbt" => ComicBookFormat.Cbt,
            ".pdf" => ComicBookFormat.Pdf,
            _ => ComicBookFormat.Unknown
        };
    }
}

using Pagefold.Core.Documents;
using Pagefold.Core.Services;

namespace Pagefold.Tests;

public sealed class FormatDetectorTests
{
    [Theory]
    [InlineData("book.cbz", ComicBookFormat.Cbz)]
    [InlineData("book.cbr", ComicBookFormat.Cbr)]
    [InlineData("book.cb7", ComicBookFormat.Cb7)]
    [InlineData("book.cbt", ComicBookFormat.Cbt)]
    [InlineData("book.pdf", ComicBookFormat.Pdf)]
    public void DetectsSupportedFormats(string path, ComicBookFormat expected)
    {
        Assert.Equal(expected, FormatDetector.Detect(path));
    }
}

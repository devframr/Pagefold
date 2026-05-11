using Pagefold.Core.Services;

namespace Pagefold.Tests;

public sealed class NaturalPathComparerTests
{
    [Fact]
    public void SortsComicPagesByNumberRuns()
    {
        var pages = new[] { "page10.jpg", "page2.jpg", "page1.jpg", "page001.jpg" };

        Array.Sort(pages, NaturalPathComparer.Instance);

        Assert.Equal(new[] { "page1.jpg", "page001.jpg", "page2.jpg", "page10.jpg" }, pages);
    }

    [Fact]
    public void SortsNestedArchivePaths()
    {
        var pages = new[] { "Issue/Page 11.png", "Issue/Page 2.png", "Issue/Page 01.png" };

        Array.Sort(pages, NaturalPathComparer.Instance);

        Assert.Equal(new[] { "Issue/Page 01.png", "Issue/Page 2.png", "Issue/Page 11.png" }, pages);
    }
}

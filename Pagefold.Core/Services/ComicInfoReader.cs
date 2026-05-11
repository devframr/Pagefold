using System.Xml.Linq;
using Pagefold.Core.Documents;

namespace Pagefold.Core.Services;

public static class ComicInfoReader
{
    public static ComicMetadata Read(Stream stream)
    {
        var document = XDocument.Load(stream);
        var root = document.Root;

        if (root is null)
        {
            return ComicMetadata.Empty;
        }

        var manga = string.Equals(Value(root, "Manga"), "Yes", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Value(root, "Manga"), "Manga", StringComparison.OrdinalIgnoreCase);

        return new ComicMetadata(
            Value(root, "Title"),
            Value(root, "Series"),
            Value(root, "Number"),
            Value(root, "Writer"),
            Value(root, "Publisher"),
            manga);
    }

    private static string? Value(XElement root, string name)
    {
        var value = root.Element(name)?.Value.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

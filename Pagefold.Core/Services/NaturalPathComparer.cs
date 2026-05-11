using System.Collections;

namespace Pagefold.Core.Services;

public sealed class NaturalPathComparer : IComparer<string>, IComparer
{
    public static NaturalPathComparer Instance { get; } = new();

    public int Compare(object? x, object? y) => Compare(x?.ToString(), y?.ToString());

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        var xi = 0;
        var yi = 0;

        while (xi < x.Length && yi < y.Length)
        {
            var xc = x[xi];
            var yc = y[yi];

            if (char.IsDigit(xc) && char.IsDigit(yc))
            {
                var numberCompare = CompareNumberRun(x, ref xi, y, ref yi);
                if (numberCompare != 0)
                {
                    return numberCompare;
                }

                continue;
            }

            var charCompare = char.ToUpperInvariant(xc).CompareTo(char.ToUpperInvariant(yc));
            if (charCompare != 0)
            {
                return charCompare;
            }

            xi++;
            yi++;
        }

        return x.Length.CompareTo(y.Length);
    }

    private static int CompareNumberRun(string x, ref int xi, string y, ref int yi)
    {
        var xStart = xi;
        var yStart = yi;

        while (xi < x.Length && x[xi] == '0')
        {
            xi++;
        }

        while (yi < y.Length && y[yi] == '0')
        {
            yi++;
        }

        var xDigits = xi;
        var yDigits = yi;

        while (xi < x.Length && char.IsDigit(x[xi]))
        {
            xi++;
        }

        while (yi < y.Length && char.IsDigit(y[yi]))
        {
            yi++;
        }

        var xLength = xi - xDigits;
        var yLength = yi - yDigits;

        if (xLength != yLength)
        {
            return xLength.CompareTo(yLength);
        }

        for (var i = 0; i < xLength; i++)
        {
            var compare = x[xDigits + i].CompareTo(y[yDigits + i]);
            if (compare != 0)
            {
                return compare;
            }
        }

        return (xi - xStart).CompareTo(yi - yStart);
    }
}

namespace Vipi.Ui;

/// <summary>Kind of a diff line: unchanged, added in the target, or removed from the source.</summary>
public enum DiffKind { Equal, Added, Removed }

/// <summary>One line of a diff (its kind and the line text, without trailing newline).</summary>
/// <param name="Kind">Whether the line is equal, added or removed.</param>
/// <param name="Text">The line text (line ending already trimmed for display).</param>
public readonly record struct DiffLine(DiffKind Kind, string Text);

/// <summary>
/// Minimal line-based diff (classic LCS) used only for the preview. Inputs are the verbatim
/// <c>CprSection.Lines</c>; line endings are stripped in the produced <see cref="DiffLine.Text"/>.
/// Profiles are small text files, so the O(n*m) table is fine here.
/// </summary>
public static class LineDiff
{
    /// <summary>Diff <paramref name="from"/> (source) into <paramref name="to"/> (destination).</summary>
    public static IReadOnlyList<DiffLine> Diff(IEnumerable<string> from, IEnumerable<string> to)
    {
        var a = from.Select(Normalize).ToArray();
        var b = to.Select(Normalize).ToArray();
        int n = a.Length, m = b.Length;

        // LCS length table.
        var lcs = new int[n + 1, m + 1];
        for (int i = n - 1; i >= 0; i--)
            for (int j = m - 1; j >= 0; j--)
                lcs[i, j] = a[i] == b[j]
                    ? lcs[i + 1, j + 1] + 1
                    : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);

        var result = new List<DiffLine>(Math.Max(n, m));
        int x = 0, y = 0;
        while (x < n && y < m)
        {
            if (a[x] == b[y])
            {
                result.Add(new DiffLine(DiffKind.Equal, a[x]));
                x++; y++;
            }
            else if (lcs[x + 1, y] >= lcs[x, y + 1])
            {
                result.Add(new DiffLine(DiffKind.Removed, a[x]));
                x++;
            }
            else
            {
                result.Add(new DiffLine(DiffKind.Added, b[y]));
                y++;
            }
        }
        while (x < n) result.Add(new DiffLine(DiffKind.Removed, a[x++]));
        while (y < m) result.Add(new DiffLine(DiffKind.Added, b[y++]));
        return result;
    }

    private static string Normalize(string line) => line.TrimEnd('\r', '\n');
}

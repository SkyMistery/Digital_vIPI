namespace Vipi.Ui;

/// <summary>Kind of a diff line: unchanged, added in the target, or removed from the source.</summary>
public enum DiffKind { Equal, Added, Removed }

/// <summary>One line of a diff (its kind and the line text, without trailing newline).</summary>
/// <param name="Kind">Whether the line is equal, added or removed.</param>
/// <param name="Text">The line text (line ending already trimmed for display).</param>
internal readonly record struct DiffLine(DiffKind Kind, string Text);

/// <summary>
/// Minimal line-based diff used only for the preview. Inputs are the verbatim <c>CprSection.Lines</c>; line
/// endings are stripped in the produced <see cref="DiffLine.Text"/>.
///
/// <para>🔴 U-001 (revisione totale 3): the page that calls this is anonymous, so the cost must be bounded no
/// matter what the files contain. Until 27 September 2026 this was a full LCS table, n·m ints: two sections of
/// 5,000 different lines allocated 100 MB per render, per destination. Now the common head and tail are
/// stripped first (a real edit touches a few lines in the middle), and the LCS table is built only when the
/// middle fits in <see cref="MaxCelle"/> cells. Beyond that the middle is shown as one replaced block: still
/// a correct diff, only less minimal — and a section that large is not a real Aurora profile anyway.</para>
/// </summary>
internal static class LineDiff
{
    /// <summary>Largest LCS table (cells) built for the middle part: 250k ints = 1 MB.</summary>
    internal const int MaxCelle = 250_000;

    /// <summary>Diff <paramref name="from"/> (source) into <paramref name="to"/> (destination).</summary>
    public static IReadOnlyList<DiffLine> Diff(IEnumerable<string> from, IEnumerable<string> to)
    {
        var a = from.Select(Normalize).ToArray();
        var b = to.Select(Normalize).ToArray();

        // Common head and tail: equal lines, no table needed.
        int testa = 0;
        while (testa < a.Length && testa < b.Length && a[testa] == b[testa]) testa++;
        int coda = 0;
        while (coda < a.Length - testa && coda < b.Length - testa
               && a[a.Length - 1 - coda] == b[b.Length - 1 - coda]) coda++;

        var result = new List<DiffLine>(Math.Max(a.Length, b.Length));
        for (int i = 0; i < testa; i++) result.Add(new DiffLine(DiffKind.Equal, a[i]));

        int n = a.Length - testa - coda, m = b.Length - testa - coda;
        if ((long)(n + 1) * (m + 1) <= MaxCelle)
            Medio(a, b, testa, n, m, result);
        else
        {
            for (int i = 0; i < n; i++) result.Add(new DiffLine(DiffKind.Removed, a[testa + i]));
            for (int j = 0; j < m; j++) result.Add(new DiffLine(DiffKind.Added, b[testa + j]));
        }

        for (int i = a.Length - coda; i < a.Length; i++) result.Add(new DiffLine(DiffKind.Equal, a[i]));
        return result;
    }

    // Classic LCS on a[off..off+n) and b[off..off+m), flat table of (n+1)·(m+1) cells.
    private static void Medio(string[] a, string[] b, int off, int n, int m, List<DiffLine> result)
    {
        int w = m + 1;
        var lcs = new int[(n + 1) * w];
        for (int i = n - 1; i >= 0; i--)
            for (int j = m - 1; j >= 0; j--)
                lcs[i * w + j] = a[off + i] == b[off + j]
                    ? lcs[(i + 1) * w + j + 1] + 1
                    : Math.Max(lcs[(i + 1) * w + j], lcs[i * w + j + 1]);

        int x = 0, y = 0;
        while (x < n && y < m)
        {
            if (a[off + x] == b[off + y])
            {
                result.Add(new DiffLine(DiffKind.Equal, a[off + x]));
                x++; y++;
            }
            else if (lcs[(x + 1) * w + y] >= lcs[x * w + y + 1])
            {
                result.Add(new DiffLine(DiffKind.Removed, a[off + x]));
                x++;
            }
            else
            {
                result.Add(new DiffLine(DiffKind.Added, b[off + y]));
                y++;
            }
        }
        while (x < n) result.Add(new DiffLine(DiffKind.Removed, a[off + x++]));
        while (y < m) result.Add(new DiffLine(DiffKind.Added, b[off + y++]));
    }

    private static string Normalize(string line) => line.TrimEnd('\r', '\n');
}

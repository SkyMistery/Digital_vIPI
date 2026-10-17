using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Models;

/// <summary>
/// Un simbolo di un <c>.sym</c> (sezione <c>[SYMBOLS]</c>, lotto «Subito» slice 17a): 13×13 pixel e il nome nel
/// commento sopra. Nel file è una riga di 13 gruppi di 13 cifre separati da <c>;</c> — ogni gruppo è una
/// <b>colonna</b>, da sinistra a destra, e ogni cifra un pixel dall'alto in basso (<c>1</c> acceso; carta «file per
/// file» §20).
/// <para>Il manuale non descrive il formato. Prova T3 del committente in Aurora (17 ottobre): i simboli del file sono la
/// tavolozza da cui si sceglie il simbolo di un ruolo (SETTINGS, linguetta SYMBOLS), nell'ordine del file; la scelta
/// si salva nel profilo come disegno. La posizione quindi non decide nessun ruolo, e il nome Aurora non lo legge.</para>
/// </summary>
public sealed class Simbolo
{
    /// <summary>Il lato del simbolo in pixel.</summary>
    public const int Lato = 13;

    /// <summary>Un simbolo tutto spento, scritto come nel file.</summary>
    public static readonly string Vuoto = Scrivi(Enumerable.Repeat(new string('0', Lato), Lato));

    /// <summary>Il nome, dal commento sopra i pixel (<c>//FIX vuoto</c>); null se il simbolo non ne ha.</summary>
    public string? Nome { get; set; }

    /// <summary>
    /// Il nome sta in una riga di testo senza <c>//</c> (<c>AC_comb SEL</c> e <c>AC_DUPE</c> sul fork): Aurora la salta
    /// (T3-2), ma è una riga che il formato non prevede (T2). Tolto, il nome si riscrive col <c>//</c>.
    /// </summary>
    public bool NomeSenzaBarre { get; set; }

    /// <summary>I pixel come si scrivono: 13 gruppi di 13 cifre, ognuno chiuso da <c>;</c>.</summary>
    public string Pixel { get; set; } = Vuoto;

    public SourceRef Source { get; set; } = null!;

    /// <summary>Le 13 colonne, da sinistra.</summary>
    public IReadOnlyList<string> Colonne() => Leggi(Pixel) ?? throw new InvalidOperationException($"Pixel non 13×13: {Pixel}");

    /// <summary>Vero se il pixel (x da sinistra, y dall'alto) è acceso.</summary>
    public bool Acceso(int x, int y) => Colonne()[x][y] == '1';

    /// <summary>Le 13 colonne di una riga di pixel, o null se non è 13×13 di sole cifre 0 e 1.</summary>
    public static IReadOnlyList<string>? Leggi(string riga)
    {
        ArgumentNullException.ThrowIfNull(riga);
        var colonne = riga.Split(';', StringSplitOptions.TrimEntries).Where(c => c.Length > 0).ToList();
        return colonne.Count == Lato && colonne.All(c => c.Length == Lato && c.All(ch => ch is '0' or '1')) ? colonne : null;
    }

    /// <summary>La riga di pixel di 13 colonne: <c>0000000000000;…;</c>.</summary>
    public static string Scrivi(IEnumerable<string> colonne)
    {
        ArgumentNullException.ThrowIfNull(colonne);
        return string.Concat(colonne.Select(c => c + ";"));
    }

    /// <summary>I pixel con quello in (x, y) acceso o spento.</summary>
    public static string ConIlPixel(string pixel, int x, int y, bool acceso)
    {
        var colonne = Leggi(pixel)?.ToArray() ?? throw new ArgumentException($"Pixel non 13×13: {pixel}", nameof(pixel));
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(x, Lato);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(y, Lato);
        char[] colonna = colonne[x].ToCharArray();
        colonna[y] = acceso ? '1' : '0';
        colonne[x] = new string(colonna);
        return Scrivi(colonne);
    }
}

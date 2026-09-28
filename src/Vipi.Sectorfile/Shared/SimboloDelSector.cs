namespace Vipi.Sectorfile.Shared;

/// <summary>
/// Un simbolo di un <c>.sym</c> (sezione <c>[SYMBOLS]</c>): 13×13 pixel. Nel file è una riga di 13 gruppi di 13 cifre
/// separati da <c>;</c> — ogni gruppo è una <b>colonna</b>, da sinistra a destra, e ogni cifra un pixel dall'alto in
/// basso (<c>1</c> acceso; carta «file per file» §20: lette come righe, il FIX punterebbe a sinistra).
/// </summary>
/// <param name="Numero">La posizione nel file, da 1: Aurora sceglie i simboli del sector per numero (T3, da provare).</param>
/// <param name="Nome">Il nome dal commento sopra (<c>//FIX vuoto</c>), o dalla riga di testo senza <c>//</c> sopra
/// (<c>AC_comb SEL</c>, vedi <paramref name="NomeSenzaCommento"/>); null se non ne ha.</param>
/// <param name="Colonne">Le 13 colonne come sono scritte, 13 cifre ciascuna.</param>
/// <param name="Riga">La riga dei pixel nel file, da 1.</param>
/// <param name="NomeSenzaCommento">Il nome è una riga di testo senza <c>//</c> (T2: 2 sul fork).</param>
public sealed record SimboloDelSector(int Numero, string? Nome, IReadOnlyList<string> Colonne, int Riga, bool NomeSenzaCommento = false)
{
    /// <summary>Il lato del simbolo in pixel.</summary>
    public const int Lato = 13;

    /// <summary>Vero se il pixel (x da sinistra, y dall'alto) è acceso.</summary>
    public bool Acceso(int x, int y) => Colonne[x][y] == '1';
}

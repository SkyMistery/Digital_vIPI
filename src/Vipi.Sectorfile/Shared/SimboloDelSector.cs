namespace Vipi.Sectorfile.Shared;

/// <summary>
/// Un disegno 13×13 come quelli di Aurora — dei <c>.sym</c> e dei <c>SYMBOLS_</c> dei profili; la mappa del Lab lo usa
/// per i punti (slice 17b). Il record che si legge e si scrive da un <c>.sym</c> è <see cref="Models.Simbolo"/>. Nel file è una riga di 13 gruppi di 13 cifre
/// separati da <c>;</c> — ogni gruppo è una <b>colonna</b>, da sinistra a destra, e ogni cifra un pixel dall'alto in
/// basso (<c>1</c> acceso; carta «file per file» §20: lette come righe, il FIX punterebbe a sinistra).
/// </summary>
/// <param name="Numero">La posizione nell'elenco, da 1 (prova T3: in Aurora la posizione non decide nessun ruolo).</param>
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

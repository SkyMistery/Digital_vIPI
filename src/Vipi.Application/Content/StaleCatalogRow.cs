namespace Vipi.Application.Content;

/// <summary>
/// Una riga di catalogo che la <b>sorgente non elenca più</b>: il suo timbro d'import è rimasto indietro
/// rispetto all'ultimo giro riuscito, mentre le altre righe dello stesso giro sono state riscritte.
///
/// <para><b>Perché non basta guardare la proiezione.</b> I cataloghi non potano mai — «i settori spariti
/// dalla sorgente restano, l'admin li nasconde» — quindi per la proiezione non è successo niente: il
/// callsign è ancora lì, visibile, e il settore resta <b>attivo</b>. È il caso della <b>rinomina</b>
/// (<c>LIRN_US0_APP</c> → <c>LIRN_US1_APP</c>): nessuno sparisce, il vecchio diventa un fantasma che
/// continua a rivendicare la sua area e a portarsi dietro il documento, e il nuovo nasce senza niente.</para>
/// </summary>
/// <param name="Callsign">Il callsign che la sorgente non manda più.</param>
/// <param name="AccCode">L'ente di competenza (per il reverse-lookup).</param>
/// <param name="Position">Suffisso di posizione (CTR/APP/TWR…): serve a proporre la rinomina.</param>
/// <param name="Scope">Aeroporto (ICAO) o ACC in cui vive: il perimetro dentro cui cercare il sostituto.</param>
/// <param name="LastSeenUtc">Ultimo timbro d'import.</param>
public sealed record StaleCatalogRow(
    string Callsign, string AccCode, string? Position, string Scope, DateTime LastSeenUtc)
{
    /// <summary>Da quanti giorni la sorgente non lo manda più (arrotondati per difetto).</summary>
    public int GiorniDiSilenzio(DateTime nowUtc) => (int)(nowUtc - LastSeenUtc).TotalDays;
}

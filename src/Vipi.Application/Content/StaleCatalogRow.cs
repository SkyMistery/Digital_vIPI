namespace Vipi.Application.Content;

/// <summary>
/// Una riga di catalogo che la <b>sorgente non elenca più</b>: il suo timbro d'import è rimasto indietro
/// rispetto all'ultimo giro riuscito, mentre le altre righe dello stesso giro sono state riscritte.
///
/// <para><b>Perché non basta guardare la proiezione.</b> I cataloghi non potano mai — «i settori spariti
/// dalla sorgente restano, l'admin li nasconde» — quindi per la proiezione non è successo niente: il
/// callsign è ancora lì, visibile, e il settore resta <b>attivo</b>.</para>
///
/// <para>⚠️ <b>Fino al 26 agosto 2026 questa riga voleva dire due cose insieme</b>, e per una sola aveva un
/// rimedio. Poteva essere una sparizione vera — <c>LIED_G_APP</c>, che la sorgente risponde 404 — oppure una
/// <b>rinomina</b>, dove non spariva nessuno: il vecchio restava come fantasma a portarsi dietro il documento
/// e il nuovo nasceva senza niente. Distinguerle si poteva solo indovinando, e indovinare voleva dire
/// spostare un documento sul settore sbagliato. Ora non serve più: l'identità della sorgente
/// (<c>AccSector.IvaoId</c> / <c>AirportSector.IvaoId</c>) rende la rinomina un <c>UPDATE</c> che avviene
/// prima, e quel che arriva qui sono <b>solo sparizioni</b>. Vedi
/// <c>docs/feature/2026-08-26-identita-dei-settori.md</c>.</para>
/// </summary>
/// <param name="Callsign">Il callsign che la sorgente non manda più.</param>
/// <param name="AccCode">L'ente di competenza (per il reverse-lookup).</param>
/// <param name="LastSeenUtc">Ultimo timbro d'import.</param>
public sealed record StaleCatalogRow(string Callsign, string AccCode, DateTime LastSeenUtc)
{
    /// <summary>Da quanti giorni la sorgente non lo manda più (arrotondati per difetto).</summary>
    public int GiorniDiSilenzio(DateTime nowUtc) => (int)(nowUtc - LastSeenUtc).TotalDays;
}

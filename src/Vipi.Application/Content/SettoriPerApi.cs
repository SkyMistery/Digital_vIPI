using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Un centro come lo vede un programma esterno: solo quelli che il sito mostra (S119).</summary>
public sealed record ApiAccRow(string Code, string Name, bool IsMilitary);

/// <summary>
/// Un settore di un centro, dalla Struttura viva (la proiezione <c>Sectors</c>, attivi).
/// </summary>
/// <param name="ParentCallsign">Il padre nell'albero effettivo (scritto, o derivato dalla scala delle posizioni):
/// può stare in un ALTRO centro — un APP di confine sotto il CTR del vicino. <c>null</c> = radice.</param>
/// <param name="CoverageOrder">Più basso = più in alto: l'ordine con cui la proiezione mette i tipi.</param>
public sealed record ApiSectorRow(
    string Callsign, string Name, SectorType Type, SectorKind Kind, string? AirportIcao, string? Frequency,
    string? ParentCallsign, int CoverageOrder);

/// <summary>
/// La lettura delle API dei settori (S119, 21 ottobre 2026, committente): una porta apposta, di sola lettura,
/// perché i servizi della Struttura che elencano i settori sono riservati agli admin, e allentarli per un
/// programma con la chiave aprirebbe anche le pagine.
/// <para>⚠️ Un centro <b>nascosto</b> o <b>estero</b> non c'è: per le pagine del sito non esiste (nascosto) o è
/// un vicino di cui non scriviamo la struttura (estero). I suoi settori compaiono solo dove la Struttura li usa,
/// come padri o come chi riceve un trasferimento.</para>
/// </summary>
public interface ISettoriPerApi
{
    /// <summary>I centri italiani visibili, in ordine di codice.</summary>
    Task<IReadOnlyList<ApiAccRow>> CentriAsync(CancellationToken ct = default);

    /// <summary>Il centro col suo codice, se è fra quelli di <see cref="CentriAsync"/>; <c>null</c> altrimenti.</summary>
    Task<ApiAccRow?> CentroAsync(string codice, CancellationToken ct = default);

    /// <summary>I settori attivi del centro, coi padri anche fuori dal centro. Vuoto se il centro non c'è.</summary>
    Task<IReadOnlyList<ApiSectorRow>> SettoriAsync(string codice, CancellationToken ct = default);
}

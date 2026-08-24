using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vipi.Application.Abstractions;

namespace Vipi.Application.Stats;

/// <summary>Esito di un giro di storico, per il log e per la pagina Sorgenti.</summary>
public sealed record AtcHistoryImportResult(int Fetched, int Created, int Updated, int ShiftsFixed, int Prefixes);

/// <summary>
/// Riempie l'archivio con lo <b>storico</b> delle connessioni ATC italiane. Un corpo solo per due usi, come
/// vuole la regola dei gate: il primo giro recupera i dodici mesi che la sorgente conserva, i successivi
/// ripassano le ultime ore.
///
/// <para><b>Perché ripassare ogni giorno se c'è il poller.</b> Il poller sa quando un ATC «non c'era più al
/// giro delle 21:03»; la sorgente sa quando ha davvero staccato. E se l'applicazione è stata giù, il poller
/// non ha visto niente, mentre lo storico sì.</para>
///
/// <para><b>I prefissi.</b> Il filtro della sorgente vuole almeno tre caratteri, quindi l'Italia si copre con
/// ventitré query <c>LIA…LIZ</c> invece di scorrere le connessioni del mondo (misurato: 414 561 sessioni in
/// archivio alla sorgente contro le 21 231 italiane di dodici mesi).</para>
/// </summary>
public sealed class AtcHistoryImportUseCase
{
    /// <summary>I prefissi italiani a tre lettere. Quelli senza traffico costano una chiamata che torna vuota.</summary>
    public static readonly IReadOnlyList<string> ItalianPrefixes = new[]
    {
        "LIA", "LIB", "LIC", "LID", "LIE", "LIG", "LIH", "LII", "LIJ", "LIK", "LIL", "LIM",
        "LIN", "LIO", "LIP", "LIQ", "LIR", "LIS", "LIT", "LIU", "LIV", "LIY", "LIZ",
    };

    private readonly IAtcHistorySource _sorgente;
    private readonly IAtcSessionStore _archivio;

    public AtcHistoryImportUseCase(IAtcHistorySource sorgente, IAtcSessionStore archivio)
    {
        _sorgente = sorgente;
        _archivio = archivio;
    }

    /// <param name="prefixes">Prefissi da interrogare; <c>null</c> = tutti quelli italiani.</param>
    public async Task<AtcHistoryImportResult> RunAsync(
        DateTimeOffset from, DateTimeOffset to, IReadOnlyList<string>? prefixes = null, CancellationToken ct = default)
    {
        var elenco = prefixes ?? ItalianPrefixes;
        var tutte = new List<SourceAtcSessionHistory>();

        foreach (var prefisso in elenco)
        {
            ct.ThrowIfCancellationRequested();

            var sessioni = await _sorgente.GetAtcSessionsAsync(prefisso, from, to, ct);
            if (sessioni.Count == 0) continue;

            tutte.AddRange(sessioni);
        }

        if (tutte.Count == 0) return new AtcHistoryImportResult(0, 0, 0, 0, elenco.Count);

        // Una sessione può uscire da due prefissi solo se qualcuno cambia le regole dei callsign, ma la
        // deduplicazione costa nulla e toglie di mezzo un doppione che sarebbe difficile da vedere.
        var distinte = tutte.GroupBy(s => s.SessionId).Select(g => g.First()).ToList();

        var (creati, aggiornati) = await _archivio.UpsertHistoryAsync(distinte, ct);

        // I turni si riconoscono solo sulla sequenza completa: si ricalcolano dopo aver scritto tutto,
        // con lo stesso raggruppatore che usa il poller.
        var turni = await _archivio.RecomputeShiftsAsync(from, to, ct);

        return new AtcHistoryImportResult(distinte.Count, creati, aggiornati, turni, elenco.Count);
    }
}

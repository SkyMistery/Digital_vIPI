using System;
using System.Threading;
using System.Threading.Tasks;
using Vipi.Application.Abstractions;

namespace Vipi.Application.Stats;

/// <summary>Esito di un giro di potatura.</summary>
public sealed record TrafficRetentionResult(int Removed, bool MoreToGo);

/// <summary>Esito di un giro di potatura delle sessioni: quante ne sono state riassunte e tolte.</summary>
public sealed record SessionRetentionResult(int Removed, bool MoreToGo);

/// <summary>
/// Pota il <b>dettaglio</b> delle tratte oltre la finestra di conservazione.
///
/// <para><b>Perché esiste.</b> La carta del servizio decide «dettaglio 12 mesi, sessioni per sempre» dal 24
/// agosto 2026, ma la decisione viveva solo sulla carta: misurato, il dettaglio cresce di ~500 000 righe
/// l'anno e non lo cancellava nessuno. È lo stesso modo in cui la retention della pubblicazione si era
/// accumulata la prima volta.</para>
///
/// <para><b>Che cosa NON tocca.</b> Le sessioni, mai. I contatori denormalizzati che le riassumono
/// (<c>TrafficCount</c>, <c>MovementCount</c>, <c>TrafficMinutes</c>) stanno sulla riga sessione apposta
/// perché la potatura sia <b>reversibile nei numeri</b>: le ore e i movimenti di due anni fa restano veri
/// anche quando il dettaglio di quei voli non c'è più. Senza quei contatori, il giorno della prima potatura
/// le statistiche di un anno fa sarebbero diventate zero.</para>
///
/// <para>⚠️ A scaglioni, e con un tetto per giro: la prima potatura di un archivio maturo ha centinaia di
/// migliaia di righe da smaltire, e nessuna di esse serve a qualcuno adesso.</para>
/// </summary>
public sealed class TrafficRetentionUseCase
{
    /// <summary>Quanto si conserva il dettaglio. Sulla carta (§5.1) sono dodici mesi.</summary>
    public const int GiorniDiDettaglio = 366;

    private readonly IAtcTrafficStore _archivio;

    public TrafficRetentionUseCase(IAtcTrafficStore archivio) => _archivio = archivio;

    /// <param name="max">Tetto di righe per giro; oltre, si riprende alla prossima notte.</param>
    /// <param name="batch">Righe per scaglione: è la dimensione della singola cancellazione.</param>
    public async Task<TrafficRetentionResult> RunAsync(
        DateTimeOffset now, int max, int batch = 2000, int giorni = GiorniDiDettaglio,
        CancellationToken ct = default)
    {
        if (max <= 0 || batch <= 0) return new TrafficRetentionResult(0, false);

        var limite = now.AddDays(-Math.Max(1, giorni));
        var tolte = 0;

        while (tolte < max)
        {
            ct.ThrowIfCancellationRequested();

            var quante = await _archivio.PruneTrafficAsync(limite, Math.Min(batch, max - tolte), ct);
            if (quante == 0) return new TrafficRetentionResult(tolte, MoreToGo: false);

            tolte += quante;
        }

        // Il tetto è stato raggiunto: c'è ancora arretrato, e lo dice a chi registra l'esito invece di
        // lasciar credere che sia finito.
        return new TrafficRetentionResult(tolte, MoreToGo: true);
    }
}


/// <summary>
/// Pota le <b>sessioni</b> ATC oltre la finestra di conservazione, lasciando il riassunto mensile.
///
/// <para><b>Perché esiste.</b> Fino al 26 agosto 2026 le sessioni non le cancellava nessuno: erano l'unica
/// tabella che cresceva senza fine — 21 275 righe nei primi dodici mesi, e nessun tetto. La carta diceva
/// «sessioni per sempre» proprio perché i contatori denormalizzati che portano (ore, movimenti) reggevano le
/// statistiche vecchie da soli. La decisione del 26 agosto è un'altra: dodici mesi anche per loro, e oltre
/// resta <c>AtcMonthRollup</c> — mese, persona, callsign.</para>
///
/// <para>⚠️ <b>Il riassunto viene prima della cancellazione, e nella stessa transazione.</b> È tutto il
/// senso della cosa: se le sessioni sparissero senza essere confluite da qualche parte, le ore di un anno
/// fa diventerebbero zero — che è l'incidente che questa classe esiste per non fare.</para>
///
/// <para>A scaglioni e con un tetto per giro, come per il dettaglio: la prima potatura di un archivio maturo
/// ha migliaia di righe da smaltire e nessuna serve a qualcuno adesso.</para>
/// </summary>
public sealed class AtcSessionRetentionUseCase
{
    /// <summary>Quanto si conservano le sessioni per esteso. Dodici mesi, come il dettaglio delle tratte.</summary>
    public const int GiorniDiSessioni = 366;

    private readonly IAtcTrafficStore _archivio;

    public AtcSessionRetentionUseCase(IAtcTrafficStore archivio) => _archivio = archivio;

    public async Task<SessionRetentionResult> RunAsync(
        DateTimeOffset now, int max, int batch = 500, int giorni = GiorniDiSessioni,
        CancellationToken ct = default)
    {
        if (max <= 0 || batch <= 0) return new SessionRetentionResult(0, false);

        var limite = now.AddDays(-Math.Max(1, giorni));
        var tolte = 0;

        while (tolte < max)
        {
            ct.ThrowIfCancellationRequested();

            var quante = await _archivio.RollupAndPruneSessionsAsync(limite, Math.Min(batch, max - tolte), ct);
            if (quante == 0) return new SessionRetentionResult(tolte, MoreToGo: false);

            tolte += quante;
        }

        return new SessionRetentionResult(tolte, MoreToGo: true);
    }
}

using Vipi.Application.Abstractions;
using Vipi.Domain.Entities;

namespace Vipi.Application.Content;

/// <summary>
/// Un NOTAM pronto per il riquadro in testa a un documento (carta
/// <c>docs/feature/2026-10-12-notam-sui-documenti.md</c>).
/// </summary>
/// <param name="InArrivo">Vero se non è ancora in vigore: si mostra lo stesso, da
/// <see cref="NotamInVista.GiorniDiAnticipo"/> giorni prima, e il riquadro lo dice.</param>
public sealed record NotamAvviso(
    int IvaoId,
    string Nome,
    DateTime? InizioUtc,
    DateTime? FineUtc,
    string Testo,
    bool InArrivo,
    IReadOnlyList<string> Aeroporti,
    IReadOnlyList<string> Centri);

/// <summary>
/// <b>Quali NOTAM riguardano un documento</b> — o la postazione che su quel documento lavora. La regola è del
/// committente (12 ottobre 2026) e sta qui, in un posto solo, perché a chiederla sono quattro pagine di documento
/// e la vista live:
/// <list type="bullet">
/// <item>vIPI d'aeroporto, vSOP militare, APP: il NOTAM <b>riguarda l'aeroporto</b>;</item>
/// <item>vIPI di ACC: il NOTAM <b>riguarda l'ACC</b>, oppure l'aeroporto di uno degli APP che quella vIPI contiene
/// («per gli APP nelle vIPI di ACC vale quello detto per gli APP»);</item>
/// <item>vLOA: <b>niente</b> («per le vLOA non prevediamo nulla per ora»). Non c'è un ambito per gli accordi, e
/// non è una dimenticanza.</item>
/// </list>
/// </summary>
public sealed class AmbitoNotam
{
    private readonly HashSet<string> _aeroporti;
    private readonly string? _acc;

    private AmbitoNotam(IEnumerable<string?> aeroporti, string? acc)
    {
        _aeroporti = aeroporti.Select(Pulito).Where(c => c.Length > 0).ToHashSet(StringComparer.Ordinal);
        _acc = Pulito(acc) is { Length: > 0 } a ? a : null;
    }

    /// <summary>Non riguarda nessun NOTAM: è l'ambito di chi non ha né uno scalo né un ACC da dichiarare.</summary>
    public static AmbitoNotam Nessuno { get; } = new(Array.Empty<string?>(), null);

    /// <summary>I documenti di uno scalo (o di più scali, in una pagina unita).</summary>
    public static AmbitoNotam Aeroporti(IEnumerable<string?> icao) => new(icao, null);

    /// <summary>La vIPI di un ACC, con gli scali degli APP che contiene.</summary>
    public static AmbitoNotam Acc(string acc, IEnumerable<string?> aeroportiDegliApp) => new(aeroportiDegliApp, acc);

    /// <summary>
    /// Lo scalo di un callsign o del codice di un ente: la testa prima del primo <c>_</c>
    /// (<c>LIRQ_NN0_APP</c> → <c>LIRQ</c>, <c>LIRE</c> → <c>LIRE</c>).
    /// </summary>
    public static string AeroportoDi(string? callsign) => Pulito((callsign ?? "").Split('_', 2)[0]);

    public bool Riguarda(IReadOnlyCollection<string> aeroporti, IReadOnlyCollection<string> centri) =>
        (_acc is not null && centri.Contains(_acc)) || aeroporti.Any(_aeroporti.Contains);

    private static string Pulito(string? codice) => (codice ?? "").Trim().ToUpperInvariant();
}

/// <summary>Quando un NOTAM si mostra. Regole senza archivio, per poterle provare da sole.</summary>
public static class NotamInVista
{
    /// <summary>Un NOTAM compare questi giorni prima di entrare in vigore (committente, 12 ottobre 2026).</summary>
    public const int GiorniDiAnticipo = 7;

    /// <summary>
    /// Si mostra da <see cref="GiorniDiAnticipo"/> giorni prima dell'inizio fino alla fine <b>esclusa</b>.
    /// ⚠️ La fine la decide la data e non il giro di import: un NOTAM sparisce al suo minuto anche se la sorgente
    /// non risponde da ore — e la sorgente gli scaduti non li toglie affatto.
    /// </summary>
    public static bool SiMostra(DateTime? inizioUtc, DateTime? fineUtc, DateTime adessoUtc) =>
        (fineUtc is not { } fine || adessoUtc < fine)
        && (inizioUtc is not { } inizio || adessoUtc >= inizio.AddDays(-GiorniDiAnticipo));

    public static bool InArrivo(DateTime? inizioUtc, DateTime adessoUtc) => inizioUtc is { } inizio && adessoUtc < inizio;

    /// <summary>
    /// I NOTAM di un documento, nell'ordine del riquadro: prima quelli in vigore (il più recente in cima), poi
    /// quelli in arrivo (il più vicino in cima).
    /// </summary>
    public static IReadOnlyList<NotamAvviso> Scegli(IEnumerable<Notam> tutti, AmbitoNotam ambito, DateTime adessoUtc) =>
        tutti.Where(n => SiMostra(n.StartUtc, n.EndUtc, adessoUtc))
             .Select(n => (Riga: n, Aeroporti: Notam.Dividi(n.Airports), Centri: Notam.Dividi(n.Centers)))
             .Where(x => ambito.Riguarda(x.Aeroporti, x.Centri))
             .Select(x => new NotamAvviso(x.Riga.IvaoId, x.Riga.Name, x.Riga.StartUtc, x.Riga.EndUtc, x.Riga.Text,
                                          InArrivo(x.Riga.StartUtc, adessoUtc), x.Aeroporti, x.Centri))
             .OrderBy(a => a.InArrivo)
             .ThenBy(a => a.InArrivo ? a.InizioUtc!.Value.Ticks : -(a.InizioUtc ?? DateTime.MinValue).Ticks)
             .ThenBy(a => a.Nome, StringComparer.Ordinal)
             .ToList();
}

/// <summary>I NOTAM da mostrare in testa a un documento.</summary>
public interface INotamDeiDocumenti
{
    /// <summary>
    /// ⚠️ Non lancia mai: il riquadro è un di più, e un archivio che non risponde non deve portarsi dietro il
    /// documento che il lettore ha chiesto. L'errore va nel registro e la pagina esce senza riquadro.
    /// </summary>
    Task<IReadOnlyList<NotamAvviso>> PerAsync(AmbitoNotam ambito, CancellationToken ct = default);
}

/// <summary>
/// Il giro che copia i NOTAM della divisione dalla sorgente all'archivio.
///
/// <para>⚠️ Un elenco letto e <b>vuoto</b> si prende per vero e svuota l'archivio: una divisione senza NOTAM
/// esiste. Il caso opposto — elenco non letto — non arriva fin qui, perché la sorgente lancia
/// (<see cref="INotamSource.GetDivisionNotamsAsync"/>). Se la sorgente rispondesse «vuoto» per sbaglio, il danno è
/// un riquadro che manca fino al giro dopo.</para>
/// </summary>
public sealed class NotamImportUseCase
{
    private readonly INotamSource _source;
    private readonly INotamStore _store;
    private readonly TimeProvider _clock;

    public NotamImportUseCase(INotamSource source, INotamStore store, TimeProvider? clock = null)
    {
        _source = source;
        _store = store;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<NotamImportResult> RunAsync(CancellationToken ct = default)
    {
        var arrivati = await _source.GetDivisionNotamsAsync(ct);
        return await _store.SincronizzaAsync(arrivati, _clock.GetUtcNow().UtcDateTime, ct);
    }
}

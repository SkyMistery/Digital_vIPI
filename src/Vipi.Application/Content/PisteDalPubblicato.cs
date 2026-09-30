using Vipi.Application.Awos;
using Vipi.Application.Weather;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Su che cosa si decide la pista in uso di uno scalo: regole, minimi LVP e soglie escluse dal ripiego.</summary>
/// <param name="Transizione">TA e fasce TL della sezione <b>congelata</b>; null = non c'è (nessun documento pubblicato,
/// sezione in Live, release di prima): il chiamante proietta i vivi, come fa il documento (U-227).</param>
public sealed record PisteDecisive(IReadOnlyList<RunwayRuleRow> Regole, LvpRow? Lvp, RunwayExclusions Escluse,
    AirportTransitionView? Transizione = null);

/// <summary>
/// Regole di scelta pista, minimi LVP e soglie escluse dal ripiego su cui decidere <b>fuori dal documento</b> — vAWOS,
/// vista rapida, elenco aeroporti: quelli della <b>release pubblicata</b>, non i vivi.
///
/// <para>⚠️ <b>Una porta sola, per tre pagine.</b> La regola stava dentro <c>AwosService</c>; la vista rapida e l'elenco
/// aeroporti leggevano invece l'anagrafica viva, e potevano indicare una pista diversa dal documento pubblico finché non
/// si ripubblicava (decisione del committente, 17-set-2026: «prendano dalla documentazione live», cioè dal documento in
/// vigore). Tre copie della stessa domanda davano prima o poi tre risposte.</para>
///
/// <para>🔴 Fino al 15 settembre 2026 anche il vAWOS leggeva l'anagrafica viva: una regola o un minimo scritti
/// nell'editor e non ancora pubblicati cambiavano subito la pista in uso e lo stato LVP sul quadro pubblico.</para>
///
/// <para>⚠️ La STESSA regola della vIPI (<c>PistaInUso</c>, <c>AirportViewDerivationService</c>): la sezione congelata
/// della release in vigore se c'è; altrimenti i vivi. «Altrimenti» comprende la sezione in <b>Live</b> — lo snapshot
/// non la porta, e allora un cambiamento nell'editor arriva subito, come nel documento — e una release di prima del
/// 12 settembre 2026 senza le regole in forma calcolabile. Edizione: la vIPI civile se pubblicata, il vSOP militare sui
/// campi che hanno solo quello. Nessun documento pubblicato (lo apre un Editor per provarlo): i vivi, non c'è altro.</para>
///
/// <para>⚠️ Le esclusioni («mai in partenza» / «mai in arrivo», carta 2026-09-17-pista-mai-usare.md) vengono dalla
/// sezione Piste congelata con la stessa regola: una casella spuntata e non pubblicata non cambia la pista.</para>
///
/// <para>⚠️ Una sezione LVP congelata SENZA minimi (<c>Minimi</c> null) vale «pubblicata senza minimi», e il quadro
/// ricade sullo standard dichiarandolo: NON si torna ai vivi, che sarebbero proprio i non pubblicati.</para>
/// </summary>
public interface IPisteDalPubblicato
{
    /// <param name="documenti">L'elenco dei documenti se il chiamante l'ha già letto; null = lo legge il servizio,
    /// <b>una volta per scope</b>.</param>
    Task<PisteDecisive> PerScaloAsync(string icao, IReadOnlyList<RunwayRuleRow> regoleVive, LvpRow? lvpVivi,
        IReadOnlyList<RunwayRow> pisteVive, IReadOnlyList<ManagedDoc>? documenti = null, CancellationToken ct = default);
}

/// <inheritdoc cref="IPisteDalPubblicato"/>
public sealed class PisteDalPubblicato : IPisteDalPubblicato
{
    private readonly IDocumentAdminService _documenti;
    private readonly IFrozenSectionReader _congelate;
    private IReadOnlyList<ManagedDoc>? _letti;

    public PisteDalPubblicato(IDocumentAdminService documenti, IFrozenSectionReader congelate)
    {
        _documenti = documenti;
        _congelate = congelate;
    }

    public async Task<PisteDecisive> PerScaloAsync(string icao, IReadOnlyList<RunwayRuleRow> regoleVive, LvpRow? lvpVivi,
        IReadOnlyList<RunwayRow> pisteVive, IReadOnlyList<ManagedDoc>? documenti = null, CancellationToken ct = default)
    {
        var id = (icao ?? "").Trim().ToUpperInvariant();
        var escluseVive = RunwayRow.Esclusioni(pisteVive);
        var docs = documenti ?? (_letti ??= await _documenti.ListAsync(ct));
        if (AwosGate.Edizione(docs, id) is not { } edizione) return new(regoleVive, lvpVivi, escluseVive);

        var snapshot = await _congelate.LoadAsync(edizione, id, ct);   // una lettura per tutte e tre
        var regole = snapshot.Get<AirportRulesView>("runwayrules")?.Regole ?? regoleVive;
        var lvp = snapshot.Get<AirportLvpView>("lvp") is { } congelata ? congelata.Minimi : lvpVivi;
        var escluse = snapshot.Get<AirportRunwaysView>("runways") is { } piste
            ? AirportRunwayRowView.Esclusioni(piste.Rows) : escluseVive;
        // 🔴 U-227: anche TA e TL dal pubblicato — il vAWOS li prendeva dall'anagrafica viva mentre documento e vista
        // rapida mostravano la sezione congelata.
        return new(regole, lvp, escluse, snapshot.Get<AirportTransitionView>("transition"));
    }
}

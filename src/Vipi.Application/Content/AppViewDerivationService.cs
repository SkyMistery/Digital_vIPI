using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Sezioni derivate dell'APP standalone risolte per la vista (frozen o live) — doc 10 §3d. La tabella
/// «Configurazioni» non si congela, ma si deriva dalle configurazioni <b>del documento mostrato</b> (doc 13 §3g).</summary>
public sealed record AppViewDerived(
    IReadOnlyList<AppFreqRow> Freqs, AppCoordination Coord, AccAorView Aor,
    IReadOnlyList<AccConfigTableView> ConfigTable, MinimaView Minima);

/// <summary>
/// Risolve le sezioni derivate (freq/coord/aor/config-table) dell'APP standalone per la VISTA (doc 10 §3d): se
/// <paramref name="useFrozen"/> e c'è una release effettiva, legge l'output CONGELATO dallo snapshot per chiave di
/// sezione; altrimenti deriva live via <see cref="IAppDocumentService"/>. Chiave di release = callsign APP. Separato
/// dallo storage per non accoppiarlo alle derivazioni; la cattura salva SOLO le sezioni Frozen → per una Live il
/// reader ritorna null e si ricade su live (nessun check di RenderMode qui).
/// </summary>
public interface IAppViewDerivationService
{
    /// <param name="view">Il documento che la pagina sta mostrando (pubblico, bozza o anteprima release): da lì —
    /// e non dalla versione di lavoro — vengono le configurazioni su cui si deriva la tabella di accorpamento.</param>
    Task<AppViewDerived> ResolveForViewAsync(string appCallsign, DocumentView view, bool useFrozen, CancellationToken ct = default);
}

/// <inheritdoc cref="IAppViewDerivationService"/>
public sealed class AppViewDerivationService : IAppViewDerivationService
{
    private const string ConfigurationsKey = "configurations";
    private const string AorKey = "aor";

    private readonly IAppDocumentService _app;
    private readonly IFrozenSectionReader _frozen;
    /// <summary>La lingua di chi legge: decide se la PROSA congelata vale, o va ricomposta live.</summary>
    private readonly ReadingLanguageContext? _lingua;


    public AppViewDerivationService(IAppDocumentService app, IFrozenSectionReader frozen, ReadingLanguageContext? lingua = null)
    {
        _app = app;
        _frozen = frozen;
        _lingua = lingua;
    }

    public async Task<AppViewDerived> ResolveForViewAsync(string appCallsign, DocumentView view, bool useFrozen, CancellationToken ct = default)
    {
        var app = (appCallsign ?? "").Trim().ToUpperInvariant();

        // Lo snapshot una volta sola (doc 14 §3c): erano quattro letture dello stesso payload.
        var frozen = useFrozen ? await _frozen.LoadAsync(ReleaseTargetType.App, app, ct) : FrozenSections.Empty;

        var freqs = frozen.Get<List<AppFreqRow>>("frequencies")
            ?? (await _app.DeriveFrequenciesAsync(app, ct)).ToList();
        // ⚠️ Solo la PROSA guarda la lingua (vedi VloaViewDerivationService): le altre congelate restano.
        var coord = frozen.GetProsa<AppCoordination>("coordination", _lingua?.Corrente)
            ?? await _app.DeriveCoordinationAsync(app, ct);
        // 🔴 T-028 (revisione del 13 settembre 2026): anche la mappa AoR live parte dal documento MOSTRATO — shape
        // extra, colori e configurazioni. Chiedendola al service si leggeva la versione di lavoro, e la pagina
        // pubblica disegnava la personalizzazione di una bozza: lo stesso difetto della tabella qui sotto.
        // Le configurazioni, UNA volta: servono alla mappa AoR (le chip) e alla tabella d'accorpamento.
        var configurazioni = await ConfigurazioniAsync(app, view, frozen, useFrozen, ct);
        var aor = frozen.Get<AccAorView>("aor")
            ?? await _app.GetAorViewAsync(app, AorCustomizationOf(view), configurazioni, ct);
        var minima = frozen.Get<MinimaView>("minima")
            ?? await _app.DeriveMinimaAsync(app, ct);

        // L'accorpamento non si congela — si ricalcola da input già congelati — ma le CONFIGURAZIONI da cui parte
        // devono essere quelle del documento mostrato (doc 13 §3g): vedi ConfigurazioniAsync.
        var configTable = await _app.DeriveConfigTableAsync(app, configurazioni, ct);

        return new AppViewDerived(freqs, coord, aor, configTable, minima);
    }

    /// <summary>
    /// Le configurazioni del documento <b>mostrato</b> (carta 2026-10-08-configurazioni-possibili §4–§5, la
    /// stessa regola di <see cref="ConfigurazioniDelDocumento"/>):
    /// <list type="bullet">
    /// <item>versione di lavoro → la Struttura di adesso (non c'è più una bozza delle configurazioni: il difetto
    /// del doc 13 §3g, «sulla pagina pubblica le configurazioni di una bozza», non può più darsi);</item>
    /// <item>release nata con le configurazioni in Struttura → la voce congelata, o la Struttura se la sezione
    /// è Live;</item>
    /// <item>release di prima → il <c>BodyJson</c> della sezione nello snapshot, com'è.</item>
    /// </list>
    /// </summary>
    private async Task<IReadOnlyList<AccConfiguration>> ConfigurazioniAsync(
        string app, DocumentView view, FrozenSections frozen, bool useFrozen, CancellationToken ct)
    {
        if (!useFrozen) return await _app.GetConfigurationsAsync(app, ct);
        if (frozen.Get<List<AccConfiguration>>(ConfigurationsKey) is { } congelate) return congelate;
        if (frozen.ConfigurazioniDallaStruttura) return await _app.GetConfigurationsAsync(app, ct);
        return ConfigurationsOf(view);
    }

    /// <summary>Configurazioni scritte nella sezione keyed del documento mostrato (vuote se la sezione manca):
    /// vale per le sole release di prima dell'8 ottobre 2026.</summary>
    private static IReadOnlyList<AccConfiguration> ConfigurationsOf(DocumentView view)
    {
        var section = view?.Sections.FirstOrDefault(s =>
            string.Equals(s.SectionKey, ConfigurationsKey, StringComparison.OrdinalIgnoreCase));
        return ConfigTableProjector.Deserialize(SectionPayload.Read(section));
    }

    /// <summary>Shape extra e colori salvati nella sezione <c>aor</c> del documento mostrato (vuoti se manca o è
    /// illeggibile: una mappa senza personalizzazione, non una pagina rotta).</summary>
    private static AorExtraShapes AorCustomizationOf(DocumentView view)
    {
        var section = view?.Sections.FirstOrDefault(s =>
            string.Equals(s.SectionKey, AorKey, StringComparison.OrdinalIgnoreCase));
        var json = SectionPayload.Read(section);
        if (string.IsNullOrWhiteSpace(json)) return new AorExtraShapes();
        try { return System.Text.Json.JsonSerializer.Deserialize<AorExtraShapes>(json) ?? new AorExtraShapes(); }
        catch (System.Text.Json.JsonException) { return new AorExtraShapes(); }
    }
}

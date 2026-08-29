using Vipi.Application.Abstractions;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Perché un giro non è stato fatto. ⚠️ Le due ragioni non sono la stessa cosa e vanno distinte nei
/// log: la prima è una <b>decisione</b> dell'amministratore, la seconda è un <b>guasto</b>.</summary>
public enum NavaidImportSkip
{
    /// <summary>La policy esclude le radioassistenze: le gestisce una persona.</summary>
    Esclusa,

    /// <summary>Il catalogo punti è vuoto — repo spostato, rete giù. ⚠️ <b>Non</b> «non ci sono più
    /// radioassistenze»: trattarlo come un giro riuscito scriverebbe «ultimo giro: adesso» su un giro che non
    /// ha letto niente.</summary>
    SorgenteMuta,
}

/// <summary>Esito del giro: o quel che ha fatto, o perché non l'ha fatto. Mai né l'uno né l'altro.</summary>
public sealed record NavaidImportReport(NavaidImportOutcome? Esito, NavaidImportSkip? Saltato, int DallaSorgente);

/// <summary>Il giro che porta le radioassistenze dal sectorfile all'anagrafica (carta vSOP militari §12b).</summary>
public interface INavaidImporter
{
    Task<NavaidImportReport> RunAsync(CancellationToken ct = default);
}

/// <summary>
/// Dal catalogo punti della divisione all'anagrafica delle radioassistenze.
///
/// <para>
/// ⚠️ <b>Passano solo VOR e NDB.</b> I <i>fix</i> sono punti di riporto: non hanno frequenza, non hanno
/// canale e non sono radioassistenze — metterli qui riempirebbe l'anagrafica di tremila righe che nessuna
/// tabella di SOP citerà mai, e la tendina da cui si sceglie diventerebbe inservibile.
/// </para>
/// <para>
/// ⚠️ <b>ILS e TACAN non arrivano da qui, e non è un buco</b>: il sectorfile ha tre famiglie di file di punti
/// e nessuna è la loro. Quelle righe le scrive una persona, e restano sue — è il motivo per cui l'import non
/// pota mai quel che non trova.
/// </para>
/// </summary>
public sealed class NavaidImporter : INavaidImporter
{
    private readonly INavaidSource _sorgente;
    private readonly INavaidCatalog _anagrafica;
    private readonly IImportPolicyStore _policy;

    public NavaidImporter(INavaidSource sorgente, INavaidCatalog anagrafica, IImportPolicyStore policy)
    {
        _sorgente = sorgente;
        _anagrafica = anagrafica;
        _policy = policy;
    }

    public async Task<NavaidImportReport> RunAsync(CancellationToken ct = default)
    {
        // Il cancello dell'amministratore: «gestisco io le radioassistenze» spegne il giro, e da quel momento
        // i campi restano modificabili a mano perché nessuno li marca più come della sorgente.
        var policy = await _policy.GetAsync(ct).ConfigureAwait(false);
        if (!policy.IsImported(ImportCategory.Navaids))
            return new NavaidImportReport(null, NavaidImportSkip.Esclusa, 0);

        var catalogo = await _sorgente.GetAsync(ct).ConfigureAwait(false);

        var righe = catalogo.Entries
            .Where(e => e.Kind is NavaidKind.Vor or NavaidKind.Ndb)
            .Select(e => new SourceNavaid(
                e.Name,
                e.Kind == NavaidKind.Vor ? NavaidRules.NaturaVor : NavaidRules.NaturaNdb,
                e.Frequency, e.Channel, e.Lat, e.Lon))
            .ToList();

        // ⚠️ Catalogo vuoto = sorgente muta (repo spostato, rete giù), NON «non ci sono più radioassistenze».
        // Trattarlo come un giro riuscito con zero righe sarebbe innocuo oggi — l'import non pota — ma
        // scriverebbe «ultimo giro riuscito: adesso» su un giro che non ha letto niente.
        if (righe.Count == 0) return new NavaidImportReport(null, NavaidImportSkip.SorgenteMuta, 0);

        var esito = await _anagrafica.ImportFromSourceAsync(righe, ct).ConfigureAwait(false);
        return new NavaidImportReport(esito, null, righe.Count);
    }
}

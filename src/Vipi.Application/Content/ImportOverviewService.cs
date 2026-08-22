using Vipi.Application.Abstractions;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Come sta una categoria di dati rispetto alla sorgente. È l'ordine in cui la pagina la racconta.</summary>
public enum ImportHealth
{
    /// <summary>Categoria esclusa dalla policy: non si importa per scelta. Non è un guasto e non è un «ok».</summary>
    Esclusa,
    /// <summary>Nessun giro automatico: arriva solo quando qualcuno la chiede.</summary>
    SuRichiesta,
    /// <summary>Il giro c'è ma non è mai riuscito (DB nuovo, o sorgente mai raggiunta).</summary>
    MaiEseguita,
    /// <summary>L'ultimo tentativo è fallito: i dati sono potenzialmente stantii finché il retry non riesce.</summary>
    InErrore,
    /// <summary>L'ultimo successo è più vecchio di due periodi: il giro è fermo anche se nessuno ha urlato.</summary>
    Ferma,
    /// <summary>Ultimo giro riuscito, dentro la cadenza attesa.</summary>
    Aggiornata,
}

/// <summary>
/// Le righe che <b>non</b> sono una categoria della policy: le anagrafiche, sempre di sorgente e senza
/// spunta.
///
/// <para><b>Perché un'enumerazione e non un secondo <c>null</c>.</b> Fino al 22 agosto 2026 l'anagrafica ACC
/// si riconosceva <i>solo</i> dal fatto che <c>Categoria</c> fosse <c>null</c>, e la pagina la nominava nel
/// ramo <c>_ =></c> di tre <c>switch</c>. Con una seconda anagrafica — quella degli aeroporti — quel ramo
/// avrebbe chiamato «ACC» anche lei, in silenzio e senza che nessun test se ne accorgesse: il dispatch che
/// funziona finché i casi sono due.</para>
/// </summary>
public enum ImportAnagrafica
{
    /// <summary>Elenco ACC + subcenter. È la base su cui tutto il resto si appoggia.</summary>
    Acc,
    /// <summary>Assegnazione degli aeroporti alla loro ACC (e primo import del loro catalogo settori).</summary>
    Aeroporti,
}

/// <summary>
/// Una riga della pagina Sorgenti: la categoria, da dove viene, com'è andato l'ultimo giro e quando è atteso
/// il prossimo.
///
/// <para>⚠️ <paramref name="Categoria"/> e <paramref name="Anagrafica"/> si escludono: una riga è o una
/// categoria con la spunta, o un'anagrafica sempre di sorgente. Esattamente uno dei due è valorizzato.</para>
/// </summary>
public sealed record ImportOverviewRow(
    ImportCategory? Categoria,
    ImportAnagrafica? Anagrafica,
    string StateKey,
    bool DaSorgente,
    ImportHealth Stato,
    DateTime? UltimoSuccessoUtc,
    DateTime? UltimoTentativoUtc,
    string? UltimoErrore,
    TimeSpan? Cadenza)
{
    /// <summary>Quando è atteso il prossimo giro automatico (null se non c'è cadenza o non è mai riuscito).</summary>
    public DateTime? ProssimoUtc =>
        Cadenza is TimeSpan p && UltimoSuccessoUtc is DateTime u ? u + p : null;
}

/// <summary>
/// Ciò che la pagina <c>/services/vsop/admin/sources</c> mostra, in <b>un</b> elenco.
///
/// <para><b>Perché un servizio e non due letture nella pagina.</b> Fino al 22 agosto 2026 la pagina aveva due
/// tabelle che parlavano delle stesse cinque cose con due vocabolari — sopra «Settori», sotto
/// <c>AirportSector</c>; sopra «da sorgente / manuale», sotto «ok / errore» — e il lettore doveva fare a
/// mente un join che sbagliava: nell'elenco degli stati compariva anche
/// <see cref="ImportCategories.SpecialAreaForeignOptOut"/>, che import non è, e mancava del tutto la riga
/// dell'anagrafica ACC, che invece si importa ogni giorno.</para>
///
/// <para>⚠️ Il verde non si regala. <c>GatedImportLoop</c> marca il successo quando il run non lancia
/// eccezioni, e con la categoria esclusa il run esce subito senza fare nulla: la riga di stato diceva «ok» e
/// la data di oggi per un import che per scelta non importa niente. La policy vince sullo stato.</para>
/// </summary>
public interface IImportOverviewService
{
    /// <summary>Le sette righe (le due anagrafiche + le cinque categorie), nell'ordine in cui si leggono.</summary>
    Task<IReadOnlyList<ImportOverviewRow>> ListAsync(CancellationToken ct = default);
}

/// <inheritdoc cref="IImportOverviewService"/>
public sealed class ImportOverviewService : IImportOverviewService
{
    private readonly IImportPolicyStore _policy;
    private readonly IImportStateStore _states;
    private readonly IImportSchedule _schedule;

    public ImportOverviewService(IImportPolicyStore policy, IImportStateStore states, IImportSchedule schedule)
    {
        _policy = policy;
        _states = states;
        _schedule = schedule;
    }

    /// <summary>
    /// Quale riga di <c>ImportState</c> racconta quale categoria. ⚠️ Le due enumerazioni non coincidono:
    /// <see cref="ImportCategories"/> nasce dagli hosted service (<c>AirportSector</c>, <c>SpecialArea</c>,
    /// <c>Sid</c> al singolare) e <see cref="ImportCategory"/> dalla policy. Sono la stessa riga a video.
    /// </summary>
    private static readonly (ImportCategory? Categoria, ImportAnagrafica? Anagrafica, string StateKey)[] Righe =
    {
        (null, ImportAnagrafica.Acc, ImportCategories.Acc),
        // ⚠️ L'anagrafica aeroporti NON ha un giro automatico, e la chiave vuota lo dice: assegnare un
        // aeroporto nuovo alla sua ACC crea entità (aeroporto + catalogo settori) e resta un atto di una
        // persona. Ma la pagina deve nominarla: era l'unico modo in cui un aeroporto della divisione entra
        // nel sito, e questo elenco non la citava affatto.
        (null, ImportAnagrafica.Aeroporti, ""),
        // ⚠️ Stessa chiave per due righe, e non è una svista: è lo STESSO giro sugli STESSI aeroporti
        // (AirportDataImportUseCase), e il gate della policy sta per categoria dentro SourceMergeInputs —
        // quindi la categoria esclusa dice «Esclusa» da sé e ciò che resta (ultimo successo, errore della
        // sorgente) è comune a entrambe. Vedi ImportCategories.AirportData.
        (ImportCategory.TransitionAltitude, null, ImportCategories.AirportData),
        (ImportCategory.Runways, null, ImportCategories.AirportData),
        (ImportCategory.Sectors, null, ImportCategories.AirportSector),
        (ImportCategory.Sids, null, ImportCategories.Sid),
        (ImportCategory.SpecialAreas, null, ImportCategories.SpecialArea),
    };

    public async Task<IReadOnlyList<ImportOverviewRow>> ListAsync(CancellationToken ct = default)
    {
        var policy = await _policy.GetAsync(ct);

        // Una query per pagina, non una per riga. ⚠️ SpecialAreaForeignOptOut resta fuori: non è un import,
        // è il segnaposto «riconciliazione già fatta» delle aree degli ACC esteri, e in un elenco intitolato
        // «stato degli import» è una riga che mente.
        var stati = (await _states.GetAllAsync(ct)).ToDictionary(s => s.Category, StringComparer.OrdinalIgnoreCase);

        var righe = new List<ImportOverviewRow>(Righe.Length);
        foreach (var (categoria, anagrafica, chiave) in Righe)
        {
            var daSorgente = categoria is null || policy.IsImported(categoria.Value);
            var cadenza = chiave.Length == 0 ? null : _schedule.PeriodOf(chiave);
            stati.TryGetValue(chiave, out var stato);

            var successo = stato is null || stato.LastSuccessUtc == default ? (DateTime?)null : stato.LastSuccessUtc;
            var errore = string.IsNullOrWhiteSpace(stato?.LastError) ? null : stato!.LastError;

            righe.Add(new ImportOverviewRow(categoria, anagrafica, chiave, daSorgente,
                Salute(daSorgente, cadenza, successo, errore), successo, stato?.LastAttemptUtc, errore, cadenza));
        }
        return righe;
    }

    private static ImportHealth Salute(bool daSorgente, TimeSpan? cadenza, DateTime? successo, string? errore)
    {
        // La policy vince su tutto: se la categoria è esclusa, quello che il loop ha scritto non la riguarda.
        if (!daSorgente) return ImportHealth.Esclusa;

        // ⚠️ L'errore batte «su richiesta», e l'ordine non è teorico: un errore in archivio significa che quel
        // giro c'era e ha fallito. Con la cadenza letta prima, una categoria che aveva fallito e la cui
        // sorgente è poi stata sconfigurata (Sectorfile:RawBaseUrl vuoto) diceva «su richiesta» e mostrava il
        // messaggio dell'errore nella riga sotto: la pill smentiva il testo che le stava accanto.
        if (errore is not null) return ImportHealth.InErrore;
        if (cadenza is null) return ImportHealth.SuRichiesta;
        if (successo is not DateTime u) return ImportHealth.MaiEseguita;

        // Due periodi, non uno: alla scadenza esatta il giro non è in ritardo, sta partendo. Il secondo
        // periodo mancato invece è un giro saltato, ed è la soglia oltre cui vale la pena guardare i log.
        return DateTime.UtcNow - u > cadenza.Value * 2 ? ImportHealth.Ferma : ImportHealth.Aggiornata;
    }
}

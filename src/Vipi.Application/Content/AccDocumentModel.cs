using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// La vIPI ACC caricata dallo storage <c>Document</c> (doc refactor 08e-acc): identità documento/versione + i blocchi
/// assemblati (<see cref="AccAssembledBlock"/>, con Id sezione + <see cref="AccBlock"/> per le derivazioni + mappa
/// chiave-figlia → Id per i salvataggi editoriali). <see cref="Data"/> è la proiezione classica <see cref="AccVipiData"/>
/// (identità + <see cref="AccBlock"/>) per i consumer che non toccano lo storage.
/// </summary>
/// <param name="AiracCycle">Ciclo AIRAC del documento MOSTRATO: quello della release da cui viene lo snapshot.
/// Null in lavorazione — una bozza non è ancora legata a un ciclo, e la pagina ricade su quello corrente. Senza
/// questo campo la vIPI ACC scriveva in pagina il ciclo di oggi accanto a un contenuto congelato a un altro ciclo
/// (doc 13 §3h).</param>
/// <param name="Language">La lingua in cui il documento è REDATTO, per la lettura bilingue (carta
/// <c>docs/feature/2026-08-27-documenti-bilingue.md</c> §7). Null sui documenti salvati prima che il campo
/// esistesse e in lavorazione, dove la sorgente è quella in cui la famiglia nasce.</param>
/// <param name="Translations">Le traduzioni CONGELATE dalla release, per lingua di lettura e impronta del testo:
/// se ci sono vincono sulla memoria viva, o una correzione fatta oggi su un altro documento cambierebbe sotto gli
/// occhi del lettore un testo già pubblicato.</param>
public sealed record AccDocumentModel(
    int DocumentId, int VersionId, bool IsDraft, string AccCode, string AccName,
    IReadOnlyList<AccAssembledBlock> Blocks, string? AiracCycle = null,
    Language? Language = null, Dictionary<string, Dictionary<string, FrozenTranslation>>? Translations = null)
{
    public AccVipiData Data => new()
    {
        AccCode = AccCode,
        AccName = AccName,
        Blocks = Blocks.Select(b => b.Block).ToList(),
    };
}

/// <summary>Sezioni derivate della vIPI ACC risolte per la vista (frozen o live), indicizzate per <c>AccBlock.Key</c>
/// (doc 10 §3d). config-table/aree non compaiono: derivano da input già congelati → sempre live nella pagina.</summary>
public sealed record AccDerivedSections(
    IReadOnlyDictionary<string, IReadOnlyList<AppFreqRow>> Freqs,
    IReadOnlyDictionary<string, AccCoordination> Coord,
    IReadOnlyDictionary<string, AccAorView> Aor,
    IReadOnlyDictionary<string, MinimaView> Minima);

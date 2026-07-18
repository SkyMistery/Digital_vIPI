namespace Vipi.Application.Content;

/// <summary>
/// La vIPI ACC caricata dallo storage <c>Document</c> (doc refactor 08e-acc): identità documento/versione + i blocchi
/// assemblati (<see cref="AccAssembledBlock"/>, con Id sezione + <see cref="AccBlock"/> per le derivazioni + mappa
/// chiave-figlia → Id per i salvataggi editoriali). <see cref="Data"/> è la proiezione classica <see cref="AccVipiData"/>
/// (identità + <see cref="AccBlock"/>) per i consumer che non toccano lo storage.
/// </summary>
public sealed record AccDocumentModel(
    int DocumentId, int VersionId, bool IsDraft, string AccCode, string AccName,
    IReadOnlyList<AccAssembledBlock> Blocks)
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
    IReadOnlyDictionary<string, AccAorView> Aor);

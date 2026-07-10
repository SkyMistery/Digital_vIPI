namespace Vipi.Application.Content;

/// <summary>
/// La vIPI ACC caricata dallo storage <c>Document</c> (doc refactor 08e-acc): identità documento/versione + i blocchi
/// assemblati (<see cref="AccAssembledBlock"/>, con Id sezione + <see cref="AccBlock"/> per le derivazioni + mappa
/// chiave-figlia → Id per i salvataggi editoriali). <see cref="Data"/> è la proiezione classica <see cref="AccProfileData"/>
/// (identità + <see cref="AccBlock"/>) per i consumer che non toccano lo storage.
/// </summary>
public sealed record AccDocumentModel(
    int DocumentId, int VersionId, bool IsDraft, string AccCode, string AccName,
    IReadOnlyList<AccAssembledBlock> Blocks)
{
    public AccProfileData Data => new()
    {
        AccCode = AccCode,
        AccName = AccName,
        Blocks = Blocks.Select(b => b.Block).ToList(),
    };
}

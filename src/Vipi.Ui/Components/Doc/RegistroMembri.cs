namespace Vipi.Ui.Components.Doc;

/// <summary>
/// Gli editor dei membri di un'unione, <b>per documento</b>: chi c'è adesso, in che ordine, e chi ricaricare.
///
/// <para>🔴 Esiste per la §S2 del filone sito (23 settembre 2026): l'elenco era una lista in cui i membri si
/// aggiungevano e basta. Un membro tolto dall'unione, o rimontato, restava dentro col suo scope già chiuso —
/// e per questo l'ospite, dopo «Hide» nella scheda delle sezioni comuni, ricaricava solo sé stesso: chiedere
/// ai membri di ricaricarsi voleva dire chiederlo anche a quelli smontati, cioè una
/// <c>ObjectDisposedException</c>. Le sezioni nascoste restavano a schermo nei membri fino al ricarico.</para>
///
/// <para>⚠️ La chiave è il <b>documento</b> e non l'istanza: un membro tolto e rimesso è un componente
/// NUOVO per lo stesso documento, e deve prendere il posto del vecchio invece di stargli accanto — due
/// voci vorrebbero dire due lock chiesti e due gruppi nell'indice.</para>
/// </summary>
public sealed class RegistroMembri
{
    private readonly List<(int DocumentId, IMembroEditor Editor)> _voci = new();

    /// <summary>Gli editor dei membri, nell'ordine dell'unione (quello dell'ultimo <see cref="TieniSolo"/>).</summary>
    public IReadOnlyList<IMembroEditor> Editor => _voci.Select(v => v.Editor).ToList();

    /// <summary>
    /// Registra l'editor del documento <paramref name="documentId"/>. <c>false</c> se era già lui: nessun
    /// cambiamento. Un editor diverso per lo stesso documento <b>sostituisce</b> il vecchio, al suo posto.
    /// </summary>
    public bool Registra(int documentId, IMembroEditor editor)
    {
        var i = _voci.FindIndex(v => v.DocumentId == documentId);
        if (i < 0)
        {
            _voci.Add((documentId, editor));
            return true;
        }
        if (ReferenceEquals(_voci[i].Editor, editor)) return false;
        _voci[i] = (documentId, editor);
        return true;
    }

    /// <summary>
    /// Tiene solo i documenti che sono ancora membri, <b>nell'ordine dato</b>. <c>true</c> se qualcosa è
    /// cambiato.
    /// <para>⚠️ Anche l'ordine: dopo «Sposta» l'indice unito segue l'unione, non l'ordine in cui i
    /// componenti si erano presentati.</para>
    /// </summary>
    public bool TieniSolo(IReadOnlyList<int> documentIds)
    {
        var prima = _voci.Select(v => v.DocumentId).ToList();
        _voci.RemoveAll(v => !documentIds.Contains(v.DocumentId));
        _voci.Sort((a, b) => IndexOf(documentIds, a.DocumentId).CompareTo(IndexOf(documentIds, b.DocumentId)));
        return !prima.SequenceEqual(_voci.Select(v => v.DocumentId));
    }

    /// <summary>
    /// Gli editor da ricaricare: quelli dei documenti <paramref name="documentIds"/>, e basta.
    /// <para>⚠️ L'insieme lo dà l'ospite dopo aver riletto l'unione, perché questo registro si aggiorna
    /// solo al ridisegno: un membro appena tolto è ancora qui dentro, e sta per essere smontato.</para>
    /// </summary>
    public IReadOnlyList<IMembroEditor> Di(IReadOnlyCollection<int> documentIds) =>
        _voci.Where(v => documentIds.Contains(v.DocumentId)).Select(v => v.Editor).ToList();

    private static int IndexOf(IReadOnlyList<int> ids, int id)
    {
        for (var i = 0; i < ids.Count; i++) if (ids[i] == id) return i;
        return int.MaxValue;
    }
}

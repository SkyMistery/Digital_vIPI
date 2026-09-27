namespace Vipi.Ui.Components.Doc;

/// <summary>
/// I membri di un'unione il cui lock è stato <b>negato</b>, e da chi: non si riprovano finché non c'è un gesto.
///
/// <para>🔴 U-052 (revisione totale 3). <c>UnionMembersEditor</c> assicura il lock dei membri a OGNI render
/// (<c>OnAfterRenderAsync</c>): un membro caricato fuori modifica, col lock di un collega, rifaceva la presa —
/// lock più ricarico, una decina di query — e il rifiuto avvisava l'ospite, che si ridisegnava, che ridisegnava
/// i membri, che riprovavano. Un giro continuo finché il collega non mollava. Il flag «sto prendendo» non lo
/// fermava: su Blazor Server <c>OnAfterRender</c> arriva dopo la conferma del browser, a flag già spento.</para>
///
/// <para>Si dimentica tutto a un <b>gesto</b> — «Modifica», un ricarico, un membro che entra o esce — e un
/// rifiuto si racconta all'ospite solo se è <b>nuovo</b>: la prima volta, o se il lock è passato a un altro.</para>
/// </summary>
public sealed class LockNegatiDeiMembri
{
    private readonly Dictionary<int, string> _negati = new();

    /// <summary>Vero se il lock di questo documento si può chiedere adesso.</summary>
    public bool DaProvare(int documentId) => !_negati.ContainsKey(documentId);

    /// <summary>Il lock è stato negato: vero se è una notizia (prima volta, o un altro che lo tiene).</summary>
    public bool Negato(int documentId, string chi)
    {
        var nuovo = !_negati.TryGetValue(documentId, out var prima) || !string.Equals(prima, chi, StringComparison.Ordinal);
        _negati[documentId] = chi;
        return nuovo;
    }

    /// <summary>Il lock è stato preso: il documento esce dall'elenco.</summary>
    public void Preso(int documentId) => _negati.Remove(documentId);

    /// <summary>Un gesto: da adesso si riprova tutto.</summary>
    public void Dimentica() => _negati.Clear();
}

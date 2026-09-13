using Vipi.Application.Content;

namespace Vipi.Ui;

/// <summary>
/// Il lock preso dall'hub documenti per un gesto solo — pubblica o scarta dalla lista, senza aprire l'editor.
///
/// <para>🔴 <b>T-041</b> (revisione del 13 settembre 2026): se il gesto falliva, il lock restava preso per trenta
/// minuti e il documento chiuso a tutti gli altri editor. Si rilascia sull'errore, ma <b>solo se l'ha preso questo
/// gesto</b>: un lock che era già mio è di un editor aperto in un'altra scheda, e toglierglielo vorrebbe dire
/// chiudergli il lavoro sotto le mani.</para>
/// </summary>
public static class LockDellaLista
{
    public static async Task EseguiAsync(IEditingService editing, int? documentId, Func<Task> gesto)
    {
        if (documentId is not int docId) { await gesto(); return; }

        var prima = await editing.InspectLockAsync(docId);
        var eraMio = prima.Locked && prima.IsMine;
        await editing.AcquireLockAsync(docId);
        try { await gesto(); }
        catch
        {
            if (!eraMio)
            {
                try { await editing.ReleaseLockAsync(docId); }
                catch (Exception) { /* il messaggio da mostrare è quello del gesto */ }
            }
            throw;
        }
    }
}

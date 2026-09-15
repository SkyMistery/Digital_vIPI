namespace Vipi.Application.Content;

/// <summary>Che cosa occupa un posto nel corpo di una sezione.</summary>
public enum TipoVoce
{
    /// <summary>Un blocco della sezione.</summary>
    Blocco,

    /// <summary>Una sotto-sezione.</summary>
    Figlia,

    /// <summary>La scheda che la PAGINA disegna sulle sezioni derivate (frequenze, coordinamenti…).</summary>
    Scheda,
}

/// <summary>Un posto nella fila del corpo: il tipo e l'id (blocco o sezione; 0 per la scheda).</summary>
public readonly record struct VoceCorpo(TipoVoce Tipo, int Id);

/// <summary>L'esito di <see cref="CorpoDiSezione.Pianifica"/>: i nuovi <c>Order</c> dei blocchi, e posizione e
/// <c>Order</c> delle sotto-sezioni.</summary>
public sealed record PianoCorpo(
    IReadOnlyList<(int Id, int Order)> Blocchi,
    IReadOnlyList<(int Id, int Posizione, int Order)> Figlie);

/// <summary>
/// Il corpo di una sezione come UNA fila di blocchi e sotto-sezioni alternati (15 settembre 2026, committente:
/// «sottosezione, blocco della sezione principale, sottosezione, blocco»).
///
/// <para>Prima il corpo era a tre scomparti fissi — figlie «prima», blocchi, figlie «dopo» (doc 11 §3g) — e
/// l'alternanza non si poteva scrivere. Qui sta l'unica risposta a «in che ordine vengono», e la usano viewer,
/// editor e stampa: tre copie della stessa fusione sarebbero tre fusioni diverse al primo ritocco.</para>
///
/// <para>⚠️ La posizione di una figlia è una <b>soglia</b> sull'<c>Order</c> dei blocchi del padre
/// (<c>DocumentSection.BodyPosition</c>), non un conteggio: un blocco cancellato lascia un buco negli
/// <c>Order</c>, e un conteggio farebbe saltare la figlia di un posto da sola.</para>
/// </summary>
public static class CorpoDiSezione
{
    /// <summary>In testa: prima anche della scheda della pagina.</summary>
    public const int InTesta = -1;

    /// <summary>
    /// La soglia effettiva di una sotto-sezione. Senza posizione scritta vale il flag storico: in testa o in
    /// CODA a tutto — che è come si leggono i documenti e le release scritti prima del 15 settembre 2026.
    /// </summary>
    public static int Soglia(int? bodyPosition, bool beforeParentBody) =>
        bodyPosition ?? (beforeParentBody ? InTesta : int.MaxValue);

    /// <summary>
    /// La fila del corpo. I blocchi in ordine di <paramref name="ordine"/>; ogni figlia prima del primo blocco
    /// con <c>Order</c> maggiore della sua soglia, a parità nell'ordine in cui arriva (cioè il suo <c>Order</c>).
    /// Con <paramref name="conScheda"/> la scheda sta dopo le figlie «in testa» e prima di tutto il resto.
    /// </summary>
    public static IReadOnlyList<(TipoVoce Tipo, TB? Blocco, TS? Figlia)> Componi<TB, TS>(
        IEnumerable<TB> blocchi, Func<TB, int> ordine,
        IEnumerable<TS> figlie, Func<TS, int> soglia,
        bool conScheda)
    {
        var bl = blocchi.Select((b, i) => (b, i)).OrderBy(x => ordine(x.b)).ThenBy(x => x.i).Select(x => x.b).ToList();
        var fi = figlie.Select((f, i) => (f, i)).OrderBy(x => soglia(x.f)).ThenBy(x => x.i).Select(x => x.f).ToList();

        var fila = new List<(TipoVoce, TB?, TS?)>(bl.Count + fi.Count + 1);
        var k = 0;
        while (k < fi.Count && soglia(fi[k]) < 0) fila.Add((TipoVoce.Figlia, default, fi[k++]));
        if (conScheda) fila.Add((TipoVoce.Scheda, default, default));

        foreach (var b in bl)
        {
            while (k < fi.Count && soglia(fi[k]) < ordine(b)) fila.Add((TipoVoce.Figlia, default, fi[k++]));
            fila.Add((TipoVoce.Blocco, b, default));
        }
        while (k < fi.Count) fila.Add((TipoVoce.Figlia, default, fi[k++]));
        return fila;
    }

    /// <summary>
    /// La fila con la voce <paramref name="indice"/> scambiata con la vicina (<paramref name="direzione"/> -1 su,
    /// +1 giù). Null se non c'è niente da fare: ai bordi, o se un <b>blocco</b> proverebbe a scavalcare la scheda —
    /// i blocchi di una sezione derivata stanno sempre sotto la scheda, ci passano solo le sotto-sezioni.
    /// </summary>
    public static IReadOnlyList<VoceCorpo>? Sposta(IReadOnlyList<VoceCorpo> fila, int indice, int direzione)
    {
        var j = indice + Math.Sign(direzione);
        if (indice < 0 || indice >= fila.Count || j < 0 || j >= fila.Count || j == indice) return null;
        var a = fila[indice].Tipo;
        var b = fila[j].Tipo;
        if ((a == TipoVoce.Blocco && b == TipoVoce.Scheda) || (a == TipoVoce.Scheda && b == TipoVoce.Blocco)) return null;

        var nuova = fila.ToList();
        (nuova[indice], nuova[j]) = (nuova[j], nuova[indice]);
        return nuova;
    }

    /// <summary>
    /// Traduce una fila in numeri da scrivere. <paramref name="tuttiIBlocchi"/> sono TUTTI i blocchi della sezione
    /// con l'<c>Order</c> attuale, anche quelli che la fila non mostra (il payload di una scheda): quelli restano
    /// al loro posto fra i blocchi, e gli altri si ridistribuiscono negli spazi liberi nell'ordine della fila.
    /// I blocchi si rinumerano densi da 1; ogni figlia prende come soglia l'<c>Order</c> dell'ultimo blocco
    /// che la precede (0 se nessuno), o <see cref="InTesta"/> se viene prima della scheda.
    /// </summary>
    public static PianoCorpo Pianifica(IReadOnlyList<VoceCorpo> fila, IReadOnlyList<(int Id, int Order)> tuttiIBlocchi)
    {
        var inFila = fila.Where(v => v.Tipo == TipoVoce.Blocco).Select(v => v.Id).ToList();
        var mostrati = inFila.ToHashSet();

        var attuali = tuttiIBlocchi.OrderBy(b => b.Order).ThenBy(b => b.Id).Select(b => b.Id).ToList();
        var coda = new Queue<int>(inFila.Where(id => attuali.Contains(id)));
        var ordinati = attuali.Select(id => mostrati.Contains(id) ? coda.Dequeue() : id).ToList();

        var nuovoOrder = new Dictionary<int, int>();
        for (var i = 0; i < ordinati.Count; i++) nuovoOrder[ordinati[i]] = i + 1;

        var conScheda = fila.Any(v => v.Tipo == TipoVoce.Scheda);
        var schedaPassata = !conScheda;
        var ultimo = 0;
        var figlie = new List<(int, int, int)>();
        foreach (var v in fila)
        {
            switch (v.Tipo)
            {
                case TipoVoce.Scheda: schedaPassata = true; break;
                case TipoVoce.Blocco: if (nuovoOrder.TryGetValue(v.Id, out var o)) ultimo = o; break;
                case TipoVoce.Figlia:
                    figlie.Add((v.Id, schedaPassata ? ultimo : InTesta, figlie.Count + 1));
                    break;
            }
        }

        return new PianoCorpo(ordinati.Select(id => (id, nuovoOrder[id])).ToList(), figlie);
    }
}

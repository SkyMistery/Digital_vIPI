namespace Vipi.Application.Content;

/// <summary>
/// «Sto congelando una release per <b>questo</b> ciclo AIRAC»: l'unico posto in cui una derivazione non deve
/// dare lo stato più recente ma quello <b>in vigore</b> a quel ciclo.
///
/// <para>Sono <b>due</b> le cose che lo chiedono, e il nome del tipo ne ricorda una sola per ragioni
/// storiche: le <b>shape</b> dei settori (la geometria in vigore a quel ciclo) e le <b>SID</b> d'aeroporto,
/// che compaiono solo dal ciclo successivo al prelievo — quindi «quali SID ci sono» ha risposte diverse a
/// cicli diversi, esattamente come la geometria.</para>
///
/// <para><b>Perché un oggetto e non un parametro.</b> Il ciclo lo conosce <c>ReleaseService</c>, e la
/// geometria la legge il repository di derivazione: in mezzo ci sono i provider di cattura e i servizi di
/// derivazione, sei firme che dovrebbero portarsi dietro un parametro che riguarda uno solo dei loro
/// chiamanti. Il tipo ha un nome che si cerca, un valore solo, e un unico posto che lo imposta.</para>
///
/// <para>⚠️ È <b>scoped</b> come il DbContext, quindi vale per una richiesta sola: non è uno stato globale.
/// Fuori dal congelamento <see cref="Cycle"/> è null, e le shape si leggono come sempre — è il caso
/// dell'editor, della derivazione live e di tutto il resto.</para>
/// </summary>
public sealed class ShapeReleaseContext
{
    /// <summary>Il ciclo per cui si sta congelando, o null fuori dal congelamento.</summary>
    public string? Cycle { get; private set; }

    /// <summary>Apre il contesto per la durata del blocco. Annidarlo non serve e non si fa: la cattura è una.</summary>
    public IDisposable Capturing(string cycle)
    {
        Cycle = cycle;
        return new Scope(this);
    }

    private sealed class Scope : IDisposable
    {
        private readonly ShapeReleaseContext _ctx;
        public Scope(ShapeReleaseContext ctx) => _ctx = ctx;
        public void Dispose() => _ctx.Cycle = null;
    }
}

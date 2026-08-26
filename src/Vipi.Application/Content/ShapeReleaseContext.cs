namespace Vipi.Application.Content;

/// <summary>
/// «Sto congelando una release per <b>questo</b> ciclo AIRAC»: l'unico posto in cui la lettura delle shape
/// non deve dare la geometria più recente ma quella <b>in vigore</b> a quel ciclo.
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

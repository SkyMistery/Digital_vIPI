using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Vipi.Ui.Components;

/// <summary>
/// Chi rende la pagina «non esiste» di un indirizzo inventato può dire alla risposta di essere un <b>404</b>.
/// Lo implementa l'host, che la richiesta HTTP ce l'ha; la UI non la vede e non deve vederla.
/// </summary>
public interface IStatoDellaRisposta
{
    /// <summary>La pagina che si sta rendendo dice che la cosa chiesta non esiste.</summary>
    void NonTrovato();
}

/// <summary>
/// Da mettere dentro il ramo «non esiste» di una pagina pubblica: non disegna niente, fa rispondere 404.
///
/// <para>🔴 <b>Perché (T-084, revisione del 13 settembre 2026).</b> Misurato in produzione:
/// <c>/services/vsop/nonexistent-xyz</c> rispondeva <b>200</b> «ACC sconosciuto» con
/// <c>Cache-Control: public</c>. Ogni percorso inventato diventava una copia tenuta in cache e una pagina
/// indicizzabile: un modo gratuito, per chiunque, di riempire la cache e i motori di ricerca.</para>
///
/// <para>⚠️ Il servizio è <b>facoltativo</b>: nei test di componente non c'è una richiesta, e un
/// <c>@inject</c> obbligatorio farebbe cadere il render per una cosa che lì non ha senso.</para>
/// </summary>
public sealed class NonTrovato : ComponentBase
{
    [Inject] private IServiceProvider Servizi { get; set; } = default!;

    protected override void OnInitialized() => Servizi.GetService<IStatoDellaRisposta>()?.NonTrovato();
}

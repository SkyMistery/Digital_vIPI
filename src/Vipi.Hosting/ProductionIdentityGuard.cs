namespace Vipi.Hosting;

/// <summary>
/// Guardia di sicurezza all'avvio (audit D1): l'identità di sviluppo (<see cref="DevCurrentUserProvider"/>)
/// impersona un utente admin onnipotente con fallback statico. In un ambiente NON di sviluppo questo sarebbe
/// un bypass totale dell'autorizzazione. La guardia rileva la combinazione pericolosa e la fa fallire all'avvio,
/// prima che l'app serva richieste. Logica pura ⇒ testabile senza host.
/// </summary>
public static class ProductionIdentityGuard
{
    /// <summary>
    /// Ritorna un messaggio d'errore se la configurazione d'identità è insicura per l'ambiente, altrimenti null.
    /// </summary>
    /// <param name="isDevelopmentEnvironment">true se l'ambiente ospitante è "Development".</param>
    /// <param name="useDevIdentity">true se il modulo è stato montato con l'identità dev fittizia.</param>
    public static string? Validate(bool isDevelopmentEnvironment, bool useDevIdentity)
    {
        if (useDevIdentity && !isDevelopmentEnvironment)
            return "Identità di sviluppo (DevCurrentUserProvider) attiva in un ambiente non-Development: " +
                   "sarebbe un bypass totale dell'autorizzazione (admin onnipotente). " +
                   "Monta il modulo con useDevIdentity:false in produzione (identità dal login del sito host).";

        return null;
    }

    /// <summary>Applica la guardia: lancia <see cref="InvalidOperationException"/> se la config è insicura.</summary>
    public static void EnsureSafe(bool isDevelopmentEnvironment, bool useDevIdentity)
    {
        if (Validate(isDevelopmentEnvironment, useDevIdentity) is { } error)
            throw new InvalidOperationException(error);
    }
}

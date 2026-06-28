namespace Vipi.Infrastructure;

/// <summary>
/// Selettore della sorgente dati esterna (sezione "DataSource" di appsettings). Disaccoppia l'app dalla
/// rete: le interfacce <c>IAirportDirectory</c>/<c>IAirportDetailProvider</c>/<c>IUserDirectory</c> ecc. sono
/// neutre; qui si sceglie quale implementazione (adapter) le serve. Oggi "Ivao"; in futuro un altro network
/// o un DB interno basta registrare un nuovo adapter e cambiare questo valore.
/// </summary>
public sealed class DataSourceOptions
{
    public const string SectionName = "DataSource";

    /// <summary>Provider attivo. Valori supportati oggi: "Ivao". Un valore sconosciuto fa fallire l'avvio.</summary>
    public string Provider { get; set; } = "Ivao";
}

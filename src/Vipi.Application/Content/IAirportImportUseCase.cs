namespace Vipi.Application.Content;

/// <summary>
/// Core import anagrafica aeroporti (punto 3): scarica l'anagrafica dalla sorgente, auto-assegna gli
/// aeroporti noti alla loro ACC di competenza e importa il catalogo settori di ciascuno. Riproietta i
/// Sector al termine. Doc refactor 03 §4.2. Nessun controllo di autorizzazione qui: lo applica il chiamante.
/// La generazione del documento è un passo separato (scollegata dall'import, doc 03 §4.3 / doc 08).
/// </summary>
public interface IAirportImportUseCase
{
    /// <summary>Esegue anagrafica → auto-assegna → import settori. Ritorna assegnati + aeroporti saltati.</summary>
    Task<AirportImportResult> RunAsync(CancellationToken ct = default);
}

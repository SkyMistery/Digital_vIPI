namespace Vipi.Application.Content;

/// <summary>
/// Orchestratore (senza authz) dell'import dei settori ATC di un aeroporto dalla sorgente:
/// lista postazioni → dettaglio per posizione (freq/shape/limiti) → upsert nel catalogo.
/// Dipende solo dalle porte neutre (nessun service): riusato da <see cref="IAirportSectorService"/>
/// (wrapper ACC-gated), dal job di import automatico e dalla generazione documento.
/// </summary>
public interface IAirportSectorImporter
{
    /// <summary>Importa/aggiorna i settori (incl. APP) dell'aeroporto. Ritorna (creati, aggiornati).</summary>
    Task<(int Created, int Updated)> ImportAsync(string icao, CancellationToken ct = default);
}

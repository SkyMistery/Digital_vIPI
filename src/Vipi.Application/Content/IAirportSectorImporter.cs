namespace Vipi.Application.Content;

/// <summary>
/// Orchestratore (senza authz) dell'import dei settori ATC di un aeroporto dalla sorgente:
/// lista postazioni → dettaglio per posizione (freq/shape/limiti) → upsert nel catalogo.
/// Dipende solo dalle porte neutre (nessun service): riusato da <see cref="IAirportSectorService"/>
/// (wrapper ACC-gated), dal job di import automatico e dalla generazione documento.
/// <para>⚠️ Rispetta la policy di import globale (<c>ImportCategory.Sectors</c>): categoria esclusa =
/// nessuna fetch e nessuna scrittura, per <b>tutti</b> i chiamanti. Il gate sta nell'implementazione
/// perché è il corpo condiviso auto/manual (vedi il commento in <c>AirportSectorImporter</c>).</para>
/// </summary>
public interface IAirportSectorImporter
{
    /// <summary>Importa/aggiorna i settori (incl. APP) dell'aeroporto. Ritorna (creati, aggiornati).</summary>
    Task<(int Created, int Updated)> ImportAsync(string icao, CancellationToken ct = default);
}

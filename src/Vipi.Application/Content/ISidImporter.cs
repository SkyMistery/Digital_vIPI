namespace Vipi.Application.Content;

/// <summary>Import SID di un aeroporto dalla sorgente (sectorfile) nel profilo strutturato, rispettando la policy.</summary>
public interface ISidImporter
{
    /// <summary>
    /// Importa/aggiorna le SID di <paramref name="icao"/>: rimpiazza le importate precedenti, preserva
    /// manuali/priorità/forzatura. No-op se la policy Sids è disattivata o la sorgente non ha il file.
    ///
    /// <para><b>Operazione di sistema, senza controllo di autorizzazione</b>: la chiama il job periodico,
    /// che gira senza utente. Dalla UI si usa <see cref="ImportForCurrentUserAsync"/>.</para>
    /// </summary>
    Task<int> ImportAsync(string icao, CancellationToken ct = default);

    /// <summary>
    /// Come <see cref="ImportAsync"/>, ma prima verifica che l'utente corrente possa editare la ACC
    /// dell'aeroporto. È l'ingresso della UI (bottone «Re-import SID» nell'editor aeroporto).
    ///
    /// <para><b>Perché due metodi.</b> Questo importatore riscrive righe — <c>ReplaceImportedSidsAsync</c>
    /// fa delete+add — ed era l'unico percorso di scrittura del progetto senza <c>EnsureCanEdit*</c>, fra
    /// oltre sessanta chiamate su venti servizi. Non era sfruttabile (Blazor consegna solo gli eventi
    /// dell'albero renderizzato, e il bottone sta dietro il controllo di editing della pagina) ma il
    /// principio è scritto in cima a <c>IEditAuthorizationService</c>: «verifica sempre server-side». Il
    /// modello dei due ingressi è quello già usato in <c>AccAdminService</c> e <c>StructureEditingService</c>,
    /// dove il commento dice «solo il manual applica il guard».</para>
    /// </summary>
    Task<int> ImportForCurrentUserAsync(string icao, CancellationToken ct = default);
}

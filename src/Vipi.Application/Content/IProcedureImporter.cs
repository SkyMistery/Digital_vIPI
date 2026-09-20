namespace Vipi.Application.Content;

/// <summary>Import delle procedure strumentali di un aeroporto — partenze <b>e</b> arrivi — dalla sorgente
/// (sectorfile) nell'anagrafica, rispettando la policy.</summary>
public interface IProcedureImporter
{
    /// <summary>
    /// Importa/aggiorna le procedure di <paramref name="icao"/>, <b>SID e STAR</b>: rimpiazza le importate
    /// precedenti di ciascun verso, preserva manuali/priorità/forzatura. No-op se la policy Sids è disattivata
    /// o la sorgente non ha il file; torna il numero di righe scritte, i due versi sommati.
    ///
    /// <para>⚠️ <b>Una categoria d'import sola per i due versi</b> (<c>ImportCategory.Sids</c>): sono lo stesso
    /// dato dalla stessa sorgente, nello stesso giro, e due interruttori chiederebbero allo staff una decisione
    /// che nessuno ha chiesto di poter prendere. Un verso che la sorgente non ha — 36 dei 90 <c>.str</c> non
    /// portano nemmeno una STAR — semplicemente non scrive niente e non cancella niente.</para>
    ///
    /// <para><b>Operazione di sistema, senza controllo di autorizzazione</b>: la chiama il job periodico,
    /// che gira senza utente. Dalla UI si usa <see cref="ImportForCurrentUserAsync"/>.</para>
    /// </summary>
    Task<int> ImportAsync(string icao, CancellationToken ct = default);

    /// <summary>
    /// Come <see cref="ImportAsync"/>, ma prima verifica che l'utente corrente possa editare la ACC
    /// dell'aeroporto. È l'ingresso della UI (bottone «Re-import SID» nell'editor aeroporto).
    ///
    /// <para><b>Perché due metodi.</b> Questo importatore riscrive righe — <c>ReplaceImportedProceduresAsync</c>
    /// fa delete+add — ed era l'unico percorso di scrittura del progetto senza <c>EnsureCanEdit*</c>, fra
    /// oltre sessanta chiamate su venti servizi. Non era sfruttabile (Blazor consegna solo gli eventi
    /// dell'albero renderizzato, e il bottone sta dietro il controllo di editing della pagina) ma il
    /// principio è scritto in cima a <c>IEditAuthorizationService</c>: «verifica sempre server-side». Il
    /// modello dei due ingressi è quello già usato in <c>AccAdminService</c> e <c>StructureEditingService</c>,
    /// dove il commento dice «solo il manual applica il guard».</para>
    /// </summary>
    Task<int> ImportForCurrentUserAsync(string icao, CancellationToken ct = default);
}

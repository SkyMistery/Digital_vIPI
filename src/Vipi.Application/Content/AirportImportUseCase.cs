using Vipi.Application.Abstractions;

namespace Vipi.Application.Content;

/// <inheritdoc cref="IAirportImportUseCase"/>
public sealed class AirportImportUseCase : IAirportImportUseCase
{
    private readonly IAirportDirectory _directory;
    private readonly IStructureEditingRepository _repo;
    private readonly IAirportSectorImporter _sectorImporter;
    private readonly ISectorProjectionService _projection;
    private readonly IImportStateStore? _stati;

    /// <param name="stati">
    /// Il registro dei giri riusciti. ⚠️ Lo timbra <b>questo</b> caso d'uso e non solo il giro notturno,
    /// perché il bottone «Assegna aeroporti noti» è l'altro chiamante e fa esattamente lo stesso lavoro: se
    /// timbrasse solo l'automatico, «due chiamate a mano» non conterebbe mai e la regola di
    /// <see cref="SogliaEliminazione"/> non scatterebbe per chi lavora a mano. Il doppio timbro del giro
    /// automatico — questo, e poi quello di <c>GatedImportLoop</c> — è innocuo: arrivano a pochi
    /// millisecondi l'uno dall'altro e il penultimo non scorre due volte.
    /// </param>
    public AirportImportUseCase(IAirportDirectory directory, IStructureEditingRepository repo,
        IAirportSectorImporter sectorImporter, ISectorProjectionService projection,
        IImportStateStore? stati = null)
    {
        _directory = directory;
        _repo = repo;
        _sectorImporter = sectorImporter;
        _projection = projection;
        _stati = stati;
    }

    public async Task<AirportImportResult> RunAsync(CancellationToken ct = default)
    {
        var ivao = await _directory.GetAirportsAsync(ct);
        var candidates = ivao
            .Where(a => !string.IsNullOrWhiteSpace(a.AccCode))
            .Select(a => (AccCode: a.AccCode!, a.Icao, a.Name))
            .ToList();
        var assigned = await _repo.AutoAssignAirportsAsync(candidates, ct);

        // Subito dopo l'assegnazione, e sull'elenco INTERO: l'assegnazione e' additiva (salta gli ICAO gia' in
        // archivio), quindi senza questo passo i campi anagrafici resterebbero al loro default su tutti gli
        // aeroporti gia' presenti — e su nessuno di quelli il giro passerebbe mai piu'.
        var refreshed = await _repo.SyncAirportSourceFieldsAsync(ivao, ct);
        // ⚠️ Qui NON si chiama piu' IStationCatalogVersion.Bump(): la spinta la da'
        // BumpCatalogoStazioniInterceptor, sul salvataggio, per chiunque scriva un Acc o un Airport.
        // Il motivo del trasloco e' un conto: al 31 agosto 2026 le chiamate a mano erano QUATTRO e i
        // posti che scrivono quelle due tabelle UNDICI. Vedi CatalogoStazioni.

        // Per ogni aeroporto appena assegnato: importa il catalogo settori (DEL/GND/TWR/APP) dalla sorgente, così
        // compaiono subito i settori. La documentazione vIPI resta un passo a parte («Genera documenti»).
        var failures = new List<AirportImportFailure>();
        foreach (var icao in assigned)
        {
            // Aeroporto senza settori nella sorgente o sorgente non disponibile: salta e riporta (il chiamante logga).
            try { await _sectorImporter.ImportAsync(icao, ct); }
            catch (Exception ex) { failures.Add(new AirportImportFailure(icao, ex)); }
        }
        // Proietta i cataloghi aggiornati nei Sector operativi (fonte autoritativa unica, Round 20).
        if (assigned.Count > 0) await _projection.SyncFromCatalogsAsync(ct);

        // Il giro è arrivato in fondo: timbralo. Vale sia per il giro notturno sia per il bottone.
        if (_stati is not null)
            await _stati.MarkSuccessAsync(ImportCategories.AirportDirectory, DateTime.UtcNow, ct);

        return new AirportImportResult(assigned.Count, failures, refreshed);
    }
}

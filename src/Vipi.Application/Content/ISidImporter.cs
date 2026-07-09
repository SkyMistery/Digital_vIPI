namespace Vipi.Application.Content;

/// <summary>Import SID di un aeroporto dalla sorgente (sectorfile) nel profilo strutturato, rispettando la policy.</summary>
public interface ISidImporter
{
    /// <summary>Importa/aggiorna le SID di <paramref name="icao"/>: rimpiazza le importate precedenti, preserva
    /// manuali/priorità/forzatura. No-op se la policy Sids è disattivata o la sorgente non ha il file.</summary>
    Task<int> ImportAsync(string icao, CancellationToken ct = default);
}

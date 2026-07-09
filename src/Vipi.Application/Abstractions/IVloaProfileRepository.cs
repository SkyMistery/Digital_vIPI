namespace Vipi.Application.Abstractions;

/// <summary>
/// Coppia risolta di una vLOA: i due ACC (Home italiano / Neighbour estero), i settori confinanti calcolati
/// dall'import (per AoR/frequenze), e l'insieme completo dei settori di ciascun ACC (per i coordinamenti, che
/// possono coinvolgere qualunque settore dei due ACC, non solo quelli di confine).
/// </summary>
public sealed record VloaPairInfo(
    string HomeAcc, string ForeignAcc, string HomeName, string ForeignName,
    IReadOnlyList<string> HomeConfining, IReadOnlyList<string> ForeignConfining,
    IReadOnlyList<string> HomeAll, IReadOnlyList<string> ForeignAll,
    string ForeignCountry);

/// <summary>Stato editoriale persistito di una vLOA: callsign nascosti da AoR/frequenze + titoli di sezioni nascoste.</summary>
public sealed record VloaProfileState(
    IReadOnlyList<string> HiddenAorSectors, IReadOnlyList<string> HiddenFrequencies,
    IReadOnlyList<string> HiddenSections);

/// <summary>Settore di confine (CTR/FSS) di un ACC col suo poligono grezzo, per il calcolo di adiacenza al volo.</summary>
public sealed record VloaSectorPoly(string Callsign, string Raw);

/// <summary>Persistenza dello stato data-driven della vLOA (VloaProfile) e risoluzione della coppia/settori confinanti.</summary>
public interface IVloaProfileRepository
{
    /// <summary>Risolve la coppia di una vLOA dai suoi <c>DocumentParty</c> + i settori confinanti persistiti
    /// (<c>NeighbourCandidate</c>), con fallback a tutti i settori di confine dei due ACC. null se il doc non è una vLOA.</summary>
    Task<VloaPairInfo?> GetPairAsync(int docId, CancellationToken ct = default);

    /// <summary>Settori di confine (CTR/FSS con poligono) di un ACC dal catalogo, per il calcolo di adiacenza al volo.</summary>
    Task<IReadOnlyList<VloaSectorPoly>> GetBoundaryPolygonsAsync(string accCode, CancellationToken ct = default);

    /// <summary>Carica lo stato editoriale (insiemi nascosti). Vuoto se non esiste ancora un VloaProfile.</summary>
    Task<VloaProfileState> LoadProfileAsync(int docId, CancellationToken ct = default);

    /// <summary>Upsert dello stato editoriale (insiemi nascosti) per la vLOA.</summary>
    Task SaveProfileAsync(int docId, VloaProfileState state, CancellationToken ct = default);

    /// <summary>Codice ACC Home (italiano) della vLOA, per l'autorizzazione all'editing. null se non risolvibile.</summary>
    Task<string?> GetHomeAccCodeAsync(int docId, CancellationToken ct = default);
}

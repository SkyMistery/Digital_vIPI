using Vipi.Application.Content;
using Vipi.Domain.Entities;

namespace Vipi.Ui.Tests;

/// <summary>
/// Il risolutore delle SID citate nel testo (§A73) per i test che non ne citano: nessun nome, nessuna SID da
/// scegliere. Lo chiedono i componenti che disegnano testo di documento — l'editor delle sezioni per le sue
/// anteprime, l'intro di pagina — e senza una registrazione il contenitore alza «No service for type».
/// </summary>
internal sealed class NessunaSidCitata : IProcedureReferenceResolver
{
    public Task<NomiProcedura> PerVistaAsync(IEnumerable<SectionView> sezioni, bool pubblica,
        string? proprioIcao = null, AirportSidView? propriaTabella = null,
            AirportSidView? propriaTabellaStar = null, CancellationToken ct = default) =>
        Task.FromResult(NomiProcedura.Vuoto);

    public Task<NomiProcedura> PerTestiAsync(IEnumerable<string?> testi, CancellationToken ct = default) =>
        Task.FromResult(NomiProcedura.Vuoto);

    public Task<IReadOnlyList<ProceduraCitabile>> ElencoAsync(string icao, ProcedureKind kind = ProcedureKind.Sid, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ProceduraCitabile>>(Array.Empty<ProceduraCitabile>());
}

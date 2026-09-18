using Vipi.Application.Content;

namespace Vipi.Ui.Tests;

/// <summary>
/// Il risolutore delle SID citate nel testo (§A73) per i test che non ne citano: nessun nome, nessuna SID da
/// scegliere. Lo chiedono i componenti che disegnano testo di documento — l'editor delle sezioni per le sue
/// anteprime, l'intro di pagina — e senza una registrazione il contenitore alza «No service for type».
/// </summary>
internal sealed class NessunaSidCitata : ISidReferenceResolver
{
    public Task<NomiSid> PerVistaAsync(IEnumerable<SectionView> sezioni, bool pubblica,
        string? proprioIcao = null, AirportSidView? propriaTabella = null, CancellationToken ct = default) =>
        Task.FromResult(NomiSid.Vuoto);

    public Task<NomiSid> PerTestiAsync(IEnumerable<string?> testi, CancellationToken ct = default) =>
        Task.FromResult(NomiSid.Vuoto);

    public Task<IReadOnlyList<SidCitabile>> ElencoAsync(string icao, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<SidCitabile>>(Array.Empty<SidCitabile>());
}

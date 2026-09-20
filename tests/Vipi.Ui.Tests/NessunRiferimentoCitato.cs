using Vipi.Application.Content;
using Vipi.Domain.Entities;

namespace Vipi.Ui.Tests;

/// <summary>
/// Il risolutore dei riferimenti (§A73, §A80, dati) per i test che non ne citano: niente nomi, niente valori,
/// niente da scegliere. Lo chiedono i componenti che disegnano testo di documento — l'editor delle sezioni per
/// le sue anteprime, l'intro di pagina — e senza una registrazione il contenitore alza «No service for type».
/// <para>⚠️ Serve le DUE porte: quella delle sole procedure (il selettore la usa per l'elenco) e quella unica
/// (i caricatori e l'editor).</para>
/// </summary>
internal sealed class NessunRiferimentoCitato : IProcedureReferenceResolver, IRiferimentiResolver
{
    Task<RiferimentiRisolti> IRiferimentiResolver.PerVistaAsync(IEnumerable<SectionView> sezioni, bool pubblica,
        string? proprioIcao, AirportSidView? propriaTabella, AirportSidView? propriaTabellaStar, CancellationToken ct) =>
        Task.FromResult(RiferimentiRisolti.Vuoto);

    Task<RiferimentiRisolti> IRiferimentiResolver.PerTestiAsync(IEnumerable<string?> testi, CancellationToken ct) =>
        Task.FromResult(RiferimentiRisolti.Vuoto);

    public Task<NomiProcedura> PerVistaAsync(IEnumerable<SectionView> sezioni, bool pubblica,
        string? proprioIcao = null, AirportSidView? propriaTabella = null,
            AirportSidView? propriaTabellaStar = null, CancellationToken ct = default) =>
        Task.FromResult(NomiProcedura.Vuoto);

    public Task<NomiProcedura> PerTestiAsync(IEnumerable<string?> testi, CancellationToken ct = default) =>
        Task.FromResult(NomiProcedura.Vuoto);

    public Task<IReadOnlyList<ProceduraCitabile>> ElencoAsync(string icao, ProcedureKind kind = ProcedureKind.Sid, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ProceduraCitabile>>(Array.Empty<ProceduraCitabile>());
}

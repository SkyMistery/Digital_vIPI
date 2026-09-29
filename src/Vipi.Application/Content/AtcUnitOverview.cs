using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Dove vive il contenuto di un ente, come lo legge la pagina degli enti.</summary>
public enum AtcUnitStato
{
    /// <summary>Ha la sua vIPI APP.</summary>
    VipiPropria,
    /// <summary>Copiato nella bozza della vIPI ACC; la sua vIPI APP resta in vigore finché quella non va in vigore.</summary>
    InSpostamento,
    /// <summary>Vive in un gruppo APP della vIPI dell'ACC.</summary>
    NellaVipiAcc,
    /// <summary>Ha una vIPI APP propria, ma il documento è stato eliminato.</summary>
    SenzaDocumento,
}

/// <summary>Una posizione dell'ente, e se IVAO la manda ancora (un settore attivo con quel nominativo).</summary>
public sealed record AtcUnitPosizione(string Callsign, bool SuIvao);

/// <summary>Una riga della pagina degli enti.</summary>
/// <param name="Documento">La vIPI APP dell'ente com'è in «Bozze &amp; versioni»; null senza documento.</param>
/// <param name="SpostamentoVerso">L'ACC nella cui bozza è già copiato (solo con <see cref="AtcUnitStato.InSpostamento"/>).</param>
public sealed record AtcUnitOverviewRow(AtcUnitRow Ente, AtcUnitStato Stato, IReadOnlyList<AtcUnitPosizione> Posizioni,
    ManagedDoc? Documento, string? SpostamentoVerso);

/// <summary>
/// La pagina di amministrazione degli enti ATC (S53, committente, 29 settembre 2026): tutti gli enti, per ACC, con le
/// loro posizioni e dove vive il loro contenuto. Prima un ente si vedeva solo aprendo la sua vIPI APP.
/// </summary>
public interface IAtcUnitOverviewService
{
    Task<IReadOnlyList<AtcUnitOverviewRow>> ListAsync(CancellationToken ct = default);
}

/// <inheritdoc cref="IAtcUnitOverviewService"/>
public sealed class AtcUnitOverviewService : IAtcUnitOverviewService
{
    private readonly IAtcUnitRepository _enti;
    private readonly IDocumentAdminRepository _documenti;
    private readonly IRemotizzazioneService _spostamenti;
    private readonly IEditAuthorizationService _authz;

    public AtcUnitOverviewService(IAtcUnitRepository enti, IDocumentAdminRepository documenti,
        IRemotizzazioneService spostamenti, IEditAuthorizationService authz)
    {
        _enti = enti;
        _documenti = documenti;
        _spostamenti = spostamenti;
        _authz = authz;
    }

    public async Task<IReadOnlyList<AtcUnitOverviewRow>> ListAsync(CancellationToken ct = default)
    {
        _authz.EnsureAtLeast(VipiRole.Editor);
        var enti = await _enti.ListAsync(null, ct);
        if (enti.Count == 0) return Array.Empty<AtcUnitOverviewRow>();

        var suIvao = await _enti.ActiveCallsignsAsync(enti.SelectMany(u => u.Positions).Distinct().ToList(), ct);
        var docs = (await _documenti.ListAsync(ct))
            .Where(d => d.Kind == ReleaseTargetType.App && d.DocumentId is not null)
            .GroupBy(d => d.DocumentId!.Value).ToDictionary(g => g.Key, g => g.First());
        var inCorso = await _spostamenti.SpostamentiInCorsoAsync(ct);

        return enti
            .OrderBy(u => u.AccCode, StringComparer.OrdinalIgnoreCase).ThenBy(u => u.Name, StringComparer.OrdinalIgnoreCase)
            .Select(u =>
            {
                var doc = u.DocumentId is int id ? docs.GetValueOrDefault(id) : null;
                var verso = inCorso.GetValueOrDefault(u.Id);
                var stato = u.Mode == AtcUnitMode.InAccVipi ? AtcUnitStato.NellaVipiAcc
                    : verso is not null ? AtcUnitStato.InSpostamento
                    : u.DocumentId is null ? AtcUnitStato.SenzaDocumento
                    : AtcUnitStato.VipiPropria;
                return new AtcUnitOverviewRow(u, stato,
                    u.Positions.Select(p => new AtcUnitPosizione(p, suIvao.Contains(p))).ToList(), doc, verso);
            })
            .ToList();
    }
}

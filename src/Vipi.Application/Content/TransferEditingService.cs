using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Auth;

namespace Vipi.Application.Content;

/// <summary>Use-case trasferimenti: lettura aperta, scrittura ACC-gated + validazione base (soft).</summary>
public interface ITransferService
{
    Task<IReadOnlyList<TransferRow>> ListByAccAsync(string accCode, CancellationToken ct = default);

    /// <summary>Trasferimenti con il "primo online" risolto sull'ATC online corrente (F3).</summary>
    Task<IReadOnlyList<ResolvedTransferRow>> ListResolvedByAccAsync(string accCode, CancellationToken ct = default);

    Task<int> AddAsync(string accCode, TransferInput input, CancellationToken ct = default);
    Task UpdateAsync(string accCode, int id, TransferInput input, CancellationToken ct = default);
    Task DeleteAsync(string accCode, int id, CancellationToken ct = default);
}

/// <inheritdoc cref="ITransferService"/>
public sealed class TransferService : ITransferService
{
    private readonly ITransferRepository _repo;
    private readonly IEditAuthorizationService _authz;
    private readonly IOnlineAtcProvider _online;

    public TransferService(ITransferRepository repo, IEditAuthorizationService authz, IOnlineAtcProvider online)
    {
        _repo = repo;
        _authz = authz;
        _online = online;
    }

    public Task<IReadOnlyList<TransferRow>> ListByAccAsync(string accCode, CancellationToken ct = default) =>
        _repo.ListByAccAsync(accCode, ct);

    public async Task<IReadOnlyList<ResolvedTransferRow>> ListResolvedByAccAsync(string accCode, CancellationToken ct = default)
    {
        var rows = await _repo.ListByAccAsync(accCode, ct);
        var online = _online.GetCurrent().Callsigns;
        return rows.Select(r => TransferOnlineResolver.Resolve(r, online)).ToList();
    }

    public async Task<int> AddAsync(string accCode, TransferInput input, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        Validate(input);
        return await _repo.AddAsync(accCode, input, ct);
    }

    public async Task UpdateAsync(string accCode, int id, TransferInput input, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        Validate(input);
        await _repo.UpdateAsync(accCode, id, input, ct);
    }

    public async Task DeleteAsync(string accCode, int id, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        await _repo.DeleteAsync(accCode, id, ct);
    }

    // Validazione SOFT: campi chiave presenti, catena non vuota e senza duplicati consecutivi.
    // Nessun controllo di esistenza degli handler (spesso di ACC confinanti).
    private static void Validate(TransferInput i)
    {
        if (string.IsNullOrWhiteSpace(i.RelationKey)) throw new ValidationException("Chiave relazione obbligatoria.");
        if (string.IsNullOrWhiteSpace(i.Cop)) throw new ValidationException("CoP obbligatorio.");
        if (string.IsNullOrWhiteSpace(i.AirportIcao)) throw new ValidationException("Aeroporto obbligatorio.");
        if (i.HandlerChain.Count == 0) throw new ValidationException("La catena handler non può essere vuota.");
        for (var k = 1; k < i.HandlerChain.Count; k++)
            if (string.Equals(i.HandlerChain[k], i.HandlerChain[k - 1], StringComparison.OrdinalIgnoreCase))
                throw new ValidationException($"Handler duplicato consecutivo: {i.HandlerChain[k]}.");
    }
}

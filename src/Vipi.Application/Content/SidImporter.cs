using System.Collections.Concurrent;
using Vipi.Application.Abstractions;
using Vipi.Domain;
using Vipi.Domain.Services;

namespace Vipi.Application.Content;

/// <inheritdoc cref="ISidImporter"/>
public sealed class SidImporter : ISidImporter
{
    // Serializza gli import sullo stesso aeroporto (job periodico + bottone editor): ReplaceImportedSidsAsync fa
    // delete+add, quindi due run concorrenti tenterebbero di scrivere due volte le stesse righe.
    //
    // ATTENZIONE: è un lock DI PROCESSO, e copre il deploy attuale (Render, istanza singola) ma non due repliche.
    // Non si può rafforzare con un indice unico su (AirportId, StableKey): quella chiave esclude di proposito la
    // cifra della revisione ed è legittimamente ripetuta quando il file .sid contiene due revisioni della stessa
    // SID. Se si passerà a più istanze servirà un lock condiviso (advisory lock DB), non un vincolo di unicità.
    // Il dizionario è limitato dal numero di aeroporti in catalogo (decine), quindi non richiede sfoltimento.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);

    private readonly ISidProvider _provider;
    private readonly IAirportRepository _repo;
    private readonly IImportPolicyStore _policy;
    private readonly IAiracService _airac;
    private readonly Vipi.Application.Auth.IEditAuthorizationService _authz;

    public SidImporter(ISidProvider provider, IAirportRepository repo, IImportPolicyStore policy,
        IAiracService airac, Vipi.Application.Auth.IEditAuthorizationService authz)
    {
        _provider = provider;
        _repo = repo;
        _policy = policy;
        _airac = airac;
        _authz = authz;
    }

    /// <inheritdoc />
    public async Task<int> ImportForCurrentUserAsync(string icao, CancellationToken ct = default)
    {
        var norm = icao.Trim().ToUpperInvariant();
        var acc = await _repo.GetAccCodeByIcaoAsync(norm, ct)
            ?? throw new Vipi.Application.Aor.ValidationException($"Aeroporto {norm} inesistente o senza ACC.");
        await _authz.EnsureCanEditAccAsync(acc, ct);
        return await ImportAsync(norm, ct);
    }

    /// <inheritdoc />
    public async Task<int> ImportAsync(string icao, CancellationToken ct = default)
    {
        icao = icao.Trim().ToUpperInvariant();
        var policy = await _policy.GetAsync(ct);
        if (!policy.IsImported(ImportCategory.Sids)) return 0;   // categoria disattivata: non toccare le SID

        var source = await _provider.GetSidsAsync(icao, ct);
        if (source.Count == 0) return 0;                          // nessun file/righe: non azzerare le importate esistenti

        var cycle = _airac.GetCycle(DateTime.UtcNow);
        var rows = source.Select(s => new ImportedSid(
            Runway: s.Runway, Fix: s.Fix, Name: s.Name, Transition: s.Transition,
            Type: s.Type, StableKey: s.StableKey, NeedsFixReview: s.NeedsFixReview)).ToList();

        // Solo la scrittura DB è serializzata (il fetch di rete resta concorrente): due run finiscono per riscrivere
        // gli stessi dati in sequenza (idempotente), senza duplicare righe.
        var gate = _locks.GetOrAdd(icao, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try { await _repo.ReplaceImportedSidsAsync(icao, rows, cycle, ct); }
        finally { gate.Release(); }
        return rows.Count;
    }
}

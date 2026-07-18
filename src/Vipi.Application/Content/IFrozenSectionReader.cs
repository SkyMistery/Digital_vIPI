using System.Text.Json;
using Vipi.Application.Abstractions;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Lettura al VIEW dell'output congelato di una sezione (doc 10 §3d). Dato il bersaglio di release e l'Id di una
/// sezione, ritorna il view-model JSON congelato nello snapshot della release EFFETTIVA, oppure null quando: non c'è
/// release effettiva, la sezione è in modalità Live, o non è stata catturata → il chiamante deriva live.
/// </summary>
public interface IFrozenSectionReader
{
    /// <summary>JSON congelato della sezione <paramref name="sectionId"/> nella release effettiva di
    /// (<paramref name="type"/>,<paramref name="key"/>), o null (→ derivare live).</summary>
    Task<string?> GetFrozenJsonAsync(ReleaseTargetType type, string key, int sectionId, CancellationToken ct = default);

    /// <summary>Come <see cref="GetFrozenJsonAsync"/> ma deserializzato in <typeparamref name="T"/>; default(T) se assente/illeggibile.</summary>
    Task<T?> GetFrozenAsync<T>(ReleaseTargetType type, string key, int sectionId, CancellationToken ct = default);
}

/// <inheritdoc cref="IFrozenSectionReader"/>
public sealed class FrozenSectionReader : IFrozenSectionReader
{
    private readonly IReleaseRepository _releases;
    public FrozenSectionReader(IReleaseRepository releases) => _releases = releases;

    public async Task<string?> GetFrozenJsonAsync(ReleaseTargetType type, string key, int sectionId, CancellationToken ct = default)
    {
        var rel = await _releases.GetEffectiveAsync(type, key, DateTime.UtcNow, ct);
        if (rel is null) return null;
        DocReleasePayload? payload;
        try { payload = JsonSerializer.Deserialize<DocReleasePayload>(rel.PayloadJson); }
        catch (JsonException) { return null; }
        return payload?.FrozenSections is { } fs && fs.TryGetValue(sectionId, out var json) ? json : null;
    }

    public async Task<T?> GetFrozenAsync<T>(ReleaseTargetType type, string key, int sectionId, CancellationToken ct = default)
    {
        var json = await GetFrozenJsonAsync(type, key, sectionId, ct);
        if (string.IsNullOrWhiteSpace(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json); }
        catch (JsonException) { return default; }
    }
}

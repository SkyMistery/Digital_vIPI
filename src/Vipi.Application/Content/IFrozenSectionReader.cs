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

    /// <summary>Come <see cref="GetFrozenAsync{T}"/> ma risolve l'Id della sezione derivabile dal suo
    /// <paramref name="sectionKey"/> leggendo <c>payload.Doc</c> della release effettiva (per i documenti a sezione
    /// unica — App/vLOA — dove la pagina non porta gli Id). default(T) se non c'è release, la sezione è Live/assente
    /// o non catturata.</summary>
    Task<T?> GetFrozenByKeyAsync<T>(ReleaseTargetType type, string key, string sectionKey, CancellationToken ct = default);
}

/// <inheritdoc cref="IFrozenSectionReader"/>
public sealed class FrozenSectionReader : IFrozenSectionReader
{
    private readonly IReleaseRepository _releases;
    public FrozenSectionReader(IReleaseRepository releases) => _releases = releases;

    public async Task<string?> GetFrozenJsonAsync(ReleaseTargetType type, string key, int sectionId, CancellationToken ct = default)
    {
        var payload = await LoadEffectivePayloadAsync(type, key, ct);
        return payload?.FrozenSections is { } fs && fs.TryGetValue(sectionId, out var json) ? json : null;
    }

    public async Task<T?> GetFrozenAsync<T>(ReleaseTargetType type, string key, int sectionId, CancellationToken ct = default) =>
        Deserialize<T>(await GetFrozenJsonAsync(type, key, sectionId, ct));

    public async Task<T?> GetFrozenByKeyAsync<T>(ReleaseTargetType type, string key, string sectionKey, CancellationToken ct = default)
    {
        var payload = await LoadEffectivePayloadAsync(type, key, ct);
        if (payload?.Doc is null || payload.FrozenSections is not { } fs) return default;
        // In payload.Doc gli Id sezione coincidono con le chiavi di FrozenSections (stesso snapshot della versione working
        // al publish). Per i doc a sezione unica la chiave derivabile è univoca → prima corrispondenza.
        var sec = FrozenSectionScan.FrozenDerived(payload.Doc)
            .FirstOrDefault(s => string.Equals(s.SectionKey, sectionKey, StringComparison.OrdinalIgnoreCase));
        return sec is not null && fs.TryGetValue(sec.Id, out var json) ? Deserialize<T>(json) : default;
    }

    private async Task<DocReleasePayload?> LoadEffectivePayloadAsync(ReleaseTargetType type, string key, CancellationToken ct)
    {
        var rel = await _releases.GetEffectiveAsync(type, key, DateTime.UtcNow, ct);
        if (rel is null) return null;
        try { return JsonSerializer.Deserialize<DocReleasePayload>(rel.PayloadJson); }
        catch (JsonException) { return null; }
    }

    private static T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json); }
        catch (JsonException) { return default; }
    }
}

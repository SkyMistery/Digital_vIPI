using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <inheritdoc cref="IDocumentProfileRepository"/>
public sealed class EfDocumentProfileRepository : IDocumentProfileRepository
{
    private readonly VipiDbContext _db;
    public EfDocumentProfileRepository(VipiDbContext db) => _db = db;

    public async Task<DocumentProfileData> GetAsync(int documentId, CancellationToken ct = default)
    {
        var p = await _db.DocumentProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.DocumentId == documentId, ct);
        return new DocumentProfileData
        {
            HiddenAorSectors = DeserializeStrings(p?.HiddenAorSectorsJson),
            HiddenFrequencies = DeserializeStrings(p?.HiddenFrequenciesJson),
            FreqOrder = DeserializeFreqOrder(p?.FreqOrderJson),
            FreqLinkSectorIds = DeserializeInts(p?.FreqLinksJson),
        };
    }

    public Task SaveFreqOrderAsync(int documentId, IReadOnlyList<AppFreqOrderOverride> overrides, CancellationToken ct = default) =>
        MutateAsync(documentId, p => p.FreqOrderJson = JsonSerializer.Serialize(overrides), ct);

    public Task SaveFreqLinksAsync(int documentId, IReadOnlyList<int> sourceSectorIds, CancellationToken ct = default) =>
        MutateAsync(documentId, p => p.FreqLinksJson = JsonSerializer.Serialize(sourceSectorIds), ct);

    // Get-or-create della riga profilo, applica la mutazione, salva.
    private async Task MutateAsync(int documentId, Action<DocumentProfile> mutate, CancellationToken ct)
    {
        var p = await _db.DocumentProfiles.FirstOrDefaultAsync(x => x.DocumentId == documentId, ct);
        if (p is null)
        {
            p = new DocumentProfile { DocumentId = documentId };
            _db.DocumentProfiles.Add(p);
        }
        mutate(p);
        await _db.SaveChangesAsync(ct);
    }

    private static IReadOnlyList<string> DeserializeStrings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new(); }
        catch (JsonException) { return Array.Empty<string>(); }
    }

    private static IReadOnlyList<int> DeserializeInts(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<int>();
        try { return JsonSerializer.Deserialize<List<int>>(json) ?? new(); }
        catch (JsonException) { return Array.Empty<int>(); }
    }

    private static IReadOnlyList<AppFreqOrderOverride> DeserializeFreqOrder(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<AppFreqOrderOverride>();
        try { return JsonSerializer.Deserialize<List<AppFreqOrderOverride>>(json) ?? new(); }
        catch (JsonException) { return Array.Empty<AppFreqOrderOverride>(); }
    }
}

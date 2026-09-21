using Vipi.Application.Abstractions;

namespace Vipi.Application.Tests;

/// <summary>
/// La quarantena in memoria: le stesse regole di <c>EfTranslationQuarantine</c> senza database.
///
/// <para>⚠️ <b>Non è un finto che dice sempre di no.</b> Un doppio che non conta niente farebbe passare
/// tutte le prove del freno senza che il freno esista: qui i tentativi si sommano davvero, e la soglia è
/// la stessa costante del codice vero — se un giorno cambia, cambiano insieme.</para>
/// </summary>
internal sealed class QuarantenaFinta : ITranslationQuarantine
{
    private sealed record Riga(int Strikes, long Caratteri, string Testo);

    private readonly Dictionary<(string Da, string A, string Hash), Riga> _righe = new();

    /// <summary>Quante volte il giro ha chiesto «chi è fermo»: serve a provare che il caso normale non paga
    /// una query per sentirsi dire «niente».</summary>
    public int LettureChieste { get; private set; }

    /// <summary>Le impronte che il giro ha perdonato perché sono tornate intere.</summary>
    public List<string> Perdonati { get; } = new();

    /// <summary>Semina una condanna già scontata, per provare il giro che trova il freno già scattato.</summary>
    public void Ferma(string da, string a, string hash, string testo, int strikes = TranslationQuarantena.Soglia) =>
        _righe[(da, a, hash)] = new Riga(strikes, 0, testo);

    public int StrikeDi(string da, string a, string hash) =>
        _righe.TryGetValue((da, a, hash), out var r) ? r.Strikes : 0;

    public long CaratteriDi(string da, string a, string hash) =>
        _righe.TryGetValue((da, a, hash), out var r) ? r.Caratteri : 0;

    public Task<IReadOnlyDictionary<string, int>> StrikeAsync(
        string sourceLang, string targetLang, IReadOnlyCollection<string> hashes, CancellationToken ct = default)
    {
        if (hashes.Count > 0) LettureChieste++;

        var esito = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var h in hashes)
            if (_righe.TryGetValue((sourceLang, targetLang, h), out var r))
                esito[h] = r.Strikes;

        return Task.FromResult<IReadOnlyDictionary<string, int>>(esito);
    }

    public Task<IReadOnlyList<string>> SegnaRottiAsync(
        string sourceLang, string targetLang, IReadOnlyList<TentativoRotto> rotti, string? engine,
        DateTime nowUtc, CancellationToken ct = default)
    {
        var appenaFermati = new List<string>();

        foreach (var r in rotti)
        {
            var chiave = (sourceLang, targetLang, r.Hash);
            _righe.TryGetValue(chiave, out var prima);
            var eraSotto = (prima?.Strikes ?? 0) < TranslationQuarantena.Soglia;

            var dopo = new Riga(
                (prima?.Strikes ?? 0) + 1,
                (prima?.Caratteri ?? 0) + r.Characters,
                r.SourceText);
            _righe[chiave] = dopo;

            if (eraSotto && dopo.Strikes >= TranslationQuarantena.Soglia) appenaFermati.Add(r.SourceText);
        }

        return Task.FromResult<IReadOnlyList<string>>(appenaFermati);
    }

    public Task DimenticaAsync(
        string sourceLang, string targetLang, IReadOnlyCollection<string> hashes, CancellationToken ct = default)
    {
        foreach (var h in hashes)
            if (_righe.Remove((sourceLang, targetLang, h))) Perdonati.Add(h);

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SegmentoInQuarantena>> FermiAsync(
        string sourceLang, string targetLang, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<SegmentoInQuarantena>>(_righe
            .Where(kv => kv.Key.Da == sourceLang && kv.Key.A == targetLang
                         && kv.Value.Strikes >= TranslationQuarantena.Soglia)
            .OrderByDescending(kv => kv.Value.Caratteri)
            .Select(kv => new SegmentoInQuarantena(
                kv.Value.Testo, kv.Value.Strikes, kv.Value.Caratteri, "azure", DateTime.UtcNow))
            .ToList());

    public Task<int> SvuotaAsync(string sourceLang, string targetLang, CancellationToken ct = default)
    {
        var chiavi = _righe.Keys.Where(k => k.Da == sourceLang && k.A == targetLang).ToList();
        foreach (var k in chiavi) _righe.Remove(k);
        return Task.FromResult(chiavi.Count);
    }
}

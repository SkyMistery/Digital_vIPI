using System.Text.Json;
using Vipi.Application.Abstractions;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Le sezioni congelate della release EFFETTIVA di un bersaglio, già lette e pronte da interrogare (doc 14 §3c).
/// <para>
/// ⚠️ È un <b>lotto</b>, e il motivo è misurato. Prima c'era un metodo per sezione, e ognuno faceva una query
/// che riportava l'intero <c>PayloadJson</c> della release più una deserializzazione completa: la vIPI ACC di
/// LIBB — due blocchi × quattro sezioni congelabili — leggeva e deserializzava <b>otto volte</b> lo stesso
/// snapshot da 62 KB, mezzo megabyte a ogni apertura della pagina pubblica; la vLOA più grande pesa 221 KB e il
/// suo viewer ne chiedeva tre. Nessuno dei quattro servizi di derivazione se ne era accorto perché ognuno aveva
/// scritto il proprio ciclo, e il costo si vedeva solo mettendoli in fila.
/// </para>
/// <para>
/// Il lotto non è mai null: quando non c'è release effettiva, o il payload è illeggibile, è <see cref="Empty"/>
/// e ogni domanda risponde <c>null</c> — cioè «deriva live», che è la stessa risposta di prima.
/// </para>
/// </summary>
public sealed class FrozenSections
{
    /// <summary>Nessuna sezione congelata: nessuna release effettiva, oppure payload illeggibile.</summary>
    public static FrozenSections Empty { get; } = new(null, null);

    private readonly IReadOnlyDictionary<int, string>? _byId;
    private readonly RawDocument? _doc;
    private readonly IReadOnlyDictionary<string, string>? _byKey;
    private Dictionary<string, int>? _idsByKey;

    private FrozenSections(IReadOnlyDictionary<int, string>? byId, RawDocument? doc,
        IReadOnlyDictionary<string, string>? byKey = null)
    {
        _byId = byId;
        _doc = doc;
        _byKey = byKey;
    }

    /// <summary>
    /// Lotto da uno snapshot di release: le sezioni congelate per Id, più il documento congelato da cui si
    /// risolve una chiave nel suo Id. Pubblico perché il lotto è il valore che la porta
    /// <see cref="IFrozenSectionReader"/> ritorna: qualunque implementazione deve poterlo costruire, non solo
    /// quella su EF.
    /// </summary>
    public static FrozenSections FromSnapshot(IReadOnlyDictionary<int, string>? byId, RawDocument? doc) =>
        byId is { Count: > 0 } ? new FrozenSections(byId, doc) : Empty;

    /// <summary>Lotto keyed direttamente per chiave di sezione, per le sorgenti che gli Id non li hanno.</summary>
    public static FrozenSections FromKeys(IReadOnlyDictionary<string, string>? byKey) =>
        byKey is { Count: > 0 }
            ? new FrozenSections(null, null, new Dictionary<string, string>(byKey, StringComparer.OrdinalIgnoreCase))
            : Empty;

    /// <summary>
    /// La lingua in cui questo snapshot è stato congelato, o null se non si sa (release pubblicate prima del
    /// 28 agosto 2026, o lotti costruiti per chiave).
    ///
    /// <para>
    /// ⚠️ <b>Serve alla sola prosa GENERATA</b> — le frasi di coordinamento — che è congelata <i>in una
    /// lingua</i>. Tutto il resto del congelato (AoR, frequenze, minime) sono numeri, geometrie e callsign,
    /// che una lingua non ce l'hanno: quelli si leggono dallo snapshot sempre, ed è quello che la release
    /// promette. Scartare l'intero lotto per un lettore in un'altra lingua vorrebbe dire mostrargli l'AoR
    /// <b>di oggi</b> invece di quella dell'AIRAC pubblicato.
    /// </para>
    /// </summary>
    public Language? Language => _doc?.Language;

    /// <summary>Vero se non c'è niente di congelato da leggere: tutto si deriverà live.</summary>
    public bool IsEmpty => (_byId is null || _byId.Count == 0) && (_byKey is null || _byKey.Count == 0);

    /// <summary>Output congelato della sezione con quell'Id, o null (→ derivare live).</summary>
    public T? Get<T>(int sectionId) where T : class =>
        _byId is not null && _byId.TryGetValue(sectionId, out var json) ? Deserialize<T>(json) : null;

    /// <summary>
    /// Output congelato della sezione derivabile con quella CHIAVE, o null (→ derivare live). Per i documenti a
    /// sezione unica — APP, vLOA, aeroporto — dove la pagina non porta gli Id delle sezioni: l'Id si risolve dal
    /// <c>Doc</c> dello snapshot, dove le chiavi coincidono con quelle catturate (stessa versione al publish).
    /// </summary>
    public T? Get<T>(string sectionKey) where T : class
    {
        if (_byKey is not null)
            return _byKey.TryGetValue(sectionKey, out var diretto) ? Deserialize<T>(diretto) : null;
        if (_byId is null || _doc is null) return null;
        _idsByKey ??= FrozenSectionScan.FrozenDerived(_doc)
            .GroupBy(s => s.SectionKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
        return _idsByKey.TryGetValue(sectionKey, out var id) ? Get<T>(id) : null;
    }

    /// <summary>
    /// La prosa GENERATA congelata, ma solo se è nella lingua di chi legge; altrimenti null, cioè «deriva
    /// live» — e la derivazione live compone nella lingua giusta.
    ///
    /// <para>
    /// ⚠️ È il pezzo che rende bilingue anche ciò che il traduttore automatico non tocca. Senza, un lettore
    /// italiano che apre una vLOA tradotta trova i coordinamenti ancora in inglese: lo snapshot li ha
    /// congelati così, e nessuno li ritraduce — perché non si devono tradurre, si devono <b>ricomporre</b>.
    /// </para>
    /// <para>
    /// Snapshot senza lingua nota (pubblicati prima del 28 agosto 2026) → si usa il congelato com'è: è
    /// esattamente il comportamento di prima, e cambiarlo farebbe ricomparire live delle release chiuse.
    /// </para>
    /// </summary>
    public T? GetProsa<T>(string sectionKey, string? linguaDiLettura) where T : class
    {
        if (Language is { } congelata && linguaDiLettura is { Length: > 0 })
        {
            var suo = congelata == Vipi.Domain.Language.En ? "en" : "it";
            if (!string.Equals(suo, linguaDiLettura, StringComparison.OrdinalIgnoreCase)) return null;
        }
        return Get<T>(sectionKey);
    }

    private static T? Deserialize<T>(string? json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<T>(json); }
        catch (JsonException) { return null; }
    }
}

/// <summary>
/// Lettura al VIEW dell'output congelato delle sezioni (doc 10 §3d, a lotti dal doc 14 §3c). Si chiede una volta
/// per pagina e si interroga in memoria: il payload della release è uno solo, e rileggerlo per sezione era il
/// costo che l'audit ha misurato.
/// </summary>
public interface IFrozenSectionReader
{
    /// <summary>Le sezioni congelate della release effettiva di (<paramref name="type"/>,<paramref name="key"/>).
    /// Mai null: senza release effettiva ritorna <see cref="FrozenSections.Empty"/>.</summary>
    Task<FrozenSections> LoadAsync(ReleaseTargetType type, string key, CancellationToken ct = default);
}

/// <inheritdoc cref="IFrozenSectionReader"/>
public sealed class FrozenSectionReader : IFrozenSectionReader
{
    private readonly IReleaseRepository _releases;
    public FrozenSectionReader(IReleaseRepository releases) => _releases = releases;

    public async Task<FrozenSections> LoadAsync(ReleaseTargetType type, string key, CancellationToken ct = default)
    {
        var rel = await _releases.GetEffectiveAsync(type, key, DateTime.UtcNow, ct);
        if (rel is null) return FrozenSections.Empty;

        DocReleasePayload? payload;
        try { payload = JsonSerializer.Deserialize<DocReleasePayload>(rel.PayloadJson); }
        catch (JsonException) { return FrozenSections.Empty; }

        return FrozenSections.FromSnapshot(payload?.FrozenSections, payload?.Doc);
    }
}

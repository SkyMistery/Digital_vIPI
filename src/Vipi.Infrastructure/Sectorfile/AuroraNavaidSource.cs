using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Sectorfile;

/// <summary>
/// Adapter GitHub del catalogo punti della divisione IT (repo pubblico raw, no auth): delega il parsing a
/// <see cref="AuroraSectorfileParser.ParseNavaids(IEnumerable{ValueTuple{NavaidKind, string}})"/> e mette il
/// risultato nella cache di processo. Lifetime transient (registrato con <c>AddHttpClient&lt;,&gt;</c>):
/// nessuno stato condiviso qui dentro, lo stato sta in <see cref="SectorfileCache"/>.
///
/// <para><b>⚠️ Quali file leggere lo dice Aurora, non noi.</b> Fino al 26 agosto 2026 i file erano <b>tre</b>,
/// scritti a mano nella configurazione (<c>itfix</c>, <c>itvor</c>, <c>itndb</c>): ma <c>ITALY.isc</c> ne cita
/// <b>otto</b>, e i punti d'oltreconfine stanno in <c>ESTERNI.fix</c>. Il costo di quella lista scritta a mano
/// si è visto nei poligoni: <c>GODRA</c> e <c>GIGUS</c> risultavano «non in catalogo» e quattro settori di
/// Milano restavano senza area, mentre i due punti c'erano — in un file che non leggevamo. Stessa regola di
/// <see cref="AuroraSectorShapeProvider"/>: si legge l'indice che carica Aurora stessa.</para>
///
/// <para>I tre percorsi di <see cref="SectorfileOptions"/> restano come <b>ripiego</b>, per quando l'indice
/// non è raggiungibile o non cita nessun file di punti: un catalogo ridotto è meglio di nessun catalogo.</para>
/// </summary>
/// <remarks>
/// È l'UNICO posto che scarica i file navaid. <see cref="AuroraSidProvider"/> passava di qui prima ancora che
/// esistessero i suggerimenti: due discese dello stesso file avrebbero significato due cache, due momenti di
/// aggiornamento diversi e la possibilità che l'editor consideri sbagliato un fix che l'import considera giusto.
/// </remarks>
public sealed class AuroraNavaidSource : INavaidSource
{
    private readonly HttpClient _http;
    private readonly SectorfileOptions _opt;
    private readonly SectorfileCache _cache;
    private readonly ILogger<AuroraNavaidSource> _log;

    public AuroraNavaidSource(HttpClient http, IOptions<SectorfileOptions> opt, SectorfileCache cache,
        ILogger<AuroraNavaidSource> log)
    {
        _http = http;
        _opt = opt.Value;
        _cache = cache;
        _log = log;
    }

    public Task<NavaidCatalog> GetAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opt.RawBaseUrl)) return Task.FromResult(NavaidCatalog.Empty);

        return _cache.GetNavaidsAsync(async token =>
        {
            var files = await ElencoFileAsync(token);

            var testi = new List<(NavaidKind Kind, string? Text)>();
            foreach (var (kind, rel) in files)
                testi.Add((kind, await GetTextOrNullAsync(rel, token)));

            var catalog = AuroraSectorfileParser.ParseNavaids(testi);

            // Un catalogo vuoto non è un errore per il chiamante (si perdono i suggerimenti), ma è la firma di
            // una sorgente che ha cambiato struttura: senza questa riga la degradazione è muta.
            if (catalog.Entries.Count == 0)
                _log.LogWarning("Catalogo punti: 0 voci da {Files} file (percorsi cambiati o repo spostato?).",
                    files.Count);
            else
                _log.LogInformation(
                    "Catalogo punti da GitHub: {Total} voci ({Fix} fix, {Vor} VOR, {Ndb} NDB) da {Files} file.",
                    catalog.Entries.Count,
                    catalog.Entries.Count(e => e.Kind == NavaidKind.Fix),
                    catalog.Entries.Count(e => e.Kind == NavaidKind.Vor),
                    catalog.Entries.Count(e => e.Kind == NavaidKind.Ndb),
                    files.Count);
            return catalog;
        }, ct);
    }

    /// <summary>Le righe dell'indice che citano un file di punti: <c>F;NAVAIDS\ESTERNI.fix</c>.
    /// La barra è quella di Windows perché l'indice lo legge Aurora, che gira lì.</summary>
    private static readonly Regex RigaNavaid = new(
        @"^\s*F\s*;\s*(NAVAIDS[\\/][A-Za-z0-9_.-]+\.(fix|vor|ndb))\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

    /// <summary>Quali file di punti leggere: dall'indice se c'è, altrimenti i tre di configurazione.</summary>
    private async Task<IReadOnlyList<(NavaidKind Kind, string Path)>> ElencoFileAsync(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_opt.SectorIndexUrl))
        {
            var indice = await ScaricaIndiceAsync(ct);
            if (indice is null)
                _log.LogWarning("Catalogo punti: indice {Url} non raggiungibile, si ripiega sui tre file di configurazione.",
                    _opt.SectorIndexUrl);
            else
            {
                var dall_indice = FileDiPunti(indice);
                if (dall_indice.Count > 0) return dall_indice;
                _log.LogWarning("Catalogo punti: {Url} non cita nessun file di punti, si ripiega sui tre file di configurazione.",
                    _opt.SectorIndexUrl);
            }
        }

        return Ripiego();
    }

    /// <summary>I tre percorsi scritti in configurazione: quel che si leggeva prima dell'indice.</summary>
    private IReadOnlyList<(NavaidKind Kind, string Path)> Ripiego() =>
        new[] { (NavaidKind.Vor, _opt.VorPath), (NavaidKind.Ndb, _opt.NdbPath), (NavaidKind.Fix, _opt.FixPath) }
            .Where(x => !string.IsNullOrWhiteSpace(x.Item2)).ToList();

    private async Task<string?> ScaricaIndiceAsync(CancellationToken ct)
    {
        // L'indice sta un livello SOPRA RawBaseUrl (che punta a Include/IT/): ha un URL suo intero, perché
        // `..` su raw.githubusercontent non si risolve.
        using var resp = await _http.GetAsync(_opt.SectorIndexUrl, ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadAsStringAsync(ct);
    }

    /// <summary>
    /// I file di punti citati dall'indice, senza ripetizioni e <b>in ordine di natura</b>: prima i VOR, poi gli
    /// NDB, poi i fix.
    ///
    /// <para>⚠️ L'ordine non è estetico: a parità di nome il catalogo tiene la PRIMA occorrenza, e con essa la
    /// natura. Seguendo l'ordine dell'indice, un nome presente sia come VOR sia come fix diventerebbe l'uno o
    /// l'altro a seconda di come qualcuno riordina <c>ITALY.isc</c>.</para>
    /// </summary>
    public static IReadOnlyList<(NavaidKind Kind, string Path)> FileDiPunti(string indice)
    {
        var visti = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var trovati = new List<(NavaidKind Kind, string Path)>();
        foreach (Match m in RigaNavaid.Matches(indice))
        {
            var rel = m.Groups[1].Value.Replace('\\', '/');
            if (!visti.Add(rel)) continue;
            var kind = m.Groups[2].Value.ToLowerInvariant() switch
            {
                "vor" => NavaidKind.Vor,
                "ndb" => NavaidKind.Ndb,
                _ => NavaidKind.Fix,
            };
            trovati.Add((kind, rel));
        }

        return trovati.OrderBy(x => x.Kind switch { NavaidKind.Vor => 0, NavaidKind.Ndb => 1, _ => 2 }).ToList();
    }

    public Task<NavaidCatalog> RefreshAsync(CancellationToken ct = default)
    {
        // Solo la fetta dei punti: chi chiede il catalogo aggiornato non sta chiedendo di riscaricare anche i
        // poligoni delle torri, che vengono dallo stesso repository ma non c'entrano con quello che sta facendo.
        _cache.InvalidateNavaids();
        return GetAsync(ct);
    }

    private Task<string?> GetTextOrNullAsync(string relative, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(relative)
            ? Task.FromResult<string?>(null)
            : SectorfileRaw.GetTextOrNullAsync(_http, _opt.RawBaseUrl, relative, ct);
}

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Sectorfile;

/// <summary>
/// Adapter GitHub del catalogo punti della divisione IT: scarica <c>itvor</c>, <c>itndb</c> e <c>itfix</c> (repo
/// pubblico raw, no auth), delega il parsing a <see cref="AuroraSectorfileParser.ParseNavaids"/> e mette il
/// risultato nella cache di processo. Lifetime transient (registrato con <c>AddHttpClient&lt;,&gt;</c>): nessuno
/// stato condiviso qui dentro, lo stato sta in <see cref="SectorfileCache"/>.
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
            var fix = await GetTextOrNullAsync(_opt.FixPath, token);
            var vor = await GetTextOrNullAsync(_opt.VorPath, token);
            var ndb = await GetTextOrNullAsync(_opt.NdbPath, token);
            var catalog = AuroraSectorfileParser.ParseNavaids(fix, vor, ndb);

            // Un catalogo vuoto non è un errore per il chiamante (si perdono i suggerimenti), ma è la firma di
            // una sorgente che ha cambiato struttura: senza questa riga la degradazione è muta.
            if (catalog.Entries.Count == 0)
                _log.LogWarning("Catalogo punti: 0 voci da {Fix}/{Vor}/{Ndb} (percorsi cambiati o repo spostato?).",
                    _opt.FixPath, _opt.VorPath, _opt.NdbPath);
            else
                _log.LogInformation("Catalogo punti da GitHub: {Total} voci ({Fix} fix, {Vor} VOR, {Ndb} NDB).",
                    catalog.Entries.Count,
                    catalog.Entries.Count(e => e.Kind == NavaidKind.Fix),
                    catalog.Entries.Count(e => e.Kind == NavaidKind.Vor),
                    catalog.Entries.Count(e => e.Kind == NavaidKind.Ndb));
            return catalog;
        }, ct);
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

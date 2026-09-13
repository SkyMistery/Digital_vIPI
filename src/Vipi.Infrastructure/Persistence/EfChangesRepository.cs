using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Application.Routing;
using Vipi.Domain;

namespace Vipi.Infrastructure.Persistence;

/// <summary>Implementazione EF di <see cref="IChangesRepository"/>.</summary>
public sealed class EfChangesRepository : IChangesRepository
{
    private readonly VipiDbContext _db;
    private readonly IReleaseTargetRegistry _targets;
    private readonly IDocRoutesRegistry _routes;
    private readonly IReleaseRepository _releases;

    public EfChangesRepository(VipiDbContext db, IReleaseTargetRegistry targets, IDocRoutesRegistry routes,
        IReleaseRepository releases)
    {
        _db = db;
        _targets = targets;
        _routes = routes;
        _releases = releases;
    }

    /// <summary>
    /// I documenti la cui release IN VIGORE è del ciclo chiesto, con numero, nota, autore e data <b>della
    /// release</b>, e i conteggi del suo snapshot contro quelli della release precedente dello stesso bersaglio.
    ///
    /// <para>🔴 <b>T-042 (13 settembre 2026).</b> Fino ad allora qui si raccontava la versione CORRENTE: dopo un
    /// «Pubblica questa versione» con la release al ciclo successivo, la pagina serviva ancora la v4 e questo
    /// elenco ne mostrava già nota e conteggi della v5. «Cosa è cambiato» è una promessa al pubblico: vale per
    /// quello che il pubblico vede.</para>
    /// </summary>
    public async Task<IReadOnlyList<ChangeRow>> ListChangedAsync(string airacCycle, CancellationToken ct = default)
    {
        var docs = await _db.Documents
            .Where(d => d.CurrentVersionId != null)
            .Include(d => d.Sectors).ThenInclude(s => s.Acc)
            // L'aeroporto descritto: da qui il descrittore prende ICAO e ACC (vedi AirportReleaseTarget).
            .Include(d => d.Airport).ThenInclude(a => a!.Acc)
            // ⚠️ E quello dell'edizione MILITARE: legame diverso, navigazione diversa. Senza, il documento
            // militare non viene descritto da nessuno e sparisce di qui in silenzio — la spiegazione lunga
            // sta su `EfDocumentAdminRepository.ListAsync`, che fa la stessa query.
            .Include(d => d.MilAirport).ThenInclude(a => a!.Acc)
            .Include(d => d.Parties).ThenInclude(p => p.Sector).ThenInclude(s => s!.Acc)
            .AsNoTracking().ToListAsync(ct);

        // Tipo, ACC e ROTTA dai descrittori + registry delle rotte (doc 13 §3e). Qui c'era la QUARTA copia della
        // risoluzione — dopo VersioniPage, ReleasePreviewPage e la ricerca — con lo stesso errore: i documenti di
        // APP standalone puntavano alla vIPI di ACC.
        var described = docs
            .Select(d => (Doc: d, Managed: Describe(d)))
            .Where(x => x.Managed is not null && !string.IsNullOrEmpty(x.Managed!.AccCode))
            .ToList();

        // Stesso gate della pagina (doc 13 §3f): niente documenti nascosti né senza release effettiva — l'elenco
        // linkava anche documenti che, aperti, dicono «non disponibile».
        var visible = await PublicDocumentGate.VisibleAsync(described, x => x.Doc, x => x.Managed!, _releases, ct);

        var teste = await ReleaseInVigore.TesteAsync(_db,
            visible.Select(v => (v.Managed!.ReleaseTarget, v.Managed!.ReleaseKey)).Distinct().ToList(),
            DateTime.UtcNow, ct);

        var rows = new List<ChangeRow>();
        foreach (var (d, managed) in visible)
        {
            if (!teste.TryGetValue((managed!.ReleaseTarget, managed.ReleaseKey), out var testa)) continue;
            if (testa.AiracCycle != airacCycle) continue;

            var acc = managed.AccCode!;
            var url = _routes.For(managed.Kind).PublicUrl(acc.ToLowerInvariant(), managed.ReleaseKey, managed.NeighbourCode);
            if (url is null) continue;

            var corrente = await ConteggiAsync(testa.Id, ct);

            // La release precedente dello stesso bersaglio: quella che il pubblico vedeva prima di questa.
            var precedenteId = await _db.DocReleases.AsNoTracking()
                .Where(r => r.TargetType == testa.Type && r.TargetKey == testa.Key && r.Id != testa.Id
                            && (r.ReleaseEffectiveUtc < testa.EffectiveUtc
                                || (r.ReleaseEffectiveUtc == testa.EffectiveUtc && r.VersionNumber < testa.VersionNumber)))
                .OrderByDescending(r => r.ReleaseEffectiveUtc).ThenByDescending(r => r.VersionNumber)
                .Select(r => (int?)r.Id).FirstOrDefaultAsync(ct);
            var precedente = precedenteId is int pid ? await ConteggiAsync(pid, ct) : (Blocchi: 0, Sezioni: 0, Titolo: (string?)null);

            rows.Add(new ChangeRow
            {
                DocTitle = corrente.Titolo ?? d.Title,
                Type = d.Type,
                AccCode = acc,
                Url = url,
                VersionNumber = testa.VersionNumber,
                Note = testa.Note,
                PublishedByUserId = testa.CreatedByUserId,
                PublishedUtc = testa.CreatedUtc,
                PrevBlocks = precedente.Blocchi,
                CurrBlocks = corrente.Blocchi,
                PrevSections = precedente.Sezioni,
                CurrSections = corrente.Sezioni,
            });
        }

        return rows.OrderByDescending(r => r.PublishedUtc).ToList();
    }

    /// <summary>Blocchi e sezioni dello snapshot di una release (tutti, nascosti compresi: è un conteggio del documento).</summary>
    private async Task<(int Blocchi, int Sezioni, string? Titolo)> ConteggiAsync(int releaseId, CancellationToken ct)
    {
        var payload = await _db.DocReleases.AsNoTracking().Where(r => r.Id == releaseId)
            .Select(r => r.PayloadJson).FirstOrDefaultAsync(ct);
        if (ReleaseInVigore.Documento(payload) is not { } doc) return (0, 0, null);
        var voce = IndiceDelleRelease.Costruisci(doc);
        return (voce.Blocchi, voce.TutteLeSezioni, doc.Title);
    }

    /// <summary>Attribuisce il documento a un tipo con gli stessi descrittori dell'elenco unificato.</summary>
    private ManagedDoc? Describe(Domain.Entities.Document doc)
    {
        foreach (var target in _targets.ByDescribeOrder)
            if (target.TryDescribe(doc, hasDraft: false, out var managed))
                return managed;
        return null;
    }
}

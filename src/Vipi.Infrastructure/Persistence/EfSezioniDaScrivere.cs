using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Le sezioni da scrivere, lette dal database (carta <c>docs/feature/2026-10-18-sezioni-da-scrivere.md</c>). Il
/// conto lo fa <see cref="SezioniDaScrivere"/>; qui si sceglie la versione di lavoro e il profilo di ogni documento.
///
/// <para>⚠️ <b>Letture in blocco</b>, come <see cref="EfStatoTraduzione"/>: lo chiamano pagine Blazor, e una query per
/// documento sarebbe la corsa sul <c>DbContext</c> del circuito già pagata più volte. Nessuna tabella di stato: si
/// calcola, e non si disallinea.</para>
/// </summary>
public sealed class EfSezioniDaScrivere : ISezioniDaScrivereService
{
    private readonly VipiDbContext _db;

    public EfSezioniDaScrivere(VipiDbContext db) => _db = db;

    public Task<IReadOnlyDictionary<int, StatoSezioni>> QuadroAsync(CancellationToken ct = default) =>
        CalcolaAsync(null, ct);

    public async Task<StatoSezioni?> DocumentoAsync(int documentId, CancellationToken ct = default) =>
        (await CalcolaAsync(documentId, ct)).GetValueOrDefault(documentId);

    private async Task<IReadOnlyDictionary<int, StatoSezioni>> CalcolaAsync(int? soloQuesto, CancellationToken ct)
    {
        var documenti = await _db.Documents.AsNoTracking()
            .Where(d => soloQuesto == null || d.Id == soloQuesto)
            .Select(d => new
            {
                d.Id, d.Type, d.Edition, d.CurrentVersionId,
                Civile = d.Airport != null,
                Militare = d.MilAirport != null,
                App = d.AtcUnit != null,
                AppMil = d.MilSectors.Any(),
            })
            .ToListAsync(ct);
        if (documenti.Count == 0) return new Dictionary<int, StatoSezioni>();
        var ids = documenti.Select(d => d.Id).ToList();

        // ⚠️ La versione di LAVORO con la regola di `EfEditingRepository.LoadForEditAsync` (e di EfStatoTraduzione):
        // bozza più recente, sennò la pubblicata corrente, sennò l'ultima. L'editor mostra quella: contarne un'altra
        // vorrebbe dire un riquadro che non torna con quel che si ha sotto gli occhi.
        var versioni = await _db.DocumentVersions.AsNoTracking()
            .Where(v => ids.Contains(v.DocumentId))
            .Select(v => new { v.Id, v.DocumentId, v.Status, v.VersionNumber })
            .ToListAsync(ct);
        var lavoro = new Dictionary<int, int>();
        foreach (var d in documenti)
        {
            var sue = versioni.Where(v => v.DocumentId == d.Id).ToList();
            if (sue.Count == 0) continue;
            lavoro[d.Id] = (sue.Where(v => v.Status == DocumentStatus.Draft).OrderByDescending(v => v.VersionNumber).FirstOrDefault()
                            ?? sue.FirstOrDefault(v => v.Id == d.CurrentVersionId)
                            ?? sue.OrderByDescending(v => v.VersionNumber).First()).Id;
        }
        var versioniDiLavoro = lavoro.Values.ToList();

        var sezioni = await _db.DocumentSections.AsNoTracking()
            .Where(s => versioniDiLavoro.Contains(s.DocumentVersionId))
            .OrderBy(s => s.Depth).ThenBy(s => s.Order).ThenBy(s => s.Id)
            .Select(s => new { s.Id, s.DocumentVersionId, s.ParentSectionId, s.SectionKey, s.Title, s.IsHidden })
            .ToListAsync(ct);
        var blocchi = (await _db.ContentBlocks.AsNoTracking()
                .Where(b => versioniDiLavoro.Contains(b.DocumentVersionId))
                .Select(b => new { b.SectionId, b.Body, b.BodyJson, Riusato = b.SharedBlockId != null })
                .ToListAsync(ct))
            .GroupBy(b => b.SectionId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<BloccoDaContare>)g
                .Select(b => new BloccoDaContare(b.Body, b.BodyJson, b.Riusato)).ToList());

        var perVersione = sezioni.GroupBy(s => s.DocumentVersionId).ToDictionary(g => g.Key, g => g
            .Select(s => new SezioneDaContare(s.Id, s.ParentSectionId, s.SectionKey, s.Title, s.IsHidden,
                blocchi.GetValueOrDefault(s.Id) ?? Array.Empty<BloccoDaContare>()))
            .ToList());

        // Le sezioni che un admin ha dichiarato facoltative (S133, carta 2026-10-28-stato-dei-documenti §1 D4): poche
        // righe, lette una volta per tutto il conto.
        var facoltative = await new EfSezioniFacoltative(_db).TutteAsync(ct);

        var esito = new Dictionary<int, StatoSezioni>();
        foreach (var d in documenti)
        {
            if (!lavoro.TryGetValue(d.Id, out var vid)) continue;
            var tutte = perVersione.GetValueOrDefault(vid) ?? new List<SezioneDaContare>();

            // Il profilo come lo sceglie l'editor (`EfEditingRepository.GetSectionCatalogPlaceAsync`): la famiglia
            // del documento, e per la vIPI di ACC il blocco. Il vSOP di un APP militare si riconosce dai settori.
            SectionProfile? profilo = d.Type == DocumentType.Vloa ? SectionProfile.Vloa
                : d.Edition == DocumentEdition.Military
                    ? (d.Militare ? SectionProfile.AirportMil : d.AppMil ? SectionProfile.AppMil : null)
                : d.Civile ? SectionProfile.Airport
                : d.App ? SectionProfile.App
                : null;

            if (profilo is SectionProfile p)
            {
                esito[d.Id] = new StatoSezioni(d.Id, new[] { SezioniDaScrivere.Conta(p, tutte, facoltative: facoltative) });
                continue;
            }
            if (d.Type != DocumentType.Vipi || d.Edition != DocumentEdition.Civil) continue;

            // La vIPI di ACC: un conto per blocco. Un blocco nascosto non conta, con tutto quel che ha sotto.
            var parti = new List<ContoSezioni>();
            foreach (var blocco in tutte.Where(s => s.ParentId is null && !s.Nascosta))
            {
                SectionProfile? delBlocco =
                    string.Equals(blocco.Chiave, SectionKeys.AccBloccoAerovia, StringComparison.OrdinalIgnoreCase) ? SectionProfile.AccAerovia
                    : string.Equals(blocco.Chiave, SectionKeys.AccBloccoApp, StringComparison.OrdinalIgnoreCase) ? SectionProfile.AccAppBlock
                    : null;
                if (delBlocco is not SectionProfile pb) continue;
                // ⚠️ Senza la radice: il percorso di una vuota parte dal blocco, che il titolo della parte dice già.
                parti.Add(SezioniDaScrivere.Conta(pb, Discendenti(blocco.Id, tutte), blocco.Titolo, facoltative));
            }
            if (parti.Count > 0) esito[d.Id] = new StatoSezioni(d.Id, parti);
        }
        return esito;
    }

    private static List<SezioneDaContare> Discendenti(int radice, IReadOnlyList<SezioneDaContare> tutte)
    {
        var dentro = new HashSet<int> { radice };
        var esito = new List<SezioneDaContare>();
        // Le sezioni arrivano per profondità: una madre è sempre già passata quando arriva la figlia.
        foreach (var s in tutte)
            if (s.ParentId is int m && dentro.Contains(m))
            {
                dentro.Add(s.Id);
                esito.Add(s);
            }
        return esito;
    }
}

using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;
using static Vipi.Application.Messaggio;

namespace Vipi.Infrastructure.Persistence;

/// <inheritdoc cref="IMilitaryDocumentService"/>
public sealed class EfMilitaryDocumentService : IMilitaryDocumentService
{
    private readonly VipiDbContext _db;
    private readonly IAiracService _airac;
    private readonly Vipi.Application.Auth.IEditAuthorizationService _authz;
    private readonly IEditingRepository _editing;
    private readonly ISpecialAreaRepository _areas;

    /// <summary>Traduttore dei testi dell'anagrafica (le descrizioni delle aree, scritte dalla sorgente in
    /// inglese). Opzionale: senza, restano nella lingua della sorgente.</summary>
    private readonly Vipi.Application.Translation.TranslationLookup? _traduzioni;

    public EfMilitaryDocumentService(VipiDbContext db, IAiracService airac,
                                     Vipi.Application.Auth.IEditAuthorizationService authz,
                                     IEditingRepository editing, ISpecialAreaRepository areas,
                                     INavaidCatalog navaids,
                                     IFrozenSectionReader? frozen = null,
                                     Vipi.Application.Translation.TranslationLookup? traduzioni = null)
    {
        _db = db;
        _airac = airac;
        _authz = authz;
        _editing = editing;
        _areas = areas;
        _navaids = navaids;
        _frozen = frozen;
        _traduzioni = traduzioni;
    }

    private readonly INavaidCatalog _navaids;
    private readonly IFrozenSectionReader? _frozen;

    public async Task<IReadOnlyList<MilAirportRow>> ListAsync(bool perStaff, CancellationToken ct = default)
    {
        // I campi CANDIDATI sono quelli con presenza militare secondo la sorgente. ⚠️ `HasMilitaryPresence`
        // è vero anche su Linate, Pisa, Ciampino: sono scali civili con sedime militare, e un SOP militare
        // ce l'hanno davvero (LIRP è fra i quindici PDF). Quindi il filtro è quello giusto — è
        // `IsMilitaryOnly` a dire un'altra cosa, e si mostra soltanto.
        var campi = await _db.Airports.AsNoTracking()
            .Where(a => a.HasMilitaryPresence && !a.IsHidden)
            .Select(a => new
            {
                a.Icao, a.Name, AccCode = a.Acc!.Code, a.IsMilitaryOnly, a.MilDocumentId, a.DocumentId,
            })
            .ToListAsync(ct).ConfigureAwait(false);

        // Una lettura sola per sapere quali hanno una release EFFETTIVA: il gate dell'elenco pubblico.
        var chiavi = campi.Where(c => c.MilDocumentId is not null).Select(c => c.Icao).ToList();
        var adesso = DateTime.UtcNow;
        var pubblicati = chiavi.Count == 0
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : (await _db.DocReleases.AsNoTracking()
                .Where(r => r.TargetType == ReleaseTargetType.AirportMil
                            && chiavi.Contains(r.TargetKey)
                            && r.ReleaseEffectiveUtc <= adesso)
                .Select(r => r.TargetKey)
                .ToListAsync(ct).ConfigureAwait(false))
              .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var righe = campi
            .Select(c => new MilAirportRow(c.Icao, c.Name, c.AccCode, c.IsMilitaryOnly,
                                           c.MilDocumentId, pubblicati.Contains(c.Icao),
                                           HaCivile: c.DocumentId is not null))
            // Prima i solo-militari, poi per ICAO: su un elenco nazionale l'ordine alfabetico puro
            // mescolerebbe Aviano con Pisa, che sono due cose diverse per chi cerca.
            .OrderByDescending(r => r.SoloMilitare)
            .ThenBy(r => r.Icao, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // ⚠️ Il gate pubblico è QUI e non nella pagina: una pagina che filtra è una pagina che può
        // dimenticarsene. Allo staff si mostrano anche i candidati senza documento — senza, il PRIMO vSOP
        // militare non sarebbe creabile da nessuna parte (il catch-22 già pagato con l'elenco APP).
        return perStaff ? righe : righe.Where(r => r.Pubblicato).ToList();
    }

    public async Task<int> CreaAsync(string icao, CancellationToken ct = default)
    {
        icao = (icao ?? "").Trim().ToUpperInvariant();
        var campo = await _db.Airports.FirstOrDefaultAsync(a => a.Icao == icao, ct).ConfigureAwait(false)
            ?? throw new Vipi.Application.Aor.ValidationException(Lingua($"Aeroporto {icao} inesistente.", $"Airport {icao} does not exist."));

        // Stesso permesso del documento civile: l'edizione non cambia chi comanda su quello scalo.
        _authz.EnsureAtLeast(VipiRole.Editor);

        if (campo.MilDocumentId is int esistente) return esistente;

        if (!campo.HasMilitaryPresence)
            // Meglio fermarsi che creare un vSOP militare su un campo che militare non è: il documento
            // resterebbe lì, vuoto, in un elenco dove nessuno saprebbe perché c'è.
            throw new Vipi.Application.Aor.ValidationException(Lingua(
                $"{icao} non risulta avere presenza militare: la sorgente non lo dice.",
                $"{icao} is not recorded as having a military presence: the source does not say so."));

        // ---- Sui campi MISTI la vIPI civile viene PRIMA (carta vSOP militari §5-bis) -------------------
        //
        // Su Pisa, Linate, Ciampino il vSOP militare descrive la METÀ militare di uno scalo che ne ha due:
        // dice cosa cambia rispetto alla vIPI civile — quale parte del sedime, quali frequenze, quali
        // procedure sono le altre. Senza la civile non c'è il «rispetto a cosa», e il documento nasce a
        // descrivere un campo di cui nessuno ha ancora scritto le piste.
        //
        // ⚠️ Vale solo per i MISTI. Su un campo solo militare la civile non esiste e non deve esistere
        // (la guardia gemella sta in AirportEditingService.EnsureDocumentAsync): chiederla qui renderebbe
        // Aviano e Ghedi — proprio i campi che un vSOP ce l'hanno — gli unici a non poterlo avere.
        //
        // ⚠️ Basta che la vIPI civile ESISTA, anche solo in bozza: pretenderla pubblicata bloccherebbe il
        // lavoro parallelo sulle due edizioni, che è il caso normale su uno scalo appena aperto.
        if (!campo.IsMilitaryOnly && campo.DocumentId is null)
            throw new Vipi.Application.Aor.ValidationException(Lingua(
                $"{icao} è uno scalo civile con presenza militare: prima si crea la vIPI civile, poi il vSOP militare.",
                $"{icao} is a civil field with a military presence: create the civil vIPI first, then the military vSOP."));

        // ⚠️ Language.It, non En (carta §1d): la lingua sorgente è quella in cui si REDIGE. I quindici PDF
        // di partenza sono in inglese, ma il documento è nostro e un lettore inglese lo ottiene tradotto.
        // ⚠️ conSegnaposto: false come l'aeroporto civile — la pagina disegna le sezioni per chiave, non
        // perché abbiano un blocco dentro.
        var (doc, _) = Seed.DocumentBirth.Crea(
            _db, _airac, $"vSOP MIL — {icao} {campo.Name}",
            Language.It, SectionProfile.AirportMil, _authz.CurrentUserId ?? 0,
            conSegnaposto: false);

        doc.Edition = DocumentEdition.Military;
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        // Il legame è sull'AEROPORTO, gemello di DocumentId: la verità sta lì dal 25 agosto 2026.
        campo.MilDocumentId = doc.Id;
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return doc.Id;
    }

    // ---- Aree di lavoro (sezione `regulated`) --------------------------------------------------------

    public async Task<int?> GetDocumentIdAsync(string icao, CancellationToken ct = default) =>
        await _db.Airports.AsNoTracking().Where(a => a.Icao == Norm(icao))
            .Select(a => a.MilDocumentId).FirstOrDefaultAsync(ct).ConfigureAwait(false);

    public Task<bool> HasPublishedAsync(string icao, CancellationToken ct = default) =>
        PubblicatoAsync(icao, militare: true, ct);

    public async Task<CivilEdition> GetCivilEditionAsync(string icao, CancellationToken ct = default)
    {
        icao = Norm(icao);

        // Una proiezione sola: esiste il documento civile, e il campo è solo militare? Le due risposte
        // stanno sulla stessa riga di `Airports`, e chiederle separatamente vorrebbe dire poterle vedere
        // in due istanti diversi.
        var campo = await _db.Airports.AsNoTracking()
            .Where(a => a.Icao == icao)
            .Select(a => new { Esiste = a.DocumentId != null, a.IsMilitaryOnly })
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

        // ICAO sconosciuto: «non esiste, non pubblicata» — e NON «solo militare», che direbbe che
        // l'assenza è a norma quando in realtà non si sa niente di quel campo.
        if (campo is null) return new CivilEdition(false, false, false);
        if (!campo.Esiste) return new CivilEdition(false, false, campo.IsMilitaryOnly);

        var adesso = DateTime.UtcNow;
        var pubblicata = await _db.DocReleases.AsNoTracking()
            .AnyAsync(r => r.TargetType == ReleaseTargetType.Airport && r.TargetKey == icao
                           && r.ReleaseEffectiveUtc <= adesso, ct).ConfigureAwait(false);

        return new CivilEdition(true, pubblicata, campo.IsMilitaryOnly);
    }

    /// <summary>
    /// Il gate del ponte civile → militare: il documento dev'esserci <b>e</b> avere una release effettiva —
    /// lo stesso gate dell'elenco pubblico, per un aeroporto solo.
    /// <para>⚠️ Le due edizioni condividono la CHIAVE di release (l'ICAO) e si distinguono per il TIPO:
    /// leggere il legame di una e la release dell'altra darebbe la risposta del documento sbagliato. Il
    /// verso opposto lo dà <see cref="GetCivilEditionAsync"/>, che deve rispondere anche a «esiste ma non è
    /// pubblicata» e quindi non può essere un booleano.</para>
    /// </summary>
    private async Task<bool> PubblicatoAsync(string icao, bool militare, CancellationToken ct)
    {
        icao = Norm(icao);
        var tipo = militare ? ReleaseTargetType.AirportMil : ReleaseTargetType.Airport;

        // ⚠️ Il predicato si sceglie FUORI dall'espressione: un ternario che salta fra due colonne dentro
        // una `Where` diventa una CASE WHEN che i provider traducono in modi diversi. Due lambda esplicite
        // sono più lunghe da leggere e più corte da spiegare.
        System.Linq.Expressions.Expression<Func<Airport, bool>> haIlDocumento = militare
            ? a => a.Icao == icao && a.MilDocumentId != null
            : a => a.Icao == icao && a.DocumentId != null;

        if (!await _db.Airports.AsNoTracking().AnyAsync(haIlDocumento, ct).ConfigureAwait(false))
            return false;

        var adesso = DateTime.UtcNow;
        return await _db.DocReleases.AsNoTracking()
            .AnyAsync(r => r.TargetType == tipo && r.TargetKey == icao
                           && r.ReleaseEffectiveUtc <= adesso, ct).ConfigureAwait(false);
    }

    // ---- Radioassistenze: il documento dice CHI e in che ordine, i valori li dà l'anagrafica (§12b) ----

    public async Task<IReadOnlyList<NavaidRow>> GetNavaidsAsync(string icao, CancellationToken ct = default)
    {
        if (await GetDocumentIdAsync(icao, ct).ConfigureAwait(false) is not int docId)
            return Array.Empty<NavaidRow>();
        var json = await _editing.GetSectionBlockJsonAsync(docId, "navaids", ct).ConfigureAwait(false);
        return await ResolveNavaidsAsync(MilNavaidsPayload.Leggi(json), ct).ConfigureAwait(false);
    }

    public async Task SaveNavaidsAsync(string icao, IReadOnlyList<NavaidKey> righe, CancellationToken ct = default)
    {
        // Come per le aree: passa da CreaAsync perché è ACC-gated e idempotente — chi non può scrivere si
        // ferma qui, e non alla riga dopo con mezza modifica già fatta.
        var docId = await CreaAsync(icao, ct).ConfigureAwait(false);
        await _editing.SaveSectionBlockJsonAsync(docId, "navaids", MilNavaidsPayload.Scrivi(righe),
            _authz.CurrentUserId ?? 0, ct).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<NavaidRow>> ResolveNavaidsAsync(
        IReadOnlyList<NavaidKey> righe, CancellationToken ct = default) =>
        righe.Count == 0
            ? Task.FromResult<IReadOnlyList<NavaidRow>>(Array.Empty<NavaidRow>())
            : _navaids.GetManyAsync(righe, ct);

    public async Task<IReadOnlyList<NavaidRow>> ResolveNavaidsForViewAsync(
        string icao, IReadOnlyList<NavaidKey> righe, bool useFrozen, CancellationToken ct = default)
    {
        if (useFrozen && _frozen is not null)
        {
            var snapshot = await _frozen.LoadAsync(ReleaseTargetType.AirportMil, Norm(icao), ct).ConfigureAwait(false);
            // ⚠️ La fotografia vince anche quando è VUOTA: una release pubblicata senza righe è una tabella
            // vuota, non «vai a vedere che c'è adesso». Distinguere «congelato a zero» da «non congelato» è
            // ciò che rende una release una release.
            if (snapshot.Get<List<NavaidRow>>("navaids") is { } congelate) return congelate;
        }
        return await ResolveNavaidsAsync(righe, ct).ConfigureAwait(false);
    }

    public async Task<RegulatedSelection> GetRegulatedAsync(string icao, CancellationToken ct = default)
    {
        if (await GetDocumentIdAsync(icao, ct).ConfigureAwait(false) is not int docId) return Manuale(null);
        var json = await _editing.GetSectionBlockJsonAsync(docId, "regulated", ct).ConfigureAwait(false);
        return Manuale(RegulatedSelectionJson.Parse(json));
    }

    public async Task SaveRegulatedAsync(string icao, RegulatedSelection selection, CancellationToken ct = default)
    {
        // Passa da CreaAsync e non da GetDocumentIdAsync: è ACC-gated e idempotente, quindi chi non può
        // scrivere si ferma qui e non alla riga dopo, con mezza modifica già fatta.
        var docId = await CreaAsync(icao, ct).ConfigureAwait(false);
        var pulita = Manuale(selection);
        var vuota = pulita.OwnIds.Count == 0 && pulita.ExtraIds.Count == 0;
        await _editing.SaveSectionBlockJsonAsync(docId, "regulated",
            vuota ? null : System.Text.Json.JsonSerializer.Serialize(pulita),
            _authz.CurrentUserId ?? 0, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SpecialAreaPick>> ListSpecialAreasAsync(string icao, CancellationToken ct = default)
    {
        var acc = await AccDiAsync(icao, ct).ConfigureAwait(false);
        return acc is null ? Array.Empty<SpecialAreaPick>()
            : await _areas.ListSpecialAreasByAccAsync(acc, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SpecialAreaPick>> ListOtherAccSpecialAreasAsync(string icao, CancellationToken ct = default)
    {
        var acc = await AccDiAsync(icao, ct).ConfigureAwait(false);
        return acc is null ? Array.Empty<SpecialAreaPick>()
            : await _areas.ListSpecialAreasExcludingAccAsync(acc, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AccSpecialAreaView>> ResolveRegulatedAreasAsync(
        RegulatedSelection selection, CancellationToken ct = default)
    {
        // ⚠️ Stessa PROIEZIONE della vIPI ACC e dell'APP, non una copia: `SpecialAreaProjection` è il motore,
        // e la selezione non sa da quale documento arriva. Riscriverla qui vorrebbe dire due mappe che col
        // tempo divergono — e una delle due sbagliata senza che nessuno se ne accorga.
        var sel = Manuale(selection);
        var ids = sel.OwnIds.Concat(sel.ExtraIds).ToList();
        if (ids.Count == 0) return Array.Empty<AccSpecialAreaView>();
        // I testi delle aree li scrive la SORGENTE in inglese: si rendono nella lingua di chi legge.
        var traduci = _traduzioni is null ? null : await _traduzioni.DallaSorgenteAsync(ct).ConfigureAwait(false);
        return SpecialAreaProjection.Build(
            await _areas.GetSpecialAreasByIdsAsync(ids, ct).ConfigureAwait(false), ids, traduci);
    }

    private Task<string?> AccDiAsync(string icao, CancellationToken ct) =>
        _db.Airports.AsNoTracking().Where(a => a.Icao == Norm(icao))
            .Select(a => a.Acc!.Code).FirstOrDefaultAsync(ct);

    private static string Norm(string s) => (s ?? "").Trim().ToUpperInvariant();

    // ⚠️ Selezione sempre MANUALE: il modo automatico è del solo blocco Aerovia della vIPI ACC, e un JSON
    // che lo portasse — scritto a mano, o copiato da un blocco ACC — farebbe comparire aree mai scelte.
    private static RegulatedSelection Manuale(RegulatedSelection? sel) => new()
    {
        OwnAuto = false,
        OwnIds = sel?.OwnIds ?? new List<string>(),
        ExtraIds = sel?.ExtraIds ?? new List<string>(),
    };
}

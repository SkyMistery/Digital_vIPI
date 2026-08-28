using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Services;

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
                                     Vipi.Application.Translation.TranslationLookup? traduzioni = null)
    {
        _db = db;
        _airac = airac;
        _authz = authz;
        _editing = editing;
        _areas = areas;
        _traduzioni = traduzioni;
    }

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
                a.Icao, a.Name, AccCode = a.Acc!.Code, a.IsMilitaryOnly, a.MilDocumentId,
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
                                           c.MilDocumentId, pubblicati.Contains(c.Icao)))
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
            ?? throw new Vipi.Application.Aor.ValidationException($"Aeroporto {icao} inesistente.");

        // Stesso permesso del documento civile: l'edizione non cambia chi comanda su quello scalo.
        await _authz.EnsureCanEditAccAsync(
            (await _db.Accs.AsNoTracking().Where(f => f.Id == campo.AccId).Select(f => f.Code)
                .FirstOrDefaultAsync(ct).ConfigureAwait(false)) ?? "", ct).ConfigureAwait(false);

        if (campo.MilDocumentId is int esistente) return esistente;

        if (!campo.HasMilitaryPresence)
            // Meglio fermarsi che creare un vSOP militare su un campo che militare non è: il documento
            // resterebbe lì, vuoto, in un elenco dove nessuno saprebbe perché c'è.
            throw new Vipi.Application.Aor.ValidationException(
                $"{icao} non risulta avere presenza militare: la sorgente non lo dice.");

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

    public async Task<bool> HasPublishedAsync(string icao, CancellationToken ct = default)
    {
        icao = Norm(icao);
        // Il documento dev'esserci E avere una release effettiva: lo stesso gate dell'elenco pubblico,
        // scritto una volta per un aeroporto solo.
        if (!await _db.Airports.AsNoTracking()
                .AnyAsync(a => a.Icao == icao && a.MilDocumentId != null, ct).ConfigureAwait(false))
            return false;

        var adesso = DateTime.UtcNow;
        return await _db.DocReleases.AsNoTracking()
            .AnyAsync(r => r.TargetType == ReleaseTargetType.AirportMil && r.TargetKey == icao
                           && r.ReleaseEffectiveUtc <= adesso, ct).ConfigureAwait(false);
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

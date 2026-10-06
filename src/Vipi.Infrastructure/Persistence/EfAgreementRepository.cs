using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Aor;      // ValidationException: la UI cattura questa, mai quella di DataAnnotations
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using static Vipi.Application.Messaggio;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Implementazione EF di <see cref="IAgreementRepository"/>: accordi, sezioni, aeroporti e clausole.
///
/// <para><b>Lo scopo dell'outline è la SEZIONE.</b> Tutto ciò che sposta, annida o scioglie ragiona sulle
/// clausole di una sola sezione — quelle di un'altra non sono alternative delle prime, sono un'altra tabella.
/// Fino al 18 agosto 2026 lo scopo era la coppia <c>(accordo, verso)</c>, che è la stessa cosa detta con due
/// chiavi invece di una.</para>
/// </summary>
public sealed class EfAgreementRepository : IAgreementRepository
{
    private readonly VipiDbContext _db;
    public EfAgreementRepository(VipiDbContext db) => _db = db;

    // ---- lettura ------------------------------------------------------------------------------------

    public async Task<IReadOnlyList<AgreementRow>> ListByAccAsync(string accCode, CancellationToken ct = default)
    {
        var agreements = await AgreementsOf(accCode).AsNoTracking()
            .Include(a => a.OwnerAcc)
            .Include(a => a.SideASector)
            .Include(a => a.SideBSector)
            .Include(a => a.Sections).ThenInclude(s => s.Airports)
            .Include(a => a.Sections).ThenInclude(s => s.Clauses)
            .OrderBy(a => a.Order).ThenBy(a => a.Id)
            .ToListAsync(ct);

        // Le sezioni che questi accordi OSPITANO: stanno di casa in un altro accordo e compaiono anche qui. Chi
        // legge le riceve insieme a quelle di casa, col verso e il posto della LORO presenza — ed è per questo
        // che derivazione, frasi, vista live e matcher non sanno niente della condivisione.
        var ids = agreements.Select(a => a.Id).ToList();
        var ospiti = ids.Count == 0
            ? new List<AgreementSectionShare>()
            : await _db.AgreementSectionShares.AsNoTracking()
                .Where(h => ids.Contains(h.AgreementId))
                .Include(h => h.Section!).ThenInclude(s => s.Airports)
                .Include(h => h.Section!).ThenInclude(s => s.Clauses)
                .ToListAsync(ct);

        var sezioni = agreements.SelectMany(a => a.Sections.Select(s => s.Id))
            .Concat(ospiti.Select(h => h.SectionId)).Distinct().ToList();
        var presenze = await PresenzeAsync(sezioni, ct);
        var ospitiPerAccordo = ospiti.ToLookup(h => h.AgreementId);

        return agreements.Select(a => Map(a, ospitiPerAccordo[a.Id], presenze)).ToList();
    }

    /// <summary>
    /// Per ogni sezione <b>condivisa</b> fra quelle indicate, tutti gli accordi in cui compare: prima quello di
    /// casa, poi gli ospiti. Una sezione che sta in un accordo solo non c'è nel dizionario.
    /// </summary>
    private async Task<IReadOnlyDictionary<int, IReadOnlyList<AgreementShareRef>>> PresenzeAsync(
        IReadOnlyCollection<int> sectionIds, CancellationToken ct)
    {
        if (sectionIds.Count == 0) return new Dictionary<int, IReadOnlyList<AgreementShareRef>>();

        var righe = await _db.AgreementSectionShares.AsNoTracking()
            .Where(h => sectionIds.Contains(h.SectionId))
            .Select(h => new
            {
                h.SectionId, h.Order,
                Ospite = h.AgreementId,
                OspiteA = h.Agreement!.SideASector!.Callsign, OspiteB = h.Agreement.SideBSector!.Callsign,
                Casa = h.Section!.AgreementId,
                CasaA = h.Section.Agreement!.SideASector!.Callsign, CasaB = h.Section.Agreement.SideBSector!.Callsign,
            })
            .ToListAsync(ct);

        return righe.GroupBy(r => r.SectionId).ToDictionary(g => g.Key, g => (IReadOnlyList<AgreementShareRef>)
            new[] { new AgreementShareRef(g.First().Casa, g.First().CasaA, g.First().CasaB) }
                .Concat(g.OrderBy(r => r.Order).ThenBy(r => r.Ospite)
                    .Select(r => new AgreementShareRef(r.Ospite, r.OspiteA, r.OspiteB)))
                .ToList());
    }

    public async Task<int?> FindByPairAsync(string accCode, int sectorX, int sectorY, CancellationToken ct = default)
    {
        var (a, b) = Canonical(sectorX, sectorY);
        return await AgreementsOf(accCode)
            .Where(x => x.SideASectorId == a && x.SideBSectorId == b)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct);
    }

    // ---- accordo ------------------------------------------------------------------------------------

    public async Task<int> AddAgreementAsync(string accCode, AgreementInput input, CancellationToken ct = default)
    {
        var accId = await AccIdAsync(accCode, ct);
        var (sideA, sideB) = Canonical(input.SideASectorId, input.SideBSectorId);

        // La coppia è unica anche per indice; qui si risponde con una frase invece che con una violazione di
        // vincolo, e l'editor può proporre di aprire quello che c'è.
        if (await _db.CoordinationAgreements.AnyAsync(x => x.SideASectorId == sideA && x.SideBSectorId == sideB, ct))
            throw new ValidationException(Lingua(
                "Fra questi due enti esiste già un accordo: aggiungi una sezione a quello.",
                "These two units already have an agreement: add a section to that one."));

        var order = (await _db.CoordinationAgreements.Where(a => a.OwnerAccId == accId)
            .MaxAsync(a => (int?)a.Order, ct) ?? 0) + 1;

        var a = new CoordinationAgreement
        {
            OwnerAccId = accId,
            SideASectorId = sideA,
            SideBSectorId = sideB,
            Note = NullIfBlank(input.Note),
            Order = order,
        };
        _db.CoordinationAgreements.Add(a);
        await _db.SaveChangesAsync(ct);
        return a.Id;
    }

    /// <summary>
    /// Cambia i due capi e la nota.
    /// <para>⚠️ <b>Se i lati si scambiano, i versi delle sezioni si ribaltano con loro.</b> I lati stanno in
    /// forma canonica (id minore = A), quindi sostituire un ente può spostare l'altro dall'altra parte: lasciare
    /// i versi com'erano farebbe dire a ogni sezione il contrario di ciò che c'era scritto, <b>senza un
    /// errore</b>. È l'unico posto dove la canonizzazione si vede, ed è la ragione per cui il verso ha dovuto
    /// lasciare la clausola per la sezione.</para>
    /// </summary>
    public async Task UpdateAgreementAsync(string accCode, int agreementId, AgreementInput input, CancellationToken ct = default)
    {
        var a = await AgreementsOf(accCode).Include(x => x.Sections)
                    .FirstOrDefaultAsync(x => x.Id == agreementId, ct)
                ?? throw new InvalidOperationException(Lingua($"Accordo {agreementId} non riguarda la ACC {accCode}.", $"Agreement {agreementId} does not belong to ACC {accCode}."));

        var (sideA, sideB) = Canonical(input.SideASectorId, input.SideBSectorId);
        if (await _db.CoordinationAgreements
                .AnyAsync(x => x.Id != agreementId && x.SideASectorId == sideA && x.SideBSectorId == sideB, ct))
            throw new ValidationException(Lingua(
                "Fra questi due enti esiste già un altro accordo.",
                "These two units already have another agreement."));

        var swapped = (a.SideASectorId == sideB && sideB != a.SideBSectorId)
                      || (a.SideBSectorId == sideA && sideA != a.SideASectorId);

        a.SideASectorId = sideA;
        a.SideBSectorId = sideB;
        a.Note = NullIfBlank(input.Note);

        if (swapped)
        {
            foreach (var s in a.Sections) s.Direction = Flip(s.Direction);
            // Anche le sezioni OSPITI: il loro verso è scritto sui lati di questo accordo, come quello delle sue.
            foreach (var h in await _db.AgreementSectionShares.Where(h => h.AgreementId == a.Id).ToListAsync(ct))
                h.Direction = Flip(h.Direction);
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAgreementAsync(string accCode, int agreementId, CancellationToken ct = default)
    {
        var a = await AgreementsOf(accCode).Include(x => x.Sections).ThenInclude(s => s.Shares)
            .FirstOrDefaultAsync(x => x.Id == agreementId, ct);
        if (a is null) return;

        // 🔴 Le sezioni di casa qui che compaiono ANCHE altrove non devono andarsene con l'accordo: la cancellazione
        // scende in cascata sulle sezioni, e porterebbe via un contenuto che altri accordi stanno mostrando. Prima
        // la casa passa al primo ospite, e si salva — solo dopo l'accordo se ne va, con le sue sole sezioni.
        var condivise = a.Sections.Where(s => s.Shares.Count > 0).ToList();
        foreach (var s in condivise) Promuovi(s);
        if (condivise.Count > 0) await _db.SaveChangesAsync(ct);

        _db.CoordinationAgreements.Remove(a);   // sezioni, aeroporti, clausole e presenze ospiti seguono in cascade
        await _db.SaveChangesAsync(ct);
    }

    // ---- sezioni ------------------------------------------------------------------------------------

    public async Task<int> AddSectionAsync(string accCode, int agreementId, AgreementSectionInput input,
        CancellationToken ct = default)
    {
        var a = await AgreementAsync(accCode, agreementId, ct);
        var order = (await _db.AgreementSections.Where(s => s.AgreementId == a.Id)
            .MaxAsync(s => (int?)s.Order, ct) ?? 0) + 1;

        var section = new AgreementSection { AgreementId = a.Id, Order = order };
        ApplySection(section, input);
        _db.AgreementSections.Add(section);
        await _db.SaveChangesAsync(ct);
        return section.Id;
    }

    public async Task UpdateSectionAsync(string accCode, int sectionId, AgreementSectionInput input,
        int? agreementId = null, CancellationToken ct = default)
    {
        var (section, ospite) = await PresenzaAsync(accCode, sectionId, agreementId, ct);

        // Traffico, aeroporti e prosa sono della SEZIONE e valgono per tutti gli accordi che la portano. Il verso
        // no: è della presenza, perché i lati di ogni accordo sono canonici in un ordine suo.
        var versoDiCasa = section.Direction;
        ApplySection(section, input);
        if (ospite is not null)
        {
            section.Direction = versoDiCasa;
            ospite.Direction = input.Direction;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteSectionAsync(string accCode, int sectionId, CancellationToken ct = default)
    {
        var casa = await SectionsOf(accCode).Where(s => s.Id == sectionId)
            .Select(s => (int?)s.AgreementId).FirstOrDefaultAsync(ct);
        if (casa is int id) await RemoveSectionAsync(accCode, sectionId, id, ct);
    }

    // ---- sezioni condivise --------------------------------------------------------------------------
    // Una sezione COMPARE in un accordo o perché è di casa (AgreementSection.AgreementId) o perché è ospite
    // (AgreementSectionShare). La regola che regge tutto: il contenuto si distrugge solo quando se ne va l'ULTIMA
    // presenza. Carta docs/feature/2026-10-06-sezioni-condivise.md.

    public async Task<AgreementPresenceUndo?> RemoveSectionAsync(string accCode, int sectionId, int agreementId,
        CancellationToken ct = default)
    {
        var section = await SectionsOf(accCode).Include(s => s.Shares).FirstOrDefaultAsync(s => s.Id == sectionId, ct);
        if (section is null) return null;

        var prima = FotoDellePresenze(section);
        var staccata = TogliPresenza(section, agreementId);
        await _db.SaveChangesAsync(ct);
        // null = non c'è una presenza da rimettere: la sezione se n'è andata per intero (era l'ultima), o lì non
        // compariva. Chi annulla un'eliminazione vera rimette il contenuto dalla fotografia, come sempre.
        return staccata ? prima : null;
    }

    public async Task<AgreementShareResult> ShareSectionAsync(string accCode, int sectionId, int senderSectorId,
        int receiverSectorId, CancellationToken ct = default)
    {
        var section = await SectionsOf(accCode).Include(s => s.Shares).FirstOrDefaultAsync(s => s.Id == sectionId, ct)
                      ?? throw new InvalidOperationException(Lingua($"Sezione {sectionId} non riguarda la ACC {accCode}.", $"Section {sectionId} does not belong to ACC {accCode}."));

        var (arrivo, creato) = await AccordoDellaCoppiaAsync(accCode, senderSectorId, receiverSectorId, ct);
        var prima = FotoDellePresenze(section) with { CreatedAgreementId = creato ? arrivo.Id : null };

        // Ci compare già (di casa o da ospite): non c'è niente da fare, e non è un errore.
        if (arrivo.Id == section.AgreementId || section.Shares.Any(h => h.AgreementId == arrivo.Id))
            return new AgreementShareResult(arrivo.Id, false, Added: false, prima);

        section.Shares.Add(new AgreementSectionShare
        {
            AgreementId = arrivo.Id,
            // ⚠️ Il verso si calcola sui lati dell'accordo che OSPITA: sono canonici in un ordine suo.
            Direction = VersoDi(arrivo, senderSectorId),
            Order = await ProssimoPostoAsync(arrivo.Id, ct),
        });
        await _db.SaveChangesAsync(ct);
        return new AgreementShareResult(arrivo.Id, creato, Added: true, prima);
    }

    public async Task<AgreementDetachResult> DetachSectionAsync(string accCode, int sectionId, int agreementId,
        CancellationToken ct = default)
    {
        var section = await SectionsOf(accCode).Include(s => s.Shares).Include(s => s.Airports).Include(s => s.Clauses)
                          .FirstOrDefaultAsync(s => s.Id == sectionId, ct)
                      ?? throw new InvalidOperationException(Lingua($"Sezione {sectionId} non riguarda la ACC {accCode}.", $"Section {sectionId} does not belong to ACC {accCode}."));
        if (section.Shares.Count == 0)
            throw new ValidationException(Lingua("La sezione non è condivisa: non c'è niente da staccare.", "The section is not shared: there is nothing to detach."));

        var ospite = section.Shares.FirstOrDefault(h => h.AgreementId == agreementId);
        if (agreementId != section.AgreementId && ospite is null)
            throw new InvalidOperationException(Lingua($"La sezione {sectionId} non compare nell'accordo {agreementId}.", $"Section {sectionId} does not appear in agreement {agreementId}."));

        var prima = FotoDellePresenze(section);
        var copia = new AgreementSection
        {
            AgreementId = agreementId,
            Kind = section.Kind,
            Direction = ospite?.Direction ?? section.Direction,
            Description = section.Description,
            Order = ospite?.Order ?? section.Order,
        };
        foreach (var apt in section.Airports.OrderBy(x => x.Order))
            copia.Airports.Add(new AgreementAirport { Icao = apt.Icao, Name = apt.Name, Order = apt.Order });

        // I gruppi di varianti prendono numeri nuovi: sono progressivi per accordo, e la copia è un'altra tabella.
        var prossimo = await ClausesOfAgreement(agreementId).MaxAsync(c => (int?)c.VariantGroup, ct) ?? 0;
        var gruppi = new Dictionary<int, int>();
        foreach (var c in section.Clauses.OrderBy(x => x.Order))
        {
            var riga = CopyOf(c);
            riga.Order = c.Order;
            riga.VariantDepth = c.VariantDepth;
            riga.IsGroupWide = c.IsGroupWide;
            if (c.VariantGroup is int g)
            {
                if (!gruppi.TryGetValue(g, out var nuovo)) gruppi[g] = nuovo = ++prossimo;
                riga.VariantGroup = nuovo;
            }
            copia.Clauses.Add(riga);
        }
        // CopyOf porta con sé la sezione di origine: la copia è figlia della sezione nuova.
        foreach (var riga in copia.Clauses) riga.SectionId = 0;

        TogliPresenza(section, agreementId);
        _db.AgreementSections.Add(copia);
        await _db.SaveChangesAsync(ct);
        return new AgreementDetachResult(copia.Id, prima with { DetachedCopyId = copia.Id });
    }

    public async Task UndoPresenceAsync(string accCode, AgreementPresenceUndo undo, CancellationToken ct = default)
    {
        // ⚠️ La guardia è sugli ACCORDI e non sulla sezione: dopo aver tolto la sola presenza che questa ACC aveva,
        // la sezione non la riguarda più — ed è proprio il caso in cui l'annulla serve.
        var coinvolti = undo.Guests.Select(g => g.AgreementId).Append(undo.Home.AgreementId).Distinct().ToList();
        var vivi = (await _db.CoordinationAgreements.Where(a => coinvolti.Contains(a.Id)).Select(a => a.Id).ToListAsync(ct)).ToHashSet();
        if (!await AgreementsOf(accCode).AnyAsync(a => coinvolti.Contains(a.Id), ct))
            throw new InvalidOperationException(Lingua($"La sezione {undo.SectionId} non riguarda la ACC {accCode}.", $"Section {undo.SectionId} does not belong to ACC {accCode}."));

        if (undo.DetachedCopyId is int copiaId
            && await _db.AgreementSections.FirstOrDefaultAsync(s => s.Id == copiaId, ct) is { } copia)
            _db.AgreementSections.Remove(copia);

        var section = await _db.AgreementSections.Include(s => s.Shares).FirstOrDefaultAsync(s => s.Id == undo.SectionId, ct);
        // La sezione o il suo accordo di casa non ci sono più: non si inventa niente.
        if (section is not null && vivi.Contains(undo.Home.AgreementId))
        {
            section.AgreementId = undo.Home.AgreementId;
            section.Direction = undo.Home.Direction;
            section.Order = undo.Home.Order;

            var attesi = undo.Guests.Where(g => vivi.Contains(g.AgreementId) && g.AgreementId != undo.Home.AgreementId)
                .ToDictionary(g => g.AgreementId);
            foreach (var h in section.Shares.Where(h => !attesi.ContainsKey(h.AgreementId)).ToList())
            {
                section.Shares.Remove(h);
                _db.AgreementSectionShares.Remove(h);
            }
            foreach (var g in attesi.Values)
            {
                var h = section.Shares.FirstOrDefault(x => x.AgreementId == g.AgreementId);
                if (h is null) section.Shares.Add(h = new AgreementSectionShare { AgreementId = g.AgreementId });
                h.Direction = g.Direction;
                h.Order = g.Order;
            }
        }
        await _db.SaveChangesAsync(ct);

        // L'accordo nato per ospitarla se ne va, ma SOLO se è rimasto vuoto.
        if (undo.CreatedAgreementId is int nato
            && await _db.CoordinationAgreements.FirstOrDefaultAsync(a => a.Id == nato, ct) is { } accordo
            && !await _db.AgreementSections.AnyAsync(s => s.AgreementId == nato, ct)
            && !await _db.AgreementSectionShares.AnyAsync(h => h.AgreementId == nato, ct))
        {
            _db.CoordinationAgreements.Remove(accordo);
            await _db.SaveChangesAsync(ct);
        }
    }

    /// <summary>La sezione e, se <paramref name="agreementId"/> è un accordo che la ospita, la sua presenza lì.
    /// Senza accordo, o con quello di casa, la presenza è la sezione stessa.</summary>
    private async Task<(AgreementSection Section, AgreementSectionShare? Ospite)> PresenzaAsync(string accCode,
        int sectionId, int? agreementId, CancellationToken ct)
    {
        var section = await SectionsOf(accCode).Include(s => s.Shares).Include(s => s.Airports)
                          .FirstOrDefaultAsync(s => s.Id == sectionId, ct)
                      ?? throw new InvalidOperationException(Lingua($"Sezione {sectionId} non riguarda la ACC {accCode}.", $"Section {sectionId} does not belong to ACC {accCode}."));
        if (agreementId is null || agreementId == section.AgreementId) return (section, null);

        var ospite = section.Shares.FirstOrDefault(h => h.AgreementId == agreementId)
                     ?? throw new InvalidOperationException(Lingua($"La sezione {sectionId} non compare nell'accordo {agreementId}.", $"Section {sectionId} does not appear in agreement {agreementId}."));
        return (section, ospite);
    }

    private static AgreementPresenceUndo FotoDellePresenze(AgreementSection s) => new(
        s.Id, new AgreementSharePlacement(s.AgreementId, s.Direction, s.Order),
        s.Shares.OrderBy(h => h.Order).ThenBy(h => h.Id)
            .Select(h => new AgreementSharePlacement(h.AgreementId, h.Direction, h.Order)).ToList());

    /// <summary>
    /// Toglie la sezione da quell'accordo. Vero se è stata solo <b>staccata</b> (vive ancora altrove); falso se
    /// se n'è andata per intero, o se lì non compariva. Non salva.
    /// </summary>
    private bool TogliPresenza(AgreementSection section, int agreementId)
    {
        if (agreementId == section.AgreementId)
        {
            if (section.Shares.Count == 0) { _db.AgreementSections.Remove(section); return false; }
            Promuovi(section);
            return true;
        }

        var ospite = section.Shares.FirstOrDefault(h => h.AgreementId == agreementId);
        if (ospite is null) return false;
        section.Shares.Remove(ospite);
        _db.AgreementSectionShares.Remove(ospite);
        return true;
    }

    /// <summary>
    /// La sezione lascia il suo accordo di casa ma ha ospiti: la casa passa al primo, col verso e il posto che la
    /// sezione aveva lì. È l'unico punto dove «casa» e «ospite» si scambiano, e nessun lettore se ne accorge.
    /// </summary>
    private void Promuovi(AgreementSection section)
    {
        var erede = section.Shares.OrderBy(h => h.Order).ThenBy(h => h.Id).First();
        section.AgreementId = erede.AgreementId;
        section.Direction = erede.Direction;
        section.Order = erede.Order;
        section.Shares.Remove(erede);
        _db.AgreementSectionShares.Remove(erede);
    }

    /// <summary>Il posto in coda fra le sezioni di un accordo, contando quelle di casa e quelle che ospita.</summary>
    private async Task<int> ProssimoPostoAsync(int agreementId, CancellationToken ct) =>
        Math.Max(
            await _db.AgreementSections.Where(s => s.AgreementId == agreementId).MaxAsync(s => (int?)s.Order, ct) ?? 0,
            await _db.AgreementSectionShares.Where(h => h.AgreementId == agreementId).MaxAsync(h => (int?)h.Order, ct) ?? 0) + 1;

    public async Task<int?> CopySectionToReverseAsync(string accCode, int sectionId, int? agreementId = null,
        CancellationToken ct = default)
    {
        var src = await SectionsOf(accCode)
                      .Include(s => s.Airports).Include(s => s.Clauses).Include(s => s.Shares)
                      .FirstOrDefaultAsync(s => s.Id == sectionId, ct)
                  ?? throw new InvalidOperationException(Lingua($"Sezione {sectionId} non riguarda la ACC {accCode}.", $"Section {sectionId} does not belong to ACC {accCode}."));

        // Una sezione ospite si copia nell'accordo da cui la si guarda, e dal verso che ha LÌ: il reciproco è di
        // quell'accordo, e nasce come sezione sua.
        var ospite = agreementId is int richiesto && richiesto != src.AgreementId
            ? src.Shares.FirstOrDefault(h => h.AgreementId == richiesto)
              ?? throw new InvalidOperationException(Lingua($"La sezione {sectionId} non compare nell'accordo {richiesto}.", $"Section {sectionId} does not appear in agreement {richiesto}."))
            : null;
        var casa = ospite?.AgreementId ?? src.AgreementId;

        var reverse = Flip(ospite?.Direction ?? src.Direction);
        var key = AirportKey(src.Airports.Select(x => x.Icao));

        // Se il reciproco c'è già non si tocca: sovrascriverlo sarebbe buttare via ciò che qualcuno ha scritto,
        // e accodarlo produrrebbe un doppione di ogni clausola. Vale anche se a dirlo è una sezione ospite.
        var esiste = await _db.AgreementSections.Include(s => s.Airports)
            .Where(s => s.Kind == src.Kind
                        && ((s.AgreementId == casa && s.Direction == reverse)
                            || s.Shares.Any(h => h.AgreementId == casa && h.Direction == reverse)))
            .ToListAsync(ct);
        if (esiste.Any(s => AirportKey(s.Airports.Select(x => x.Icao)) == key)) return null;

        var order = await ProssimoPostoAsync(casa, ct);

        var copy = new AgreementSection
        {
            AgreementId = casa,
            Kind = src.Kind,
            Direction = reverse,
            Description = src.Description,
            Order = order,
        };
        var airportOrder = 0;
        foreach (var apt in src.Airports.OrderBy(x => x.Order))
            copy.Airports.Add(new AgreementAirport { Icao = apt.Icao, Name = apt.Name, Order = ++airportOrder });

        // I gruppi di varianti si rinumerano dentro la copia: sono progressivi per accordo, e riusarli farebbe
        // sembrare le clausole del verso opposto varianti delle prime.
        var nextGroup = await ClausesOfAgreement(casa).MaxAsync(c => (int?)c.VariantGroup, ct) ?? 0;
        var groupMap = new Dictionary<int, int>();

        foreach (var c in src.Clauses.OrderBy(x => x.Order))
        {
            var copia = CopyOf(c);
            // CopyOf porta con sé la sezione di origine: la copia è figlia della sezione nuova.
            copia.SectionId = 0;
            copia.Order = c.Order;
            copia.VariantDepth = c.VariantDepth;
            copia.IsGroupWide = c.IsGroupWide;
            if (c.VariantGroup is int g)
            {
                if (!groupMap.TryGetValue(g, out var mapped)) groupMap[g] = mapped = ++nextGroup;
                copia.VariantGroup = mapped;
            }
            copy.Clauses.Add(copia);
        }

        _db.AgreementSections.Add(copy);
        await _db.SaveChangesAsync(ct);
        return copy.Id;
    }

    public async Task<int> MergeSectionsAsync(string accCode, int keepId, int absorbId, CancellationToken ct = default)
    {
        if (keepId == absorbId) return 0;

        var keep = await SectionsOf(accCode).Include(s => s.Airports)
                       .FirstOrDefaultAsync(s => s.Id == keepId, ct)
                   ?? throw new InvalidOperationException(Lingua($"Sezione {keepId} non riguarda la ACC {accCode}.", $"Section {keepId} does not belong to ACC {accCode}."));
        var absorb = await SectionsOf(accCode).Include(s => s.Airports)
                         .FirstOrDefaultAsync(s => s.Id == absorbId, ct)
                     ?? throw new InvalidOperationException(Lingua($"Sezione {absorbId} non riguarda la ACC {accCode}.", $"Section {absorbId} does not belong to ACC {accCode}."));

        // Una sezione condivisa non si unisce: l'unione ne cambierebbe il contenuto anche negli altri accordi che
        // la portano, senza che chi li guarda l'abbia chiesto.
        if (await _db.AgreementSectionShares.AnyAsync(h => h.SectionId == keepId || h.SectionId == absorbId, ct))
            throw new ValidationException(Lingua(
                "Una delle due sezioni è condivisa con un altro accordo: staccala prima di unirle.",
                "One of the two sections is shared with another agreement: detach it before merging."));

        // ⚠️ Le condizioni si rivalidano QUI e non si dànno per buone dalla segnalazione: fra il cruscotto e il
        // tasto l'archivio può essere cambiato, e unire due tabelle che dicono cose diverse le mescolerebbe
        // senza che nessuno possa più separarle.
        if (keep.AgreementId != absorb.AgreementId || keep.Kind != absorb.Kind || keep.Direction != absorb.Direction
            || AirportKey(keep.Airports.Select(x => x.Icao)) != AirportKey(absorb.Airports.Select(x => x.Icao)))
            throw new ValidationException(Lingua("Le due sezioni non dicono la stessa cosa: si uniscono solo le gemelle.", "The two sections do not say the same thing: only twins can be merged."));

        var moving = await _db.AgreementClauses.Where(c => c.SectionId == absorbId)
            .OrderBy(c => c.Order).ToListAsync(ct);

        var order = await _db.AgreementClauses.Where(c => c.SectionId == keepId)
            .MaxAsync(c => (int?)c.Order, ct) ?? 0;
        var nextGroup = await ClausesOfAgreement(keep.AgreementId).MaxAsync(c => (int?)c.VariantGroup, ct) ?? 0;
        var groupMap = new Dictionary<int, int>();

        foreach (var c in moving)
        {
            c.SectionId = keepId;
            c.Order = ++order;
            if (c.VariantGroup is int g)
            {
                if (!groupMap.TryGetValue(g, out var mapped)) groupMap[g] = mapped = ++nextGroup;
                c.VariantGroup = mapped;
            }
        }

        // Il guscio se ne va DOPO che le clausole hanno cambiato padre: cancellarlo prima le porterebbe con sé
        // in cascade, e l'unione perderebbe proprio ciò che doveva salvare.
        await _db.SaveChangesAsync(ct);
        _db.AgreementSections.Remove(absorb);
        await _db.SaveChangesAsync(ct);

        return moving.Count;
    }

    /// <summary>
    /// Riscrive gli aeroporti al posto di aggiornarli uno per uno. La differenza si vede quando l'editore ne
    /// toglie uno: con l'aggiornamento «per differenza» servirebbe sapere quale riga togliere, e l'editor
    /// dovrebbe portarsi dietro gli id — cioè conoscere la persistenza per modificare un elenco. Qui l'elenco è
    /// il dato, e chi lo scrive lo scrive intero.
    /// </summary>
    private void ApplySection(AgreementSection s, AgreementSectionInput i)
    {
        s.Kind = i.Kind;
        s.Direction = i.Direction;
        s.Description = NullIfBlank(i.Description);

        _db.AgreementAirports.RemoveRange(s.Airports);
        s.Airports.Clear();
        var order = 0;
        foreach (var apt in i.Airports)
            s.Airports.Add(new AgreementAirport
            {
                Icao = apt.Icao.Trim().ToUpperInvariant(),
                // Il nome si tiene solo per gli scali fuori catalogo, dove è l'unica fonte. Per gli altri
                // arriva dal catalogo, e una copia qui divergerebbe alla prima rinomina.
                Name = NullIfBlank(apt.Name),
                Order = ++order,
            });
    }

    // ---- clausole -----------------------------------------------------------------------------------

    public async Task<int> AddClauseAsync(string accCode, int sectionId, AgreementClauseInput input,
        CancellationToken ct = default)
    {
        var section = await SectionsOf(accCode).FirstOrDefaultAsync(s => s.Id == sectionId, ct)
                      ?? throw new InvalidOperationException(Lingua($"Sezione {sectionId} non riguarda la ACC {accCode}.", $"Section {sectionId} does not belong to ACC {accCode}."));

        var order = (await Scope(section.Id).MaxAsync(c => (int?)c.Order, ct) ?? 0) + 1;

        var c = new AgreementClause { SectionId = section.Id, Order = order };
        ApplyClause(c, input);
        _db.AgreementClauses.Add(c);
        await _db.SaveChangesAsync(ct);
        return c.Id;
    }

    /// <summary>🔴 U-178: le clausole in coda alla sezione in UN <c>SaveChanges</c>, cioè in una transazione: entrano
    /// tutte o nessuna.</summary>
    public async Task<int> AddClausesAsync(string accCode, int sectionId, IReadOnlyList<AgreementClauseInput> inputs,
        CancellationToken ct = default)
    {
        var section = await SectionsOf(accCode).FirstOrDefaultAsync(s => s.Id == sectionId, ct)
                      ?? throw new InvalidOperationException(Lingua($"Sezione {sectionId} non riguarda la ACC {accCode}.", $"Section {sectionId} does not belong to ACC {accCode}."));

        var order = await Scope(section.Id).MaxAsync(c => (int?)c.Order, ct) ?? 0;
        foreach (var input in inputs)
        {
            var c = new AgreementClause { SectionId = section.Id, Order = ++order };
            ApplyClause(c, input);
            _db.AgreementClauses.Add(c);
        }
        await _db.SaveChangesAsync(ct);
        return inputs.Count;
    }

    public async Task UpdateClauseAsync(string accCode, int clauseId, AgreementClauseInput input, CancellationToken ct = default)
    {
        var c = await ClauseInAccAsync(accCode, clauseId, ct);
        ApplyClause(c, input);

        if (c.VariantGroup is int group)
        {
            if (c.IsGroupWide && c.VariantDepth > 0)
                throw new ValidationException(Lingua("Una clausola «in ogni caso» non può essere l'eccezione di un'altra.", "An «in any case» clause cannot be the exception to another one."));

            // I PUNTI sono l'identità dell'accordo dentro un gruppo — le varianti sono lo stesso accordo detto a
            // condizioni diverse — quindi cambiarli su una clausola li cambia sulle sorelle. Propagare è meglio
            // che rifiutare: l'invariante resta vera senza chiedere di ripetere la stessa modifica su ognuna.
            foreach (var s in await _db.AgreementClauses
                         .Where(x => x.SectionId == c.SectionId && x.VariantGroup == group && x.Id != c.Id)
                         .ToListAsync(ct))
                s.Cops = c.Cops;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteClauseAsync(string accCode, int clauseId, CancellationToken ct = default)
    {
        var c = await ClausesOf(accCode).FirstOrDefaultAsync(x => x.Id == clauseId, ct);
        if (c is null) return;
        var group = c.VariantGroup;
        _db.AgreementClauses.Remove(c);
        if (group is int g) await DissolveIfAloneAsync(c.SectionId, g, ct);
        await _db.SaveChangesAsync(ct);
    }

    public Task<int> AddAlternativeAsync(string accCode, int clauseId, CancellationToken ct = default) =>
        AddVariantAsync(accCode, clauseId, asException: false, ct);

    public Task<int> AddExceptionAsync(string accCode, int clauseId, CancellationToken ct = default) =>
        AddVariantAsync(accCode, clauseId, asException: true, ct);

    /// <summary>
    /// Nasce una clausola nell'outline del gruppo, copiata dalla sorgente meno la condizione — che è
    /// esattamente ciò che deve dire di diverso. I dati restano piatti (nessuna eredità di campo: con un livello
    /// nullable, «null = eredita» sarebbe indistinguibile da «null = non specificato»), ma chi scrive non
    /// ridigita venti campi per cambiarne uno.
    /// </summary>
    private async Task<int> AddVariantAsync(string accCode, int clauseId, bool asException, CancellationToken ct)
    {
        var src = await ClauseInAccAsync(accCode, clauseId, ct);

        // Il gruppo nasce alla prima variante: progressivo per ACCORDO (non per sezione), così due gruppi «1» di
        // sezioni diverse non si somigliano leggendo l'archivio a mano.
        if (src.VariantGroup is null)
            src.VariantGroup = await NextGroupAsync(src.SectionId, ct);
        var group = src.VariantGroup.Value;

        // Un'alternativa di una clausola annidata resta al livello di QUELLA clausola, non torna a 0:
        // «pari-grado alla sorgente» è la promessa del tasto, e vale a qualunque profondità.
        var depth = asException ? src.VariantDepth + 1 : src.VariantDepth;

        var rows = await GroupRowsAsync(src, group, ct);
        // L'eccezione va subito sotto la sorgente; l'alternativa dopo l'ultimo discendente della sorgente,
        // altrimenti spezzerebbe in due un blocco già scritto.
        var after = asException ? src : Subtree(rows, src)[^1];

        var copy = CopyOf(src);
        copy.VariantGroup = group;
        copy.VariantDepth = depth;
        copy.Order = after.Order + 1;
        // ⚠️ La CONDIZIONE no: è ciò che la clausola nuova deve dire di diverso, e copiarla darebbe due clausole
        // identiche. CopyOf la porta perché serve alla duplicazione del gruppo, dove invece va tenuta.
        copy.ConditionLabel = null; copy.ConditionRefId = null;
        copy.ConditionAreaLabel = null; copy.ConditionAreaNegated = false; copy.ConditionAreaAll = false;
        copy.ConditionCustomLabel = null;

        foreach (var x in await Scope(src.SectionId).Where(x => x.Order > after.Order).ToListAsync(ct))
            x.Order++;

        _db.AgreementClauses.Add(copy);
        await _db.SaveChangesAsync(ct);
        return copy.Id;
    }

    /// <summary>
    /// La copia di UNA clausola, subito sotto l'originale. È l'alternativa con la condizione tenuta: si
    /// duplica una riga per scriverne una quasi uguale, e la condizione è metà di ciò che si sta copiando.
    /// </summary>
    public async Task<int> DuplicateClauseAsync(string accCode, int clauseId, CancellationToken ct = default)
    {
        var src = await ClauseInAccAsync(accCode, clauseId, ct);

        // Dopo il SOTTOALBERO della sorgente, non subito dopo la sua riga: le eccezioni descrivono la clausola
        // che le ospita, e una copia infilata in mezzo se le prenderebbe — l'appartenenza qui è per ordine, e
        // cambia significato senza dare errore.
        var after = src.VariantGroup is int g ? Subtree(await GroupRowsAsync(src, g, ct), src)[^1] : src;

        var copy = CopyOf(src);
        // ⚠️ Il gruppo NON nasce qui, al contrario dell'alternativa: due righe indipendenti restano
        // indipendenti. Un gruppo aperto di nascosto legherebbe i PUNTI delle due — `UpdateClauseAsync` li
        // propaga alle sorelle — e la copia comincerebbe a riscrivere l'originale.
        copy.VariantGroup = src.VariantGroup;
        copy.VariantDepth = src.VariantDepth;
        copy.IsGroupWide = src.IsGroupWide;
        copy.Order = after.Order + 1;

        foreach (var x in await Scope(src.SectionId).Where(x => x.Order > after.Order).ToListAsync(ct))
            x.Order++;

        _db.AgreementClauses.Add(copy);
        await _db.SaveChangesAsync(ct);
        return copy.Id;
    }

    public async Task DetachVariantAsync(string accCode, int clauseId, CancellationToken ct = default)
    {
        var c = await ClauseInAccAsync(accCode, clauseId, ct);
        if (c.VariantGroup is not int group) return;

        // Sfilare una clausola porta via il suo SOTTOALBERO: le eccezioni descrivono la clausola che le ospita,
        // e lasciarle indietro le riassegnerebbe in silenzio a quella di sopra — cambiando ciò che dicono.
        var moved = Subtree(await GroupRowsAsync(c, group, ct), c);

        var shift = c.VariantDepth;
        foreach (var x in moved) { x.VariantDepth -= shift; x.IsGroupWide = false; }

        // Resta un gruppo solo se ha ancora qualcosa da tenere insieme; una clausola sola non è un gruppo.
        var newGroup = moved.Count > 1 ? await NextGroupAsync(c.SectionId, ct) : (int?)null;
        foreach (var x in moved) x.VariantGroup = newGroup;

        await DissolveIfAloneAsync(c.SectionId, group, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task MoveClauseAsync(string accCode, int clauseId, bool up, CancellationToken ct = default)
    {
        var c = await ClauseInAccAsync(accCode, clauseId, ct);
        var rows = await ScopeRowsAsync(c, ct);

        // Si muove il BLOCCO, non la riga: una capofila che si sposta lasciando indietro le sue eccezioni le
        // riassegna a quella di sopra, e quelle continuano a dire quello che dicevano di un'altra alternativa.
        // Nessun errore, significato cambiato: è la trappola dell'appartenenza per ordine.
        var block = Subtree(rows, c);
        var first = rows.IndexOf(block[0]);
        var last = first + block.Count - 1;

        // Il vicino nella stessa sezione è a sua volta un blocco: si scavalca intero, non riga per riga.
        List<AgreementClause>? neighbour = null;
        if (up && first > 0) neighbour = Subtree(rows, RootOf(rows, first - 1));
        else if (!up && last < rows.Count - 1) neighbour = Subtree(rows, rows[last + 1]);
        if (neighbour is null) return;   // estremo: no-op

        var reordered = new List<AgreementClause>(rows);
        reordered.RemoveAll(block.Contains);
        var anchor = reordered.IndexOf(up ? neighbour[0] : neighbour[^1]);
        reordered.InsertRange(up ? anchor : anchor + 1, block);
        Renumber(reordered);

        await _db.SaveChangesAsync(ct);
    }

    public async Task MoveClauseToAsync(string accCode, int clauseId, int targetClauseId, CancellationToken ct = default)
    {
        var c = await ClauseInAccAsync(accCode, clauseId, ct);
        var target = await ClauseInAccAsync(accCode, targetClauseId, ct);
        // Fra sezioni diverse non si trascina: cambiare la sezione di una clausola è dire un'altra cosa, non
        // spostarla — sono due tabelle, e il traffico o il verso cambierebbero sotto la riga.
        if (c.Id == target.Id || c.SectionId != target.SectionId) return;

        var rows = await ScopeRowsAsync(c, ct);
        var block = Subtree(rows, c);
        if (block.Any(x => x.Id == target.Id)) return;   // dentro sé stesso: non c'è dove andare

        var scendendo = target.Order > c.Order;
        rows.RemoveAll(block.Contains);
        var at = rows.IndexOf(target);
        if (at < 0) return;
        // Scendendo si va DOPO il bersaglio, salendo PRIMA: è quello che si aspetta chi trascina.
        rows.InsertRange(scendendo ? at + 1 : at, block);
        Renumber(rows);

        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> DuplicateVariantGroupAsync(string accCode, int clauseId, CancellationToken ct = default)
    {
        var c = await ClauseInAccAsync(accCode, clauseId, ct);
        if (c.VariantGroup is not int group) return 0;

        var rows = await GroupRowsAsync(c, group, ct);
        if (rows.Count == 0) return 0;

        var newGroup = await NextGroupAsync(c.SectionId, ct);
        var order = await Scope(c.SectionId).MaxAsync(x => (int?)x.Order, ct) ?? 0;

        foreach (var src in rows)
        {
            var copy = CopyOf(src);
            copy.VariantGroup = newGroup;
            // La struttura si copia com'è: profondità e clausole trasversali sono ciò che rende utile duplicare
            // un gruppo invece delle sue righe una per una.
            copy.VariantDepth = src.VariantDepth;
            copy.IsGroupWide = src.IsGroupWide;
            copy.Order = ++order;
            _db.AgreementClauses.Add(copy);
        }

        await _db.SaveChangesAsync(ct);
        return rows.Count;
    }

    // ---- modifica in blocco -------------------------------------------------------------------------

    public async Task<int> SetLevelAsync(string accCode, IReadOnlyList<int> clauseIds, ParsedLevel level,
        CancellationToken ct = default)
    {
        var rows = await ClausesInAccAsync(accCode, clauseIds, ct);
        foreach (var r in rows)
        {
            r.LevelConstraint = level.Constraint;
            r.LevelValue = level.Constraint == LevelConstraint.Special ? null : level.Value;
            r.LevelSpecial = level.Constraint == LevelConstraint.Special ? NullIfBlank(level.Special) : null;
            r.LevelUnit = level.Unit;
            r.Parity = level.Parity;
            r.VerticalState = level.VerticalState;
        }

        // Nessuna propagazione al gruppo: il livello è della singola clausola, ed è proprio ciò che due varianti
        // dicono diverso. Propagarlo le renderebbe tutte uguali.
        await _db.SaveChangesAsync(ct);
        return rows.Count;
    }

    public async Task<int> SetConditionAsync(string accCode, IReadOnlyList<int> clauseIds, string? areaLabel,
        bool areaNegated, bool areaAll, string? customLabel, CancellationToken ct = default)
    {
        var rows = await ClausesInAccAsync(accCode, clauseIds, ct);

        // La regola di ValidateClause (U-154): una clausola «in ogni caso» deve dire a quali condizioni vale. La
        // pista non si tocca qui, quindi basta lei; senza, la barra svuoterebbe ciò che il pannello non salverebbe.
        // Controllata PRIMA di toccare le righe: tracciate e modificate, un salvataggio dopo le scriverebbe.
        if (NullIfBlank(areaLabel) is null && NullIfBlank(customLabel) is null
            && rows.Any(r => r.IsGroupWide && string.IsNullOrWhiteSpace(r.ConditionLabel)))
            throw new ValidationException(Lingua(
                "Una clausola «in ogni caso» deve dire a quali condizioni vale.",
                "A «in any case» clause has to say under which conditions it applies."));

        foreach (var r in rows)
        {
            r.ConditionAreaLabel = NullIfBlank(areaLabel);
            // ⚠️ La polarità segue l'etichetta: senza area non vuol dire niente, e lasciata accesa
            // riaccenderebbe il rovescio alla prossima area scritta.
            r.ConditionAreaNegated = r.ConditionAreaLabel is not null && areaNegated;
            r.ConditionAreaAll = r.ConditionAreaLabel is not null && areaAll;
            r.ConditionCustomLabel = NullIfBlank(customLabel);
        }

        // La pista non si tocca, e non è una dimenticanza: dipende dall'aeroporto, e la stessa sigla su scali
        // diversi è una pista diversa. Una sezione con quattro aeroporti lo rende ancora più vero di prima.
        await _db.SaveChangesAsync(ct);
        return rows.Count;
    }

    public async Task<int> DeleteClausesAsync(string accCode, IReadOnlyList<int> clauseIds, CancellationToken ct = default)
    {
        var rows = await ClausesInAccAsync(accCode, clauseIds, ct);
        if (rows.Count == 0) return 0;

        var groups = rows.Where(r => r.VariantGroup is not null)
            .Select(r => (r.SectionId, Group: r.VariantGroup!.Value)).Distinct().ToList();

        _db.AgreementClauses.RemoveRange(rows);
        foreach (var (sectionId, group) in groups)
            await DissolveIfAloneAsync(sectionId, group, ct);
        await _db.SaveChangesAsync(ct);
        return rows.Count;
    }

    // ---- ripristino ---------------------------------------------------------------------------------

    public async Task<int> RestoreAgreementAsync(string accCode, AgreementSnapshot snapshot, CancellationToken ct = default)
    {
        // ⚠️ Il ripristino è FUORI dalle regole di proposito: un annulla che rifiutasse di rimettere ciò che ha
        // appena cancellato sarebbe peggio della regola. Passa comunque dalla forma canonica, perché quella non
        // è una regola editoriale ma la chiave dell'archivio.
        var accId = await AccIdAsync(accCode, ct);
        var (sideA, sideB) = Canonical(snapshot.Data.SideASectorId, snapshot.Data.SideBSectorId);
        var order = (await _db.CoordinationAgreements.Where(a => a.OwnerAccId == accId)
            .MaxAsync(a => (int?)a.Order, ct) ?? 0) + 1;

        var a = new CoordinationAgreement
        {
            OwnerAccId = accId,
            SideASectorId = sideA,
            SideBSectorId = sideB,
            Note = NullIfBlank(snapshot.Data.Note),
            Order = order,
        };
        foreach (var s in snapshot.Sections.OrderBy(x => x.Order))
        {
            // Una sezione che era condivisa torna come PRESENZA di quella che vive ancora altrove: rimetterne il
            // contenuto ne farebbe una copia, e da lì in poi le due divergerebbero senza che nessuno l'abbia scelto.
            if (s.SharedSectionId is int condivisa && await _db.AgreementSections.AnyAsync(x => x.Id == condivisa, ct))
                a.SharedSections.Add(new AgreementSectionShare { SectionId = condivisa, Direction = s.Data.Direction, Order = s.Order });
            else
                a.Sections.Add(SectionFrom(s));
        }

        // Prima di scrivere (U-061): rifiutata dopo il SaveChanges, la fotografia rotta restava salvata.
        foreach (var s in a.Sections) ControllaOutline(s.Clauses);
        _db.CoordinationAgreements.Add(a);
        await _db.SaveChangesAsync(ct);
        return a.Id;
    }

    public async Task<int?> RestoreSectionAsync(string accCode, AgreementSectionRestore restore, CancellationToken ct = default)
    {
        // Solo negli accordi che esistono ancora: ricrearne uno per ospitare la sezione sarebbe inventare una
        // relazione che nessuno ha scritto.
        var a = await AgreementsOf(accCode).FirstOrDefaultAsync(x => x.Id == restore.AgreementId, ct);
        if (a is null) return null;

        // Era condivisa e vive ancora altrove: torna come presenza, non come copia (vedi RestoreAgreementAsync).
        if (restore.Section.SharedSectionId is int condivisa
            && await _db.AgreementSections.Include(x => x.Shares).FirstOrDefaultAsync(x => x.Id == condivisa, ct) is { } viva)
        {
            if (viva.AgreementId != a.Id && viva.Shares.All(h => h.AgreementId != a.Id))
            {
                viva.Shares.Add(new AgreementSectionShare
                {
                    AgreementId = a.Id, Direction = restore.Section.Data.Direction, Order = restore.Section.Order,
                });
                await _db.SaveChangesAsync(ct);
            }
            return viva.Id;
        }

        var section = SectionFrom(restore.Section);
        section.AgreementId = a.Id;
        // L'outline vive dentro la sezione, e questa arriva intera: si controlla da sola, prima di scrivere.
        ControllaOutline(section.Clauses);
        _db.AgreementSections.Add(section);
        await _db.SaveChangesAsync(ct);
        return section.Id;
    }

    public async Task<int> RestoreClausesAsync(string accCode, IReadOnlyList<AgreementClauseRestore> clauses,
        IReadOnlyList<AgreementOutlineRestore>? sorelle = null, CancellationToken ct = default)
    {
        if (clauses.Count == 0) return 0;

        var ids = clauses.Select(c => c.SectionId).Distinct().ToList();
        var alive = (await SectionsOf(accCode).Where(s => ids.Contains(s.Id)).Select(s => s.Id).ToListAsync(ct))
            .ToHashSet();

        var restored = clauses.Where(c => alive.Contains(c.SectionId)).Select(c => ClauseFrom(c.SectionId, c.Clause)).ToList();
        var esistenti = await _db.AgreementClauses.Where(c => alive.Contains(c.SectionId)).ToListAsync(ct);

        // U-061: l'eliminazione ha sciolto il gruppo rimasto di una, e la superstite ha perso gruppo, profondità e
        // «in ogni caso». Si rimettono solo a chi è ancora fuori da ogni gruppo: una sorella messa altrove nel
        // frattempo è una scelta successiva, e l'annulla non la disfa.
        var perId = esistenti.ToDictionary(c => c.Id);
        var rientri = (sorelle ?? [])
            .Where(o => perId.TryGetValue(o.ClauseId, out var c) && c.VariantGroup is null)
            .ToDictionary(o => o.ClauseId);

        // L'outline come SARÀ, controllato prima di toccare qualunque riga tracciata: rifiutato dopo il
        // SaveChanges, l'orfano restava salvato; rifiutato dopo aver modificato le sorelle, un salvataggio
        // successivo dello stesso contesto le scriverebbe lo stesso.
        ControllaOutline(esistenti.Select(c => rientri.TryGetValue(c.Id, out var o)
                ? new PostoInOutline(c.SectionId, c.Order, o.VariantGroup, o.VariantDepth, o.IsGroupWide, c.Cops)
                : PostoDi(c))
            .Concat(restored.Select(PostoDi)));

        foreach (var o in rientri.Values)
        {
            var c = perId[o.ClauseId];
            c.VariantGroup = o.VariantGroup;
            c.VariantDepth = o.VariantDepth;
            c.IsGroupWide = o.IsGroupWide;
        }
        _db.AgreementClauses.AddRange(restored);
        await _db.SaveChangesAsync(ct);
        return restored.Count;
    }

    private static AgreementSection SectionFrom(AgreementSectionSnapshot s)
    {
        var section = new AgreementSection
        {
            Kind = s.Data.Kind,
            Direction = s.Data.Direction,
            Description = NullIfBlank(s.Data.Description),
            Order = s.Order,
        };
        var order = 0;
        foreach (var apt in s.Data.Airports)
            section.Airports.Add(new AgreementAirport
            {
                Icao = apt.Icao.Trim().ToUpperInvariant(),
                Name = NullIfBlank(apt.Name),
                Order = ++order,
            });
        foreach (var c in s.Clauses.OrderBy(x => x.Order))
            section.Clauses.Add(ClauseFrom(0, c));
        return section;
    }

    /// <summary>Una clausola dalla sua fotografia: qui la posizione (ordine, gruppo, profondità) <b>viene dal
    /// dato</b>, ed è la differenza con <see cref="AddClauseAsync"/> — lì la decide il repository perché si sta
    /// scrivendo, qui si sta rimettendo.</summary>
    private static AgreementClause ClauseFrom(int sectionId, AgreementClauseSnapshot s)
    {
        var c = new AgreementClause
        {
            Order = s.Order,
            VariantGroup = s.VariantGroup,
            VariantDepth = s.VariantDepth,
        };
        if (sectionId > 0) c.SectionId = sectionId;
        ApplyClause(c, s.Data);
        c.IsGroupWide = s.Data.IsGroupWide;
        return c;
    }

    /// <summary>
    /// Gli invarianti dell'outline di un ripristino, sezione per sezione, <b>prima</b> di scrivere. Una fotografia
    /// può essere vecchia di un archivio che nel frattempo è cambiato — la clausola di cui era eccezione può non
    /// esserci più — e non deve poter rientrare rotta: un'eccezione orfana descrive la clausola sbagliata, senza
    /// nessun errore a dirlo.
    /// </summary>
    private static void ControllaOutline(IEnumerable<AgreementClause> clausole) =>
        ControllaOutline(clausole.Select(PostoDi));

    private static void ControllaOutline(IEnumerable<PostoInOutline> posti)
    {
        foreach (var perSection in posti.OrderBy(x => x.Order).GroupBy(x => x.SectionId))
        {
            var depthByGroup = new Dictionary<int, int>();
            foreach (var r in perSection)
            {
                if (r.VariantGroup is not int g) continue;

                if (r.IsGroupWide && r.VariantDepth > 0)
                    throw new ValidationException(Lingua("Una clausola «in ogni caso» non può essere l'eccezione di un'altra.", "An «in any case» clause cannot be the exception to another one."));

                var previous = depthByGroup.TryGetValue(g, out var d) ? d : -1;
                if (r.VariantDepth > previous + 1)
                    throw new ValidationException(Lingua(
                        $"La clausola «{r.Cops}» sta a profondità {r.VariantDepth} senza una clausola di " +
                        $"profondità {r.VariantDepth - 1} che la preceda.",
                        $"Clause «{r.Cops}» sits at depth {r.VariantDepth} with no clause at depth " +
                        $"{r.VariantDepth - 1} before it."));

                depthByGroup[g] = r.VariantDepth;
            }
        }
    }

    /// <summary>Ciò che il controllo dell'outline guarda di una clausola. La sezione di una clausola nuova dentro
    /// una sezione nuova è 0 per tutte: stanno comunque insieme, ed è giusto così.</summary>
    private sealed record PostoInOutline(int SectionId, int Order, int? VariantGroup, int VariantDepth, bool IsGroupWide, string Cops);

    private static PostoInOutline PostoDi(AgreementClause c) =>
        new(c.SectionId, c.Order, c.VariantGroup, c.VariantDepth, c.IsGroupWide, c.Cops);

    // ---- spostare fra accordi -----------------------------------------------------------------------
    // Un accordo è UNA coppia di enti: una clausola scritta sotto la coppia sbagliata (ES2 ⇄ Padova quando a quella
    // quota il settore è ES5) fino al 4 ottobre 2026 si poteva solo riscrivere. Qui si dice a chi appartiene
    // davvero — «chi cede → chi riceve» — e sezione o clausole vanno nell'accordo di QUELLA coppia, che nasce se
    // non c'è. Carta docs/feature/2026-10-04-copertura-unica.md §8.

    public async Task<AgreementMoveResult> MoveSectionAsync(string accCode, int sectionId, int senderSectorId,
        int receiverSectorId, CancellationToken ct = default)
    {
        var section = await SectionsOf(accCode).Include(s => s.Agreement)
                          .FirstOrDefaultAsync(s => s.Id == sectionId, ct)
                      ?? throw new InvalidOperationException(Lingua($"Sezione {sectionId} non riguarda la ACC {accCode}.", $"Section {sectionId} does not belong to ACC {accCode}."));

        // Una sezione condivisa non si sposta: «spostare» una presenza sola si legge in due modi (portare via la
        // sezione a tutti, o solo a questo accordo) e nessuno dei due è ovvio. Prima si stacca, o si toglie.
        if (await _db.AgreementSectionShares.AnyAsync(h => h.SectionId == section.Id, ct))
            throw new ValidationException(Lingua(
                "La sezione è condivisa con un altro accordo: staccala, o toglila da uno dei due, prima di spostarla.",
                "The section is shared with another agreement: detach it, or remove it from one of the two, before moving it."));

        var prima = new AgreementSectionPlacement(section.Id, section.AgreementId, section.Direction, section.Order);
        var (arrivo, creato) = await AccordoDellaCoppiaAsync(accCode, senderSectorId, receiverSectorId, ct);
        var verso = VersoDi(arrivo, senderSectorId);

        var clausole = await Scope(section.Id).OrderBy(c => c.Order).ToListAsync(ct);
        var posti = clausole.Select(c => new AgreementClausePlacement(c.Id, c.SectionId, c.Order, c.VariantGroup)).ToList();
        var disfa = new AgreementMoveUndo(prima, posti, Array.Empty<int>(), creato ? arrivo.Id : null);

        // Stessa coppia e stesso verso: non c'è niente da spostare. Stessa coppia e verso opposto: è «gira il
        // verso», e si fa — è quel che è stato chiesto.
        if (arrivo.Id == section.AgreementId)
        {
            if (verso == section.Direction) return new AgreementMoveResult(arrivo.Id, null, 0, false, disfa);
            section.Direction = verso;
            await _db.SaveChangesAsync(ct);
            return new AgreementMoveResult(arrivo.Id, section.Id, 0, false, disfa);
        }

        // ⚠️ I gruppi di varianti sono progressivi PER ACCORDO: portati tali e quali si fonderebbero con quelli
        // dell'accordo di arrivo che hanno lo stesso numero, e due tabelle diverse diventerebbero varianti l'una
        // dell'altra senza un errore. Stessa rinumerazione di MergeSectionsAsync.
        var prossimo = await ClausesOfAgreement(arrivo.Id).MaxAsync(c => (int?)c.VariantGroup, ct) ?? 0;
        Rinumera(clausole, ref prossimo);

        section.Order = (await _db.AgreementSections.Where(s => s.AgreementId == arrivo.Id)
            .MaxAsync(s => (int?)s.Order, ct) ?? 0) + 1;
        section.AgreementId = arrivo.Id;
        section.Direction = verso;
        await _db.SaveChangesAsync(ct);

        return new AgreementMoveResult(arrivo.Id, section.Id, clausole.Count, creato, disfa);
    }

    public async Task<AgreementMoveResult> MoveClausesAsync(string accCode, IReadOnlyList<int> clauseIds,
        int senderSectorId, int receiverSectorId, CancellationToken ct = default)
    {
        var scelte = await ClausesInAccAsync(accCode, clauseIds, ct);
        var (arrivo, creato) = await AccordoDellaCoppiaAsync(accCode, senderSectorId, receiverSectorId, ct);
        var verso = VersoDi(arrivo, senderSectorId);

        var posti = new List<AgreementClausePlacement>();
        var sezioniCreate = new List<int>();
        int? sezioneDiArrivo = null;
        var spostate = 0;
        var prossimo = await ClausesOfAgreement(arrivo.Id).MaxAsync(c => (int?)c.VariantGroup, ct) ?? 0;

        foreach (var daSezione in scelte.GroupBy(c => c.SectionId))
        {
            var origine = await _db.AgreementSections.Include(s => s.Airports).Include(s => s.Shares)
                .FirstAsync(s => s.Id == daSezione.Key, ct);
            // Già al suo posto: la sezione compare nell'accordo di quella coppia — di casa o da ospite — in quel verso.
            var versoLi = origine.AgreementId == arrivo.Id
                ? origine.Direction
                : origine.Shares.FirstOrDefault(h => h.AgreementId == arrivo.Id)?.Direction;
            if (versoLi == verso) continue;

            // ⚠️ Un gruppo di varianti si sposta INTERO: sono righe che dicono la stessa cosa a condizioni diverse,
            // e portarne via una lascerebbe di qua un'alternativa senza le sue sorelle e di là una riga che non è
            // più l'eccezione di nessuno.
            var righe = await Scope(origine.Id).OrderBy(c => c.Order).ToListAsync(ct);
            var gruppi = daSezione.Where(c => c.VariantGroup is not null).Select(c => c.VariantGroup).ToHashSet();
            var singole = daSezione.Where(c => c.VariantGroup is null).Select(c => c.Id).ToHashSet();
            var blocco = righe.Where(c => singole.Contains(c.Id) || (c.VariantGroup is not null && gruppi.Contains(c.VariantGroup))).ToList();
            if (blocco.Count == 0) continue;

            // La sezione di arrivo è quella che dice la STESSA cosa — stesso traffico, stessi scali — nell'accordo
            // e nel verso nuovi. Se non c'è nasce, vuota di prosa: la descrizione era dell'altra tabella.
            var chiave = AirportKey(origine.Airports.Select(a => a.Icao));
            var candidate = await _db.AgreementSections.Include(s => s.Airports)
                .Where(s => s.AgreementId == arrivo.Id && s.Kind == origine.Kind && s.Direction == verso)
                .OrderBy(s => s.Order).ToListAsync(ct);
            var destinazione = candidate.FirstOrDefault(s => AirportKey(s.Airports.Select(a => a.Icao)) == chiave);
            if (destinazione is null)
            {
                destinazione = new AgreementSection
                {
                    AgreementId = arrivo.Id, Kind = origine.Kind, Direction = verso,
                    Order = (await _db.AgreementSections.Where(s => s.AgreementId == arrivo.Id)
                        .MaxAsync(s => (int?)s.Order, ct) ?? 0) + 1,
                    Airports = origine.Airports.OrderBy(a => a.Order)
                        .Select(a => new AgreementAirport { Icao = a.Icao, Name = a.Name, Order = a.Order }).ToList(),
                };
                _db.AgreementSections.Add(destinazione);
                await _db.SaveChangesAsync(ct);
                sezioniCreate.Add(destinazione.Id);
            }

            posti.AddRange(blocco.Select(c => new AgreementClausePlacement(c.Id, c.SectionId, c.Order, c.VariantGroup)));
            Rinumera(blocco, ref prossimo);
            var ordine = await Scope(destinazione.Id).MaxAsync(c => (int?)c.Order, ct) ?? 0;
            foreach (var c in blocco)
            {
                c.SectionId = destinazione.Id;
                c.Order = ++ordine;
            }
            sezioneDiArrivo ??= destinazione.Id;
            spostate += blocco.Count;
            // Si salva sezione per sezione: la prossima legge l'ordine e i gruppi che questa ha appena scritto.
            await _db.SaveChangesAsync(ct);
        }

        return new AgreementMoveResult(arrivo.Id, sezioneDiArrivo, spostate, creato,
            new AgreementMoveUndo(null, posti, sezioniCreate, creato ? arrivo.Id : null));
    }

    public async Task UndoMoveAsync(string accCode, AgreementMoveUndo undo, CancellationToken ct = default)
    {
        if (undo.Section is { } s
            && await SectionsOf(accCode).FirstOrDefaultAsync(x => x.Id == s.SectionId, ct) is { } sezione)
        {
            sezione.AgreementId = s.AgreementId;
            sezione.Direction = s.Direction;
            sezione.Order = s.Order;
        }

        var ids = undo.Clauses.Select(p => p.ClauseId).ToList();
        var clausole = (await ClausesInAccAsync(accCode, ids, ct)).ToDictionary(c => c.Id);
        foreach (var p in undo.Clauses)
            if (clausole.TryGetValue(p.ClauseId, out var c))
            {
                c.SectionId = p.SectionId;
                c.Order = p.Order;
                c.VariantGroup = p.VariantGroup;
            }
        await _db.SaveChangesAsync(ct);

        // Quel che era nato per fare posto se ne va, ma SOLO se è rimasto vuoto: nel frattempo qualcuno può
        // averci scritto, e annullare uno spostamento non autorizza a cancellare il lavoro di un altro.
        foreach (var id in undo.CreatedSectionIds)
            if (await _db.AgreementSections.FirstOrDefaultAsync(x => x.Id == id, ct) is { } nata
                && !await _db.AgreementClauses.AnyAsync(c => c.SectionId == id, ct))
                _db.AgreementSections.Remove(nata);
        await _db.SaveChangesAsync(ct);

        if (undo.CreatedAgreementId is int accordo
            && await _db.CoordinationAgreements.FirstOrDefaultAsync(x => x.Id == accordo, ct) is { } nato
            && !await _db.AgreementSections.AnyAsync(x => x.AgreementId == accordo, ct))
        {
            _db.CoordinationAgreements.Remove(nato);
            await _db.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// L'accordo della coppia «chi cede, chi riceve»: quello che c'è, o uno nuovo della ACC.
    /// <para>⚠️ La coppia è unica in tutto l'archivio, non per ACC: se l'accordo esiste ma non riguarda questa ACC
    /// (nessuno dei due enti è suo, e non ne è responsabile) non ci si scrive dentro da qui.</para>
    /// </summary>
    private async Task<(CoordinationAgreement Accordo, bool Creato)> AccordoDellaCoppiaAsync(string accCode,
        int senderSectorId, int receiverSectorId, CancellationToken ct)
    {
        if (senderSectorId <= 0 || receiverSectorId <= 0)
            throw new ValidationException(Lingua("Indica chi cede e chi riceve.", "Say who hands over and who receives."));
        if (senderSectorId == receiverSectorId)
            throw new ValidationException(Lingua("Chi cede e chi riceve non possono essere lo stesso ente.", "The sender and the receiver cannot be the same unit."));
        if (await _db.Sectors.CountAsync(x => x.Id == senderSectorId || x.Id == receiverSectorId, ct) != 2)
            throw new ValidationException(Lingua("Uno dei due enti non esiste più.", "One of the two units no longer exists."));

        var (a, b) = Canonical(senderSectorId, receiverSectorId);
        var esistente = await _db.CoordinationAgreements.FirstOrDefaultAsync(x => x.SideASectorId == a && x.SideBSectorId == b, ct);
        if (esistente is not null)
        {
            if (!await AgreementsOf(accCode).AnyAsync(x => x.Id == esistente.Id, ct))
                throw new ValidationException(Lingua(
                    $"Fra questi due enti esiste già un accordo, ma non riguarda la ACC {accCode}.",
                    $"These two units already have an agreement, but it does not belong to ACC {accCode}."));
            return (esistente, false);
        }

        var accId = await AccIdAsync(accCode, ct);
        var nuovo = new CoordinationAgreement
        {
            OwnerAccId = accId, SideASectorId = a, SideBSectorId = b,
            Order = (await _db.CoordinationAgreements.Where(x => x.OwnerAccId == accId).MaxAsync(x => (int?)x.Order, ct) ?? 0) + 1,
        };
        _db.CoordinationAgreements.Add(nuovo);
        await _db.SaveChangesAsync(ct);
        return (nuovo, true);
    }

    /// <summary>Il verso in cui, in quell'accordo, cede quell'ente: i lati sono canonici, il verso no.</summary>
    private static AgreementDirection VersoDi(CoordinationAgreement accordo, int senderSectorId) =>
        accordo.SideASectorId == senderSectorId ? AgreementDirection.AtoB : AgreementDirection.BtoA;

    /// <summary>Dà numeri nuovi ai gruppi di varianti delle clausole che cambiano accordo, uno per gruppo.</summary>
    private static void Rinumera(IEnumerable<AgreementClause> clausole, ref int prossimo)
    {
        var mappa = new Dictionary<int, int>();
        foreach (var c in clausole)
            if (c.VariantGroup is int g)
            {
                if (!mappa.TryGetValue(g, out var nuovo)) mappa[g] = nuovo = ++prossimo;
                c.VariantGroup = nuovo;
            }
    }

    // ---- attrezzi dell'outline ----------------------------------------------------------------------

    /// <summary>Le clausole di una <b>sezione</b>: è lo scopo dentro cui l'ordine ha significato.</summary>
    private IQueryable<AgreementClause> Scope(int sectionId) =>
        _db.AgreementClauses.Where(x => x.SectionId == sectionId);

    /// <summary>Le clausole di tutto l'accordo: i gruppi di varianti sono progressivi per accordo, e per
    /// numerarne uno nuovo bisogna vederli tutti.</summary>
    private IQueryable<AgreementClause> ClausesOfAgreement(int agreementId) =>
        _db.AgreementClauses.Where(x => x.Section!.Agreement!.Id == agreementId);

    private Task<int> AgreementIdOfAsync(int sectionId, CancellationToken ct) =>
        _db.AgreementSections.Where(s => s.Id == sectionId).Select(s => s.AgreementId).FirstAsync(ct);

    /// <summary>Il prossimo numero di gruppo libero nell'accordo che ospita la sezione.</summary>
    private async Task<int> NextGroupAsync(int sectionId, CancellationToken ct) =>
        (await ClausesOfAgreement(await AgreementIdOfAsync(sectionId, ct))
            .MaxAsync(x => (int?)x.VariantGroup, ct) ?? 0) + 1;

    private Task<List<AgreementClause>> ScopeRowsAsync(AgreementClause c, CancellationToken ct) =>
        Scope(c.SectionId).OrderBy(x => x.Order).ToListAsync(ct);

    private Task<List<AgreementClause>> GroupRowsAsync(AgreementClause c, int group, CancellationToken ct) =>
        Scope(c.SectionId).Where(x => x.VariantGroup == group).OrderBy(x => x.Order).ToListAsync(ct);

    /// <summary>
    /// La clausola più tutto ciò che le appartiene: quelle che la seguono finché restano nel suo gruppo e più
    /// profonde di lei. È la definizione di sottoalbero in un outline, e serve ovunque una clausola si muova o
    /// si stacchi — perché muovere una capofila senza le sue eccezioni le riassegna a un'altra alternativa
    /// <b>senza un errore</b>: nessuna eccezione, nessun log, solo un accordo che dice un'altra cosa.
    /// </summary>
    private static List<AgreementClause> Subtree(List<AgreementClause> rowsInOrder, AgreementClause root)
    {
        var i = rowsInOrder.FindIndex(x => x.Id == root.Id);
        if (i < 0 || root.VariantGroup is null) return new List<AgreementClause> { root };
        var block = new List<AgreementClause> { rowsInOrder[i] };
        for (var k = i + 1; k < rowsInOrder.Count
                            && rowsInOrder[k].VariantGroup == root.VariantGroup
                            && rowsInOrder[k].VariantDepth > root.VariantDepth; k++)
            block.Add(rowsInOrder[k]);
        return block;
    }

    /// <summary>Risale dalla clausola in posizione <paramref name="index"/> alla radice del suo blocco: serve a
    /// scavalcare all'insù un vicino che è a sua volta un sottoalbero, e non finirgli in mezzo.</summary>
    private static AgreementClause RootOf(List<AgreementClause> rowsInOrder, int index)
    {
        var r = rowsInOrder[index];
        if (r.VariantGroup is null || r.VariantDepth == 0) return r;
        for (var k = index - 1; k >= 0; k--)
            if (rowsInOrder[k].VariantGroup == r.VariantGroup && rowsInOrder[k].VariantDepth < r.VariantDepth)
                return rowsInOrder[k];
        return r;
    }

    private static void Renumber(List<AgreementClause> rowsInOrder)
    {
        for (var i = 0; i < rowsInOrder.Count; i++) rowsInOrder[i].Order = i + 1;
    }

    /// <summary>Scioglie un gruppo rimasto con una sola clausola: un gruppo di uno non è un gruppo. Non salva —
    /// il chiamante è già dentro la sua <c>SaveChangesAsync</c>.</summary>
    private async Task DissolveIfAloneAsync(int sectionId, int group, CancellationToken ct)
    {
        var candidati = await Scope(sectionId).Where(x => x.VariantGroup == group).ToListAsync(ct);

        // ⚠️ La query filtra su ciò che sta NEL DATABASE, ma qui siamo prima della SaveChanges: la clausola
        // appena sfilata (gruppo a null) o appena rimossa torna comunque indietro dal SELECT. Va riletto lo
        // stato in memoria, che è quello che sta per essere scritto — altrimenti il gruppo sembra ancora
        // affollato e non si scioglie mai.
        var remaining = candidati
            .Where(x => x.VariantGroup == group && _db.Entry(x).State != EntityState.Deleted)
            .ToList();
        if (remaining.Count > 1) return;
        foreach (var x in remaining) { x.VariantGroup = null; x.VariantDepth = 0; x.IsGroupWide = false; }
    }

    /// <summary>Copia editoriale di una clausola: i campi, non l'identità né la posizione.</summary>
    private static AgreementClause CopyOf(AgreementClause src) => new()
    {
        SectionId = src.SectionId,
        Cops = src.Cops,
        LevelValue = src.LevelValue,
        LevelUnit = src.LevelUnit,
        LevelConstraint = src.LevelConstraint,
        LevelSpecial = src.LevelSpecial,
        Parity = src.Parity,
        VerticalState = src.VerticalState,
        ConditionLabel = src.ConditionLabel,
        ConditionRefId = src.ConditionRefId,
        ConditionAreaLabel = src.ConditionAreaLabel,
        ConditionAreaNegated = src.ConditionAreaNegated,
        ConditionAreaAll = src.ConditionAreaAll,
        ConditionCustomLabel = src.ConditionCustomLabel,
        HandoffKind = src.HandoffKind,
        HandoffLabel = src.HandoffLabel,
        HandoffLevelValue = src.HandoffLevelValue,
        HandoffLevelUnit = src.HandoffLevelUnit,
        HandoffLevelConstraint = src.HandoffLevelConstraint,
        CommsHandoffKind = src.CommsHandoffKind,
        CommsHandoffLabel = src.CommsHandoffLabel,
        SpeedValue = src.SpeedValue,
        SpeedConstraint = src.SpeedConstraint,
    };

    // ---- guardie e conversioni ----------------------------------------------------------------------

    /// <summary>
    /// Gli accordi che <b>riguardano</b> la ACC: ne è responsabile, o ha un capo fra i suoi settori. La stessa
    /// regola vale in lettura e in scrittura — chi vede un accordo può scriverlo, se ha il permesso sulla
    /// propria ACC.
    /// <para>È scritta come query e non come metodo su un'entità: un predicato C# dentro un <c>Where</c> EF non
    /// si traduce in SQL, e il difetto non si vede compilando — si vede al primo salvataggio.</para>
    /// </summary>
    private IQueryable<CoordinationAgreement> AgreementsOf(string accCode) =>
        _db.CoordinationAgreements.Where(a => a.OwnerAcc!.Code == accCode
                                              || a.SideASector!.Acc!.Code == accCode
                                              || a.SideBSector!.Acc!.Code == accCode);

    /// <summary>Le sezioni che riguardano la ACC: quelle di casa in un suo accordo e quelle che un suo accordo
    /// <b>ospita</b>. Chi le vede le può scrivere — una sezione condivisa è di tutti gli accordi che la portano.</summary>
    private IQueryable<AgreementSection> SectionsOf(string accCode) =>
        _db.AgreementSections.Where(s => s.Agreement!.OwnerAcc!.Code == accCode
                                         || s.Agreement!.SideASector!.Acc!.Code == accCode
                                         || s.Agreement!.SideBSector!.Acc!.Code == accCode
                                         || s.Shares.Any(h => h.Agreement!.OwnerAcc!.Code == accCode
                                                              || h.Agreement!.SideASector!.Acc!.Code == accCode
                                                              || h.Agreement!.SideBSector!.Acc!.Code == accCode));

    /// <summary>Le clausole degli accordi che riguardano la ACC. Stessa regola di <see cref="SectionsOf"/>,
    /// una relazione più in là.</summary>
    private IQueryable<AgreementClause> ClausesOf(string accCode) =>
        _db.AgreementClauses.Where(x => x.Section!.Agreement!.OwnerAcc!.Code == accCode
                                        || x.Section!.Agreement!.SideASector!.Acc!.Code == accCode
                                        || x.Section!.Agreement!.SideBSector!.Acc!.Code == accCode
                                        || x.Section!.Shares.Any(h => h.Agreement!.OwnerAcc!.Code == accCode
                                                                      || h.Agreement!.SideASector!.Acc!.Code == accCode
                                                                      || h.Agreement!.SideBSector!.Acc!.Code == accCode));

    private async Task<int> AccIdAsync(string accCode, CancellationToken ct) =>
        await _db.Accs.Where(a => a.Code == accCode).Select(a => (int?)a.Id).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"ACC {accCode} inesistente.");

    private async Task<CoordinationAgreement> AgreementAsync(string accCode, int agreementId, CancellationToken ct) =>
        await AgreementsOf(accCode).FirstOrDefaultAsync(x => x.Id == agreementId, ct)
            ?? throw new InvalidOperationException(Lingua($"Accordo {agreementId} non riguarda la ACC {accCode}.", $"Agreement {agreementId} does not belong to ACC {accCode}."));

    private async Task<AgreementClause> ClauseInAccAsync(string accCode, int clauseId, CancellationToken ct) =>
        await ClausesOf(accCode).FirstOrDefaultAsync(x => x.Id == clauseId, ct)
            ?? throw new InvalidOperationException(Lingua($"Clausola {clauseId} non riguarda la ACC {accCode}.", $"Clause {clauseId} does not belong to ACC {accCode}."));

    /// <summary>Le clausole indicate che riguardano davvero la ACC: il filtro è la guardia, non un dettaglio.</summary>
    private async Task<List<AgreementClause>> ClausesInAccAsync(string accCode, IReadOnlyList<int> clauseIds, CancellationToken ct) =>
        clauseIds.Count == 0
            ? new List<AgreementClause>()
            : await ClausesOf(accCode).Where(x => clauseIds.Contains(x.Id)).ToListAsync(ct);

    /// <summary>I due lati in forma canonica: id minore = A. È la chiave dell'unicità, non una scelta
    /// editoriale — e non ha significato perché il verso sta sulla sezione.</summary>
    private static (int A, int B) Canonical(int x, int y) => x <= y ? (x, y) : (y, x);

    private static AgreementDirection Flip(AgreementDirection d) =>
        d == AgreementDirection.AtoB ? AgreementDirection.BtoA : AgreementDirection.AtoB;

    /// <summary>La chiave con cui due sezioni «hanno gli stessi scali»: normalizzata e ordinata, perché
    /// «LIBD·LIBR» e «LIBR·LIBD» sono lo stesso gruppo.</summary>
    private static string AirportKey(IEnumerable<string> icaos) =>
        string.Join("·", icaos.Select(x => x.Trim().ToUpperInvariant()).OrderBy(x => x, StringComparer.Ordinal));

    private static void ApplyClause(AgreementClause c, AgreementClauseInput i)
    {
        // I punti si normalizzano passando dall'elenco: spazi, vuoti e separatori doppi spariscono qui, una
        // volta, invece che in ogni posto che li rilegge.
        c.Cops = CopList.Format(CopList.Parse(i.Cops));
        c.LevelValue = i.LevelConstraint == LevelConstraint.Special ? null : i.LevelValue;
        c.LevelUnit = i.LevelUnit;
        c.LevelConstraint = i.LevelConstraint;
        c.LevelSpecial = i.LevelConstraint == LevelConstraint.Special ? NullIfBlank(i.LevelSpecial) : null;
        c.Parity = i.Parity;
        c.VerticalState = i.VerticalState;

        c.ConditionLabel = NullIfBlank(i.ConditionLabel);
        c.ConditionRefId = c.ConditionLabel is null ? null : i.ConditionRefId;
        c.ConditionAreaLabel = NullIfBlank(i.ConditionAreaLabel);
        c.ConditionAreaNegated = c.ConditionAreaLabel is not null && i.ConditionAreaNegated;
        c.ConditionAreaAll = c.ConditionAreaLabel is not null && i.ConditionAreaAll;
        c.ConditionCustomLabel = NullIfBlank(i.ConditionCustomLabel);

        // Senza tipo non c'è trasferimento distinto: i campi correlati si azzerano, così una clausola tornata a
        // «coincide con l'ingresso» non si porta dietro un livello fantasma.
        c.HandoffKind = i.HandoffKind;
        c.HandoffLabel = i.HandoffKind == TransferHandoffKind.Unspecified ? null : NullIfBlank(i.HandoffLabel);
        c.HandoffLevelValue = i.HandoffKind == TransferHandoffKind.Unspecified ? null : i.HandoffLevelValue;
        c.HandoffLevelUnit = i.HandoffLevelUnit;
        c.HandoffLevelConstraint = i.HandoffLevelConstraint;
        c.CommsHandoffKind = i.CommsHandoffKind;
        c.CommsHandoffLabel = i.CommsHandoffKind == TransferHandoffKind.Unspecified ? null : NullIfBlank(i.CommsHandoffLabel);

        c.SpeedConstraint = i.SpeedConstraint;
        c.SpeedValue = i.SpeedConstraint == SpeedConstraint.Unspecified ? null : i.SpeedValue;

        // «Scavalca le alternative» ha senso solo dentro un gruppo: fuori non ci sono alternative da scavalcare.
        c.IsGroupWide = i.IsGroupWide && c.VariantGroup is not null;
    }

    private static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>L'accordo com'è letto: le sezioni di casa e quelle che ospita, ognuna col verso e il posto della
    /// SUA presenza qui, e con l'elenco degli altri accordi in cui compare.</summary>
    private static AgreementRow Map(CoordinationAgreement a, IEnumerable<AgreementSectionShare> ospiti,
        IReadOnlyDictionary<int, IReadOnlyList<AgreementShareRef>> presenze)
    {
        IReadOnlyList<AgreementShareRef> Altri(int sectionId) => presenze.TryGetValue(sectionId, out var tutte)
            ? tutte.Where(x => x.AgreementId != a.Id).ToList()
            : Array.Empty<AgreementShareRef>();

        return new AgreementRow
        {
            Id = a.Id,
            OwnerAccCode = a.OwnerAcc?.Code ?? "",
            SideA = new AgreementEndpoint(a.SideASectorId, a.SideASector?.Callsign ?? $"#{a.SideASectorId}"),
            SideB = new AgreementEndpoint(a.SideBSectorId, a.SideBSector?.Callsign ?? $"#{a.SideBSectorId}"),
            Note = a.Note,
            Order = a.Order,
            Sections = AgreementSectionOrder.Sort(
                a.Sections.Select(s => MapSection(s, s.Direction, s.Order, Altri(s.Id)))
                    .Concat(ospiti.Where(h => h.Section is not null)
                        .Select(h => MapSection(h.Section!, h.Direction, h.Order, Altri(h.SectionId))))),
        };
    }

    private static AgreementSectionRow MapSection(AgreementSection s, AgreementDirection direction, int order,
        IReadOnlyList<AgreementShareRef> sharedWith) => new()
    {
        Id = s.Id,
        Kind = s.Kind,
        Direction = direction,
        Description = s.Description,
        Order = order,
        Airports = s.Airports.OrderBy(x => x.Order).Select(x => new AgreementAirportRow(x.Icao, x.Name, x.Order)).ToList(),
        Clauses = s.Clauses.OrderBy(c => c.Order).ThenBy(c => c.Id).Select(MapClause).ToList(),
        SharedWith = sharedWith,
    };

    private static AgreementClauseRow MapClause(AgreementClause c) => new()
    {
        Id = c.Id,
        SectionId = c.SectionId,
        Cops = c.Cops,
        LevelValue = c.LevelValue,
        LevelUnit = c.LevelUnit,
        LevelConstraint = c.LevelConstraint,
        LevelSpecial = c.LevelSpecial,
        Parity = c.Parity,
        VerticalState = c.VerticalState,
        ConditionLabel = c.ConditionLabel,
        ConditionRefId = c.ConditionRefId,
        ConditionAreaLabel = c.ConditionAreaLabel,
        ConditionAreaNegated = c.ConditionAreaNegated,
        ConditionAreaAll = c.ConditionAreaAll,
        ConditionCustomLabel = c.ConditionCustomLabel,
        HandoffKind = c.HandoffKind,
        HandoffLabel = c.HandoffLabel,
        HandoffLevelValue = c.HandoffLevelValue,
        HandoffLevelUnit = c.HandoffLevelUnit,
        HandoffLevelConstraint = c.HandoffLevelConstraint,
        CommsHandoffKind = c.CommsHandoffKind,
        CommsHandoffLabel = c.CommsHandoffLabel,
        SpeedValue = c.SpeedValue,
        SpeedConstraint = c.SpeedConstraint,
        VariantGroup = c.VariantGroup,
        VariantDepth = c.VariantDepth,
        IsGroupWide = c.IsGroupWide,
        Order = c.Order,
    };
}

using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;

namespace Vipi.Infrastructure.Persistence;

/// <inheritdoc cref="ISectorProjectionService"/>
public sealed class EfSectorProjectionService : ISectorProjectionService
{
    private readonly VipiDbContext _db;
    private readonly IDocumentImpactService? _impacts;

    /// <param name="impacts">
    /// Dove finiscono le segnalazioni quando un settore sparisce, viene nascosto o cambia padre. È
    /// <b>opzionale</b> di proposito: la proiezione deve restare usabile da sola (i test di proiezione non
    /// hanno niente a che vedere con la casella), e un giro senza casella è un giro che proietta e basta,
    /// non un giro che fallisce.
    /// </param>
    public EfSectorProjectionService(VipiDbContext db, IDocumentImpactService? impacts = null)
    {
        _db = db;
        _impacts = impacts;
    }

    /// <summary>
    /// Quota di settori proiettati che possono sparire in un giro <b>senza</b> che la cosa venga presa per
    /// buona. Oltre questa, il catalogo è sospetto — un import a metà, un database appena sostituito, un ACC
    /// nascosto in blocco — e aprire una segnalazione per ognuno vorrebbe dire seppellire la casella la
    /// prima volta che qualcosa va storto a monte.
    /// </summary>
    private const double QuotaSparizioniSospetta = 0.25;

    /// <summary>Sotto questo numero di sparizioni la quota non si applica: su tre settori in archivio, uno
    /// solo che se ne va supera il 25% ed è un fatto del tutto normale.</summary>
    private const int SparizioniMinimePerLaQuota = 5;

    public async Task<int> SyncFromCatalogsAsync(CancellationToken ct = default)
    {
        // Mappe di risoluzione: codice ACC → Id, ICAO aeroporto → Id.
        var accIdByCode = await _db.Accs.ToDictionaryAsync(a => a.Code, a => a.Id, StringComparer.OrdinalIgnoreCase, ct);
        var airportIdByIcao = await _db.Airports.ToDictionaryAsync(a => a.Icao, a => a.Id, StringComparer.OrdinalIgnoreCase, ct);

        // ACC nascosti → i loro settori sono effettivamente nascosti.
        var hiddenAccCodes = await _db.Accs.Where(a => a.IsHidden).Select(a => a.Code)
            .ToListAsync(ct);
        var hiddenAcc = hiddenAccCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 1. Insieme desiderato dai cataloghi (callsign → attributi proiettati). ATIS escluso (non è un settore).
        var desired = new Dictionary<string, Desired>(StringComparer.OrdinalIgnoreCase);

        var accSectors = await _db.AccSectors.AsNoTracking().ToListAsync(ct);
        foreach (var s in accSectors)
        {
            if (s.IsHidden || hiddenAcc.Contains(s.CenterId)) continue;
            if (IsAtis(s.Position)) continue;
            if (!accIdByCode.TryGetValue(s.CenterId, out var accId)) continue;
            desired[s.ComposePosition] = new Desired(
                Callsign: s.ComposePosition, AccId: accId, Type: MapType(s.Position),
                Kind: SectorKind.Acc, Frequency: s.Frequency, AirportId: null, AirportIcao: null,
                ParentCallsign: s.ParentCallsign, IsAccApp: true,   // APP da un subcenter ACC è per natura "di ACC"
                AtcCallsign: s.AtcCallsign, Position: s.Position, ImportedAtUtc: s.ImportedAtUtc);
        }

        // Padre impostato sul nodo AEROPORTO in /services/vsop/admin/sector-structure (`Airport.ParentCallsign`): è il
        // legame che l'admin vede e compila, e vale per TUTTE le posizioni di quell'aeroporto.
        var airportParentByIcao = await _db.Airports
            .Where(a => a.ParentCallsign != null)
            .ToDictionaryAsync(a => a.Icao, a => a.ParentCallsign!, StringComparer.OrdinalIgnoreCase, ct);

        var airportSectors = await _db.AirportSectors.AsNoTracking().ToListAsync(ct);

        // Posizioni visibili per aeroporto, per la scaletta interna (sotto).
        var visibleByIcao = airportSectors
            .Where(s => !s.IsHidden && !hiddenAcc.Contains(s.AccCode) && !IsAtis(s.Position))
            .GroupBy(s => s.AirportIcao, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var s in airportSectors)
        {
            if (s.IsHidden || hiddenAcc.Contains(s.AccCode)) continue;
            if (IsAtis(s.Position)) continue;
            if (!accIdByCode.TryGetValue(s.AccCode, out var accId)) continue;
            airportIdByIcao.TryGetValue(s.AirportIcao, out var airportId);
            desired[s.ComposePosition] = new Desired(
                Callsign: s.ComposePosition, AccId: accId, Type: MapType(s.Position),
                Kind: SectorKind.Airport, Frequency: s.Frequency,
                AirportId: airportId == 0 ? null : airportId, AirportIcao: s.AirportIcao,
                ParentCallsign: s.ParentCallsign ?? LadderParent(s, visibleByIcao, airportParentByIcao),
                IsAccApp: s.IsAccApp,
                AtcCallsign: s.AtcCallsign, Position: s.Position, ImportedAtUtc: s.ImportedAtUtc);
        }

        // 2. Settori già presenti che ci interessano: tutti i proiettati + quelli col callsign desiderato (per adottarli).
        var desiredKeys = desired.Keys.ToList();
        var existing = await _db.Sectors
            .Where(s => s.IsProjected || desiredKeys.Contains(s.Callsign))
            .ToListAsync(ct);
        var byCallsign = existing.ToDictionary(s => s.Callsign, s => s, StringComparer.OrdinalIgnoreCase);

        // Contesto per le segnalazioni (§6). Il catalogo INTERO, nascosti compresi: serve a distinguere «il
        // callsign non c'è più» da «il callsign c'è ma qualcuno l'ha nascosto», che per chi legge sono due
        // fatti diversi — il primo lo decide la sorgente, il secondo una persona.
        var tuttiICallsignInCatalogo = accSectors.Select(x => x.ComposePosition)
            .Concat(airportSectors.Select(x => x.ComposePosition))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var accDiCallsign = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var x in accSectors) accDiCallsign[x.ComposePosition] = x.CenterId;
        foreach (var x in airportSectors) accDiCallsign[x.ComposePosition] = x.AccCode;
        var primaAttivi = existing.Count(s => s.IsProjected && s.IsActive);
        // ⚠️ Il codice ACC per Id, e non solo quello del catalogo: quando un callsign SPARISCE la sua riga di
        // catalogo non c'è più, quindi l'ACC glielo può dire solo il settore proiettato (AccId), che invece
        // resta. Senza questa mappa la segnalazione di sparizione partiva con l'ACC vuoto — e senza ACC il
        // reverse-lookup non trova la vIPI ACC, cioè proprio il documento che deve avvisare.
        var accCodeById = accIdByCode.ToDictionary(kv => kv.Value, kv => kv.Key);

        var changed = 0;
        var tornati = new List<Sector>();

        // 3. Upsert per callsign (preserva Id e i legami editoriali DocumentId/IsPrimary/FeaturedRank).
        foreach (var d in desired.Values)
        {
            var friendly = FriendlyName(d);
            if (!byCallsign.TryGetValue(d.Callsign, out var sector))
            {
                sector = new Sector { Callsign = d.Callsign, Name = friendly };
                _db.Sectors.Add(sector);
                byCallsign[d.Callsign] = sector;
            }
            sector.AccId = d.AccId;
            sector.Type = d.Type;
            sector.Kind = d.Kind;
            sector.ApproachKind = d.Type == SectorType.App
                ? (d.IsAccApp ? ApproachKind.Remotized : ApproachKind.Standalone)
                : null;
            sector.DefaultFrequency = d.Frequency;
            sector.AirportId = d.AirportId;
            sector.AirportIcao = d.AirportIcao;
            sector.CoverageOrder = CoverageFor(d.Type);
            // Nome amichevole dalla sorgente (AtcCallsign IVAO, fallback "{ICAO} {Tipo}"). Assegnato quando il Name
            // è vuoto o un SEGNAPOSTO (== callsign grezzo, residuo di proiezioni vecchie): riarmonizza senza clobberare
            // un nome realmente personalizzato dall'admin.
            if (string.IsNullOrWhiteSpace(sector.Name)
                || string.Equals(sector.Name, sector.Callsign, StringComparison.OrdinalIgnoreCase))
                sector.Name = friendly;
            sector.IsProjected = true;
            // Tornato: era disattivato e il catalogo lo rimette in circolazione. La segnalazione aperta quando
            // sparì non ha più causa, e sara' questa lista a farla chiudere (§6).
            if (sector.Id != 0 && !sector.IsActive) tornati.Add(sector);
            sector.IsActive = true;

            // ⚠️ IL TIMBRO DELLA RIGA DI CATALOGO, NON L'ORA DI ADESSO. Qui c'era `DateTime.UtcNow`, e
            // faceva due danni insieme.
            //
            // Il primo è il costo: questa proiezione gira a OGNI AVVIO (RunVipiStartupMaintenance), e
            // scrivendo un valore nuovo su ogni riga marcava come modificati TUTTI i settori. Misurato il
            // 27 agosto 2026: 312 UPDATE su 465 query d'avvio, ogni volta, senza che nulla fosse cambiato.
            // Su un database condiviso con il sito che ci ospita è lavoro che paga qualcun altro.
            //
            // Il secondo è il significato, ed è il più serio. Il campo si chiama «importato alle» e la
            // regola D8 delle eliminazioni gli chiede «la sorgente lo manda ancora?». Con `UtcNow` la
            // risposta era «sì, perché abbiamo riavviato»: un settore sparito dalla sorgente a luglio
            // tornava fresco a ogni riavvio. EfDeletionRepository lo sapeva e ci girava intorno — legge il
            // timbro dalle righe di catalogo e usa questo solo come ripiego, con scritto sopra che «dice
            // quando è nato lo specchio, non quando la sorgente ha parlato». Adesso i due coincidono, e
            // quel ripiego smette di essere una mezza verità.
            //
            // Conseguenza voluta: al PRIMO avvio dopo questa modifica le righe si aggiornano una volta
            // sola — dall'ora del riavvio a quella del catalogo — e dai giri successivi tacciono.
            sector.ImportedAtUtc = d.ImportedAtUtc;
            changed++;
        }

        // 4. Padre (contenimento) derivato dal ParentCallsign del catalogo. Se il padre diretto è NASCOSTO
        //    (non è in `desired`), il figlio risale la catena dei ParentCallsign fino al primo antenato VISIBILE
        //    (nonno, bisnonno…). Un solo code-path che copre settore nascosto, ACC nascosto e orfano: si aggancia
        //    solo a callsign confermati in `desired` (tutti upsertati IsActive=true), mai a un settore disattivato.
        var parentOf = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in accSectors) parentOf[s.ComposePosition] = s.ParentCallsign;
        // Stesso padre derivato usato in `desired`: se la mappa usasse il ParentCallsign grezzo, la risalita
        // verso un antenato visibile ripartirebbe da null proprio per le posizioni che il fix aggancia.
        foreach (var s in airportSectors)
            parentOf[s.ComposePosition] = s.ParentCallsign ?? LadderParent(s, visibleByIcao, airportParentByIcao);

        // Il padre di PRIMA, letto adesso che nessuno l'ha ancora toccato: serve a distinguere «riparentato»
        // da «era già così». Solo per i settori che esistevano (i nuovi non hanno un prima).
        var padrePrima = existing.Where(s => s.Id != 0)
            .ToDictionary(s => s.Callsign, s => s.ParentSectorId, StringComparer.OrdinalIgnoreCase);

        foreach (var d in desired.Values)
        {
            var child = byCallsign[d.Callsign];
            var visibleParentCs = NearestVisibleAncestor(d.ParentCallsign, desired, parentOf);
            if (visibleParentCs != null
                && byCallsign.TryGetValue(visibleParentCs, out var parent)
                && !ReferenceEquals(parent, child))
            {
                child.ParentSector = parent;   // EF risolve l'Id alla SaveChanges anche per le nuove righe
            }
            else
            {
                child.ParentSector = null;
                child.ParentSectorId = null;
            }
        }

        // Chi ha cambiato padre davvero. ⚠️ Si guarda DOPO l'assegnazione ma PRIMA del salvataggio, e si
        // confronta con la fotografia di prima: un settore appena creato non è «riparentato», è nato.
        var riparentati = new List<Sector>();
        foreach (var d in desired.Values)
        {
            var child = byCallsign[d.Callsign];
            if (!padrePrima.TryGetValue(d.Callsign, out var prima)) continue;   // nuovo: non è un cambio
            var dopo = child.ParentSector?.Id ?? child.ParentSectorId;
            if (prima != dopo) riparentati.Add(child);
        }

        // 5. Orfani: settori PROIETTATI il cui callsign non è più nel catalogo visibile → disattiva (non cancella).
        //
        //    ⚠️ **Dal 25 agosto 2026 il legame al documento NON si recide più.** Prima si azzeravano anche
        //    DocumentId/IsPrimary/FeaturedRank, per una ragione vera — un settore che non esiste più non deve
        //    restare agganciato a un Document, o in rigenerazione nascono artefatti doppio-documento e
        //    «primari» fantasma. Il prezzo era però più alto del rimedio: la riga tornava al giro successivo,
        //    il legame no, e il documento restava sganciato per sempre — con la pagina pubblica muta, perché
        //    i bersagli di release cercano un settore ATTIVO col documento. Ora il legame resta e la
        //    segnalazione avvisa; a recidere sarà l'admin, dalla sezione «Orfani» della Struttura, quando avrà
        //    deciso. Il motivo originale resta coperto: chi risolve un documento filtra su IsActive
        //    (EfAccDerivationRepository) o parte dall'aeroporto, e la rigenerazione riallinea solo gli attivi.
        var spariti = new List<Sector>();
        foreach (var s in existing)
        {
            if (s.IsProjected && !desired.ContainsKey(s.Callsign) && s.IsActive)
            {
                s.IsActive = false;
                spariti.Add(s);
                changed++;
            }
        }

        await _db.SaveChangesAsync(ct);

        // 6. La casella: che cosa raccontare ai documenti. Dopo il salvataggio, perché una segnalazione su uno
        //    stato non ancora scritto sarebbe una bugia se il salvataggio fallisse.
        await SegnalaAsync(spariti, riparentati, tornati, tuttiICallsignInCatalogo, primaAttivi, accDiCallsign,
            accCodeById, ct);

        // I poligoni/visibilità dei settori appena riproiettati possono aver cambiato i confini esteri: invalida la
        // cache del set confinanti (altrimenti resta stantia fino al TTL di 5 min). Questo è il choke point comune di
        // ogni mutazione catalogo (import ACC/aeroporti, hide, neighbour), quindi basta invalidare qui.
        EfHierarchyEditingService.InvalidateConfiningCache();
        return changed;
    }

    /// <summary>Risale la catena dei <c>ParentCallsign</c> partendo da <paramref name="parentCallsign"/> e ritorna il
    /// primo antenato presente in <paramref name="desired"/> (cioè VISIBILE), saltando gli antenati nascosti; null se la
    /// catena finisce (radice reale) o si esaurisce. Guard anti-ciclo con un set dei callsign già visitati.</summary>
    private static string? NearestVisibleAncestor(
        string? parentCallsign, Dictionary<string, Desired> desired, Dictionary<string, string?> parentOf)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cur = parentCallsign;
        while (!string.IsNullOrWhiteSpace(cur) && seen.Add(cur))
        {
            if (desired.ContainsKey(cur)) return cur;                       // antenato visibile → stop
            cur = parentOf.TryGetValue(cur, out var p) ? p : null;          // nascosto → sali di un livello
        }
        return null;
    }

    /// <param name="ImportedAtUtc">
    /// Il timbro della RIGA DI CATALOGO da cui questo settore è proiettato: quando la sorgente l'ha nominata
    /// l'ultima volta. Viaggia fin qui perché è quello che finisce su <c>Sector.ImportedAtUtc</c> — vedi il
    /// commento all'assegnazione, che spiega perché non è più <c>DateTime.UtcNow</c>.
    /// </param>
    private sealed record Desired(
        string Callsign, int AccId, SectorType Type, SectorKind Kind,
        string? Frequency, int? AirportId, string? AirportIcao, string? ParentCallsign, bool IsAccApp,
        string? AtcCallsign, string? Position, DateTime? ImportedAtUtc);

    /// <summary>Adatta le righe di catalogo al modello puro della scaletta (<see cref="AirportPositionLadder"/>).</summary>
    private static string? LadderParent(
        AirportSector sector,
        IReadOnlyDictionary<string, List<AirportSector>> visibleByIcao,
        IReadOnlyDictionary<string, string> airportParentByIcao)
    {
        var positions = visibleByIcao.TryGetValue(sector.AirportIcao, out var rows)
            ? rows.Select(ToLadder).ToList()
            : new List<LadderPosition>();

        return AirportPositionLadder.ParentOf(
            ToLadder(sector), positions,
            airportParentByIcao.GetValueOrDefault(sector.AirportIcao), sector.AirportIcao);
    }

    private static LadderPosition ToLadder(AirportSector s) =>
        new(s.ComposePosition, MapType(s.Position), s.ParentCallsign);

    private static bool IsAtis(string? position) =>
        string.Equals(position, "ATIS", StringComparison.OrdinalIgnoreCase);

    /// <summary>Mappa il suffisso position del catalogo al SectorType operativo.</summary>
    private static SectorType MapType(string? position) => (position?.Trim().ToUpperInvariant()) switch
    {
        "DEL" => SectorType.Del,
        "GND" => SectorType.Gnd,
        "TWR" => SectorType.Twr,
        "APP" or "DEP" => SectorType.App,
        "CTR" or "FSS" => SectorType.Ctr,
        _ => SectorType.Ctr,
    };

    /// <summary>Nome amichevole del settore: nome display IVAO (<c>AtcCallsign</c>) se presente, altrimenti
    /// composto <c>"{ICAO} {Tipo}"</c> (es. "LIRF Approach"), infine il callsign grezzo come ultima spiaggia.</summary>
    private static string FriendlyName(Desired d)
    {
        if (!string.IsNullOrWhiteSpace(d.AtcCallsign)) return d.AtcCallsign!.Trim();
        var icao = IcaoPrefix(d.Callsign) ?? d.Callsign;
        var label = LabelOf(d.Position, d.Type);
        return string.IsNullOrEmpty(label) ? d.Callsign : $"{icao} {label}";
    }

    /// <summary>Etichetta leggibile del ruolo (allineata ai nomi già in DB). Fallback sul <see cref="SectorType"/> se
    /// la position del catalogo è assente.</summary>
    private static string LabelOf(string? position, SectorType type) => (position?.Trim().ToUpperInvariant()) switch
    {
        "DEL" => "Delivery",
        "GND" => "Ground",
        "TWR" => "Tower",
        "APP" => "Approach",
        "DEP" => "Departure",
        "CTR" => "Control",
        "FSS" => "Information",
        "ATIS" => "ATIS",
        _ => type switch
        {
            SectorType.Del => "Delivery",
            SectorType.Gnd => "Ground",
            SectorType.Twr or SectorType.ITwr => "Tower",
            SectorType.App => "Approach",
            SectorType.Ctr => "Control",
            _ => "",
        },
    };

    /// <summary>ICAO = i 4 caratteri prima del primo '_' del callsign (LIRF_TW1_APP → LIRF); null se non conforme.</summary>
    private static string? IcaoPrefix(string callsign)
    {
        var i = callsign.IndexOf('_');
        return i == 4 ? callsign[..4].ToUpperInvariant() : null;
    }

    /// <summary>Posto nella scaletta d'aeroporto (condiviso con l'editor gerarchia): più basso = più in alto.</summary>
    private static int CoverageFor(SectorType type) => AirportPositionLadder.Rung(type);

    /// <summary>
    /// Racconta alla casella che cosa è cambiato: settori spariti, nascosti, riparentati — e chiude da sé le
    /// segnalazioni la cui causa non c'è più (un callsign che torna).
    ///
    /// <para>⚠️ <b>La guardia dell'avvio a freddo.</b> Questa proiezione gira a OGNI avvio
    /// (<c>ProjectVipiSectors</c>), prima e indipendentemente dagli import. Con un catalogo vuoto o a metà —
    /// database appena sostituito, import fallito, ACC nascosti in blocco — <b>ogni</b> settore proiettato
    /// risulterebbe sparito, e la casella si riempirebbe di centinaia di righe false proprio nel momento in
    /// cui qualcosa è già andato storto. È lo stesso pericolo che l'import delle aree disinnesca da mesi («se
    /// la fetch fallisce non si pota»), applicato qui. Due soglie: catalogo vuoto, e sparizioni oltre un
    /// quarto dei settori attivi.</para>
    /// </summary>
    private async Task SegnalaAsync(
        IReadOnlyList<Sector> spariti, IReadOnlyList<Sector> riparentati, IReadOnlyList<Sector> tornati,
        IReadOnlySet<string> callsignInCatalogo, int primaAttivi,
        IReadOnlyDictionary<string, string> accDiCallsign, IReadOnlyDictionary<int, string> accCodeById,
        CancellationToken ct)
    {
        if (_impacts is null) return;

        // Un callsign TORNATO: la causa non c'è più, e la riga aperta non deve restare a fare rumore. Si
        // chiude col calcolo (utente 0), non con una persona: non l'ha risolta nessuno, si è risolta da sé.
        foreach (var s in tornati)
            await _impacts.ClearBySourceAsync(
                new[] { ImpactKind.SectorGone, ImpactKind.SectorHidden }, s.Callsign, ct);

        if (callsignInCatalogo.Count == 0)
            return;   // catalogo vuoto: non è «sono spariti tutti», è «non lo sappiamo».

        if (spariti.Count >= SparizioniMinimePerLaQuota
            && primaAttivi > 0
            && (double)spariti.Count / primaAttivi > QuotaSparizioniSospetta)
            return;   // sparizione di massa: catalogo sospetto, si proietta ma non si segnala.

        foreach (var s in spariti)
        {
            var acc = AccDi(s);
            // Il callsign è ancora in catalogo? Allora non è sparito: l'ha nascosto qualcuno.
            var kind = callsignInCatalogo.Contains(s.Callsign) ? ImpactKind.SectorHidden : ImpactKind.SectorGone;
            await _impacts.RaiseForSectorAsync(kind, s.Callsign, acc, ct);
        }

        foreach (var s in riparentati)
            await _impacts.RaiseForSectorAsync(ImpactKind.SectorReparented, s.Callsign, AccDi(s), ct);

        // Il codice ACC del settore: prima il catalogo (è la verità corrente), poi la riga proiettata — che
        // per un callsign sparito è l'unica cosa rimasta a saperlo.
        string AccDi(Sector s) =>
            accDiCallsign.TryGetValue(s.Callsign, out var a) ? a
            : accCodeById.TryGetValue(s.AccId, out var c) ? c
            : s.Acc?.Code ?? "";
    }
}

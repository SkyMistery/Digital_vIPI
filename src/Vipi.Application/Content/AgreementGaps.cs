using System;
using System.Collections.Generic;
using System.Linq;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Cosa manca, in una riga.</summary>
/// <param name="Kind">Che genere di lacuna: guida l'ordine e l'etichetta.</param>
/// <param name="Subject">Di chi si parla: un settore, un aeroporto, una coppia di enti.</param>
/// <param name="Count">Quante cose (clausole, punti); 0 = il conteggio non dice niente per questo genere.</param>
/// <param name="Items">I pezzi che qualificano la lacuna — i punti che stanno da un lato solo. Vuoto se non
/// servono.</param>
/// <param name="AgreementId">L'accordo da aprire per sistemarla, quando ce n'è uno.</param>
/// <param name="SectionId">La sezione da aprire, quando la lacuna è di una sezione.</param>
/// <param name="PairSectionId">La <b>seconda</b> sezione, quando la lacuna è una relazione fra due — le gemelle
/// da unire, i due versi da confrontare. Senza di lei la voce potrebbe solo indicare, non offrire di
/// sistemare.</param>
/// <remarks>
/// ⚠️ Nessun campo porta <b>parole</b>: le lacune si mostrano in un'interfaccia che esiste anche in inglese, e
/// una frase composta qui uscirebbe in italiano dentro una pagina inglese. È lo stesso motivo per cui la frase
/// di coordinamento vive nel template e non nella vista.
/// </remarks>
public sealed record AgreementGap(
    AgreementGapKind Kind, string Subject, int Count, IReadOnlyList<string> Items, int? AgreementId,
    int? SectionId = null, int? PairSectionId = null);

/// <summary>
/// I generi di lacuna, in ordine di gravità. L'ordine dell'enum <b>è</b> l'ordine di presentazione: prima ciò
/// che fa dire al documento una cosa diversa da quella concordata, in fondo una scrittura che manca.
///
/// <para>⚠️ Due voci sono sparite il 18 agosto 2026, e non perché non contassero: <b>non possono più esistere</b>.
/// «Accordo senza ricevente» era un lato B vuoto, e adesso le due colonne sono NOT NULL; «reciproco scritto in un
/// accordo a parte» era la stessa relazione in due schede, e adesso la coppia è unica per indice. Una guardia che
/// non può scattare non è una guardia: è un'abitudine.</para>
/// </summary>
public enum AgreementGapKind
{
    /// <summary>Due sezioni speculari (i due versi dello stesso traffico) elencano punti <b>diversi</b>. È il
    /// caso che nessuno vede a occhio, ed è già successo in archivio (BELIX di qua, OLGAT di là).</summary>
    AsymmetricDirections,

    /// <summary>Un traffico scritto in <b>un verso solo</b> dove il reciproco avrebbe senso: i sorvoli, che al
    /// confine passano nei due sensi. Prima non si poteva nemmeno porre la domanda — i due versi vivevano in
    /// accordi diversi.</summary>
    MissingReverse,

    /// <summary>
    /// Due sezioni <b>gemelle</b> nello stesso accordo: stesso traffico, stesso verso, stessi aeroporti.
    /// <para>È un <b>avviso</b>, non un errore (decisione del 18 agosto 2026): due arrivi a LIRF che valgono a
    /// condizioni diverse si scrivono con le <b>varianti</b> dentro una sezione sola, ma vietare la seconda
    /// sezione non lo insegnerebbe a nessuno — lo direbbe soltanto «no». Si segnala, e si offre «unisci».</para>
    /// </summary>
    TwinSections,

    /// <summary>Una clausola verso un APP che non dice ancora dove avviene il trasferimento: il suo livello può
    /// voler dire «autorizzato» o «al trasferimento», e solo chi l'ha scritta lo sa.</summary>
    ToReview,

    /// <summary>Una sezione senza nemmeno una clausola: c'è l'intestazione, ma il documento non renderà niente.
    /// È lavoro in corso quasi sempre, e per questo sta qui in basso e non fra le cose gravi.</summary>
    EmptySection,

    /// <summary>Un confinante estero <b>confermato</b> con cui non esiste nessun accordo.</summary>
    NeighbourWithoutAgreement,

    /// <summary>Un aeroporto della ACC senza nessuna sezione di arrivo.</summary>
    AirportWithoutArrivals,

    /// <summary>Un settore della ACC che non compare in nessun accordo: la sua vIPI non dice niente.</summary>
    SectorWithoutAgreements,
}

/// <summary>
/// **Dove lavorare.** Legge gli accordi di una ACC e dice cosa manca, in ordine di gravità.
///
/// <para>Funzione pura: le lacune sono un giudizio — «questo aeroporto dovrebbe avere degli arrivi» — e un
/// giudizio va potuto leggere e smentire. Nessuna di queste voci è un errore: sono cose che <b>probabilmente</b>
/// andrebbero scritte, e chi guarda decide.</para>
///
/// <para>La più importante resta <see cref="AgreementGapKind.AsymmetricDirections"/>, e non perché sia la più
/// grave: perché è l'unica che nessuno può vedere guardando una riga alla volta. Dal 18 agosto 2026 però il
/// confronto è <b>dentro un accordo</b> — le due sezioni stanno una sotto l'altra — e non più fra due schede
/// scritte in momenti diversi da persone diverse.</para>
/// </summary>
public static class AgreementGaps
{
    public static IReadOnlyList<AgreementGap> Find(
        string accCode,
        IReadOnlyList<AgreementRow> agreements,
        IReadOnlyList<SuggestionSector> accSectors,
        IReadOnlyList<string> accAirports,
        IReadOnlySet<string> confirmedNeighbourAccs,
        IReadOnlyDictionary<string, SectorType> sectorTypes)
    {
        var gaps = new List<AgreementGap>();

        foreach (var a in agreements)
        {
            // 1) Sezioni speculari con punti diversi, e sezioni senza reciproco.
            //
            //    ⚠️ Solo i traffici SENZA aeroporto (sorvoli, VFR, altro). Un arrivo non ha un reciproco: il
            //    traffico scende verso uno scalo e basta, e un ACC→APP è a senso unico per natura. Confrontandoli
            //    si ottenevano sei segnalazioni su sette false — provato a schermo — e una categoria che urla
            //    sempre insegna a non guardarla.
            foreach (var pair in Mirrors(a))
            {
                if (pair.Reverse is null)
                {
                    gaps.Add(new AgreementGap(AgreementGapKind.MissingReverse, Describe(a, pair.Section),
                        pair.Section.Clauses.Count, Array.Empty<string>(), a.Id, pair.Section.Id));
                    continue;
                }

                // Il confronto è lo stesso conto che il riquadro di lavoro fa fra i due versi: uno solo, in
                // AgreementPoints, letto da due posti.
                var spaiati = AgreementPoints.UnpairedBetween(pair.Section, pair.Reverse);
                if (spaiati.Count == 0) continue;
                gaps.Add(new AgreementGap(AgreementGapKind.AsymmetricDirections,
                    $"{a.SideA.Callsign} ⇄ {a.SideB.Callsign}", spaiati.Count, spaiati, a.Id,
                    pair.Section.Id, pair.Reverse.Id));
            }

            // 2) Sezioni gemelle: stesso traffico, stesso verso, stessi scali. Avviso, non errore.
            foreach (var twins in a.Sections
                         .GroupBy(s => (s.Kind, s.Direction, Airports: Key(s)))
                         .Where(g => g.Count() > 1))
            {
                var elenco = twins.OrderBy(s => s.Order).ToList();
                gaps.Add(new AgreementGap(AgreementGapKind.TwinSections, Describe(a, elenco[0]),
                    elenco.Sum(s => s.Clauses.Count), Array.Empty<string>(), a.Id,
                    elenco[0].Id, elenco[1].Id));
            }

            // 3) Clausole verso un APP senza faccetta trasferimento.
            var daRivedere = a.Sections.Sum(s => s.Clauses.Count(c => NeedsReview(a, s, c, sectorTypes)));
            if (daRivedere > 0)
                gaps.Add(new AgreementGap(AgreementGapKind.ToReview, Describe(a), daRivedere,
                    Array.Empty<string>(), a.Id));

            // 4) Sezioni vuote: l'intestazione c'è, il documento non renderà niente.
            foreach (var s in a.Sections.Where(s => s.Clauses.Count == 0).OrderBy(s => s.Order))
                gaps.Add(new AgreementGap(AgreementGapKind.EmptySection, Describe(a, s), 0,
                    Array.Empty<string>(), a.Id, s.Id));
        }

        // 5) Confinanti confermati senza nessun accordo. Il confronto è per ACC dell'ente, non per callsign:
        //    un accordo con una qualunque posizione del centro estero basta a dire che il confine è scritto.
        var accsInAgreements = agreements
            .SelectMany(a => new[] { a.SideA.Callsign, a.SideB.Callsign })
            .Select(cs => AccOf(cs, accSectors))
            .Where(x => x is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase)!;
        foreach (var vicino in confirmedNeighbourAccs.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            if (!accsInAgreements.Contains(vicino))
                gaps.Add(new AgreementGap(AgreementGapKind.NeighbourWithoutAgreement, vicino, 0,
                    Array.Empty<string>(), null));

        // 6) Aeroporti senza arrivi. Solo gli arrivi e non anche le partenze: un aeroporto senza partenze scritte
        //    è comune e legittimo (le consegna la torre), uno senza arrivi lascia scoperto chi ci deve scendere.
        var conArrivi = agreements.SelectMany(a => a.Sections)
            .Where(s => s.Kind == TransferFlowKind.Arrival)
            .SelectMany(s => s.Airports.Select(x => x.Icao))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var icao in accAirports.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            if (!conArrivi.Contains(icao))
                gaps.Add(new AgreementGap(AgreementGapKind.AirportWithoutArrivals, icao, 0,
                    Array.Empty<string>(), null));

        // 7) Settori che non compaiono in nessun accordo: la loro vIPI non ha coordinamenti da mostrare.
        var citati = agreements.SelectMany(a => new[] { a.SideA.Callsign, a.SideB.Callsign })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var s in accSectors
                     .Where(s => string.Equals(s.AccCode, accCode, StringComparison.OrdinalIgnoreCase))
                     .Where(s => s.Type is SectorType.Ctr or SectorType.App)
                     .Where(s => !citati.Contains(s.Callsign))
                     .OrderBy(s => s.Callsign, StringComparer.OrdinalIgnoreCase))
            gaps.Add(new AgreementGap(AgreementGapKind.SectorWithoutAgreements, s.Callsign, 0,
                Array.Empty<string>(), null));

        return gaps.OrderBy(g => g.Kind).ThenBy(g => g.Subject, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Una sezione e la sua specular: la stessa cosa nel verso opposto, se qualcuno l'ha scritta.</summary>
    private sealed record Mirror(AgreementSectionRow Section, AgreementSectionRow? Reverse);

    /// <summary>
    /// Le sezioni che <b>hanno</b> un reciproco possibile — sorvoli, VFR, altro — accoppiate col loro verso
    /// opposto. Ogni coppia si restituisce una volta sola, dalla parte del verso <c>AtoB</c>: segnalarla due
    /// volte direbbe la stessa cosa due volte.
    /// </summary>
    private static IEnumerable<Mirror> Mirrors(AgreementRow a)
    {
        var mirrorable = a.Sections
            .Where(s => s.Kind is not (TransferFlowKind.Arrival or TransferFlowKind.Departure))
            .Where(s => s.Clauses.Count > 0)
            .ToList();

        foreach (var s in mirrorable.Where(s => s.Direction == AgreementDirection.AtoB))
            yield return new Mirror(s, mirrorable.FirstOrDefault(
                x => x.Kind == s.Kind && x.Direction == AgreementDirection.BtoA && Key(x) == Key(s)));

        // Un verso BtoA senza il suo AtoB non sarebbe altrimenti visto da nessuno: il giro sopra parte dall'altro
        // capo, che qui non c'è.
        foreach (var s in mirrorable.Where(s => s.Direction == AgreementDirection.BtoA))
            if (!mirrorable.Any(x => x.Kind == s.Kind && x.Direction == AgreementDirection.AtoB && Key(x) == Key(s)))
                yield return new Mirror(s, null);
    }

    /// <summary>La chiave con cui due sezioni «dicono la stessa cosa»: gli scali, normalizzati.</summary>
    private static string Key(AgreementSectionRow s) =>
        string.Join("·", s.Airports.Select(x => x.Icao.Trim().ToUpperInvariant()).OrderBy(x => x, StringComparer.Ordinal));

    private static bool NeedsReview(AgreementRow a, AgreementSectionRow s, AgreementClauseRow c,
        IReadOnlyDictionary<string, SectorType> types)
    {
        if (c.HandoffKind != TransferHandoffKind.Unspecified) return false;
        var receiver = a.Receiver(s.Direction).Callsign;
        return types.TryGetValue(receiver, out var t) && t == SectorType.App;
    }

    private static string? AccOf(string callsign, IReadOnlyList<SuggestionSector> sectors) =>
        sectors.FirstOrDefault(s => string.Equals(s.Callsign, callsign, StringComparison.OrdinalIgnoreCase))?.AccCode;

    /// <summary>Come si nomina un accordo in un elenco: i due capi.</summary>
    private static string Describe(AgreementRow a) => $"{a.SideA.Callsign} ⇄ {a.SideB.Callsign}";

    /// <summary>Come si nomina una sezione: i capi nel verso in cui vale, e il traffico.</summary>
    private static string Describe(AgreementRow a, AgreementSectionRow s)
    {
        var testa = $"{a.Sender(s.Direction).Callsign} → {a.Receiver(s.Direction).Callsign}";
        var apts = s.AirportsLabel;
        return apts.Length > 0 ? $"{testa} ({apts})" : testa;
    }
}

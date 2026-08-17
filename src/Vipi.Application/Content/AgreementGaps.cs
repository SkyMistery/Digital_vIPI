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
/// <param name="PairAgreementId">Il <b>secondo</b> accordo, quando la lacuna è una relazione fra due — il
/// reciproco scritto a parte. Senza di lui la voce potrebbe solo indicare, non offrire di sistemare.</param>
/// <remarks>
/// ⚠️ Nessun campo porta <b>parole</b>: le lacune si mostrano in un'interfaccia che esiste anche in inglese, e
/// una frase composta qui uscirebbe in italiano dentro una pagina inglese. È lo stesso motivo per cui la frase
/// di coordinamento vive nel template e non nella vista.
/// </remarks>
public sealed record AgreementGap(
    AgreementGapKind Kind, string Subject, int Count, IReadOnlyList<string> Items, int? AgreementId,
    int? PairAgreementId = null);

/// <summary>
/// I generi di lacuna, in ordine di gravità. L'ordine dell'enum <b>è</b> l'ordine di presentazione: la prima è
/// quella che manda traffico a UNICOM adesso, l'ultima è una scrittura che manca.
/// </summary>
public enum AgreementGapKind
{
    /// <summary>Un accordo senza nessuno sul lato che riceve: il traffico finisce a UNICOM, e succede ORA.</summary>
    NoReceiver,

    /// <summary>Due enti hanno accordi solo in un verso, e i punti dei due versi <b>non coincidono</b>. È il caso
    /// che nessuno vede a occhio, ed è già successo in archivio (BELIX di qua, OLGAT di là).</summary>
    AsymmetricDirections,

    /// <summary>
    /// Il reciproco di un accordo è scritto in un <b>accordo a parte</b> invece che nel suo verso opposto: stessi
    /// enti a lati scambiati, stesso traffico, stessi aeroporti.
    /// <para>Sta qui e non più in basso perché è <b>sistemabile in un gesto</b> e perché, coi due versi a vista,
    /// quei due accordi mostrano ognuno un verso vuoto mentre il contenuto sta nel nodo accanto: chi guarda
    /// conclude che il reciproco manca, e lo riscrive una terza volta.</para>
    /// </summary>
    ReverseInSeparateAgreement,

    /// <summary>Una clausola verso un APP che non dice ancora dove avviene il trasferimento: il suo livello può
    /// voler dire «autorizzato» o «al trasferimento», e solo chi l'ha scritta lo sa.</summary>
    ToReview,

    /// <summary>Un confinante estero <b>confermato</b> con cui non esiste nessun accordo.</summary>
    NeighbourWithoutAgreement,

    /// <summary>Un aeroporto della ACC senza nessun accordo di arrivo.</summary>
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
/// <para>La più importante è <see cref="AgreementGapKind.AsymmetricDirections"/>, e non perché sia la più
/// grave: perché è l'unica che nessuno può vedere guardando una pagina alla volta. Le altre si notano aprendo
/// il ramo giusto; quella richiede di confrontare due accordi scritti in momenti diversi da persone diverse.</para>
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

        // 1) Accordi senza nessuno che riceva: il traffico finisce a UNICOM adesso.
        foreach (var a in agreements.Where(a => a.Parties.All(p => p.Side != AgreementSide.B)))
            gaps.Add(new AgreementGap(AgreementGapKind.NoReceiver, Describe(a), a.Clauses.Count,
                Array.Empty<string>(), a.Id));

        // 2) Versi asimmetrici: due enti che si scambiano traffico su insiemi di punti DIVERSI nei due sensi.
        //
        //    ⚠️ Il confronto attraversa gli ACCORDI, non solo i versi di uno. Un accordo bilaterale scritto come
        //    tale porta i due versi dentro di sé; ma due accordi a un verso ciascuno fra gli stessi enti sono
        //    esattamente la stessa cosa detta in due posti — ed è come il travaso lascia l'archivio, perché
        //    accoppiarli automaticamente avrebbe voluto dire scegliere quale dei due versi valesse.
        //
        //    Cercare solo dentro un accordo faceva mancare proprio il caso per cui questo cruscotto esiste: in
        //    archivio LIBB→LGGG elenca BELIX e LGGG→LIBB elenca OLGAT, e sono due accordi separati.
        //
        //    ⚠️ Solo i traffici SENZA aeroporto (sorvoli, VFR, altro). Un arrivo non ha un reciproco: il traffico
        //    scende verso uno scalo e basta, e un ACC→APP è a senso unico per natura. Confrontandoli si
        //    ottenevano sei segnalazioni su sette false — provato a schermo — e una categoria che urla sempre
        //    insegna a non guardarla.
        foreach (var g in PointsByPair(agreements.Where(a => a.TrafficKind
                         is not (TransferFlowKind.Arrival or TransferFlowKind.Departure)).ToList())
                     .GroupBy(x => x.Pair, PairComparer.Instance)
                     .Where(g => g.Count() > 1))
        {
            var versi = g.ToList();
            // Il confronto è lo stesso che il riquadro di lavoro fa fra i due versi di un accordo: un conto
            // solo, in AgreementPoints, letto da due posti.
            var spaiati = AgreementPoints.Unpaired(versi.Select(v => (IReadOnlySet<string>)v.Points).ToList());
            if (spaiati.Count == 0) continue;

            // L'accordo da aprire è quello del primo verso: da lì si raggiunge l'altro capo, che nell'albero è
            // sotto la stessa controparte.
            gaps.Add(new AgreementGap(AgreementGapKind.AsymmetricDirections,
                $"{g.Key.A} ⇄ {g.Key.B}", spaiati.Count, spaiati, versi[0].AgreementId));
        }

        // 2-bis) Il reciproco scritto in un accordo A PARTE. È come il travaso ha lasciato l'archivio, e coi due
        //        versi a vista diventa attivamente ingannevole: entrambi gli accordi mostrano un verso vuoto
        //        mentre il contenuto sta nell'altro nodo. Qui si propone di unirli — la proposta è strettissima
        //        (parti specchiate, stesso traffico, STESSI aeroporti), sennò urla dove non deve.
        foreach (var p in AgreementMerge.Candidates(agreements, new AgreementViewpoint(accCode, accSectors)))
        {
            var keep = agreements.First(a => a.Id == p.KeepId);
            gaps.Add(new AgreementGap(AgreementGapKind.ReverseInSeparateAgreement, Describe(keep), p.Clauses,
                Array.Empty<string>(), p.KeepId, p.AbsorbId));
        }

        // 3) Clausole verso un APP senza faccetta trasferimento.
        foreach (var a in agreements)
        {
            var daRivedere = a.Clauses.Count(c => NeedsReview(a, c, sectorTypes));
            if (daRivedere > 0)
                gaps.Add(new AgreementGap(AgreementGapKind.ToReview, Describe(a), daRivedere,
                    Array.Empty<string>(), a.Id));
        }

        // 4) Confinanti confermati senza nessun accordo. Il confronto è per ACC dell'ente, non per callsign:
        //    un accordo con una qualunque posizione del centro estero basta a dire che il confine è scritto.
        var accsInAgreements = agreements
            .SelectMany(a => a.Parties)
            .Select(p => AccOf(p.Callsign, accSectors))
            .Where(x => x is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase)!;
        foreach (var vicino in confirmedNeighbourAccs.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            if (!accsInAgreements.Contains(vicino))
                gaps.Add(new AgreementGap(AgreementGapKind.NeighbourWithoutAgreement, vicino, 0,
                    Array.Empty<string>(), null));

        // 5) Aeroporti senza arrivi. Solo gli arrivi e non anche le partenze: un aeroporto senza partenze scritte
        //    è comune e legittimo (le consegna la torre), uno senza arrivi lascia scoperto chi ci deve scendere.
        var conArrivi = agreements
            .Where(a => a.TrafficKind == TransferFlowKind.Arrival)
            .SelectMany(a => a.Airports.Select(x => x.Icao))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var icao in accAirports.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            if (!conArrivi.Contains(icao))
                gaps.Add(new AgreementGap(AgreementGapKind.AirportWithoutArrivals, icao, 0,
                    Array.Empty<string>(), null));

        // 6) Settori che non compaiono in nessun accordo: la loro vIPI non ha coordinamenti da mostrare.
        var citati = agreements.SelectMany(a => a.Parties).Select(p => p.Callsign)
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

    /// <summary>Un verso di scambio fra due enti: chi cede, chi riceve, e su quali punti.</summary>
    private sealed record DirectionPoints(string From, string To, HashSet<string> Points, int AgreementId)
    {
        /// <summary>La coppia, <b>senza verso</b>: è la chiave con cui andata e ritorno si trovano.</summary>
        public (string A, string B) Pair =>
            string.Compare(From, To, StringComparison.OrdinalIgnoreCase) <= 0 ? (From, To) : (To, From);
    }

    /// <summary>
    /// Tutti i versi di scambio, uno per (accordo × direzione × coppia di enti). Un accordo con più enti per
    /// lato produce più coppie: sono davvero scambi distinti, e vanno confrontati come tali.
    /// </summary>
    private static IEnumerable<DirectionPoints> PointsByPair(IReadOnlyList<AgreementRow> agreements)
    {
        foreach (var a in agreements)
            foreach (var d in new[] { AgreementDirection.AtoB, AgreementDirection.BtoA })
            {
                var points = AgreementPoints.Of(a, d);
                if (points.Count == 0) continue;

                var from = Side(a, d == AgreementDirection.AtoB ? AgreementSide.A : AgreementSide.B);
                var to = Side(a, d == AgreementDirection.AtoB ? AgreementSide.B : AgreementSide.A);
                foreach (var f in from)
                    foreach (var t in to)
                        yield return new DirectionPoints(f, t, points, a.Id);
            }
    }

    private static IReadOnlyList<string> Side(AgreementRow a, AgreementSide side) =>
        a.Parties.Where(p => p.Side == side).OrderBy(p => p.Order).Select(p => p.Callsign).ToList();

    /// <summary>Confronto della coppia senza verso: <c>(A,B)</c> e <c>(B,A)</c> sono la stessa coppia.</summary>
    private sealed class PairComparer : IEqualityComparer<(string A, string B)>
    {
        public static readonly PairComparer Instance = new();

        public bool Equals((string A, string B) x, (string A, string B) y) =>
            string.Equals(x.A, y.A, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.B, y.B, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string A, string B) v) =>
            HashCode.Combine(v.A.ToUpperInvariant(), v.B.ToUpperInvariant());
    }

    private static bool NeedsReview(AgreementRow a, AgreementClauseRow c,
        IReadOnlyDictionary<string, SectorType> types)
    {
        if (c.HandoffKind != TransferHandoffKind.Unspecified) return false;
        var side = c.Direction == AgreementDirection.AtoB ? AgreementSide.B : AgreementSide.A;
        return a.Parties.Where(p => p.Side == side)
            .Any(p => types.TryGetValue(p.Callsign, out var t) && t == SectorType.App);
    }

    private static string? AccOf(string callsign, IReadOnlyList<SuggestionSector> sectors) =>
        sectors.FirstOrDefault(s => string.Equals(s.Callsign, callsign, StringComparison.OrdinalIgnoreCase))?.AccCode;

    /// <summary>Come si nomina un accordo in un elenco: i due capi, e il traffico. Senza un capo si dice quello
    /// che c'è — un accordo scritto a metà va comunque riconosciuto per poterlo aprire.</summary>
    private static string Describe(AgreementRow a)
    {
        var sideA = string.Join(" · ", a.Parties.Where(p => p.Side == AgreementSide.A)
            .OrderBy(p => p.Order).Select(p => p.Callsign));
        var sideB = string.Join(" · ", a.Parties.Where(p => p.Side == AgreementSide.B)
            .OrderBy(p => p.Order).Select(p => p.Callsign));
        var apts = string.Join(" · ", a.Airports.OrderBy(x => x.Order).Select(x => x.Icao));

        var testa = $"{(sideA.Length > 0 ? sideA : "—")} → {(sideB.Length > 0 ? sideB : "—")}";
        return apts.Length > 0 ? $"{testa} ({apts})" : testa;
    }
}

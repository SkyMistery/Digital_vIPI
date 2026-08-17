using System;
using System.Collections.Generic;
using System.Linq;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Due accordi che sono i <b>due versi</b> della stessa cosa, scritti separati.</summary>
/// <param name="KeepId">L'accordo che resta, e che riceve le clausole dell'altro nel suo verso libero.</param>
/// <param name="AbsorbId">L'accordo assorbito: le sue clausole passano di là, e il guscio sparisce.</param>
/// <param name="Clauses">Quante clausole si spostano: è il numero che la conferma deve dire.</param>
public sealed record ReverseAgreementPair(int KeepId, int AbsorbId, int Clauses);

/// <summary>
/// **Il reciproco scritto altrove.**
///
/// <para>Il travaso dal modello vecchio ha lasciato i due versi in accordi <b>separati</b>, e a ragione: accoppiarli
/// da sé avrebbe voluto dire scegliere quale dei due valesse. Il risultato è che sul <c>vipi.db</c> vero tre
/// relazioni sono scritte due volte in senso opposto — <c>LIBB_ES_CTR ⇄ LGGG_W_CTR</c>, <c>⇄ LDZO_CTR</c>,
/// <c>⇄ LAAA_CTR</c>, tutte sorvoli — e con i due versi a vista quei tre accordi mostrerebbero un verso pieno e
/// uno vuoto <b>mentre il loro reciproco vive nel nodo accanto</b>.</para>
///
/// <para><b>Perché la proposta è così stretta.</b> Servono parti specchiate, stesso tipo di traffico <b>e stessi
/// aeroporti</b>. Le altre cinque relazioni a versi opposti dell'archivio hanno aeroporti diversi — sono arrivi
/// per gruppo di scali, non lo stesso accordo — e proporle insegnerebbe a ignorare la proposta. Una categoria che
/// urla sempre non si guarda più: è già successo col cruscotto delle lacune.</para>
///
/// <para>Funzione pura, come gli altri tre ausili: «questi due sono lo stesso accordo» è un <b>giudizio</b>, e un
/// giudizio va potuto provare e smentire senza un database. Chi decide resta chi ha scritto quei documenti — qui
/// si propone, e l'unione si vede prima di scriverla.</para>
/// </summary>
public static class AgreementMerge
{
    /// <summary>
    /// Le coppie che si possono unire, una per relazione.
    /// </summary>
    /// <param name="lens">Serve solo a scegliere <b>quale dei due resta</b>: si preferisce quello che da qui si
    /// legge nel verso di casa, così l'accordo unito non nasce già girato al contrario.</param>
    public static IReadOnlyList<ReverseAgreementPair> Candidates(
        IReadOnlyList<AgreementRow> agreements, AgreementViewpoint lens)
    {
        var found = new List<ReverseAgreementPair>();
        var visti = new HashSet<int>();

        foreach (var x in agreements.OrderBy(a => a.Id))
        {
            if (visti.Contains(x.Id)) continue;

            foreach (var y in agreements.Where(a => a.Id != x.Id).OrderBy(a => a.Id))
            {
                if (visti.Contains(y.Id) || !IsReverseOf(x, y)) continue;

                var (keep, absorb) = Prefer(x, y, lens);
                if (!TargetFree(keep, absorb)) continue;
                if (absorb.Clauses.Count == 0) continue;   // un guscio vuoto non è un reciproco

                found.Add(new ReverseAgreementPair(keep.Id, absorb.Id, absorb.Clauses.Count));
                visti.Add(x.Id);
                visti.Add(y.Id);
                break;
            }
        }

        return found;
    }

    /// <summary>
    /// <paramref name="y"/> dice la stessa cosa di <paramref name="x"/> al rovescio: parti specchiate, stesso
    /// traffico, stessi aeroporti.
    /// </summary>
    public static bool IsReverseOf(AgreementRow x, AgreementRow y)
    {
        var xa = Side(x, AgreementSide.A);
        var xb = Side(x, AgreementSide.B);
        // Un accordo senza controparte (traffico a UNICOM) non ha un rovescio: mancherebbe chi lo scrive.
        if (xa.Count == 0 || xb.Count == 0) return false;

        return xa.SetEquals(Side(y, AgreementSide.B))
               && xb.SetEquals(Side(y, AgreementSide.A))
               && x.TrafficKind == y.TrafficKind
               && Airports(x).SetEquals(Airports(y));
    }

    /// <summary>
    /// Il verso in cui una clausola dell'accordo assorbito finisce in quello che resta: si <b>ribalta</b>, perché
    /// i due accordi hanno i lati scambiati. Un <c>A→B</c> di là è un <c>B→A</c> di qua.
    /// </summary>
    public static AgreementDirection Flip(AgreementDirection d) =>
        d == AgreementDirection.AtoB ? AgreementDirection.BtoA : AgreementDirection.AtoB;

    /// <summary>
    /// I versi che le clausole occuperanno sono <b>liberi</b> in quello che resta.
    /// <para>Senza questo controllo l'unione accoderebbe le clausole di due scritture diverse nella stessa
    /// tabella, e non ci sarebbe modo di sapere quale delle due valga — cioè esattamente la scelta che il travaso
    /// si era rifiutato di fare.</para>
    /// </summary>
    public static bool TargetFree(AgreementRow keep, AgreementRow absorb) =>
        absorb.Clauses.Select(c => Flip(c.Direction)).Distinct()
            .All(d => !keep.Clauses.Any(c => c.Direction == d));

    /// <summary>Chi resta: prima chi da qui si legge nel verso di casa, poi chi ha più clausole, poi l'id più
    /// basso — perché la proposta non deve cambiare fra due caricamenti.</summary>
    private static (AgreementRow Keep, AgreementRow Absorb) Prefer(
        AgreementRow x, AgreementRow y, AgreementViewpoint lens)
    {
        var xNear = lens.Orient(x).NearSide == AgreementSide.A;
        var yNear = lens.Orient(y).NearSide == AgreementSide.A;
        if (xNear != yNear) return xNear ? (x, y) : (y, x);

        if (x.Clauses.Count != y.Clauses.Count)
            return x.Clauses.Count > y.Clauses.Count ? (x, y) : (y, x);

        return x.Id <= y.Id ? (x, y) : (y, x);
    }

    private static HashSet<string> Side(AgreementRow a, AgreementSide side) =>
        a.Parties.Where(p => p.Side == side).Select(p => p.Callsign)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static HashSet<string> Airports(AgreementRow a) =>
        a.Airports.Select(x => x.Icao).ToHashSet(StringComparer.OrdinalIgnoreCase);
}

using System;
using System.Collections.Generic;
using System.Linq;
using Vipi.Application.Abstractions;

namespace Vipi.Application.Stats;

/// <summary>Il conto di un aeroporto in un giorno: quanto traffico, e quanto ha trovato un controllore.</summary>
/// <param name="Inbound">Arrivi.</param>
/// <param name="Outbound">Partenze.</param>
/// <param name="Overflight">Sorvoli: <b>non</b> sono movimenti del campo, ma si contano perché il campo li vede.</param>
/// <param name="Covered">Arrivi e partenze caduti in un minuto in cui una posizione del campo era aperta.</param>
// ⚠️ Pubblico perché compare nella FIRMA di un tipo pubblico: chi lo restringe scopre che il
// compilatore lo dice da sé (CS0050/CS0051/CS0053). È superficie del modulo quanto il tipo che lo
// espone (ADR-0005 D6, revisione del 6 settembre 2026, R-009).
public sealed record AirportDayTally(int Inbound, int Outbound, int Overflight, int Covered)
{
    /// <summary>I movimenti veri del campo: arrivi più partenze. I sorvoli restano fuori, come nel §15.2.</summary>
    public int Movements => Inbound + Outbound;

    public static readonly AirportDayTally Empty = new(0, 0, 0, 0);
}

/// <summary>
/// Quanto del traffico di un aeroporto ha trovato un controllore acceso.
///
/// <para><b>La regola, e perché va DETTA e non nascosta.</b> La sorgente non dichiara l'istante del
/// movimento: dichiara quando il pilota si è collegato (<c>createdAt</c>) e quando è stato visto l'ultima
/// volta (<c>lastTrack.timestamp</c>). Quindi:</para>
/// <list type="bullet">
///   <item><b>arrivo</b> → conta l'<b>ultimo avvistamento</b>: è il momento in cui era su quel campo;</item>
///   <item><b>partenza</b> → conta il <b>collegamento</b>: è l'istante più vicino al decollo che esista.</item>
/// </list>
///
/// <para>⚠️ È un'<b>approssimazione</b>, e la pagina la scrive. Un pilota che si collega quaranta minuti
/// prima del push, con la torre che apre nel frattempo, risulta scoperto: la sorgente non dà nessun modo di
/// fare meglio senza campionare ogni volo ogni minuto — mezzo milione di righe l'anno che ne diventerebbero
/// trenta. «Coperto» qui vuol dire <b>c'era un controllore su quel campo in quell'istante</b>, non «quel
/// volo è stato lavorato»: sono due cose diverse e la seconda non è misurabile.</para>
///
/// <para>⚠️ Gli intervalli ATC arrivano già <b>uniti</b> (<see cref="CoverageGrid.Unione"/>): due posizioni
/// aperte insieme sullo stesso campo sono un'apertura sola, o il conto dei minuti sarebbe doppio.</para>
///
/// <para>Puro e deterministico: nessun I/O, nessun orologio.</para>
/// </summary>
public static class AirportCoverage
{
    /// <summary>
    /// L'istante a cui attribuire il movimento; <c>null</c> quando la sorgente non ne dà nessuno — e allora
    /// il movimento <b>non si conta affatto</b>, né come coperto né come scoperto: metterlo fra gli scoperti
    /// gonfierebbe la parte mancante con righe di cui non sappiamo niente.
    /// </summary>
    public static DateTimeOffset? Instant(SourceAirportMovement m) =>
        m.Kind == AirportMovementKind.Outbound
            ? m.ConnectedUtc ?? m.LastSeenUtc
            : m.LastSeenUtc ?? m.ConnectedUtc;

    /// <summary>
    /// Il conto di una finestra. <paramref name="spans"/> sono le aperture ATC di <b>quel</b> campo, già
    /// unite; <paramref name="from"/>/<paramref name="to"/> ritagliano, perché la sorgente restituisce anche
    /// voli appena fuori dal bordo chiesto.
    /// </summary>
    public static AirportDayTally Tally(
        IReadOnlyList<SourceAirportMovement> movimenti,
        IReadOnlyList<OnlineSpan> spans,
        DateTimeOffset from,
        DateTimeOffset to)
    {
        int inbound = 0, outbound = 0, overflight = 0, coperti = 0;

        foreach (var m in Distinti(movimenti))
        {
            if (Instant(m) is not { } quando) continue;
            if (quando < from || quando >= to) continue;

            switch (m.Kind)
            {
                case AirportMovementKind.Inbound: inbound++; break;
                case AirportMovementKind.Outbound: outbound++; break;
                default: overflight++; continue;    // il sorvolo non è un movimento del campo
            }

            if (Dentro(spans, quando)) coperti++;
        }

        return new AirportDayTally(inbound, outbound, overflight, coperti);
    }

    /// <summary>
    /// Quanto possono stare lontani due pezzi dello stesso callsign sulla stessa rotta per essere lo stesso volo:
    /// oltre, è il volo di linea del giorno dopo (o di stasera), non una riconnessione.
    /// </summary>
    public static readonly TimeSpan StessoVolo = TimeSpan.FromHours(3);

    /// <summary>
    /// Lo stesso volo può tornare più volte nella stessa risposta: una riconnessione lo ripresenta con un
    /// <c>id</c> nuovo. L'identità è <b>callsign + partenza + arrivo + verso</b>, a pezzi distanti meno di
    /// <see cref="StessoVolo"/>.
    ///
    /// <para>🔴 U-228 (revisione totale 3): la chiave era il <b>piano di volo</b>, e alla riconnessione il pilota
    /// ne deposita uno nuovo — sul dato vero <c>AZA1430 LIRF→LICJ</c> contava due arrivi. È la stessa lezione già
    /// pagata dal riempimento a posteriori (<c>EfAtcTrafficStore</c>). E del gruppo si teneva il PRIMO pezzo, cioè
    /// per un arrivo l'istante della caduta a metà volo: ora l'arrivo è il pezzo visto per ultimo e la partenza
    /// quello collegato per primo, come dice <see cref="Instant"/>.</para>
    ///
    /// <para>⚠️ Il verso fa parte della chiave: un LIRF→LIRF (circuito, rientro) è una partenza <b>e</b> un
    /// arrivo dello stesso campo, e sono due movimenti, non uno.</para>
    /// </summary>
    private static IEnumerable<SourceAirportMovement> Distinti(IReadOnlyList<SourceAirportMovement> movimenti)
    {
        var gruppi = movimenti.GroupBy(m => (
            m.Kind,
            Cs: (m.PilotCallsign ?? "").Trim().ToUpperInvariant(),
            Dep: (m.DepIcao ?? "").Trim().ToUpperInvariant(),
            Arr: (m.ArrIcao ?? "").Trim().ToUpperInvariant()));

        foreach (var g in gruppi)
        {
            var pezzi = g.OrderBy(m => m.ConnectedUtc ?? m.LastSeenUtc ?? DateTimeOffset.MinValue).ToList();
            var volo = new List<SourceAirportMovement>();
            DateTimeOffset? fineVolo = null;
            foreach (var m in pezzi)
            {
                var inizio = m.ConnectedUtc ?? m.LastSeenUtc;
                if (volo.Count > 0 && inizio is { } i && fineVolo is { } f && i - f > StessoVolo)
                {
                    yield return Rappresentante(volo);
                    volo.Clear();
                    fineVolo = null;
                }
                volo.Add(m);
                var fine = m.LastSeenUtc ?? m.ConnectedUtc;
                if (fine is { } fn && (fineVolo is null || fn > fineVolo)) fineVolo = fn;
            }
            if (volo.Count > 0) yield return Rappresentante(volo);
        }
    }

    /// <summary>Il pezzo che rappresenta il volo: l'ultimo visto per un arrivo, il primo collegato per il resto.</summary>
    private static SourceAirportMovement Rappresentante(List<SourceAirportMovement> volo) =>
        volo[0].Kind == AirportMovementKind.Inbound
            ? volo.MaxBy(m => m.LastSeenUtc ?? m.ConnectedUtc ?? DateTimeOffset.MinValue)!
            : volo.MinBy(m => m.ConnectedUtc ?? m.LastSeenUtc ?? DateTimeOffset.MaxValue)!;

    /// <summary>
    /// Ricerca binaria sugli intervalli uniti: sono ordinati per costruzione, e una scansione lineare per
    /// ognuno di decine di migliaia di movimenti costerebbe due ordini di grandezza in più.
    /// </summary>
    private static bool Dentro(IReadOnlyList<OnlineSpan> spans, DateTimeOffset quando)
    {
        int lo = 0, hi = spans.Count - 1;
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            var s = spans[mid];
            if (quando < s.StartUtc) hi = mid - 1;
            else if (quando >= s.EndUtc) lo = mid + 1;
            else return true;
        }
        return false;
    }
}

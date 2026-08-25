using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vipi.Application.Abstractions;
using Vipi.Domain;

namespace Vipi.Application.Stats;

/// <summary>Esito di un giro di consolidamento, per il registro e per la pagina Sorgenti.</summary>
public sealed record AirportRollupResult(int Chunks, int Days, int Movements, int Airports);

/// <summary>
/// Consolida il <b>traffico di ogni aeroporto italiano</b>, giorno per giorno, e quanto di quel traffico ha
/// trovato un controllore acceso.
///
/// <para><b>Che domanda risponde, e perché non la rispondeva già nessuno.</b> L'archivio delle statistiche
/// sa quanto abbiamo controllato <i>noi</i>; non sa quanto traffico <b>c'era</b>. La differenza fra le due
/// è la sola risposta onesta a «quanto dell'Italia copriamo davvero», ed è quello che si legge nella
/// sezione «Aeroporti» della pagina di divisione.</para>
///
/// <para><b>Quanto costa.</b> Una chiamata per blocco (fino a trenta giorni di un aeroporto): novantatré
/// campi per dodici mesi sono ~1 100 chiamate una volta sola, poi una manciata a notte. Il tetto per giro
/// spalma l'arretrato su più notti invece di fare mille richieste in un colpo — stessa scelta del
/// riempimento retroattivo delle sessioni.</para>
///
/// <para>⚠️ <b>Gate condiviso con le sessioni</b> (<see cref="ImportCategory.AtcSessions"/>) e non una
/// categoria nuova: è lo stesso servizio, e chi spegne la raccolta delle statistiche si aspetta che si
/// spenga tutta. Una categoria nuova avrebbe voluto un <c>bool NOT NULL</c> in più — che nasce
/// <c>false</c> su ogni riga esistente, cioè spento a chi non ha chiesto niente.</para>
/// </summary>
public sealed class AirportTrafficRollupUseCase
{
    private readonly IAirportTrafficSource _sorgente;
    private readonly IAirportTrafficRollupStore _archivio;
    private readonly IImportPolicyStore _policy;

    public AirportTrafficRollupUseCase(
        IAirportTrafficSource sorgente, IAirportTrafficRollupStore archivio, IImportPolicyStore policy)
    {
        _sorgente = sorgente;
        _archivio = archivio;
        _policy = policy;
    }

    /// <param name="max">Quanti blocchi al massimo in questo giro.</param>
    public async Task<AirportRollupResult> RunAsync(
        DateTimeOffset from, DateTimeOffset to, int max, DateTimeOffset now, CancellationToken ct = default)
    {
        // Il gate sta prima di qualunque chiamata, come nel resto del giro sorgenti.
        var policy = await _policy.GetAsync(ct);
        if (!policy.IsImported(ImportCategory.AtcSessions))
            return new AirportRollupResult(0, 0, 0, 0);

        var icaos = await _archivio.AirportsAsync(ct);
        if (icaos.Count == 0) return new AirportRollupResult(0, 0, 0, 0);

        var conosciuti = await _archivio.KnownDaysAsync(from, to, ct);
        var blocchi = AirportRollupPlanner.Plan(icaos, conosciuti, from, to, max, now);
        if (blocchi.Count == 0) return new AirportRollupResult(0, 0, 0, 0);

        // ⚠️ La finestra delle aperture è quella dei GIORNI INTERI, non quella chiesta dal chiamante: il
        // piano consolida da mezzanotte a mezzanotte, e chiedere le aperture su `from..to` tagliava fuori
        // tutto quel che sta dentro l'ultimo giorno (con `from == to` restava una finestra di ampiezza
        // zero, e ogni campo risultava chiuso). Il difetto l'hanno preso i test, non lo schermo.
        var finestraDa = new DateTimeOffset(from.UtcDateTime.Date, TimeSpan.Zero);
        var finestraA = new DateTimeOffset(to.UtcDateTime.Date, TimeSpan.Zero).AddDays(1);

        // Le aperture di TUTTI i campi in un colpo: sono qualche migliaio di righe per un anno, e chiederle
        // dentro il ciclo dei blocchi vorrebbe dire una query per aeroporto per blocco.
        var aperture = await _archivio.AtcOpeningsAsync(finestraDa, finestraA, ct);
        var perCampo = aperture
            .GroupBy(a => a.Icao, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                // ⚠️ Unite: torre e terra aperte insieme sono UN'apertura, non due, o i minuti sarebbero doppi.
                g => CoverageGrid.Unione(
                    g.Select(a => new OnlineSpan(a.StartUtc, a.EndUtc)).ToList(), finestraDa, finestraA),
                StringComparer.OrdinalIgnoreCase);

        var righe = new List<AirportDayCount>();
        var movimenti = 0;

        foreach (var b in blocchi)
        {
            ct.ThrowIfCancellationRequested();

            var mov = await _sorgente.GetMovementsAsync(b.Icao, b.From, b.To, ct);
            var spans = perCampo.TryGetValue(b.Icao, out var s) ? s : Array.Empty<OnlineSpan>();

            for (var giorno = b.From; giorno < b.To; giorno = giorno.AddDays(1))
            {
                var fine = giorno.AddDays(1);
                var conto = AirportCoverage.Tally(mov, spans, giorno, fine);

                righe.Add(new AirportDayCount(
                    b.Icao, giorno, conto.Inbound, conto.Outbound, conto.Overflight,
                    conto.Covered, MinutiAperti(spans, giorno, fine)));

                movimenti += conto.Movements;
            }
        }

        var scritte = await _archivio.SaveAsync(righe, now, ct);

        return new AirportRollupResult(
            blocchi.Count, scritte, movimenti, blocchi.Select(b => b.Icao).Distinct().Count());
    }

    /// <summary>
    /// Quanti minuti di quel giorno il campo era aperto. Gli intervalli arrivano già uniti, quindi si
    /// sommano senza paura di contare due volte lo stesso minuto.
    /// </summary>
    private static int MinutiAperti(
        IReadOnlyList<OnlineSpan> spans, DateTimeOffset da, DateTimeOffset a)
    {
        var totale = 0.0;
        foreach (var s in spans)
        {
            var inizio = s.StartUtc > da ? s.StartUtc : da;
            var fine = s.EndUtc < a ? s.EndUtc : a;
            if (fine > inizio) totale += (fine - inizio).TotalMinutes;
        }

        // Il tetto è la giornata: una sessione a cavallo del bordo non può dare 1500 minuti su 1440.
        return (int)Math.Round(Math.Min(totale, (a - da).TotalMinutes));
    }
}

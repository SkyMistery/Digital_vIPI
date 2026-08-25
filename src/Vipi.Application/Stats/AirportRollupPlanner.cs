using System;
using System.Collections.Generic;
using System.Linq;

namespace Vipi.Application.Stats;

/// <summary>Un giorno già consolidato, e quando lo si è preso.</summary>
public sealed record KnownAirportDay(string Icao, DateTime Day, DateTime FetchedUtc);

/// <summary>Un blocco da chiedere alla sorgente: un aeroporto, una finestra.</summary>
public sealed record AirportRollupChunk(string Icao, DateTimeOffset From, DateTimeOffset To)
{
    /// <summary>Quanti giorni copre: serve al chiamante per spezzare la risposta.</summary>
    public int Days => (int)Math.Round((To - From).TotalDays);
}

/// <summary>
/// Che cosa chiedere alla sorgente, stanotte.
///
/// <para><b>Perché a blocchi e non giorno per giorno.</b> Una chiamata copre una finestra qualunque:
/// misurato il 25 agosto 2026 su LIRF, trenta giorni sono 981 KB e 1,3 s contro i 60 KB di un giorno solo.
/// Chiedere giorno per giorno il recupero di dodici mesi vorrebbe dire <b>34 000</b> chiamate invece di
/// ~1 100 — trenta volte tanto per lo stesso dato.</para>
///
/// <para><b>Perché il più recente prima.</b> L'arretrato si recupera in settimane; ieri interessa oggi. Con
/// l'ordine opposto, la pagina resterebbe vuota nel presente finché non è finito il passato.</para>
///
/// <para>⚠️ Un giorno preso <b>mentre era ancora in corso</b> va ripassato: <see cref="Assestamento"/> è
/// l'attesa dopo la mezzanotte prima di fidarsi di un conto. Senza, il consolidamento di oggi resterebbe
/// per sempre quello delle 14:00 e mancherebbe tutta la sera — che è quando l'Italia vola.</para>
///
/// <para>Puro e deterministico. Nessun I/O, nessun orologio interno.</para>
/// </summary>
public static class AirportRollupPlanner
{
    /// <summary>Quanto si aspetta, dopo la fine di un giorno, prima di considerarlo definitivo.</summary>
    public static readonly TimeSpan Assestamento = TimeSpan.FromHours(2);

    /// <summary>Giorni per blocco: la finestra misurata come buona su LIRF (981 KB, 1,3 s).</summary>
    public const int GiorniPerBlocco = 30;

    public static IReadOnlyList<AirportRollupChunk> Plan(
        IReadOnlyList<string> icaos,
        IReadOnlyList<KnownAirportDay> conosciuti,
        DateTimeOffset from,
        DateTimeOffset to,
        int max,
        DateTimeOffset now,
        int giorniPerBlocco = GiorniPerBlocco)
    {
        if (max <= 0 || icaos.Count == 0) return Array.Empty<AirportRollupChunk>();

        var preso = conosciuti
            .GroupBy(k => (k.Icao, k.Day.Date))
            .ToDictionary(g => g.Key, g => g.Max(k => k.FetchedUtc));

        var primo = from.UtcDateTime.Date;
        var ultimo = to.UtcDateTime.Date;

        var blocchi = new List<AirportRollupChunk>();

        // Dal giorno più recente all'indietro: il presente prima del passato.
        foreach (var icao in icaos)
        {
            var giorno = ultimo;
            while (giorno >= primo)
            {
                if (!DaRifare(preso, icao, giorno, now)) { giorno = giorno.AddDays(-1); continue; }

                // Il blocco si estende all'indietro finché i giorni servono e non si sfora la misura.
                var fine = giorno;
                var inizio = giorno;
                var quanti = 1;
                while (quanti < giorniPerBlocco &&
                       inizio.AddDays(-1) >= primo &&
                       DaRifare(preso, icao, inizio.AddDays(-1), now))
                {
                    inizio = inizio.AddDays(-1);
                    quanti++;
                }

                blocchi.Add(new AirportRollupChunk(
                    icao,
                    new DateTimeOffset(inizio, TimeSpan.Zero),
                    new DateTimeOffset(fine.AddDays(1), TimeSpan.Zero)));

                giorno = inizio.AddDays(-1);
            }
        }

        // ⚠️ L'ordinamento è GLOBALE, non per aeroporto: con un tetto di venti blocchi e novantatré
        // aeroporti, ordinare dentro ogni aeroporto vorrebbe dire spendere tutto il tetto sui primi due in
        // ordine alfabetico, e LIRF non arriverebbe mai.
        return blocchi
            .OrderByDescending(b => b.To)
            .ThenBy(b => b.Icao, StringComparer.Ordinal)
            .Take(max)
            .ToList();
    }

    /// <summary>Vero se quel giorno manca, o è stato preso prima di essersi assestato.</summary>
    private static bool DaRifare(
        IReadOnlyDictionary<(string, DateTime), DateTime> preso, string icao, DateTime giorno, DateTimeOffset now)
    {
        if (!preso.TryGetValue((icao, giorno), out var quando)) return true;

        var definitivoDa = giorno.AddDays(1) + Assestamento;

        // Un giorno preso dopo il suo assestamento non si ripassa mai più. Uno preso prima si ripassa, ma
        // solo quando l'assestamento è passato davvero: altrimenti il consolidamento di oggi rifarebbe
        // ogni notte tutti i giorni di oggi, in un giro che non finisce.
        return quando < definitivoDa && now >= definitivoDa;
    }
}

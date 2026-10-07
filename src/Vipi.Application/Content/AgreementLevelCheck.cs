using System;
using System.Collections.Generic;
using System.Linq;
using Vipi.Application.Abstractions;
using Vipi.Application.Stats;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Una clausola scritta per un ente d'area che, su quel punto e a quella quota, quel cielo <b>non lo tiene</b>:
/// lo tiene un settore più specifico, che da lui pende.
/// </summary>
/// <param name="Cop">Il punto su cui la geometria l'ha visto.</param>
/// <param name="LevelText">La quota della clausola, com'è scritta.</param>
/// <param name="Written">L'ente scritto nell'accordo.</param>
/// <param name="Holder">Il settore che lì, a quella quota, tiene il cielo quando è aperto.</param>
/// <param name="WrittenIsSender">Vero se l'ente scritto è chi cede; falso se è chi riceve.</param>
public sealed record AgreementLevelWarning(
    int AgreementId, int SectionId, int ClauseId, string Cop, string LevelText,
    string Written, string Holder, bool WrittenIsSender);

/// <summary>
/// **La geometria avvisa.** Legge gli accordi e dice quali clausole sono scritte per un ente d'area che a quella
/// quota non è il settore giusto — «ES2 ⇄ Padova a FL350», quando sopra FL325 il cielo è di ES5.
///
/// <para><b>Perché non si guarda la banda del settore scritto</b> (era la regola pensata il 4 ottobre 2026, e i
/// dati veri l'hanno smentita). In produzione ES2 e WS2 vanno da SFC a UNL: è giusto, perché con ES5 e WS5 chiusi
/// tengono davvero tutto. «FL350 è fuori dalla banda di ES2» è falso per il catalogo, e l'avviso non scatterebbe
/// mai. La domanda vera è un'altra: <b>esiste un settore più specifico che, aperto, a quella quota tiene quello
/// stesso cielo?</b> Lo dice la geometria, fra i soli settori che <b>pendono</b> da quello scritto (la catena di
/// ripiego: se chiudono, il loro cielo torna a lui).</para>
///
/// <para><b>Lo scritto comanda</b>: questo non corregge niente, e non cambia chi vede che cosa. È una voce del
/// cruscotto delle lacune, con accanto il tasto per spostare le clausole nell'accordo della coppia giusta.</para>
///
/// <para>Quando <b>tace</b>, di proposito:</para>
/// <list type="bullet">
/// <item>l'ente scritto non è d'area — per un APP un trasferimento un po' fuori dal suo spazio è la norma;</item>
/// <item>il punto non è un punto (<c>ALL</c>, un'aerovia, una STAR) o il catalogo non sa dov'è;</item>
/// <item>il punto non sta dentro nessuno dei settori che pendono dall'ente scritto: è fuori, o sul confine, e la
/// geometria non ha niente da dire;</item>
/// <item>l'ente scritto tiene quel cielo ad <b>almeno una</b> delle quote che la clausola ammette — «FL350 o
/// inferiore» vale anche sotto FL325 — o sul <b>confine esatto</b> (si guarda 50 ft sopra e 50 ft sotto);</item>
/// <item>il settore più specifico è l'altro capo dell'accordo, o pende da lui: è il trasferimento stesso.</item>
/// </list>
///
/// <para>Puro e deterministico, nessun I/O. Carta <c>docs/feature/2026-10-04-copertura-unica.md</c> §10.</para>
/// </summary>
public static class AgreementLevelCheck
{
    /// <summary>Quanto ci si scosta dalla quota scritta per non giudicare sul confine esatto, in piedi.</summary>
    private const int Margine = 50;

    /// <summary>Le due quote estreme a cui si prova un vincolo aperto: «o superiore», «o inferiore».</summary>
    private const int Alto = 60000, Basso = 1500;

    private static readonly StringComparer OIC = StringComparer.OrdinalIgnoreCase;

    public static IReadOnlyList<AgreementLevelWarning> Find(
        IReadOnlyList<AgreementRow> agreements,
        IReadOnlyList<SectorVolumeRow> settori,
        CopPositions punti,
        IReadOnlyDictionary<string, IReadOnlyList<FallbackRow>> dichiarate,
        Func<string, string?> padreDi,
        IDictionary<SectorVolumeRow, SectorVolume?>? volumi = null)
    {
        if (agreements.Count == 0 || settori.Count == 0 || punti.Count == 0) return Array.Empty<AgreementLevelWarning>();

        // Gli enti d'area di CONTROLLO: un servizio informazioni e un ente militare stanno sopra tutto il cielo di
        // tutti, e «più specifico di ES2» lo sarebbero sempre (stessa ragione di CoverageFallback).
        var area = settori
            .Where(s => s.Type == SectorType.Ctr && !CoverageFallback.Informazioni(s.Callsign) && !RipiegoMilitare.Militare(s.Callsign))
            .Select(s => s.Callsign).ToHashSet(OIC);
        if (area.Count == 0) return Array.Empty<AgreementLevelWarning>();

        // Tutti aperti: ognuno tiene il suo. È la domanda «di chi è questo cielo quando c'è», non «chi lo copre adesso».
        var claims = SectorVolumeMap.BuildClaims(settori, settori.Select(s => s.Callsign).ToHashSet(OIC), cs => cs, volumi)
            .Where(c => area.Contains(c.Volume.Callsign)).ToList();

        // Chi PENDE da un ente a una quota: lui, e ogni settore d'area la cui catena di ripiego a quella quota passa da lui.
        var domini = new Dictionary<(string, int), HashSet<string>>();
        HashSet<string> Dominio(string ente, int ft)
        {
            if (domini.TryGetValue((ente.ToUpperInvariant(), ft), out var gia)) return gia;
            var dentro = new HashSet<string>(OIC) { ente };
            foreach (var s in area)
                if (FallbackChain.Candidates(s, ft, dichiarate, padreDi).Contains(ente, OIC)) dentro.Add(s);
            return domini[(ente.ToUpperInvariant(), ft)] = dentro;
        }

        var avvisi = new List<AgreementLevelWarning>();
        foreach (var a in agreements)
            foreach (var s in a.Sections)
            {
                var (cede, riceve) = (a.Sender(s.Direction).Callsign, a.Receiver(s.Direction).Callsign);
                foreach (var c in s.Clauses)
                {
                    var (quote, testo) = Quote(c);
                    if (quote.Count == 0) continue;

                    foreach (var (scritto, altro, cedente) in new[] { (cede, riceve, true), (riceve, cede, false) })
                    {
                        if (!area.Contains(scritto)) continue;

                        foreach (var cop in CopList.Parse(c.Cops).Distinct(OIC))
                        {
                            if (!NavaidCheck.IsCheckable(cop) || !punti.TryGet(cop, out var punto)) continue;

                            string? piuSpecifico = null;
                            var tieneLui = false;
                            foreach (var ft in quote)
                            {
                                var suoi = Dominio(scritto, ft);
                                var dellAltro = Dominio(altro, ft);
                                var candidati = claims
                                    .Where(k => suoi.Contains(k.Volume.Callsign) && !dellAltro.Contains(k.Volume.Callsign))
                                    .ToList();
                                var vince = TrafficAttribution.AttributeClaim(candidati, punto.Lat, punto.Lon, ft, FlightPhase.Airborne)
                                    ?.Volume.Callsign;
                                if (vince is null) continue;
                                if (OIC.Equals(vince, scritto)) { tieneLui = true; break; }
                                piuSpecifico ??= vince;   // la prima quota è quella più vicina a ciò che è scritto
                            }

                            if (!tieneLui && piuSpecifico is not null)
                                avvisi.Add(new AgreementLevelWarning(a.Id, s.Id, c.Id, cop, testo, scritto, piuSpecifico, cedente));
                        }
                    }
                }
            }
        return avvisi;
    }

    /// <summary>
    /// Le quote a cui provare la clausola, in piedi, dalla più vicina a ciò che è scritto. Vuoto = la clausola non
    /// ha una quota su cui ragionare (livello speciale, o nessuno).
    /// <para>⚠️ Si guarda la quota a cui il traffico <b>passa di mano</b>: quella della faccetta trasferimento se
    /// c'è, altrimenti il livello autorizzato — come <see cref="FallbackChain.HandoffFeetOf"/>.</para>
    /// </summary>
    private static (IReadOnlyList<int> Quote, string Testo) Quote(AgreementClauseRow c)
    {
        var faccetta = c.HasHandoff && c.HandoffLevelValue is not null;
        var vincolo = faccetta ? c.HandoffLevelConstraint : c.LevelConstraint;
        var ft = faccetta ? FallbackChain.FeetOf(c.HandoffLevelValue, c.HandoffLevelUnit) : FallbackChain.FeetOf(c.LevelValue, c.LevelUnit);
        var testo = faccetta ? c.HandoffLevelText : c.LevelText;
        if (ft is not int quota) return (Array.Empty<int>(), testo);

        IReadOnlyList<int> quote = vincolo switch
        {
            // Mai sul confine esatto: un passo sotto e uno sopra. Se l'ente scritto tiene da una delle due parti, basta.
            LevelConstraint.Exact => new[] { quota - Margine, quota + Margine },
            LevelConstraint.AtOrAbove => new[] { quota + Margine, Alto },
            LevelConstraint.AtOrBelow => new[] { quota - Margine, Basso },
            _ => Array.Empty<int>(),
        };
        return (quote.Where(q => q > 0).ToList(), testo);
    }
}

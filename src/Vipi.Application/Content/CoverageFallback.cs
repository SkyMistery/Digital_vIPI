using System;
using System.Collections.Generic;
using System.Linq;
using Vipi.Application.Abstractions;
using Vipi.Application.Stats;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Perché un rinvio ha dato — o non ha dato — una risposta. È la frase da mostrare, non un codice
/// d'errore: un rinvio che tace e uno che non sa dove sia il punto sono due cose diverse per chi legge.</summary>
public enum CoverageFallbackOutcome
{
    /// <summary>Ha risposto: <see cref="CoverageFallbackResult.TargetCallsign"/> è chi copre quel punto.</summary>
    Resolved,

    /// <summary>Il CoP <b>non è un punto</b>: <c>Y01-Y12</c>, <c>ALL</c>, <c>TOPNO 3A</c>. Non è un dato che
    /// manca — è una domanda che non si può porre, e la risposta va <b>scritta</b>.</summary>
    NotAPoint,

    /// <summary>Ha la forma di un punto ma nessun catalogo lo colloca. Si apre a mano una coordinata in
    /// anagrafica, oppure si scrive il ripiego.</summary>
    PointUnknown,

    /// <summary>La riga non porta una quota al trasferimento («as coordinated»): un volume non si può
    /// interrogare senza dire a che altezza.</summary>
    NoLevel,

    /// <summary>Nessun settore ammesso copre quel punto a quella quota: la catena prosegue sul padre.</summary>
    NobodyCovers,
}

/// <summary>L'esito di un rinvio: chi raccoglie, e se non raccoglie nessuno, perché.</summary>
public readonly record struct CoverageFallbackResult(CoverageFallbackOutcome Outcome, string? TargetCallsign)
{
    public static CoverageFallbackResult No(CoverageFallbackOutcome perche) => new(perche, null);

    /// <summary>Come lo vuole <see cref="FallbackChain.Candidates"/>: zero o un candidato.</summary>
    public IReadOnlyList<string> AsCandidates() =>
        TargetCallsign is { Length: > 0 } cs ? new[] { cs } : Array.Empty<string>();
}

/// <summary>
/// «Chi copre <b>questo punto</b>, a <b>questa quota</b>, adesso»: la risposta che un
/// <see cref="FallbackTargetKind.Coverage"/> va a cercare.
///
/// <para><b>Non è un secondo albero.</b> Le pretese gliele passa
/// <see cref="SectorVolumeMap.BuildClaims"/>, che collassa i settori chiusi sull'albero di copertura; la
/// scelta fra i candidati la fa <see cref="TrafficAttribution.AttributeClaim"/>, che ordina per
/// <b>profondità nell'albero</b> prima che per geometria. La geometria decide soltanto <b>in quale ramo</b>
/// ci si trova — che è precisamente il pezzo che un <c>ParentCallsign</c> singolo non sa dire.</para>
///
/// <para>Puro e deterministico, nessun I/O. Carta
/// <c>docs/feature/2026-09-10-rinvio-geometrico.md</c> §4.</para>
/// </summary>
public static class CoverageFallback
{
    /// <summary>
    /// Quanto è «generale» una posizione. Serve al filtro di rango, e l'ordine è quello top-down di sempre
    /// (DEL→GND→TWR→APP→CTR): più alto = più generale.
    /// </summary>
    /// <remarks>⚠️ Un <b>FSS</b> non ha un valore suo in <see cref="SectorType"/>: nella proiezione è tipato
    /// <see cref="SectorType.Ctr"/> (misurato: <c>LIMM_FSS</c> → <c>Ctr</c> nella tabella <c>Sectors</c>).
    /// È il comportamento voluto — un FSS è un ente d'area — ma va saputo, perché il rango non lo distingue.</remarks>
    public static int Rango(SectorType tipo) => tipo switch
    {
        SectorType.Del => 0,
        SectorType.Gnd => 1,
        SectorType.Twr or SectorType.ITwr => 2,
        SectorType.App => 3,
        _ => 4,
    };

    /// <summary>
    /// Chi raccoglie il traffico di <paramref name="cop"/> quando il ricevente nominale è chiuso.
    /// </summary>
    /// <param name="cop">Il punto di trasferimento, come è scritto nella clausola.</param>
    /// <param name="levelFeet">La quota <b>al trasferimento</b>, in piedi. Null ⇒ non si risponde.</param>
    /// <param name="posizioni">Dove stanno i punti.</param>
    /// <param name="claims">Le pretese di adesso, già collassate con la <b>catena</b> (non coi soli padri).</param>
    /// <param name="tipoRicevente">Il tipo del ricevente nominale: dà la soglia del filtro di rango.</param>
    /// <param name="accRicevente">L'ACC del ricevente nominale: a pari specificità, si preferisce il suo.</param>
    /// <param name="fuoriGioco">
    /// Chi non può raccogliere: il <b>cedente e il suo dominio</b>. ⚠️ Un trasferimento «al confine dell'AoR»
    /// avviene <i>dentro</i> il settore che cede — il CoP di Ghedi sta dentro il poligono di
    /// <c>LIPX_ES0_APP</c> — e chi consegna non può essere chi raccoglie.
    /// </param>
    /// <param name="accDi">callsign → codice ACC, per lo spareggio «stesso centro».</param>
    public static CoverageFallbackResult Resolve(
        string? cop,
        int? levelFeet,
        CopPositions posizioni,
        IReadOnlyList<SectorClaim> claims,
        SectorType tipoRicevente,
        string? accRicevente,
        IReadOnlySet<string> fuoriGioco,
        Func<string, string?> accDi)
    {
        // ⚠️ Prima «che cosa è questo token», poi «che quota ha»: sono due frasi diverse per chi legge, e la
        // prima è quella che dice all'admin che non c'è niente da aggiustare.
        if (!NavaidCheck.IsCheckable(cop)) return CoverageFallbackResult.No(CoverageFallbackOutcome.NotAPoint);
        if (!posizioni.TryGet(cop, out var punto)) return CoverageFallbackResult.No(CoverageFallbackOutcome.PointUnknown);
        if (levelFeet is not int ft) return CoverageFallbackResult.No(CoverageFallbackOutcome.NoLevel);

        var soglia = Rango(tipoRicevente);
        var ammessi = claims.Where(c =>
            Rango(c.Type) >= soglia
            && !fuoriGioco.Contains(c.Volume.Callsign)
            && !fuoriGioco.Contains(c.SessionCallsign)).ToList();

        if (ammessi.Count == 0) return CoverageFallbackResult.No(CoverageFallbackOutcome.NobodyCovers);

        // Lo spareggio «stesso centro» si fa in DUE passate invece che dentro il confronto: così il
        // comparatore di TrafficAttribution resta uno solo, e le statistiche non ereditano una preferenza
        // che riguarda i trasferimenti e non loro.
        SectorClaim? vinta = null;
        if (accRicevente is { Length: > 0 })
        {
            var dentro = ammessi
                .Where(c => string.Equals(accDi(c.Volume.Callsign), accRicevente, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (dentro.Count > 0)
                vinta = TrafficAttribution.AttributeClaim(dentro, punto.Lat, punto.Lon, ft, FlightPhase.Airborne);
        }

        vinta ??= TrafficAttribution.AttributeClaim(ammessi, punto.Lat, punto.Lon, ft, FlightPhase.Airborne);

        return vinta is null
            ? CoverageFallbackResult.No(CoverageFallbackOutcome.NobodyCovers)
            : new CoverageFallbackResult(CoverageFallbackOutcome.Resolved, vinta.Value.SessionCallsign);
    }
}

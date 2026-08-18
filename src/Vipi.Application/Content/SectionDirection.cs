using System;
using System.Collections.Generic;
using System.Linq;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Cosa si sa di un capo dell'accordo, per capire se «possiede» un aeroporto.</summary>
/// <param name="Callsign">Il callsign del settore.</param>
/// <param name="AccCode">La ACC del settore; null se ignota.</param>
public sealed record AgreementSideHint(string Callsign, string? AccCode);

/// <summary>Cosa si sa di un aeroporto, per capire chi lo copre.</summary>
/// <param name="CoverageChain">La catena di copertura, dal padre immediato in su (<c>Airport.ParentCallsign</c>
/// e i suoi antenati). Vuota se l'aeroporto non è ancora collocato nell'albero.</param>
/// <param name="AccCode">La ACC di competenza dell'aeroporto; null se ignota.</param>
public sealed record AirportCoverageHint(IReadOnlyList<string> CoverageChain, string? AccCode);

/// <summary>
/// **Il verso proposto per una sezione.**
///
/// <para>Una sezione «arrivi verso LIRF» ha un verso solo: cede chi non ha LIRF, riceve chi ce l'ha. Il verso
/// non si <b>ricalcola</b> a ogni lettura — l'AoR cambia, l'accordo scritto no — ma quando la sezione nasce
/// arriva già proposto, e chi scrive lo corregge con un clic se la proposta sbaglia.</para>
///
/// <para><b>Provata contro l'archivio prima di scriverla:</b> sulla coppia <c>LIBB_ES_CTR ⇄ LGGG_W_CTR</c> la
/// regola dà <c>A→B</c> per gli arrivi a LGKF (greco) e <c>B→A</c> per gli arrivi a LIBD (italiano), cioè
/// esattamente i versi con cui quei due accordi erano scritti a mano prima della conversione.</para>
///
/// <para>Funzione pura: «di chi è questo aeroporto» è un <b>giudizio</b>, e un giudizio va potuto provare e
/// smentire senza un database.</para>
/// </summary>
public static class SectionDirection
{
    /// <summary>
    /// Quale lato «possiede» l'aeroporto, o <c>null</c> se non si riesce a dirlo (nessuno dei due, o entrambi).
    /// <para>Due criteri in ordine: la <b>catena di copertura</b> dell'aeroporto passa per un lato; altrimenti la
    /// <b>ACC</b> dell'aeroporto è quella di un lato. Il secondo è più grosso del primo e serve agli accordi
    /// ACC↔ACC, dove nessuno dei due capi copre lo scalo direttamente.</para>
    /// </summary>
    public static AgreementSide? OwningSide(AgreementSideHint a, AgreementSideHint b, AirportCoverageHint airport)
    {
        var chain = airport.CoverageChain;
        var byChain = Pick(chain.Contains(a.Callsign, StringComparer.OrdinalIgnoreCase),
                           chain.Contains(b.Callsign, StringComparer.OrdinalIgnoreCase));
        if (byChain is not null) return byChain;

        if (string.IsNullOrWhiteSpace(airport.AccCode)) return null;
        return Pick(Same(a.AccCode, airport.AccCode), Same(b.AccCode, airport.AccCode));
    }

    /// <summary>
    /// Il verso proposto: gli <b>arrivi</b> vanno verso chi possiede lo scalo, le <b>partenze</b> vengono da lui.
    /// Senza un proprietario riconoscibile — o per un traffico che con lo scalo non c'entra — si propone
    /// <c>A→B</c>, che è il verso in cui l'accordo si legge di default.
    /// </summary>
    public static AgreementDirection Propose(TransferFlowKind kind, AgreementSide? owner) => (kind, owner) switch
    {
        (TransferFlowKind.Arrival, AgreementSide.A) => AgreementDirection.BtoA,
        (TransferFlowKind.Arrival, AgreementSide.B) => AgreementDirection.AtoB,
        (TransferFlowKind.Departure, AgreementSide.A) => AgreementDirection.AtoB,
        (TransferFlowKind.Departure, AgreementSide.B) => AgreementDirection.BtoA,
        _ => AgreementDirection.AtoB,
    };

    /// <summary>Il verso proposto per una sezione dell'accordo, dal primo dei suoi aeroporti: con più scali
    /// nello stesso gruppo il verso è comunque uno, e il primo è quello sotto cui il gruppo si legge.</summary>
    public static AgreementDirection Propose(TransferFlowKind kind, AgreementSideHint a, AgreementSideHint b,
        AirportCoverageHint? firstAirport) =>
        firstAirport is null ? Propose(kind, (AgreementSide?)null) : Propose(kind, OwningSide(a, b, firstAirport));

    /// <summary>Il verso opposto.</summary>
    public static AgreementDirection Flip(AgreementDirection d) =>
        d == AgreementDirection.AtoB ? AgreementDirection.BtoA : AgreementDirection.AtoB;

    /// <summary>Un lato solo, o niente: «tutti e due» non è una risposta più utile di «nessuno».</summary>
    private static AgreementSide? Pick(bool a, bool b) =>
        a && !b ? AgreementSide.A : b && !a ? AgreementSide.B : null;

    private static bool Same(string? x, string? y) =>
        !string.IsNullOrWhiteSpace(x) && string.Equals(x, y, StringComparison.OrdinalIgnoreCase);
}

using System;
using System.Collections.Generic;
using System.Linq;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Da che parte sta «noi» in un accordo, guardandolo da una ACC.
/// </summary>
/// <param name="NearSide">Il lato che appartiene alla ACC che sta guardando.</param>
/// <param name="IsInternal">Entrambi i lati sono in casa (ACC ↔ un suo avvicinamento): non c'è un «loro».</param>
/// <param name="IsDetached">Nessun lato è in casa — l'accordo è visibile solo perché la ACC ne è responsabile.</param>
public sealed record AgreementOrientation(AgreementSide NearSide, bool IsInternal, bool IsDetached)
{
    public AgreementSide FarSide => NearSide == AgreementSide.A ? AgreementSide.B : AgreementSide.A;

    /// <summary>Il verso «noi → loro»: quello in cui il traffico esce da casa.</summary>
    public AgreementDirection Outbound =>
        NearSide == AgreementSide.A ? AgreementDirection.AtoB : AgreementDirection.BtoA;

    /// <summary>Il verso «loro → noi».</summary>
    public AgreementDirection Inbound =>
        NearSide == AgreementSide.A ? AgreementDirection.BtoA : AgreementDirection.AtoB;
}

/// <summary>
/// **La lente con cui una ACC guarda i propri accordi.**
///
/// <para>Un accordo non ha un verso «giusto»: <c>LIBB_ES_CTR → LDZO_CTR</c> e <c>LDZO_CTR → LIBB_ES_CTR</c> sono
/// lo stesso confine letto dai due capi, e quale dei due sia il lato A dipende solo da chi l'ha scritto per
/// primo. La visibilità passa già dalle <b>parti</b> («gli accordi che hanno una parte fra i miei settori»,
/// <see cref="IAgreementService.ListByAccAsync"/>), ma la vista non sapeva dire «noi» e «loro»: indicizzava sul
/// lato B, e per i 13 accordi di LIBB — 10 su 11 di LIRR — in cui la ACC <i>è</i> il lato B, l'albero si
/// chiamava col nome dei nostri stessi settori.</para>
///
/// <para>⚠️ <b>L'orientamento è una lente, non un dato: A e B in archivio non si toccano.</b> Scambiarli
/// cambierebbe di significato le clausole di <b>entrambi</b> i versi e le release già congelate, e non
/// esisterebbe comunque un verso giusto per un accordo di confine — dipende da chi lo apre.</para>
///
/// <para>Si costruisce una volta per caricamento e porta il proprio indice: l'albero la interroga per ogni
/// accordo e per ogni ente, e una ricerca lineare per parte costerebbe un giro completo dei settori a ogni
/// render.</para>
/// </summary>
public sealed class AgreementViewpoint
{
    private readonly string _accCode;
    private readonly Dictionary<string, string> _accByCallsign;

    /// <param name="accCode">La ACC che sta guardando.</param>
    /// <param name="sectors">I settori conosciuti, per sapere di chi è un callsign. Cross-ACC: senza gli enti
    /// esteri e quelli delle altre ACC italiane la lente non saprebbe dire che <c>LDZO_CTR</c> non è di casa.</param>
    public AgreementViewpoint(string accCode, IReadOnlyList<SuggestionSector> sectors)
    {
        _accCode = accCode ?? "";
        _accByCallsign = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in sectors)
            _accByCallsign[s.Callsign] = s.AccCode;
    }

    /// <summary>La ACC di un ente, o <c>null</c> se il callsign non è in catalogo (accordo scritto verso un ente
    /// che nel frattempo è sparito: si mostra, non si nasconde).</summary>
    public string? AccOf(string callsign) =>
        _accByCallsign.TryGetValue(callsign ?? "", out var acc) ? acc : null;

    private bool IsHome(string callsign) =>
        string.Equals(AccOf(callsign), _accCode, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Come si legge questo accordo da qui.
    /// <para>Un accordo <b>interno</b> (area ↔ un proprio avvicinamento: tre in archivio) non ha un «loro»: la
    /// convenzione è che «noi» sia il lato A, dichiarata perché la testata la annunci invece di lasciarla
    /// indovinare.</para>
    /// </summary>
    public AgreementOrientation Orient(AgreementRow a)
    {
        var homeA = a.Parties.Any(p => p.Side == AgreementSide.A && IsHome(p.Callsign));
        var homeB = a.Parties.Any(p => p.Side == AgreementSide.B && IsHome(p.Callsign));

        if (homeA && homeB) return new AgreementOrientation(AgreementSide.A, IsInternal: true, IsDetached: false);
        if (homeB) return new AgreementOrientation(AgreementSide.B, IsInternal: false, IsDetached: false);
        // Nessun lato in casa: la ACC lo vede perché ne è responsabile. Resta il lato A, che è come è scritto.
        return new AgreementOrientation(AgreementSide.A, IsInternal: false, IsDetached: !homeA);
    }

    /// <summary>Gli enti del lato di casa, nell'ordine scritto.</summary>
    public IReadOnlyList<string> Near(AgreementRow a) => PartiesOn(a, Orient(a).NearSide);

    /// <summary>Gli enti della controparte, nell'ordine scritto. Vuoto = il traffico finisce a UNICOM.</summary>
    public IReadOnlyList<string> Far(AgreementRow a) => PartiesOn(a, Orient(a).FarSide);

    /// <summary>
    /// La ACC della controparte: <b>primo livello dell'albero</b>, perché «l'accordo con Roma» è il modo in cui
    /// un accordo viene in mente. <c>null</c> quando non c'è controparte (a UNICOM) o quando è fuori catalogo.
    /// </summary>
    /// <remarks>Se la controparte ha enti di ACC diverse (un lato può portare più enti) vince quella del primo
    /// ente scritto: l'ordine delle parti è deciso da chi ha scritto l'accordo, ed è l'unico criterio che non
    /// inventa una gerarchia fra due centri.</remarks>
    public string? FarAccCode(AgreementRow a) =>
        Far(a).Select(AccOf).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

    /// <summary>Il verso di una clausola letto da qui: vero se il traffico esce di casa.</summary>
    public bool IsOutbound(AgreementRow a, AgreementClauseRow c) => c.Direction == Orient(a).Outbound;

    private static IReadOnlyList<string> PartiesOn(AgreementRow a, AgreementSide side) =>
        a.Parties.Where(p => p.Side == side).OrderBy(p => p.Order).Select(p => p.Callsign).ToList();
}

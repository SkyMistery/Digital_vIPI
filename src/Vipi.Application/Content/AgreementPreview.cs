using System.Collections.Generic;
using System.Linq;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// La frase che il documento renderà per una clausola, composta in locale dall'editor.
///
/// <para><b>Il PRIMO caso, non tutti.</b> Una clausola con tre punti su quattro aeroporti produce dodici righe
/// nel documento: mostrarle tutte in anteprima riempirebbe il riquadro di frasi che differiscono per una parola.
/// L'anteprima serve a rispondere a «come suonerà», e per quello basta la prima — le altre sono la stessa frase
/// con un altro nome dentro.</para>
///
/// <para>Passa dagli stessi attrezzi della derivazione reale (<see cref="CoordinationPreviewContext"/>,
/// <see cref="Outline"/>): l'anteprima che si compone da sé è l'anteprima che prima o poi mente.</para>
/// </summary>
public static class AgreementPreview
{
    /// <summary>La frase per una clausola, o <c>null</c> se i dati non bastano a farne una (un arrivo o una
    /// partenza senza aeroporto) — esattamente come la derivazione reale, che in quel caso non rende la riga.
    /// I due capi ci sono sempre: sono lo schema.</summary>
    public static string? Compose(CoordinationPreviewContext ctx, AgreementRow agreement,
        AgreementSectionRow section, AgreementClauseRow clause)
    {
        var sender = agreement.Sender(section.Direction);
        var receiver = agreement.Receiver(section.Direction);

        var airport = section.Airports.OrderBy(x => x.Order).FirstOrDefault();
        var cop = CopList.Parse(clause.Cops)[0];

        // La catena si legge sulle clausole della STESSA SEZIONE: quelle di un'altra non sono antenati, sono
        // un'altra tabella.
        var siblings = section.Clauses.OrderBy(c => c.Order).ToList();
        var chain = Outline.ConditionChain(siblings, clause,
            x => new ConditionClause(x.ConditionLabel, x.ConditionAreaLabel, x.ConditionCustomLabel));

        return ctx.Compose(
            sender.Callsign, receiver.Callsign, airport?.Icao, section.Kind,
            clause.LevelConstraint, clause.LevelValue, clause.LevelUnit, clause.LevelSpecial,
            clause.Parity, clause.VerticalState, cop, chain, Facet(clause));
    }

    /// <summary>La faccetta trasferimento della clausola, nella forma che il composer si aspetta.</summary>
    public static TransferHandoffFacet Facet(AgreementClauseRow c) => new(
        c.HandoffKind, c.HandoffLabel, c.HandoffLevelValue, c.HandoffLevelUnit, c.HandoffLevelConstraint,
        c.CommsHandoffKind, c.CommsHandoffLabel, c.SpeedValue, c.SpeedConstraint, c.IsGroupWide);
}

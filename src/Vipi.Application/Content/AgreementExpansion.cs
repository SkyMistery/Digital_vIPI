using System;
using System.Collections.Generic;
using System.Linq;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Proietta le **sezioni** degli accordi nelle righe piatte di sempre (<see cref="TransferFlowRow"/> +
/// <see cref="TransferPointRow"/>).
///
/// <para><b>Perché esiste.</b> Cinque consumatori leggono quelle righe — la derivazione dei coordinamenti
/// (vIPI ACC/APP e vLOA), il composer delle frasi, la vista live, il matcher Aurora, la stampa — e per tutti e
/// cinque la lettura giusta è quella piatta: due varianti SONO due candidati distinti, due punti sono due righe
/// di tabella. Riscriverli sull'accordo significherebbe portare in cinque posti una struttura che serve solo a
/// chi scrive.</para>
///
/// <para>È lo stesso schema già stabilito per i settori: cataloghi = fonte unica, <c>Sector</c> = proiezione.
/// L'accordo è la fonte, queste righe sono la proiezione.</para>
///
/// <para><b>Una sezione = una tabella = un verso.</b> Dal 18 agosto 2026 il tipo di traffico, gli aeroporti e il
/// verso stanno sulla sezione, e i due capi sono <b>uno per lato</b>: il prodotto cartesiano
/// «mittenti × riceventi» che c'era qui non esiste più, e con lui il caso — mai visto in archivio — di un
/// accordo che diceva «chiunque di questi cede a chiunque di quelli».</para>
///
/// <para><b>Gli id sono della passata, non del dato.</b> Nessun consumatore li persiste: servono a
/// <c>CoordTable</c> per tenere insieme le varianti di uno stesso blocco — chiave <c>(FlowId, VariantGroup)</c> —
/// e sono progressivi e deterministici dentro una sola espansione. Chi volesse identificare una clausola per
/// scriverci deve passare dall'accordo, non da qui.</para>
/// </summary>
public static class AgreementExpansion
{
    /// <summary>
    /// Le righe piatte di un insieme di accordi: una riga-flusso per ogni (sezione × aeroporto), un punto per
    /// ogni (clausola × punto dell'elenco).
    /// </summary>
    public static IReadOnlyList<TransferFlowRow> Expand(IReadOnlyList<AgreementRow> agreements)
    {
        var flows = new List<TransferFlowRow>();
        var nextFlowId = 1;
        var nextPointId = 1;

        foreach (var a in agreements.OrderBy(x => x.Order).ThenBy(x => x.Id))
            foreach (var section in a.Sections.OrderBy(s => s.Order).ThenBy(s => s.Id))
            {
                if (section.Clauses.Count == 0) continue;

                var sender = a.Sender(section.Direction);
                var receiver = a.Receiver(section.Direction);

                // Nessun aeroporto = una sola «colonna» senza aeroporto: è il caso dei sorvoli, dove la
                // relazione con lo scalo non esiste proprio (e la frase usa la forma neutra).
                var airports = section.Airports.Count > 0
                    ? section.Airports.OrderBy(x => x.Order).Select(x => ((string?)x.Icao, x.Name)).ToList()
                    : new List<(string? Icao, string? Name)> { (null, null) };

                // L'elenco degli scali si porta sulle righe solo quando sono PIU' D'UNO: con uno solo
                // ripeterebbe il nodo sotto cui la riga si legge già.
                var airportLabel = section.Airports.Count > 1 ? section.AirportsLabel : null;

                var clauses = section.Clauses.OrderBy(c => c.Order).ThenBy(c => c.Id).ToList();

                foreach (var (icao, name) in airports)
                {
                    var points = new List<TransferPointRow>();
                    var order = 1;

                    foreach (var c in clauses)
                        foreach (var cop in CopList.Parse(c.Cops))
                            points.Add(Point(nextPointId++, order++, c, cop, receiver, airportLabel));

                    flows.Add(new TransferFlowRow
                    {
                        Id = nextFlowId++,
                        AccCode = a.OwnerAccCode,
                        OwningSectorId = sender.SectorId,
                        OwningSectorCallsign = sender.Callsign,
                        Kind = section.Kind,
                        AirportIcao = icao,
                        AirportName = name,
                        Description = section.Description,
                        Order = a.Order,
                        Points = points,
                    });
                }
            }

        return flows;
    }

    private static TransferPointRow Point(int id, int order, AgreementClauseRow c, string cop,
        AgreementEndpoint receiver, string? airportLabel) =>
        new()
        {
            Id = id,
            Cop = cop,
            ClauseId = c.Id,
            Cops = c.Cops,
            AgreementAirports = airportLabel,
            LevelValue = c.LevelValue,
            LevelUnit = c.LevelUnit,
            LevelConstraint = c.LevelConstraint,
            LevelSpecial = c.LevelSpecial,
            Parity = c.Parity,
            VerticalState = c.VerticalState,
            // Stessa formattazione del repository di prima: il testo del livello è il display, e vive in un
            // posto solo perché tabella, frase e vista live lo confrontano a occhio.
            LevelText = LevelFormatting.Format(c.LevelValue, c.LevelUnit, c.LevelConstraint, c.LevelSpecial,
                                               c.Parity, c.VerticalState),
            NextSectorId = receiver.SectorId,
            NextSectorCallsign = receiver.Callsign,
            ConditionLabel = c.ConditionLabel,
            ConditionRefId = c.ConditionRefId,
            ConditionAreaLabel = c.ConditionAreaLabel,
            ConditionCustomLabel = c.ConditionCustomLabel,
            HandoffKind = c.HandoffKind,
            HandoffLabel = c.HandoffLabel,
            HandoffLevelValue = c.HandoffLevelValue,
            HandoffLevelUnit = c.HandoffLevelUnit,
            HandoffLevelConstraint = c.HandoffLevelConstraint,
            CommsHandoffKind = c.CommsHandoffKind,
            CommsHandoffLabel = c.CommsHandoffLabel,
            SpeedValue = c.SpeedValue,
            SpeedConstraint = c.SpeedConstraint,
            VariantGroup = c.VariantGroup,
            VariantDepth = c.VariantDepth,
            IsGroupWide = c.IsGroupWide,
            Order = order,
        };
}

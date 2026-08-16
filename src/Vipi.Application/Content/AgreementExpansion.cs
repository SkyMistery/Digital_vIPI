using System;
using System.Collections.Generic;
using System.Linq;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Proietta gli **accordi** nelle righe piatte di sempre (<see cref="TransferFlowRow"/> +
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
/// <para><b>Gli id sono della passata, non del dato.</b> Nessun consumatore li persiste: servono a
/// <c>CoordTable</c> per tenere insieme le varianti di uno stesso blocco — chiave <c>(FlowId, VariantGroup)</c> —
/// e sono progressivi e deterministici dentro una sola espansione. Chi volesse identificare una clausola per
/// scriverci deve passare dall'accordo, non da qui.</para>
/// </summary>
public static class AgreementExpansion
{
    /// <summary>
    /// Le righe piatte di un insieme di accordi. Una riga-flusso per ogni combinazione
    /// (mittente × aeroporto × direzione), un punto per ogni (clausola × punto dell'elenco × ricevente).
    ///
    /// <para>Il prodotto fra i due lati è voluto e non è una svista: un accordo con più enti su entrambi i lati
    /// dice davvero «chiunque di questi cede a chiunque di quelli». Nella pratica un lato è quasi sempre uno
    /// solo, e l'editor avvisa quando non lo è.</para>
    /// </summary>
    public static IReadOnlyList<TransferFlowRow> Expand(IReadOnlyList<AgreementRow> agreements)
    {
        var flows = new List<TransferFlowRow>();
        var nextFlowId = 1;
        var nextPointId = 1;

        foreach (var a in agreements.OrderBy(x => x.Order).ThenBy(x => x.Id))
        {
            var sideA = Parties(a, AgreementSide.A);
            var sideB = Parties(a, AgreementSide.B);

            // Nessun aeroporto = una sola «colonna» senza aeroporto: è il caso dei sorvoli, dove la relazione
            // con lo scalo non esiste proprio (e la frase usa la forma neutra).
            var airports = a.Airports.Count > 0
                ? a.Airports.OrderBy(x => x.Order).Select(x => ((string?)x.Icao, x.Name)).ToList()
                : new List<(string? Icao, string? Name)> { (null, null) };

            // L'elenco degli scali si porta sulle righe solo quando sono PIU' D'UNO: con uno solo ripeterebbe
            // il nodo sotto cui la riga si legge gia'.
            var airportLabel = a.Airports.Count > 1
                ? string.Join(" · ", a.Airports.OrderBy(x => x.Order).Select(x => x.Icao))
                : null;

            foreach (var direction in Directions(a))
            {
                var senders = direction == AgreementDirection.AtoB ? sideA : sideB;
                var receivers = direction == AgreementDirection.AtoB ? sideB : sideA;
                var clauses = a.Clauses.Where(c => c.Direction == direction)
                    .OrderBy(c => c.Order).ThenBy(c => c.Id).ToList();
                if (clauses.Count == 0) continue;

                // Senza nessuno che ceda non c'è flusso da rendere: l'accordo è scritto a metà, e lo dice
                // l'editor. Senza nessuno che riceva il flusso c'è invece eccome — le sue righe finiscono a
                // UNICOM, ed è esattamente ciò che il filtro «senza ricevente» deve poter trovare.
                foreach (var sender in senders)
                    foreach (var (icao, name) in airports)
                    {
                        var points = new List<TransferPointRow>();
                        var order = 1;

                        foreach (var c in clauses)
                            foreach (var cop in CopList.Parse(c.Cops))
                            {
                                if (receivers.Count == 0)
                                {
                                    points.Add(Point(nextPointId++, order++, c, cop, null, airportLabel));
                                    continue;
                                }
                                foreach (var r in receivers)
                                    points.Add(Point(nextPointId++, order++, c, cop, r, airportLabel));
                            }

                        flows.Add(new TransferFlowRow
                        {
                            Id = nextFlowId++,
                            AccCode = a.OwnerAccCode,
                            OwningSectorId = sender.SectorId,
                            OwningSectorCallsign = sender.Callsign,
                            Kind = a.TrafficKind,
                            AirportIcao = icao,
                            AirportName = name,
                            Description = a.Description,
                            Order = a.Order,
                            Points = points,
                        });
                    }
            }
        }

        return flows;
    }

    private static List<AgreementPartyRow> Parties(AgreementRow a, AgreementSide side) =>
        a.Parties.Where(p => p.Side == side).OrderBy(p => p.Order).ThenBy(p => p.SectorId).ToList();

    /// <summary>Le direzioni che l'accordo dice davvero, nell'ordine canonico. «È bilaterale» non è un flag: è
    /// avere clausole nei due versi.</summary>
    private static IEnumerable<AgreementDirection> Directions(AgreementRow a)
    {
        if (a.Clauses.Any(c => c.Direction == AgreementDirection.AtoB)) yield return AgreementDirection.AtoB;
        if (a.Clauses.Any(c => c.Direction == AgreementDirection.BtoA)) yield return AgreementDirection.BtoA;
    }

    private static TransferPointRow Point(int id, int order, AgreementClauseRow c, string cop,
        AgreementPartyRow? receiver, string? airportLabel) =>
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
            NextSectorId = receiver?.SectorId,
            NextSectorCallsign = receiver?.Callsign,
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

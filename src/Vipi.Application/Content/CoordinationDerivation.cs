using System;
using System.Collections.Generic;
using System.Linq;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Una riga di coordinamento risolta, taggata per il raggruppamento dei consumatori.
/// <para><see cref="OurSectorCallsign"/> = il settore del blocco/dominio; <see cref="CounterpartCallsign"/> =
/// l'altro capo (a chi cediamo, o il CTR vicino che ci consegna). <see cref="CounterpartType"/> serve all'APP
/// per bucketizzare (verso ACC / verso torri); l'ACC risolve l'etichetta ACC dal counterpart.</para></summary>
public sealed record CoordinationEntry(
    string OurSectorCallsign,
    string CounterpartCallsign,
    SectorType CounterpartType,
    string? AirportIcao,
    TransferFlowKind Kind,
    bool IsIncoming,
    AppCoordRow Row);

/// <summary>Cuore deterministico della derivazione coordinamenti condiviso da <see cref="AccDerivationService"/>
/// e <see cref="AppDocumentService"/> (funzione pura). Regola di direzione unica: il proprietario del flusso
/// detiene il traffico e lo cede — mittente = owner, destinatario = next — come la pagina trasferimenti, senza
/// inversioni. Due passi: (1) flussi POSSEDUTI dai settori del blocco/dominio (qualsiasi Next, qualsiasi tipo);
/// (2) arrivi ENTRANTI che un CTR vicino consegna a un settore del blocco/dominio. Il grouping resta ai
/// consumatori (l'ACC per Settore→ACC→Aeroporto/Sorvoli, l'APP per verso-ACC/verso-torri/sorvoli).</summary>
public static class CoordinationDerivation
{
    /// <summary>Mappa ICAO→nome effettiva per la derivazione: il catalogo DB vince, i nomi salvati sui flussi
    /// (aeroporti fuori DB, nuovi/esteri) riempiono i buchi. Così la frase e le etichette mostrano «Nome ICAO»
    /// anche per gli aeroporti non a catalogo. Un solo punto di fusione, condiviso dai consumatori.</summary>
    public static IReadOnlyDictionary<string, string> MergeAirportNames(
        IReadOnlyDictionary<string, string> airportMap, IReadOnlyList<TransferFlowRow> flows)
    {
        var merged = new Dictionary<string, string>(airportMap, System.StringComparer.OrdinalIgnoreCase);
        foreach (var f in flows)
            if (!string.IsNullOrWhiteSpace(f.AirportIcao) && !string.IsNullOrWhiteSpace(f.AirportName)
                && !merged.ContainsKey(f.AirportIcao!))
                merged[f.AirportIcao!] = f.AirportName!;
        return merged;
    }

    public static IReadOnlyList<CoordinationEntry> Build(
        IReadOnlyList<TransferFlowRow> flows,
        IReadOnlySet<string> owners,
        IReadOnlyDictionary<string, SectorType> types,
        IReadOnlyDictionary<string, string> nameMap,
        IReadOnlyDictionary<string, string> codeMap,
        IReadOnlyDictionary<string, string> airportMap,
        IReadOnlyDictionary<string, string> atcMap,
        CoordinationSentenceTemplate tpl)
    {
        var entries = new List<CoordinationEntry>();

        string? Compose(string sender, string receiver, string? icao, TransferPointRow p, TransferFlowKind kind)
            => CoordinationSentences.Compose(tpl, types, nameMap, codeMap, airportMap, atcMap, sender, receiver, icao,
                p.LevelConstraint, p.LevelValue, p.LevelUnit, p.LevelSpecial, p.Parity, p.Cop, kind,
                p.ConditionLabel, p.ConditionAreaLabel, p.ConditionCustomLabel);

        // 1) Flussi POSSEDUTI dai settori del blocco/dominio (qualsiasi Next: ACC/APP/torre; qualsiasi tipo).
        foreach (var flow in flows.Where(f => owners.Contains(f.OwningSectorCallsign)))
            foreach (var p in flow.Points)
            {
                var next = p.NextSectorCallsign;
                if (string.IsNullOrWhiteSpace(next) || !types.TryGetValue(next!, out var nextType)) continue;
                var row = new AppCoordRow(p.Cop, p.LevelText, next!, flow.Kind)
                {
                    OwnerCallsign = flow.OwningSectorCallsign,
                    AirportIcao = flow.AirportIcao,
                    Constraint = p.LevelConstraint,
                    Sentence = Compose(flow.OwningSectorCallsign, next!, flow.AirportIcao, p, flow.Kind),
                    ConditionLabel = p.ConditionDisplay,
                };
                entries.Add(new CoordinationEntry(flow.OwningSectorCallsign, next!, nextType, flow.AirportIcao, flow.Kind, IsIncoming: false, row));
            }

        // 2) Arrivi ENTRANTI: un CTR vicino (non membro) consegna a un settore del blocco/dominio.
        foreach (var flow in flows)
        {
            if (flow.Kind != TransferFlowKind.Arrival) continue;
            var owner = flow.OwningSectorCallsign;
            if (owners.Contains(owner)) continue;
            if (!types.TryGetValue(owner, out var ownerType) || ownerType != SectorType.Ctr) continue;
            foreach (var p in flow.Points)
            {
                var recv = p.NextSectorCallsign;
                if (string.IsNullOrWhiteSpace(recv) || !owners.Contains(recv!)) continue;
                var row = new AppCoordRow(p.Cop, p.LevelText, owner, TransferFlowKind.Arrival)
                {
                    OwnerCallsign = owner,
                    AirportIcao = flow.AirportIcao,
                    Constraint = p.LevelConstraint,
                    // Mittente = CTR vicino (owner), destinatario = nostro settore del blocco (recv).
                    Sentence = Compose(owner, recv!, flow.AirportIcao, p, TransferFlowKind.Arrival),
                    ConditionLabel = p.ConditionDisplay,
                };
                entries.Add(new CoordinationEntry(recv!, owner, ownerType, flow.AirportIcao, TransferFlowKind.Arrival, IsIncoming: true, row));
            }
        }

        return entries;
    }

    /// <summary>Proietta le righe di coordinamento nell'albero unico Settore → ACC → [Aeroporto (arrivi/partenze)] +
    /// [Sorvoli/VFR/altro senza aeroporto]. Condiviso da <see cref="AccDerivationService"/> (vIPI ACC) e dalla vLOA:
    /// stessa struttura, la lingua delle etichette di tipo arriva da <paramref name="kindLabel"/> (IT o EN).</summary>
    public static IReadOnlyList<AccSectorApps> BuildAccTree(
        IReadOnlyList<CoordinationEntry> entries,
        IReadOnlyDictionary<string, string> codeMap,
        IReadOnlyDictionary<string, string> atcMap,
        IReadOnlyDictionary<string, string> airportMap,
        IReadOnlyDictionary<string, string> accNameMap,
        Func<TransferFlowKind, string> kindLabel)
    {
        // Etichetta breve del settore (es. «NE»): codice (MiddleIdentifier), poi nome IVAO, poi callsign.
        string SectorLabel(string cs)
        {
            var code = codeMap.GetValueOrDefault(cs);
            if (!string.IsNullOrWhiteSpace(code)) return code!;
            var atc = atcMap.GetValueOrDefault(cs);
            return string.IsNullOrWhiteSpace(atc) ? cs : atc!;
        }
        string AirportLabel(string? icao) =>
            string.IsNullOrWhiteSpace(icao) ? "—"
            : (airportMap.TryGetValue(icao!, out var n) ? $"{n} {icao}" : icao!);

        // ACC del nodo = quello del counterpart (ricevente per gli uscenti, CTR vicino per gli entranti).
        string AccOf(CoordinationEntry e) => accNameMap.GetValueOrDefault(e.CounterpartCallsign, "ACC");
        static bool IsAirportKind(TransferFlowKind k) => k is TransferFlowKind.Arrival or TransferFlowKind.Departure;

        return entries.GroupBy(e => e.OurSectorCallsign, StringComparer.OrdinalIgnoreCase)
            .OrderBy(sg => SectorLabel(sg.Key), StringComparer.OrdinalIgnoreCase)
            .Select(sg => new AccSectorApps(
                SectorLabel(sg.Key),
                sg.GroupBy(AccOf, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(ag => ag.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(ag => new AccAccAirports(
                        ag.Key,
                        // Aeroporti: solo arrivi/partenze (richiedono un aeroporto).
                        ag.Where(e => IsAirportKind(e.Kind))
                            .GroupBy(e => e.AirportIcao ?? "", StringComparer.OrdinalIgnoreCase)
                            .OrderBy(pg => AirportLabel(pg.Key), StringComparer.OrdinalIgnoreCase)
                            .Select(pg =>
                            {
                                // pg contiene solo Arrival/Departure (filtrato sopra): partiziona in una sola passata.
                                var byArrival = pg.ToLookup(e => e.Kind == TransferFlowKind.Arrival);
                                return new AccAirportFlows(
                                    AirportLabel(pg.Key),
                                    byArrival[true].Select(e => e.Row).ToList(),
                                    byArrival[false].Select(e => e.Row).ToList());
                            })
                            .ToList(),
                        // Sorvoli/VFR/altro: senza aeroporto, raggruppati per etichetta di tipo.
                        ag.Where(e => !IsAirportKind(e.Kind))
                            .GroupBy(e => e.Kind)
                            .OrderBy(kg => kg.Key)
                            .Select(kg => new AccExtraFlows(kindLabel(kg.Key), kg.Select(e => e.Row).ToList()))
                            .ToList()))
                    .ToList()))
            .ToList();
    }
}

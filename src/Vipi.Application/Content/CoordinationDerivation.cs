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

    /// <summary>
    /// Riempie la riga di tabella a partire dal punto: livelli, faccetta trasferimento, velocità, gruppo di
    /// varianti. La frase arriva già composta dal chiamante, perché ACC e vLOA la compongono con mappe di nomi
    /// diverse; tutto il resto è identico, ed è il motivo per cui sta qui e non due volte.
    /// <para>Le colonne della faccetta viaggiano GIÀ A PAROLE: la parola del luogo («al confine dell'AoR») è
    /// lingua, la lingua vive nel template, e il template esiste anche in inglese per le vLOA. Una vista che
    /// traducesse da sé rifarebbe quel lavoro — e lo rifarebbe in italiano anche dentro una vLOA.</para>
    /// </summary>
    public static AppCoordRow ToRow(
        CoordinationSentenceTemplate tpl, TransferPointRow p, TransferFlowRow flow,
        string next, TransferFlowKind kind, string ownerCallsign, string? sentence, string? lead = null) =>
        new(p.Cop, p.LevelText, next, kind)
        {
            OwnerCallsign = ownerCallsign,
            AirportIcao = flow.AirportIcao,
            Constraint = p.LevelConstraint,
            Sentence = sentence,
            // In TABELLA la riga mostra il proprio DELTA: il rientro dà il contesto, e ripetere la condizione
            // della capofila su ogni eccezione allungherebbe la colonna nascondendo ciò che cambia. La frase
            // invece cumula, perché viaggia da sola nella prosa (vedi CoordinationSentenceComposer).
            // Il marcatore della riga trasversale viene dal TEMPLATE come la frase, non dalle risorse
            // dell'interfaccia: sono la stessa cosa detta a due centimetri di distanza, e con la UI in inglese
            // e le frasi in italiano si leggevano in due lingue diverse. Visto a schermo, non da un test.
            ConditionLabel = p.IsGroupWide
                ? $"{tpl.GroupWide} · {p.ConditionDisplay}"
                : p.ConditionDisplay,
            Handoff = TransferHandoffText.Place(tpl, p.HandoffKind, p.HandoffLabel),
            HandoffLevel = TransferHandoffText.Level(tpl, p.HandoffLevelValue, p.HandoffLevelUnit, p.HandoffLevelConstraint),
            CommsHandoff = TransferHandoffText.CommsPlace(tpl, TransferHandoffFacet.From(p)),
            Speed = TransferHandoffText.Speed(tpl, p.SpeedValue, p.SpeedConstraint),
            FlowId = flow.Id,
            // La provenienza: da quale clausola viene la riga, e cosa diceva quella clausola per intero.
            ClauseId = p.ClauseId,
            Points = p.Cops,
            LeadSentence = lead,
            AgreementAirports = p.AgreementAirports,
            VariantGroup = p.VariantGroup,
            VariantDepth = p.VariantDepth,
            IsGroupWide = p.IsGroupWide,
        };

    /// <summary>
    /// La catena delle condizioni che valgono per una riga, dalla capofila dell'outline alla riga stessa.
    /// <para>La risalita vive in <see cref="Outline"/>: la fa anche l'editor, su una forma diversa, e due
    /// risalite scritte a mano potrebbero leggere due strutture diverse dallo stesso outline.</para>
    /// </summary>
    public static IReadOnlyList<ConditionClause> ConditionChain(IReadOnlyList<TransferPointRow> flowPoints, TransferPointRow p) =>
        Outline.ConditionChain(flowPoints, p,
            x => new ConditionClause(x.ConditionLabel, x.ConditionAreaLabel, x.ConditionCustomLabel));

    /// <summary>La riga di cui <paramref name="p"/> è un'eccezione, o <c>null</c>. Serve anche alla TABELLA
    /// dell'editor, che deve dire di quale riga una condizione è l'eccezione.</summary>
    public static TransferPointRow? ParentOf(IReadOnlyList<TransferPointRow> flowPoints, TransferPointRow p) =>
        Outline.ParentOf(flowPoints, p);

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

        // La catena si costruisce dal flusso a cui la riga appartiene: gli antenati si risalgono per posizione.
        string? Compose(string sender, string receiver, TransferFlowRow flow, TransferPointRow p, TransferFlowKind kind)
            => CoordinationSentences.Compose(tpl, types, nameMap, codeMap, airportMap, atcMap, sender, receiver,
                flow.AirportIcao,
                p.LevelConstraint, p.LevelValue, p.LevelUnit, p.LevelSpecial, p.Parity, p.Cop, kind,
                ConditionChain(flow.Points, p), p.VerticalState, TransferHandoffFacet.From(p));

        // La frase CAPOFILA: chi trasferisce a chi e che traffico, senza livello ne' punto — quelli sono cio' che
        // la tabella dice riga per riga. Si compone qui, dove ci sono le mappe, perche' la lingua vive nel
        // template e la vista non deve rimetterla insieme da se'.
        string? Lead(string sender, string receiver, TransferFlowRow flow, TransferFlowKind kind)
            => CoordinationSentences.ComposeLead(tpl, types, nameMap, codeMap, airportMap, atcMap,
                sender, receiver, flow.AirportIcao, kind);

        AppCoordRow Row(TransferPointRow p, string next, TransferFlowRow flow, string sentenceOwner, string sentenceTarget,
            TransferFlowKind kind) =>
            ToRow(tpl, p, flow, next, kind, sentenceOwner, Compose(sentenceOwner, sentenceTarget, flow, p, kind),
                  Lead(sentenceOwner, sentenceTarget, flow, kind));

        // 1) Flussi POSSEDUTI dai settori del blocco/dominio (qualsiasi Next: ACC/APP/torre; qualsiasi tipo).
        foreach (var flow in flows.Where(f => owners.Contains(f.OwningSectorCallsign)))
            foreach (var p in flow.Points)
            {
                var next = p.NextSectorCallsign;
                if (string.IsNullOrWhiteSpace(next) || !types.TryGetValue(next!, out var nextType)) continue;
                var row = Row(p, next!, flow, flow.OwningSectorCallsign, next!, flow.Kind);
                entries.Add(new CoordinationEntry(flow.OwningSectorCallsign, next!, nextType, flow.AirportIcao, flow.Kind, IsIncoming: false, row));
            }

        // 2) ENTRANTI: un ente ESTERNO al blocco consegna a un settore del blocco/dominio.
        //
        // Fino all'11 agosto 2026 questo passo accettava solo `Kind == Arrival` da un `Ctr`: la conseguenza era
        // che la vIPI di un ACC NON mostrava le partenze che i suoi APP gli consegnano — un accordo bilaterale
        // visibile da un lato solo. La sezione estesa deve portare tutto ciò che entra o esce dall'ente, quindi
        // il filtro è caduto: conta solo che il ricevente sia nostro e il mittente no.
        foreach (var flow in flows)
        {
            var owner = flow.OwningSectorCallsign;
            if (owners.Contains(owner)) continue;                            // già coperto dal passo 1
            if (!types.TryGetValue(owner, out var ownerType)) continue;      // mittente non risolto: riga muta
            foreach (var p in flow.Points)
            {
                var recv = p.NextSectorCallsign;
                if (string.IsNullOrWhiteSpace(recv) || !owners.Contains(recv!)) continue;
                // Mittente = ente vicino (owner), destinatario = nostro settore del blocco (recv).
                var row = Row(p, owner, flow, owner, recv!, flow.Kind);
                entries.Add(new CoordinationEntry(recv!, owner, ownerType, flow.AirportIcao, flow.Kind, IsIncoming: true, row));
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

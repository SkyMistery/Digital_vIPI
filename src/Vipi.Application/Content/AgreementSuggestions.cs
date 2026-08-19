using System;
using System.Collections.Generic;
using System.Linq;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Un ente proposto come parte di un accordo, con la ragione per cui lo è.</summary>
/// <param name="Callsign">Il callsign da mettere nella parte.</param>
/// <param name="Reason">Perché è proposto — si mostra accanto alla voce: chi sceglie deve poter dire di no.</param>
/// <param name="Rank">Ordine di proposta: più basso = più probabile. Non è una certezza, è una scommessa.</param>
public sealed record AgreementSuggestion(string Callsign, AgreementSuggestionReason Reason, int Rank);

/// <summary>Perché un ente è proposto. È un enum e non una stringa perché la <b>lingua</b> di questa spiegazione
/// sta nell'interfaccia, e l'interfaccia esiste anche in inglese.</summary>
public enum AgreementSuggestionReason
{
    /// <summary>Serve uno degli aeroporti dell'accordo (il callsign ne porta l'ICAO): torre, ground, delivery.</summary>
    AirportUnit,

    /// <summary>È l'avvicinamento che copre uno degli aeroporti dell'accordo.</summary>
    AirportApproach,

    /// <summary>È un settore di un ACC estero <b>confinante confermato</b>: il caso degli accordi di confine.</summary>
    ConfirmedNeighbour,

    /// <summary>È già l'altro capo di un accordo con questa stessa parte: chi ha scritto un accordo con Roma ne
    /// scriverà probabilmente un altro.</summary>
    ExistingCounterpart,
}

/// <summary>
/// Chi proporre come **parte** di un accordo, e perché.
///
/// <para>Funzione pura, e non è un dettaglio di comodo: la proposta è una <b>scommessa</b> — «probabilmente
/// vuoi la torre di Bari» — e una scommessa va potuta leggere, provare e smentire senza aprire un database.
/// Chi sceglie vede sempre la ragione accanto alla voce: una proposta senza il suo perché è indistinguibile
/// da un dato, e verrebbe accettata anche quando è sbagliata.</para>
///
/// <para>Non filtra mai l'elenco completo: <b>propone in cima</b>. Un accordo può legare due enti che nessuna
/// di queste regole prevede — è successo con LGKR_APP, aggiunto a mano — e un elenco che li nascondesse
/// renderebbe impossibile scrivere proprio i casi che valeva la pena scrivere.</para>
/// </summary>
public static class AgreementSuggestions
{
    /// <summary>
    /// Gli enti da proporre per il lato che <b>riceve</b>, dato ciò che l'accordo dice finora.
    /// </summary>
    /// <param name="airports">Gli ICAO dell'accordo; vuoto per sorvoli/VFR/altro.</param>
    /// <param name="sectors">Tutti i settori noti (callsign + ACC + tipo): l'elenco da cui si pesca.</param>
    /// <param name="airportParents">ICAO → callsign dell'ente che copre l'aeroporto (l'APP, o l'ACC).</param>
    /// <param name="confirmedNeighbourAccs">I codici ACC esteri confinanti <b>confermati</b>.</param>
    /// <param name="alreadyThere">Chi è già nell'accordo: non si propone due volte.</param>
    public static IReadOnlyList<AgreementSuggestion> ForReceivingSide(
        TransferFlowKind kind,
        IReadOnlyList<string> airports,
        IReadOnlyList<SuggestionSector> sectors,
        IReadOnlyDictionary<string, string> airportParents,
        IReadOnlySet<string> confirmedNeighbourAccs,
        IReadOnlySet<string> alreadyThere)
    {
        var found = new Dictionary<string, AgreementSuggestion>(StringComparer.OrdinalIgnoreCase);

        void Offer(string callsign, AgreementSuggestionReason reason, int rank)
        {
            if (string.IsNullOrWhiteSpace(callsign) || alreadyThere.Contains(callsign)) return;
            // La prima ragione vince: sono ordinate per forza, e la seconda direbbe qualcosa di più debole
            // sulla stessa voce.
            if (!found.ContainsKey(callsign))
                found[callsign] = new AgreementSuggestion(callsign, reason, rank);
        }

        // 1) Gli enti degli aeroporti dell'accordo. Solo per arrivi e partenze: un sorvolo non ha un aeroporto
        //    da consegnare, e proporgli una torre sarebbe proporre qualcosa che non ha senso.
        if (kind is TransferFlowKind.Arrival or TransferFlowKind.Departure)
        {
            foreach (var icao in airports)
            {
                // L'ente che copre l'aeroporto, dichiarato nella gerarchia: è il ricevente più probabile di un
                // arrivo, e viene prima di qualunque deduzione dal callsign.
                if (airportParents.TryGetValue(icao, out var parent))
                    Offer(parent, AgreementSuggestionReason.AirportApproach, 0);

                // Poi gli enti il cui callsign porta l'ICAO. È la convenzione con cui i cataloghi IVAO nominano
                // le posizioni (LIBD_TWR, LIBD_CS0_APP), e qui è una deduzione dichiarata: se un domani la
                // convenzione cambiasse, questa proposta smetterebbe di comparire — non produrrebbe voci
                // sbagliate.
                foreach (var s in sectors.Where(s => ServesAirport(s.Callsign, icao)))
                    Offer(s.Callsign, s.Type == SectorType.App
                        ? AgreementSuggestionReason.AirportApproach
                        : AgreementSuggestionReason.AirportUnit, s.Type == SectorType.App ? 1 : 2);
            }
        }

        // 2) I confinanti esteri confermati. Valgono per QUALUNQUE tipo di traffico: un sorvolo di confine è il
        //    caso tipico, ma anche un arrivo su uno scalo estero passa di lì.
        foreach (var s in sectors.Where(s => confirmedNeighbourAccs.Contains(s.AccCode))
                     .OrderBy(s => s.Type == SectorType.Ctr ? 0 : 1)   // il centro prima delle sue posizioni
                     .ThenBy(s => s.Callsign, StringComparer.OrdinalIgnoreCase))
            Offer(s.Callsign, AgreementSuggestionReason.ConfirmedNeighbour, 3);

        return found.Values
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.Callsign, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Vero se il callsign nomina una posizione di quell'aeroporto secondo la convenzione IVAO: l'ICAO è il
    /// primo pezzo del callsign (<c>LIBD_TWR</c>, <c>LIBD_CS0_APP</c>).
    /// <para>Il confronto è sul PEZZO e non sul prefisso: <c>LIBD</c> non deve pescare <c>LIBDX_TWR</c>, e un
    /// prefisso nudo lo farebbe.</para>
    /// </summary>
    private static bool ServesAirport(string callsign, string icao)
    {
        if (string.IsNullOrWhiteSpace(callsign) || string.IsNullOrWhiteSpace(icao)) return false;
        var cut = callsign.IndexOf('_');
        var head = cut < 0 ? callsign : callsign[..cut];
        return string.Equals(head, icao, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Il minimo che serve a proporre un ente: come si chiama, di chi è, e che tipo è.</summary>
public sealed record SuggestionSector(string Callsign, string AccCode, SectorType Type);

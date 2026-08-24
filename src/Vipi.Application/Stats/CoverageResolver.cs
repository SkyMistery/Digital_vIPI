using System;
using System.Collections.Generic;

namespace Vipi.Application.Stats;

/// <summary>Un nodo dell'albero di copertura: il minimo che serve alla discesa.</summary>
/// <param name="Callsign">Callsign del settore (<c>Sector.Callsign</c>).</param>
/// <param name="ParentCallsign">Callsign del padre nell'albero proiettato; <c>null</c> = radice.</param>
public readonly record struct CoverageNode(string Callsign, string? ParentCallsign);

/// <summary>
/// Chi copre cosa, adesso. Un settore è coperto dal <b>primo antenato online</b> risalendo da lui, sé compreso;
/// se nessun antenato è online, non lo copre nessuno.
///
/// <para>È il verso opposto di <c>TransferOnlineResolver</c>: quello risale per trovare il ricevente di un
/// punto di trasferimento, questo scende per sapere quale traffico attribuire a una sessione ATC.</para>
///
/// <para>⚠️ L'albero da passare è quello <b>proiettato</b> (<c>Sector.ParentSectorId</c>), non
/// <c>AccSector.ParentCallsign</c>: nei cataloghi il padre lo hanno solo ACC e APP, mentre DEL/GND/TWR lo
/// ricavano dalla scaletta <c>AirportPositionLadder</c> — derivazione che la proiezione ha già fatto.
/// Rifarla qui significherebbe sbagliarla in un secondo modo.</para>
///
/// <para>Puro e deterministico, nessun I/O. Confronti sui callsign sempre senza distinzione di maiuscole.</para>
/// </summary>
public static class CoverageResolver
{
    /// <summary>
    /// Per ogni settore dell'albero, il callsign online che lo copre (o <c>null</c> se nessuno).
    /// I callsign online che non stanno nell'albero (settori esteri, posizioni fuori catalogo) sono ignorati.
    /// </summary>
    public static IReadOnlyDictionary<string, string?> Owners(
        IReadOnlyList<CoverageNode> nodes, IReadOnlySet<string> online)
    {
        var parents = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var n in nodes)
            if (!string.IsNullOrWhiteSpace(n.Callsign))
                parents[n.Callsign] = n.ParentCallsign;

        var owners = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var n in nodes)
            if (!string.IsNullOrWhiteSpace(n.Callsign))
                owners[n.Callsign] = FirstOnlineAncestor(n.Callsign, parents, online);

        return owners;
    }

    /// <summary>
    /// I settori coperti da <paramref name="target"/> adesso, lui compreso. Vuoto se <paramref name="target"/>
    /// non è online: chi non è connesso non gestisce niente.
    /// </summary>
    public static IReadOnlyList<string> CoveredBy(
        string target, IReadOnlyList<CoverageNode> nodes, IReadOnlySet<string> online)
    {
        var covered = new List<string>();
        if (string.IsNullOrWhiteSpace(target) || !online.Contains(target)) return covered;

        foreach (var pair in Owners(nodes, online))
            if (string.Equals(pair.Value, target, StringComparison.OrdinalIgnoreCase))
                covered.Add(pair.Key);

        return covered;
    }

    /// <summary>
    /// Risale la catena dei padri partendo dal settore stesso e restituisce il primo callsign online.
    /// La guardia sui nodi già visti chiude i cicli (dato sporco possibile in archivio: A→B→A), che
    /// altrimenti sarebbero un blocco del poller, non un risultato sbagliato.
    /// </summary>
    private static string? FirstOnlineAncestor(
        string callsign, IReadOnlyDictionary<string, string?> parents, IReadOnlySet<string> online)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = callsign;

        while (current is not null && visited.Add(current))
        {
            if (online.Contains(current)) return current;
            current = parents.TryGetValue(current, out var parent) ? parent : null;
        }
        return null;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>La posizione di una sezione nell'ordine imposto. È una chiave e non un numero perché l'ordine si
/// <b>ricava</b> dai dati della sezione: salvarlo vorrebbe dire tenerlo d'accordo a ogni modifica.</summary>
public readonly record struct SectionSortKey(int GroupRank, string GroupKey, int KindRank, int DirectionRank,
    int Order, int Id) : IComparable<SectionSortKey>
{
    public int CompareTo(SectionSortKey other)
    {
        var c = GroupRank.CompareTo(other.GroupRank);
        if (c != 0) return c;
        c = string.Compare(GroupKey, other.GroupKey, StringComparison.OrdinalIgnoreCase);
        if (c != 0) return c;
        c = KindRank.CompareTo(other.KindRank);
        if (c != 0) return c;
        c = DirectionRank.CompareTo(other.DirectionRank);
        if (c != 0) return c;
        c = Order.CompareTo(other.Order);
        return c != 0 ? c : Id.CompareTo(other.Id);
    }
}

/// <summary>
/// **L'ordine delle sezioni dentro un accordo, imposto.**
///
/// <para>Prima il traffico legato agli scali, un aeroporto per volta e per ognuno gli <b>arrivi</b> e poi le
/// <b>partenze</b>: sono il reciproco l'uno dell'altra, e leggerli accostati è il solo modo di accorgersi che
/// uno dei due manca. Poi i <b>sorvoli</b>, i due versi di fila. In fondo VFR e Altro.</para>
///
/// <para><b>Perché imposto e non trascinabile.</b> L'ordine delle sezioni non dice niente: non è struttura come
/// l'ordine delle clausole, dove una riga appartiene all'ultima meno profonda che la precede. Lasciarlo a mano
/// vorrebbe dire poter nascondere una partenza in fondo, lontano dai suoi arrivi. L'<c>Order</c> salvato resta
/// come <b>ultimo</b> criterio, cioè fra sezioni che si somigliano su tutto il resto.</para>
/// </summary>
public static class AgreementSectionOrder
{
    /// <summary>La chiave d'ordine di una sezione, dai suoi dati.</summary>
    public static SectionSortKey KeyOf(TransferFlowKind kind, AgreementDirection direction, string airportsLabel,
        int order, int id) =>
        new(GroupRank(kind),
            kind is TransferFlowKind.Arrival or TransferFlowKind.Departure ? airportsLabel : "",
            kind == TransferFlowKind.Arrival ? 0 : 1,
            direction == AgreementDirection.AtoB ? 0 : 1,
            order, id);

    /// <summary>Le sezioni nell'ordine in cui si leggono.</summary>
    public static IReadOnlyList<AgreementSectionRow> Sort(IEnumerable<AgreementSectionRow> sections) =>
        sections.OrderBy(KeyOf).ToList();

    /// <summary>La chiave d'ordine di una sezione già letta.</summary>
    public static SectionSortKey KeyOf(AgreementSectionRow s) =>
        KeyOf(s.Kind, s.Direction, s.AirportsLabel, s.Order, s.Id);

    /// <summary>La chiave del <b>gruppo</b> di una sezione: gli scali per arrivi e partenze, vuota per tutto il
    /// resto. Due sezioni con la stessa chiave si leggono attaccate, ed è ciò che accosta gli arrivi a LIRF alle
    /// partenze da LIRF.</summary>
    public static string GroupKey(AgreementSectionRow s) => KeyOf(s).GroupKey;

    /// <summary>Il blocco a cui la sezione appartiene: scali (0), sorvoli (1), VFR (2), Altro (3).</summary>
    public static int GroupRank(TransferFlowKind kind) => kind switch
    {
        TransferFlowKind.Arrival or TransferFlowKind.Departure => 0,
        TransferFlowKind.Overflight => 1,
        TransferFlowKind.Vfr => 2,
        _ => 3,
    };
}

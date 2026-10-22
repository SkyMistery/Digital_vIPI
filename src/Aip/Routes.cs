using System.Text.Json.Serialization;

namespace Aip;

/// <summary>
/// Una rotta ATS di ENR 3 (3.1 rotte convenzionali, 3.2 rotte di navigazione d'area, 3.3 le altre), come la stampa
/// l'AIP: i punti in ordine e, fra due punti, un tratto coi suoi dati (carta F6 §6, slice 2).
/// <para>I tratti sono uno meno dei punti: <c>Segments[i]</c> va da <c>Points[i]</c> a <c>Points[i + 1]</c>.</para>
/// </summary>
public sealed record Route
{
    /// <summary>La sezione dell'AIP: <c>ENR 3.2</c>.</summary>
    public required string Section { get; init; }

    /// <summary>L'identificativo della rotta, com'è stampato: <c>L12</c>, <c>KY139</c>.</summary>
    public required string Designator { get; init; }

    /// <summary>La specifica di navigazione, dove l'AIP la dice: <c>RNAV 5</c>.</summary>
    public string? Navigation { get; init; }

    public required IReadOnlyList<RoutePoint> Points { get; init; }

    public required IReadOnlyList<RouteSegment> Segments { get; init; }

    /// <summary>La pagina della prima riga della rotta, con la sua data AIRAC.</summary>
    public AipPage? Page { get; init; }

    /// <summary>
    /// Le note scritte dopo l'ultimo punto, nella colonna delle note (M740 nel 2611: «For continuation see AIP MALTA»;
    /// carta «L'AIP intero», passo 2d).
    /// </summary>
    public string? Remarks { get; init; }

    /// <summary>
    /// Il riquadro «NOTE / REMARKS» sotto la tabella (carta «L'AIP intero», passo 3): le note numerate, in italiano e in
    /// inglese («Il tratto UNIMI-NAXAV è utilizzabile solo in direzione nord»). Vuoto se l'AIP stampa «NIL»; null se il
    /// riquadro non c'è.
    /// </summary>
    public IReadOnlyList<RouteNote>? Notes { get; init; }

    // ⚠️ Scritta a mano: l'uguaglianza dei record confronterebbe le liste per riferimento.
    public bool Equals(Route? other) =>
        other is not null
        && Section == other.Section && Designator == other.Designator && Navigation == other.Navigation
        && Points.SequenceEqual(other.Points) && Segments.SequenceEqual(other.Segments) && Page == other.Page
        && Remarks == other.Remarks
        && (Notes is null ? other.Notes is null : other.Notes is not null && Notes.SequenceEqual(other.Notes));

    public override int GetHashCode() => HashCode.Combine(Section, Designator);
}

/// <summary>
/// Una nota del riquadro «NOTE / REMARKS» di una rotta: il numero com'è stampato (null se la nota è una sola e senza
/// numero), il testo italiano e quello inglese.
/// </summary>
public sealed record RouteNote(int? Number, string Text, string? English);

/// <summary>
/// Un punto significativo di una rotta. <paramref name="Designator"/>: il nome con cui la rotta lo usa (<c>UNIMI</c>, e per
/// una radioassistenza la sua sigla, <c>VIC</c>); <paramref name="Text"/>: com'è stampato (<c>VICENZA VOR/DME 'VIC'</c>).
/// <paramref name="Compulsory"/>: punto di riporto obbligatorio (▲) o a richiesta (∆). <paramref name="Remarks"/>: quel che
/// l'AIP scrive accanto al punto (l'ente che cambia, «For continuation see AIP AUSTRIA»).
/// </summary>
public sealed record RoutePoint(string Designator, string Text, Position Position, bool Compulsory, string? Remarks = null);

/// <summary>
/// In che verso della rotta valgono i livelli di crociera di una parità: nell'ordine dei punti dell'AIP (↓, dall'alto in
/// basso della tabella), al contrario (↑), o in nessuno (la colonna è vuota).
/// </summary>
public enum RouteDirection
{
    None,
    Forward,
    Backward,
}

/// <summary>
/// Un tratto di rotta fra due punti, con le colonne dell'AIP: rotta magnetica e distanza, limiti verticali, ampiezza
/// laterale, per i livelli dispari e pari il verso e la quota minima (MNM FL), le note.
/// </summary>
public sealed record RouteSegment
{
    /// <summary>Le rotte magnetiche, come le stampa l'AIP: <c>153° / 333°</c>.</summary>
    public string? Track { get; init; }

    public double? DistanceNm { get; init; }

    public VerticalLimit? Upper { get; init; }

    public VerticalLimit? Lower { get; init; }

    /// <summary>L'ampiezza laterale, in NM, come la stampa l'AIP (<c>5</c>).</summary>
    public string? LateralLimits { get; init; }

    /// <summary>Il verso dei livelli dispari (colonna 5, la prima).</summary>
    public RouteDirection Odd { get; init; }

    /// <summary>Il verso dei livelli pari (colonna 5, la seconda).</summary>
    public RouteDirection Even { get; init; }

    /// <summary>La quota minima per i livelli dispari (MNM FL), come stampata: <c>130</c>.</summary>
    public string? MinimumOdd { get; init; }

    public string? MinimumEven { get; init; }

    public string? Remarks { get; init; }

    /// <summary>La rotta qui si interrompe: fra i due punti non c'è un tratto (l'AIP lo segna con una riga di barre).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Discontinuity { get; init; }

    /// <summary>Il tratto si percorre nell'ordine dei punti: una delle due parità ha il verso avanti.</summary>
    [JsonIgnore]
    public bool Forward => Odd == RouteDirection.Forward || Even == RouteDirection.Forward;

    /// <summary>Il tratto si percorre al contrario.</summary>
    [JsonIgnore]
    public bool Backward => Odd == RouteDirection.Backward || Even == RouteDirection.Backward;
}

/// <summary>
/// Una riga di ENR 3.4.2 (elenco delle radioassistenze e delle rotte e procedure associate): la sigla, il tipo, il nome, le
/// rotte ATS e le procedure interessate. Ci sono anche le radioassistenze senza attese, che come <see cref="Holding"/> non
/// ci sarebbero: nel 2611 diciassette, AHO TACAN con «EA star, EA sid, EA ial» (carta «L'AIP intero», passo 2a).
/// </summary>
public sealed record NavaidProcedures
{
    public required string Section { get; init; }

    /// <summary>La sigla: <c>AHO</c>.</summary>
    public required string Designator { get; init; }

    /// <summary>Il tipo com'è stampato: <c>TACAN</c>, <c>VOR/DME</c>.</summary>
    public string? Type { get; init; }

    /// <summary>Il nome fra parentesi sotto la sigla, senza le parentesi: <c>Alghero</c>.</summary>
    public string? Name { get; init; }

    /// <summary>Le rotte ATS e le procedure interessate, com'è stampate, una per riga (<c>L 5</c>, <c>EA star</c>).</summary>
    public string? Affected { get; init; }

    public AipPage? Page { get; init; }
}

/// <summary>Il verso della virata di un'attesa.</summary>
public enum TurnDirection
{
    Right,
    Left,
}

/// <summary>
/// Un'attesa di ENR 3.4.1 (procedure di attesa in rotta sui punti di riporto) o di ENR 3.4.2 (sulle radioassistenze): il
/// punto o la sigla, il riferimento dato dall'AIP (radiale e distanza da una radioassistenza, se c'è), la quota minima
/// (MHA/MHL), il verso della virata, la rotta d'avvicinamento magnetica, le note. Un punto può avere più attese (le varianti
/// numerate «(1)», «(2)»). Anche le attese delle tabelle di AD 2 e AD 3 (le STAR, gli avvicinamenti), con la sezione dello
/// scalo (<c>AD 2 LIRN</c>) e qualche colonna in più.
/// </summary>
public sealed record Holding
{
    public required string Section { get; init; }

    /// <summary>Il punto dell'attesa: <c>ABRUS</c>; in ENR 3.4.2 la sigla della radioassistenza: <c>AEA</c>.</summary>
    public required string Fix { get; init; }

    /// <summary>ENR 3.4.2: il tipo della radioassistenza com'è stampato (<c>VOR/DME</c>); la sigla da sola non basta (PIS).</summary>
    public string? FixType { get; init; }

    /// <summary>Il riferimento della colonna 2, com'è stampato: <c>RDL070/10NM PAN</c>.</summary>
    public string? Bearing { get; init; }

    /// <summary>La quota minima (MHA/MHL), come stampata: <c>3000</c>, <c>FL110</c>.</summary>
    public required VerticalLimit Minimum { get; init; }

    public required TurnDirection Turn { get; init; }

    /// <summary>La rotta d'avvicinamento magnetica, in gradi (<c>250</c>).</summary>
    public required int InboundTrack { get; init; }

    /// <summary>I rimandi alle note accanto all'attesa: <c>(1)</c>, <c>(2)(3)</c>.</summary>
    public string? NoteRefs { get; init; }

    /// <summary>Le note del punto, com'è stampato (valgono per tutte le sue attese).</summary>
    public string? Remarks { get; init; }

    /// <summary>ENR 3.4.2: le rotte ATS e le procedure interessate, com'è stampate, una per riga (<c>L 5</c>, <c>EA star</c>).</summary>
    public string? Affected { get; init; }

    /// <summary>AD 2 e AD 3 (le tabelle delle attese, passo 6 della carta «L'AIP intero»): la rotta vera, fra parentesi: <c>036.6</c>.</summary>
    public string? TrueTrack { get; init; }

    /// <summary>AD 2 e AD 3: il tratto in allontanamento, com'è stampato (minuti o NM, secondo la tabella): <c>1</c>, <c>3.0 4.2/-</c>.</summary>
    public string? Leg { get; init; }

    /// <summary>AD 2 e AD 3: la quota massima, dove la tabella la dà: <c>FL105</c>.</summary>
    public VerticalLimit? Maximum { get; init; }

    /// <summary>AD 2 e AD 3: la velocità massima com'è stampata: <c>210</c>, <c>-200</c>.</summary>
    public string? Speed { get; init; }

    /// <summary>AD 2 e AD 3: la specifica di navigazione com'è stampata: <c>RNAV1</c>, <c>RNP 1</c>.</summary>
    public string? Navigation { get; init; }

    /// <summary>AD 2 e AD 3: la riga della tabella com'è stampata, tutte le colonne (anche D e WD, la declinazione).</summary>
    public string? Text { get; init; }

    public AipPage? Page { get; init; }
}

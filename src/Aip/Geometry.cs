using System.Text.Json.Serialization;

namespace Aip;

/// <summary>
/// La forma di un'area, nella grammatica dell'AIP (carta madre §5): un <b>cerchio</b>, oppure un <b>anello</b>
/// di tratti — rette, archi, tratti di confine o di costa. 🔴 Non sono vertici già disegnati: un arco resta
/// un arco (centro, raggio, verso), e la densità la sceglie chi lo disegna (il converter per Aurora, slice 0b).
/// <para>⚠️ Nel JSON il campo <c>kind</c> va scritto <b>per primo</b> nell'oggetto: su net8 il lettore lo
/// pretende lì. <see cref="AipJson"/> lo scrive così; chi scrive un file a mano deve fare lo stesso.</para>
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(CircleGeometry), "circle")]
[JsonDerivedType(typeof(RingGeometry), "ring")]
[JsonDerivedType(typeof(CorridorGeometry), "corridor")]
public abstract record Geometry;

/// <summary>
/// «Corridor 10 NM wide joining following points: &lt;C&gt;; &lt;C&gt;; …» (ENR 5.2.2.5, carta F6 slice 4a): una linea di punti
/// e la sua ampiezza <b>intera</b>, come stampata (metà per lato). Non è un anello: chi disegna ne fa il contorno.
/// <para>Il tipo che ha portato lo schema alla versione 2 (la regola della 0a: un tipo nuovo di geometria la fa salire).</para>
/// </summary>
public sealed record CorridorGeometry(Distance Width, IReadOnlyList<Position> Points) : Geometry
{
    public bool Equals(CorridorGeometry? other) => other is not null && Width == other.Width && Points.SequenceEqual(other.Points);

    public override int GetHashCode() => HashCode.Combine(Width, Points.Count);
}

/// <summary>«Circular area centered on &lt;C&gt; within a 1.0 NM radius.»</summary>
public sealed record CircleGeometry(Position Center, Distance Radius) : Geometry;

/// <summary>
/// Un anello chiuso di tratti. Ogni tratto comincia nel suo <see cref="Segment.From"/> e finisce dove comincia
/// il seguente; l'ultimo torna sul primo («to point of origin»), che quindi <b>non</b> si ripete in fondo.
/// </summary>
public sealed record RingGeometry(IReadOnlyList<Segment> Segments) : Geometry
{
    public bool Equals(RingGeometry? other) => other is not null && Segments.SequenceEqual(other.Segments);

    public override int GetHashCode() => Segments.Count;
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(LineSegment), "line")]
[JsonDerivedType(typeof(ArcSegment), "arc")]
[JsonDerivedType(typeof(BorderSegment), "border")]
public abstract record Segment(Position From);

/// <summary>Da <see cref="Segment.From"/> in linea retta (lossodromica o ortodromica: lo decide chi disegna) al tratto dopo.</summary>
public sealed record LineSegment(Position From) : Segment(From);

/// <summary>
/// «&lt;C&gt; then arc of circle in clockwise direction radius 2.8 NM centred on &lt;C&gt; till point &lt;C&gt;»:
/// da <see cref="Segment.From"/> lungo l'arco attorno a <see cref="Center"/> fino all'inizio del tratto dopo.
/// <para><see cref="Radius"/> è quello stampato; può mancare (il lettore della F1 lo usa solo per il controllo:
/// gli estremi e il centro bastano a disegnare l'arco).</para>
/// </summary>
public sealed record ArcSegment(Position From, Position Center, Distance? Radius, ArcDirection Direction) : Segment(From);

/// <summary>
/// «&lt;C&gt; Italian northern geographical border till point &lt;C&gt;», «line at 500 m from coast»: il PDF nomina
/// la linea ma non ne dà la geometria. Si tiene la frase com'è (<see cref="Text"/>); il tracciato lo prende chi
/// disegna da un'altra fonte (per l'Italia <c>itgeo.geo</c>), e lo dice.
/// </summary>
public sealed record BorderSegment(Position From, BorderKind Border, string Text) : Segment(From);

public enum ArcDirection
{
    Clockwise,
    CounterClockwise,
}

public enum BorderKind
{
    /// <summary>Un confine di Stato.</summary>
    State,

    /// <summary>La costa, o una linea a distanza fissa dalla costa.</summary>
    Coast,

    /// <summary>Un fiume, o un'altra linea naturale.</summary>
    River,
}

namespace Vipi.Sectorfile.Models;

/// <summary>
/// A record of an ACC/*.artcc file, which mixes two kinds of line (F2 slice 5): the labels
/// (<c>L;ABDAB;lat;lon;8;</c>, a <see cref="LabelPoint"/>) and the boundary lines
/// (<c>T;COPs;lat;lon;</c>, a <see cref="StaticBoundaryGroup"/> — the same format as .hartcc/.lartcc, DUMMY
/// separators included). In A the <c>T;</c> lines were «malformed»: 5 041 of them on the master of
/// 22 September 2026, all the boundaries of FRA.artcc and FRA-gates.artcc.
/// </summary>
public abstract class ElementoArtcc;

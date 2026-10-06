using Vipi.Sectorfile.Validazione;

namespace Vipi.Sectorfile.Shared;

/// <summary>
/// Le distanze che usa il validatore, per chi ragiona sulla stessa geometria fuori dal motore (le etichette calcolate
/// del Lab, slice 14d): le stesse formule, così una soglia vuol dire la stessa cosa nei due posti.
/// </summary>
public static class Distanze
{
    public const double MetriPerNm = 1852;

    /// <summary>La distanza fra due punti, in metri (piano locale: sotto le decine di miglia l'errore non conta).</summary>
    public static double Metri(Coordinate a, Coordinate b) => Validatore.Metri(a, b);

    /// <summary>La distanza di un punto da un segmento, in metri.</summary>
    public static double MetriDalSegmento(Coordinate p, Coordinate a, Coordinate b) => ControlloDellaTerra.MetriDalSegmento(p, a, b);

    public static double Nm(Coordinate a, Coordinate b) => Metri(a, b) / MetriPerNm;

    public static double NmDalSegmento(Coordinate p, Coordinate a, Coordinate b) => MetriDalSegmento(p, a, b) / MetriPerNm;
}

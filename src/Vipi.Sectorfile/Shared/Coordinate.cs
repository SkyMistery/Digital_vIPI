namespace Vipi.Sectorfile.Shared;

/// <summary>
/// Immutable value type — canonical representation is decimal degrees.
/// Serialised to/from DMS by <see cref="CoordinateConverter"/>.
/// Positive latitude = North, positive longitude = East.
/// </summary>
public readonly struct Coordinate : IEquatable<Coordinate>
{
    public double LatitudeDeg { get; }

    public double LongitudeDeg { get; }

    public Coordinate(double latitudeDeg, double longitudeDeg)
    {
        LatitudeDeg = latitudeDeg;
        LongitudeDeg = longitudeDeg;
    }

    public bool Equals(Coordinate other)
        => LatitudeDeg.Equals(other.LatitudeDeg) && LongitudeDeg.Equals(other.LongitudeDeg);

    public override bool Equals(object? obj) => obj is Coordinate other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(LatitudeDeg, LongitudeDeg);

    public static bool operator ==(Coordinate left, Coordinate right) => left.Equals(right);

    public static bool operator !=(Coordinate left, Coordinate right) => !left.Equals(right);

    public override string ToString()
        => $"{LatitudeDeg.ToString(System.Globalization.CultureInfo.InvariantCulture)}, " +
           $"{LongitudeDeg.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
}

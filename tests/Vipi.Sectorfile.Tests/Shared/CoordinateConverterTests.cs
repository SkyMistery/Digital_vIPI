using System.Collections.Concurrent;
using Vipi.Sectorfile.Shared;
using Xunit;

namespace Vipi.Sectorfile.Shared.Tests;

/// <summary>
/// Phase 1 coverage for <see cref="CoordinateConverter"/>. Test IDs map to
/// DEVELOPMENT_PLAN §1.x / §32.x and TEST_MATRIX.
/// </summary>
public class CoordinateConverterTests
{
    private const double Tol = 1e-6;

    // §1.1 — Parse dotted DMS latitude N
    [Fact]
    public void Parse_DottedDms_LatitudeNorth()
    {
        var c = CoordinateConverter.Parse("N041.48.01.000");
        Assert.Equal(41.800278, c.LatitudeDeg, 6);
    }

    // §1.2 — Parse dotted DMS latitude S
    [Fact]
    public void Parse_DottedDms_LatitudeSouth()
    {
        var c = CoordinateConverter.Parse("S015.30.00.000");
        Assert.Equal(-15.5, c.LatitudeDeg, 6);
    }

    // §1.3 — Parse dotted DMS longitude E
    [Fact]
    public void Parse_DottedDms_LongitudeEast()
    {
        var c = CoordinateConverter.Parse("E012.14.20.000");
        Assert.Equal(12.238889, c.LongitudeDeg, 6);
    }

    // §1.4 — Parse dotted DMS longitude W
    [Fact]
    public void Parse_DottedDms_LongitudeWest()
    {
        var c = CoordinateConverter.Parse("W079.00.00.000");
        Assert.Equal(-79.0, c.LongitudeDeg, 6);
    }

    // §1.5 — Compact DMS == dotted DMS (§1.1)
    [Fact]
    public void Parse_CompactDms_EqualsDotted()
    {
        var dotted = CoordinateConverter.Parse("N041.48.01.000");
        var compact = CoordinateConverter.Parse("N0414801000");
        Assert.Equal(dotted.LatitudeDeg, compact.LatitudeDeg, Tol);
    }

    // §1.6 — Parse decimal positive
    [Fact]
    public void Parse_DecimalPositive()
    {
        var c = CoordinateConverter.Parse("41.80027778");
        Assert.Equal(41.80027778, c.LatitudeDeg, 8);
    }

    // §1.7 — Parse decimal negative
    [Fact]
    public void Parse_DecimalNegative()
    {
        var c = CoordinateConverter.Parse("-15.5");
        Assert.Equal(-15.5, c.LatitudeDeg, Tol);
    }

    // §1.8 — Fix reference resolvable
    [Fact]
    public void Parse_FixReference_Resolvable()
    {
        var expected = new Coordinate(41.8, 12.5);
        var resolver = new FakeFixResolver { ["ABDAB"] = expected };

        var c = CoordinateConverter.Parse("ABDAB", resolver);
        Assert.Equal(expected, c);
    }

    // §1.9 — Fix reference not resolvable → exception
    [Fact]
    public void Parse_FixReference_Unresolvable_Throws()
    {
        var resolver = new FakeFixResolver();
        var ex = Assert.Throws<CoordinateParseException>(() => CoordinateConverter.Parse("ABDAB", resolver));
        Assert.Equal("Unresolved fix reference: ABDAB", ex.Message);
    }

    // §1.10 — Empty token → exception
    [Fact]
    public void Parse_EmptyToken_Throws()
        => Assert.Throws<CoordinateParseException>(() => CoordinateConverter.Parse(""));

    // §1.11 — Null token → exception
    [Fact]
    public void Parse_NullToken_Throws()
        => Assert.Throws<CoordinateParseException>(() => CoordinateConverter.Parse(null!));

    // §1.12 — Unrecognised token → exception (lowercase/symbols are not a valid format)
    [Fact]
    public void Parse_UnrecognisedToken_Throws()
        => Assert.Throws<CoordinateParseException>(() => CoordinateConverter.Parse("foo_bar"));

    // §1.18 — Fix reference with digits (e.g. APT.fix "BC404") → resolvable
    [Fact]
    public void Parse_FixReference_WithDigits_Resolvable()
    {
        var expected = new Coordinate(45.0, 9.0);
        var resolver = new FakeFixResolver { ["BC404"] = expected };
        Assert.Equal(expected, CoordinateConverter.Parse("BC404", resolver));
    }

    // §1.19 — Fix reference starting with a cardinal letter then letters+digits ("EA400").
    // Not DMS: the char after the cardinal is a letter, not a digit. Resolvable as a fix.
    [Fact]
    public void Parse_FixReference_CardinalLetterPrefix_Resolvable()
    {
        var expected = new Coordinate(46.0, 9.05);
        var resolver = new FakeFixResolver { ["EA400"] = expected };
        Assert.Equal(expected, CoordinateConverter.Parse("EA400", resolver));
    }

    // §1.20 — Digit-bearing fix reference, unresolvable → CoordinateParseException (not silent).
    [Fact]
    public void Parse_FixReference_WithDigits_Unresolvable_Throws()
    {
        var ex = Assert.Throws<CoordinateParseException>(
            () => CoordinateConverter.Parse("BC404", new FakeFixResolver()));
        Assert.Equal("Unresolved fix reference: BC404", ex.Message);
    }

    // §1.13 — Round-trip dotted DMS (latitude token)
    [Fact]
    public void RoundTrip_DottedDms_Latitude()
    {
        const string input = "N041.48.01.000";
        var c = CoordinateConverter.Parse(input);
        Assert.Equal(input, CoordinateConverter.LatitudeToDottedDms(c.LatitudeDeg));
    }

    // §1.13 — Round-trip dotted DMS (longitude token)
    [Fact]
    public void RoundTrip_DottedDms_Longitude()
    {
        const string input = "E012.14.20.000";
        var c = CoordinateConverter.Parse(input);
        Assert.Equal(input, CoordinateConverter.LongitudeToDottedDms(c.LongitudeDeg));
    }

    // §1.14 — ToDottedDms zero-pads longitude degrees to 3 digits
    [Fact]
    public void Format_Longitude_ZeroPadsThreeDigits()
        => Assert.Equal("E012.14.20.000", CoordinateConverter.LongitudeToDottedDms(12.238889));

    // §1.15 — ToDottedDms zero-pads latitude degrees to 3 digits
    [Fact]
    public void Format_Latitude_ZeroPadsThreeDigits()
        => Assert.Equal("N041.48.01.000", CoordinateConverter.LatitudeToDottedDms(41.80027778));

    // §1.16 — Leading/trailing whitespace trimmed
    [Fact]
    public void Parse_TrimsWhitespace()
    {
        var c = CoordinateConverter.Parse("  N041.48.01.000  ");
        Assert.Equal(41.800278, c.LatitudeDeg, 6);
    }

    // §1.17 — Concurrency: 100 parallel threads, no race / consistent results
    [Fact]
    public void Parse_IsThreadSafe()
    {
        var results = new ConcurrentBag<double>();
        Parallel.For(0, 100, _ =>
        {
            var c = CoordinateConverter.Parse("N041.48.01.000");
            results.Add(c.LatitudeDeg);
        });

        Assert.Equal(100, results.Count);
        Assert.All(results, lat => Assert.Equal(41.800278, lat, 6));
    }

    // §32.1 — Parse("ABDAB", null) → exception
    [Fact]
    public void Parse_FixReference_NullNavaids_Throws()
        => Assert.Throws<CoordinateParseException>(() => CoordinateConverter.Parse("ABDAB", null));

    // §32.2 — Parse("ABDAB", resolver) → resolved
    [Fact]
    public void Parse_FixReference_WithResolver_Resolves()
    {
        var expected = new Coordinate(45.0, 9.0);
        var resolver = new FakeFixResolver { ["ABDAB"] = expected };
        Assert.Equal(expected, CoordinateConverter.Parse("ABDAB", resolver));
    }

    // §32.3 — Compact DMS longitude with cardinal
    [Fact]
    public void Parse_CompactDms_Longitude()
    {
        var c = CoordinateConverter.Parse("E0124820000");
        Assert.Equal(12 + (48 / 60.0) + (20 / 3600.0), c.LongitudeDeg, 6);
    }

    // §32.4 — Compact DMS latitude S → negative
    [Fact]
    public void Parse_CompactDms_LatitudeSouth_Negative()
    {
        var c = CoordinateConverter.Parse("S0414801000");
        Assert.True(c.LatitudeDeg < 0);
        Assert.Equal(-41.800278, c.LatitudeDeg, 6);
    }

    // §32.5 — Round-trip of all numeric formats: precision loss < 1 mm (~9e-9 deg)
    [Theory]
    [InlineData("N041.48.01.000")]
    [InlineData("S015.30.00.000")]
    [InlineData("E012.14.20.000")]
    [InlineData("W079.00.00.000")]
    public void RoundTrip_NumericFormats_SubMillimetrePrecision(string input)
    {
        bool isLat = input[0] is 'N' or 'S';
        var c = CoordinateConverter.Parse(input);

        string formatted = isLat
            ? CoordinateConverter.LatitudeToDottedDms(c.LatitudeDeg)
            : CoordinateConverter.LongitudeToDottedDms(c.LongitudeDeg);

        var reparsed = CoordinateConverter.Parse(formatted);
        double original = isLat ? c.LatitudeDeg : c.LongitudeDeg;
        double after = isLat ? reparsed.LatitudeDeg : reparsed.LongitudeDeg;

        // 1 mm ≈ 9e-9 degrees of latitude.
        Assert.True(Math.Abs(original - after) < 9e-9, $"precision loss too large for {input}");
    }

    /// <summary>In-test <see cref="IFixResolver"/> backed by a dictionary.</summary>
    private sealed class FakeFixResolver : IFixResolver
    {
        private readonly Dictionary<string, Coordinate> _map = new(StringComparer.Ordinal);

        public Coordinate this[string ident] { set => _map[ident] = value; }

        public bool TryResolve(string ident, out Coordinate position)
            => _map.TryGetValue(ident, out position);
    }
}

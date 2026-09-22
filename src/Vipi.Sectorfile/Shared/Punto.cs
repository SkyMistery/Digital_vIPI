namespace Vipi.Sectorfile.Shared;

/// <summary>
/// Un punto del sector come lo scrive un file: per <b>coordinate</b> (DMS puntato, DMS compatto, decimale) o per
/// <b>nome</b> (<c>AMSOR;AMSOR;</c>) — il tipo unico del punto (carta F2 §2.3, slice 4).
/// </summary>
/// <remarks>
/// <para>Aurora ammette il nome ovunque ci sia una coppia lat/lon, e lo risolve nei cataloghi dichiarati
/// dall'<c>.isc</c>. A non lo modellava: le 417 righe per nome di <c>.tfl</c>, <c>.sid</c> e <c>.mva</c> erano
/// «Skipping malformed line», e in un <c>.tfl</c> una di loro <b>chiudeva il settore</b> — i vertici dopo
/// finivano fuori dal record (<c>libb_es_ctr.tfl</c>: 72 righe).</para>
/// <para>Il nome sta in DUE campi, e di solito sono uguali. Quando non lo sono (<c>ALPHA SOUTH;ALPHA SUOTH</c>,
/// un refuso; <c>MC905;MC904</c>) Aurora prende la latitudine dal primo e la longitudine dal secondo: il punto li
/// tiene tutti e due, perché riscriverne uno solo cambierebbe il punto. Dirlo è compito del validatore.</para>
/// <para>Nome o coordinata: un campo che comincia con un emisfero seguito da una cifra, o con una cifra o un
/// segno e senza lettere, è una coordinata (<c>2NM NORTH LUCERA</c>, un VRP, è un nome) — se non si legge, è una coordinata sbagliata, non un nome (<c>N047.44.75.000</c>
/// resta un errore). Nell'albero nessun nome di punto comincia così (carta F2 §7).</para>
/// </remarks>
public readonly struct Punto : IEquatable<Punto>
{
    private Punto(Coordinate? posizione, string? nome, string? nomeLongitudine)
    {
        Posizione = posizione;
        Nome = nome;
        NomeLongitudine = nomeLongitudine;
    }

    /// <summary>Le coordinate, o null se il punto è dato per nome.</summary>
    public Coordinate? Posizione { get; }

    /// <summary>Il nome scritto nel campo della latitudine, o null se il punto è dato per coordinate.</summary>
    public string? Nome { get; }

    /// <summary>Il nome scritto nel campo della longitudine: di solito uguale a <see cref="Nome"/>.</summary>
    public string? NomeLongitudine { get; }

    /// <summary>Vero se il punto è dato per nome e va risolto nei cataloghi.</summary>
    public bool PerNome => Posizione is null;

    /// <summary>Un punto per coordinate.</summary>
    public static Punto Da(Coordinate posizione) => new(posizione, null, null);

    /// <summary>Un punto per nome; <paramref name="nomeLongitudine"/> assente = lo stesso nome nei due campi.</summary>
    public static Punto Nominato(string nome, string? nomeLongitudine = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        return new(null, nome.Trim(), (nomeLongitudine ?? nome).Trim());
    }

    public static implicit operator Punto(Coordinate posizione) => Da(posizione);

    /// <summary>Legge i due campi di un punto: coordinate (vedi <see cref="CoordinateConverter.ParsePair"/>) o nome.</summary>
    /// <exception cref="CoordinateParseException">Campo vuoto, o coordinata che non si legge.</exception>
    public static Punto Leggi(string campoLat, string campoLon)
    {
        if (string.IsNullOrWhiteSpace(campoLat) || string.IsNullOrWhiteSpace(campoLon))
        {
            throw new CoordinateParseException("Coordinate token is null or empty.");
        }

        string lat = campoLat.Trim();
        string lon = campoLon.Trim();
        if (PareUnaCoordinata(lat) || PareUnaCoordinata(lon))
        {
            return Da(CoordinateConverter.ParsePair(lat, lon));
        }

        if (lat.StartsWith("//", StringComparison.Ordinal) || lon.StartsWith("//", StringComparison.Ordinal))
        {
            throw new CoordinateParseException($"Comment is not a point: {lat};{lon}");
        }

        return Nominato(lat, lon);
    }

    /// <summary>Come <see cref="Leggi"/>, senza eccezioni.</summary>
    public static bool TryLeggi(string campoLat, string campoLon, out Punto punto)
    {
        try
        {
            punto = Leggi(campoLat, campoLon);
            return true;
        }
        catch (CoordinateParseException)
        {
            punto = default;
            return false;
        }
    }

    /// <summary>I due campi da scrivere: DMS puntato (la forma del file la rimette <c>FormaDelPunto</c>) o i nomi.</summary>
    public (string Lat, string Lon) Campi()
        => Posizione is { } c
            ? (CoordinateConverter.LatitudeToDottedDms(c.LatitudeDeg), CoordinateConverter.LongitudeToDottedDms(c.LongitudeDeg))
            : (Nome!, NomeLongitudine!);

    /// <summary>La riga <c>LAT;LON;</c> del punto.</summary>
    public string Riga()
    {
        var (lat, lon) = Campi();
        return lat + ";" + lon + ";";
    }

    /// <summary>
    /// Le coordinate del punto: le sue, o quelle del nome nel <paramref name="catalogo"/>. Con due nomi diversi,
    /// come Aurora: la latitudine dal primo, la longitudine dal secondo.
    /// </summary>
    public bool TryRisolvi(IFixResolver? catalogo, out Coordinate posizione)
    {
        if (Posizione is { } c)
        {
            posizione = c;
            return true;
        }

        posizione = default;
        if (catalogo is null
            || !catalogo.TryResolve(Nome!, out var perLat)
            || !catalogo.TryResolve(NomeLongitudine!, out var perLon))
        {
            return false;
        }

        posizione = new Coordinate(perLat.LatitudeDeg, perLon.LongitudeDeg);
        return true;
    }

    // Una cifra o un segno in testa fanno una coordinata decimale solo senza lettere: i VRP dei .vfi hanno nomi come
    // `2NM NORTH LUCERA` e `5.5NM EAST LAMPEDUSA`, e i .vrt li citano (F2 slice 6: 7 righe opache prima di questa regola).
    private static bool PareUnaCoordinata(string campo)
        => ((char.IsAsciiDigit(campo[0]) || campo[0] is '-' or '+') && !campo.Any(char.IsAsciiLetter))
        || (campo.Length > 1 && "NSEWnsew".Contains(campo[0]) && char.IsAsciiDigit(campo[1]));

    public bool Equals(Punto other)
        => Nullable.Equals(Posizione, other.Posizione)
        && string.Equals(Nome, other.Nome, StringComparison.Ordinal)
        && string.Equals(NomeLongitudine, other.NomeLongitudine, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is Punto other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Posizione, Nome, NomeLongitudine);

    public static bool operator ==(Punto left, Punto right) => left.Equals(right);

    public static bool operator !=(Punto left, Punto right) => !left.Equals(right);

    public override string ToString() => Posizione?.ToString() ?? (Nome == NomeLongitudine ? Nome! : $"{Nome};{NomeLongitudine}");
}

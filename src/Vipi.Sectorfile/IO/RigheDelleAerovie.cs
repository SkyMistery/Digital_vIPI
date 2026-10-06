using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>Un punto di un tracciato: il nome com'è scritto, dove sta (null: non si trova) e la sua riga (da 1).</summary>
public sealed record PuntoDiAerovia(string Nome, Coordinate? Posizione, int Riga);

/// <summary>Un pezzo di tracciato: le righe <c>T;</c> di un'aerovia, di seguito, fino a un'interruzione.</summary>
public sealed record PezzoDiAerovia(string Aerovia, IReadOnlyList<PuntoDiAerovia> Punti);

/// <summary>Un'etichetta <c>L;</c>: la riga (da 1), il nome com'è scritto e dove sta.</summary>
public sealed record EtichettaDiAerovia(int Riga, string Nome, Coordinate? Posizione)
{
    /// <summary>Le aerovie che l'etichetta nomina: un tratto condiviso le unisce col trattino (<c>L53-P873</c>).</summary>
    public IReadOnlyList<string> Nomi { get; } = Nome.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

/// <summary>Un file di aerovie letto dalle righe: i pezzi dei tracciati, le etichette, le righe delle interruzioni.</summary>
public sealed record AerovieLette(IReadOnlyList<PezzoDiAerovia> Pezzi, IReadOnlyList<EtichettaDiAerovia> Etichette, IReadOnlyList<int> Interruzioni)
{
    /// <summary>I nomi delle aerovie, nell'ordine in cui compaiono.</summary>
    public IReadOnlyList<string> Nomi { get; } = [.. Pezzi.Select(p => p.Aerovia).Distinct(StringComparer.Ordinal)];

    /// <summary>I tratti (due punti di seguito nello stesso pezzo, tutti e due trovati) di un'aerovia.</summary>
    public IEnumerable<(PuntoDiAerovia Da, PuntoDiAerovia A)> TrattiDi(string aerovia)
        => Pezzi.Where(p => string.Equals(p.Aerovia, aerovia, StringComparison.Ordinal))
            .SelectMany(p => Enumerable.Range(0, Math.Max(0, p.Punti.Count - 1)).Select(i => (Da: p.Punti[i], A: p.Punti[i + 1])))
            .Where(t => t.Da.Posizione is not null && t.A.Posizione is not null);
}

/// <summary>
/// Le righe di un <c>.lairway</c>/<c>.hairway</c> come aerovie (lotto «Subito» slice 14): nel modello un'aerovia è un
/// record per ogni fila di righe con lo stesso nome — i pezzi, i <c>BREAK</c> e le etichette sono record diversi, in
/// parti diverse del file. Chi ragiona per aerovia (i controlli, le etichette calcolate, aggiungere e togliere) legge
/// da qui, in un modo solo.
/// </summary>
/// <remarks>
/// Un pezzo si chiude a un <c>T;BREAK</c>, a un'altra aerovia, a una riga vuota, a un commento o a un'etichetta — come
/// fa il lettore del motore, che lì chiude il record. Una riga di tag (<c>//@</c>, <c>//@@</c>) non chiude niente: i tag
/// dei tratti stanno fra un punto e l'altro.
/// </remarks>
public static class RigheDelleAerovie
{
    public const string Interruzione = "BREAK";

    public static AerovieLette Leggi(IReadOnlyList<string> righe, Func<string, Coordinate?> punto)
    {
        ArgumentNullException.ThrowIfNull(righe);
        ArgumentNullException.ThrowIfNull(punto);

        var pezzi = new List<PezzoDiAerovia>();
        var etichette = new List<EtichettaDiAerovia>();
        var interruzioni = new List<int>();
        List<PuntoDiAerovia>? aperto = null;
        string? aerovia = null;
        for (int i = 0; i < righe.Count; i++)
        {
            string riga = righe[i].Trim();
            if (Metadati.EUnTag(riga))
                continue;

            string[] campi = riga.Split(';');
            string tipo = campi[0].Trim();
            if (riga.Length == 0 || riga.StartsWith("//", StringComparison.Ordinal) || campi.Length < 4 || tipo is not ("T" or "L"))
            {
                (aperto, aerovia) = (null, null);
                continue;
            }

            string nome = campi[1].Trim();
            if (tipo == "L")
            {
                etichette.Add(new(i + 1, nome, Dove(campi[2], campi[3], punto)));
                (aperto, aerovia) = (null, null);
                continue;
            }

            if (string.Equals(nome, Interruzione, StringComparison.OrdinalIgnoreCase))
            {
                // Il pezzo dopo è della stessa aerovia, ma non è attaccato a questo.
                interruzioni.Add(i + 1);
                aperto = null;
                continue;
            }

            if (aperto is null || !string.Equals(nome, aerovia, StringComparison.Ordinal))
            {
                aperto = [];
                aerovia = nome;
                pezzi.Add(new(nome, aperto));
            }

            aperto.Add(new(campi[2].Trim(), Dove(campi[2], campi[3], punto), i + 1));
        }

        return new(pezzi, etichette, interruzioni);
    }

    /// <summary>Vero se il campo è una coordinata (<c>N045…</c>), falso se è un nome.</summary>
    public static bool Coordinata(string campo)
        => campo.Trim() is { Length: > 1 } t && "NSEWnsew".Contains(t[0], StringComparison.Ordinal) && char.IsAsciiDigit(t[1]);

    private static Coordinate? Dove(string lat, string lon, Func<string, Coordinate?> punto)
    {
        if (!Coordinata(lat))
            return punto(lat.Trim());
        try
        {
            return CoordinateConverter.ParsePair(lat.Trim(), lon.Trim());
        }
        catch (CoordinateParseException)
        {
            return null;
        }
    }
}

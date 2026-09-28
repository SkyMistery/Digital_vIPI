using System.Text.RegularExpressions;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Core.Ispezione;

/// <summary>Il fix proposto per una procedura: i punti che il nome può voler dire, dal più vicino allo scalo.</summary>
/// <param name="Radice">La parte del nome che dice il punto (<c>EKLO</c> di <c>EKLO8R</c>).</param>
/// <param name="Candidati">I punti del master che la radice può voler dire, dal più vicino allo scalo.</param>
public sealed record FixProposto(string Radice, IReadOnlyList<PuntoDelCatalogo> Candidati)
{
    /// <summary>Un candidato solo: si propone da solo (P7, «automatici»); più d'uno si sceglie.</summary>
    public bool Automatico => Candidati.Count == 1;
}

/// <summary>
/// Il fix intero proposto dal nome di una SID o di una STAR (lotto «Subito» slice 9c, «file per file» P7): il nome di
/// una procedura è il punto, un numero e una lettera (<c>EKLO8R</c>, <c>OST1E</c>); il punto si scrive intero o
/// accorciato a quattro lettere (<c>EKLOS</c> → <c>EKLO</c>) perché il nome non superi sei caratteri. Nelle SID con la
/// transizione nel nome composto (<c>SOS5A-ESI8H</c>) conta la prima parte.
/// <para>Scelta dell'agente, dalla misura sul fork: si cercano i fix, i VOR e gli NDB del master scelto — il nome
/// uguale alla radice, o un nome di cinque lettere che comincia con una radice di quattro — entro
/// <see cref="Raggio"/> dallo scalo; più vicino, prima.</para>
/// </summary>
public static partial class FixDalNome
{
    /// <summary>Quanto lontano dallo scalo si cerca, in miglia nautiche: una SID o una STAR non va oltre.</summary>
    public const double Raggio = 150;

    private static readonly HashSet<string> Cataloghi = new(StringComparer.Ordinal) { "fix", "vor", "ndb" };

    /// <summary>La radice del nome, o null se il nome non ha la forma punto-numero-lettera (militari, luoghi).</summary>
    public static string? Radice(string nome)
    {
        ArgumentNullException.ThrowIfNull(nome);
        string prima = nome.Split('-')[0].Trim().ToUpperInvariant();
        return Forma().Match(prima) is { Success: true } m ? m.Groups[1].Value : null;
    }

    /// <summary>I candidati per il nome di una procedura dello scalo; null se il nome non ha una radice.</summary>
    public static FixProposto? Di(string nome, string scalo, CatalogoDeiPunti catalogo)
    {
        ArgumentNullException.ThrowIfNull(catalogo);
        if (Radice(nome) is not { } radice)
            return null;
        var centro = catalogo.Cerca(scalo ?? "")?.Posizione;

        bool Vicino(PuntoDelCatalogo p) => centro is not { } c || Miglia(c, p.Posizione) <= Raggio;

        var candidati = catalogo.Cerca(radice) is { } uguale && Cataloghi.Contains(uguale.Catalogo) && Vicino(uguale)
            ? [uguale]
            : radice.Length is 3 or 4
                ? catalogo.Suggerisci(radice, int.MaxValue)
                          .Where(p => p.Nome.Length == 5 && p.Nome.StartsWith(radice, StringComparison.OrdinalIgnoreCase)
                                      && Cataloghi.Contains(p.Catalogo) && Vicino(p))
                          .ToList()
                : [];
        var ordinati = centro is { } da ? candidati.OrderBy(p => Miglia(da, p.Posizione)).ToList() : candidati;
        return new FixProposto(radice, ordinati);
    }

    /// <summary>La distanza in miglia nautiche (sfera, abbastanza per dire «vicino allo scalo»).</summary>
    private static double Miglia(Coordinate a, Coordinate b)
    {
        const double Gradi = Math.PI / 180;
        double dLat = (b.LatitudeDeg - a.LatitudeDeg) * Gradi;
        double dLon = (b.LongitudeDeg - a.LongitudeDeg) * Gradi;
        double h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                   + Math.Cos(a.LatitudeDeg * Gradi) * Math.Cos(b.LatitudeDeg * Gradi) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * 3440.065 * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    [GeneratedRegex(@"^([A-Z]{2,5}?)\d[A-Z]?$")]
    private static partial Regex Forma();
}

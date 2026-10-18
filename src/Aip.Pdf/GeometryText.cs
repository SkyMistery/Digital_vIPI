using System.Globalization;
using System.Text.RegularExpressions;

namespace Aip.Pdf;

/// <summary>Che cosa il lettore delle geometrie non ha capito, o ha fatto con una scelta da dire. Un codice, non una frase.</summary>
public enum GeometryIssueKind
{
    /// <summary>
    /// Parole che non sono né coordinate né frasi del vocabolario. 🔴 La guardia della F0: gli identificativi fuori
    /// standard (<c>EUC 60</c>, <c>Zona '29'</c>) saldavano un'area alla precedente senza errore. <c>Detail</c> = le parole.
    /// </summary>
    UnknownWords,

    /// <summary>All'arco o al cerchio manca un pezzo. <c>Detail</c> = <c>inizio</c>, <c>centro</c>, <c>fine</c>, <c>raggio</c>, <c>verso</c>.</summary>
    ArcIncomplete,

    /// <summary>Il testo finisce con un anello aperto (manca «to point of origin»): si chiude lo stesso, e si dice.</summary>
    NotClosed,

    /// <summary>Una coordinata fuori intervallo (gradi, primi o secondi). <c>Detail</c> = come era scritta.</summary>
    BadCoordinate,
}

public sealed record GeometryIssue(GeometryIssueKind Kind, string? Detail = null);

public sealed record GeometryReadResult(IReadOnlyList<Geometry> Geometries, IReadOnlyList<GeometryIssue> Issues);

/// <summary>
/// I limiti laterali come li scrive l'AIP — vertici, archi, cerchi, confini, «point of origin» — letti nel modello:
/// <b>archi come archi</b> (centro, raggio stampato, verso), non vertici. Puro, senza I/O, non lancia mai.
/// <para>Copiato dal lettore della F1 (<c>Vipi.Application/Coordinates/AipGeometryReader</c>; committente, 18 ottobre:
/// «copia») — il vocabolario e il modo di comporre sono gli stessi; cambia l'uscita (il modello, non i vertici) e il
/// riconoscitore delle coordinate, che qui conosce solo le forme dell'AIP (<c>44°50'06"N 007°09'12"E</c> e la forma
/// compatta <c>445006N 0070912E</c>), perché ogni altra cosa dev'essere una parola, e la guardia la deve vedere.</para>
/// <para><b>A flusso, non a righe</b>: nell'AIP una frase va a capo ovunque, anche fra il numero e l'unità del raggio.</para>
/// </summary>
public static partial class GeometryText
{
    internal enum Senso { Arco, Orario, Antiorario, Centro, FinoAlPunto, PuntoDiOrigine, AreaCircolare, Tratto, Connettivo }

    /// <summary>Il vocabolario della F1. Si confronta dalla frase più lunga, e ciò che è riconosciuto esce dal testo.</summary>
    internal static readonly IReadOnlyList<(string Frase, Senso Senso)> Vocabolario =
    [
        ("ARC OF CIRCLE", Senso.Arco),
        ("ANTI-CLOCKWISE", Senso.Antiorario),
        ("ANTICLOCKWISE", Senso.Antiorario),
        ("COUNTER-CLOCKWISE", Senso.Antiorario),
        ("COUNTERCLOCKWISE", Senso.Antiorario),
        ("CLOCKWISE", Senso.Orario),
        ("CENTRED ON", Senso.Centro),
        ("CENTERED ON", Senso.Centro),
        ("TILL POINT OF ORIGIN", Senso.PuntoDiOrigine),
        ("TO POINT OF ORIGIN", Senso.PuntoDiOrigine),
        ("POINT OF ORIGIN", Senso.PuntoDiOrigine),
        ("TILL POINT", Senso.FinoAlPunto),
        ("CIRCULAR AREA", Senso.AreaCircolare),
        ("THEN LINE JOINING POINTS", Senso.Connettivo),
        ("LINE JOINING POINTS", Senso.Connettivo),
        ("IN DIRECTION", Senso.Connettivo),
        ("DIRECTION", Senso.Connettivo),
        ("WITHIN A", Senso.Connettivo),
        ("THEN", Senso.Connettivo),
        ("IN", Senso.Connettivo),

        ("ARCO DI CERCHIO", Senso.Arco),
        ("IN SENSO ANTIORARIO", Senso.Antiorario),
        ("ANTIORARIO", Senso.Antiorario),
        ("IN SENSO ORARIO", Senso.Orario),
        ("ORARIO", Senso.Orario),
        ("CENTRATO IN", Senso.Centro),
        ("CENTRATO SU", Senso.Centro),
        ("CENTRATA IN", Senso.Centro),
        ("CENTRATA SU", Senso.Centro),
        ("CENTRATO", Senso.Centro),
        ("FINO AL PUNTO DI ORIGINE", Senso.PuntoDiOrigine),
        ("AL PUNTO DI ORIGINE", Senso.PuntoDiOrigine),
        ("PUNTO DI ORIGINE", Senso.PuntoDiOrigine),
        ("FINO AL PUNTO", Senso.FinoAlPunto),
        ("AREA CIRCOLARE", Senso.AreaCircolare),
        ("QUINDI LINEA CONGIUNGENTE I PUNTI", Senso.Connettivo),
        ("POI LINEA CONGIUNGENTE I PUNTI", Senso.Connettivo),
        ("LINEA CONGIUNGENTE I PUNTI", Senso.Connettivo),
        ("DI RAGGIO", Senso.Connettivo),
        ("QUINDI", Senso.Connettivo),
        ("POI", Senso.Connettivo),

        ("GEOGRAPHICAL BORDER", Senso.Tratto),
        ("BORDER", Senso.Tratto),
        ("BOUNDARY", Senso.Tratto),
        ("COASTLINE", Senso.Tratto),
        ("COAST", Senso.Tratto),
        ("RIVER", Senso.Tratto),
        ("ALONG", Senso.Tratto),
        ("CONFINE", Senso.Tratto),
        ("COSTA", Senso.Tratto),
        ("FIUME", Senso.Tratto),
        ("LUNGO", Senso.Tratto),

        ("-", Senso.Connettivo),
        ("–", Senso.Connettivo),
        ("—", Senso.Connettivo),
    ];

    private const RegexOptions Opzioni = RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase;

    /// <summary>Il raggio, come la F1: numero e unità subito dopo <c>radius</c>, o fra <c>within a</c> e <c>radius</c>.</summary>
    [GeneratedRegex(@"\b(?:RADIUS|(?:DI\s+)?RAGGIO(?:\s+DI)?)\s+(?<v>\d+(?:[.,]\d+)?)\s*(?<u>NM|KM|M)\b|\bWITHIN\s+A\s+(?<v>\d+(?:[.,]\d+)?)\s*(?<u>NM|KM|M)\s+RADIUS\b", Opzioni)]
    private static partial Regex RxRaggio();

    /// <summary>
    /// La coordinata dell'AIP, latitudine e longitudine insieme: <c>44°50'06"N 007°09'12"E</c> (gradi, primi, secondi
    /// coi decimali facoltativi, i simboli che i PDF scrivono in più modi) o compatta <c>445006N 0070912E</c>.
    /// </summary>
    [GeneratedRegex(
        @"(?<la>\d{2})\s*[°º˚]\s*(?<lm>\d{2})\s*['’´′]\s*(?<ls>\d{2}(?:[.,]\d+)?)\s*(?:''|[""”″])?\s*(?<lh>[NS])(?![A-Z])[\s,;]*" +
        @"(?<oa>\d{3})\s*[°º˚]\s*(?<om>\d{2})\s*['’´′]\s*(?<os>\d{2}(?:[.,]\d+)?)\s*(?:''|[""”″])?\s*(?<oh>[EW])(?![A-Z])" +
        @"|\b(?<la>\d{2})(?<lm>\d{2})(?<ls>\d{2}(?:[.,]\d+)?)\s?(?<lh>[NS])[\s,;/]*(?<oa>\d{3})(?<om>\d{2})(?<os>\d{2}(?:[.,]\d+)?)\s?(?<oh>[EW])(?![A-Z])",
        Opzioni)]
    private static partial Regex RxCoordinata();

    [GeneratedRegex(@"⟦R(?<k>\d+)⟧", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxSegnaposto();

    [GeneratedRegex(@"\s+")]
    private static partial Regex RxSpazi();

    [GeneratedRegex(@"[\s;,.:()\[\]""'“”‘’]+")]
    private static partial Regex RxSeparatori();

    private abstract record Elemento;

    private sealed record Punto(Position Valore) : Elemento;

    /// <summary>Le parole fra due punti: com'erano scritte, e in maiuscolo senza punteggiatura; il raggio se c'era.</summary>
    private sealed record Frase(string Originale, string Testo, Distance? Raggio) : Elemento;

    public static GeometryReadResult Read(string? text)
    {
        var problemi = new List<GeometryIssue>();
        if (string.IsNullOrWhiteSpace(text)) return new GeometryReadResult([], problemi);

        var elementi = Spezza(text.Replace("⟦", "").Replace("⟧", ""), problemi);
        var geometrie = Componi(elementi, problemi);
        return new GeometryReadResult(geometrie, problemi);
    }

    // ---- 1. Il flusso: punti e frasi ----

    private static List<Elemento> Spezza(string testo, List<GeometryIssue> problemi)
    {
        var raggi = new List<Distance>();
        var t = RxRaggio().Replace(testo, m =>
        {
            var u = m.Groups["u"].Value.ToUpperInvariant();
            raggi.Add(new Distance(
                double.Parse(m.Groups["v"].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture),
                u switch { "KM" => DistanceUnit.Km, "M" => DistanceUnit.M, _ => DistanceUnit.Nm }));
            return $" ⟦R{raggi.Count - 1}⟧ ";
        });

        var elementi = new List<Elemento>();
        var da = 0;
        foreach (Match m in RxCoordinata().Matches(t))
        {
            AggiungiFrase(t[da..m.Index]);
            if (Coordinata(m) is { } p) elementi.Add(new Punto(p));
            else problemi.Add(new GeometryIssue(GeometryIssueKind.BadCoordinate, m.Value.Trim()));
            da = m.Index + m.Length;
        }
        AggiungiFrase(t[da..]);
        return elementi;

        void AggiungiFrase(string pezzo)
        {
            Distance? raggio = null;
            var senza = RxSegnaposto().Replace(pezzo, s =>
            {
                raggio = raggi[int.Parse(s.Groups["k"].Value, CultureInfo.InvariantCulture)];
                return " ";
            });
            var pulito = RxSeparatori().Replace(senza, " ").Trim().ToUpperInvariant();
            if (pulito.Length == 0 && raggio is null) return;
            elementi.Add(new Frase(RxSeparatori().Replace(senza, " ").Trim(), pulito, raggio));
        }
    }

    private static Position? Coordinata(Match m)
    {
        static double N(Group g) => double.Parse(g.Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture);
        double la = N(m.Groups["la"]), lm = N(m.Groups["lm"]), ls = N(m.Groups["ls"]);
        double oa = N(m.Groups["oa"]), om = N(m.Groups["om"]), os = N(m.Groups["os"]);
        if (la > 90 || oa > 180 || lm >= 60 || om >= 60 || ls >= 60 || os >= 60) return null;
        var lat = la + lm / 60 + ls / 3600;
        var lon = oa + om / 60 + os / 3600;
        if (m.Groups["lh"].Value.Equals("S", StringComparison.OrdinalIgnoreCase)) lat = -lat;
        if (m.Groups["oh"].Value.Equals("W", StringComparison.OrdinalIgnoreCase)) lon = -lon;
        return new Position(lat, lon);
    }

    // ---- 2. Il significato delle frasi ----

    internal static (HashSet<Senso> Sensi, string Avanzo) Classifica(string frase)
    {
        var sensi = new HashSet<Senso>();
        var resto = $" {frase} ";
        foreach (var (voce, senso) in Vocabolario.OrderByDescending(v => v.Frase.Length))
        {
            var cercata = $" {voce} ";
            var i = resto.IndexOf(cercata, StringComparison.Ordinal);
            while (i >= 0)
            {
                sensi.Add(senso);
                resto = string.Concat(resto.AsSpan(0, i), " ", resto.AsSpan(i + cercata.Length - 1));
                i = resto.IndexOf(cercata, StringComparison.Ordinal);
            }
        }
        sensi.Remove(Senso.Connettivo);
        return (sensi, RxSpazi().Replace(resto, " ").Trim());
    }

    /// <summary>La frase di un tratto di confine senza le parole che legano («till point», «then», «to point of origin»).</summary>
    private static string TestoDelTratto(string originale)
    {
        var parole = originale.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        foreach (var (voce, senso) in Vocabolario.Where(v => v.Senso is Senso.FinoAlPunto or Senso.PuntoDiOrigine or Senso.Connettivo)
                     .OrderByDescending(v => v.Frase.Length))
        {
            var v = voce.Split(' ');
            for (var i = 0; i + v.Length <= parole.Count; i++)
            {
                if (Enumerable.Range(0, v.Length).All(k => parole[i + k].Equals(v[k], StringComparison.OrdinalIgnoreCase))
                    && (senso != Senso.Connettivo || i == 0 || i + v.Length == parole.Count))
                {
                    parole.RemoveRange(i, v.Length);
                    i--;
                }
            }
        }
        return string.Join(' ', parole);
    }

    private static BorderKind TipoDelTratto(string testo) =>
        testo.Contains("COAST", StringComparison.Ordinal) || testo.Contains("COSTA", StringComparison.Ordinal) ? BorderKind.Coast
        : testo.Contains("RIVER", StringComparison.Ordinal) || testo.Contains("FIUME", StringComparison.Ordinal) ? BorderKind.River
        : BorderKind.State;

    // ---- 3. Le geometrie ----

    private enum Attesa { Niente, CentroDellArco, FineDellArco, PuntoDiFine, CentroDelCerchio }

    private static List<Geometry> Componi(List<Elemento> elementi, List<GeometryIssue> problemi)
    {
        var geometrie = new List<Geometry>();
        var tratti = new List<Segment>();
        var attesa = Attesa.Niente;
        var verso = ArcDirection.Clockwise;
        var versoNoto = false;
        Distance? raggio = null;
        Position? centro = null;

        void Incompleto(string manca) => problemi.Add(new GeometryIssue(GeometryIssueKind.ArcIncomplete, manca));

        void ChiudiAnello(bool dichiarato)
        {
            if (tratti.Count == 0) return;
            if (!dichiarato) problemi.Add(new GeometryIssue(GeometryIssueKind.NotClosed));
            geometrie.Add(new RingGeometry(tratti));
            tratti = [];
        }

        // L'ultimo tratto aperto diventa l'arco: comincia dove cominciava, gira attorno al centro.
        void ArcoDallUltimo()
        {
            if (raggio is null) Incompleto("raggio");
            if (!versoNoto) Incompleto("verso");
            tratti[^1] = new ArcSegment(tratti[^1].From, centro!.Value, raggio, verso);
            raggio = null;
            centro = null;
        }

        void Vertice(Position p)
        {
            if (tratti.Count > 0 && Stesso(tratti[^1].From, p)) return;
            // Il primo vertice ripetuto in fondo è la chiusura dell'anello, scritta coi numeri.
            if (tratti.Count >= 3 && Stesso(tratti[0].From, p)) { ChiudiAnello(dichiarato: true); return; }
            tratti.Add(new LineSegment(p));
        }

        foreach (var el in elementi)
        {
            if (el is Punto p)
            {
                switch (attesa)
                {
                    case Attesa.CentroDellArco:
                        centro = p.Valore;
                        attesa = Attesa.FineDellArco;
                        continue;
                    case Attesa.FineDellArco:
                    case Attesa.PuntoDiFine:
                        ArcoDallUltimo();
                        attesa = Attesa.Niente;
                        Vertice(p.Valore);
                        continue;
                    case Attesa.CentroDelCerchio when centro is null:
                        centro = p.Valore;
                        if (raggio is { } r) EmettiCerchio(r);
                        continue;
                    case Attesa.CentroDelCerchio:
                        Incompleto("raggio");
                        attesa = Attesa.Niente;
                        centro = null;
                        break;
                }
                Vertice(p.Valore);
                continue;
            }

            var f = (Frase)el;
            var (sensi, avanzo) = Classifica(f.Testo);

            if (sensi.Contains(Senso.Tratto))
            {
                if (tratti.Count == 0) Incompleto("inizio");
                else tratti[^1] = new BorderSegment(tratti[^1].From, TipoDelTratto(f.Testo), TestoDelTratto(f.Originale));
            }
            else if (avanzo.Length > 0)
            {
                problemi.Add(new GeometryIssue(GeometryIssueKind.UnknownWords, avanzo));
            }

            var raggioDellaFrase = f.Raggio;
            if (raggioDellaFrase is not null && attesa is not Attesa.CentroDelCerchio) raggio = raggioDellaFrase;

            if (attesa is Attesa.CentroDellArco or Attesa.FineDellArco or Attesa.PuntoDiFine
                && (sensi.Contains(Senso.Orario) || sensi.Contains(Senso.Antiorario)))
            {
                verso = sensi.Contains(Senso.Antiorario) ? ArcDirection.CounterClockwise : ArcDirection.Clockwise;
                versoNoto = true;
            }

            switch (attesa)
            {
                case Attesa.FineDellArco when sensi.Contains(Senso.PuntoDiOrigine):
                    ArcoDallUltimo();
                    attesa = Attesa.Niente;
                    ChiudiAnello(dichiarato: true);
                    continue;
                case Attesa.FineDellArco when sensi.Contains(Senso.FinoAlPunto):
                    attesa = Attesa.PuntoDiFine;
                    continue;
                case Attesa.FineDellArco:
                case Attesa.PuntoDiFine:
                    Incompleto("fine");
                    attesa = Attesa.Niente;
                    break;
                case Attesa.CentroDellArco:
                    Incompleto("centro");
                    attesa = Attesa.Niente;
                    break;
                case Attesa.CentroDelCerchio when centro is not null && raggioDellaFrase is { } rc:
                    EmettiCerchio(rc);
                    raggioDellaFrase = null;
                    break;
                case Attesa.CentroDelCerchio:
                    Incompleto(centro is null ? "centro" : "raggio");
                    attesa = Attesa.Niente;
                    centro = null;
                    break;
            }

            if (sensi.Contains(Senso.AreaCircolare))
            {
                ChiudiAnello(dichiarato: false);
                attesa = Attesa.CentroDelCerchio;
                raggio = raggioDellaFrase;
                centro = null;
                continue;
            }

            if (sensi.Contains(Senso.Arco))
            {
                if (tratti.Count == 0) { Incompleto("inizio"); continue; }
                verso = sensi.Contains(Senso.Antiorario) ? ArcDirection.CounterClockwise : ArcDirection.Clockwise;
                versoNoto = sensi.Contains(Senso.Orario) || sensi.Contains(Senso.Antiorario);
                raggio = raggioDellaFrase;
                if (sensi.Contains(Senso.Centro)) attesa = Attesa.CentroDellArco;
                else Incompleto("centro");
                continue;
            }

            if (sensi.Contains(Senso.PuntoDiOrigine)) ChiudiAnello(dichiarato: true);
        }

        switch (attesa)
        {
            case Attesa.CentroDellArco: Incompleto("centro"); break;
            case Attesa.FineDellArco or Attesa.PuntoDiFine: Incompleto("fine"); break;
            case Attesa.CentroDelCerchio: Incompleto(centro is null ? "centro" : "raggio"); break;
        }

        ChiudiAnello(dichiarato: false);
        return geometrie;

        void EmettiCerchio(Distance r)
        {
            geometrie.Add(new CircleGeometry(centro!.Value, r));
            attesa = Attesa.Niente;
            raggio = null;
            centro = null;
        }
    }

    private static bool Stesso(Position a, Position b) =>
        Math.Abs(a.Lat - b.Lat) < 1e-6 && Math.Abs(a.Lon - b.Lon) < 1e-6;
}

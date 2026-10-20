using System.Globalization;
using System.Text.RegularExpressions;

namespace Aip.Pdf.Italia;

/// <summary>
/// La prosa delle SID e delle STAR convenzionali (carta F7, slice 7b): «DELIK – TR 310° (RDL 130 LMT VOR) – LMT VOR/DME to be
/// left on RDL 258 LMT VOR until 14 NM LMT DME, then turn right to join RDL 273 LMT VOR (TR 093°) bound to SUGEP» diventa
/// tratti come quelli delle tabelle. I punti con nome restano col nome; quelli senza (una radiale a una distanza DME, due
/// radiali che si incrociano, la fine di un arco DME) si <b>calcolano</b> dalle radioassistenze dell'AIP, con la declinazione
/// della stazione, e il tratto dice com'è definito (<see cref="ProcedureLeg.Computed"/>).
/// <para>
/// Si legge una frase solo se è fatta <b>tutta</b> di pezzi noti: una parola che resta fuori (una quota da lasciare,
/// «Leaving 4000 FT», un localizzatore) e la procedura resta col suo testo, senza tratti, e il perché va nelle segnalazioni.
/// Non si inventa niente.
/// </para>
/// </summary>
public static partial class ItalianAd2Prose
{
    /// <summary>Una radioassistenza per i calcoli: dove sta e la declinazione del VOR (gradi, est positivo).</summary>
    public sealed record Station(Position Position, double Variation);

    /// <summary>La pista di una SID: la soglia e la rotta vera, per «RWY heading».</summary>
    public sealed record Runway(Position Threshold, double TrueBearing);

    /// <summary>
    /// I tratti di <paramref name="procedure"/> dalla sua prosa, o il perché non si leggono. <paramref name="point"/> dà la
    /// posizione di un punto con nome (ENR 4.4, WAYPOINT LIST), <paramref name="station"/> quella di una radioassistenza,
    /// <paramref name="runway"/> la prima pista della procedura.
    /// </summary>
    public static (IReadOnlyList<ProcedureLeg> Legs, string? Problem) Decode(
        Procedure procedure, Func<string, Position?> point, Func<string, Station?> station, Runway? runway = null)
    {
        ArgumentNullException.ThrowIfNull(procedure);
        ArgumentNullException.ThrowIfNull(point);
        ArgumentNullException.ThrowIfNull(station);
        if (procedure.Kind is not (ProcedureKind.Sid or ProcedureKind.Star or ProcedureKind.Transition))
            return ([], "non è una SID, una STAR o una transizione");
        var testo = Route(procedure.Description);
        if (testo is null) return ([], "senza testo");
        // Una STAR che non dice da dove comincia («Proceed on TR 119° … bound to TAQ VOR/DME») parte dal punto del suo nome.
        var inizio = procedure.Kind == ProcedureKind.Star ? procedure.Designator.Split(' ')[0] : null;
        return new Lettore(testo, point, station, runway).Leggi(inizio);
    }

    /// <summary>
    /// Tutte le procedure di <paramref name="document"/> senza tratti, lette dalla prosa coi punti e le radioassistenze dello
    /// stesso documento (dopo le correzioni: un punto corretto a mano vale anche qui). Le altre restano come sono; quelle
    /// che non si leggono tornano col perché.
    /// </summary>
    public static (AipDocument Document, IReadOnlyList<(Procedure Procedure, string Problem)> NotRead) DecodeAll(AipDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Procedures is not { Count: > 0 } procedure) return (document, []);
        var punti = (document.Points ?? []).GroupBy(p => p.Designator, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().Position, StringComparer.Ordinal);
        // Un VOR (la declinazione è sua); una sigla con più impianti (PIS è un DVOR/DME e un locator) prende quello col VOR.
        // Anche quelli di scalo (AD 2.19: BRL, CMP, CSL non sono in ENR 4.1).
        var stazioni = (document.Navaids ?? []).Concat((document.Aerodromes ?? []).SelectMany(a => a.NavigationAids))
            .Select(n => (n.Designator, Declinazione: Variation(n.MagneticVariation), n.Position))
            .Where(n => n.Declinazione is not null)
            .GroupBy(n => n.Designator, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => new Station(g.First().Position, g.First().Declinazione!.Value), StringComparer.Ordinal);
        var nonLette = new List<(Procedure, string)>();
        var nuove = procedure.Select(p =>
        {
            if (p.Legs.Count > 0 || p.Kind is not (ProcedureKind.Sid or ProcedureKind.Star or ProcedureKind.Transition)) return p;
            var pista = (document.Aerodromes ?? []).FirstOrDefault(a => a.Icao == p.Aerodrome)?.Runways
                .FirstOrDefault(r => p.Runways.Count > 0 && r.Designator == p.Runways[0]) is { Threshold: { } soglia } r
                && Gradi(r.TrueBearing) is { } vera ? new Runway(soglia, vera) : null;
            var (tratti, problema) = Decode(p, n => punti.TryGetValue(n, out var x) ? x : null, n => stazioni.GetValueOrDefault(n), pista);
            if (problema is not null)
            {
                nonLette.Add((p, problema));
                return p;
            }
            return p with { Legs = tratti, LegsFromDescription = true };
        }).ToList();
        return (document with { Procedures = nuove }, nonLette);
    }

    /// <summary>La declinazione stampata, est positivo: <c>3° E-2010.0</c> → 3; null se non c'è.</summary>
    internal static double? Variation(string? printed)
    {
        if (printed is null || RxDeclinazione().Match(printed) is not { Success: true } m) return null;
        var gradi = double.Parse(m.Groups["g"].Value, CultureInfo.InvariantCulture);
        return m.Groups["v"].Value == "W" ? -gradi : gradi;
    }

    [GeneratedRegex(@"(?<g>\d+(?:\.\d+)?)\s*\D?\s*(?<v>[EW])\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxDeclinazione();

    /// <summary>I gradi di una rotta stampata: <c>098.49°</c> → 98,49.</summary>
    private static double? Gradi(string? printed) =>
        printed is not null && RxGradi().Match(printed) is { Success: true } m ? double.Parse(m.Value, CultureInfo.InvariantCulture) : null;

    [GeneratedRegex(@"\d+(?:\.\d+)?", RegexOptions.CultureInvariant)]
    private static partial Regex RxGradi();

    /// <summary>
    /// Il percorso dalla prosa: le righe fino alla prima che finisce col punto, senza le note fra parentesi in testa
    /// («(ATC discretion)») e senza le righe delle quote («SUGEP: 4000 FT») che stanno sotto.
    /// </summary>
    internal static string? Route(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return null;
        var righe = new List<string>();
        foreach (var grezza in description.Split('\n'))
        {
            var riga = grezza.Trim();
            if (riga.Length == 0) continue;
            if (righe.Count == 0 && (riga.StartsWith('(') || riga.StartsWith("Minimum", StringComparison.Ordinal))) continue;
            // Una riga con le quote e senza verbi è sotto il percorso; una con un verbo ne fa ancora parte («Leaving 4000 FT turn»).
            if (righe.Count > 0 && RxRigaDopo().IsMatch(riga) && !RxVerbo().IsMatch(riga)) break;
            righe.Add(riga);
            if (riga.EndsWith('.')) break;
        }
        return righe.Count == 0 ? null : Normalizza(string.Join(' ', righe));
    }

    internal static string Normalizza(string testo)
    {
        var t = testo.Replace('–', '-').Replace('—', '-');
        t = RxTrattino().Replace(t, " - ");
        t = RxNumeroSpezzato().Replace(t, "TR $1$2°");
        t = RxNm().Replace(t, "$1 NM");
        t = RxAttaccato().Replace(t, "$1 $2");
        t = RxVorDme().Replace(t, "VOR/DME");
        t = RxVorSpezzato().Replace(t, "VOR");
        t = RxRadialeColGrado().Replace(t, "RDL $1");
        t = RxTrack().Replace(t, "TR $1°");
        t = RxMca().Replace(t, " ");
        t = RxDmeAbbreviato().Replace(t, "${d} NM ${n} DME"); // «D20 OST DME», «43 NM PNZ TAC»
        t = RxIas().Replace(t, " ");
        t = RxQuotaDaRaggiungere().Replace(t, " ");
        t = RxParentesiDiNota().Replace(t, " ");
        return RxSpazi().Replace(t, " ").Trim();
    }

    private sealed class Lettore(string testo, Func<string, Position?> punto, Func<string, Station?> stazione, Runway? pista)
    {
        private readonly List<ProcedureLeg> _tratti = [];
        private Position? _qui;
        private string? _quiNome;
        private Linea? _linea;
        private (string Stazione, double Raggio)? _arco;
        private string? _virata;
        private double? _declinazione;
        private string? _problema;

        public (IReadOnlyList<ProcedureLeg>, string?) Leggi(string? inizio)
        {
            if (inizio is not null && RxPezzo().Match(testo) is var primo && !(primo.Index == 0 && primo.Groups["nome"].Success && !Parola(primo.Groups["nome"].Value)))
            {
                if ((punto(inizio) ?? stazione(inizio)?.Position) is not { } p)
                    return ([], $"la STAR non dice da dove comincia e {inizio} non è un punto dell'AIP");
                VersoUnNome(inizio, p);
            }
            var resto = new System.Text.StringBuilder();
            var da = 0;
            foreach (Match m in RxPezzo().Matches(testo))
            {
                resto.Append(testo, da, m.Index - da).Append(' ');
                da = m.Index + m.Length;
                Pezzo(m);
                if (_problema is not null) return ([], _problema);
            }
            resto.Append(testo, da, testo.Length - da);
            var avanzo = RxRiempitivo().Replace(resto.ToString(), " ").Trim();
            if (avanzo.Length > 0) return ([], $"non capito: «{avanzo}»");
            if (_arco is not null) return ([], "un arco DME senza la radiale dove finisce");
            if (_tratti.Count == 0) return ([], "nessun punto");
            return (_tratti, null);
        }

        private void Pezzo(Match m)
        {
            var g = m.Groups;
            if (g["virata"].Success)
            {
                _virata = g["virata"].Value.StartsWith('l') || g["virata"].Value.StartsWith('L') ? "L" : "R";
                return;
            }
            if (g["loc"].Success)
            {
                _problema = $"un localizzatore ({g["loc"].Value}): il punto dove si intercetta non si calcola";
                return;
            }
            if (g["lascia"].Success)
            {
                _problema = $"«{g["lascia"].Value.Trim()}»: il punto dove si lascia una quota non si calcola";
                return;
            }
            if (g["pistaH"].Success)
            {
                if (pista is null)
                {
                    _problema = "«RWY heading» senza la soglia della pista nell'AIP";
                    return;
                }
                _linea = Linea.DallaPista(pista);
                return;
            }
            if (g["lasciaR"].Success)
            {
                // «LMT VOR/DME to be left on RDL 258 LMT VOR until 14 NM LMT DME»: prima la stazione, poi fuori sulla radiale.
                var nome = g["lasciaN1"].Success ? g["lasciaN1"].Value : g["lasciaN2"].Success ? g["lasciaN2"].Value : g["lasciaN3"].Value;
                if (Stazione(nome) is not { } s) return;
                if (_qui is null || _quiNome != nome) VersoUnNome(nome, s.Position);
                var radiale = Numero(g["lasciaR"]);
                var d = Numero(g["lasciaD"]);
                if (g["lasciaN3"].Value != nome)
                {
                    _problema = $"la radiale di {nome} con la distanza di {g["lasciaN3"].Value}";
                    return;
                }
                _linea = Linea.SuRadiale(nome, s, radiale, versoFuori: true);
                Calcolato(Sphere.Destination(s.Position, radiale + s.Variation, d), $"RDL {radiale:000}/{Dist(d)} NM {nome}", $"{radiale:000}°");
                return;
            }
            if (g["arcoD"].Success)
            {
                var nome = g["arcoN"].Value;
                if (Stazione(nome) is not { } s) return;
                var d = Numero(g["arcoD"]);
                if (_qui is { } qui && Math.Abs(Sphere.DistanceNm(s.Position, qui) - d) > 0.05)
                {
                    // Dall'arco più lontano (17 NM) a quello della procedura (15 NM): sulla stessa radiale.
                    var radiale = Sphere.BearingDeg(s.Position, qui);
                    Calcolato(Sphere.Destination(s.Position, radiale, d), $"RDL {Sphere.Normalize(radiale - s.Variation):000}/{Dist(d)} NM {nome}", null);
                }
                _arco = (nome, d);
                _linea = null;
                return;
            }
            if (g["incrocioR"].Success)
            {
                var nome = g["incrocioN"].Value;
                if (Stazione(nome) is not { } s) return;
                var radiale = Numero(g["incrocioR"]);
                if (_arco is { } arco)
                {
                    // La fine dell'arco DME.
                    var c = Stazione(arco.Stazione)!;
                    var fine = Sphere.Destination(c.Position, radiale + s.Variation, arco.Raggio);
                    var definizione = $"RDL {radiale:000}/{Dist(arco.Raggio)} NM {arco.Stazione}";
                    if (_qui is { } qui)
                    {
                        var inizio = Sphere.BearingDeg(c.Position, qui);
                        var delta = Sphere.Normalize(radiale + s.Variation - inizio);
                        _tratti.Add(new ProcedureLeg
                        {
                            PathTerminator = "AF", Computed = definizione, Position = fine, ArcCenter = arco.Stazione, ArcRadiusNm = arco.Raggio,
                            Turn = delta < 180 ? "R" : "L",
                        });
                        _qui = fine;
                        _quiNome = null;
                        _virata = null;
                    }
                    else
                    {
                        // Una SID che comincia sull'arco: si parte da dove l'arco finisce.
                        Calcolato(fine, definizione, null);
                    }
                    _arco = null;
                    return;
                }
                if (_linea is null)
                {
                    _problema = $"«crossing RDL {radiale:000} {nome}» senza una rotta da incrociare";
                    return;
                }
                var altra = Linea.SuRadiale(nome, s, radiale, versoFuori: null);
                if (_linea.Incrocio(altra) is not { } p)
                {
                    _problema = $"la rotta non incrocia la RDL {radiale:000} {nome}";
                    return;
                }
                Calcolato(p, $"INT {_linea.Nome}/RDL {radiale:000} {nome}", _linea.Rotta);
                return;
            }
            if (g["puntoR"].Success)
            {
                var nome = g["puntoN"].Value;
                if (Stazione(nome) is not { } s) return;
                var radiale = Numero(g["puntoR"]);
                var d = Numero(g["puntoD"]);
                Calcolato(Sphere.Destination(s.Position, radiale + s.Variation, d), $"RDL {radiale:000}/{Dist(d)} NM {nome}", _linea?.Rotta);
                return;
            }
            if (g["lineaR"].Success || g["lineaR2"].Success)
            {
                var nome = g["lineaR"].Success ? g["lineaN"].Value : g["lineaN2"].Value;
                if (Stazione(nome) is not { } s) return;
                var radiale = g["lineaR"].Success ? Numero(g["lineaR"]) : Numero(g["lineaR2"]);
                var tr = g["lineaT"].Success ? Numero(g["lineaT"]) : g["lineaT2"].Success ? Numero(g["lineaT2"]) : (double?)null;
                bool? fuori = tr is { } t ? Math.Abs(Sphere.Normalize(t - radiale + 180) - 180) < 90 : null;
                _linea = Linea.SuRadiale(nome, s, radiale, fuori, tr);
                return;
            }
            if (g["rotta"].Success)
            {
                // «on TR 224°» da dove si è; prima del primo punto (una SID appena decollata) non disegna niente.
                var tr = Numero(g["rotta"]);
                if (_qui is { } qui)
                {
                    if ((_declinazione ?? DeclinazioneDelTesto()) is not { } v) return;
                    _linea = Linea.DaUnPunto(qui, tr, v);
                }
                else
                {
                    _linea = null;
                }
                return;
            }
            if (g["dmeD"].Success)
            {
                var nome = g["dmeN"].Value;
                if (Stazione(nome) is not { } s) return;
                var d = Numero(g["dmeD"]);
                if (_linea is null)
                {
                    _problema = $"«{Dist(d)} NM {nome} DME» senza una rotta";
                    return;
                }
                if (_linea.Stazione == nome)
                {
                    var radiale = _linea.Radiale!.Value;
                    Calcolato(Sphere.Destination(s.Position, radiale + s.Variation, d), $"RDL {radiale:000}/{Dist(d)} NM {nome}", _linea.Rotta);
                }
                else if (_linea.Distanza(s.Position, d, _qui) is { } p)
                {
                    Calcolato(p, $"{_linea.Nome}/{Dist(d)} NM {nome} DME", _linea.Rotta);
                }
                else
                {
                    _problema = $"la rotta non arriva a {Dist(d)} NM da {nome}";
                }
                return;
            }
            if (g["nome"].Success)
            {
                var nome = g["nome"].Value;
                if (Parola(nome)) return;
                var posizione = g["vor"].Success ? stazione(nome)?.Position ?? punto(nome) : punto(nome) ?? stazione(nome)?.Position;
                if (posizione is null && g["defR"].Success && stazione(g["defN"].Value) is { } s)
                    posizione = Sphere.Destination(s.Position, Numero(g["defR"]) + s.Variation, Numero(g["defD"]));
                if (posizione is null)
                {
                    _problema = $"{nome}: non è un punto né una radioassistenza dell'AIP";
                    return;
                }
                VersoUnNome(nome, posizione.Value);
            }
        }

        private void VersoUnNome(string nome, Position posizione)
        {
            if (_quiNome == nome) return;
            var primo = _tratti.Count == 0 && _qui is null;
            _tratti.Add(new ProcedureLeg
            {
                PathTerminator = primo ? "IF" : _linea?.Rotta is not null ? "CF" : "TF",
                Fix = nome,
                Course = primo ? null : _linea?.Rotta,
                Turn = primo ? null : _virata,
            });
            _virata = null;
            _qui = posizione;
            _quiNome = nome;
            if (_linea is { Stazione: null }) _linea = null;
        }

        private void Calcolato(Position p, string definizione, string? rotta)
        {
            var primo = _tratti.Count == 0 && _qui is null;
            _tratti.Add(new ProcedureLeg
            {
                PathTerminator = primo ? "IF" : "CF", Computed = definizione, Position = p,
                Course = primo ? null : rotta, Turn = primo ? null : _virata,
            });
            _virata = null;
            _qui = p;
            _quiNome = null;
        }

        private Station? Stazione(string nome)
        {
            if (stazione(nome) is { } s)
            {
                _declinazione = s.Variation;
                return s;
            }
            _problema = $"{nome}: non è una radioassistenza dell'AIP";
            return null;
        }

        private double? DeclinazioneDelTesto()
        {
            foreach (Match m in RxStazioneNelTesto().Matches(testo))
                if (stazione(m.Groups[1].Value) is { } s)
                    return s.Variation;
            _problema = "una rotta senza una radioassistenza da cui prendere la declinazione";
            return null;
        }
    }

    /// <summary>Una rotta nel piano: una radiale di una stazione, o una rotta da un punto.</summary>
    private sealed class Linea
    {
        public string? Stazione { get; private init; }
        public double? Radiale { get; private init; }
        public required string Nome { get; init; }
        public string? Rotta { get; private init; }
        private Position Origine { get; init; }
        private double Direzione { get; init; } // vera, nel verso del volo se noto
        private bool VersoNoto { get; init; }

        public static Linea SuRadiale(string stazione, Station s, double radiale, bool? versoFuori, double? tr = null) => new()
        {
            Stazione = stazione, Radiale = radiale, Nome = $"RDL {radiale:000} {stazione}", Origine = s.Position,
            Direzione = Sphere.Normalize(radiale + s.Variation + (versoFuori == false ? 180 : 0)), VersoNoto = versoFuori is not null,
            Rotta = tr is { } t ? $"{t:000}°" : versoFuori == true ? $"{radiale:000}°" : versoFuori == false ? $"{Sphere.Normalize(radiale + 180):000}°" : null,
        };

        // Dalla soglia lungo la pista: il decollo e la «RWY heading» (una pista parallela sposta di qualche centinaio di metri).
        public static Linea DallaPista(Runway pista) => new()
        {
            Nome = "RWY heading", Origine = pista.Threshold, Direzione = pista.TrueBearing, VersoNoto = true,
        };

        public static Linea DaUnPunto(Position da, double tr, double declinazione) => new()
        {
            Nome = $"TR {tr:000}°", Origine = da, Direzione = Sphere.Normalize(tr + declinazione), VersoNoto = true, Rotta = $"{tr:000}°",
        };

        // Nel piano tangente all'origine (NM): bastano per distanze di decine di miglia, sotto la precisione della prosa.
        private (double X, double Y) Piano(Position p) =>
            ((p.Lon - Origine.Lon) * 60 * Math.Cos(Origine.Lat * Math.PI / 180), (p.Lat - Origine.Lat) * 60);

        private Position DalPiano(double x, double y) =>
            new(Origine.Lat + y / 60, Origine.Lon + x / (60 * Math.Cos(Origine.Lat * Math.PI / 180)));

        private (double X, double Y) Versore => (Math.Sin(Direzione * Math.PI / 180), Math.Cos(Direzione * Math.PI / 180));

        public Position? Incrocio(Linea altra)
        {
            var (dx, dy) = Versore;
            var (ox, oy) = Piano(altra.Origine);
            var (ex, ey) = altra.Versore;
            var det = dx * -ey - dy * -ex;
            if (Math.Abs(det) < 1e-6) return null;
            var t = (ox * -ey - oy * -ex) / det;
            if (Radiale is not null && !VersoNoto && t < 0) t = -t; // una radiale si legge dalla stazione in fuori
            return DalPiano(t * dx, t * dy);
        }

        /// <summary>Dove la rotta arriva a <paramref name="d"/> NM da <paramref name="centro"/>: il primo davanti a <paramref name="qui"/>.</summary>
        public Position? Distanza(Position centro, double d, Position? qui)
        {
            var (dx, dy) = Versore;
            var (cx, cy) = Piano(centro);
            // |t·v − c|² = d²
            var b = -2 * (dx * cx + dy * cy);
            var c = cx * cx + cy * cy - d * d;
            var disc = b * b - 4 * c;
            if (disc < 0) return null;
            var t1 = (-b - Math.Sqrt(disc)) / 2;
            var t2 = (-b + Math.Sqrt(disc)) / 2;
            var tQui = 0.0;
            if (qui is { } q)
            {
                var (qx, qy) = Piano(q);
                tQui = qx * dx + qy * dy;
            }
            // Davanti, nel verso del volo: dietro non ci si torna (null, e la procedura resta in prosa).
            var t = new[] { t1, t2 }.Where(x => x > tQui - 1e-6).DefaultIfEmpty(double.NaN).Min();
            if (double.IsNaN(t)) return null;
            return DalPiano(t * dx, t * dy);
        }
    }

    private static double Numero(Group g) => double.Parse(g.Value, CultureInfo.InvariantCulture);

    private static string Dist(double d) => d.ToString("0.#", CultureInfo.InvariantCulture);

    private static bool Parola(string s) => s is "RDL" or "TR" or "NM" or "DME" or "VOR" or "ARC" or "INT" or "LOC" or "IAS" or "KT" or "MAX"
        or "ATC" or "FT" or "FL" or "RWY" or "ILS" or "NDB" or "TRL" or "MCL" or "MCA";

    private const string Nav = @"[A-Z]{2,3}";
    private const string D = @"\d+(?:\.\d+)?";

    // I pezzi, in ordine di precedenza: chi comincia prima vince, a parità vince il primo dell'alternativa.
    [GeneratedRegex(
        @"(?<lascia>\b(?:[Ll]eaving|until\s+leaving)\s+\d+\s*(?:FT|ft)\b)"
        + @"|(?<pistaH>\b(?:RWY|runway|RUNWAY)\s+heading\b(?:\s*\(\s*TR\s+\d{3}°\s*\))?)"
        + @"|(?<loc>\b[A-Z]{3,5}\s+LOC\b)"
        + @"|(?:(?<lasciaN1>" + Nav + @")\s+VOR(?:/DME)?\s+to\s+be\s+left\s+on|\bLeave\s+(?<lasciaN2>" + Nav + @")\s+VOR(?:/DME)?\s+on)\s+RDL\s+(?<lasciaR>\d{3})(?:\s+" + Nav + @"\s+VOR)?\s+until\s+(?<lasciaD>" + D + @")\s+NM\s+(?<lasciaN3>" + Nav + @")\s+DME"
        + @"|\bARC\s+(?<arcoD>" + D + @")\s+NM\s+(?<arcoN>" + Nav + @")\s+DME"
        + @"|(?:\bcrossing|\bINT\s+TR\s+\d{3}°\s*/)\s+RDL\s+(?<incrocioR>\d{3})\s+(?<incrocioN>" + Nav + @")(?:\s+VOR)?"
        + @"|\b(?:INT\s+)?RDL\s+(?<puntoR>\d{3})\s*/\s*(?<puntoD>" + D + @")\s+NM\s+(?<puntoN>" + Nav + @")\s+VOR/DME"
        + @"|\bTR\s+(?<lineaT>\d{3})°\s*\(\s*RDL\s+(?<lineaR>\d{3})\s+(?<lineaN>" + Nav + @")\s+VOR(?:\s+or\s+RDL\s+\d{3}\s+" + Nav + @"\s+VOR)?\s*\)"
        + @"|\bRDL\s+(?<lineaR2>\d{3})\s+(?<lineaN2>" + Nav + @")(?:\s+VOR(?:/DME)?)?(?:\s*\(\s*RDL\s+\d{3}\s+" + Nav + @"\s+VOR\s*\))?(?:\s*\(\s*TR\s+(?<lineaT2>\d{3})°\s*\))?"
        + @"|\bTR\s+(?<rotta>\d{3})°"
        + @"|\b(?<dmeD>" + D + @")\s+NM\s+(?<dmeN>" + Nav + @")\s+DME(?:\s*\(\s*RDL\s+\d{3}\s*/\s*" + D + @"\s+NM\s+" + Nav + @"\s+VOR/DME\s*\))?"
        + @"|\b(?<virata>[Tt]urn\s+(?:left|right))\b"
        // Un nome, e fra parentesi la sua definizione: una radiale e una distanza (serve se il punto non è nell'AIP), o
        // un'altra («NPC LOC/13 NM NPC DME») che non serve.
        + @"|\b(?<nome>[A-Z]{2,5})\b(?<vor>\s+(?:VORTAC|VOR(?:/DME)?)\b)?(?:\s*\(\s*(?:INT\s+)?RDL\s+(?<defR>\d{3})\s*/\s*(?<defD>" + D + @")\s+NM\s+(?<defN>" + Nav + @")\s+VOR/DME(?:\s+or\s+[^)]*)?\s*\)|\s*\([^)]*\))?",
        RegexOptions.CultureInvariant)]
    private static partial Regex RxPezzo();

    // Quel che può restare fra i pezzi senza dire niente.
    [GeneratedRegex(@"\b(?:then|and|proceed|on|to|bound|direct|until|till|point|via|join|joining|intercept|follow|at|from|after|take-off|continue|the|be|or|turn|left|right|crossing|maintain|From|Proceed|Turn|Join|At|Then|And|After|Crossing)\b|[,;.\-()]",
        RegexOptions.CultureInvariant)]
    private static partial Regex RxRiempitivo();

    [GeneratedRegex(@"\b(" + Nav + @")\s+VOR", RegexOptions.CultureInvariant)]
    private static partial Regex RxStazioneNelTesto();

    // Una riga dopo il percorso: le quote, le note, la WAYPOINT LIST.
    [GeneratedRegex(@"^(?:REMARK|Waypoint|R\s?emain|Minimum|\(A\d|\*)|\b\d{3,5}\s?FT\b|\bFL\s?\d{2,3}\b|:\s*(?:\d|FL)|^[A-Z0-9 /–-]+:", RegexOptions.CultureInvariant)]
    private static partial Regex RxRigaDopo();

    [GeneratedRegex(@"\b(?:turn|proceed|join|bound|until|then|direct|crossing|intercept)\b")]
    private static partial Regex RxVerbo();

    [GeneratedRegex(@"\bD(?<d>\d+(?:\.\d+)?)\s+(?<n>[A-Z]{2,3})\s+DME\b|\b(?<d>\d+(?:\.\d+)?)\s+NM\s+(?<n>[A-Z]{2,3})\s+TAC\b")]
    private static partial Regex RxDmeAbbreviato();

    [GeneratedRegex(@"\bV\s+OR\b")]
    private static partial Regex RxVorSpezzato();

    [GeneratedRegex(@"\bRDL\s+(\d{3})°")]
    private static partial Regex RxRadialeColGrado();

    [GeneratedRegex(@"\btrack\s+(\d{3})°")]
    private static partial Regex RxTrack();

    [GeneratedRegex(@"\((?:MCA|MCL)\s+\d+\s*(?:ft|FT)\s*\)|\(not\s+before[^)]*\)")]
    private static partial Regex RxMca();

    [GeneratedRegex(@"\s+-\s*|\s*-\s+")]
    private static partial Regex RxTrattino();

    [GeneratedRegex(@"\bTR\s?(\d{2})\s(\d)°")]
    private static partial Regex RxNumeroSpezzato();

    [GeneratedRegex(@"(\d)\s?NM\b")]
    private static partial Regex RxNm();

    [GeneratedRegex(@"\b(TR|RDL)(\d)")]
    private static partial Regex RxAttaccato();

    [GeneratedRegex(@"VOR\s*/\s*DME|VOR\s+DME")]
    private static partial Regex RxVorDme();

    [GeneratedRegex(@"\(\s*IAS\s+MAX\s+\d+\s*KT\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex RxIas();

    [GeneratedRegex(@"\bto\s+be\s+(?:reached|crossed)\s+at\s+or\s+above\s+\d+\s*(?:ft|FT)\b")]
    private static partial Regex RxQuotaDaRaggiungere();

    [GeneratedRegex(@"\((?:ATC\s+discretion|to\s+be\s+used\s+only)[^)]*\)", RegexOptions.IgnoreCase)]
    private static partial Regex RxParentesiDiNota();

    [GeneratedRegex(@"\s+")]
    private static partial Regex RxSpazi();
}

/// <summary>
/// Distanze, rotte e punti di arrivo sulla sfera, come <c>Aip.Aurora.Sphere</c>: copiate qui perché l'importer si porta via
/// da solo (carta F6 §3) e non dipende dal converter.
/// </summary>
internal static class Sphere
{
    private const double RaggioNm = 6371008.8 / 1852.0;
    private const double Rad = Math.PI / 180.0;

    public static double DistanceNm(Position a, Position b)
    {
        var dLat = (b.Lat - a.Lat) * Rad;
        var dLon = (b.Lon - a.Lon) * Rad;
        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(a.Lat * Rad) * Math.Cos(b.Lat * Rad) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * RaggioNm * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    public static double BearingDeg(Position a, Position b)
    {
        var dLon = (b.Lon - a.Lon) * Rad;
        var y = Math.Sin(dLon) * Math.Cos(b.Lat * Rad);
        var x = Math.Cos(a.Lat * Rad) * Math.Sin(b.Lat * Rad) - Math.Sin(a.Lat * Rad) * Math.Cos(b.Lat * Rad) * Math.Cos(dLon);
        return Normalize(Math.Atan2(y, x) / Rad);
    }

    public static Position Destination(Position from, double bearingDeg, double distanceNm)
    {
        var d = distanceNm / RaggioNm;
        var b = bearingDeg * Rad;
        var lat1 = from.Lat * Rad;
        var lon1 = from.Lon * Rad;
        var lat2 = Math.Asin(Math.Sin(lat1) * Math.Cos(d) + Math.Cos(lat1) * Math.Sin(d) * Math.Cos(b));
        var lon2 = lon1 + Math.Atan2(Math.Sin(b) * Math.Sin(d) * Math.Cos(lat1), Math.Cos(d) - Math.Sin(lat1) * Math.Sin(lat2));
        return new Position(lat2 / Rad, (lon2 / Rad + 540) % 360 - 180);
    }

    public static double Normalize(double deg)
    {
        var r = deg % 360.0;
        return r < 0 ? r + 360.0 : r;
    }
}

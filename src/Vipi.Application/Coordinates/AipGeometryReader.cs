using System.Globalization;
using System.Text.RegularExpressions;

namespace Vipi.Application.Coordinates;

/// <summary>
/// Legge i limiti laterali scritti come li scrive l'AIP — vertici, archi, cerchi, «point of origin» — e li riduce
/// ad aree di vertici. Puro, deterministico, senza I/O (carta <c>docs/feature/2026-09-18-f1-archi-convertitore.md</c>
/// §3).
///
/// <para><b>A flusso, non a righe</b>: nell'AIP una frase va a capo ovunque, anche fra il numero e l'unità del
/// raggio. Il testo diventa una sequenza di <b>punti</b> e di <b>frasi</b>; le coordinate le riconosce il codice
/// di <see cref="CoordinateParser"/>, non un secondo riconoscitore.</para>
///
/// <para><b>Il vocabolario è una tabella</b> (<see cref="Vocabolario"/>): una variante nuova vista in un PDF è
/// una riga in più, non un <c>if</c>.</para>
/// </summary>
public static class AipGeometryReader
{
    /// <summary>Che cosa vuol dire una frase del vocabolario.</summary>
    internal enum Senso
    {
        Arco,
        Orario,
        Antiorario,
        Centro,
        FinoAlPunto,
        PuntoDiOrigine,
        AreaCircolare,

        /// <summary>Parole che legano e non dicono niente di geometrico: si tolgono e basta.</summary>
        Connettivo,
    }

    /// <summary>
    /// Il vocabolario. ⚠️ Si confronta <b>dalla frase più lunga</b>, e ciò che è riconosciuto esce dal testo:
    /// così <c>ANTI-CLOCKWISE</c> non lascia dietro un <c>CLOCKWISE</c>, e <c>TILL POINT OF ORIGIN</c> non si legge
    /// come <c>TILL POINT</c>.
    /// </summary>
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
        ("-", Senso.Connettivo),
    ];

    /// <summary>
    /// ⚠️ Le frasi che ACCENDONO il lettore. Solo frasi lunghe, mai una parola sola: <c>then</c>, <c>point</c>,
    /// <c>from</c> stanno anche nei commenti degli AOD, e un sectorfile commentato non deve cambiare lettore
    /// (carta F1 §8).
    /// </summary>
    private static readonly string[] FrasiDiAccensione = ["ARC OF CIRCLE", "POINT OF ORIGIN", "CIRCULAR AREA"];

    private const RegexOptions Opzioni = RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture;

    /// <summary>
    /// Il raggio: <b>numero + unità subito dopo</b> <c>radius</c>, o fra <c>within a</c> e <c>radius</c>. Le unità
    /// si leggono SOLO qui: altrove <c>M</c> e <c>NM</c> non vogliono dire niente, e il <c>17</c> del raggio letto
    /// come un angolo era proprio il difetto di partenza (carta F1 §0).
    /// </summary>
    private static readonly Regex RxRaggio = new(
        @"\bRADIUS\s+(?<v>\d+(?:[.,]\d+)?)\s*(?<u>NM|KM|M)\b|\bWITHIN\s+A\s+(?<v>\d+(?:[.,]\d+)?)\s*(?<u>NM|KM|M)\s+RADIUS\b",
        Opzioni);

    private static readonly Regex RxSegnaposto = new(@"^⟦R(?<k>\d+)⟧$", Opzioni);

    private static readonly Regex RxSpazi = new(@"\s+", Opzioni);

    private static readonly char[] Separatori = [' ', '\t', ';', ',', '/'];

    /// <summary>Il testo parla la lingua dell'AIP? Solo con una frase lunga del vocabolario.</summary>
    public static bool Riconosce(string? testo)
    {
        if (string.IsNullOrWhiteSpace(testo)) return false;
        var t = RxSpazi.Replace(testo.ToUpperInvariant(), " ");
        foreach (var f in FrasiDiAccensione)
            if (t.Contains(f, StringComparison.Ordinal)) return true;
        return false;
    }

    /// <summary>Legge il testo. Non lancia mai: ciò che non si capisce esce come segnalazione.</summary>
    /// <param name="puntiPerGrado">Densità di archi e cerchi (<see cref="ArcGeometry.DensitaBase"/> di base).</param>
    public static CoordinateReadResult Leggi(string? testo, double puntiPerGrado = ArcGeometry.DensitaBase)
    {
        if (string.IsNullOrWhiteSpace(testo)) return CoordinateReadResult.Vuoto;

        var segnalazioni = new List<CoordinateIssue>();
        var originali = testo.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var (elementi, righeConPunti) = Spezza(originali, segnalazioni);
        var aree = Componi(elementi, originali, puntiPerGrado, segnalazioni);
        return new CoordinateReadResult(aree, segnalazioni, righeConPunti, originali.Length);
    }

    // ---- 1. Il flusso: punti e frasi, con la riga da cui vengono ----

    private abstract record Elemento(int Riga);

    private sealed record Punto(int Riga, (double Lat, double Lon) Valore) : Elemento(Riga);

    /// <summary>Le parole fra due punti, già in maiuscolo; <paramref name="RaggioNm"/> se contenevano un raggio.</summary>
    private sealed record Frase(int Riga, string Testo, double? RaggioNm) : Elemento(Riga);

    private static (List<Elemento> Elementi, int RigheConPunti) Spezza(string[] originali, List<CoordinateIssue> segnalazioni)
    {
        var quante = originali.Length;
        if (quante > CoordinateParser.MaxRighe)
        {
            segnalazioni.Add(new CoordinateIssue(CoordinateIssueKind.TroppeRighe, 0, "",
                $"{quante} > {CoordinateParser.MaxRighe}"));
            quante = CoordinateParser.MaxRighe;
        }

        // Il raggio si toglie PRIMA di cercare le coordinate, sul testo intero: va a capo fra numero e unità
        // («radius\n15.0 NM»). Al suo posto resta un segnaposto, più gli a capo che conteneva — così i numeri
        // di riga di quello che segue restano veri.
        var raggi = new List<double>();
        var intero = string.Join('\n', originali, 0, quante).ToUpperInvariant();
        intero = RxRaggio.Replace(intero, m =>
        {
            raggi.Add(InNm(m.Groups["v"].Value, m.Groups["u"].Value));
            return $" ⟦R{raggi.Count - 1}⟧ " + new string('\n', m.Value.Count(c => c == '\n'));
        });

        var elementi = new List<Elemento>();
        var angoli = new List<CoordinateParser.Angolo>();
        var rigaAngoli = 0;
        int? senzaEmisfero = null;
        var parole = new List<string>();
        var rigaParole = 0;
        double? raggio = null;
        var righeConPunti = new HashSet<int>();

        void ChiudiFrase()
        {
            if (parole.Count == 0 && raggio is null) return;
            elementi.Add(new Frase(rigaParole, string.Join(' ', parole), raggio));
            parole.Clear();
            raggio = null;
        }

        void ChiudiAngoli()
        {
            if (angoli.Count == 0) return;
            var testoRiga = originali[rigaAngoli - 1].Trim();
            for (var k = 0; k + 1 < angoli.Count; k += 2)
            {
                if (!CoordinateParser.ProvaCoppia(angoli[k], angoli[k + 1], out var p, out var avviso))
                {
                    segnalazioni.Add(new CoordinateIssue(avviso ?? CoordinateIssueKind.RigaNonLetta, rigaAngoli, testoRiga));
                    continue;
                }
                if (avviso is { } a) segnalazioni.Add(new CoordinateIssue(a, rigaAngoli, testoRiga));
                ChiudiFrase();
                elementi.Add(new Punto(rigaAngoli, p));
                righeConPunti.Add(rigaAngoli);
            }
            if (angoli.Count % 2 != 0)
                segnalazioni.Add(new CoordinateIssue(CoordinateIssueKind.AngoloSpaiato, rigaAngoli, testoRiga));
            angoli.Clear();
            senzaEmisfero = null;
        }

        var righe = intero.Split('\n');
        for (var i = 0; i < righe.Length; i++)
        {
            var riga = CoordinateParser.RiscriviFormeSpezzate(CoordinateParser.NormalizzaSegni(righe[i]));
            foreach (var pezzo in riga.Split(Separatori, StringSplitOptions.RemoveEmptyEntries))
            {
                var segnaposto = RxSegnaposto.Match(pezzo);
                if (segnaposto.Success)
                {
                    ChiudiAngoli();
                    if (parole.Count == 0) rigaParole = i + 1;
                    raggio = raggi[int.Parse(segnaposto.Groups["k"].Value, CultureInfo.InvariantCulture)];
                    continue;
                }

                // Il punto in fondo è la fine della frase («…till point of origin.»), non un decimale.
                var token = pezzo.Trim('"', '\'', '[', ']', '(', ')').TrimEnd('.');
                if (token.Length == 0) continue;

                if (CoordinateParser.ProvaPezzo(token, angoli, ref senzaEmisfero))
                {
                    if (angoli.Count == 1) rigaAngoli = i + 1;
                    continue;
                }

                ChiudiAngoli();
                if (parole.Count == 0 && raggio is null) rigaParole = i + 1;
                parole.Add(token);
            }
        }
        ChiudiAngoli();
        ChiudiFrase();

        return (elementi, righeConPunti.Count);
    }

    private static double InNm(string valore, string unita)
    {
        var v = double.Parse(valore.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture);
        return unita switch
        {
            "KM" => v * 1000.0 / 1852.0,
            "M" => v / 1852.0,
            _ => v,
        };
    }

    // ---- 2. Il significato delle frasi ----

    /// <summary>
    /// I sensi di una frase. Le frasi del vocabolario si cercano dalla più lunga e si TOLGONO dal testo man
    /// mano, così una più corta non ritrova un pezzo di una più lunga. Quello che avanza non è riconosciuto.
    /// </summary>
    internal static (HashSet<Senso> Sensi, string Avanzo) Classifica(string frase)
    {
        var sensi = new HashSet<Senso>();
        var resto = $" {RxSpazi.Replace(frase, " ").Trim()} ";
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
        return (sensi, resto.Trim());
    }

    // ---- 3. Le aree ----

    private enum Attesa { Niente, CentroDellArco, FineDellArco, PuntoDiFine, CentroDelCerchio }

    private static List<CoordinateArea> Componi(
        List<Elemento> elementi, string[] originali, double densita, List<CoordinateIssue> segnalazioni)
    {
        var aree = new List<CoordinateArea>();
        var vertici = new List<(double Lat, double Lon)>();
        var attesa = Attesa.Niente;
        var orario = true;
        double? raggio = null;
        (double Lat, double Lon) centro = default;
        var rigaCentro = 0;

        void ChiudiArea(bool dichiarata)
        {
            if (vertici.Count == 0) return;
            if (vertici.Count > 2 && Stesso(vertici[0], vertici[^1])) vertici.RemoveAt(vertici.Count - 1);
            aree.Add(new CoordinateArea(null, vertici, AnelloChiuso: dichiarata));
            vertici = [];
        }

        void Arco((double Lat, double Lon) fine, bool finoAllOrigine)
        {
            var arco = ArcGeometry.Arco(vertici[^1], fine, centro, orario, raggio ?? 0, densita);
            if (raggio is not null && arco.RaggioIncoerente)
                segnalazioni.Add(new CoordinateIssue(CoordinateIssueKind.RaggioIncoerente, rigaCentro,
                    originali[rigaCentro - 1].Trim(),
                    arco.ScartoNm.ToString("0.00", CultureInfo.InvariantCulture)));

            // Il primo punto dell'arco è l'ultimo vertice, che c'è già; l'ultimo è la fine, che entra come
            // vertice — tranne quando è il punto di origine, che è la chiusura dell'anello.
            var fino = finoAllOrigine ? arco.Punti.Count - 1 : arco.Punti.Count;
            for (var k = 1; k < fino; k++) vertici.Add(arco.Punti[k]);
        }

        foreach (var el in elementi)
        {
            if (el is Punto p)
            {
                switch (attesa)
                {
                    case Attesa.CentroDellArco:
                        centro = p.Valore;
                        rigaCentro = p.Riga;
                        attesa = Attesa.FineDellArco;
                        break;
                    case Attesa.PuntoDiFine:
                        Arco(p.Valore, finoAllOrigine: false);
                        attesa = Attesa.Niente;
                        break;
                    case Attesa.CentroDelCerchio:
                        centro = p.Valore;
                        rigaCentro = p.Riga;
                        if (raggio is { } r) { EmettiCerchio(r); }
                        break;
                    default:
                        if (vertici.Count == 0 || !Stesso(vertici[^1], p.Valore)) vertici.Add(p.Valore);
                        break;
                }
                continue;
            }

            var f = (Frase)el;
            var (sensi, _) = Classifica(f.Testo);
            if (f.RaggioNm is { } rf) raggio = rf;

            if (attesa == Attesa.FineDellArco)
            {
                if (sensi.Contains(Senso.PuntoDiOrigine) && vertici.Count > 0)
                {
                    Arco(vertici[0], finoAllOrigine: true);
                    attesa = Attesa.Niente;
                    ChiudiArea(dichiarata: true);
                    continue;
                }
                if (sensi.Contains(Senso.FinoAlPunto)) { attesa = Attesa.PuntoDiFine; continue; }
            }

            if (attesa == Attesa.CentroDelCerchio && f.RaggioNm is { } rc && rigaCentro > 0)
            {
                EmettiCerchio(rc);
                continue;
            }

            if (sensi.Contains(Senso.AreaCircolare))
            {
                ChiudiArea(dichiarata: false);
                attesa = Attesa.CentroDelCerchio;
                raggio = f.RaggioNm;
                rigaCentro = 0;
                continue;
            }

            if (sensi.Contains(Senso.Arco) && vertici.Count > 0)
            {
                orario = !sensi.Contains(Senso.Antiorario);
                raggio = f.RaggioNm;
                attesa = sensi.Contains(Senso.Centro) ? Attesa.CentroDellArco : Attesa.Niente;
                continue;
            }

            if (sensi.Contains(Senso.PuntoDiOrigine)) ChiudiArea(dichiarata: true);
        }

        ChiudiArea(dichiarata: false);
        return aree;

        void EmettiCerchio(double r)
        {
            aree.Add(new CoordinateArea(null, ArcGeometry.Cerchio(centro, r, densita), AnelloChiuso: true));
            attesa = Attesa.Niente;
            raggio = null;
            rigaCentro = 0;
        }
    }

    private static bool Stesso((double Lat, double Lon) a, (double Lat, double Lon) b) =>
        Math.Abs(a.Lat - b.Lat) < 1e-6 && Math.Abs(a.Lon - b.Lon) < 1e-6;
}

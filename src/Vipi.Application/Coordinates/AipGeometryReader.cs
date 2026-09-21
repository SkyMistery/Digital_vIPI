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

        /// <summary>
        /// Confine di stato, costa, fiume: una geometria che il testo NOMINA ma non dà. Si unisce con una retta
        /// e si dice (carta F1 §3.6): la geometria vera sta in <c>GEO/itgeo.geo</c>, ed è lavoro del Lab (F6).
        /// </summary>
        TrattoNonDisegnabile,

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

        // L'italiano: nell'AIP sta quasi sempre in forma BILINGUE mescolata («centrato in/centered on»), e la
        // barra spezza le due lingue in parole. Il raggio, lì, è solo nella metà inglese; in italiano puro è
        // «di raggio 5.0 NM» (vedi RxRaggio).
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

        // Le geometrie nominate e non date. Basta UNA di queste parole perché la frase intera sia il tratto:
        // «Italian northern geographical border till point», «line at 500 m from coast», «lungo il fiume Po».
        ("GEOGRAPHICAL BORDER", Senso.TrattoNonDisegnabile),
        ("BORDER", Senso.TrattoNonDisegnabile),
        ("BOUNDARY", Senso.TrattoNonDisegnabile),
        ("COASTLINE", Senso.TrattoNonDisegnabile),
        ("COAST", Senso.TrattoNonDisegnabile),
        ("RIVER", Senso.TrattoNonDisegnabile),
        ("ALONG", Senso.TrattoNonDisegnabile),
        ("CONFINE", Senso.TrattoNonDisegnabile),
        ("COSTA", Senso.TrattoNonDisegnabile),
        ("FIUME", Senso.TrattoNonDisegnabile),
        ("LUNGO", Senso.TrattoNonDisegnabile),

        // I separatori fra i vertici, quando stanno da soli: «0091308E - 453115N». Il trattino lungo arriva
        // dai PDF di ENR 2.1.1.1 al posto di quello corto.
        ("-", Senso.Connettivo),
        ("–", Senso.Connettivo),
        ("—", Senso.Connettivo),
    ];

    /// <summary>
    /// ⚠️ Le frasi che ACCENDONO il lettore. Solo frasi lunghe, mai una parola sola: <c>then</c>, <c>point</c>,
    /// <c>from</c> stanno anche nei commenti degli AOD, e un sectorfile commentato non deve cambiare lettore
    /// (carta F1 §8).
    /// </summary>
    private static readonly string[] FrasiDiAccensione =
    [
        "ARC OF CIRCLE", "POINT OF ORIGIN", "CIRCULAR AREA",
        "ARCO DI CERCHIO", "PUNTO DI ORIGINE", "AREA CIRCOLARE",
    ];

    private const RegexOptions Opzioni = RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture;

    /// <summary>
    /// Il raggio: <b>numero + unità subito dopo</b> <c>radius</c>, o fra <c>within a</c> e <c>radius</c>. Le unità
    /// si leggono SOLO qui: altrove <c>M</c> e <c>NM</c> non vogliono dire niente, e il <c>17</c> del raggio letto
    /// come un angolo era proprio il difetto di partenza (carta F1 §0).
    ///
    /// <para>In italiano: <c>di raggio 5.0 NM</c>, <c>di raggio di 60 NM</c>. Nel bilingue <c>di raggio/then arc
    /// … radius 5.0 NM</c> dopo «raggio» c'è la barra e non un numero: il raggio lo prende la metà inglese.</para>
    /// </summary>
    private static readonly Regex RxRaggio = new(
        @"\b(?:RADIUS|(?:DI\s+)?RAGGIO(?:\s+DI)?)\s+(?<v>\d+(?:[.,]\d+)?)\s*(?<u>NM|KM|M)\b" +
        @"|\bWITHIN\s+A\s+(?<v>\d+(?:[.,]\d+)?)\s*(?<u>NM|KM|M)\s+RADIUS\b",
        Opzioni);

    private static readonly Regex RxSegnaposto = new(@"^⟦R(?<k>\d+)⟧$", Opzioni);

    private static readonly Regex RxSpazi = new(@"\s+", Opzioni);

    private static readonly char[] Separatori = [' ', '\t', ';', ',', '/'];

    /// <summary>
    /// Il tetto dei punti che archi e cerchi possono GENERARE in un ingresso (carta F1 §8). Come
    /// <see cref="CoordinateParser.MaxRighe"/> non difende il disco ma il circuito Blazor, da cui passano i punti:
    /// 20 000 sono cinque cerchi a 10 pt/°, e più di tutti gli archi dell'AIP messi insieme a 1 pt/°.
    /// </summary>
    public const int MaxPuntiGenerati = 20_000;

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

        void AggiungiParola(string parola, int riga)
        {
            ChiudiAngoli();
            if (parole.Count == 0 && raggio is null) rigaParole = riga;
            parole.Add(parola);
        }

        // 🔴 LA GUARDIA DI F0: nell'AIP ogni coordinata DICHIARA l'emisfero (`452630N`, `44°51'24"N`, anche
        // staccato: `24" N`). Un numero che non lo dichiara non è una coordinata ma una parola: `Zona '29'`,
        // `EUC 60`, il `500` di «line at 500 m from coast». Letti come angoli, saldavano un'area alla precedente
        // senza errore. L'angolo senza emisfero resta SOSPESO per un pezzo, il tempo di vedere se il pezzo dopo
        // è la sua lettera; se no torna parola.
        (string Testo, int Riga)? sospeso = null;
        void RendiParolaIlSospeso()
        {
            if (sospeso is not { } s) return;
            sospeso = null;
            angoli.RemoveAt(angoli.Count - 1);
            senzaEmisfero = null;
            AggiungiParola(s.Testo, s.Riga);
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
                    RendiParolaIlSospeso();
                    ChiudiAngoli();
                    if (parole.Count == 0) rigaParole = i + 1;
                    raggio = raggi[int.Parse(segnaposto.Groups["k"].Value, CultureInfo.InvariantCulture)];
                    continue;
                }

                // Il punto in fondo è la fine della frase («…till point of origin.»), non un decimale.
                var token = pezzo.Trim('"', '\'', '[', ']', '(', ')').TrimEnd('.');
                if (token.Length == 0) continue;

                var emisfero = token.Length == 1 && token[0] is 'N' or 'S' or 'E' or 'W';
                if (!emisfero) RendiParolaIlSospeso();

                if (CoordinateParser.ProvaPezzo(token, angoli, ref senzaEmisfero))
                {
                    if (angoli.Count == 1) rigaAngoli = i + 1;
                    sospeso = senzaEmisfero is null ? null : (token, i + 1);
                    continue;
                }

                AggiungiParola(token, i + 1);
            }
        }
        RendiParolaIlSospeso();
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
        var rigaArco = 0;
        var generati = 0;
        var tettoDetto = false;

        // Oltre il tetto non si butta niente: l'arco si disegna rado, e lo si dice UNA volta.
        bool OltreIlTetto(int punti)
        {
            if (generati + punti <= MaxPuntiGenerati) return false;
            if (!tettoDetto)
            {
                tettoDetto = true;
                segnalazioni.Add(new CoordinateIssue(CoordinateIssueKind.TroppiPunti, 0, "",
                    MaxPuntiGenerati.ToString(CultureInfo.InvariantCulture)));
            }
            return true;
        }

        void ChiudiArea(bool dichiarata)
        {
            if (vertici.Count == 0) return;
            if (vertici.Count > 2 && Stesso(vertici[0], vertici[^1])) vertici.RemoveAt(vertici.Count - 1);
            aree.Add(new CoordinateArea(null, vertici, AnelloChiuso: dichiarata));
            vertici = [];
        }

        void Arco((double Lat, double Lon) fine, bool finoAllOrigine)
        {
            // Senza raggio l'arco si disegna lo stesso: passa per gli estremi, e il raggio serviva solo a
            // controllarli. Ma lo si dice.
            if (raggio is null) Incompleto(rigaArco, "raggio");
            var arco = ArcGeometry.Arco(vertici[^1], fine, centro, orario, raggio ?? 0, densita);
            if (OltreIlTetto(arco.Punti.Count))
                arco = ArcGeometry.Arco(vertici[^1], fine, centro, orario, raggio ?? 0, ArcGeometry.DensitaMinima);
            generati += arco.Punti.Count;
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
                        continue;

                    // La fine senza «till point» davanti si prende lo stesso: il punto è lì, al suo posto.
                    case Attesa.FineDellArco:
                    case Attesa.PuntoDiFine:
                        Arco(p.Valore, finoAllOrigine: false);
                        attesa = Attesa.Niente;
                        continue;

                    case Attesa.CentroDelCerchio when rigaCentro == 0:
                        centro = p.Valore;
                        rigaCentro = p.Riga;
                        if (raggio is { } r) EmettiCerchio(r);
                        continue;

                    // Un secondo punto mentre il cerchio aspetta il raggio: il raggio non arriverà più.
                    case Attesa.CentroDelCerchio:
                        Incompleto(rigaArco, "raggio");
                        attesa = Attesa.Niente;
                        break;
                }
                if (vertici.Count == 0 || !Stesso(vertici[^1], p.Valore)) vertici.Add(p.Valore);
                continue;
            }

            var f = (Frase)el;
            var (sensi, avanzo) = Classifica(f.Testo);

            // ⚠️ Nulla si scarta in silenzio. Un tratto si dice con la frase intera (le parole attorno a
            // «border» — «Italian northern geographical» — sono il suo nome, non un avanzo da segnalare a parte).
            if (sensi.Contains(Senso.TrattoNonDisegnabile))
                segnalazioni.Add(new CoordinateIssue(CoordinateIssueKind.TrattoNonDisegnabile, f.Riga, Originale(f.Riga), f.Testo));
            else if (avanzo.Length > 0)
                segnalazioni.Add(new CoordinateIssue(CoordinateIssueKind.FraseNonRiconosciuta, f.Riga, Originale(f.Riga), avanzo));

            if (f.RaggioNm is { } rf) raggio = rf;

            // 🔴 Una frase può CHIUDERE una cosa e APRIRNE un'altra: fra due cerchi di seguito le parole
            // «within a 1.0 NM radius. Circular area centered on» sono una frase sola. Chiudere e poi saltare il
            // resto (`continue`) perdeva l'apertura, e il cerchio dopo usciva come un'area di UN punto — trovato
            // dal test del tetto. Quindi chi chiude prosegue; e il raggio, se l'ha speso la chiusura, non passa
            // alla cosa nuova.
            var raggioDellaFrase = f.RaggioNm;

            // Quello che si stava aspettando, e una frase al suo posto.
            switch (attesa)
            {
                case Attesa.FineDellArco when sensi.Contains(Senso.PuntoDiOrigine) && vertici.Count > 0:
                    Arco(vertici[0], finoAllOrigine: true);
                    attesa = Attesa.Niente;
                    ChiudiArea(dichiarata: true);
                    break;
                case Attesa.FineDellArco when sensi.Contains(Senso.FinoAlPunto):
                    attesa = Attesa.PuntoDiFine;
                    continue;
                case Attesa.FineDellArco:
                case Attesa.PuntoDiFine:
                    Incompleto(rigaArco, "fine");
                    attesa = Attesa.Niente;
                    break;
                case Attesa.CentroDellArco:
                    Incompleto(rigaArco, "centro");
                    attesa = Attesa.Niente;
                    break;
                case Attesa.CentroDelCerchio when rigaCentro > 0 && f.RaggioNm is { } rc:
                    EmettiCerchio(rc);
                    raggioDellaFrase = null;
                    break;
                case Attesa.CentroDelCerchio:
                    Incompleto(rigaArco, rigaCentro == 0 ? "centro" : "raggio");
                    attesa = Attesa.Niente;
                    break;
            }

            if (sensi.Contains(Senso.AreaCircolare))
            {
                ChiudiArea(dichiarata: false);
                attesa = Attesa.CentroDelCerchio;
                raggio = raggioDellaFrase;
                rigaCentro = 0;
                rigaArco = f.Riga;
                continue;
            }

            if (sensi.Contains(Senso.Arco))
            {
                rigaArco = f.Riga;
                if (vertici.Count == 0) { Incompleto(rigaArco, "inizio"); continue; }
                orario = !sensi.Contains(Senso.Antiorario);
                raggio = raggioDellaFrase;
                if (sensi.Contains(Senso.Centro)) attesa = Attesa.CentroDellArco;
                else Incompleto(rigaArco, "centro");
                continue;
            }

            if (sensi.Contains(Senso.PuntoDiOrigine)) ChiudiArea(dichiarata: true);
        }

        // Il testo finisce mentre un arco o un cerchio aspettava ancora un pezzo.
        switch (attesa)
        {
            case Attesa.CentroDellArco: Incompleto(rigaArco, "centro"); break;
            case Attesa.FineDellArco or Attesa.PuntoDiFine: Incompleto(rigaArco, "fine"); break;
            case Attesa.CentroDelCerchio: Incompleto(rigaArco, rigaCentro == 0 ? "centro" : "raggio"); break;
        }

        ChiudiArea(dichiarata: false);
        return aree;

        string Originale(int riga) => riga >= 1 && riga <= originali.Length ? originali[riga - 1].Trim() : "";

        void Incompleto(int riga, string manca) =>
            segnalazioni.Add(new CoordinateIssue(CoordinateIssueKind.ArcoIncompleto, riga, Originale(riga), manca));

        void EmettiCerchio(double r)
        {
            var cerchio = ArcGeometry.Cerchio(centro, r, densita);
            if (OltreIlTetto(cerchio.Count)) cerchio = ArcGeometry.Cerchio(centro, r, ArcGeometry.DensitaMinima);
            generati += cerchio.Count;
            aree.Add(new CoordinateArea(null, cerchio, AnelloChiuso: true));
            attesa = Attesa.Niente;
            raggio = null;
            rigaCentro = 0;
        }
    }

    private static bool Stesso((double Lat, double Lon) a, (double Lat, double Lon) b) =>
        Math.Abs(a.Lat - b.Lat) < 1e-6 && Math.Abs(a.Lon - b.Lon) < 1e-6;
}

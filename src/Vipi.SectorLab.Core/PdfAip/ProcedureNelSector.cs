using System.Text.RegularExpressions;
using Aip;
using Aip.Aurora;

namespace Vipi.SectorLab.Core.PdfAip;

/// <summary>
/// Le righe dei gesti sulle procedure (carta F7, slice 7e): una STAR dell'AIP come voce di uno <c>.str</c>, una SID (con la sua
/// transizione, se c'è) come voce di un <c>.sid</c> <b>col tracciato</b> (D2), i punti che il sector non ha nella sezione
/// del loro scalo di <c>APT.fix</c>.
/// <para>La voce è un blocco (§M): <c>//@"NOME" nav=… note="AIP 2610"</c>, <c>//@START</c>, la testata, un punto per riga
/// (<c>PUNTO;PUNTO;</c>, il primo di una STAR con la coda del nome come fa il sector: <c>ARBUM;ARBUM;3R;</c>) e sopra ogni
/// punto con una quota o una velocità il suo <c>//@@"PUNTO" alt=+FL190 spd=-220</c> (D3), <c>//@END "NOME"</c>.</para>
/// <para>Il nome nella convenzione del file (D1): quante lettere tengono i nomi dello scalo (<c>XIBR5A</c> 4, <c>IRK7A</c>
/// 3, <c>BAGIX3R</c> intere), la più frequente fra le voci che si riconoscono.</para>
/// </summary>
public static partial class ProcedureNelSector
{
    /// <summary>Il file dei punti di scalo del sector: una sezione per scalo, aperta dal commento <c>//LIRF</c>.</summary>
    public const string FileDeiPuntiDiScalo = "APT.fix";

    [GeneratedRegex(@"^(?<b>[A-Z]+)\s?(?<c>\d{1,2}[A-Z])$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxNome();

    [GeneratedRegex(@"\(\d+\)|\*", RegexOptions.CultureInvariant)]
    private static partial Regex RxRimandi();

    [GeneratedRegex(@"^//\s*(?<icao>L[A-Z]{3})\b", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxSezioneDiScalo();

    /// <summary>
    /// Quante lettere tengono i nomi del file: la lunghezza più frequente fra le coppie nome del sector → designatore dell'AIP
    /// in cui il nome è l'inizio del designatore; <paramref name="diBase"/> se non ce n'è nessuna. «Intere» è il nome intero.
    /// </summary>
    public static int Lettere(IEnumerable<(string Sector, string Aip)> coppie, int diBase = 5)
    {
        ArgumentNullException.ThrowIfNull(coppie);
        var lunghezze = new List<int>();
        foreach (var (sector, aip) in coppie)
        {
            var s = RxNome().Match(sector.Trim());
            var a = RxNome().Match(aip.Replace(" ", "", StringComparison.Ordinal));
            if (!s.Success || !a.Success || !a.Groups["b"].Value.StartsWith(s.Groups["b"].Value, StringComparison.Ordinal)) continue;
            // Un nome intero vale «tutte le lettere», qualunque sia la sua lunghezza.
            lunghezze.Add(s.Groups["b"].Value.Length == a.Groups["b"].Value.Length ? int.MaxValue : s.Groups["b"].Value.Length);
        }
        return lunghezze.Count == 0 ? diBase : lunghezze.GroupBy(l => l).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).First().Key;
    }

    /// <summary>Il nome del sector di un designatore dell'AIP con quelle lettere: <c>XIBRI 5A</c>, 4 → <c>XIBR5A</c>.</summary>
    public static string Nome(string designatore, int lettere)
    {
        ArgumentNullException.ThrowIfNull(designatore);
        var a = RxNome().Match(designatore.Replace(" ", "", StringComparison.Ordinal));
        if (!a.Success) return designatore.Replace(" ", "", StringComparison.Ordinal);
        var b = a.Groups["b"].Value;
        return b[..Math.Min(lettere, b.Length)] + a.Groups["c"].Value;
    }

    /// <summary>La coda del nome (<c>3R</c>): il sector la scrive sul primo punto di una STAR.</summary>
    public static string? Coda(string designatore) =>
        RxNome().Match(designatore.Replace(" ", "", StringComparison.Ordinal)) is { Success: true } a ? a.Groups["c"].Value : null;

    /// <summary>
    /// La quota di un tratto come valore di <c>alt=</c> (§M): <c>+FL110</c>, <c>+4000</c>, <c>=4000</c> (AT), <c>FL120/FL170</c>
    /// (fra due), com'è stampata senza «FT» e senza i rimandi. Null se il tratto non ne ha.
    /// </summary>
    public static string? Quota(string? stampata)
    {
        if (string.IsNullOrWhiteSpace(stampata)) return null;
        var t = RxRimandi().Replace(stampata, " ").Replace("FT", "", StringComparison.OrdinalIgnoreCase).Replace("FL ", "FL", StringComparison.Ordinal);
        var parti = t.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (parti.Count == 2 && parti[0].Equals("AT", StringComparison.OrdinalIgnoreCase)) return "=" + parti[1];
        // Una finestra: «-FL170» sopra e «+FL120» sotto nella cella.
        if (parti.Count == 2 && parti.FirstOrDefault(p => p.StartsWith('+')) is { } sopra && parti.FirstOrDefault(p => p.StartsWith('-')) is { } sotto)
            return $"{sopra[1..]}/{sotto[1..]}";
        var v = string.Concat(parti);
        return v.Length == 0 ? null : v;
    }

    /// <summary>La velocità come valore di <c>spd=</c>: <c>-210</c>, <c>=200</c> (AT). Null se il tratto non ne ha.</summary>
    public static string? Velocita(string? stampata)
    {
        if (string.IsNullOrWhiteSpace(stampata)) return null;
        var parti = RxRimandi().Replace(stampata, " ").Replace("KT", "", StringComparison.OrdinalIgnoreCase)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (parti.Count == 2 && parti[0].Equals("AT", StringComparison.OrdinalIgnoreCase)) return "=" + parti[1];
        var v = string.Concat(parti);
        return v.Length == 0 ? null : v;
    }

    /// <summary>La specifica di navigazione come valore di <c>nav=</c> (<c>RNAV1/RNP1</c>), dal primo tratto che la dice.</summary>
    public static string? Navigazione(IEnumerable<Procedure> pezzi)
    {
        ArgumentNullException.ThrowIfNull(pezzi);
        var stampata = pezzi.SelectMany(p => p.Legs).Select(t => t.Navigation).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
        if (stampata is null) return null;
        var v = RxRimandi().Replace(stampata, "").Replace(" ", "", StringComparison.Ordinal);
        return v.Length == 0 ? null : v;
    }

    /// <summary>Vero se la procedura è RNAV (l'8° campo del sector): la specifica dice RNAV o RNP.</summary>
    public static bool Rnav(IEnumerable<Procedure> pezzi) =>
        Navigazione(pezzi) is { } n && (n.Contains("RNAV", StringComparison.OrdinalIgnoreCase) || n.Contains("RNP", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// I punti <b>con nome</b> in fila di una o più procedure di seguito (una SID e la sua transizione): il punto comune una
    /// volta. Quelli calcolati della prosa (7b) non ci sono: non vanno cercati né portati, si scrivono come coordinate.
    /// </summary>
    public static IReadOnlyList<string> Punti(IEnumerable<Procedure> pezzi)
    {
        ArgumentNullException.ThrowIfNull(pezzi);
        var punti = new List<string>();
        foreach (var t in pezzi.SelectMany(p => p.Legs))
            if (t.Fix is { } f && (punti.Count == 0 || punti[^1] != f))
                punti.Add(f);
        return punti;
    }

    /// <summary>La testata di una STAR: <c>LIRF;16L:16R;ELKA3A;;;;;1;</c>.</summary>
    public static string TestataDellaStar(string icao, IReadOnlyList<string> piste, string nome, bool rnav) =>
        $"{icao};{string.Join(':', piste)};{nome};;;;;{(rnav ? "1;" : "")}";

    /// <summary>La testata di una SID: <c>LIRF;25;XIBR5A; ; ;0;XIBRI;1;</c> (il campo 7 è l'ultimo punto).</summary>
    /// <param name="campiVuoti">Il file scrive le sue SID coi campi 4-6 vuoti (<c>LIRN;06;ALAX7G;;;;ALAXI;1;</c>, come
    /// <c>lirn.sid</c>) invece che con gli spazi e lo 0 (<c>lirf.sid</c>): la SID nuova fa come le altre.</param>
    public static string TestataDellaSid(string icao, IReadOnlyList<string> piste, string nome, string ultimo, bool rnav, bool campiVuoti = false) =>
        campiVuoti
            ? $"{icao};{string.Join(':', piste)};{nome};;;;{ultimo};{(rnav ? "1;" : "")}"
            : $"{icao};{string.Join(':', piste)};{nome}; ; ;0;{ultimo};{(rnav ? "1;" : "")}";

    /// <summary>Vero se la maggior parte delle SID del file ha i campi 4-6 vuoti (<c>;;;;</c>): la forma per una SID nuova.</summary>
    public static bool CampiVuoti(IEnumerable<object> recordDelFile)
    {
        var sid = recordDelFile.OfType<Vipi.Sectorfile.Models.SidProcedure>().Where(s => s.Runway.Trim() != "MAPS").ToList();
        return sid.Count(s => s.Field4.Length == 0 && s.Field5.Length == 0 && s.DefaultVisible is null) * 2 > sid.Count;
    }

    /// <summary>
    /// Il blocco intero di una procedura nuova: dichiarazione, <c>//@START</c>, testata, punti coi loro <c>//@@</c>,
    /// <c>//@END</c>. <paramref name="coda"/>: la coda del nome sul primo punto (le STAR).
    /// </summary>
    public static IReadOnlyList<string> Blocco(string nome, string testata, IReadOnlyList<Procedure> pezzi, string nota, string? coda,
        Func<string, Position?>? posizione = null)
    {
        ArgumentNullException.ThrowIfNull(pezzi);
        return [Dichiarazione(nome, pezzi, nota), "//@START", testata, .. Corpo(pezzi, coda, posizione), $"//@END \"{nome}\""];
    }

    /// <summary>La dichiarazione del blocco: la specifica, il punto finale per una SID, la nota del ciclo.</summary>
    public static string Dichiarazione(string nome, IReadOnlyList<Procedure> pezzi, string nota)
    {
        var chiavi = new List<string>();
        if (Navigazione(pezzi) is { } nav) chiavi.Add($"nav={Valore(nav)}");
        chiavi.Add($"note={Valore(nota)}");
        return $"//@\"{nome}\" {string.Join(' ', chiavi)}";
    }

    /// <summary>
    /// I punti in fila, ognuno col suo <c>//@@</c> se ha una quota o una velocità. I punti senza nome della prosa (7b) come
    /// coordinate (<c>N038.52.03.360;E015.58.11.280;</c>, come il sector disegna già le convenzionali); un arco DME (<c>AF</c>)
    /// un punto ogni 5° attorno alla sua radioassistenza, che <paramref name="posizione"/> trova (senza, solo la fine).
    /// </summary>
    public static IReadOnlyList<string> Corpo(IReadOnlyList<Procedure> pezzi, string? coda, Func<string, Position?>? posizione = null)
    {
        ArgumentNullException.ThrowIfNull(pezzi);
        var tratti = pezzi.SelectMany(p => p.Legs).ToList();
        var righe = new List<string>();
        string? ultimo = null;
        Position? qui = null;
        foreach (var t in tratti)
        {
            if (t.Fix is { } nome)
            {
                if (ultimo == nome) continue;
                // Quota e velocità del tratto che arriva al punto (il primo con quel punto, nell'ordine dei pezzi).
                var tratto = tratti.FirstOrDefault(x => x.Fix == nome && (x.Altitude is not null || x.Speed is not null));
                var chiavi = new List<string>();
                if (Quota(tratto?.Altitude) is { } alt) chiavi.Add($"alt={Valore(alt)}");
                if (Velocita(tratto?.Speed) is { } spd) chiavi.Add($"spd={Valore(spd)}");
                if (chiavi.Count > 0) righe.Add($"//@@\"{nome}\" {string.Join(' ', chiavi)}");
                righe.Add(righe.Count(r => !r.StartsWith("//", StringComparison.Ordinal)) == 0 && coda is not null ? $"{nome};{nome};{coda};" : $"{nome};{nome};");
                ultimo = nome;
                qui = posizione?.Invoke(nome);
            }
            else if (t.Position is { } p)
            {
                if (t.PathTerminator is "AF" or "RF" && qui is { } da && (t.ArcCenterPosition ?? (t.ArcCenter is { } c ? posizione?.Invoke(c) : null)) is { } centro)
                    righe.AddRange(Arco(da, p, centro, orario: t.Turn == "R").Select(x => AuroraText.Coordinates(x) + ";"));
                righe.Add(AuroraText.Coordinates(p) + ";");
                ultimo = null;
                qui = p;
            }
        }
        return righe;
    }

    /// <summary>I punti in mezzo a un arco attorno a <paramref name="centro"/>, uno ogni 5° (né l'inizio né la fine).</summary>
    private static IEnumerable<Position> Arco(Position da, Position a, Position centro, bool orario)
    {
        var b0 = Sphere.BearingDeg(centro, da);
        var b1 = Sphere.BearingDeg(centro, a);
        double r0 = Sphere.DistanceNm(centro, da), r1 = Sphere.DistanceNm(centro, a);
        var ampiezza = orario ? Sphere.Normalize(b1 - b0) : Sphere.Normalize(b0 - b1);
        var passi = (int)Math.Ceiling(ampiezza / 5);
        for (var i = 1; i < passi; i++)
        {
            var f = (double)i / passi;
            yield return Sphere.Destination(centro, orario ? b0 + ampiezza * f : b0 - ampiezza * f, r0 + (r1 - r0) * f);
        }
    }

    /// <summary>Un valore di tag: fra virgolette se ha spazi (§M regola 5).</summary>
    private static string Valore(string v) => v.Contains(' ', StringComparison.Ordinal) ? $"\"{v.Replace("\"", "'", StringComparison.Ordinal)}\"" : v;

    /// <summary>La riga di un punto di scalo di <c>APT.fix</c>: <c>RN601;N040.51.28.880;E014.13.47.070;3;</c>.</summary>
    public static string RigaDelPuntoDiScalo(SignificantPoint p)
    {
        ArgumentNullException.ThrowIfNull(p);
        return $"{p.Designator};{AuroraText.Coordinates(p.Position)};3;";
    }

    /// <summary>
    /// Dove vanno le righe nuove dei punti di uno scalo in <c>APT.fix</c>: in fondo alla sezione dello scalo (dopo l'ultima
    /// riga di dati prima della sezione dopo), o — se lo scalo non ha una sezione — una sezione nuova in fondo al file. Le
    /// sostituzioni sono per numero di riga, da 1.
    /// </summary>
    public static IReadOnlyDictionary<int, IReadOnlyList<string>> NellaSezioneDelloScalo(IReadOnlyList<string> righe, string icao, IReadOnlyList<string> nuove)
    {
        ArgumentNullException.ThrowIfNull(righe);
        ArgumentNullException.ThrowIfNull(nuove);
        var inizio = -1;
        for (var i = 0; i < righe.Count; i++)
            if (RxSezioneDiScalo().Match(righe[i].Trim()) is { Success: true } m && m.Groups["icao"].Value == icao)
            {
                inizio = i;
                break;
            }
        if (inizio < 0)
        {
            var ultima = righe.Count - 1;
            return new Dictionary<int, IReadOnlyList<string>> { [ultima + 1] = [righe[ultima], .. string.IsNullOrWhiteSpace(righe[ultima]) ? Array.Empty<string>() : [""], $"//{icao}", .. nuove] };
        }
        var fine = righe.Count;
        for (var i = inizio + 1; i < righe.Count; i++)
            if (RxSezioneDiScalo().IsMatch(righe[i].Trim()))
            {
                fine = i;
                break;
            }
        // L'ultima riga di dati della sezione (non vuota, non un commento, non un tag).
        var dopo = fine - 1;
        while (dopo > inizio && (string.IsNullOrWhiteSpace(righe[dopo]) || (righe[dopo].TrimStart().StartsWith("//", StringComparison.Ordinal) && !righe[dopo].TrimStart().StartsWith("//@END", StringComparison.Ordinal))))
            dopo--;
        return new Dictionary<int, IReadOnlyList<string>> { [dopo + 1] = [righe[dopo], .. nuove] };
    }
}

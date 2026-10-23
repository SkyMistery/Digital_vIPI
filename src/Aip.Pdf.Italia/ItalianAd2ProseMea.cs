using System.Globalization;
using System.Text.RegularExpressions;

namespace Aip.Pdf.Italia;

/// <summary>
/// Segnalazioni 2611, passo 8 (committente, per ATCSim: «la 2 per tutte le STAR»): le MEA a tratti delle minime di una STAR.
/// «MEA: ENEPA – RDL 327/25NM COM VOR/DME: 6000FT; RDL327/25NM COM VOR/DME - COM VOR/DME: 5000FT» (LICB ENEPA 1T): dove la
/// MEA cambia su un punto DME che il percorso non ha già, il tratto si spezza lì (un <c>CD</c>, o <c>FD</c> se parte da un
/// punto con nome, fino a 25 NM COM, poi il tratto di prima fino a COM); e ogni tratto prende la MEA del pezzo in cui sta.
/// </summary>
public static partial class ItalianAd2Prose
{
    /// <summary>Un punto dei pezzi di MEA: dove sta, e se è un punto DME (radiale e distanza da una stazione) come.</summary>
    private sealed record PuntoDellaMea(Position Posizione, string? Nome, string? Stazione, double? Radiale, double? Dme)
    {
        public string Definizione => $"RDL {Radiale:000}/{(Dme!.Value % 1 == 0 ? Dme.Value.ToString("0", CultureInfo.InvariantCulture) : Dme.Value.ToString("0.#", CultureInfo.InvariantCulture))} NM {Stazione}";
    }

    /// <summary>
    /// I tratti della STAR con le MEA delle minime: spezzati dove la MEA cambia su un punto DME che sta su un tratto (entro
    /// mezzo miglio), e con la MEA di ogni pezzo sui tratti fra i suoi due capi. I pezzi che non si ritrovano sul percorso non
    /// cambiano niente.
    /// </summary>
    internal static IReadOnlyList<ProcedureLeg> ConLeMea(IReadOnlyList<ProcedureLeg> tratti, string? minimi, Func<string, Position?> punto, Func<string, Station?> stazione)
    {
        var pezzi = PezziDiMea(minimi, punto, stazione);
        if (pezzi.Count == 0 || tratti.Count < 2) return tratti;
        var lista = tratti.ToList();

        // Dove la MEA cambia su un punto DME senza un tratto che ci finisca: il tratto che ci passa si spezza.
        foreach (var capo in pezzi.Skip(1).Select(p => p.Da).Where(p => p.Dme is not null))
        {
            var fini = Fini(lista, punto, stazione);
            if (fini.Any(f => f is { } x && Sphere.DistanceNm(x, capo.Posizione) < 0.5)) continue;
            for (var i = 1; i < lista.Count; i++)
            {
                if (fini[i - 1] is not { } da || fini[i] is not { } a || lista[i].PathTerminator is "AF" or "RF") continue;
                var lungo = Sphere.DistanceNm(da, a);
                var avanti = Sphere.DistanceNm(da, capo.Posizione);
                var rotta = Sphere.BearingDeg(da, a);
                var scarto = avanti * Math.Sin((Sphere.BearingDeg(da, capo.Posizione) - rotta) * Math.PI / 180);
                var lungoLaRotta = avanti * Math.Cos((Sphere.BearingDeg(da, capo.Posizione) - rotta) * Math.PI / 180);
                if (Math.Abs(scarto) > 0.5 || lungoLaRotta < 0.5 || lungoLaRotta > lungo - 0.5) continue;
                var tratto = lista[i];
                lista.Insert(i, new ProcedureLeg
                {
                    PathTerminator = lista[i - 1].Fix is null ? "CD" : "FD",
                    Computed = capo.Definizione, Position = capo.Posizione, Course = tratto.Course, Turn = tratto.Turn,
                    Navaid = capo.Stazione, Radial = capo.Radiale, DmeNm = capo.Dme,
                });
                // Il tratto di prima ora parte dal punto DME, non più da un punto con nome.
                lista[i + 1] = tratto with { Turn = null, PathTerminator = tratto.PathTerminator == "FD" ? "CD" : tratto.PathTerminator };
                break;
            }
        }

        // La MEA di ogni pezzo sui tratti dal suo capo di partenza a quello d'arrivo.
        var tutte = Fini(lista, punto, stazione);
        var dopo = 0;
        foreach (var pezzo in pezzi)
        {
            var da = Indice(lista, tutte, pezzo.Da, dopo);
            if (da < 0) continue;
            var a = Indice(lista, tutte, pezzo.A, da + 1);
            if (a < 0) continue;
            for (var i = da + 1; i <= a; i++)
                lista[i] = lista[i] with { MinimumAltitude = pezzo.Quota };
            dopo = a;
        }
        return lista;
    }

    private static int Indice(List<ProcedureLeg> tratti, IReadOnlyList<Position?> fini, PuntoDellaMea p, int da)
    {
        for (var i = da; i < tratti.Count; i++)
        {
            if (p.Nome is { } n && tratti[i].Fix == n) return i;
            if (fini[i] is { } f && Sphere.DistanceNm(f, p.Posizione) < 0.5) return i;
        }
        return -1;
    }

    private static List<Position?> Fini(List<ProcedureLeg> tratti, Func<string, Position?> punto, Func<string, Station?> stazione) =>
        [.. tratti.Select(t => t.Position ?? (t.Fix is { } f ? punto(f) ?? stazione(f)?.Position : null))];

    /// <summary>I pezzi «da – a: quota» dopo «MEA:» (o «MEL/MEA:»), coi due capi ritrovati.</summary>
    private static List<(PuntoDellaMea Da, PuntoDellaMea A, string Quota)> PezziDiMea(string? minimi, Func<string, Position?> punto, Func<string, Station?> stazione)
    {
        var pezzi = new List<(PuntoDellaMea, PuntoDellaMea, string)>();
        if (minimi is null) return pezzi;
        foreach (Match blocco in RxBloccoDiMea().Matches(minimi.Replace('\n', ' ')))
        {
            foreach (var testo in blocco.Groups["pezzi"].Value.Split(';'))
            {
                var m = RxPezzoDiMea().Match(RxFraParentesi().Replace(testo, " "));
                if (!m.Success) continue;
                if (Capo(m.Groups["da"].Value, punto, stazione) is not { } da || Capo(m.Groups["a"].Value, punto, stazione) is not { } a) continue;
                pezzi.Add((da, a, RxSpazi().Replace(m.Groups["q"].Value, " ").Trim()));
            }
        }
        return pezzi;
    }

    /// <summary>Un capo di un pezzo di MEA: un punto DME («RDL 327/25NM COM VOR/DME», «INT RDL 321 BAR VOR/16 NM BAR DME») o un nome.</summary>
    private static PuntoDellaMea? Capo(string testo, Func<string, Position?> punto, Func<string, Station?> stazione)
    {
        if (RxCapoDme().Match(testo) is { Success: true } d)
        {
            var nome = d.Groups["s"].Success ? d.Groups["s"].Value : d.Groups["s2"].Value;
            if (d.Groups["s2"].Success && d.Groups["s1"].Value != nome) return null;
            if (stazione(nome) is not { } s) return null;
            var radiale = double.Parse(d.Groups["r"].Value, CultureInfo.InvariantCulture);
            var dme = double.Parse((d.Groups["d"].Success ? d.Groups["d"] : d.Groups["d2"]).Value, CultureInfo.InvariantCulture);
            return new PuntoDellaMea(Sphere.Destination(s.Position, radiale + s.Variation, dme), null, nome, radiale, dme);
        }
        if (RxCapoArco().Match(testo) is { Success: true } c && c.Groups["s"].Value == c.Groups["s1"].Value && stazione(c.Groups["s"].Value) is { } centro)
        {
            var radiale = double.Parse(c.Groups["r"].Value, CultureInfo.InvariantCulture);
            var dme = double.Parse(c.Groups["d"].Value, CultureInfo.InvariantCulture);
            return new PuntoDellaMea(Sphere.Destination(centro.Position, radiale + centro.Variation, dme), null, c.Groups["s"].Value, radiale, dme);
        }
        if (RxCapoNome().Match(testo) is { Success: true } n && (punto(n.Value) ?? stazione(n.Value)?.Position) is { } dove)
            return new PuntoDellaMea(dove, n.Value, null, null, null);
        return null;
    }

    [GeneratedRegex(@"\b(?:MEL\s*/\s*MEA|MEA\s*/\s*MEL|MEA)\s*:\s*(?<pezzi>.*?)(?=\bME[AL]\b|$)", RegexOptions.CultureInvariant)]
    private static partial Regex RxBloccoDiMea();

    // «ENEPA – RDL 327/25NM COM VOR/DME: 6000FT», «BANAV – RDL 013/30 NM BAR VOR/DME FL 80», «LMT VOR/DME – …: TRL».
    [GeneratedRegex(@"^\s*(?<da>[^–]+?)\s*(?:–|\s-\s)\s*(?<a>.+?)\s*:?\s*(?<q>FL\s*\d{2,3}|\d{3,5}\s*(?:FT|ft)|TRL)\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxPezzoDiMea();

    [GeneratedRegex(@"\([^)]*\)", RegexOptions.CultureInvariant)]
    private static partial Regex RxFraParentesi();

    [GeneratedRegex(@"RDL\s*(?<r>\d{3})(?:\s*/\s*D?\s*(?<d>\d+(?:\.\d+)?)\s*NM\s+(?<s>[A-Z]{3})\b|\s+(?<s1>[A-Z]{3})\s+VOR\s*/\s*(?<d2>\d+(?:\.\d+)?)\s*NM\s+(?<s2>[A-Z]{3})\s+DME)", RegexOptions.CultureInvariant)]
    private static partial Regex RxCapoDme();

    [GeneratedRegex(@"ARC\s*(?<d>\d+(?:\.\d+)?)\s*NM\s+(?<s>[A-Z]{3})\s+DME\s*/\s*RDL\s*(?<r>\d{3})\s+(?<s1>[A-Z]{3})\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxCapoArco();

    [GeneratedRegex(@"^\s*[A-Z][A-Z0-9]{1,4}\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxCapoNome();
}

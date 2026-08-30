using Vipi.Application.Abstractions;

namespace Vipi.Application.Airspace;

/// <summary>
/// Che cosa non torna fra l'AIP e la nostra anagrafica. ⚠️ È un <b>codice</b>: il testo lo scrive la UI.
///
/// <para>⚠️ <b>L'ordine è la gravità</b>, e il rapporto ci si appoggia per ordinare le righe. Prima le
/// <b>discordanze</b> — i due archivi dicono cose diverse, e uno dei due sbaglia — poi le <b>assenze</b>, poi
/// le <b>lacune</b>, che non sono errori ma cose che nessuno ha ancora scritto.</para>
///
/// <para>Questo enum si può riordinare, a differenza di quasi tutti gli altri: non finisce in nessun payload
/// di release e non si scrive da nessuna parte — il rapporto si calcola quando qualcuno lo chiede.</para>
/// </summary>
public enum NavaidDiffKind
{
    /// <summary>Stessa radioassistenza, frequenza diversa. <b>Uno dei due archivi sbaglia.</b></summary>
    FrequenzaDiversa,

    /// <summary>Stessa radioassistenza, canale diverso: <b>tutti e due</b> lo dicono, e non è lo stesso.</summary>
    CanaleDiverso,

    /// <summary>Stessa radioassistenza, posizione lontana più della soglia.</summary>
    PosizioneDiversa,

    /// <summary>L'AIP ce l'ha, l'anagrafica no.</summary>
    SoloNellAip,

    /// <summary>L'anagrafica ce l'ha, l'AIP no.</summary>
    SoloInAnagrafica,

    /// <summary>Più di una riga con lo stesso codice da una parte o dall'altra: si guarda a mano.</summary>
    DaGuardareAMano,

    /// <summary>
    /// L'AIP dice il canale e noi no. <b>È una lacuna, non una discordanza</b>, e sta separata apposta:
    /// misurato dal vivo il 29 agosto 2026, su 54 righe di canale <b>49 erano questa</b> e solo <b>5</b> erano
    /// canali davvero diversi. Tenendole insieme, i cinque che contano sparivano in mezzo agli altri.
    /// </summary>
    CanaleMancante,

    /// <summary>L'anagrafica non dice che tipo è, e l'AIP lo dice. Lacuna, come sopra.</summary>
    TipoMancante,
}

/// <summary>Una differenza, col codice e i due valori messi a confronto.</summary>
public sealed record NavaidDiff(
    NavaidDiffKind Kind, string Code, string Family, string? Aip, string? Nostro, string? Nota = null);

/// <summary>
/// Confronta le radioassistenze dell'<b>AIP</b> con la nostra <b>anagrafica</b>. PURA: nessun I/O, nessuna
/// scrittura, nessuna proposta di correzione automatica.
///
/// <para>⚠️ <b>Segnala e basta</b> (decisione 9 del committente): le correzioni si fanno nel <b>sectorfile</b>
/// e da lì si reimportano. Questo è il motivo per cui il risultato è un elenco di <b>differenze</b> e non un
/// elenco di <i>modifiche da applicare</i>: la seconda forma inviterebbe a premere un tasto che non deve
/// esistere.</para>
///
/// <para>⚠️ <b>L'accoppiamento è su codice + famiglia, e solo quando è UNO a UNO.</b> Nel file `GRO` compare
/// due volte in VHF — Grosseto è un VOR e un TACAN — e nell'anagrafica succede lo stesso. Accoppiarli a
/// indovinare produrrebbe due differenze inventate; si dice invece «da guardare a mano», che è la verità.</para>
/// </summary>
public static class NavaidAipReport
{
    /// <summary>Oltre questa distanza le due posizioni non sono più la stessa cosa. Un decimo di miglio.</summary>
    public const double SogliaNm = 0.1;

    private const double NmPerGradoLat = 60.0;

    /// <summary>Le differenze, dalla più grave alla più lieve, e a parità in ordine di codice.</summary>
    public static IReadOnlyList<NavaidDiff> Confronta(
        IReadOnlyList<AipNavaid> aip, IReadOnlyList<NavaidRow> anagrafica)
    {
        var perAip = aip.GroupBy(n => (n.Code, n.Kind)).ToDictionary(g => g.Key, g => g.ToList());
        var perNostro = anagrafica.GroupBy(n => (Code: Norm(n.Code), Kind: Norm(n.Kind)))
            .ToDictionary(g => g.Key, g => g.ToList());

        var esito = new List<NavaidDiff>();
        foreach (var chiave in perAip.Keys.Concat(perNostro.Keys).Distinct())
        {
            perAip.TryGetValue(chiave, out var daAip);
            perNostro.TryGetValue(chiave, out var daNoi);

            if (daNoi is null)
            {
                foreach (var a in daAip!)
                    esito.Add(new NavaidDiff(NavaidDiffKind.SoloNellAip, chiave.Item1, chiave.Item2,
                        Descrizione(a), null, a.Name));
                continue;
            }

            if (daAip is null)
            {
                foreach (var n in daNoi)
                    esito.Add(new NavaidDiff(NavaidDiffKind.SoloInAnagrafica, chiave.Item1, chiave.Item2,
                        null, Descrizione(n)));
                continue;
            }

            if (daAip.Count > 1 || daNoi.Count > 1)
            {
                esito.Add(new NavaidDiff(NavaidDiffKind.DaGuardareAMano, chiave.Item1, chiave.Item2,
                    $"{daAip.Count}", $"{daNoi.Count}"));
                continue;
            }

            esito.AddRange(Confronta(daAip[0], daNoi[0]));
        }

        return esito
            .OrderBy(d => (int)d.Kind)
            .ThenBy(d => d.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<NavaidDiff> Confronta(AipNavaid a, NavaidRow n)
    {
        var fAip = AirspaceNavaidReader.NormalizzaFrequenza(a.Frequency);
        var fNostra = AirspaceNavaidReader.NormalizzaFrequenza(n.Frequency);
        if (fAip is not null && fNostra is not null && fAip != fNostra)
            yield return new NavaidDiff(NavaidDiffKind.FrequenzaDiversa, a.Code, a.Kind, fAip, fNostra);

        // ⚠️ Un canale che noi non abbiamo NON è la stessa cosa di un canale diverso, e tenerli insieme
        // nascondeva i cinque che contano dietro quarantanove che non dicono niente di nuovo.
        var cAip = Norm(a.Channel);
        var cNostro = Norm(n.Channel);
        if (cAip != cNostro)
        {
            var quale = cAip.Length > 0 && cNostro.Length > 0
                ? NavaidDiffKind.CanaleDiverso
                : NavaidDiffKind.CanaleMancante;
            yield return new NavaidDiff(quale, a.Code, a.Kind,
                cAip.Length > 0 ? cAip : null, cNostro.Length > 0 ? cNostro : null);
        }

        if (a.Latitude is { } la && a.Longitude is { } lo && n.Latitude is { } ln && n.Longitude is { } lgn)
        {
            var d = DistanzaNm(la, lo, ln, lgn);
            if (d > SogliaNm)
                yield return new NavaidDiff(NavaidDiffKind.PosizioneDiversa, a.Code, a.Kind,
                    Coord(la, lo), Coord(ln, lgn), $"{d:0.0} NM");
        }

        // ⚠️ Il tipo dell'anagrafica è EDITORIALE e nasce vuoto: qui non si propone di riempirlo da solo, si
        // dice che l'AIP una risposta ce l'ha. Chi cura le radioassistenze decide se è la stessa.
        if (string.IsNullOrWhiteSpace(n.Type) && !string.IsNullOrWhiteSpace(a.Type))
            yield return new NavaidDiff(NavaidDiffKind.TipoMancante, a.Code, a.Kind, a.Type, null);
    }

    private static string Descrizione(AipNavaid a) =>
        string.Join(" · ", new[] { a.Type, AirspaceNavaidReader.NormalizzaFrequenza(a.Frequency), a.Channel }
            .Where(x => !string.IsNullOrWhiteSpace(x)));

    private static string Descrizione(NavaidRow n) =>
        string.Join(" · ", new[] { n.Type, AirspaceNavaidReader.NormalizzaFrequenza(n.Frequency), n.Channel }
            .Where(x => !string.IsNullOrWhiteSpace(x)));

    private static string Coord(double lat, double lon) =>
        $"{lat.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)}, " +
        $"{lon.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)}";

    /// <summary>Distanza in miglia, equirettangolare: alle latitudini italiane basta e avanza per una soglia.</summary>
    private static double DistanzaNm(double lat1, double lon1, double lat2, double lon2)
    {
        var k = Math.Cos((lat1 + lat2) / 2.0 * Math.PI / 180.0);
        var dLat = lat1 - lat2;
        var dLon = (lon1 - lon2) * k;
        return Math.Sqrt(dLat * dLat + dLon * dLon) * NmPerGradoLat;
    }

    private static string Norm(string? s) => (s ?? "").Trim().ToUpperInvariant();
}

using System.Globalization;

namespace Vipi.SectorLab.Core.Aurora;

/// <summary>
/// La prova R4 della carta «file per file» (§17): i punti di startup come una mappa <c>STARTUP</c> nel <c>MAPS</c> dello
/// <c>.str</c> dello scalo col <b>tipo 2</b> (il tasto HOLD, l'unico che il <c>MAPS</c> non usa): ogni punto un trattino
/// (<c>&lt;br&gt;</c>), il nome nell'etichetta del punto. Il dubbio: col tasto HOLD la mappa starebbe nell'elenco delle attese
/// della pista. Per la prova, su LIRF, due trattini corti sui primi due stand del <c>.gts</c> (le posizioni vere dei punti
/// di startup non ci sono: qui conta dove e come compare la mappa, non dove sta il trattino).
/// </summary>
public sealed class PuntiDiStartup : ProvaInAurora
{
    /// <summary>Lo scalo della prova.</summary>
    public const string Scalo = "LIRF";

    /// <summary>Mezza lunghezza del trattino, in secondi di longitudine (circa 13 m a Roma).</summary>
    private const double MezzoTrattino = 0.5;

    public override string Argomento => "punti-di-startup";

    public override string Titolo => "i punti di startup col tasto HOLD (R4)";

    public override string Perche
        => "«File per file» R4: una mappa STARTUP nel MAPS dello .str col tipo 2 (tasto HOLD). Dove compare, come si accende, disturba le attese?";

    public override ProvaPreparata? Prepara(string radice, out string? perche)
    {
        string it = Path.Combine(radice, "SectorFiles", "Include", "IT");
        string str = Path.Combine(it, Scalo.ToLowerInvariant() + ".str"), gts = Path.Combine(it, Scalo.ToLowerInvariant() + ".gts");
        if (!File.Exists(str) || !File.Exists(gts))
        {
            perche = $"Mancano {Scalo.ToLowerInvariant()}.str o {Scalo.ToLowerInvariant()}.gts: la prova si fa su {Scalo}.";
            return null;
        }

        var stand = RigheDelFile.Leggi(gts).Righe.Select(r => r.Trim().Split(';'))
            .Where(c => c.Length >= 4 && c[1].Trim() == Scalo && c[2].Trim().Length > 0).Take(2).ToList();
        if (stand.Count < 2)
        {
            perche = $"In {Scalo.ToLowerInvariant()}.gts non ci sono due stand da usare come posizione dei trattini.";
            return null;
        }

        var righe = RigheDelFile.Leggi(str);
        string intestazione = $"{Scalo};MAPS;";
        int ultimaMappa = righe.Righe.FindLastIndex(r => r.TrimStart().StartsWith(intestazione, StringComparison.Ordinal));
        if (ultimaMappa < 0)
        {
            perche = $"In {Scalo.ToLowerInvariant()}.str non c'è il MAPS.";
            return null;
        }

        // Il blocco va dopo l'ultima voce del MAPS, prima della procedura che la segue.
        int dopo = righe.Righe.FindIndex(ultimaMappa + 1, r => r.TrimStart().StartsWith(Scalo + ";", StringComparison.Ordinal));
        if (dopo < 0)
            dopo = righe.Righe.Count;
        var blocco = new List<string> { $"{Scalo};MAPS;STARTUP; ; ;2;" };
        for (int i = 0; i < stand.Count; i++)
        {
            string lat = stand[i][2].Trim(), lon = stand[i][3].Trim();
            string? ovest = SpostaLaLongitudine(lon, -MezzoTrattino), est = SpostaLaLongitudine(lon, MezzoTrattino);
            if (ovest is null || est is null)
            {
                perche = $"La longitudine dello stand {stand[i][0]} ({lon}) non si legge.";
                return null;
            }

            // Ogni trattino un tratto: <br> sul primo punto apre il tratto, il nome sta sul secondo.
            blocco.Add(i == 0 ? $"{lat};{ovest};" : $"{lat};{ovest};<br>");
            blocco.Add($"{lat};{est};S{i + 1};");
        }

        blocco.Add("");
        righe.Righe.InsertRange(dopo, blocco);
        perche = null;
        string relativo = $"SectorFiles/Include/IT/{Scalo.ToLowerInvariant()}.str";
        var domande = new List<DomandaDellaProva>
        {
            new($"Scelto {Scalo}, la mappa STARTUP compare col tasto HOLD? Dove: fra le attese di una pista, o a parte?", $"il tasto HOLD di {Scalo}, dove stanno HLD-CMP e HLD-TAQ"),
            new("Accesa, si vedono due trattini coi nomi S1 e S2?", $"il piazzale di {Scalo}, sugli stand {stand[0][0].Trim()} e {stand[1][0].Trim()}"),
            new($"Le attese di {Scalo} si accendono e si spengono come prima?", $"il tasto HOLD di {Scalo}, una pista alla volta"),
            new("Il sector si carica senza errori nuovi?", "i messaggi di Aurora al caricamento del sector (SectorError.log)"),
        };
        var fatto = new List<string>
        {
            $"{relativo}: la mappa {Scalo};MAPS;STARTUP di tipo 2 (tasto HOLD), dopo l'ultima voce del MAPS: due trattini di circa 26 m, S1 e S2, sugli stand {stand[0][0].Trim()} e {stand[1][0].Trim()}.",
        };
        return new ProvaPreparata([new FileDellaProva(relativo, righe.Testo())], domande, fatto);
    }

    /// <summary>Una longitudine <c>E012.16.18.365</c> spostata di qualche secondo (negativi verso ovest), o null.</summary>
    internal static string? SpostaLaLongitudine(string lon, double secondi)
    {
        var parti = lon.Split('.');
        if (parti.Length != 4 || parti[0].Length != 4 || parti[0][0] is not ('E' or 'W')
            || !int.TryParse(parti[0][1..], NumberStyles.None, CultureInfo.InvariantCulture, out int gradi)
            || !int.TryParse(parti[1], NumberStyles.None, CultureInfo.InvariantCulture, out int min)
            || !double.TryParse(parti[2] + "." + parti[3], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double sec))
            return null;
        double totale = (parti[0][0] == 'E' ? 1 : -1) * (gradi * 3600 + min * 60 + sec) + secondi;
        char emisfero = totale >= 0 ? 'E' : 'W';
        totale = Math.Round(Math.Abs(totale), 3);
        int g = (int)(totale / 3600), m = (int)((totale - g * 3600) / 60);
        double s = totale - g * 3600 - m * 60;
        return string.Create(CultureInfo.InvariantCulture, $"{emisfero}{g:000}.{m:00}.{s:00.000}");
    }
}

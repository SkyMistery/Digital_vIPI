using System.Globalization;

namespace Vipi.SectorLab.Core.Aurora;

/// <summary>
/// La prova A8 della carta «file per file» (§1): le etichette dei fix di confine di <c>FRA.artcc</c> scritte per
/// riferimento (<c>L;ABDAB;ABDAB;ABDAB;8;</c>) invece che con le coordinate copiate dal fix. Si cambia solo un'etichetta
/// che ha un fix omonimo <b>nella stessa posizione</b> (sul fork tutte e 104). E per vedere che il riferimento vale
/// davvero, un fix (uno che sta in un file solo dei NAVAIDS) si sposta di 3 primi verso nord: la sua etichetta deve
/// seguirlo.
/// </summary>
public sealed class EtichettePerRiferimento : ProvaInAurora
{
    /// <summary>Il file delle etichette dei fix di confine, dalla radice del repo.</summary>
    public const string File = "SectorFiles/Include/IT/ACC/FRA.artcc";

    /// <summary>Di quanti primi si sposta verso nord il fix della prova.</summary>
    public const int Primi = 3;

    public override string Argomento => "etichette-per-riferimento";

    public override string Titolo => "le etichette dei fix di confine per riferimento (A8)";

    public override string Perche
        => "«File per file» A8: L;ABDAB;ABDAB;ABDAB;8; invece delle coordinate copiate, così spostare il fix sposta l'etichetta. Si vede uguale? Segue il fix?";

    public override ProvaPreparata? Prepara(string radice, out string? perche)
    {
        string it = Path.Combine(radice, "SectorFiles", "Include", "IT");
        string artcc = Path.Combine(radice, File.Replace('/', Path.DirectorySeparatorChar));
        if (!System.IO.File.Exists(artcc))
        {
            perche = $"Nel sector non c'è {File}.";
            return null;
        }

        // I punti dei NAVAIDS per nome: dove stanno, in quali file e a che riga.
        var punti = new Dictionary<string, List<(string File, int Riga, string Lat, string Lon)>>(StringComparer.Ordinal);
        var navaids = new Dictionary<string, RigheDelFile>(StringComparer.Ordinal);
        string cartella = Path.Combine(it, "NAVAIDS");
        IEnumerable<string> fileDeiPunti = Directory.Exists(cartella) ? Directory.EnumerateFiles(cartella, "*.fix").Order(StringComparer.Ordinal) : [];
        foreach (string fix in fileDeiPunti)
        {
            string relativo = "SectorFiles/Include/IT/NAVAIDS/" + Path.GetFileName(fix);
            var righe = RigheDelFile.Leggi(fix);
            navaids[relativo] = righe;
            for (int i = 0; i < righe.Righe.Count; i++)
            {
                var campi = righe.Righe[i].Trim().Split(';');
                if (campi.Length >= 3 && campi[0].Length > 0 && !campi[0].StartsWith("//", StringComparison.Ordinal))
                    (punti.TryGetValue(campi[0], out var l) ? l : punti[campi[0]] = []).Add((relativo, i, campi[1].Trim(), campi[2].Trim()));
            }
        }

        var etichette = RigheDelFile.Leggi(artcc);
        int scritte = 0, lasciate = 0;
        string? spostato = null;
        for (int i = 0; i < etichette.Righe.Count; i++)
        {
            var campi = etichette.Righe[i].Trim().Split(';');
            if (campi.Length < 4 || campi[0] != "L")
                continue;
            string nome = campi[1].Trim();
            if (!punti.TryGetValue(nome, out var omonimi) || !omonimi.Any(p => p.Lat == campi[2].Trim() && p.Lon == campi[3].Trim()))
            {
                lasciate++;
                continue;
            }

            campi[2] = nome;
            campi[3] = nome;
            etichette.Righe[i] = string.Join(';', campi);
            scritte++;
            // Il fix da spostare: il primo che sta in un file solo, così non c'è dubbio su quale legga Aurora.
            spostato ??= omonimi.Count == 1 ? nome : null;
        }

        if (scritte == 0)
        {
            perche = $"In {File} nessuna etichetta ha un fix omonimo nella stessa posizione: non c'è niente da scrivere per riferimento.";
            return null;
        }

        var file = new List<FileDellaProva> { new(File, etichette.Testo()) };
        string? dove = null;
        if (spostato is not null)
        {
            var (fileDelFix, riga, lat, _) = punti[spostato][0];
            var righe = navaids[fileDelFix];
            var campi = righe.Righe[riga].Split(';');
            campi[1] = PiuANord(lat, Primi) ?? lat;
            righe.Righe[riga] = string.Join(';', campi);
            file.Add(new FileDellaProva(fileDelFix, righe.Testo()));
            dove = $"{spostato}: era a {lat}, ora a {campi[1]} ({fileDelFix[(fileDelFix.LastIndexOf('/') + 1)..]})";
        }

        perche = null;
        var domande = new List<DomandaDellaProva>
        {
            new($"Le etichette dei fix di confine si vedono tutte, dove stavano ({scritte} scritte per riferimento)?", "il confine FRA (tasto ACC), per esempio ABDAB, ABESI, ADEXI"),
        };
        if (spostato is not null)
            domande.Add(new($"L'etichetta di {spostato} si è spostata col fix, {Primi} primi più a nord?", dove!));
        domande.Add(new("Il sector si carica senza errori nuovi?", "i messaggi di Aurora al caricamento del sector (SectorError.log)"));
        var fatto = new List<string> { $"{File}: {scritte} etichette L; per riferimento al fix omonimo ({lasciate} lasciate com'erano)." };
        if (dove is not null)
            fatto.Add($"Per la prova, il fix {dove}.");
        return new ProvaPreparata(file, domande, fatto);
    }

    /// <summary>Una latitudine <c>N046.02.37.000</c> spostata di qualche primo verso nord, o null se non si legge.</summary>
    internal static string? PiuANord(string lat, int primi)
    {
        var parti = lat.Split('.');
        if (parti.Length != 4 || parti[0].Length != 4 || parti[0][0] is not ('N' or 'S')
            || !int.TryParse(parti[0][1..], NumberStyles.None, CultureInfo.InvariantCulture, out int gradi)
            || !int.TryParse(parti[1], NumberStyles.None, CultureInfo.InvariantCulture, out int min))
            return null;
        int totale = (parti[0][0] == 'N' ? 1 : -1) * (gradi * 60 + min) + primi;
        char emisfero = totale >= 0 ? 'N' : 'S';
        totale = Math.Abs(totale);
        return string.Create(CultureInfo.InvariantCulture, $"{emisfero}{totale / 60:000}.{totale % 60:00}.{parti[2]}.{parti[3]}");
    }
}

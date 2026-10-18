using System.Text;

namespace Vipi.SectorLab.Core.Aurora;

/// <summary>Le righe di un file di testo del sector, con l'a capo che aveva (per riscriverlo uguale).</summary>
internal sealed class RigheDelFile
{
    private RigheDelFile(List<string> righe, string acapo, bool finisceAcapo)
    {
        Righe = righe;
        Acapo = acapo;
        FinisceAcapo = finisceAcapo;
    }

    public List<string> Righe { get; }

    public string Acapo { get; }

    public bool FinisceAcapo { get; }

    public static RigheDelFile Leggi(string percorso)
    {
        string testo = File.ReadAllText(percorso, Encoding.Latin1);
        string acapo = testo.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var righe = testo.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
        bool finisce = righe.Count > 0 && righe[^1].Length == 0;
        if (finisce)
            righe.RemoveAt(righe.Count - 1);
        return new RigheDelFile(righe, acapo, finisce);
    }

    public string Testo() => string.Join(Acapo, Righe) + (FinisceAcapo ? Acapo : "");
}

/// <summary>
/// La prova F5 della carta «file per file» (§7): i tre file di <c>ENRVFI</c> (i punti VFR della FIR, uno per ACC) sotto
/// <c>[VFRFIX]</c> invece di <c>[VFRENR]</c>, in <c>ITALY.isc</c> e negli <c>.isc</c> degli ACC. Si vedono? Come si
/// accendono, senza un aeroporto? Gli altri file non si toccano.
/// </summary>
public sealed class VfrDiFirComeFix : ProvaInAurora
{
    /// <summary>La cartella dei punti VFR della FIR, come la scrivono gli <c>.isc</c>.</summary>
    private const string Cartella = @"ENRVFI\";

    public override string Argomento => "vfr-di-fir-come-fix";

    public override string Titolo => "i VFR della FIR sotto [VFRFIX] (F5)";

    public override string Perche
        => "«File per file» F5: i tre file di ENRVFI oggi stanno sotto [VFRENR]; sotto [VFRFIX] si vedono, e come si accendono senza un aeroporto?";

    public override ProvaPreparata? Prepara(string radice, out string? perche)
    {
        string sectorFiles = Path.Combine(radice, "SectorFiles");
        var file = new List<FileDellaProva>();
        var spostati = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        IEnumerable<string> iscs = Directory.Exists(sectorFiles) ? Directory.EnumerateFiles(sectorFiles, "*.isc").Order(StringComparer.Ordinal) : [];
        foreach (string isc in iscs)
        {
            var righe = RigheDelFile.Leggi(isc);
            var mossi = Sposta(righe.Righe);
            if (mossi.Count == 0)
                continue;
            spostati.UnionWith(mossi);
            file.Add(new FileDellaProva("SectorFiles/" + Path.GetFileName(isc), righe.Testo()));
        }

        if (file.Count == 0)
        {
            perche = "Negli .isc non c'è nessun file di ENRVFI sotto [VFRENR]: la prova non ha niente da spostare.";
            return null;
        }

        perche = null;
        var esempi = spostati.Select(s => (File: s, Primo: PrimoPunto(Path.Combine(radice, "SectorFiles", "Include", "IT", s.Replace('\\', Path.DirectorySeparatorChar)))))
            .Where(e => e.Primo is not null).Select(e => $"{e.Primo} ({Path.GetFileName(e.File)})").ToList();
        var domande = new List<DomandaDellaProva>
        {
            new($"I punti VFR della FIR si vedono sulla mappa ({spostati.Count} file: {string.Join(", ", spostati.Select(Path.GetFileName))})?",
                esempi.Count > 0 ? $"i punti VFR, per esempio {string.Join(", ", esempi)}" : "i punti VFR della FIR"),
            new("Si accendono e spengono senza scegliere un aeroporto? Con quale tasto o voce?", "il tasto o il menu dei punti VFR"),
            new("I punti VFR degli aeroporti si vedono e si accendono come prima?", "i punti VFR di LIRF (lirf.vfi), scelto LIRF"),
            new("Il sector si carica senza errori nuovi?", "i messaggi di Aurora al caricamento del sector (SectorError.log)"),
        };
        var fatto = new List<string>
        {
            $"{string.Join(", ", file.Select(f => f.Percorso[(f.Percorso.LastIndexOf('/') + 1)..]))}: {string.Join(", ", spostati)} da [VFRENR] a [VFRFIX].",
        };
        return new ProvaPreparata(file, domande, fatto);
    }

    /// <summary>
    /// Sposta sotto <c>[VFRFIX]</c> le righe <c>F;ENRVFI\…</c> della sezione <c>[VFRENR]</c>. Se la sezione ha solo quelle,
    /// cambia il titolo; se ne ha anche altre, le righe vanno in una sezione <c>[VFRFIX]</c> nuova, subito dopo. Torna i
    /// file spostati.
    /// </summary>
    internal static List<string> Sposta(List<string> righe)
    {
        int titolo = righe.FindIndex(r => r.Trim().Equals("[VFRENR]", StringComparison.OrdinalIgnoreCase));
        if (titolo < 0)
            return [];
        int fine = titolo + 1;
        while (fine < righe.Count && !righe[fine].TrimStart().StartsWith('['))
            fine++;
        var sezione = Enumerable.Range(titolo + 1, fine - titolo - 1).ToList();
        var dellaFir = sezione.Where(i => EDellaFir(righe[i])).ToList();
        if (dellaFir.Count == 0)
            return [];
        var spostati = dellaFir.Select(i => righe[i].Trim()[2..]).ToList();
        if (sezione.All(i => EDellaFir(righe[i]) || righe[i].Trim().Length == 0))
        {
            righe[titolo] = "[VFRFIX]";
            return spostati;
        }

        var nuove = dellaFir.Select(i => righe[i]).ToList();
        foreach (int i in dellaFir.OrderDescending())
            righe.RemoveAt(i);
        int dopo = fine - dellaFir.Count;
        righe.InsertRange(dopo, ["[VFRFIX]", .. nuove]);
        return spostati;
    }

    private static bool EDellaFir(string riga)
    {
        string pulita = riga.Trim();
        return pulita.StartsWith("F;", StringComparison.OrdinalIgnoreCase) && pulita[2..].StartsWith(Cartella, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Il nome del primo punto di un <c>.vfi</c> (primo campo della prima riga di dati), o null.</summary>
    private static string? PrimoPunto(string percorso)
    {
        if (!File.Exists(percorso))
            return null;
        return File.ReadLines(percorso, Encoding.Latin1).Select(r => r.Trim())
            .FirstOrDefault(r => r.Length > 0 && !r.StartsWith("//", StringComparison.Ordinal))?.Split(';')[0].Trim() is { Length: > 0 } nome ? nome : null;
    }
}

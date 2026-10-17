using Vipi.SectorLab.Core.Ispezione;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.IO;

namespace Vipi.SectorLab.Core.Git;

/// <summary>
/// Le righe del changelog che il Lab propone per un ramo di lavoro (carta F4, slice 6; «file per file» C1): una per
/// file cambiato rispetto al ramo da cui il lavoro è nato, nella sezione della sua FIR, col testo dai record — aggiunti,
/// tolti, modificati — confrontati col motore. L'AOD le corregge prima di scriverle: le legge chi usa Aurora.
/// <para>La FIR di uno scalo: le FIR che lo hanno nel loro <c>OTHER/&lt;fir&gt;.ap</c> (misura del 17 ottobre: ogni
/// <c>&lt;FIR&gt;.isc</c> carica il suo). Gli scali di confine stanno in più <c>.ap</c> con righe identiche (LIRQ in
/// <c>limm</c>, <c>lipp</c>, <c>lirr</c>): allora le sezioni possibili sono più d'una e sceglie l'AOD. Un file che non è
/// di uno scalo va in <c>Generale</c>, uno che porta il nome di una FIR (<c>lirr.frq</c>) nella sua.</para>
/// </summary>
public static class RigheProposte
{
    private static readonly string[] Fir = ["LIBB", "LIMM", "LIPP", "LIRR"];

    /// <summary>Per ogni scalo, le FIR che lo hanno nel loro <c>.ap</c>.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> FirDegliScali(string radice)
    {
        ArgumentException.ThrowIfNullOrEmpty(radice);
        var scali = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (string fir in Fir)
        {
            string ap = Path.Combine(radice, "SectorFiles", "Include", "IT", "OTHER", fir.ToLowerInvariant() + ".ap");
            if (!File.Exists(ap))
                continue;
            foreach (string riga in File.ReadLines(ap))
            {
                if (riga.Length > 5 && riga[4] == ';' && riga[..4].All(char.IsAsciiLetterUpper))
                {
                    var sue = scali.TryGetValue(riga[..4], out var gia) ? gia : scali[riga[..4]] = [];
                    if (!sue.Contains(fir))
                        sue.Add(fir);
                }
            }
        }

        return scali.ToDictionary(s => s.Key, s => (IReadOnlyList<string>)s.Value, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Lo scalo di un file (<c>lirl.sid</c> → <c>LIRL</c>), se le prime quattro lettere sono uno scalo; o null.</summary>
    public static string? Scalo(string percorso, IReadOnlyDictionary<string, IReadOnlyList<string>> fir)
    {
        ArgumentNullException.ThrowIfNull(fir);
        string nome = Path.GetFileNameWithoutExtension(percorso);
        return nome.Length >= 4 && fir.ContainsKey(nome[..4]) ? nome[..4].ToUpperInvariant() : null;
    }

    /// <summary>Le sezioni del changelog possibili per un file: quelle della FIR dello scalo, la FIR del nome, o Generale.</summary>
    public static IReadOnlyList<string> SezioniDelFile(string percorso, IReadOnlyDictionary<string, IReadOnlyList<string>> fir)
    {
        ArgumentNullException.ThrowIfNull(fir);
        if (Scalo(percorso, fir) is { } scalo)
            return fir[scalo];
        string nome = Path.GetFileNameWithoutExtension(percorso).ToUpperInvariant();
        return Fir.FirstOrDefault(f => nome == f || nome.StartsWith(f + "_", StringComparison.Ordinal)) is { } suo ? [suo] : ["Generale"];
    }

    /// <summary>
    /// Le righe proposte per il ramo: i file del sector cambiati da quando è nato da <paramref name="base_"/> (le sole
    /// registrazioni: i file salvati e non registrati non contano). Il changelog stesso no.
    /// </summary>
    public static async Task<IReadOnlyList<RigaDelChangelog>> ProponiAsync(Git git, string radice, string base_, string ramo, CancellationToken annulla = default)
    {
        ArgumentNullException.ThrowIfNull(git);
        var origine = await git.EseguiAsync(radice, ["merge-base", "origin/" + base_, ramo], annulla).ConfigureAwait(false);
        if (!origine.Riuscito)
            return [];
        string da = origine.Uscita.Trim();
        var diff = await git.EseguiAsync(radice, ["diff", "--name-status", "--no-renames", da, ramo, "--", Registrazione.CartellaDelSector], annulla).ConfigureAwait(false);
        if (!diff.Riuscito)
            return [];

        var fir = FirDegliScali(radice);
        var righe = new List<RigaDelChangelog>();
        foreach (string voce in diff.Righe)
        {
            var parti = voce.Split('\t');
            if (parti.Length < 2 || parti[1] == Changelog.Percorso)
                continue;
            string stato = parti[0], percorso = parti[1];
            string? prima = stato == "A" ? null : await Contenuto(git, radice, da, percorso, annulla).ConfigureAwait(false);
            string? dopo = stato == "D" ? null : await Contenuto(git, radice, ramo, percorso, annulla).ConfigureAwait(false);
            string cosa = CosaECambiato(percorso, prima, dopo);
            string? scalo = Scalo(percorso, fir);
            righe.Add(new RigaDelChangelog(SezioniDelFile(percorso, fir), scalo is null ? cosa : $"{scalo}: {cosa}"));
        }

        return righe;
    }

    /// <summary>
    /// Che cosa è cambiato in un file, in parole: i record aggiunti, tolti, modificati (per tipo e nome), letti col motore.
    /// Un file che il motore non legge: aggiunto, tolto o aggiornato.
    /// </summary>
    public static string CosaECambiato(string percorso, string? prima, string? dopo)
    {
        ArgumentException.ThrowIfNullOrEmpty(percorso);
        string nome = Path.GetFileName(percorso);
        if (dopo is null)
            return $"tolto il file {nome}";
        // Un file che il motore non legge (prima o dopo): si dice solo che è nuovo o aggiornato.
        var nuovi = Record(percorso, dopo);
        var vecchi = prima is null ? [] : Record(percorso, prima);
        if (nuovi is null || vecchi is null)
            return prima is null ? $"nuovo file {nome}" : $"aggiornato {nome}";

        var parti = new List<string>();
        var restanti = vecchi.ToList();
        var aggiunti = new List<(string Tipo, string Nome)>();
        var modificati = new List<(string Tipo, string Nome)>();
        foreach (var nuovo in nuovi)
        {
            int uguale = restanti.FindIndex(v => v.Tipo == nuovo.Tipo && v.Nome == nuovo.Nome && v.Righe == nuovo.Righe);
            if (uguale >= 0)
            {
                restanti.RemoveAt(uguale);
                continue;
            }

            int stesso = restanti.FindIndex(v => v.Tipo == nuovo.Tipo && v.Nome == nuovo.Nome);
            if (stesso >= 0)
            {
                restanti.RemoveAt(stesso);
                modificati.Add((nuovo.Tipo, nuovo.Nome));
            }
            else
            {
                aggiunti.Add((nuovo.Tipo, nuovo.Nome));
            }
        }

        Racconta(parti, "aggiunt", aggiunti);
        Racconta(parti, "tolt", [.. restanti.Select(r => (r.Tipo, r.Nome))]);
        Racconta(parti, "modificat", modificati);
        return parti.Count == 0 ? $"aggiornato {nome}" : string.Join("; ", parti);
    }

    private static void Racconta(List<string> parti, string verbo, IReadOnlyList<(string Tipo, string Nome)> record)
    {
        foreach (var gruppo in record.GroupBy(r => r.Tipo))
        {
            var nomi = gruppo.Select(r => r.Nome).Distinct(StringComparer.Ordinal).ToList();
            string desinenza = nomi.Count == 1 ? "o" : "i";
            string elenco = nomi.Count <= 6 ? string.Join(", ", nomi) : $"{nomi.Count}";
            parti.Add(nomi.Count <= 6 ? $"{verbo}{desinenza} {gruppo.Key} {elenco}" : $"{verbo}{desinenza} {elenco} {gruppo.Key}");
        }
    }

    /// <summary>I record di un testo, letti dal motore come quel file: tipo (il nome che il Lab gli dà), nome, righe scritte.</summary>
    private static List<(string Tipo, string Nome, string Righe)>? Record(string percorso, string testo)
    {
        string cartella = Path.Combine(Path.GetTempPath(), "lab-changelog-" + Guid.NewGuid().ToString("N"));
        string file = Path.Combine(cartella, percorso.Replace('/', Path.DirectorySeparatorChar));
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, testo);
            return Formati.Usa(file, new RaccoltaDiAvvisi(), new LettiPerTipo(file, percorso), out var letti) ? letti : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return null;
        }
        finally
        {
            if (Directory.Exists(cartella))
                Directory.Delete(cartella, recursive: true);
        }
    }

    private static async Task<string?> Contenuto(Git git, string radice, string commit, string percorso, CancellationToken annulla)
    {
        var esito = await git.EseguiAsync(radice, ["show", $"{commit}:{percorso}"], annulla).ConfigureAwait(false);
        return esito.Riuscito ? esito.Uscita : null;
    }

    /// <summary>Legge il file temporaneo col lettore giusto: per ogni record il nome del tipo, l'etichetta, le righe scritte.</summary>
    private sealed class LettiPerTipo(string file, string relativo) : IUsoDelFormato<List<(string Tipo, string Nome, string Righe)>>
    {
        public List<(string Tipo, string Nome, string Righe)> Usa<T>(IFileParser<T> lettore, IFileSaver<T> scrittore)
            where T : class
        {
            var letto = lettore.Parse(file, new Vipi.Sectorfile.Shared.ColorPalette());
            return [.. letto.Records.Select(r => (
                DescrizioniDeiCampi.Di(r, relativo)?.Nome ?? r.GetType().Name,
                Ispettore.Etichetta(r),
                string.Join("\n", scrittore.Serialize(r))))];
        }
    }
}

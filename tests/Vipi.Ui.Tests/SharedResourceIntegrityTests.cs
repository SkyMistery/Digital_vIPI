using System.Text.RegularExpressions;
using System.Xml.Linq;
using Vipi.Application.Content;
using Vipi.Application.Diagnostics;
using Vipi.Application.Stats;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Xunit;
using Xunit.Abstractions;

namespace Vipi.Ui.Tests;

/// <summary>
/// Guardie sui due file di risorse condivise. Nascono da un guasto reale: 14 chiavi <c>Country_*</c> erano
/// finite due volte in ciascun file, il compilatore di risorse le scartava con altrettanti <c>MSB3568</c>, e
/// il job CI <c>build-net8</c> — che compila con <c>-warnaserror</c> — non passava più. La suite era verde
/// lo stesso, perché <c>dotnet test</c> gira senza quel flag: <b>1391 test verdi e build di produzione rotta
/// sono compatibili</b>, ed è la ragione per cui queste tre guardie stanno qui e non nella CI.
///
/// <para>Leggono i <c>.resx</c> dal disco, non le risorse compilate: un duplicato, per definizione, nella
/// risorsa compilata non c'è più — l'ha già buttato via chi compila.</para>
/// </summary>
public sealed class SharedResourceIntegrityTests
{
    private readonly ITestOutputHelper _out;

    public SharedResourceIntegrityTests(ITestOutputHelper output) => _out = output;

    private const string PercorsoIt = "src/Vipi.Ui/Resources/SharedResource.resx";
    private const string PercorsoEn = "src/Vipi.Ui/Resources/SharedResource.en.resx";

    [Theory]
    [InlineData(PercorsoIt)]
    [InlineData(PercorsoEn)]
    public void Nessuna_chiave_ripetuta(string percorsoRelativo)
    {
        var chiavi = Chiavi(percorsoRelativo);
        var doppie = chiavi.GroupBy(k => k, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} ×{g.Count()}")
            .ToList();

        foreach (var d in doppie) _out.WriteLine(d);

        Assert.True(doppie.Count == 0,
            $"{percorsoRelativo}: {doppie.Count} chiavi ripetute. Il compilatore di risorse le scarta con " +
            "MSB3568 e con -warnaserror la build di produzione fallisce; in più quale delle due vinca " +
            "dipende dall'ordine nel file, cioè da niente di dichiarato.\n  " + string.Join("\n  ", doppie));
    }

    [Fact]
    public void Italiano_e_inglese_hanno_le_stesse_chiavi()
    {
        var it = Chiavi(PercorsoIt).ToHashSet(StringComparer.Ordinal);
        var en = Chiavi(PercorsoEn).ToHashSet(StringComparer.Ordinal);

        var soloIt = it.Except(en).OrderBy(k => k, StringComparer.Ordinal).ToList();
        var soloEn = en.Except(it).OrderBy(k => k, StringComparer.Ordinal).ToList();

        Assert.True(soloIt.Count == 0 && soloEn.Count == 0,
            "I due file di risorse non combaciano. Una chiave che esiste solo in italiano non è un errore " +
            "di compilazione: in inglese ricade sul valore italiano, in silenzio.\n" +
            $"  solo in it ({soloIt.Count}): {string.Join(", ", soloIt)}\n" +
            $"  solo in en ({soloEn.Count}): {string.Join(", ", soloEn)}");
    }

    /// <summary>
    /// Ogni <c>L["Chiave"]</c> scritto nei sorgenti deve esistere nelle risorse: una chiave assente non
    /// lancia — il localizzatore restituisce il NOME della chiave, che finisce a schermo.
    ///
    /// <para>⚠️ <b>Anche <c>En["Chiave"]</c></b> (<see cref="EnglishStrings"/>, la briciola di pane in
    /// inglese fisso): legge le <b>stesse</b> chiavi dallo stesso resx e si comporta allo stesso modo —
    /// chiave assente, nome della chiave a schermo. Guardarne una sola voleva dire lasciare scoperta metà
    /// della briciola, che sta in cima a ventinove pagine.</para>
    ///
    /// <para>⚠️ Restano fuori le chiavi <b>composte a runtime</b>: le copre
    /// <see cref="Ogni_valore_di_enum_reso_a_schermo_ha_la_sua_chiave"/>, che parte dall'enum invece che
    /// dal sorgente.</para>
    /// </summary>
    [Fact]
    public void Ogni_chiave_usata_nel_codice_esiste_nelle_risorse()
    {
        var definite = Chiavi(PercorsoIt).ToHashSet(StringComparer.Ordinal);
        var radice = RadiceDelRepo();

        // ⚠️ Anche `Vipi.Host`, e non è teoria: l'host ha un `<head>`, due pagine d'errore e i suoi
        // endpoint, e niente gli impedisce di chiedere una chiave. Finché la guardia guardava una cartella
        // sola, una chiave sbagliata scritta là si sarebbe vista solo a schermo — che è esattamente il modo
        // in cui è arrivata fin qui quella di ImpactKind.
        var progetti = new[] { "Vipi.Ui", "Vipi.Host" }
            .Select(p => Path.Combine(radice, "src", p))
            .Where(Directory.Exists);

        var usate = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in progetti
                     .SelectMany(c => Directory.EnumerateFiles(c, "*.*", SearchOption.AllDirectories))
                     .Where(f => (f.EndsWith(".razor", StringComparison.OrdinalIgnoreCase) ||
                                  f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) &&
                                 !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                                 !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
        {
            foreach (Match m in Regex.Matches(File.ReadAllText(file), @"\b(?:L|En)\[""([A-Za-z0-9_]+)""(?<coda>\s*\+)?"))
            {
                // `L["Country_" + code]`: la chiave si compone a runtime, qui non è verificabile.
                if (m.Groups["coda"].Success) continue;
                usate.TryAdd(m.Groups[1].Value, Path.GetRelativePath(radice, file));
            }
        }

        var mancanti = usate.Where(kv => !definite.Contains(kv.Key))
            .Select(kv => $"{kv.Key}  ({kv.Value})")
            .ToList();

        foreach (var m in mancanti) _out.WriteLine(m);

        Assert.True(mancanti.Count == 0,
            $"{mancanti.Count} chiavi usate nel codice e assenti da SharedResource.resx: a schermo compare " +
            "il nome della chiave.\n  " + string.Join("\n  ", mancanti));
    }

    /// <summary>
    /// Ogni <c>L["Chiave", a, b…]</c> passa ALMENO tanti argomenti quanti la frase ne chiede, in tutte e due le lingue.
    ///
    /// <para>🔴 <b>Nasce da un difetto di produzione.</b> <c>Ape_ReimportDone</c> ha quattro segnaposto ({3} = le
    /// procedure SID/STAR); l'editor militare ne passava tre. <c>string.Format</c> lancia, il localizzatore rilancia,
    /// e il circuito cadeva a ogni «Re-import da IVAO» (2 volte il 23 settembre 2026, 1.43.0). Nessuna guardia lo
    /// vedeva: la chiave esisteva, ed è l'unica cosa che <see cref="Ogni_chiave_usata_nel_codice_esiste_nelle_risorse"/>
    /// controlla.</para>
    ///
    /// <para>⚠️ Senza argomenti (<c>L["Chiave"]</c>) non si controlla: il localizzatore non formatta, e c'è chi usa la
    /// frase come modello. Argomenti IN PIÙ sono innocui (<c>string.Format</c> li ignora), quindi non si contano.</para>
    /// </summary>
    [Fact]
    public void Ogni_chiamata_con_argomenti_ne_passa_quanti_la_frase_ne_chiede()
    {
        var it = Segnaposto(PercorsoIt);
        var en = Segnaposto(PercorsoEn);
        var radice = RadiceDelRepo();

        var controllate = 0;
        var corte = new List<string>();
        foreach (var file in new[] { "Vipi.Ui", "Vipi.Host" }
                     .Select(p => Path.Combine(radice, "src", p))
                     .Where(Directory.Exists)
                     .SelectMany(c => Directory.EnumerateFiles(c, "*.*", SearchOption.AllDirectories))
                     .Where(f => (f.EndsWith(".razor", StringComparison.OrdinalIgnoreCase) ||
                                  f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) &&
                                 !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                                 !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
        {
            var testo = File.ReadAllText(file);
            foreach (Match m in Regex.Matches(testo, @"\b(?:L|En)\[""([A-Za-z0-9_]+)""\s*,"))
            {
                var passati = ContaArgomenti(testo, m.Index + m.Length);
                if (passati < 0) continue;   // non ho trovato la chiusura: meglio tacere che inventare
                controllate++;
                var chiave = m.Groups[1].Value;
                var chiesti = Math.Max(it.GetValueOrDefault(chiave), en.GetValueOrDefault(chiave));
                if (passati < chiesti)
                    corte.Add($"{chiave}: {passati} argomenti, la frase ne chiede {chiesti}  " +
                              $"({Path.GetRelativePath(radice, file)}:{testo[..m.Index].Count(c => c == '\n') + 1})");
            }
        }

        foreach (var c in corte) _out.WriteLine(c);

        // Un controllo che non trova niente non prova niente: il giorno che la regex smettesse di combaciare
        // passerebbe verde per sempre.
        Assert.True(controllate > 50, $"solo {controllate} chiamate con argomenti trovate: la regex non combacia più?");
        Assert.True(corte.Count == 0,
            $"{corte.Count} chiamate passano meno argomenti di quanti ne chiede la frase: string.Format lancia e " +
            "il circuito cade quando quel ramo gira davvero.\n  " + string.Join("\n  ", corte));
    }

    /// <summary>Per ogni chiave, quanti argomenti chiede la frase: l'indice di segnaposto più alto + 1.</summary>
    private static Dictionary<string, int> Segnaposto(string percorsoRelativo)
    {
        var percorso = Path.Combine(RadiceDelRepo(), percorsoRelativo.Replace('/', Path.DirectorySeparatorChar));
        var esito = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var e in XDocument.Load(percorso).Root!.Elements("data"))
        {
            var nome = e.Attribute("name")?.Value;
            var valore = e.Element("value")?.Value;
            if (string.IsNullOrEmpty(nome) || valore is null) continue;
            // `{{` e `}}` sono graffe letterali, non segnaposto.
            var pulito = valore.Replace("{{", "", StringComparison.Ordinal);
            var massimo = Regex.Matches(pulito, @"\{(\d+)").Select(x => int.Parse(x.Groups[1].Value) + 1)
                .DefaultIfEmpty(0).Max();
            esito[nome] = massimo;
        }
        return esito;
    }

    /// <summary>
    /// Conta gli argomenti dopo la chiave, partendo subito dopo la PRIMA virgola, fino alla <c>]</c> che chiude
    /// l'indicizzatore. Salta parentesi annidate, stringhe e caratteri. −1 se la chiusura non si trova.
    /// </summary>
    private static int ContaArgomenti(string testo, int da)
    {
        var argomenti = 1;
        var profondita = 0;
        for (var i = da; i < testo.Length; i++)
        {
            var c = testo[i];
            if (c is '"' or '\'')
            {
                // Stringa o carattere: si salta fino alla chiusura, rispettando le sequenze di escape.
                for (i++; i < testo.Length && testo[i] != c; i++)
                    if (testo[i] == '\\') i++;
                continue;
            }
            if (c is '(' or '[' or '{') profondita++;
            else if (c is ')' or '}') profondita--;
            else if (c == ']')
            {
                if (profondita == 0) return argomenti;
                profondita--;
            }
            else if (c == ',' && profondita == 0) argomenti++;
            if (profondita < 0) return -1;
        }
        return -1;
    }

    /// <summary>
    /// L'altra famiglia composta a runtime, e non nasce da un enum: le chiavi del VERSO nell'editor delle
    /// procedure. <c>AirportSidsEditor</c> è montato due volte — partenze e arrivi — e chiede le sue frasi
    /// con <c>L[K("Ape_SidQualcosa")]</c>, dove <c>K</c> scambia <c>_Sid</c> con <c>_Star</c> sugli arrivi.
    ///
    /// <para>🔴 <b>Erano fuori da TUTTE le guardie.</b> <see cref="Ogni_chiave_usata_nel_codice_esiste_nelle_risorse"/>
    /// cerca il letterale <c>L["…"]</c> e <c>L[K("…")]</c> non lo è; <see cref="Ogni_valore_di_enum_reso_a_schermo_ha_la_sua_chiave"/>
    /// parte da un enum e qui di enum non ce n'è. Restavano scoperte 17 chiavi per verso: una gemella
    /// dimenticata non lancia niente — stampa il NOME DELLA CHIAVE in testa alla tabella.</para>
    ///
    /// <para>⚠️ Si pretende anche di averne TROVATE: un controllo che non trova niente non prova niente, e
    /// una regex che smettesse di combaciare (il giorno che <c>K</c> si chiama altrimenti) passerebbe verde
    /// per sempre.</para>
    /// </summary>
    [Fact]
    public void Ogni_chiave_composta_col_verso_esiste_nelle_due_forme()
    {
        var it = Chiavi(PercorsoIt).ToHashSet(StringComparer.Ordinal);
        var en = Chiavi(PercorsoEn).ToHashSet(StringComparer.Ordinal);
        var radice = RadiceDelRepo();

        var usate = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory
                     .EnumerateFiles(Path.Combine(radice, "src", "Vipi.Ui"), "*.razor", SearchOption.AllDirectories)
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
            foreach (Match m in Regex.Matches(File.ReadAllText(file), @"\bL\[K\(""([A-Za-z0-9_]+)""\)"))
                usate.TryAdd(m.Groups[1].Value, Path.GetRelativePath(radice, file));

        Assert.True(usate.Count > 0, "nessuna chiave composta con K(\"…\") trovata: la regex non combacia più.");

        var mancanti = new List<string>();
        foreach (var (chiave, dove) in usate)
            foreach (var forma in new[] { chiave, chiave.Replace("_Sid", "_Star", StringComparison.Ordinal) })
            {
                if (!it.Contains(forma)) mancanti.Add($"{forma}  (it, da {dove})");
                if (!en.Contains(forma)) mancanti.Add($"{forma}  (en, da {dove})");
            }

        foreach (var m in mancanti) _out.WriteLine(m);

        Assert.True(mancanti.Count == 0,
            $"{mancanti.Count} forme di chiavi composte col verso assenti dalle risorse: sulla tabella del " +
            "verso che le chiede comparirebbe il nome della chiave.\n  " + string.Join("\n  ", mancanti));
    }

    /// <summary>
    /// Le famiglie di chiavi che si compongono a runtime da un <b>enum</b>: prefisso e vocabolario che lo
    /// riempie. È dichiarativa apposta — se un domani si aggiunge un enum reso con
    /// <c>L["Prefisso_" + valore]</c>, si aggiunge una riga qui.
    ///
    /// <para>⚠️ <b>Si scrive <c>typeof</c> e non il nome dell'enum come stringa</b>: così un enum rinominato
    /// o spostato non compila, invece di far passare un test che ha smesso di guardare qualcosa.</para>
    /// </summary>
    public static TheoryData<string, Type> FamiglieComposte() => new()
    {
        { "Audit_Cat_", typeof(AuditNarrator.Categoria) },
        { "TaskStatus_", typeof(EditorTaskStatus) },
        { "Stats_Tag_", typeof(TrafficTag) },
        { "Stats_TagHint_", typeof(TrafficTag) },
        { "Diag_Area_", typeof(ConsistencyArea) },
        { "Sorg_St_", typeof(ImportHealth) },
        // Due famiglie sullo stesso enum: l'etichetta corta del riepilogo in Diagnostica e la frase intera
        // della riga «da rivedere».
        { "ImpactKind_", typeof(ImpactKind) },
        { "Impact_", typeof(ImpactKind) },
        // I due assi della biblioteca allegati: sono chip, colonne e voci di tendina, quindi ogni valore
        // nuovo si vede a schermo — e senza la sua riga si vedrebbe il NOME DELLA CHIAVE.
        { "AttKind_", typeof(AttachmentKind) },
        { "AttScope_", typeof(AttachmentScope) },
        // Da dove arriva una citazione: è la pillola davanti a ogni riga di «citato da», e senza la sua
        // chiave si leggerebbe il nome del valore — proprio nella schermata su cui si decide una cancellazione.
        { "AttSrc_", typeof(Vipi.Application.Abstractions.AttachmentCitationSource) },
        // Il modo di resa di un blocco allegato e l'altezza del riquadro: sono due tendine dell'editor.
        { "AttMode_", typeof(AttachmentDisplayMode) },
        { "AttHeight_", typeof(AttachmentEmbedHeight) },
    };

    /// <summary>
    /// Ogni valore di un enum reso a schermo ha la sua riga nelle risorse.
    ///
    /// <para>⚠️ <b>È il buco da cui è passato un difetto vero.</b>
    /// <see cref="Ogni_chiave_usata_nel_codice_esiste_nelle_risorse"/> salta di proposito le chiavi
    /// composte — <c>L["ImpactKind_" + r.Key]</c> non è verificabile leggendo il sorgente — e in quel buco
    /// stavano <c>ImpactKind_SectorRenamed</c> e <c>ImpactKind_SectorDetached</c>, mai scritte in nessuno
    /// dei due file. Entrambi gli impatti si alzano davvero (<c>EfCallsignRenameService</c>,
    /// <c>DeletionService</c>), quindi la tabella di Diagnostica scriveva <b>il nome della chiave</b> —
    /// in italiano e in inglese. Un valore aggiunto a un enum non rompe niente: fa comparire il suo nome
    /// tecnico a schermo, e nessuno lo denuncia.</para>
    ///
    /// <para>Il controllo è a senso unico e dev'esserlo: si pretende che ogni valore abbia la sua chiave,
    /// non che ogni chiave col prefisso corrisponda a un valore — <c>Impact_Title</c> e
    /// <c>Impact_ToReview</c> stanno in quella famiglia e non sono impatti.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(FamiglieComposte))]
    public void Ogni_valore_di_enum_reso_a_schermo_ha_la_sua_chiave(string prefisso, Type enumerazione)
    {
        var it = Chiavi(PercorsoIt).ToHashSet(StringComparer.Ordinal);
        var en = Chiavi(PercorsoEn).ToHashSet(StringComparer.Ordinal);

        var mancanti = Enum.GetNames(enumerazione)
            .Select(v => prefisso + v)
            .Where(k => !it.Contains(k) || !en.Contains(k))
            .ToList();

        foreach (var m in mancanti) _out.WriteLine(m);

        Assert.True(mancanti.Count == 0,
            $"{enumerazione.Name}: {mancanti.Count} valori senza riga nelle risorse. A schermo compare il " +
            "nome della chiave, e succede solo quando quel valore capita davvero — cioè non in una prova, " +
            "ma su un sito vero davanti a chi lo usa.\n  " + string.Join("\n  ", mancanti));
    }

    /// <summary>
    /// I giorni della settimana, l'altra famiglia composta a runtime (<c>L[$"Day_{g}"]</c> in
    /// <c>StatsHome</c> e <c>CoverageHeatmap</c>). ⚠️ Non è un enum: l'indice è <b>1 = lunedì</b> e arriva
    /// da un conteggio, quindi la sola cosa che si può pretendere è che ci siano tutti e sette, corti e
    /// per esteso. Un giorno mancante darebbe «Day_7» come intestazione di colonna.
    /// </summary>
    [Fact]
    public void I_sette_giorni_ci_sono_in_tutte_e_due_le_forme()
    {
        var it = Chiavi(PercorsoIt).ToHashSet(StringComparer.Ordinal);
        var en = Chiavi(PercorsoEn).ToHashSet(StringComparer.Ordinal);

        var mancanti = Enumerable.Range(1, 7)
            .SelectMany(g => new[] { $"Day_{g}", $"Day_{g}_Full" })
            .Where(k => !it.Contains(k) || !en.Contains(k))
            .ToList();

        Assert.True(mancanti.Count == 0,
            "Giorni senza riga nelle risorse: a schermo compare «Day_3» al posto del giorno.\n  " +
            string.Join("\n  ", mancanti));
    }

    /// <summary>
    /// Ogni rilievo del confronto col sectorfile ha le sue <b>tre</b> righe nelle risorse: la categoria, la
    /// spiegazione e il bersaglio.
    ///
    /// <para>⚠️ Sono chiavi che il sorgente non nomina mai come letterali — stanno in costanti
    /// (<c>SectorfileComparison.CatFrequenza</c>) e nel campo <c>DetailKey</c> di un record — quindi
    /// <see cref="Ogni_chiave_usata_nel_codice_esiste_nelle_risorse"/> non le vede. È lo stesso buco da cui
    /// erano passati <c>ImpactKind_SectorRenamed</c> e <c>ImpactKind_SectorDetached</c>: chiavi mancanti che
    /// non rompono niente e fanno comparire il <b>nome della chiave</b> a schermo, in tutte e due le lingue,
    /// solo quando quel rilievo capita davvero.</para>
    /// </summary>
    [Fact]
    public void Ogni_rilievo_di_coerenza_sectorfile_ha_le_sue_righe()
    {
        var it = Chiavi(PercorsoIt).ToHashSet(StringComparer.Ordinal);
        var en = Chiavi(PercorsoEn).ToHashSet(StringComparer.Ordinal);

        var attese = Vipi.Application.Diagnostics.SectorfileComparison.Categorie
            .SelectMany(c => new[] { c, c.Replace("Diag_Cat_", "Diag_Msg_", StringComparison.Ordinal) })
            .Concat(new[] { "Diag_Ent_SfPosizione", "Diag_Ent_SfAeroporto", "Diag_Ent_SfPista" })
            .ToList();

        var mancanti = attese.Where(k => !it.Contains(k) || !en.Contains(k)).ToList();

        Assert.True(mancanti.Count == 0,
            $"{mancanti.Count} chiavi del confronto col sectorfile senza riga nelle risorse.\n  " +
            string.Join("\n  ", mancanti));
    }

    private static IReadOnlyList<string> Chiavi(string percorsoRelativo)
    {
        var percorso = Path.Combine(RadiceDelRepo(), percorsoRelativo.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(percorso), "File di risorse non trovato: " + percorso);

        // I `<data>` di intestazione (mimetype/version/reader/writer) sono `<resheader>`: non entrano.
        return XDocument.Load(percorso).Root!.Elements("data")
            .Select(e => e.Attribute("name")?.Value)
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n!)
            .ToList();
    }

    /// <summary>Risale dalla cartella dell'assembly fino alla soluzione: fallisce forte se non la trova.</summary>
    private static string RadiceDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Vipi.slnx"))) dir = dir.Parent;
        Assert.True(dir is not null, "Vipi.slnx non trovata risalendo da " + AppContext.BaseDirectory);
        return dir!.FullName;
    }
}

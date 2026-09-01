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

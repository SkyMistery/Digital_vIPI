using System.Text.Json;
using Vipi.Application.Content;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Il lettore delle traduzioni congelate legge <b>due forme</b> e ne scrive <b>una</b>.
///
/// <para>
/// ⚠️ <b>Perché conta più di quanto sembri.</b> Gli snapshot di release sono documenti <b>in vigore</b>:
/// stanno nel database, sono ciò che il pubblico legge, e l'unico modo di riscriverli è che il loro editor
/// ripubblichi. Un cambio di forma che non sapesse leggere quella vecchia non darebbe un errore: darebbe
/// un'eccezione di deserializzazione su una pagina pubblica, oppure — peggio — un documento che si apre
/// con tutte le traduzioni sparite.
/// </para>
///
/// <para>La forma vecchia (fino al 28 agosto 2026) è la stringa nuda: <c>"impronta": "testo"</c>.</para>
/// </summary>
public class FrozenTranslationJsonTests
{
    private static Dictionary<string, FrozenTranslation> Leggi(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, FrozenTranslation>>(json)!;

    [Fact]
    public void La_stringa_nuda_delle_release_vecchie_si_legge_ancora()
    {
        var letto = Leggi("""{"abc":"Contact the tower."}""");

        Assert.Equal("Contact the tower.", letto["abc"].Text);
        // ⚠️ Non riletta, ed è tutto quello che quello snapshot può dire di sé: il timbro non c'era.
        Assert.False(letto["abc"].Reviewed);
    }

    [Fact]
    public void La_forma_nuova_porta_il_timbro()
    {
        var letto = Leggi("""{"a":{"t":"Riletta","r":true},"b":{"t":"No","r":false}}""");

        Assert.Equal("Riletta", letto["a"].Text);
        Assert.True(letto["a"].Reviewed);
        Assert.False(letto["b"].Reviewed);
    }

    [Fact]
    public void Le_due_forme_convivono_nello_stesso_snapshot()
    {
        // Non è un caso di laboratorio: è quel che succede se un domani si congelasse per lingue diverse
        // in momenti diversi, o se qualcuno rimettesse in circolo un payload vecchio.
        var letto = Leggi("""{"vecchia":"Testo","nuova":{"t":"Testo","r":true}}""");

        Assert.Equal("Testo", letto["vecchia"].Text);
        Assert.False(letto["vecchia"].Reviewed);
        Assert.True(letto["nuova"].Reviewed);
    }

    [Fact]
    public void Un_campo_che_questa_versione_non_conosce_non_fa_inciampare()
    {
        var letto = Leggi("""{"a":{"t":"Testo","r":true,"motore":"azure","quando":"2026-08-28"}}""");

        Assert.Equal("Testo", letto["a"].Text);
        Assert.True(letto["a"].Reviewed);
    }

    [Theory]
    [InlineData("""{"a":null}""")]
    [InlineData("""{"a":42}""")]
    [InlineData("""{"a":["testo"]}""")]
    [InlineData("""{"a":{"t":99,"r":"forse"}}""")]
    public void Una_forma_sconosciuta_vale_NIENTE_DI_CONGELATO_e_non_solleva(string json)
    {
        // ⚠️ Uno snapshot è un documento in vigore: se una voce è illeggibile si legge «niente di
        // congelato» e quella frase resta nella lingua sorgente. Un documento a chiazze si legge male ma
        // si legge; un documento che non si apre non si legge affatto.
        var letto = Leggi(json);

        Assert.False(letto["a"].HasText);
        Assert.False(letto["a"].Reviewed);
    }

    [Fact]
    public void Una_voce_illeggibile_non_rovina_quelle_dopo()
    {
        // ⚠️ È la ragione per cui la forma sconosciuta si CONSUMA invece di essere solo ignorata: un
        // lettore lasciato a metà di un valore sbaglia tutto il resto dell'oggetto — cioè trasforma un
        // segmento illeggibile in un documento illeggibile.
        var letto = Leggi("""{"rotta":[1,2,{"x":"y"}],"buona":{"t":"Sto qui","r":true}}""");

        Assert.False(letto["rotta"].HasText);
        Assert.Equal("Sto qui", letto["buona"].Text);
        Assert.True(letto["buona"].Reviewed);
    }

    [Fact]
    public void Si_scrive_una_forma_sola_anche_quando_il_timbro_e_falso()
    {
        // Due forme in uscita vorrebbero dire che la forma di un dato dipende dal suo valore: chi apre lo
        // snapshot a mano — che è come si guarda quando qualcosa non torna — vedrebbe metà dei segmenti in
        // un modo e metà nell'altro senza capire perché.
        var json = JsonSerializer.Serialize(new Dictionary<string, FrozenTranslation>
        {
            ["a"] = new("Riletta", true),
            ["b"] = new("No", false),
        });

        Assert.Equal("""{"a":{"t":"Riletta","r":true},"b":{"t":"No","r":false}}""", json);
    }

    [Fact]
    public void Quel_che_si_scrive_si_rilegge_uguale()
    {
        var originale = new Dictionary<string, FrozenTranslation>
        {
            ["a"] = new("Con le \"virgolette\" e un a-capo\ninterno", true),
            ["b"] = new("", false),
        };

        var riletto = Leggi(JsonSerializer.Serialize(originale));

        Assert.Equal(originale["a"], riletto["a"]);
        Assert.Equal(originale["b"], riletto["b"]);
    }

    // ---- Il giro intero, sul payload vero -------------------------------------------------------------

    /// <summary>
    /// ⚠️ I test qui sopra provano il convertitore <b>da solo</b>, e non basta: quel che finisce nel
    /// database è un <see cref="DocReleasePayload"/> serializzato con la chiamata di
    /// <c>ReleaseService.BuildSnapshotJsonAsync</c>, due livelli di annidamento più in là. Se un domani
    /// quella chiamata prendesse delle <c>JsonSerializerOptions</c> — una politica sui nomi, un
    /// convertitore generico — il timbro potrebbe perdersi <b>in silenzio</b>, e il difetto tornerebbe
    /// identico a com'era: l'avviso che non si spegne più.
    /// </summary>
    private static string SerializzaComeUnaRelease(DocReleasePayload payload) => JsonSerializer.Serialize(payload);

    private static DocReleasePayload PayloadCon(Dictionary<string, Dictionary<string, FrozenTranslation>> congelate) =>
        new()
        {
            Doc = new RawDocument
            {
                Title = "vIPI — LIBC Crotone",
                AiracCycle = "2609",
                Roots = new List<RawSection>(),
                Language = Vipi.Domain.Language.It,
                Translations = congelate,
            },
        };

    [Fact]
    public void Il_timbro_sopravvive_al_giro_completo_dello_snapshot()
    {
        var payload = PayloadCon(new(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = new(StringComparer.Ordinal)
            {
                ["h1"] = new("Reviewed by a person", true),
                ["h2"] = new("Straight from the engine", false),
            },
        });

        var riletto = JsonSerializer.Deserialize<DocReleasePayload>(SerializzaComeUnaRelease(payload))!;
        var congelate = riletto.Doc.Translations!["en"];

        Assert.True(congelate["h1"].Reviewed);
        Assert.Equal("Reviewed by a person", congelate["h1"].Text);
        Assert.False(congelate["h2"].Reviewed);
    }

    [Fact]
    public void Uno_snapshot_pubblicato_PRIMA_del_timbro_si_apre_ancora()
    {
        // Il payload come lo scriveva il codice fino al 28 agosto 2026: la traduzione è una stringa nuda.
        // Questi snapshot sono documenti IN VIGORE e nessuno li riscrive finché il loro editor non
        // ripubblica — se non si aprissero, il pubblico vedrebbe un errore al posto di una vLOA.
        const string vecchio = """
            {"Doc":{"Title":"vLOA","AiracCycle":"2609","Roots":[],"Language":1,
             "Translations":{"it":{"h1":"La presente lettera si applica."}}},"FrozenSections":{}}
            """;

        var riletto = JsonSerializer.Deserialize<DocReleasePayload>(vecchio)!;
        var congelate = riletto.Doc.Translations!["it"];

        Assert.Equal("La presente lettera si applica.", congelate["h1"].Text);
        Assert.False(congelate["h1"].Reviewed);
    }
}

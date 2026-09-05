using Vipi.Application.Content;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Dove sta il payload di una sezione «scheda + blocchi», e soprattutto dove NON sta. La regola è una sola e la
/// usano lettura (viewer, editor, cattura Frozen) e scrittura (repository): chiederla in due modi diversi è il
/// modo di perdere un payload — o il contenuto di chi redige — senza che nessuno protesti.
/// </summary>
public class SectionPayloadTests
{
    private const string TabellaAMano = "{\"columns\":[\"Colonna 1\"],\"rows\":[{\"cells\":[\"CELLA-MIA\"]}]}";
    private const string Immagine = "{\"mediaId\":\"abc\",\"alt\":null,\"width\":800,\"height\":600}";
    private const string Allegato = "{\"ref\":\"allegato:loa-lfmm\",\"titolo\":\"LoA Marsiglia\"}";
    private const string PayloadMil = "{\"variant\":\"milnavaids\",\"rows\":[{\"code\":\"TAR\"}]}";
    private const string PayloadApp = "{\"Key\":\"aerovia\",\"Kind\":0,\"MemberCallsigns\":[]}";
    private const string PayloadAree = "[\"1029\",\"1068\"]";

    [Theory]
    [InlineData(TabellaAMano)]
    [InlineData(Immagine)]
    [InlineData(Allegato)]
    public void I_blocchi_editoriali_non_sono_payload(string json) =>
        Assert.True(SectionPayload.EEditoriale(json));

    [Theory]
    [InlineData(PayloadMil)]
    [InlineData(PayloadApp)]
    [InlineData(PayloadAree)]        // forma storica delle aree: la radice è un ARRAY, non un oggetto
    [InlineData("{\"title\":\"\"}")]
    [InlineData("{\"OwnAuto\":false,\"OwnIds\":[\"1161\"]}")]
    public void I_payload_delle_schede_restano_payload(string json) =>
        Assert.False(SectionPayload.EEditoriale(json));

    [Fact]
    public void Una_tabella_di_struttura_ha_la_variante_e_non_si_confonde_con_quella_a_mano()
    {
        // Le due si somigliano — sono tutt'e due tabelle — e la variante è ciò che le distingue.
        Assert.True(SectionPayload.EEditoriale("{\"columns\":[],\"rows\":[]}"));
        Assert.False(SectionPayload.EEditoriale("{\"variant\":\"milcallsigns\",\"columns\":[],\"rows\":[]}"));
    }

    [Fact]
    public void Si_salta_il_contenuto_e_si_prende_la_struttura_che_viene_dopo()
    {
        var scelto = SectionPayload.Scegli(new[] { null, "  ", TabellaAMano, Immagine, PayloadMil });

        Assert.Equal(PayloadMil, scelto);
    }

    [Fact]
    public void Se_ci_sono_solo_blocchi_editoriali_la_sezione_non_ha_payload()
    {
        // ⚠️ È il cuore del difetto del 5 settembre 2026: qui prima tornava la tabella di chi redige, e la
        // scheda ci scriveva sopra al primo salvataggio.
        Assert.Null(SectionPayload.Scegli(new[] { TabellaAMano, Immagine }));
        Assert.Null(SectionPayload.Scegli(new string?[] { null, "" }));
        Assert.Null(SectionPayload.Scegli(null));
    }

    [Fact]
    public void Un_json_rotto_non_si_riscrive()
    {
        // Non è una struttura leggibile: trattarlo da payload vorrebbe dire sovrascriverlo alla prima
        // occasione, e quel blocco è comunque roba di qualcuno.
        Assert.True(SectionPayload.EEditoriale("{oops"));
    }
}

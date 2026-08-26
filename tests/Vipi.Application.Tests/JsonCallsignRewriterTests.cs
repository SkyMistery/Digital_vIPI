using System.Text.Json;
using System.Text.Json.Nodes;
using Vipi.Application.Content;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// La riscrittura dei puntatori dentro <c>ContentBlock.BodyJson</c>. Le forme provate sono quelle vere,
/// copiate dal <c>vipi.db</c> del 26 agosto 2026 (35 righe le usano).
/// </summary>
public class JsonCallsignRewriterTests
{
    private static string? Rewrite(string json, string vecchio = "LIMF_TWR", string nuovo = "LIMF_N_TWR") =>
        JsonCallsignRewriter.Rewrite(json, vecchio, nuovo);

    [Fact]
    public void Riscrive_un_elenco_di_callsign()
    {
        var esito = Rewrite("""{"Callsigns":["LIMF_TWR","LIMZ_TWR"],"Colors":{}}""");

        var arr = JsonNode.Parse(esito!)!["Callsigns"]!.AsArray();
        Assert.Equal(new[] { "LIMF_N_TWR", "LIMZ_TWR" }, arr.Select(x => x!.GetValue<string>()));
    }

    [Fact]
    public void Riscrive_dentro_gli_oggetti_annidati_di_un_array()
    {
        // La forma delle configurazioni AoR: Open è un array di oggetti, OpenCallsigns un array di stringhe.
        const string json = """
            [{"Key":"cfg:bbbb0611","Name":"Conf 1",
              "Open":[{"Callsign":"LIMF_WW0_APP","CenterPoint":null,"Range":null}],
              "OpenCallsigns":["LIMF_WW0_APP"]}]
            """;

        var esito = Rewrite(json, "LIMF_WW0_APP", "LIMF_W_APP")!;

        var cfg = JsonNode.Parse(esito)!.AsArray()[0]!;
        Assert.Equal("LIMF_W_APP", cfg["Open"]![0]!["Callsign"]!.GetValue<string>());
        Assert.Equal("LIMF_W_APP", cfg["OpenCallsigns"]![0]!.GetValue<string>());
        Assert.Equal("Conf 1", cfg["Name"]!.GetValue<string>());          // i nomi liberi restano
        Assert.Equal("cfg:bbbb0611", cfg["Key"]!.GetValue<string>());     // e le chiavi pure
    }

    /// <summary>
    /// La ragione per cui si cammina l'albero invece di fare cerca-e-sostituisci: un callsign è prefisso di
    /// un altro, e una sostituzione testuale rovinerebbe il secondo.
    /// </summary>
    [Fact]
    public void Non_tocca_un_callsign_che_comincia_col_vecchio()
    {
        var esito = Rewrite("""{"MemberCallsigns":["LIRR_NE_CTR","LIRR_NE1_CTR"]}""",
            "LIRR_NE_CTR", "LIRR_N_CTR")!;

        var arr = JsonNode.Parse(esito)!["MemberCallsigns"]!.AsArray();
        Assert.Equal(new[] { "LIRR_N_CTR", "LIRR_NE1_CTR" }, arr.Select(x => x!.GetValue<string>()));
    }

    [Fact]
    public void Non_tocca_una_chiave_che_si_chiama_come_il_callsign()
    {
        var esito = Rewrite("""{"LIMF_TWR":{"Range":5}}""");

        // La chiave resta; non c'è nessun VALORE stringa da cambiare, quindi non c'è niente da riscrivere.
        Assert.Null(esito);
    }

    [Fact]
    public void Senza_occorrenze_non_riscrive_e_non_riformatta() =>
        Assert.Null(Rewrite("""{ "Callsigns" : [ "LIMZ_TWR" ] }"""));

    [Fact]
    public void Un_testo_che_non_e_json_si_lascia_stare() =>
        Assert.Null(Rewrite("LIMF_TWR trasferisce a LIMZ_TWR"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Vuoto_o_nullo_non_produce_niente(string? json) =>
        Assert.Null(JsonCallsignRewriter.Rewrite(json, "LIMF_TWR", "LIMF_N_TWR"));

    [Fact]
    public void Il_confronto_ignora_le_maiuscole()
    {
        var esito = Rewrite("""{"Callsigns":["limf_twr"]}""")!;
        Assert.Equal("LIMF_N_TWR", JsonNode.Parse(esito)!["Callsigns"]![0]!.GetValue<string>());
    }

    /// <summary>
    /// Un contenitore annidato in profondità si muta sul posto: se il walker provasse a riassegnarlo al
    /// genitore, <c>System.Text.Json.Nodes</c> lancerebbe «The node already has a parent».
    /// </summary>
    [Fact]
    public void Riscrive_in_profondita_senza_staccare_i_nodi()
    {
        const string json = """{"a":{"b":{"c":[{"d":["LIMF_TWR"]}]}}}""";

        var esito = Rewrite(json)!;

        Assert.Equal("LIMF_N_TWR",
            JsonNode.Parse(esito)!["a"]!["b"]!["c"]![0]!["d"]![0]!.GetValue<string>());
    }

    [Fact]
    public void Una_radice_che_e_gia_il_callsign_si_sostituisce()
    {
        var esito = Rewrite("\"LIMF_TWR\"")!;
        Assert.Equal("LIMF_N_TWR", JsonSerializer.Deserialize<string>(esito));
    }
}

using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// La conversione dei testi già scritti (§A73, slice 5). I testi sono quelli della vSOP MIL LIBV sulla copia di
/// produzione del 18 settembre 2026: «Expect CDC6A», una cella «CDC6A/B».
/// </summary>
public class ConversioneSidTests
{
    private static readonly SidCitabile[] Libv =
    {
        new("LIBV", "CDC6A", "CDC 6A", "14L, 14R"),
        new("LIBV", "CDC6B", "CDC 6B", "32L, 32R"),
        new("LIBV", "VIE6A", "VIENNA 6A", "14L, 14R"),
    };

    private static BloccoDaCercare Prosa(int id, string testo, string dove = "Partenze") =>
        new(id, dove, BlockFormat.Prose, testo, null);

    private static BloccoDaCercare Tabella(int id, params string[] celle) =>
        new(id, "Punti", BlockFormat.Table, null, TabellaGenerica.Scrivi(new[] { "Punto", "SID" },
            new[] { (IReadOnlyList<string>)celle }));

    [Fact]
    public void Una_SID_scritta_a_mano_si_propone_col_suo_riferimento()
    {
        var esito = ConversioneSid.Cerca(new[] { Prosa(1, "RWY 14L/R: Expect CDC6A and then join the zones.") }, Libv);

        var p = Assert.Single(esito.Proposte);
        Assert.Equal(("CDC6A", "[[SID LIBV CDC6A]]", 1), (p.Trovato, p.Sid.Riferimento, p.Volte));
        Assert.Contains("Expect CDC6A and", p.Contesto);
    }

    [Theory]
    [InlineData("Expect VIENNA 6A.", "VIENNA 6A")]
    [InlineData("Expect VIENNA6A.", "VIENNA6A")]
    [InlineData("Expect VIE6A.", "VIE6A")]
    public void Si_riconoscono_codice_nome_completo_e_nome_senza_spazio(string testo, string trovato)
    {
        var p = Assert.Single(ConversioneSid.Cerca(new[] { Prosa(1, testo) }, Libv).Proposte);
        Assert.Equal(trovato, p.Trovato);
        Assert.Equal("[[SID LIBV VIE6A]]", p.Sid.Riferimento);
    }

    /// <summary>La cella della LIBV vera: due SID in una. Non si converte a metà, si elenca.</summary>
    [Fact]
    public void Una_forma_compatta_si_elenca_e_non_si_converte()
    {
        var esito = ConversioneSid.Cerca(new[] { Tabella(2, "DANTO", "CDC6A/B") }, Libv);

        Assert.Empty(esito.Proposte);
        var d = Assert.Single(esito.DaSistemare);
        Assert.Equal(("CDC6A/B", 2), (d.Trovato, d.BloccoId));
    }

    /// <summary>Una forma compatta che non ha niente a che fare con le SID dello scalo (una STAR) non si elenca.</summary>
    [Fact]
    public void Una_forma_compatta_che_non_e_una_SID_dello_scalo_si_ignora()
    {
        Assert.Empty(ConversioneSid.Cerca(new[] { Prosa(1, "Arrivals via ROZHU5A/5B.") }, Libv).DaSistemare);
    }

    [Fact]
    public void Ciò_che_è_già_un_riferimento_non_si_ripropone()
    {
        Assert.Empty(ConversioneSid.Cerca(new[] { Prosa(1, "Expect [[SID LIBV CDC6A]].") }, Libv).Proposte);
    }

    [Theory]
    [InlineData("XCDC6A")]
    [InlineData("CDC6AB")]
    [InlineData("cdc6a")]
    public void Solo_a_parola_intera_e_in_maiuscolo(string testo)
    {
        Assert.Empty(ConversioneSid.Cerca(new[] { Prosa(1, testo) }, Libv).Proposte);
    }

    [Fact]
    public void Convertire_cambia_solo_le_forme_scelte_e_lascia_il_resto()
    {
        var b = Prosa(1, "Expect CDC6A, or CDC6B; never [[SID LIBV CDC6A]] twice. CDC6A again.");
        var esito = ConversioneSid.Cerca(new[] { b }, Libv);
        var scelta = esito.Proposte.Where(p => p.Trovato == "CDC6A");

        var (body, json) = ConversioneSid.Converti(b, scelta);

        Assert.Equal("Expect [[SID LIBV CDC6A]], or CDC6B; never [[SID LIBV CDC6A]] twice. [[SID LIBV CDC6A]] again.", body);
        Assert.Null(json);
    }

    [Fact]
    public void In_una_tabella_si_convertono_le_celle_e_la_tabella_resta_la_stessa()
    {
        var b = Tabella(2, "DANTO", "CDC6A", "CDC6A/B");
        var esito = ConversioneSid.Cerca(new[] { b }, Libv);

        var (body, json) = ConversioneSid.Converti(b, esito.Proposte);

        Assert.Null(body);
        var (colonne, righe) = TabellaGenerica.Leggi(json);
        Assert.Equal(new[] { "Punto", "SID" }, colonne);
        Assert.Equal(new[] { "DANTO", "[[SID LIBV CDC6A]]", "CDC6A/B" }, righe[0]);
    }

    // ---- dalla revisione del 18 settembre 2026 ----------------------------------------------------------

    /// <summary>Le proprietà della tabella che non sono celle — ★, primario, gruppo, unificata, id — restano: la
    /// conversione tocca le celle sul JSON originale.</summary>
    [Fact]
    public void Convertire_una_cella_lascia_le_altre_proprieta_della_tabella()
    {
        const string json = """{"tableId":"cfg-ops","unified":true,"columns":["Punto","SID"],"rows":[{"group":"Nord","primary":true,"star":true,"r":"x1","cells":["DANTO","CDC6A"]}]}""";
        var b = new BloccoDaCercare(3, "Punti", BlockFormat.Table, null, json);

        var (_, nuovo) = ConversioneSid.Converti(b, ConversioneSid.Cerca(new[] { b }, Libv).Proposte);

        using var doc = System.Text.Json.JsonDocument.Parse(nuovo!);
        var r = doc.RootElement;
        Assert.Equal("cfg-ops", r.GetProperty("tableId").GetString());
        Assert.True(r.GetProperty("unified").GetBoolean());
        var riga = r.GetProperty("rows")[0];
        Assert.Equal(("Nord", true, true, "x1"),
            (riga.GetProperty("group").GetString(), riga.GetProperty("primary").GetBoolean(),
             riga.GetProperty("star").GetBoolean(), riga.GetProperty("r").GetString()));
        Assert.Equal("[[SID LIBV CDC6A]]", riga.GetProperty("cells")[1].GetString());
    }

    /// <summary>Due SID intere con la barra in mezzo: prima se ne convertiva una sola, e il testo restava a metà.</summary>
    [Fact]
    public void Due_SID_con_la_barra_in_mezzo_si_elencano_e_non_si_convertono_a_meta()
    {
        var esito = ConversioneSid.Cerca(new[] { Prosa(1, "Expect CDC6A/CDC6B.") }, Libv);

        Assert.Empty(esito.Proposte);
        Assert.Equal("CDC6A/CDC6B", Assert.Single(esito.DaSistemare).Trovato);
    }

    [Theory]
    [InlineData("Carta: https://example.org/carte/CDC6A.pdf")]   // dentro un indirizzo
    [InlineData("Via XCDC-CDC6A.")]                              // pezzo di un composto
    [InlineData("Via CDC6A-EXT.")]
    public void Attaccata_a_una_barra_o_a_un_trattino_non_e_una_SID_da_convertire(string testo)
    {
        Assert.Empty(ConversioneSid.Cerca(new[] { Prosa(1, testo) }, Libv).Proposte);
    }

    [Fact]
    public void Senza_scelte_per_il_blocco_non_cambia_niente()
    {
        Assert.Equal((null, null), ConversioneSid.Converti(Prosa(1, "Expect CDC6A."), Array.Empty<PropostaSid>()));
    }
}

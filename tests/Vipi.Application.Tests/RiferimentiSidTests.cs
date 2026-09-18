using Vipi.Application.Content;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Le SID citate nel testo (§A73, carta <c>2026-09-18-riferimenti-sid-nel-testo.md</c>): la radice del nome, la
/// sostituzione col nome di oggi, e il ripiego sull'ultimo nome visto. I nomi vengono dalla copia di produzione
/// del 18 settembre 2026.
/// </summary>
public class RiferimentiSidTests
{
    // ⚠️ Punto «—» apposta: senza punto il nome resta il CODICE, e queste prove guardano la scelta della riga.
    // Il nome completo (punto + designatore) ha le sue prove, in fondo.
    private static AirportSidRowView Riga(string nome, string pista = "16", string fix = "—") =>
        new(pista, fix, nome, "—", "—", "—", "—", "—", "—");

    private static NomiSid Nomi(string icao, params string[] nomi) =>
        new(new Dictionary<string, AirportSidView>
        {
            [icao] = new(nomi.Select(n => Riga(n)).ToList()),
        });

    // ---- la radice -----------------------------------------------------------------------------------

    [Theory]
    [InlineData("OST1E", "OST?E")]
    [InlineData("ost1e", "OST?E")]
    [InlineData("BRL1Z-ARL1K", "BRL?Z-ARL?K")]
    [InlineData("ABS6A LOG7A", "ABS?A LOG?A")]
    [InlineData("SALENTO5A", "SALENTO?A")]
    [InlineData("GOLF 1", "GOLF 1")]
    [InlineData("GOLF  1", "GOLF 1")]
    [InlineData("OMNI", "OMNI")]
    [InlineData("TACAN3", "TACAN3")]
    [InlineData("FRASCA DEP16", "FRASCA DEP16")]
    public void La_radice_toglie_la_sola_cifra_di_revisione(string nome, string radice)
    {
        Assert.Equal(radice, RiferimentiSid.Radice(nome));
    }

    // ---- la sostituzione -----------------------------------------------------------------------------

    [Fact]
    public void Il_riferimento_esce_col_nome_di_oggi()
    {
        var testo = "Expect [[SID LIRF OST1E]] then climb.";
        Assert.Equal("Expect OST2E then climb.", RiferimentiSid.Sostituisci(testo, Nomi("LIRF", "OST2E", "RATI1D")));
    }

    /// <summary>⚠️ Senza nomi risolti il riferimento non esce MAI grezzo: esce l'ultimo nome visto. È ciò che
    /// rende sicuro ogni punto che disegna testo senza passare dal risolutore (anteprime, didascalie, ricerca).</summary>
    [Fact]
    public void Senza_nomi_esce_l_ultimo_nome_visto()
    {
        Assert.Equal("Expect OST1E.", RiferimentiSid.Sostituisci("Expect [[SID LIRF OST1E]].", null));
    }

    [Fact]
    public void Una_SID_che_non_c_e_piu_lascia_l_ultimo_nome_visto()
    {
        Assert.Equal("Expect OST1E.", RiferimentiSid.Sostituisci("Expect [[SID LIRF OST1E]].", Nomi("LIRF", "RATI1D")));
    }

    [Fact]
    public void La_stessa_radice_in_un_altro_scalo_non_conta()
    {
        Assert.Equal("CDC6A", RiferimentiSid.Sostituisci("[[SID LIBV CDC6A]]", Nomi("LIBC", "CDC8A")));
    }

    /// <summary>Una SID sono più righe, una per pista (<c>CDC6A</c> a LIBV per 14L e 14R): il riferimento non
    /// sceglie la pista.</summary>
    [Fact]
    public void Una_SID_su_due_piste_e_un_nome_solo()
    {
        var nomi = new NomiSid(new Dictionary<string, AirportSidView>
        {
            ["LIBV"] = new(new[] { Riga("CDC7A", "14L"), Riga("CDC7A", "14R") }),
        });
        Assert.Equal("CDC7A", RiferimentiSid.Sostituisci("[[SID LIBV CDC6A]]", nomi));
        Assert.False(nomi.Ambigua("LIBV", "CDC?A"));
    }

    /// <summary>L'unica radice ambigua della copia di produzione: LIBG, ROBO1H e ROBO5H vive insieme. Vince la
    /// revisione più alta, e la coppia resta segnata per l'editor.</summary>
    [Fact]
    public void Due_revisioni_vive_vince_la_piu_alta()
    {
        var nomi = Nomi("LIBG", "ROBO5H", "ROBO1H");
        Assert.Equal("ROBO5H", RiferimentiSid.Sostituisci("[[SID LIBG ROBO1H]]", nomi));
        Assert.True(nomi.Ambigua("LIBG", "ROBO?H"));

        var alRovescio = Nomi("LIBG", "ROBO1H", "ROBO5H");
        Assert.Equal("ROBO5H", RiferimentiSid.Sostituisci("[[SID LIBG ROBO1H]]", alRovescio));
    }

    [Fact]
    public void I_nomi_militari_si_trovano_esatti()
    {
        var nomi = Nomi("LIPL", "GOLF 1", "GOLF 3");
        Assert.Equal("SID GOLF 3", RiferimentiSid.Sostituisci("SID [[SID LIPL GOLF 3]]", nomi));
        // ⚠️ Senza revisione GOLF 1 non «diventa» GOLF 3: sono due procedure diverse.
        Assert.Equal("GOLF 1", RiferimentiSid.Sostituisci("[[SID LIPL GOLF 1]]", Nomi("LIPL", "GOLF 3")));
    }

    [Fact]
    public void I_nomi_composti_si_aggiornano_pezzo_per_pezzo()
    {
        Assert.Equal("BRL2Y-ARL1K",
            RiferimentiSid.Sostituisci("[[SID LIRF BRL1Y-ARL1K]]", Nomi("LIRF", "BRL2Y-ARL1K", "BRL1Z-ARL1K")));
    }

    [Fact]
    public void Piu_riferimenti_nello_stesso_testo()
    {
        var nomi = new NomiSid(new Dictionary<string, AirportSidView>
        {
            ["LIRF"] = new(new[] { Riga("OST2E") }),
            ["LIRA"] = new(new[] { Riga("TIBER7A") }),
        });
        Assert.Equal("OST2E o TIBER7A",
            RiferimentiSid.Sostituisci("[[SID LIRF OST1E]] o [[SID LIRA TIBER6A]]", nomi));
    }

    /// <summary>Nelle tabelle il riferimento sta dentro le stringhe del JSON, e si sostituisce sul testo del
    /// JSON: il risultato deve restare JSON valido.</summary>
    [Fact]
    public void Dentro_il_JSON_di_una_tabella_il_JSON_resta_valido()
    {
        var json = """{"columns":["Punto","SID"],"rows":[{"cells":["DANTO","[[SID LIBV CDC6A]]"]}]}""";
        var fuori = RiferimentiSid.Sostituisci(json, Nomi("LIBV", "CDC7A"))!;
        using var doc = System.Text.Json.JsonDocument.Parse(fuori);
        Assert.Equal("CDC7A", doc.RootElement.GetProperty("rows")[0].GetProperty("cells")[1].GetString());
    }

    [Theory]
    [InlineData("[[SID LIRF ]]")]
    [InlineData("[[SID LIR OST1E]]")]
    [InlineData("[[sid LIRF OST1E]]")]
    [InlineData("[[SID LIRF OST\"1E]]")]
    [InlineData("[SID LIRF OST1E]")]
    public void Cio_che_non_e_un_riferimento_resta_com_e(string testo)
    {
        Assert.Equal(testo, RiferimentiSid.Sostituisci(testo, Nomi("LIRF", "OST2E")));
    }

    [Fact]
    public void Testo_nullo_o_senza_riferimenti_torna_identico()
    {
        Assert.Null(RiferimentiSid.Sostituisci(null, null));
        const string testo = "Nessuna SID qui: OST1E scritto a mano resta scritto a mano.";
        Assert.Same(testo, RiferimentiSid.Sostituisci(testo, Nomi("LIRF", "OST2E")));
    }

    // ---- gli scali citati ------------------------------------------------------------------------------

    [Fact]
    public void Gli_scali_citati_si_raccolgono_da_prosa_e_tabelle()
    {
        var scali = RiferimentiSid.ScaliCitati(new[]
        {
            "Expect [[SID LIRF OST1E]] or [[SID LIRF RATI1D]].",
            """{"rows":[{"cells":["[[SID LIRA TIBER6A]]"]}]}""",
            null,
            "niente",
        });
        Assert.Equal(new[] { "LIRA", "LIRF" }, scali.OrderBy(s => s).ToArray());
    }

    // ---- il nome completo -------------------------------------------------------------------------------

    /// <summary>Richiesta del committente (18 settembre 2026): nel testo la SID esce col punto per esteso,
    /// <c>BANAV 9A</c>, non col codice troncato del sectorfile.</summary>
    [Fact]
    public void Nel_testo_esce_il_punto_per_esteso_col_designatore()
    {
        var nomi = new NomiSid(new Dictionary<string, AirportSidView>
        {
            ["LIBD"] = new(new[] { Riga("BANA9A", "07", "BANAV") }),
        });
        Assert.Equal("Expect BANAV 9A.", RiferimentiSid.Sostituisci("Expect [[SID LIBD BANA8A]].", nomi));
    }

    /// <summary>Il punto è quello della tabella, cioè quello CORRETTO a mano: su LIRF «SIV» è SOSIV.</summary>
    [Fact]
    public void Il_punto_corretto_a_mano_vale_anche_nel_testo()
    {
        Assert.Equal("SOSIV 1E", RiferimentiSid.NomeEsteso("SOSIV", "SIV1E"));
    }

    [Theory]
    [InlineData("—", "OST2E", "OST2E")]
    [InlineData("", "OST2E", "OST2E")]
    [InlineData("ARLAK", "BRL1Z-ARL1K", "BRL1Z-ARL1K")]
    [InlineData("VIL LORIS KILO", "GOLF 1", "GOLF 1")]
    [InlineData("CDC", "CDC3L", "CDC 3L")]
    [InlineData("BV-VICTOR", "VICTOR6A", "VICTOR6A")]   // LIBV: punto VFR militare col prefisso
    public void Senza_un_punto_unico_resta_il_codice(string fix, string nome, string atteso)
    {
        Assert.Equal(atteso, RiferimentiSid.NomeEsteso(fix, nome));
    }

    // ---- il controllo dell'editor (slice 4) ------------------------------------------------------------

    [Fact]
    public void Una_SID_che_non_c_e_piu_si_segnala_con_le_sezioni_che_la_citano()
    {
        var nomi = Nomi("LIRF", "RATI1D");
        var esito = ControlloSidCitate.Controlla(new (string, string?)[]
        {
            ("Remarks", "Expect [[SID LIRF OST1E]]."),
            ("Procedure", """{"rows":[{"cells":["[[SID LIRF OST1E]]"]}]}"""),
            ("Remarks", "Again [[SID LIRF OST1E]]."),
            ("Remarks", "And [[SID LIRF RATI1D]]."),
        }, nomi);

        var r = Assert.Single(esito);
        Assert.Equal(("LIRF", "OST1E", SidDaRivedereTipo.NonTrovata, "OST1E"), (r.Icao, r.Codice, r.Tipo, r.Esce));
        Assert.Equal(new[] { "Remarks", "Procedure" }, r.Dove);
    }

    [Fact]
    public void Una_radice_ambigua_si_segnala_con_tutte_le_revisioni_vive()
    {
        var nomi = new NomiSid(new Dictionary<string, AirportSidView>
        {
            ["LIBG"] = new(new[] { Riga("ROBO1H", "07", "ROBOT"), Riga("ROBO5H", "25", "ROBOT") }),
        });
        var r = Assert.Single(ControlloSidCitate.Controlla(new (string, string?)[] { ("Note", "[[SID LIBG ROBO1H]]") }, nomi));

        Assert.Equal(SidDaRivedereTipo.Ambigua, r.Tipo);
        Assert.Equal("ROBOT 5H", r.Esce);
        Assert.Equal(new[] { "ROBOT 1H", "ROBOT 5H" }, r.Alternative);
    }

    [Fact]
    public void Le_SID_che_si_trovano_non_si_segnalano()
    {
        Assert.Empty(ControlloSidCitate.Controlla(new (string, string?)[]
        {
            ("Note", "Expect [[SID LIRF OST1E]]."), ("Note", null), ("Note", "niente"),
        }, Nomi("LIRF", "OST2E")));
    }

    [Fact]
    public void Scrivi_e_Sostituisci_fanno_andata_e_ritorno()
    {
        var rif = RiferimentiSid.Scrivi("lirf", "ost1e");
        Assert.Equal("[[SID LIRF OST1E]]", rif);
        Assert.Equal("OST1E", RiferimentiSid.Sostituisci(rif, null));
    }
}

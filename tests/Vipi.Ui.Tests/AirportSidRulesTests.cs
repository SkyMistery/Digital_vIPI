using Vipi.Ui.Components.App;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// I filtri e le regole delle SID — il cuore deterministico dell'editor più grosso del progetto (252 righe
/// di marcatura), estratto dalla pagina perché fosse provabile.
///
/// <para>
/// ⚠️ Perché conta più di quanto sembri: questi filtri decidono quali procedure un editore <b>vede</b>. Una
/// riga che sparisce per un filtro sbagliato è una riga che nessuno corregge — e resta pubblicata com'è.
/// </para>
/// </summary>
public class AirportSidRulesTests
{
    private static ImportedSidEdit Imp(int id, string fix, string nome, string? pista, bool daRivedere = false) =>
        new() { Id = id, Fix = fix, Name = nome, Runway = pista, NeedsReview = daRivedere };

    private static ImportedSidEdit[] Archivio() => new[]
    {
        Imp(1, "ALAXI", "ALAXI 5A", "16L"),
        Imp(2, "ELKAP", "ELKAP 3B", "34R", daRivedere: true),
        Imp(3, "TAQ", "TAQ 1X", "16L", daRivedere: true),
        Imp(4, "OST", "OST 2C", null),
    };

    private static string[] Nomi(IEnumerable<ImportedSidEdit> q) => q.Select(e => e.Name).ToArray();

    // ---------------------------------------------------------------------------------------------------
    // Filtro delle importate
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void Senza_filtro_si_vedono_tutte()
    {
        Assert.Equal(4, AirportSidRules.Importate(Archivio(), new SidFiltro()).Count());
    }

    [Fact]
    public void Il_testo_cerca_in_FIX_nome_e_pista()
    {
        Assert.Equal(new[] { "ALAXI 5A" }, Nomi(AirportSidRules.Importate(Archivio(), new SidFiltro(Cerca: "alaxi"))));
        Assert.Equal(new[] { "ELKAP 3B" }, Nomi(AirportSidRules.Importate(Archivio(), new SidFiltro(Cerca: "3B"))));
        Assert.Equal(new[] { "ALAXI 5A", "TAQ 1X" }, Nomi(AirportSidRules.Importate(Archivio(), new SidFiltro(Cerca: "16L"))));
    }

    [Fact]
    public void La_pista_scelta_filtra_ed_e_esatta()
    {
        // ⚠️ Esatta e non «contiene»: con «16» un filtro permissivo mostrerebbe anche 16R quando si è
        // scelto 16L, e sono due procedure diverse.
        Assert.Equal(new[] { "ALAXI 5A", "TAQ 1X" }, Nomi(AirportSidRules.Importate(Archivio(), new SidFiltro(Pista: "16L"))));
        Assert.Empty(AirportSidRules.Importate(Archivio(), new SidFiltro(Pista: "16")));
    }

    [Fact]
    public void Solo_da_rivedere_lascia_le_altre_fuori()
    {
        Assert.Equal(new[] { "ELKAP 3B", "TAQ 1X" },
            Nomi(AirportSidRules.Importate(Archivio(), new SidFiltro(SoloDaRivedere: true))));
    }

    [Fact]
    public void I_filtri_si_sommano()
    {
        var q = AirportSidRules.Importate(Archivio(), new SidFiltro(Cerca: "TAQ", Pista: "16L", SoloDaRivedere: true));
        Assert.Equal(new[] { "TAQ 1X" }, Nomi(q));
    }

    // ---------------------------------------------------------------------------------------------------
    // Le chip delle piste — il caso che il parametro `ignoraPista` esiste per proteggere
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void Le_chip_delle_piste_NON_risentono_della_pista_gia_scelta()
    {
        // ⚠️ È la regola che rende usabile il pannello: se il conteggio delle chip applicasse anche il
        // filtro pista, scelta «16L» l'elenco mostrerebbe solo 16L — e non si potrebbe più cambiare idea.
        var piste = AirportSidRules.PisteImportate(Archivio(), new SidFiltro(Pista: "16L"));

        Assert.Equal(new[] { "16L", "34R" }, piste.Select(p => p.Ident).ToArray());
        Assert.Equal(2, piste.Single(p => p.Ident == "16L").Count);
        Assert.Equal(1, piste.Single(p => p.Ident == "34R").Count);
    }

    [Fact]
    public void Le_chip_risentono_invece_degli_ALTRI_filtri()
    {
        // Il conteggio dice quante ne troverei premendo quella chip ADESSO, col testo che ho scritto.
        var piste = AirportSidRules.PisteImportate(Archivio(), new SidFiltro(SoloDaRivedere: true));

        Assert.Equal(new[] { "16L", "34R" }, piste.Select(p => p.Ident).ToArray());
        Assert.Equal(1, piste.Single(p => p.Ident == "16L").Count);   // solo TAQ, non ALAXI
    }

    [Fact]
    public void Le_SID_senza_pista_non_diventano_una_chip()
    {
        // Valgono per tutte: non sono una pista da offrire.
        Assert.DoesNotContain(AirportSidRules.PisteImportate(Archivio(), new SidFiltro()),
            p => string.IsNullOrWhiteSpace(p.Ident));
    }

    // ---------------------------------------------------------------------------------------------------
    // SID scritte a mano
    // ---------------------------------------------------------------------------------------------------

    private static SidEdit Man(string? fix, string? nome, string? pista = null) =>
        new() { Fix = fix, Name = nome, Runway = pista };

    [Fact]
    public void La_ricerca_fra_le_manuali_guarda_gli_stessi_tre_campi()
    {
        var tutte = new[] { Man("ALAXI", "ALAXI 5A", "16L"), Man("ELKAP", "ELKAP 3B", "34R") };

        Assert.Single(AirportSidRules.Manuali(tutte, "alaxi"));
        Assert.Single(AirportSidRules.Manuali(tutte, "34R"));
        Assert.Equal(2, AirportSidRules.Manuali(tutte, "").Count());
        Assert.Equal(2, AirportSidRules.Manuali(tutte, null).Count());
    }

    private static string[] Chiavi(SidEdit[] righe, params string[] piste) =>
        AirportSidRules.Issues(righe, piste).Select(i => i.Key).ToArray();

    [Fact]
    public void FIX_o_nome_mancante_si_segnala()
    {
        Assert.Equal(new[] { "Ape_IssueSidMissing" }, Chiavi(new[] { Man("ALAXI", "  ") }, "16L"));
        Assert.Equal(new[] { "Ape_IssueSidMissing" }, Chiavi(new[] { Man(null, "ALAXI 5A") }, "16L"));
    }

    [Fact]
    public void Una_pista_che_lo_scalo_non_ha_si_segnala()
    {
        var problemi = AirportSidRules.Issues(new[] { Man("ALAXI", "ALAXI 5A", "07") }, new[] { "16L", "34R" });

        var avviso = Assert.Single(problemi);
        Assert.Equal("Ape_IssueSidUnknownRw", avviso.Key);
        Assert.Equal(new object[] { 1, "07" }, avviso.Args);
    }

    [Fact]
    public void Una_SID_senza_pista_non_e_un_problema()
    {
        // Vale per tutte le piste: è una forma legittima, non una riga incompleta.
        Assert.Empty(AirportSidRules.Issues(new[] { Man("TAQ", "TAQ 1X") }, new[] { "16L" }));
    }

    [Fact]
    public void La_STESSA_procedura_su_due_piste_diverse_NON_e_un_duplicato()
    {
        // ⚠️ È il caso normale, non l'eccezione: una SID pubblicata per 16L e per 34R sono due righe vere.
        // Se la chiave del duplicato fosse solo FIX+nome, l'editor segnalerebbe metà dell'archivio.
        Assert.Empty(AirportSidRules.Issues(
            new[] { Man("ALAXI", "ALAXI 5A", "16L"), Man("ALAXI", "ALAXI 5A", "34R") },
            new[] { "16L", "34R" }));
    }

    [Fact]
    public void Due_righe_identiche_sono_un_duplicato_e_si_nomina_la_seconda()
    {
        var problemi = AirportSidRules.Issues(
            new[] { Man("ALAXI", "ALAXI 5A", "16L"), Man("ALAXI", "ALAXI 5A", "16L") },
            new[] { "16L" });

        var doppia = Assert.Single(problemi);
        Assert.Equal("Ape_IssueSidDup", doppia.Key);
        Assert.Equal(2, doppia.Args[0]);   // la riga di troppo, non la prima
    }

    [Fact]
    public void Una_riga_incompleta_non_conta_come_duplicato()
    {
        // Due righe appena aggiunte sono due vuoti uguali: segnalarle direbbe a chi scrive che ha
        // sbagliato, mentre non ha ancora finito.
        var problemi = AirportSidRules.Issues(new[] { Man(null, null), Man(null, null) }, new[] { "16L" });

        Assert.All(problemi, p => Assert.Equal("Ape_IssueSidMissing", p.Key));
    }

    // ---------------------------------------------------------------------------------------------------
    // Liste separate da virgole (transizioni, categorie)
    // ---------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("A,B,C", "B", true)]
    [InlineData("A, B , C", "b", true)]      // spazi e maiuscole non contano
    [InlineData("A,B", "AB", false)]         // ⚠️ è un token, non una sotto-stringa
    [InlineData("", "A", false)]
    [InlineData(null, "A", false)]
    public void Un_token_c_e_solo_se_e_un_ELEMENTO_della_lista(string? csv, string tok, bool atteso) =>
        Assert.Equal(atteso, AirportSidRules.HasTok(csv, tok));
}

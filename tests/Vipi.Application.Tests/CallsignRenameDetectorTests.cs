using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Il riconoscimento della rinomina, sui casi che vengono dai dati veri.
/// </summary>
public class CallsignRenameDetectorTests
{
    private static IReadOnlyList<CallsignRename> Detect(
        Dictionary<int, string> nostri, params (int? Id, string Callsign)[] sorgente) =>
        CallsignRenameDetector.Detect(SourceCatalog.Subcenter, nostri, sorgente);

    [Fact]
    public void Stesso_id_nominativo_diverso_e_una_rinomina()
    {
        var r = Detect(new() { [1174] = "LIRR_NE_CTR" }, (1174, "LIRR_NEW_CTR"));

        var sola = Assert.Single(r);
        Assert.Equal(1174, sola.IvaoId);
        Assert.Equal("LIRR_NE_CTR", sola.OldCallsign);
        Assert.Equal("LIRR_NEW_CTR", sola.NewCallsign);
        Assert.Equal(SourceCatalog.Subcenter, sola.Catalog);
    }

    /// <summary>
    /// Il caso vero del 22 agosto 2026, e la ragione per cui l'euristica è stata buttata via.
    /// <c>LIRR_NE1_CTR</c> è nato accanto a <c>LIRR_NE_CTR</c> con la STESSA frequenza (124.2) e lo stesso
    /// nome IVAO («Roma Radar»): per un confronto a occhio era la rinomina perfetta. Per l'id è un id nuovo,
    /// quindi una riga nuova, e il vecchio non si tocca.
    /// </summary>
    [Fact]
    public void Uno_sdoppiamento_non_e_una_rinomina()
    {
        var r = Detect(new() { [1174] = "LIRR_NE_CTR" },
            (1174, "LIRR_NE_CTR"),      // il vecchio, che la sorgente manda ancora
            (3916, "LIRR_NE1_CTR"));    // il nuovo, mai visto

        Assert.Empty(r);
    }

    [Fact]
    public void Un_id_mai_visto_e_una_riga_nuova_non_una_rinomina() =>
        Assert.Empty(Detect(new() { [1174] = "LIRR_NE_CTR" }, (3916, "LIRR_NE1_CTR")));

    [Fact]
    public void Nominativo_invariato_non_e_un_cambiamento() =>
        Assert.Empty(Detect(new() { [1174] = "LIRR_NE_CTR" }, (1174, "LIRR_NE_CTR")));

    [Fact]
    public void Il_confronto_ignora_maiuscole_e_spazi() =>
        Assert.Empty(Detect(new() { [1174] = "LIRR_NE_CTR" }, (1174, "  lirr_ne_ctr ")));

    /// <summary>
    /// Le righe che la sorgente non ha mai mandato — i settori esteri catalogati a mano — non hanno un'identità
    /// da seguire. Senza questa guardia una riga sintetica con lo stesso callsign di un'altra produrrebbe
    /// rinomine inventate.
    /// </summary>
    [Fact]
    public void Una_riga_senza_id_non_produce_rinomine() =>
        Assert.Empty(Detect(new() { [1174] = "LIRR_NE_CTR" }, (null, "LGKR_APP")));

    /// <summary>
    /// Un archivio che non ha ancora fatto il backfill non ha id da confrontare: il primo giro deve essere
    /// silenzioso, non una raffica di rinomine.
    /// </summary>
    [Fact]
    public void Il_primo_giro_su_un_archivio_senza_id_e_silenzioso() =>
        Assert.Empty(Detect(new(), (1174, "LIRR_NE_CTR"), (3916, "LIRR_NE1_CTR")));

    [Fact]
    public void Un_id_ripetuto_nella_stessa_risposta_conta_una_volta_sola()
    {
        var r = Detect(new() { [1174] = "LIRR_NE_CTR" },
            (1174, "LIRR_A_CTR"),
            (1174, "LIRR_B_CTR"));

        Assert.Equal("LIRR_A_CTR", Assert.Single(r).NewCallsign);
    }

    [Fact]
    public void Piu_rinomine_nello_stesso_giro_escono_tutte()
    {
        var r = Detect(new() { [1] = "LIRR_A_CTR", [2] = "LIRR_B_CTR", [3] = "LIRR_C_CTR" },
            (1, "LIRR_A1_CTR"), (2, "LIRR_B_CTR"), (3, "LIRR_C1_CTR"));

        Assert.Equal(new[] { 1, 3 }, r.Select(x => x.IvaoId).OrderBy(x => x));
    }
}

/// <summary>
/// La traduzione dei nominativi storici: quel che serve a non spezzare una postazione in due righe di
/// statistica solo perché a giugno si chiamava in un altro modo.
/// </summary>
public class CallsignHistoryTests
{
    private static CallsignHistory Storia(params (string, string)[] alias) => new(alias);

    [Fact]
    public void Senza_alias_e_vuota_e_non_traduce_niente()
    {
        var s = Storia();
        Assert.True(s.IsEmpty);
        Assert.Equal("LIRR_NE_CTR", s.Canonical("LIRR_NE_CTR"));
    }

    [Fact]
    public void Traduce_un_nominativo_dismesso() =>
        Assert.Equal("LIRR_NEW_CTR",
            Storia(("LIRR_NE_CTR", "LIRR_NEW_CTR")).Canonical("LIRR_NE_CTR"));

    [Fact]
    public void Un_nominativo_vivo_resta_com_e() =>
        Assert.Equal("LIRR_S_CTR",
            Storia(("LIRR_NE_CTR", "LIRR_NEW_CTR")).Canonical("LIRR_S_CTR"));

    /// <summary>Un settore può essere rinominato più volte: la catena si segue fino in fondo.</summary>
    [Fact]
    public void Segue_la_catena_di_piu_rinomine() =>
        Assert.Equal("LIRR_C_CTR",
            Storia(("LIRR_A_CTR", "LIRR_B_CTR"), ("LIRR_B_CTR", "LIRR_C_CTR")).Canonical("LIRR_A_CTR"));

    [Fact]
    public void Il_confronto_ignora_le_maiuscole() =>
        Assert.Equal("LIRR_NEW_CTR",
            Storia(("LIRR_NE_CTR", "LIRR_NEW_CTR")).Canonical("lirr_ne_ctr"));

    /// <summary>Un ciclo non dovrebbe esistere, ma se esiste non deve appendere la pagina che lo incontra.</summary>
    [Fact]
    public void Un_ciclo_non_manda_in_loop()
    {
        var s = Storia(("A", "B"), ("B", "A"));
        Assert.Contains(s.Canonical("A"), new[] { "A", "B" });
    }
}

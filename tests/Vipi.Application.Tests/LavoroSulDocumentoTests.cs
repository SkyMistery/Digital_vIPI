using Vipi.Application.Content;

namespace Vipi.Application.Tests;

/// <summary>S133, carta 2026-10-28-stato-dei-documenti §1 D2: le quattro colonne del lavoro nella pagina Documenti e i
/// chip che le filtrano, con una regola sola.</summary>
public class LavoroSulDocumentoTests
{
    private static readonly DateTime Ora = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

    private static ConfrontoSalvato Confronto(string? ciclo = "2611", int pubblico = 0, string? programmata = null,
        int vsProgrammata = 0, bool derivate = false) => new(1, Ora, ciclo, pubblico, programmata, vsProgrammata, derivate);

    [Fact]
    public void Niente_da_fare()
    {
        var l = new LavoroSulDocumento(0, 0, Confronto(), null, false);

        Assert.False(l.Qualcosa);
        Assert.All(LavoroSulDocumento.Filtri, f => Assert.False(l.Passa(f)));
    }

    [Fact]
    public void Sezioni_vuote_o_frasi_da_tradurre_sono_da_completare()
    {
        Assert.True(new LavoroSulDocumento(2, 0, null, null, false).Passa("complete"));
        Assert.True(new LavoroSulDocumento(0, 5, null, null, false).Passa("complete"));
    }

    [Fact]
    public void Da_pubblicare_vuole_una_release_in_vigore_diversa_dalla_bozza()
    {
        Assert.True(new LavoroSulDocumento(0, 0, Confronto(pubblico: 3), null, false).Passa("publish"));
        Assert.False(new LavoroSulDocumento(0, 0, Confronto(ciclo: null, pubblico: 3), null, false).Passa("publish"));
        Assert.False(new LavoroSulDocumento(0, 0, null, null, false).Passa("publish"));   // il giro non l'ha ancora visto
    }

    /// <summary>Trovato a schermo il 28 ottobre: con una programmata che porta già la bozza, quel che il pubblico non vede
    /// ancora arriva con lei, e «da pubblicare» chiedeva un lavoro già fatto. Se la programmata è indietro, sì.</summary>
    [Fact]
    public void Con_una_programmata_allineata_non_e_da_pubblicare()
    {
        Assert.False(new LavoroSulDocumento(0, 0, Confronto(pubblico: 1, programmata: "2611"), null, false).Passa("publish"));
        Assert.True(new LavoroSulDocumento(0, 0, Confronto(pubblico: 1, programmata: "2611", vsProgrammata: 1), null, false).Passa("publish"));
    }

    /// <summary>Anche le sole derivate congelate cambiate fanno una programmata indietro: è il caso U-053.</summary>
    [Fact]
    public void La_programmata_e_indietro_per_le_sezioni_o_per_le_derivate()
    {
        Assert.True(new LavoroSulDocumento(0, 0, Confronto(programmata: "2612", vsProgrammata: 1), null, false).Passa("scheduled"));
        Assert.True(new LavoroSulDocumento(0, 0, Confronto(programmata: "2612", derivate: true), null, false).Passa("scheduled"));
        Assert.False(new LavoroSulDocumento(0, 0, Confronto(programmata: "2612"), null, false).Passa("scheduled"));
    }

    [Fact]
    public void Un_documento_nascosto_non_e_da_pubblicare_ma_resta_da_completare_e_da_rivedere()
    {
        var l = new LavoroSulDocumento(1, 0, Confronto(pubblico: 3, programmata: "2612", vsProgrammata: 2),
            new ImpactBadge(1, 0, 0, 0), Nascosto: true);

        Assert.False(l.Pubblicare);
        Assert.False(l.Programmata);
        Assert.True(l.Completare);
        Assert.True(l.DaRivedere);
        Assert.True(l.Passa("any"));
    }

    [Fact]
    public void Un_filtro_sconosciuto_lascia_passare_tutto() =>
        Assert.True(new LavoroSulDocumento(0, 0, null, null, false).Passa("boh"));
}

using Vipi.Application.Translation;

namespace Vipi.Application.Tests;

/// <summary>
/// Le tre descrizioni delle aree D del 37° Stormo che Azure rendeva rotte a ogni giro (§A64.3, 18 settembre 2026):
/// «37th WING A/A TRAINING AREA» in maiuscolo, protetto come sigla, tradotto lo stesso, frase buttata e rispedita.
/// </summary>
public class FrasiAreeIvaoTests
{
    /// <summary>
    /// I testi COME STANNO IN ARCHIVIO, scritti qui a parte con i caratteri espliciti (U+2022, tabulazione, spazio in
    /// coda, CRLF): letti dalla copia di produzione del 18 settembre 2026, righe 239, 240 e 241 di <c>SpecialAreas</c>.
    /// Il seme serve solo se la sua impronta è quella del segmento che il corpus manda al giro.
    /// </summary>
    private static readonly string[] InArchivio =
    {
        "•\t37th WING A/A TRAINING AREA",
        "•\t37th WING A/A TRAINING AREA \r\n•\tNot avlb with all SIDs via TRP from LICJ \r\n"
            + "•\tIncompatible with all the STAR NDB/VOR",
        "•\t37th WING A/A TRAINING AREA \r\n•\tIncompatible with Hi-Tacan RWY 31L\r\n"
            + "•\tIncompatibile with the follwing STARs:\r\n-\tPAL1F, PIVOP1F e MEGAN1F RWY 13R\r\n"
            + "-\tPAL1A, PIVOP1A e MEGAN1A RWY 31L",
    };

    [Fact]
    public async Task Semina_le_tre_con_l_impronta_dei_segmenti_veri()
    {
        var memoria = new MemoriaDiTraduzioneFinta();

        Assert.Equal(3, await FrasiAreeIvao.SeminaAsync(memoria));

        var impronteSeminate = memoria.Umane.Keys.Select(TranslationText.Hash).ToHashSet();
        foreach (var testo in InArchivio)
            Assert.Contains(TranslationText.Hash(TranslationText.Normalize(testo)), impronteSeminate);
        Assert.All(memoria.Umane.Values, it => Assert.Contains("AREA ADDESTRAMENTO A/A DEL 37° STORMO", it));
    }

    [Fact]
    public async Task Gli_identificatori_restano_quelli_del_sorgente()
    {
        var memoria = new MemoriaDiTraduzioneFinta();
        await FrasiAreeIvao.SeminaAsync(memoria);

        var mazara = memoria.Umane.Single(kv => kv.Key.Contains("Hi-Tacan")).Value;
        foreach (var id in new[] { "RWY 31L", "RWY 13R", "PAL1F", "PIVOP1F", "MEGAN1F", "PAL1A", "PIVOP1A", "MEGAN1A" })
            Assert.Contains(id, mazara);
        Assert.Contains("LICJ", memoria.Umane.Single(kv => kv.Key.Contains("LICJ")).Value);
    }

    [Fact]
    public async Task Una_resa_umana_gia_scritta_vince_e_il_secondo_giro_non_scrive_niente()
    {
        var memoria = new MemoriaDiTraduzioneFinta().GiaUmana(InArchivio[0]);

        Assert.Equal(2, await FrasiAreeIvao.SeminaAsync(memoria));
        Assert.DoesNotContain(memoria.Umane.Keys, k => TranslationText.Hash(k) == TranslationText.Hash(InArchivio[0]));
        Assert.Equal(0, await FrasiAreeIvao.SeminaAsync(memoria));
    }
}

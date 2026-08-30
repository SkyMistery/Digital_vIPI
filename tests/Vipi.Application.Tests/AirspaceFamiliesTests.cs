using Vipi.Application.Airspace;
using Vipi.Domain;

namespace Vipi.Application.Tests;

/// <summary>
/// La famiglia di un volume e la lista bianca. I casi sono <b>nomi veri</b> del file del 15 luglio 2026: sono
/// quelli che hanno fatto cadere la prima regola, che si fermava alla categoria.
/// </summary>
public class AirspaceFamiliesTests
{
    [Theory]
    [InlineData("Control Traffic Region", "ALGHERO CTR", AirspaceFamily.Ctr)]
    [InlineData("Terminal Manoeuvring Area", "TMA ROMA Z1 GIGLIO", AirspaceFamily.Tma)]
    [InlineData("Flight Information Region", "FIR MILANO", AirspaceFamily.Fir)]
    [InlineData("Transponder Mandatory Zone", "FMC MINW---SSR4610", AirspaceFamily.Tmz)]
    [InlineData("Restricted area", "LI-R14A-S.SEVERA (FURBARA)", AirspaceFamily.Restricted)]
    [InlineData("Prohibited area", "LI-P1-ETNA", AirspaceFamily.Prohibited)]
    [InlineData("Danger area", "LI-D6-MATERA", AirspaceFamily.Danger)]
    [InlineData("Gliding area", "VFR SECT ARCO", AirspaceFamily.Gliding)]
    [InlineData("Over The Horizon", "LI TRA424 - CAMERI", AirspaceFamily.Other)]
    public void La_Categoria_Basta_Quando_Dice_Gia_Tutto(string categoria, string nome, AirspaceFamily attesa) =>
        Assert.Equal(attesa, AirspaceFamilies.Classify(categoria, nome));

    [Theory]
    [InlineData("Airspace class C", "PISA CTR Z3", AirspaceFamily.Ctr)]          // l'unico volume di classe C
    [InlineData("Airspace class A", "CTA MILANO Z1 BRERA", AirspaceFamily.Cta)]  // l'unico di classe A
    [InlineData("Airspace class D", "CTA MILANO Z3 GARDA", AirspaceFamily.Cta)]
    [InlineData("Airspace class D", "ATZ ATZ ALBENGA LIMG", AirspaceFamily.Atz)] // 36 ATZ stanno in classe D
    [InlineData("Airspace class D", "MATZ CERVIA-TWR", AirspaceFamily.Atz)]      // e 13 MATZ
    [InlineData("Airspace class G", "ATZ CROTONE LIBC", AirspaceFamily.Atz)]
    public void Sulle_Classi_Decide_Il_Nome_Non_La_Categoria(string categoria, string nome, AirspaceFamily attesa)
    {
        // ⚠️ Fermarsi alla categoria vorrebbe dire chiamare CTA quarantanove zone di traffico d'aeroporto —
        // proprio quelle che devono fare da ripiego alle torri senza poligono.
        Assert.Equal(attesa, AirspaceFamilies.Classify(categoria, nome));
    }

    [Theory]
    [InlineData("Airspace class D", "LAMPEDUSA", AirspaceFamily.Cta)]                       // controllato
    [InlineData("Airspace class G", "FLYING CLUIB SABAUDIA(AIRWRK)", AirspaceFamily.Other)] // non controllato
    public void Senza_Una_Parola_Riconoscibile_Decide_La_Classe(string categoria, string nome, AirspaceFamily attesa) =>
        Assert.Equal(attesa, AirspaceFamilies.Classify(categoria, nome));

    [Fact]
    public void Una_Categoria_Sconosciuta_Non_Fa_Cadere_Niente() =>
        Assert.Equal(AirspaceFamily.Other, AirspaceFamilies.Classify("Qualcosa di nuovo", "BOH"));

    [Theory]
    [InlineData(AirspaceFamily.Ctr)]
    [InlineData(AirspaceFamily.Cta)]
    [InlineData(AirspaceFamily.Tma)]
    [InlineData(AirspaceFamily.Atz)]
    [InlineData(AirspaceFamily.Fir)]
    [InlineData(AirspaceFamily.Tmz)]
    public void Le_Famiglie_Di_Struttura_Si_Usano(AirspaceFamily famiglia)
    {
        Assert.True(AirspaceFamilies.IsUsable(famiglia));
        Assert.Null(AirspaceFamilies.WhyNotUsable(famiglia));
    }

    [Theory]
    [InlineData(AirspaceFamily.Restricted)]
    [InlineData(AirspaceFamily.Prohibited)]
    [InlineData(AirspaceFamily.Danger)]
    [InlineData(AirspaceFamily.Gliding)]
    [InlineData(AirspaceFamily.Other)]
    public void Le_Aree_Regolamentate_E_Le_Altre_No(AirspaceFamily famiglia)
    {
        // Decisione del committente del 29 agosto 2026: quelle vengono solo dal catalogo IVAO.
        Assert.False(AirspaceFamilies.IsUsable(famiglia));
        Assert.NotNull(AirspaceFamilies.WhyNotUsable(famiglia));
    }

    [Fact]
    public void Lelenco_Delle_Utilizzabili_E_Quello_Che_La_Ui_Accende() =>
        Assert.Equal(
            [AirspaceFamily.Ctr, AirspaceFamily.Cta, AirspaceFamily.Tma, AirspaceFamily.Atz,
             AirspaceFamily.Fir, AirspaceFamily.Tmz],
            AirspaceFamilies.Usable);

    [Theory]
    [InlineData("Airspace class D", "D")]
    [InlineData("Airspace class G", "G")]
    [InlineData("Control Traffic Region", null)]
    [InlineData(null, null)]
    public void La_Lettera_Della_Classe_Si_Conserva(string? categoria, string? attesa) =>
        Assert.Equal(attesa, AirspaceFamilies.ClassOf(categoria));
}

using Vipi.Application.Airspace;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Le regole delle correzioni a mano dei volumi dell'AIP (carta docs/feature/2026-09-30-correzioni-spazi-aerei.md):
/// come si sovrappongono, che cosa si salva, e che cosa si segnala quando arriva un file nuovo.
/// </summary>
public class AirspaceCorrectionsTests
{
    private static AirspaceVolumeRow Volume(string nome, string @base = "GND", string tetto = "2500 FT AMSL",
        AirspaceFamily famiglia = AirspaceFamily.Ctr, string? classe = "D", int ordinale = 0, int id = 1)
    {
        var b = AirspaceLevelParser.Parse(@base)!;
        var t = AirspaceLevelParser.Parse(tetto)!;
        return new AirspaceVolumeRow(id, 1, famiglia, nome, "Control Traffic Region", classe,
            b.Datum, b.Feet, b.Raw, t.Datum, t.Feet, t.Raw, "[[9,45],[9.5,45],[9.25,45.5]]", 1, 3,
            $"{famiglia}|{nome}|{@base}|{tetto}".ToUpperInvariant(), ordinale, 45, 9, 45.5, 9.5);
    }

    private static AirspaceCorrectionRow Correzione(AirspaceVolumeRow file, AirspaceCorrectionInput input, int id = 7) =>
        AirspaceCorrections.Desired(file, input, id)!;

    [Fact]
    public void Si_salvano_solo_i_campi_diversi_dal_file()
    {
        var file = Volume("PROVA CTR");
        var c = Correzione(file, new AirspaceCorrectionInput(AirspaceFamily.Ctr, "d", "gnd", "3000 FT AMSL"));

        Assert.Null(c.Family);                 // uguale
        Assert.False(c.ClassCorrected);        // «d» è la D del file
        Assert.Null(c.BaseRaw);                // «gnd» è il GND del file
        Assert.Equal("3000 FT AMSL", c.TopRaw);
        Assert.Equal("2500 FT AMSL", c.FileTopRaw);   // quel che il file diceva
        Assert.Equal(file.NaturalKey, c.VolumeKey);
    }

    [Fact]
    public void Uguale_al_file_in_tutto_non_e_una_correzione()
    {
        var file = Volume("PROVA CTR");
        Assert.Null(AirspaceCorrections.Desired(file,
            new AirspaceCorrectionInput(AirspaceFamily.Ctr, "D", "GND", "2500FT AMSL")));
    }

    [Fact]
    public void Nessuna_classe_e_una_correzione_anche_lei()
    {
        var c = Correzione(Volume("PROVA CTR"), new AirspaceCorrectionInput(AirspaceFamily.Ctr, "", "GND", "2500 FT AMSL"));
        Assert.True(c.ClassCorrected);
        Assert.Null(c.AirspaceClass);
        Assert.Null(AirspaceCorrections.Apply(Volume("PROVA CTR"), c).AirspaceClass);
    }

    [Fact]
    public void La_correzione_si_sovrappone_ma_la_chiave_resta_quella_del_file()
    {
        var file = Volume("PROVA CTR");
        var c = Correzione(file, new AirspaceCorrectionInput(AirspaceFamily.Tma, "C", "1500 FT AMSL", "FL95"));

        var v = AirspaceCorrections.Apply(file, c);

        Assert.True(v.IsCorrected);
        Assert.Equal(AirspaceFamily.Tma, v.Family);
        Assert.Equal("C", v.AirspaceClass);
        Assert.Equal((AirspaceDatum.Amsl, 1500), (v.BaseDatum, v.BaseFeet));
        Assert.Equal((AirspaceDatum.FlightLevel, 9500), (v.TopDatum, v.TopFeet));
        Assert.Equal("1500 FT AMSL → FL95", v.BandLabel);
        Assert.Equal(file.NaturalKey, v.NaturalKey);   // gli agganci la citano
        Assert.Equal(file.PolygonJson, v.PolygonJson);
        Assert.False(AirspaceCorrections.Apply(file, null).IsCorrected);
    }

    [Theory]
    [InlineData("Z", "GND", "FL95", "Class")]
    [InlineData("D", "boh", "FL95", "Base")]
    [InlineData("D", "GND", "", "Top")]
    [InlineData("D", "FL100", "FL95", "Order")]
    [InlineData("D", "3000 FT AMSL", "FL25", "Order")]
    [InlineData("D", "UNL", "FL95", "Order")]
    [InlineData("", "1000 FT AGL", "3000 FT AMSL", null)]   // AGL contro AMSL non si confronta
    [InlineData("d", "GND", "UNL", null)]
    public void Una_correzione_sbagliata_si_dice(string classe, string @base, string tetto, string? atteso) =>
        Assert.Equal(atteso, AirspaceCorrections.Validate(new AirspaceCorrectionInput(AirspaceFamily.Ctr, classe, @base, tetto)));

    [Fact]
    public void Stesso_file_niente_da_segnalare()
    {
        var file = Volume("PROVA CTR");
        var c = Correzione(file, new AirspaceCorrectionInput(AirspaceFamily.Ctr, "D", "GND", "3000 FT AMSL"));

        Assert.Empty(AirspaceCorrections.Review([c], [file]));
    }

    [Fact]
    public void Il_file_cambia_un_campo_non_corretto_niente_da_segnalare()
    {
        var c = Correzione(Volume("PROVA CTR"), new AirspaceCorrectionInput(AirspaceFamily.Ctr, "D", "GND", "3000 FT AMSL"));
        var nuovo = Volume("PROVA CTR", classe: "C");   // la classe non era corretta, e la chiave non la contiene

        Assert.Empty(AirspaceCorrections.Review([c], [nuovo]));
    }

    [Fact]
    public void Il_file_cambia_un_campo_corretto_si_segnala()
    {
        var c = Correzione(Volume("PROVA CTR"), new AirspaceCorrectionInput(AirspaceFamily.Ctr, "C", "GND", "2500 FT AMSL"));
        var nuovo = Volume("PROVA CTR", classe: "E");

        var f = Assert.Single(AirspaceCorrections.Review([c], [nuovo]));
        Assert.Equal(AirspaceCorrectionFindingKind.FileChanged, f.Kind);
        Assert.False(f.KeyChanged);
        var d = Assert.Single(f.Diffs);
        Assert.Equal((AirspaceCorrectionField.Class, "D", "E", "C"), (d.Field, d.FileBefore, d.FileNow, d.Corrected));
    }

    [Fact]
    public void Il_file_ora_dice_gia_cosi_e_la_chiave_cambia_si_ritrova_per_nome()
    {
        var c = Correzione(Volume("PROVA CTR"), new AirspaceCorrectionInput(AirspaceFamily.Ctr, "D", "GND", "3000 FT AMSL"));
        var nuovo = Volume("PROVA CTR", tetto: "3000 FT AMSL", id: 99);   // il tetto è nella chiave

        var f = Assert.Single(AirspaceCorrections.Review([c], [nuovo, Volume("ALTRO CTR", id: 5)]));
        Assert.Equal(AirspaceCorrectionFindingKind.FileAgrees, f.Kind);
        Assert.True(f.KeyChanged);
        Assert.Equal(99, f.Volume!.Id);
    }

    [Fact]
    public void Nome_non_unico_nel_file_il_volume_non_si_indovina()
    {
        // GRAZZANISE CTR Z2 compare due volte con bande diverse: scegliere a caso sposterebbe la correzione.
        var c = Correzione(Volume("GRAZZANISE CTR Z2"), new AirspaceCorrectionInput(AirspaceFamily.Ctr, "D", "GND", "3000 FT AMSL"));
        var file = new[]
        {
            Volume("GRAZZANISE CTR Z2", tetto: "2000 FT AMSL", id: 1),
            Volume("GRAZZANISE CTR Z2", @base: "2000 FT AMSL", tetto: "FL65", id: 2),
        };

        var f = Assert.Single(AirspaceCorrections.Review([c], file));
        Assert.Equal(AirspaceCorrectionFindingKind.VolumeMissing, f.Kind);
        Assert.Null(f.Volume);
    }

    [Fact]
    public void Volume_sparito_si_segnala()
    {
        var c = Correzione(Volume("PROVA CTR"), new AirspaceCorrectionInput(AirspaceFamily.Tma, "D", "GND", "2500 FT AMSL"));

        var f = Assert.Single(AirspaceCorrections.Review([c], [Volume("ALTRO CTR")]));
        Assert.Equal(AirspaceCorrectionFindingKind.VolumeMissing, f.Kind);
        Assert.Equal(AirspaceCorrectionField.Family, Assert.Single(f.Diffs).Field);
    }

    [Fact]
    public void Un_volume_preso_per_chiave_da_una_correzione_non_lo_ritrova_un_altra_per_nome()
    {
        var a = Volume("PROVA CTR");
        var ca = Correzione(a, new AirspaceCorrectionInput(AirspaceFamily.Ctr, "C", "GND", "2500 FT AMSL"), id: 1);
        // Una seconda correzione con lo stesso nome, di un volume che nel file non c'è più.
        var cb = Correzione(Volume("PROVA CTR", tetto: "FL65"), new AirspaceCorrectionInput(AirspaceFamily.Ctr, "C", "GND", "FL65"), id: 2);

        var f = Assert.Single(AirspaceCorrections.Review([ca, cb], [a]));
        Assert.Equal(2, f.Correction.Id);
        Assert.Equal(AirspaceCorrectionFindingKind.VolumeMissing, f.Kind);
    }
}

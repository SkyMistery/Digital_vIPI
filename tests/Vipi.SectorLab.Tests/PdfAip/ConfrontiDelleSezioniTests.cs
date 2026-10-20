using Vipi.SectorLab.Core.PdfAip;

namespace Vipi.SectorLab.Tests.PdfAip;

/// <summary>Riordino R6: le righe dei confronti di oggi portate a una forma sola, e la voce dell'AIP di una riga.</summary>
public sealed class ConfrontiDelleSezioniTests
{
    [Fact]
    public void LeFormeDiconoGliScartiELeZone()
    {
        var righe = ConfrontiDelleSezioni.Da([
            new DifferenzaDiForma(EsitoDelConfronto.FormaDiversa, "Bologna CTR", 3, "LIPE CTR", "lipe.str", 4, 0.4, 1.25, (44.5, 11.3), true),
            new DifferenzaDiForma(EsitoDelConfronto.MancaNelSector, "Forlì CTR", 1, null, null, null, null, null, null, false),
        ]);

        Assert.Equal(("Bologna CTR", "LIPE CTR", "lipe.str", (int?)4), (righe[0].Aip, righe[0].Sector, righe[0].File, righe[0].Record));
        Assert.Contains("3 zone nell'AIP", righe[0].Dettagli);
        Assert.Contains(righe[0].Dettagli, d => d.Contains("1,25 NM", StringComparison.Ordinal));
        Assert.Contains(righe[0].Dettagli, d => d.Contains("non si giudica", StringComparison.Ordinal));
        Assert.Empty(righe[1].Dettagli);
    }

    [Fact]
    public void ISoliNomiDiconoDiChiSonoSecondoLEsito()
    {
        var righe = ConfrontiDelleSezioni.Da([
            new DifferenzaDiEnr4(EsitoDelConfronto.MancaNelSector, "NUOVO", null, null, []),
            new DifferenzaDiEnr4(EsitoDelConfronto.SoloNelSector, "NANKA", "itfix.fix", 3, ["ENR 4.4 non lo ha"]),
            new DifferenzaDiEnr4(EsitoDelConfronto.Uguale, "ABADI", "itfix.fix", 0, []),
        ]);

        Assert.Equal(("NUOVO", (string?)null), (righe[0].Aip, righe[0].Sector));
        Assert.Equal(((string?)null, "NANKA"), (righe[1].Aip, righe[1].Sector));
        Assert.Equal(("ABADI", "ABADI"), (righe[2].Aip, righe[2].Sector));

        var conti = ConfrontiDelleSezioni.Conteggi(righe);
        Assert.Equal(1, conti[EsitoDelConfronto.MancaNelSector]);
        Assert.Equal(0, conti[EsitoDelConfronto.FormaDiversa]);
    }

    [Fact]
    public void LaVoceDellAipSiTrovaPerNome()
    {
        VoceDellAip Voce(string titolo) => new("Punti", titolo, [], []);
        IReadOnlyList<VoceDellAip> voci = [Voce("ABADI"), Voce("OST VOR/DME"), Voce("Bologna CTR Zona '1'"), Voce("OSTEG")];

        Assert.Equal(0, ConfrontiDelleSezioni.VoceDelNome(voci, "abadi"));
        Assert.Equal(1, ConfrontiDelleSezioni.VoceDelNome(voci, "OST"));
        Assert.Equal(2, ConfrontiDelleSezioni.VoceDelNome(voci, "Bologna CTR"));
        Assert.Null(ConfrontiDelleSezioni.VoceDelNome(voci, "NESSUNO"));
        Assert.Null(ConfrontiDelleSezioni.VoceDelNome(voci, null));
    }

    [Fact]
    public void LeSezioniStannoNelleParti()
    {
        Assert.Equal(13, Enum.GetValues<SezioneDelConfronto>().Length);
        Assert.Equal("ENR 4", ConfrontiDelleSezioni.Parte(SezioneDelConfronto.Punti));
        Assert.Equal("AD 2", ConfrontiDelleSezioni.Parte(SezioneDelConfronto.Procedure));
        var nomi = Enum.GetValues<SezioneDelConfronto>().Select(ConfrontiDelleSezioni.Nome).ToList();
        Assert.All(nomi, n => Assert.False(string.IsNullOrWhiteSpace(n)));
        Assert.Equal(nomi.Count, nomi.Distinct().Count());
    }
}

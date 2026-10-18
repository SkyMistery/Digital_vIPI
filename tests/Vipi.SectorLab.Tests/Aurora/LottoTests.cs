using Vipi.SectorLab.Core.Aurora;

namespace Vipi.SectorLab.Tests.Aurora;

/// <summary>
/// Carta F5, slice 2: il lotto — i gesti salvati da «visto in Aurora», ognuno con dove guardarlo; nei dati del Lab, un
/// file per sector, che sopravvive a un riavvio.
/// </summary>
public sealed class LottoTests : IDisposable
{
    private readonly string _radice = Path.Combine(Path.GetTempPath(), "lotto-" + Guid.NewGuid().ToString("N"));
    private readonly DateTimeOffset _ora = new(2026, 10, 18, 10, 0, 0, TimeSpan.Zero);

    public LottoTests() => Directory.CreateDirectory(_radice);

    public void Dispose()
    {
        if (Directory.Exists(_radice))
            Directory.Delete(_radice, recursive: true);
    }

    private string Dati => Path.Combine(_radice, "dati");

    private string Sector => Path.Combine(_radice, "ciclo");

    [Fact]
    public void SalvatiPortatiVisti_ERiletti()
    {
        var lotto = Lotto.Leggi(Dati, Sector);
        Assert.Empty(lotto.Voci);

        lotto.Salvati([("Name di BD100", ["SectorFiles/Include/IT/NAVAIDS/APT.fix"])], _ora);
        lotto.Portato(_ora.AddMinutes(1));
        lotto.Salvati([("zona X di LIRF", ["SectorFiles/Include/IT/lirf.mva"])], _ora.AddMinutes(2));
        lotto.Scrivi(Dati);

        var riletto = Lotto.Leggi(Dati, Sector + Path.DirectorySeparatorChar);
        Assert.Equal(2, riletto.Voci.Count);
        Assert.Equal(1, riletto.DaVedere);
        Assert.Equal(1, riletto.DaPortare);
        Assert.Equal(_ora.AddMinutes(1), riletto.Voci[0].Portata);
        Assert.Equal("la MVA Selection, le zone di LIRF", riletto.Voci[1].Dove);

        // «Visto»: escono le voci in vetrina, resta quella da portare.
        riletto.VistoInAurora(_ora.AddMinutes(3));
        Assert.Equal(["zona X di LIRF"], riletto.Voci.Select(v => v.Cosa));
        Assert.Equal(_ora.AddMinutes(3), riletto.Visto);

        // Un altro sector ha il suo lotto.
        Assert.Empty(Lotto.Leggi(Dati, Path.Combine(_radice, "prova")).Voci);
    }

    [Fact]
    public void CambiatoSectorORamo_UnaVoceSolaPerTutto()
    {
        var lotto = Lotto.Leggi(Dati, Sector);
        lotto.Portato(_ora, "cambiato ramo (airac/2611 → prova/t4): tutto da rivedere");

        var voce = Assert.Single(lotto.Voci);
        Assert.Empty(voce.File);
        Assert.Equal("tutto il sector", voce.Dove);
        Assert.Equal(1, lotto.DaVedere);
    }

    /// <summary>Slice 3: ✅ e non segnate se ne vanno col «visto», le ❌ restano come note finché non si tolgono.</summary>
    [Fact]
    public void GliEsiti_LeNonVaRestanoComeNote()
    {
        const string fix = "SectorFiles/Include/IT/NAVAIDS/APT.fix";
        var lotto = Lotto.Leggi(Dati, Sector);
        lotto.Salvati(
        [
            ("Name di BD100", [fix], [new RecordDelLotto(fix, "BD100")]),
            ("zona X di LIRF", ["SectorFiles/Include/IT/lirf.mva"], []),
            ("Name di BD200", [fix], [new RecordDelLotto(fix, "BD200")]),
        ], _ora);

        // Non ancora in vetrina: niente esito.
        Assert.False(lotto.Segna(0, EsitoInAurora.Come));
        lotto.Portato(_ora.AddMinutes(1));
        Assert.True(lotto.Segna(0, EsitoInAurora.NonVa, "  l'etichetta copre il fix "));
        Assert.True(lotto.Segna(1, EsitoInAurora.NonVa, "zona vuota"));
        Assert.True(lotto.Segna(2, EsitoInAurora.Come, "ignorata"));
        Assert.Equal("l'etichetta copre il fix", lotto.Voci[0].Nota);
        Assert.Null(lotto.Voci[2].Nota);
        // Un secondo clic toglie l'esito.
        Assert.True(lotto.Segna(1, null));
        Assert.Null(lotto.Voci[1].Esito);

        lotto.VistoInAurora(_ora.AddMinutes(2));
        lotto.Scrivi(Dati);

        var riletto = Lotto.Leggi(Dati, Sector);
        Assert.Empty(riletto.Voci);
        var nota = Assert.Single(riletto.Note);
        Assert.Equal("l'etichetta copre il fix", nota.Nota);
        Assert.True(nota.Riguarda(fix, "BD100"));
        Assert.False(nota.Riguarda(fix, "BD200"));

        Assert.True(riletto.TogliLaNota(nota.Id));
        Assert.Empty(riletto.Note);
    }

    [Fact]
    public void UnaNotaSenzaRecord_RiguardaIlFile()
    {
        var nota = new NotaDaAurora("x", "zona X di LIRF", "zona vuota", ["SectorFiles/Include/IT/lirf.mva"], [], _ora);

        Assert.True(nota.Riguarda("SectorFiles/Include/IT/lirf.mva", "qualunque"));
        Assert.False(nota.Riguarda("SectorFiles/Include/IT/lirf.sid", null));
    }

    [Fact]
    public void UnLottoIlleggibile_SiRicomincia()
    {
        string file = Lotto.FileDi(Dati, Sector);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, "{ non è json");

        Assert.Empty(Lotto.Leggi(Dati, Sector).Voci);
    }

    [Theory]
    [InlineData("SectorFiles/Include/IT/lirf.sid", "le SID di LIRF: finestra delle procedure, per pista")]
    [InlineData("SectorFiles/Include/IT/ENRMVA/limm.mva", "la MVA Selection, voce dell'ACC (LIMM)")]
    [InlineData("SectorFiles/Include/IT/NAVAIDS/APT.fix", "i punti sulla mappa (NAVAIDS: FIX)")]
    [InlineData("SectorFiles/Include/IT/GEO/itgeo.geo", "le linee geografiche (itgeo.geo)")]
    [InlineData("SectorFiles/Include/IT/strano.xyz", "il file strano.xyz")]
    public void DoveGuardare_DalTipoDiFile(string file, string dove) => Assert.Equal(dove, DoveGuardare.Di(file));

    [Fact]
    public void DoveGuardare_SenzaDoppioni()
        => Assert.Equal("le SID di LIRF: finestra delle procedure, per pista",
            DoveGuardare.Per(["SectorFiles/Include/IT/lirf.sid", "SectorFiles/Include/IT/lirf.sid"]));
}

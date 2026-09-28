using Vipi.SectorLab.Core.Ispezione;
using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Tests.Ui;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Modifiche;

/// <summary>
/// La rinomina di una voce col nome nelle righe di dati (lotto «Subito» slice 7f): confini, MVA, aerovie, aree P/R/D,
/// nel suo file, righe nascoste e tag del blocco compresi.
/// </summary>
public sealed class RinominaDellaVoceTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();

    public void Dispose() => _albero.Dispose();

    private static IReadOnlyList<string> Dopo(IReadOnlyList<string> righe, TipoDellaVoce tipo, string vecchio, string nuovo)
    {
        var sostituzioni = RinominaDellaVoce.Sostituzioni(righe, tipo, vecchio, nuovo);
        return [.. righe.Select((r, i) => sostituzioni.TryGetValue(i + 1, out var nuove) ? nuove[0] : r)];
    }

    [Fact]
    public void UnConfineCambiaLeRigheTAncheNascosteEIlSuoTagNonLeEtichette()
    {
        string[] righe = ["//@\"RR CONF1\" note=x", "T;RR CONF1;N044.00.00.000;E011.00.00.000;", "// T;RR CONF1;TIPNI;TIPNI;",
                          "T;RR CONF2;N044.00.00.000;E011.00.00.000;", "L;RR CONF1;N044.00.00.000;E011.00.00.000;"];

        Assert.Equal(["//@\"RR NORD\" note=x", "T;RR NORD;N044.00.00.000;E011.00.00.000;", "// T;RR NORD;TIPNI;TIPNI;",
                      "T;RR CONF2;N044.00.00.000;E011.00.00.000;", "L;RR CONF1;N044.00.00.000;E011.00.00.000;"],
            Dopo(righe, TipoDellaVoce.Confine, "RR CONF1", "RR NORD"));
    }

    [Fact]
    public void UnaMvaCambiaAncheIlGruppoDel5CampoSoloSeEraUguale()
    {
        string[] righe = ["L;LIMM;N045.28.00.000;E008.32.00.000;25;8;", "T;LIMM;N045.35.12.000;E008.31.14.000;LIMM;",
                          "T;LIMM;N045.33.29.000;E008.28.03.000;ALTRO;"];

        Assert.Equal(["L;MILANO;N045.28.00.000;E008.32.00.000;25;8;", "T;MILANO;N045.35.12.000;E008.31.14.000;MILANO;",
                      "T;MILANO;N045.33.29.000;E008.28.03.000;ALTRO;"],
            Dopo(righe, TipoDellaVoce.Mva, "LIMM", "MILANO"));
    }

    [Fact]
    public void UnAeroviaCambiaITrattiELaSuaParolaNelleEtichetteCondivise()
    {
        string[] righe = ["T;M984;RODRU;RODRU;", "T;M9841;RODRU;RODRU;", "L;M984;N045.51.27.050;E009.45.13.640;",
                          "L;M984-Y740;N046.05.55.390;E010.16.23.930;", "L;Y740;N046.13.48.240;E010.38.47.920;"];

        Assert.Equal(["T;M985;RODRU;RODRU;", "T;M9841;RODRU;RODRU;", "L;M985;N045.51.27.050;E009.45.13.640;",
                      "L;M985-Y740;N046.05.55.390;E010.16.23.930;", "L;Y740;N046.13.48.240;E010.38.47.920;"],
            Dopo(righe, TipoDellaVoce.Aerovia, "M984", "M985"));
    }

    [Fact]
    public void UnAreaCambiaIl6Campo()
        => Assert.Equal(["N043.04.30.000;E011.33.39.000;N043.09.52.000;E011.29.40.000;RESTRICT;R107X;"],
            Dopo(["N043.04.30.000;E011.33.39.000;N043.09.52.000;E011.29.40.000;RESTRICT;R107B;"], TipoDellaVoce.Area, "R107B", "R107X"));

    [Theory]
    [InlineData("DUMMY", TipoDellaVoce.Confine, "non è un nome")]
    [InlineData("M984-Y740", TipoDellaVoce.Aerovia, "«-»")]
    [InlineData("Y 740", TipoDellaVoce.Aerovia, "spazi")]
    [InlineData("Y740", TipoDellaVoce.Aerovia, "C'è già")]
    public void UnNomeCheNonVaSiRifiuta(string nuovo, TipoDellaVoce tipo, string perche)
    {
        var altre = new[] { new VoceDellaSelezione("Y740", [1], []) };

        Assert.Contains(perche, RinominaDellaVoce.PercheNonVa(nuovo, tipo, altre), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DallaSchedaLaVoceSiRinominaESiAnnulla()
    {
        const string Fra = "SectorFiles/Include/IT/ACC/FRA.artcc";
        var lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"));
        Assert.True(await lab.ApriEValidaAsync(_albero.Radice));
        var npz = lab.VociDi(Fra)!.Single(v => v.Nome == "NPZ");
        Assert.True(lab.SiRinominaLaVoce(Fra, npz));
        Assert.False(lab.SiRinominaLaVoce(Fra, lab.VociDi(Fra)!.Single(v => v.Nome == "Etichette (L)")));

        Assert.True(lab.RinominaLaVoce(Fra, npz, "NPZ NUOVA"));

        var rinominata = lab.VociDi(Fra)!.Single(v => v.Nome == "NPZ NUOVA");
        Assert.Equal(npz.Record, rinominata.Record);
        Assert.DoesNotContain(lab.VociDi(Fra)!, v => v.Nome == "NPZ");

        lab.Annulla();

        Assert.Contains(lab.VociDi(Fra)!, v => v.Nome == "NPZ");
        Assert.False(lab.Modifiche.CEQualcosa);
    }
}

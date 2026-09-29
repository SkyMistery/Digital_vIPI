using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Validazione;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>
/// Lotto «Subito», slice 11b (carta «file per file» M4): nei <c>.rw</c> la rotta con decimali (Aurora rallenta; si propone
/// arrotondata al grado), il verso primario oltre il 18 (il manuale lo vuole fra 01 e 18; si propone coi versi scambiati),
/// la rotta scritta lontana da quella delle soglie (soglie invertite: <c>LIDW 15</c>, <c>LIKL 36</c>). Le voci di menu
/// (<c>//MENU MAPPE</c>, <c>//ACC</c>) non sono piste e non si guardano.
/// </summary>
public sealed class ControlloDellePisteTests : IDisposable
{
    private readonly string _cartella = Path.Combine(Path.GetTempPath(), "piste-" + Guid.NewGuid().ToString("N"));

    public ControlloDellePisteTests() => Directory.CreateDirectory(_cartella);

    public void Dispose() => Directory.Delete(_cartella, recursive: true);

    private IReadOnlyList<ProblemaDelSector> Valida(params string[] piste)
    {
        string percorso = Path.Combine(_cartella, "itrw.rw");
        File.WriteAllText(percorso, string.Join("\r\n",
            ["//MENU MAPPE", "LIAP;MAPS;;0;0;0;0;N000.00.00.000;E000.00.00.000;N000.00.00.000;E000.00.00.000;", "//PISTE", .. piste,
             "//ACC", "LIRR;NE;;0;0;0;0;N000.00.00.000;E000.00.00.000;N000.00.00.000;E000.00.00.000;"]) + "\r\n");
        return Validatore.ValidaIlFile(percorso, "itrw.rw");
    }

    [Fact]
    public void UnaRottaConDecimaliEUnAvvisoColGradoTondoProposto()
    {
        var p = Assert.Single(Valida("LIBD;07;25;162;158;065.49;245.49;N041.08.07.620;E016.45.37.240;N041.08.49.590;E016.47.53.030;"));
        Assert.Equal((Regola.RottaConDecimali, 4, Gravita.Avviso), (p.Regola, p.Riga, p.Gravita));
        Assert.Equal("LIBD;07;25;162;158;065;245;N041.08.07.620;E016.45.37.240;N041.08.49.590;E016.47.53.030;", p.Proposta);
    }

    [Fact]
    public void UnVersoPrimarioOltreIl18EUnAvviso_ElaPropostaScambiaIVersiEArrotonda()
    {
        var problemi = Valida("LIMC;35R;17L;691;745;345.9;165.9;N045.36.56.700;E008.44.14.990;N045.38.31.330;E008.43.48.850;");

        Assert.Equal([Regola.RottaConDecimali, Regola.PrimariaOltre18], problemi.Select(p => p.Regola).Order());
        string giusta = "LIMC;17L;35R;745;691;166;346;N045.38.31.330;E008.43.48.850;N045.36.56.700;E008.44.14.990;";
        Assert.All(problemi, p => Assert.Equal(giusta, p.Proposta));
    }

    [Fact]
    public void UnaRottaLontanaDaQuellaDelleSoglieEUnAvviso_LaDeclinazioneNo()
    {
        // Come LIKL 36 del fork: la soglia del 36 è a nord di quella del 18. LIMC è giusta (riga vera del fork).
        var problemi = Valida(
            "LIKL;18;36;0;0;180;360;N045.00.00.000;E011.00.00.000;N045.01.00.000;E011.00.00.000;",
            "LIMC;17L;35R;745;691;166;346;N045.38.31.330;E008.43.48.850;N045.36.56.700;E008.44.14.990;");

        var p = Assert.Single(problemi);
        Assert.Equal((Regola.RottaDiversaDalleSoglie, 4), (p.Regola, p.Riga));
        Assert.Contains("000", p.Dettaglio);
    }

    // LIMW del fork: la rotta del 09 non c'è; scambiando i versi diventa la primaria, e si scrive la reciproca.
    [Fact]
    public void ScambiandoIVersiLaRottaCheMancaELaReciproca()
    {
        var p = Assert.Single(Valida("LIMW;27;09;1774;1796;261;;N045.44.20.990;E007.22.37.750;N045.44.16.610;E007.21.28.700;"));
        Assert.Equal("LIMW;09;27;1796;1774;081;261;N045.44.16.610;E007.21.28.700;N045.44.20.990;E007.22.37.750;", p.Proposta);
    }

    [Fact]
    public void LaRottaVeraDalleSoglieStaNelModello()
    {
        var pista = new Runway
        {
            Threshold1 = Shared.CoordinateConverter.ParsePair("N045.00.00.000", "E011.00.00.000"),
            Threshold2 = Shared.CoordinateConverter.ParsePair("N045.01.00.000", "E011.00.00.000"),
        };
        Assert.Equal(0, pista.RottaVeraDalleSoglie!.Value, 1);
    }
}

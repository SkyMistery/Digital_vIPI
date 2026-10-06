using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>
/// Lotto «Subito», slice 16a (carta «file per file» S4): il 5° campo di una riga <c>[VFRROUTE]</c> è «Route Military»
/// (manuale IVAO: <c>1</c> = sì, <c>0</c> o vuoto = no; il 4° è riservato). Fino a qui il modello non lo conosceva: le
/// 16 righe <c>…;;1;</c> di <c>libv.vrt</c> e <c>licz.vrt</c> restavano com'erano, e un punto aggiunto a una rotta
/// militare nasceva senza.
/// </summary>
public sealed class VrtMilitareTests
{
    private readonly CollectingWarnings _warnings = new();

    private RottaVfr[] Leggi(params string[] righe)
        => [.. new VrtParser(_warnings).Parse(ParserTestHelpers.Read(string.Join("\r\n", righe) + "\r\n"), "lizz.vrt").Records];

    [Fact]
    public void UnaRottaConTutteLeRigheAUnoEMilitare()
    {
        var rotte = Leggi("1;N040.56.40.000;E016.38.30.000;;1;", "1;N040.53.00.000;E017.00.00.000;;1;", "2;MNL;MNL;", "2;TROIA;TROIA;;0;");

        Assert.Equal([true, false], rotte.Select(r => r.Militare));
        Assert.All(rotte, r => Assert.False(r.MilitareAMeta));
        Assert.Empty(_warnings.Snapshot());
    }

    [Fact]
    public void UnaRottaMilitareSoloSuAlcuneRigheLoDice()
    {
        var rotta = Assert.Single(Leggi("1;MNL;MNL;;1;", "1;TROIA;TROIA;", "1;FOGGIA;FOGGIA;;1;"));

        Assert.False(rotta.Militare);
        Assert.True(rotta.MilitareAMeta);
    }

    [Fact]
    public void LoScrittoreMetteIlMilitareSuOgniRiga_AncheSuUnPuntoNuovo()
    {
        var rotta = Assert.Single(Leggi("1;MNL;MNL;;1;", "1;TROIA;TROIA;;1;"));
        rotta.Punti.Add(Punto.Nominato("FOGGIA"));

        Assert.Equal(["1;MNL;MNL;;1;", "1;TROIA;TROIA;;1;", "1;FOGGIA;FOGGIA;;1;"], new VrtSaver().Serialize(rotta));

        rotta.Militare = false;
        Assert.Equal(["1;MNL;MNL;", "1;TROIA;TROIA;", "1;FOGGIA;FOGGIA;"], new VrtSaver().Serialize(rotta));
    }
}

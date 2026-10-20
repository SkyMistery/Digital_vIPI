using Vipi.SectorLab.Core.Testo;

namespace Vipi.SectorLab.Tests.Testo;

/// <summary>
/// Pagina Testo T2 (D13, «cancellare a mano un record che ha metadati chiede prima»): i record coi metadati che il testo
/// scritto non ha più, e il testo senza i loro tag.
/// </summary>
public sealed class MetadatiNelTestoTests
{
    [Theory]
    [InlineData("//@\"BC404\" note=\"prova\"", "BC404")]
    [InlineData("  //@\"STAR RNAV(ALL)\" compose=A", "STAR RNAV(ALL)")]
    [InlineData("//@LIRF CTR fix=X", "LIRF CTR")]          // la forma vecchia, senza virgolette
    [InlineData("//@START", null)]
    [InlineData("//@END \"BC404\"", null)]
    [InlineData("//@@\"AAAAA\" dir=both", null)]            // il tag di un punto
    [InlineData("//@source=AIRAC2610", null)]               // il tag del file
    [InlineData("BC404;N039.05.11.290;E017.03.27.750;3;", null)]
    public void IlNomeDichiarato(string riga, string? nome) => Assert.Equal(nome, MetadatiNelTesto.NomeDichiarato(riga));

    private static readonly string[] Prima =
    [
        "//@\"A\" note=\"uno\"", "//@START", "A;1;", "//@END \"A\"",
        "//@\"B\" note=\"due\"", "B;2;",                   // senza blocco: la dichiarazione subito sopra
        "C;3;",
    ];

    [Fact]
    public void ITolti_SoloQuelliCheNonSonoPiuInUnaRiga()
    {
        Assert.Empty(MetadatiNelTesto.Tolti(Prima, Prima));
        Assert.Equal(["A"], MetadatiNelTesto.Tolti(Prima, [.. Prima.Where(r => r != "A;1;")]));
        // A cambiato ma ancora lì (le coordinate): non è tolto.
        Assert.Empty(MetadatiNelTesto.Tolti(Prima, [.. Prima.Select(r => r == "A;1;" ? "A;9;" : r)]));
        Assert.Equal(["A", "B"], MetadatiNelTesto.Tolti(Prima, ["C;3;"]));
    }

    [Fact]
    public void SenzaIMetadati_LaDichiarazioneEIlSuoBlocco()
    {
        var senzaA = MetadatiNelTesto.SenzaIMetadatiDi([.. Prima.Where(r => r != "A;1;")], ["A"]);
        Assert.Equal(["//@\"B\" note=\"due\"", "B;2;", "C;3;"], senzaA);

        var senzaB = MetadatiNelTesto.SenzaIMetadatiDi([.. Prima.Where(r => r != "B;2;")], ["B"]);
        Assert.Equal(["//@\"A\" note=\"uno\"", "//@START", "A;1;", "//@END \"A\"", "C;3;"], senzaB);
    }
}

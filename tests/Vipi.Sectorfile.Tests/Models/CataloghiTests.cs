using Vipi.Sectorfile.Models;
using Xunit;

namespace Vipi.Sectorfile.Models.Tests;

/// <summary>
/// I nomi che un record cita (lotto «Subito» slice 7): quelli del validatore (<c>NomeNonRisolto</c>), che ora chiede
/// anche il «chi lo usa» del Lab. Una sola risposta per due domande: se si separassero, il Lab direbbe «nessuno lo
/// usa» di un punto che il validatore cerca.
/// </summary>
public class CataloghiTests
{
    [Fact]
    public void UnAeroviaCitaIPuntiPerNomeNonLeCoordinate()
    {
        var aerovia = new Airway { Name = "M984" };
        aerovia.FixLabels.Add("LUSIL");
        aerovia.FixLabels.Add("N046.02.35.000");

        Assert.Equal(["LUSIL"], Cataloghi.Usati(aerovia));
    }

    [Fact]
    public void UnFixNonCitaNiente()
        => Assert.Empty(Cataloghi.Usati(new Fix { Name = "LUSIL" }));
}

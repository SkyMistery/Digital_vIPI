namespace Vipi.SectorLab.Tests;

/// <summary>
/// La decisione della carta F3 §2.1, tenuta vera: la logica e le pagine del Lab non si portano dietro il sito — né
/// Vipi.Infrastructure, né EF, né l'host, né i componenti del sito.
/// <para>Si guarda la cartella dei test e non <c>GetReferencedAssemblies</c>: quello elenca solo gli assiemi
/// che il codice USA, e un progetto appena nato non ne usa nessuno — il test passerebbe a vuoto. Qui arriva
/// invece tutto quello che i riferimenti di progetto tirano dentro, usato o no.</para>
/// </summary>
public sealed class NessunaDipendenzaDelSitoTests
{
    [Theory]
    [InlineData("Vipi.Infrastructure.dll")]
    [InlineData("Vipi.Hosting.dll")]
    [InlineData("Vipi.Host.dll")]
    [InlineData("Vipi.Ui.dll")]
    [InlineData("Microsoft.EntityFrameworkCore.dll")]
    public void IlSitoNonArrivaNelLab(string assieme)
    {
        Assert.False(File.Exists(Path.Combine(AppContext.BaseDirectory, assieme)),
            $"{assieme} è arrivato fra le dipendenze di Vipi.SectorLab.Core o Vipi.SectorLab.Ui");
    }

    [Theory]
    [InlineData("Vipi.SectorLab.Core.dll")]
    [InlineData("Vipi.SectorLab.Ui.dll")]
    [InlineData("Vipi.Sectorfile.dll")]
    [InlineData("Vipi.Application.dll")]
    public void IlMotoreESiArriva(string assieme)
    {
        // Il rovescio: se la cartella fosse un'altra, i cinque sopra passerebbero per niente.
        Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, assieme)), $"{assieme} manca");
    }
}

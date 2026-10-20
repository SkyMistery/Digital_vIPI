using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests;

/// <summary>
/// Dove il Lab tiene i suoi dati (20 ottobre): <c>--dati</c> per simulare il primo avvio di un AOD senza toccare il Lab
/// vero, e l'autoprova che non scrive più «ultima cartella» e «recenti» nei dati di chi usa il Lab.
/// </summary>
public sealed class CartellaDeiDatiTests
{
    [Fact]
    public void SenzaArgomentiSonoQuelliDiSempre()
    {
        Assert.Equal(SessioneDelLab.CartellaDeiDatiDiBase, SessioneDelLab.CartellaDeiDati(null, autoprova: false));
        Assert.Equal(SessioneDelLab.CartellaDeiDatiDiBase, SessioneDelLab.CartellaDeiDati("  ", autoprova: false));
    }

    [Fact]
    public void LAutoprovaNonUsaIDatiDiChiUsaIlLab()
    {
        string cartella = SessioneDelLab.CartellaDeiDati(null, autoprova: true);
        Assert.Equal(SessioneDelLab.CartellaDellAutoprova, cartella);
        Assert.NotEqual(SessioneDelLab.CartellaDeiDatiDiBase, cartella);
    }

    [Fact]
    public void LaCartellaSceltaVinceAncheSullAutoprova()
    {
        string scelta = Path.Combine(Path.GetTempPath(), "primo-avvio");
        Assert.Equal(Path.GetFullPath(scelta), SessioneDelLab.CartellaDeiDati(scelta, autoprova: false));
        Assert.Equal(Path.GetFullPath(scelta), SessioneDelLab.CartellaDeiDati(scelta, autoprova: true));
    }

    [Fact]
    public void LaSessioneScriveSoloNellaSuaCartella()
    {
        string cartella = Path.Combine(Path.GetTempPath(), "lab-dati-" + Guid.NewGuid().ToString("N"));
        try
        {
            var lab = new SessioneDelLab(cartella);
            Assert.StartsWith(cartella, lab.Registro.Cartella, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(cartella))
                Directory.Delete(cartella, recursive: true);
        }
    }
}

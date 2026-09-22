using System.Reflection;

namespace Vipi.SectorLab.Tests;

/// <summary>
/// Le due trappole del guscio trovate nella slice 1 (carta F3), che si vedono solo col guscio COMPILATO: il test ne legge
/// la cartella d'uscita (il progetto di test lo fa compilare prima, senza referenziarlo).
/// </summary>
public sealed class GuscioTests
{
    private static readonly string Uscita = typeof(GuscioTests).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(a => a.Key == "UscitaDelGuscio").Value!;

    [Fact]
    public void IlManifestoDelGuscioPortaBlazorWebJs()
    {
        // 🔴 Da .NET 10 blazor.web.js entra solo nei progetti con dei .razor loro, e solo con OutputType «Exe»: il guscio
        // non ha .razor ed è «WinExe». Senza le righe del suo csproj la pagina arriva e il circuito non parte mai.
        string manifesto = Path.Combine(Uscita, "VipiSectorLab.staticwebassets.endpoints.json");
        Assert.True(File.Exists(manifesto), $"manca {manifesto}: il guscio non è stato compilato?");
        Assert.Contains("\"_framework/blazor.web.", File.ReadAllText(manifesto), StringComparison.Ordinal);
    }

    [Fact]
    public void IlGuscioEUnAppAFinestraNonDaConsole()
    {
        // Il rovescio del test sopra: la scorciatoia per avere blazor.web.js è mettere «Exe» a tutto il progetto, e con
        // l'inferenza di WinForms spenta dietro la finestra del Lab si aprirebbe una console (controprova fatta: rosso).
        // Sottosistema PE: 2 = GUI, 3 = console.
        string assieme = Path.Combine(Uscita, "VipiSectorLab.dll");
        Assert.True(File.Exists(assieme), $"manca {assieme}: il guscio non è stato compilato?");
        Assert.Equal(2, SottosistemaPe(assieme));
    }

    private static int SottosistemaPe(string percorso)
    {
        using var lettore = new BinaryReader(File.OpenRead(percorso));
        lettore.BaseStream.Position = 0x3C;
        int intestazionePe = lettore.ReadInt32();
        // Firma «PE\0\0» (4) + intestazione COFF (20) + 68 byte di intestazione facoltativa fino al sottosistema.
        lettore.BaseStream.Position = intestazionePe + 4 + 20 + 68;
        return lettore.ReadUInt16();
    }
}

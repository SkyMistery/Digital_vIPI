using Microsoft.Extensions.Logging;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// Il registro del Lab (chiesto alle prove a mano del 23 settembre): i gesti e gli errori in un file al giorno, tenuti
/// 14 giorni, e gli errori del framework portati lì dal ponte.
/// </summary>
public sealed class RegistroTests : IDisposable
{
    private static readonly DateTime Oggi = new(2026, 9, 23, 11, 0, 0);

    private readonly AlberoDiProva _albero = new();
    private readonly string _dati;

    public RegistroTests() => _dati = Path.Combine(_albero.Radice, "dati-del-lab");

    public void Dispose() => _albero.Dispose();

    private string DiOggi => Path.Combine(_dati, "log", "lab-20260923.txt");

    [Fact]
    public void UnaRigaFinisceNelFileDiOggi_ConCategoriaETesto()
    {
        var registro = new Registro(_dati, () => Oggi);

        registro.Scrivi("modifica", "APT.fix#3 Name = «X»");
        registro.Errore("salvataggio", new IOException("disco pieno"));

        string testo = File.ReadAllText(DiOggi);
        Assert.Contains("modifica", testo, StringComparison.Ordinal);
        Assert.Contains("APT.fix#3 Name = «X»", testo, StringComparison.Ordinal);
        Assert.Contains("ERRORE", testo, StringComparison.Ordinal);
        Assert.Contains("System.IO.IOException: disco pieno", testo, StringComparison.Ordinal);
    }

    [Fact]
    public void IFilePiuVecchiDiQuattordiciGiorniSiTolgono_GliAltriNo()
    {
        string log = Path.Combine(_dati, "log");
        Directory.CreateDirectory(log);
        File.WriteAllText(Path.Combine(log, "lab-20260901.txt"), "vecchio");     // 22 giorni
        File.WriteAllText(Path.Combine(log, "lab-20260915.txt"), "recente");     // 8 giorni
        File.WriteAllText(Path.Combine(log, "appunti.txt"), "non è del registro");

        _ = new Registro(_dati, () => Oggi);

        Assert.False(File.Exists(Path.Combine(log, "lab-20260901.txt")));
        Assert.True(File.Exists(Path.Combine(log, "lab-20260915.txt")));
        Assert.True(File.Exists(Path.Combine(log, "appunti.txt")));
    }

    [Fact]
    public void IlPonteScriveAvvisiEdErroriDelFramework_NonLeInformazioni()
    {
        var registro = new Registro(_dati, () => Oggi);
        var logger = new PonteDelRegistro(registro).CreateLogger("Microsoft.AspNetCore.Components.Server.Circuits.CircuitHost");

        logger.LogInformation("circuito aperto");
        logger.LogError(new InvalidOperationException("componente caduto"), "Unhandled exception in circuit");

        string testo = File.ReadAllText(DiOggi);
        Assert.DoesNotContain("circuito aperto", testo, StringComparison.Ordinal);
        Assert.Contains("Unhandled exception in circuit", testo, StringComparison.Ordinal);
        Assert.Contains("componente caduto", testo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ILabRegistraApertura_Modifica_ERifiuto()
    {
        var lab = new SessioneDelLab(_dati);
        Assert.True(await lab.ApriEValidaAsync(_albero.Radice));

        lab.CambiaCampo("SectorFiles/Include/IT/NAVAIDS/APT.fix", 3, "Position", "N041.00.00.000 E012.00.00.000");
        lab.CambiaCampo("SectorFiles/Include/IT/NAVAIDS/APT.fix", 3, "Position", "dove mi pare");

        string testo = File.ReadAllText(lab.Registro.FileDiOggi);
        Assert.Contains("apertura", testo, StringComparison.Ordinal);
        Assert.Contains("validazione", testo, StringComparison.Ordinal);
        Assert.Contains("APT.fix#3 Position = «N041.00.00.000 E012.00.00.000»", testo, StringComparison.Ordinal);
        Assert.Contains("rifiutata", testo, StringComparison.Ordinal);
    }
}

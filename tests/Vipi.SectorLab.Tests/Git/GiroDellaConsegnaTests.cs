using Vipi.SectorLab.Core.Git;

namespace Vipi.SectorLab.Tests.Git;

/// <summary>Riordino R5, le due consegne come giri: ogni passo fatto o da fare dallo stato, e il prossimo.</summary>
public sealed class GiroDellaConsegnaTests
{
    private static StatoDelRamo Ramo(string ramo, int avanti = 0, string? monte = "origin/x", params string[] cambiati) =>
        new("C:/sector", ramo, "https://example.invalid/sector.git", monte, avanti, 0, [.. cambiati.Select(c => new FileCambiato(c, "cambiato"))]);

    private static string Stati(IReadOnlyList<PassoDelGiro> passi) => string.Join(" ", passi.Select(p => $"{p.Chiave}:{p.Stato}"));

    [Fact]
    public void IlSector_IlProssimoEIlPrimoDaFare()
    {
        // Modifiche in sospeso, file salvati da registrare, una registrazione da mandare, gesti da portare in vetrina.
        var passi = GiroDellaConsegna.DelSector(2, 1, Ramo("aod/704798/prova", avanti: 1, cambiati: "SectorFiles/Include/IT/x.fix"),
            vetrina: true, daPortare: 3, daVedere: 0, predefinito: "master");

        Assert.Equal("modifiche:DaFare salva:DaFare registra:DaFare vetrina:DaFare manda:DaFare", Stati(passi));
        Assert.Equal("2 modifiche in sospeso · 1 problema nuovo", passi[0].Detto);
        Assert.Equal("modifiche", GiroDellaConsegna.Prossimo(passi)?.Chiave);
        Assert.Equal("1 file salvato da registrare", passi[2].Detto);
        Assert.Equal("3 gesti da portare in vetrina", passi[3].Detto);
        Assert.Equal("1 registrazione da mandare", passi[4].Detto);

        // Tutto a posto: nessun prossimo.
        var aPosto = GiroDellaConsegna.DelSector(0, 0, Ramo("aod/704798/prova"), vetrina: true, 0, 0, "master");
        Assert.Equal("modifiche:Fatto salva:Fatto registra:Fatto vetrina:Fatto manda:Fatto", Stati(aPosto));
        Assert.Null(GiroDellaConsegna.Prossimo(aPosto));
    }

    [Fact]
    public void IlSector_QuelloCheQuiNonSiFaDiceIlPerche()
    {
        // Sul ramo predefinito non si registra (e non si manda, se avanti); senza vetrina non si porta.
        var passi = GiroDellaConsegna.DelSector(0, 0, Ramo("master", avanti: 1, cambiati: "SectorFiles/Include/IT/x.fix"), vetrina: false, 0, 0, "master");

        Assert.Equal("modifiche:Fatto salva:Fatto registra:NonSiFa vetrina:NonSiFa manda:NonSiFa", Stati(passi));
        Assert.Contains("mai master", passi[4].Detto, StringComparison.Ordinal);
        Assert.Contains("non registra su master", passi[2].Detto, StringComparison.Ordinal);
        Assert.Contains("Impostazioni", passi[3].Detto, StringComparison.Ordinal);
        Assert.Null(GiroDellaConsegna.Prossimo(passi));

        // Senza git; e un ramo di lavoro mai mandato; e in vetrina, da guardare in Aurora.
        Assert.Equal("il sector non è in un repo git", GiroDellaConsegna.DelSector(0, 0, null, true, 0, 0, "master")[2].Detto);
        var mai = GiroDellaConsegna.DelSector(0, 0, Ramo("aod/704798/prova", monte: null), vetrina: true, 0, 2, "master");
        Assert.Equal(("mai mandato", StatoDelPasso.DaFare), (mai[4].Detto, mai[4].Stato));
        Assert.Equal("vetrina", GiroDellaConsegna.Prossimo(mai)?.Chiave);
        Assert.Contains("da guardare in Aurora", mai[3].Detto, StringComparison.Ordinal);
    }

    [Fact]
    public void LAip_IPdfLaLetturaLeSegnalazioniEGit()
    {
        // Niente PDF e niente lettura: si comincia dai PDF.
        var vuoto = GiroDellaConsegna.DellAip(0, null, 0, null);
        Assert.Equal("pdf:DaFare leggi:NonSiFa segnalazioni:Fatto registra:NonSiFa manda:NonSiFa", Stati(vuoto));

        // PDF nella cartella, da leggere.
        Assert.Equal("leggi", GiroDellaConsegna.Prossimo(GiroDellaConsegna.DellAip(4, null, 0, null))?.Chiave);

        // Letto (anche con la cartella vuota), segnalazioni da guardare (non fermano il giro), un file da registrare.
        var letto = GiroDellaConsegna.DellAip(0, "2610", 7, Ramo("aip/2610", cambiati: "2610/li.json"));
        Assert.Equal("pdf:Fatto leggi:Fatto segnalazioni:DaGuardare registra:DaFare manda:Fatto", Stati(letto));
        Assert.Equal("registra", GiroDellaConsegna.Prossimo(letto)?.Chiave);

        // L'AIP si manda solo dal ramo del ciclo.
        var main = GiroDellaConsegna.DellAip(0, "2610", 0, Ramo("main", avanti: 1));
        Assert.Equal(StatoDelPasso.NonSiFa, main[4].Stato);
        Assert.Contains("aip/", main[4].Detto, StringComparison.Ordinal);
    }
}

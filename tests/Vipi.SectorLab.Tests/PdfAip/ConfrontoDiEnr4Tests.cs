using Aip;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Core.PdfAip;
using Vipi.SectorLab.Tests.Ui;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.PdfAip;

/// <summary>
/// Carta F6, slice 3b: le radioassistenze di ENR 4.1 contro i <c>.vor</c> e i <c>.ndb</c>, i punti di ENR 4.4 contro i
/// <c>.fix</c>, in sola lettura, coi casi della misura del 19 ottobre (ELB col canale TACAN sbagliato, PIS che è due
/// radioassistenze, GRO due righe nel sector e nessuna nell'AIP, ADFAM con l'uso diverso, NANKA che ENR 4.4 non ha più).
/// Dati inventati.
/// </summary>
public sealed class ConfrontoDiEnr4Tests : IDisposable
{
    private readonly AlberoDiProva _albero = new();
    private readonly SessioneDelLab _lab;
    private readonly string _espanso;

    public ConfrontoDiEnr4Tests()
    {
        _lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"));
        _espanso = Path.Combine(_albero.Radice, "espanso");
    }

    public void Dispose() => _albero.Dispose();

    private const string Vor =
        "ALG;113.80;N040.37.41.300;E008.14.38.000;;;;HLD-ALG;\r\n"
        + "ELB;114.70;N042.43.50.000;E010.23.45.000;0;2;88X;\r\n"
        + "BOA;117.10;N044.34.13.000;E011.17.26.000;;;;\r\n"
        + "GRO;109.85;N042.45.37.200;E011.04.10.000;;;;\r\n"
        + "GRO;;N042.45.37.200;E011.04.10.000;0;3;35Y;\r\n";

    private const string Ndb =
        "PIS;379.0;N043.35.21.000;E010.17.49.000;\r\n"
        + "TRP;317.0;N037.54.52.000;E012.29.35.000;\r\n";

    private const string Fix =
        "ABADI;N040.45.19.000;E018.38.30.000;2;1;\r\n"
        + "ADFAM;N045.59.42.000;E011.31.31.000;0;0;\r\n"
        + "TIMOV;N041.00.00.000;E012.00.00.000;0;1;\r\n"
        + "NANKA;N041.07.24.000;E018.52.12.000;0;1;\r\n";

    private static Navaid Radioassistenza(string sigla, string tipo, NavaidKind genere, double lat, double lon, string? frequenza, string? canale = null) => new()
    {
        Section = "ENR 4.1", Designator = sigla, Name = sigla, Type = tipo, Kind = genere, Frequency = frequenza, Channel = canale,
        Position = new Position(lat, lon), Components = [new NavaidComponent(null, new Position(lat, lon))],
    };

    private static double Gradi(int g, int p, double s) => g + p / 60.0 + s / 3600.0;

    private static AipDocument Modello() => new()
    {
        State = "LI",
        Navaids =
        [
            // Il sector non scrive il canale: non è una differenza.
            Radioassistenza("ALG", "VORTAC", NavaidKind.Vortac, Gradi(40, 37, 41), Gradi(8, 14, 38), "113.80 MHZ", "85X"),
            Radioassistenza("ELB", "VORTAC", NavaidKind.Vortac, Gradi(42, 43, 50), Gradi(10, 23, 45), "114.70 MHZ", "94X"),
            // A 2 NM da dove lo mette il sector.
            Radioassistenza("BOA", "DVOR/DME", NavaidKind.DvorDme, Gradi(44, 32, 13), Gradi(11, 17, 26), "117.10 MHZ", "118X"),
            // PIS: il locator nel .ndb, il DVOR/DME che il sector non ha.
            Radioassistenza("PIS", "L", NavaidKind.Locator, Gradi(43, 35, 21), Gradi(10, 17, 49), "379.00 KHZ"),
            Radioassistenza("PIS", "DVOR/DME", NavaidKind.DvorDme, Gradi(43, 40, 36), Gradi(10, 23, 30), "112.10 MHZ", "58X"),
            Radioassistenza("TRP", "NDB", NavaidKind.Ndb, Gradi(37, 54, 52), Gradi(12, 29, 35), "317.50 KHZ"),
        ],
        Points =
        [
            new SignificantPoint { Section = "ENR 4.4", Designator = "ABADI", Position = new Position(Gradi(40, 45, 19), Gradi(18, 38, 30)), Routes = ["P92"], Procedures = "BR star", Fra = "FRA(I)" },
            new SignificantPoint { Section = "ENR 4.4", Designator = "ADFAM", Position = new Position(Gradi(45, 59, 42), Gradi(11, 31, 31)), Procedures = "PB Star" },
            new SignificantPoint { Section = "ENR 4.4", Designator = "TIMOV", Position = new Position(41.0, 12.0 + 2.0 / 45), Routes = ["L12"], Fra = "FRA(I)" },
            new SignificantPoint { Section = "ENR 4.4", Designator = "XOPTA", Position = new Position(42.0, 12.0), Procedures = "RA star" },
            new SignificantPoint { Section = "ENR 4.4", Designator = "ZZZZZ", Position = new Position(43.0, 12.0), Routes = ["M2"] },
        ],
    };

    private async Task ApriAsync()
    {
        _albero.Scrivi("SectorFiles/Include/IT/NAVAIDS/itvor.vor", Vor);
        _albero.Scrivi("SectorFiles/Include/IT/NAVAIDS/itndb.ndb", Ndb);
        _albero.Scrivi("SectorFiles/Include/IT/NAVAIDS/itfix.fix", Fix);
        // XOPTA solo in un fix di scalo, ABADI anche lì ma altrove.
        var apt = _albero.Percorso("SectorFiles/Include/IT/NAVAIDS/APT.fix");
        _albero.Scrivi("SectorFiles/Include/IT/NAVAIDS/APT.fix", File.ReadAllText(apt).TrimEnd()
            + "\r\nXOPTA;N042.00.00.000;E012.00.00.000;3;\r\nABADI;N041.00.00.000;E018.00.00.000;3;\r\n");
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        Assert.True(_lab.CambiaLAipEspanso(_espanso), _lab.Rifiuto);
        Directory.CreateDirectory(Path.Combine(_espanso, "2611"));
        await File.WriteAllTextAsync(Path.Combine(_espanso, "2611", "li.json"), AipJson.Serialize(Modello() with { Airac = "2611" }));
    }

    [Fact]
    public async Task LeRadioassistenze_PosizioneFrequenzaCanale_ELeDueDiPis()
    {
        await ApriAsync();

        Assert.True(await _lab.ConfrontaEnr4Async(radioassistenze: true), _lab.Rifiuto);

        var esiti = _lab.UltimoConfrontoDiEnr4!.Value.Esiti;
        var per = esiti.Where(e => e.Esito != EsitoDelConfronto.SoloNelSector).ToDictionary(e => e.Nome);
        Assert.Equal(EsitoDelConfronto.Uguale, per["ALG VORTAC"].Esito);
        Assert.EndsWith("NAVAIDS/itvor.vor", per["ALG VORTAC"].File, StringComparison.Ordinal);
        Assert.Equal(EsitoDelConfronto.FormaDiversa, per["ELB VORTAC"].Esito);
        Assert.Contains("canale AIP 94X, sector 88X", per["ELB VORTAC"].Dettagli);
        Assert.Contains(per["BOA DVOR/DME"].Dettagli, d => d.StartsWith("nel sector a 2.00 NM", StringComparison.Ordinal));
        Assert.Equal(EsitoDelConfronto.Uguale, per["PIS L"].Esito);
        Assert.EndsWith("NAVAIDS/itndb.ndb", per["PIS L"].File, StringComparison.Ordinal);
        Assert.Equal(EsitoDelConfronto.MancaNelSector, per["PIS DVOR/DME"].Esito);
        Assert.Contains("frequenza AIP 317.50 kHz, sector 317.0", per["TRP NDB"].Dettagli);
        // GRO: le due righe del sector (VOR e TACAN), che ENR 4.1 non ha.
        Assert.Equal(["GRO", "GRO"], esiti.Where(e => e.Esito == EsitoDelConfronto.SoloNelSector).Select(e => e.Nome));
        Assert.False(_lab.Modifiche.CEQualcosa);
    }

    [Fact]
    public async Task IPunti_StessoNomeAltroPunto_LUsoEIlFra_EQuelliCheEnr44NonHa()
    {
        await ApriAsync();

        Assert.True(await _lab.ConfrontaEnr4Async(radioassistenze: false), _lab.Rifiuto);

        var esiti = _lab.UltimoConfrontoDiEnr4!.Value.Esiti.ToDictionary(e => e.Nome);
        // ABADI: in itfix.fix al suo posto, in APT.fix altrove (si dice); uso 2 e FRA 1 come l'AIP.
        Assert.Equal(EsitoDelConfronto.Uguale, esiti["ABADI"].Esito);
        Assert.False(esiti["ABADI"].UsoDiverso);
        Assert.EndsWith("itfix.fix", esiti["ABADI"].File, StringComparison.Ordinal);
        Assert.Contains(esiti["ABADI"].Dettagli, d => d.StartsWith("anche in APT.fix", StringComparison.Ordinal));
        // ADFAM: solo procedure nell'AIP (terminale), «in rotta» nel sector.
        Assert.True(esiti["ADFAM"].UsoDiverso);
        Assert.Contains("uso: dall'AIP 1 (terminale), sector 0 (in rotta)", esiti["ADFAM"].Dettagli);
        Assert.Equal(EsitoDelConfronto.FormaDiversa, esiti["TIMOV"].Esito);
        Assert.Contains(esiti["TIMOV"].Dettagli, d => d.StartsWith("stesso nome, altro punto: nel sector a", StringComparison.Ordinal));
        // XOPTA sta solo in un fix di scalo: uguale, e l'uso non si guarda (non è itfix.fix).
        Assert.Equal(EsitoDelConfronto.Uguale, esiti["XOPTA"].Esito);
        Assert.False(esiti["XOPTA"].UsoDiverso);
        Assert.Equal(EsitoDelConfronto.MancaNelSector, esiti["ZZZZZ"].Esito);
        Assert.Equal(EsitoDelConfronto.SoloNelSector, esiti["NANKA"].Esito);
        Assert.DoesNotContain(_lab.UltimoConfrontoDiEnr4!.Value.Esiti, e => e.Esito == EsitoDelConfronto.SoloNelSector && e.File!.EndsWith("APT.fix", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SenzaEnr4NellAipEspansoSiDice()
    {
        await ApriAsync();
        await File.WriteAllTextAsync(Path.Combine(_espanso, "2611", "li.json"), AipJson.Serialize(new AipDocument { State = "LI", Airac = "2611" }));

        Assert.False(await _lab.ConfrontaEnr4Async(radioassistenze: false));
        Assert.Contains("servono i PDF di ENR 4", _lab.Rifiuto, StringComparison.Ordinal);
    }

    // --- slice 3c: i gesti --------------------------------------------------------------------------------------------

    private string[] Righe(string file) => [.. _lab.RigheDiAdesso("SectorFiles/Include/IT/NAVAIDS/" + file)];

    private DifferenzaDiEnr4 Riga(string nome) => _lab.UltimoConfrontoDiEnr4!.Value.Esiti.Single(e => e.Nome == nome);

    [Fact]
    public async Task Sostituisci_SoloICampiDiversi_IlRestoResta()
    {
        await ApriAsync();
        Assert.True(await _lab.ConfrontaEnr4Async(radioassistenze: true), _lab.Rifiuto);

        // ELB: il canale; la visibilità e il tipo (0;2) restano.
        Assert.True(_lab.SostituisciLaRadioassistenza(Riga("ELB VORTAC")), _lab.Rifiuto);
        Assert.Contains("ELB;114.70;N042.43.50.000;E010.23.45.000;0;2;94X;", Righe("itvor.vor"));
        Assert.Equal(EsitoDelConfronto.Uguale, Riga("ELB VORTAC").Esito);
        Assert.StartsWith("ELB VORTAC dall'AIP 2611", _lab.DaAnnullare, StringComparison.Ordinal);

        // BOA: la posizione, al punto dell'AIP.
        Assert.True(_lab.SostituisciLaRadioassistenza(Riga("BOA DVOR/DME")), _lab.Rifiuto);
        Assert.Contains("BOA;117.10;N044.32.13.000;E011.17.26.000;;;;", Righe("itvor.vor"));

        // TRP: la frequenza dell'NDB, coi kHz come li scrive il sector.
        Assert.True(_lab.SostituisciLaRadioassistenza(Riga("TRP NDB")), _lab.Rifiuto);
        Assert.Contains("TRP;317.5;N037.54.52.000;E012.29.35.000;", Righe("itndb.ndb"));

        // Una riga uguale non si sostituisce.
        Assert.False(_lab.SostituisciLaRadioassistenza(Riga("ALG VORTAC")));

        // Annullare rifà il confronto: TRP torna diversa, e con «Annulla tutto» anche ELB e BOA.
        _lab.Annulla();
        Assert.Contains("TRP;317.0;N037.54.52.000;E012.29.35.000;", Righe("itndb.ndb"));
        Assert.Equal(EsitoDelConfronto.FormaDiversa, Riga("TRP NDB").Esito);
        _lab.AnnullaTutte();
        Assert.Equal(EsitoDelConfronto.FormaDiversa, Riga("ELB VORTAC").Esito);
        Assert.Equal(EsitoDelConfronto.FormaDiversa, Riga("BOA DVOR/DME").Esito);
    }

    [Fact]
    public async Task PortaNelSector_LaRadioassistenzaNelSuoFile_EIlPuntoInOrdineDiNome()
    {
        await ApriAsync();
        Assert.True(await _lab.ConfrontaEnr4Async(radioassistenze: true), _lab.Rifiuto);

        Assert.True(_lab.PortaNelSectorDiEnr4(["PIS DVOR/DME"]), _lab.Rifiuto);
        // Nel .vor (non nel .ndb, dove c'è già il locator), mostrato, tipo VOR/DME, col canale.
        Assert.Contains("PIS;112.10;N043.40.36.000;E010.23.30.000;0;1;58X;", Righe("itvor.vor"));
        Assert.Equal(EsitoDelConfronto.Uguale, Riga("PIS DVOR/DME").Esito);
        // Una riga che il sector ha già non si porta.
        Assert.False(_lab.PortaNelSectorDiEnr4(["ALG VORTAC"]));

        Assert.True(await _lab.ConfrontaEnr4Async(radioassistenze: false), _lab.Rifiuto);
        Assert.True(_lab.PortaNelSectorDiEnr4(["ZZZZZ"]), _lab.Rifiuto);
        var fix = Righe("itfix.fix");
        // Sulla M2 e senza procedure né FRA: uso 0, fra 0; in fondo, dopo TIMOV e NANKA che vengono prima.
        Assert.Equal("ZZZZZ;N043.00.00.000;E012.00.00.000;0;0;", fix.Last(r => r.Length > 0));
        Assert.Equal(EsitoDelConfronto.Uguale, Riga("ZZZZZ").Esito);
    }

    [Theory]
    [InlineData("390.00 KHZ", NavaidKind.Ndb, null, "AVI;390.0;N045.55.28.000;E012.25.43.000;")]
    [InlineData("113.80 MHZ", NavaidKind.Vortac, "85X", "AVI;113.80;N045.55.28.000;E012.25.43.000;0;2;85X;")]
    [InlineData(null, NavaidKind.Tacan, "111X", "AVI;;N045.55.28.000;E012.25.43.000;0;3;111X;")]
    public void LaRigaNuovaDiUnaRadioassistenza(string? frequenza, NavaidKind tipo, string? canale, string attesa) =>
        Assert.Equal(attesa, Enr4NelSector.RigaNuova(Radioassistenza("AVI", tipo.ToString(), tipo, Gradi(45, 55, 28), Gradi(12, 25, 43), frequenza, canale)));

    [Fact]
    public void LaRigaSostituitaTieneLAttesaEQuelCheSegue()
    {
        var n = Radioassistenza("ALG", "VORTAC", NavaidKind.Vortac, 40.6, 8.2, "113.85 MHZ", "85X");
        var nuova = Enr4NelSector.Sostituita("ALG;113.80;N040.37.41.300;E008.14.38.000;;;;HLD-ALG;", n, 0.05, 0.1, out var cambiati);

        Assert.Equal("ALG;113.85;N040.37.41.300;E008.14.38.000;;;;HLD-ALG;", nuova);
        Assert.Equal(["frequenza 113.80 → 113.85"], cambiati);
    }

    [Fact]
    public async Task CorreggiLUso_Il4E5CampoDellAip_LAttesaResta()
    {
        // ADFAM con un'attesa nel 6° campo, che resta.
        await ApriAsync();
        _albero.Scrivi("SectorFiles/Include/IT/NAVAIDS/itfix.fix", Fix.Replace("ADFAM;N045.59.42.000;E011.31.31.000;0;0;", "ADFAM;N045.59.42.000;E011.31.31.000;0;1;HLD-ADFAM;"));
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        Assert.True(await _lab.ConfrontaEnr4Async(radioassistenze: false), _lab.Rifiuto);
        Assert.Contains("FRA: nell'AIP no, sector 1", Riga("ADFAM").Dettagli);

        Assert.True(_lab.CorreggiLUso(["ADFAM"]), _lab.Rifiuto);

        Assert.Contains("ADFAM;N045.59.42.000;E011.31.31.000;1;0;HLD-ADFAM;", Righe("itfix.fix"));
        Assert.False(Riga("ADFAM").UsoDiverso);
        Assert.StartsWith("uso di ADFAM dall'AIP 2611", _lab.DaAnnullare, StringComparison.Ordinal);
        // Una riga senza «uso diverso» non si tocca.
        Assert.False(_lab.CorreggiLUso(["ABADI"]));
    }

    [Fact]
    public async Task NelloSpazioConfronti_LaSezioneDeiPunti_ELaRigaSceltaSuiDueLati()
    {
        // Riordino R6: la sezione si confronta col confronto di oggi; una riga sceglie il record del sector e la voce dell'AIP.
        await ApriAsync();

        Assert.True(await _lab.ConfrontaLaSezioneAsync(SezioneDelConfronto.Punti), _lab.Rifiuto);

        Assert.NotNull(_lab.StratoDellAip);   // caricato da sé: serve alla mappa e alla scheda della voce
        // Sulla mappa del Confronto solo la sezione: coste, i punti del sector, dell'AIP i soli punti.
        Assert.Equal(["aip", "punti", "sfondo"], _lab.AccesiDi(SpazioDiLavoro.Confronti).Order(StringComparer.Ordinal));
        Assert.Contains("§aip:" + AipSullaMappa.Radioassistenze, _lab.SpentiDi(SpazioDiLavoro.Confronti));
        Assert.DoesNotContain("§aip:" + AipSullaMappa.Punti, _lab.SpentiDi(SpazioDiLavoro.Confronti));
        var (ciclo, righe) = _lab.Confronti[SezioneDelConfronto.Punti];
        Assert.Equal("2611", ciclo);
        Assert.Equal(_lab.UltimoConfrontoDiEnr4!.Value.Esiti.Count, righe.Count);
        int abadi = righe.ToList().FindIndex(r => r.Aip == "ABADI" && r.File is not null);
        Assert.True(abadi >= 0);
        int versione = _lab.VersioneDellaCoppia;

        _lab.ScegliLaRigaDelConfronto(abadi);

        Assert.Equal((righe[abadi].File!, righe[abadi].Record!.Value), _lab.Scelta);
        Assert.Equal("ABADI", _lab.VoceDellAipScelta?.Titolo);
        Assert.Equal(_lab.Scelta, _lab.CoppiaDelConfronto.Sector);
        Assert.Equal((_lab.StratoDellAip!.File, _lab.SceltaDellAip!.Value), _lab.CoppiaDelConfronto.Aip);
        Assert.True(_lab.VersioneDellaCoppia > versione);
        Assert.Equal(abadi, _lab.RigaDelConfrontoScelta);

        // Passando a un'altra sezione, e tornando a una già fatta, la mappa segue la sezione aperta.
        Assert.True(await _lab.ConfrontaLaSezioneAsync(SezioneDelConfronto.Radioassistenze), _lab.Rifiuto);
        Assert.Contains("radioassistenze", _lab.AccesiDi(SpazioDiLavoro.Confronti));
        _lab.ApriLaSezioneDelConfronto(SezioneDelConfronto.Punti);
        Assert.Contains("punti", _lab.AccesiDi(SpazioDiLavoro.Confronti));
        Assert.DoesNotContain("radioassistenze", _lab.AccesiDi(SpazioDiLavoro.Confronti));
    }

    [Fact]
    public async Task LaPaginaDeiConfronti_SezioniRigheEFiltri()
    {
        await ApriAsync();
        using var contesto = new Bunit.TestContext();
        contesto.JSInterop.Mode = JSRuntimeMode.Loose;
        contesto.Services.AddSingleton(_lab);
        var pagina = contesto.RenderComponent<Vipi.SectorLab.Ui.Components.ConfrontiVista>();
        Assert.Equal(13, pagina.FindAll("[data-sezione]").Count);
        Assert.NotNull(pagina.Find("[data-confronti-vuoto]"));

        pagina.Find("[data-sezione='punti']").Click();

        pagina.WaitForAssertion(() => Assert.NotNull(pagina.Find("[data-righe]")));
        var tutte = _lab.Confronti[SezioneDelConfronto.Punti].Righe;
        // Di base si vede quel che è da guardare; gli uguali si accendono dal filtro.
        Assert.Equal(tutte.Count(r => r.Esito != EsitoDelConfronto.Uguale), pagina.FindAll("[data-riga]").Count);
        pagina.Find("[data-filtro='Uguale']").Change(true);
        pagina.WaitForAssertion(() => Assert.Equal(tutte.Count, pagina.FindAll("[data-riga]").Count));
        Assert.NotNull(pagina.Find("[data-conti='punti']"));
    }
}

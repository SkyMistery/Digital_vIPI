using Vipi.SectorLab.Core.Mappa;
using Vipi.SectorLab.Core.Sessione;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Mappa;

/// <summary>
/// I simboli dei punti sulla mappa (lotto «Subito» slice 17b): quelli di base di Aurora, uno per ruolo, copiati dai
/// profili installati (<c>SYMBOLS_*</c> di <c>BASIC_RADAR.cpr</c>). Prima (slice 4d) venivano dal <c>.sym</c> per nome:
/// la prova T3 ha mostrato che Aurora disegna coi simboli del profilo, e il committente li ha scelti il 17 ottobre.
/// </summary>
public sealed class SimboliDellaMappaTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();

    public void Dispose() => _albero.Dispose();

    private static FormaDellaMappa Punto(string? tipo) => new("x", 0, TipoDiForma.Punto, "P", [], [], Punto: tipo);

    private static string? Ruolo(string? tipo)
        => SimboliDellaMappa.DiAurora.Di(Punto(tipo)) is { } indice ? SimboliDellaMappa.DiAurora.Simboli[indice].Nome : null;

    [Theory]
    [InlineData("FIX", "FIX_ENR")]            // tipo non scritto
    [InlineData("FIX:0", "FIX_ENR")]
    [InlineData("FIX:1", "FIX_TERM")]
    [InlineData("FIX:2", "FIX_ENR_TERM")]
    [InlineData("FIX:3", "FIX_ENR")]          // nascosto: Aurora non lo mostra
    [InlineData("FIX:7", "FIX_ENR")]          // tipo che la tabella non ha: il simbolo della famiglia
    [InlineData("FIX:B", "FIX_BOUND")]
    [InlineData("VOR:0", "FIX_VOR")]
    [InlineData("VOR:1", "FIX_VORDME")]
    [InlineData("VOR:2", "FIX_VORTAC")]
    [InlineData("VOR:3", "FIX_TACAN")]
    [InlineData("VOR:4", "FIX_DME")]
    [InlineData("NDB", "FIX_NDB")]
    [InlineData("VFR", "FIX_VFR")]
    [InlineData("VFR:0", "FIX_VFRN")]
    [InlineData("VFR:2", "FIX_VFRH")]
    [InlineData("VFR:3", "FIX_VFRA")]
    [InlineData("APT", "AIRPORT")]
    [InlineData("APT:0", "AIRPORT")]
    [InlineData("APT:1", "HELIPORT")]
    [InlineData("APT:2", "MILITARY")]
    [InlineData("APT:3", "PRIVATE")]
    [InlineData("APT:4", "UNCONTROLLED")]
    public void OgniTipoHaIlSimboloDelSuoRuolo(string tipo, string ruolo)
        => Assert.Equal(ruolo, Ruolo(tipo));

    [Theory]
    [InlineData(null)]      // una forma che non è un punto
    [InlineData("GATE")]
    public void SenzaUnRuoloRestaUnCerchio(string? tipo)
        => Assert.Null(Ruolo(tipo));

    [Fact]
    public void IDisegniSonoQuelliDeiProfili()
    {
        var simboli = SimboliDellaMappa.DiAurora.Simboli;

        Assert.Equal(19, simboli.Count);
        Assert.All(simboli, s => Assert.Equal(13, s.Colonne.Count));
        Assert.All(simboli, s => Assert.All(s.Colonne, c => Assert.Equal(13, c.Length)));
        // Il rombo pieno dei VFR (committente, 28 settembre, con uno schermo di Aurora): pieno al centro, vuoto in alto.
        var vfr = simboli.Single(s => s.Nome == "FIX_VFR");
        Assert.True(vfr.Acceso(6, 5));
        Assert.False(vfr.Acceso(6, 1));
        // Lo scalo controllato: il cerchio tagliato da una barra in diagonale, dall'alto a sinistra.
        var scalo = simboli.Single(s => s.Nome == "AIRPORT");
        Assert.True(scalo.Acceso(1, 1));
        Assert.True(scalo.Acceso(11, 11));
    }

    // Un disegno 13×13 riconoscibile: solo la prima colonna accesa.
    private const string Barra = "1111111111111;0000000000000;0000000000000;0000000000000;0000000000000;0000000000000;0000000000000;0000000000000;0000000000000;0000000000000;0000000000000;0000000000000;0000000000000;";

    private string Aurora(params (string Profilo, string Testo)[] profili)
    {
        string aurora = Path.Combine(_albero.Radice, "AuroraWindows");
        Directory.CreateDirectory(Path.Combine(aurora, "Profiles"));
        foreach (var (profilo, testo) in profili)
            File.WriteAllText(Path.Combine(aurora, "Profiles", profilo), testo);
        return aurora;
    }

    [Fact]
    public void ISimboliSiLeggonoDalProfiloDiAurora()
    {
        // 20 ottobre: i simboli come li disegna Aurora, dal profilo; del .cpr solo le righe SYMBOLS_ dei punti.
        string aurora = Aurora(
            ("BASIC_RADAR.cpr", "[Connection]\r\nPASSWORD=non-si-legge\r\n[PREFS]\r\nSYMBOLS_FIX_ENR=" + Barra + "\r\nSYMBOLS_FIX_VOR=rotto;\r\nSYMBOLS_SID=" + Barra + "\r\n"),
            ("ALTRO.cpr", "SYMBOLS_FIX_NDB=" + Barra + "\r\n"));

        var simboli = SimboliDellaMappa.DallaCartellaDiAurora(aurora, null);

        Assert.Equal("BASIC_RADAR.cpr", simboli.Profilo);
        Assert.True(simboli.Simboli.Single(s => s.Nome == "FIX_ENR").Acceso(0, 12));
        // Un disegno rotto resta quello di base; un ruolo che il profilo non ha, pure.
        Assert.Equal(SimboliDellaMappa.DiAurora.Simboli.Single(s => s.Nome == "FIX_VOR").Colonne,
                     simboli.Simboli.Single(s => s.Nome == "FIX_VOR").Colonne);
        Assert.Equal(19, simboli.Simboli.Count);

        Assert.Equal("ALTRO.cpr", SimboliDellaMappa.DallaCartellaDiAurora(aurora, "altro.cpr").Profilo);
        Assert.Same(SimboliDellaMappa.DiAurora, SimboliDellaMappa.DallaCartellaDiAurora(Path.Combine(_albero.Radice, "non-ce"), null));
    }

    [Fact]
    public async Task IlProfiloSiSceglieSiRicordaELaMappaSiRicolora()
    {
        string aurora = Aurora(("BASIC_RADAR.cpr", ""), ("ALTRO.cpr", "SYMBOLS_FIX_ENR=" + Barra + "\r\n"));
        string dati = Path.Combine(_albero.Radice, "dati-del-lab");
        var lab = new SessioneDelLab(dati) { CartellaDiAuroraDiBase = aurora };
        Assert.True(await lab.ApriAsync(_albero.Radice));
        Assert.Equal("BASIC_RADAR.cpr", lab.Simboli.Profilo);
        int versione = lab.VersioneDeiColori;

        Assert.True(lab.ScegliIlProfiloDiAurora("ALTRO.cpr"));

        Assert.True(lab.VersioneDeiColori > versione);
        Assert.True(lab.Simboli.Simboli.Single(s => s.Nome == "FIX_ENR").Acceso(0, 0));
        Assert.Equal("ALTRO.cpr", new SessioneDelLab(dati) { CartellaDiAuroraDiBase = aurora }.Simboli.Profilo);
        Assert.False(lab.ScegliIlProfiloDiAurora("NON_CE.cpr"));
    }

    [Fact]
    public void IlTipoDelFixViaggiaConLaForma()
    {
        var sessione = SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);
        var catalogo = CatalogoDeiPunti.PerOgniIsc(sessione)["ITALY.isc"];

        var bc404 = Geometria.DelFile(sessione.File["SectorFiles/Include/IT/NAVAIDS/APT.fix"], catalogo).First(f => f.Etichetta == "BC404");

        Assert.Equal("FIX:3", bc404.Punto);   // BC404;…;3; (nascosto)
    }

    [Fact]
    public async Task ISimboliNonDipendonoDalSymDelMaster()
    {
        var lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"));
        Assert.True(await lab.ApriAsync(_albero.Radice));

        Assert.Same(SimboliDellaMappa.DiAurora, lab.Simboli);
        var bc404 = lab.Strati.Single(s => s.Id == "punti").Forme.First(f => f.Etichetta == "BC404");
        Assert.Equal("FIX_ENR", lab.Simboli.Simboli[lab.Simboli.Di(bc404)!.Value].Nome);
    }
}

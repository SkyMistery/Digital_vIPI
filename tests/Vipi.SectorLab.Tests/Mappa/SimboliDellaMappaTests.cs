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

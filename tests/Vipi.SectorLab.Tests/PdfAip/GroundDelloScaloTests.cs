using Aip;
using Vipi.SectorLab.Core.PdfAip;
using Vipi.SectorLab.Tests.Ui;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.PdfAip;

/// <summary>
/// Carta «Gli aeroporti nel Lab», §2 D, controlli 1 e 2: le soglie dei <c>.rw</c> contro AD 2.12 in metri, e i nomi delle
/// taxiway di AD 2.8 contro i <c>.txi</c>. Le colonne di AD 2.8 hanno la forma di quelle del 2611 (LIRN, LIRF, LIMC, LIME,
/// LIRP, LIDA, LIBP); gli scali e il sector sono inventati.
/// </summary>
public sealed class GroundDelloScaloTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();
    private readonly SessioneDelLab _lab;

    public GroundDelloScaloTests() => _lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"));

    public void Dispose() => _albero.Dispose();

    private const string Testata = "Identificativo Larghezza Superficie Resistenza\ndella TWY (M)\nDesignator of Width (M) Surface Strength\nTWY\n";

    private static Aerodrome ConLaVoce(string valore) => new()
    {
        Icao = "LIXX", Name = "PROVA",
        Sections = [new AerodromeSection { Number = "AD 2.8", Items = [new AerodromeItem("2", "Larghezza, superficie e resistenza delle TWY", valore)], Page = new AipPage("AD 2 LIXX 1-5") }],
    };

    private static Aerodrome ConLaColonna(string colonna) => new()
    {
        Icao = "LIXX", Name = "PROVA",
        Sections = [new AerodromeSection { Number = "AD 2.8", Rows = [["2", "Larghezza, superficie e resistenza delle TWY", colonna, "Larghezza (M) Width (M) 23 M"]] }],
    };

    [Fact]
    public void LaVoce_UnaTaxiwayPerRiga_ConLeFormeDelloScalo()
    {
        var t = GroundDelloScalo.Taxiway(ConLaVoce(Testata + "A 15.0 M ASPH PCN 36/A/X/T\nTWY N 15.0 M CONC/ASPH NIL\nA1 30 M BITUMCONG PCN 58/F/B/W/T\nB 18 M ASPH NIL"))!;

        Assert.Equal(["A", "A1", "B", "N"], t.Nomi);
        Assert.False(t.SenzaNome);
        Assert.Equal("AD 2 LIXX 1-5", t.Pagina?.Reference);

        // LIDA «TWY 16.0 M», LILH «1) TWY 15.0 M», LIBP «ALL 24 M», gli eliporti «NIL NIL NIL NIL».
        Assert.True(GroundDelloScalo.Taxiway(ConLaVoce(Testata + "TWY 16.0 M CONC SIWL 1200 KG"))!.SenzaNome);
        Assert.True(GroundDelloScalo.Taxiway(ConLaVoce(Testata + "1) TWY 15.0 M NIL NIL\n2) TWY 7.5 M NIL NIL"))!.SenzaNome);
        Assert.True(GroundDelloScalo.Taxiway(ConLaVoce(Testata + "ALL 24 M NIL PCN 74/F/A/W/T"))!.Tutte);
        var nil = GroundDelloScalo.Taxiway(ConLaVoce("Identificativo Larghezza Superficie Resistenza\ndella TWY (M)\nDesignator Width (M) Surface Strength\nof TWY\nNIL NIL NIL NIL"))!;
        Assert.Empty(nil.Nomi);
        Assert.False(nil.SenzaNome || nil.Tutte);
    }

    [Fact]
    public void LaColonna_NomiComposti_RimandiNoteELarghezze()
    {
        // LIRN: la colonna semplice, con «E» e (LIRL) «M» che sono taxiway.
        Assert.Equal(["A", "B", "BC", "E", "M", "TN"], GroundDelloScalo.Taxiway(ConLaColonna("Identificativo della TWY Designator of TWY A B BC E M TN"))!.Nomi);
        // LIRF: i composti si spezzano, le note portano rimandi fra parentesi e larghezze.
        Assert.Equal(["A", "AD", "AE", "AK", "AL", "BG", "DA", "DB"],
            GroundDelloScalo.Taxiway(ConLaColonna("Identificativo della TWY Designator of TWY A AD-AE AK-AL NOTE larghezza e 23 M (tra TWY A 30 M (tra TWY BG REMARKS width 23 M (between DA-DB"))!.Nomi);
        // LIMC: un composto spezzato su due righe di testo, col trattino in fondo.
        Assert.Equal(["DE", "EM", "F", "GE", "GW", "ZC"], GroundDelloScalo.Taxiway(ConLaColonna("Designator of TWY EM-DE-GE- GW-F - ZC"))!.Nomi);
        // LIME: dopo «HOLDING POSITION» c'è un punto d'attesa; LIRP: i rimandi «(4)»; LIPH: la larghezza nella stessa colonna.
        Assert.Equal(["B", "BC"], GroundDelloScalo.Taxiway(ConLaColonna("Designator of TWY B - EAST OF TWY BC UNTIL HOLDING POSITION B5"))!.Nomi);
        Assert.Equal(["H", "J"], GroundDelloScalo.Taxiway(ConLaColonna("Designator of TWY H (4) J (3)"))!.Nomi);
        Assert.Equal(["A", "B"], GroundDelloScalo.Taxiway(ConLaColonna("Identificativo Larghezza della TWY (M) Designator of Width TWY A 23 M B 23 M"))!.Nomi);
    }

    [Fact]
    public void SenzaAd28_NienteDaLeggere()
    {
        Assert.Null(GroundDelloScalo.Taxiway(new Aerodrome { Icao = "LIXX", Name = "PROVA" }));
        // LIPG, LIQN sul 2611: la riga 2 senza la colonna dei nomi.
        Assert.Null(GroundDelloScalo.Taxiway(new Aerodrome
        {
            Icao = "LIXX", Name = "PROVA",
            Sections = [new AerodromeSection { Number = "AD 2.8", Rows = [["2", "Larghezza, superficie e resistenza delle TWY", "", ""]] }],
        }));
    }

    private static readonly Position Soglia16L = new(41.85, 12.26), Soglia34R = new(41.81, 12.28);

    private static Aerodrome Scalo() => ConLaColonna("Identificativo della TWY Designator of TWY A B C") with
    {
        // 07/25 senza le coordinate della soglia (LIAQ, LICP sul 2611); 09 nessun .rw la ha.
        Runways =
        [
            new Runway { Designator = "16L", Threshold = Soglia16L }, new Runway { Designator = "34R", Threshold = Soglia34R },
            new Runway { Designator = "07" }, new Runway { Designator = "25" }, new Runway { Designator = "09" },
        ],
    };

    private async Task ApriAsync()
    {
        // 16L al punto dell'AIP, 34R spostata di 10,8" di latitudine (circa 333 m); 13L non è nell'AIP.
        _albero.Scrivi("SectorFiles/Include/IT/OTHER/itrw.rw",
            "//MENU MAPPE\r\n"
            + "LIXX;MAPS;;0;0;0;0;N000.00.00.000;E000.00.00.000;N000.00.00.000;E000.00.00.000;\r\n"
            + "//PISTE\r\n"
            + "LIXX;16L;34R;14;14;160;340;N041.51.00.000;E012.15.36.000;N041.48.46.800;E012.16.48.000;\r\n"
            + "LIXX;07;25;14;14;070;250;N041.48.00.000;E012.12.00.000;N041.48.30.000;E012.15.00.000;\r\n"
            + "LIXX;13L;31R;14;14;130;310;N041.49.00.000;E012.12.00.000;N041.49.30.000;E012.15.00.000;\r\n"
            + "LIYY;16;34;14;14;160;340;N041.51.00.000;E012.15.36.000;N041.48.36.000;E012.16.48.000;\r\n");
        _albero.Scrivi("SectorFiles/Include/IT/lixx.txi",
            "A;LIXX;N0415100000;E0121536000;\r\n"
            + "A;LIXX;N0415101000;E0121537000;\r\n"
            + "c;LIXX;N0415102000;E0121538000;\r\n"
            + "D;LIXX;N0415103000;E0121539000;\r\n"
            + "MIL4;LIXX;N0415104000;E0121540000;\r\n"
            + "APRON NORD;LIXX;N0415105000;E0121541000;\r\n"
            + "B;LIYY;N0415106000;E0121542000;\r\n");
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
    }

    [Fact]
    public async Task LeSoglie_InMetriTestataPerTestata()
    {
        await ApriAsync();

        var g = GroundDelloScalo.Di(Scalo(), _lab.Sessione!);

        Assert.Equal(["16L", "34R", "07", "25", "13L", "31R"], g.Soglie.Select(s => s.Testata));
        var a = g.Soglie[0];
        Assert.True(a.NellAip);
        Assert.InRange(a.Metri!.Value, 0, 1);
        Assert.False(a.Diversa);
        var b = g.Soglie[1];
        Assert.InRange(b.Metri!.Value, 330, 340);
        Assert.True(b.Diversa);
        Assert.EndsWith("itrw.rw", b.File, StringComparison.Ordinal);
        // 07 c'è in AD 2.12 ma senza coordinate; 13L non c'è.
        Assert.True(g.Soglie[2].NellAip);
        Assert.Null(g.Soglie[2].Metri);
        Assert.False(g.Soglie[4].NellAip);
        // Le piste dell'AIP che nessun .rw ha: solo la 09 (la 07 senza coordinate c'è).
        Assert.Equal(["09"], g.PisteSenzaSector);
    }

    [Fact]
    public async Task LeTaxiway_AipControTxi_ConLeAltreEtichetteAParte()
    {
        await ApriAsync();

        var t = GroundDelloScalo.Di(Scalo(), _lab.Sessione!).Taxiway;

        // «c» in minuscolo è la C; «A» due volte è una; la B di LIYY non è di LIXX.
        Assert.Equal(["A", "C"], t.Uguali);
        Assert.Equal(["B"], t.MancanoNelSector);
        Assert.Equal(["D"], t.SoloNelSector);
        Assert.Equal(["APRON NORD", "MIL4"], t.AltreEtichette);
        Assert.Equal(["SectorFiles/Include/IT/lixx.txi"], t.File);
        Assert.False(_lab.Modifiche.CEQualcosa);
    }
}

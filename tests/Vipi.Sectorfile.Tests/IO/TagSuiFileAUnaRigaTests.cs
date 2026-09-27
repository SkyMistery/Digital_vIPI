using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>
/// Lotto «Subito», slice 1b: i tag <c>//@</c> sui file a una riga per record (carta «file per file» §M) — piste
/// (M9), scali (M10), stand (R2b), taxiway (R6), fix, VOR, NDB, punti VFR, posizioni, attese. Ogni tipo ha il suo
/// catalogo e il suo nome d'aggancio; sull'albero intero la prova «tag su tutto» la fa <c>tools/Vipi.SectorfileProva</c>.
/// </summary>
public sealed class TagSuiFileAUnaRigaTests
{
    private readonly CollectingWarnings _warnings = new();

    private const string Pista = "LIRN;06;24;294;294;058;238;N040.52.54.060;E014.16.53.610;N040.53.28.950;E014.18.07.600;";
    private const string Scalo = "LIRN;294;8000;N040.53.04.000;E014.17.27.000;Napoli;";
    private const string UnoStand = "101;LIRN;N040.53.10.000;E014.17.30.000;";
    private const string UnFix = "ABBOZ;N046.02.37.000;E011.07.49.000;1;0;HLD-ABBOZ;";

    private static string Righe(params string[] righe) => string.Join("\r\n", righe) + "\r\n";

    private ParseResult<Runway> Rw(string testo) => new RwParser(_warnings).Parse(ParserTestHelpers.Read(testo), "itrw.rw");

    private ParseResult<AirportInfo> Ap(string testo) => new ApParser(_warnings).Parse(ParserTestHelpers.Read(testo), "itap.ap", new ColorPalette());

    private ParseResult<Stand> Gts(string testo) => new GtsParser(_warnings).Parse(ParserTestHelpers.Read(testo), "lirn.gts", new ColorPalette());

    private ParseResult<Fix> Fix(string testo) => new FixParser(_warnings).Parse(ParserTestHelpers.Read(testo), "itfix.fix", new ColorPalette());

    // M9: la pista è la coppia, e le chiavi per verso hanno il numero davanti; la TORA dagli intermedi è un elenco.
    [Fact]
    public void LaPistaPortaIDatiPerVerso()
    {
        var letto = Rw(Righe("//PISTE", "//@\"LIRN 06/24\" width=45 length=2628 06.thr=399 06.tora=2628 06.int=B:2367,H:1649 06.circuit=R 24.dep=no", Pista));

        var metadati = Metadati.Leggi(letto);

        Assert.Empty(metadati.Problemi);
        var della = Assert.Single(metadati.Record);
        Assert.Equal(("LIRN 06/24", "B:2367,H:1649", "no"), (della.Nome, della.Chiavi["06.int"], della.Chiavi["24.dep"]));
    }

    [Theory]
    [InlineData("06.width=45")]     // la larghezza è della pista, non di un verso
    [InlineData("magvar=4E")]       // chiave dello scalo, non della pista
    public void UnaChiaveFuoriPostoSullaPistaEUnAvviso(string chiave)
    {
        var metadati = Metadati.Leggi(Rw(Righe("//PISTE", "//@\"LIRN 06/24\" " + chiave, Pista)));

        Assert.Equal(TipoDiProblemaDeiMetadati.ChiaveSconosciuta, Assert.Single(metadati.Problemi).Tipo);
    }

    // M10: si scrive, si rilegge, e tolto il tag il file torna quello di prima.
    [Fact]
    public void LoScaloSiScriveESiRilegge()
    {
        var letto = Ap(Righe(Scalo));
        var chiavi = new Dictionary<string, string>
        {
            ["magvar"] = "4E", ["magvar.year"] = "2025.0", ["refcode"] = "4D", ["rff"] = "8", ["traffic"] = "IFR/VFR",
            ["pref"] = "24", ["tailwind"] = "10", ["ats"] = "H24",
        };

        var scritto = Metadati.Scrivi(letto, letto.Records[0], r => Metadati.NomeDelRecord(r), chiavi);
        var righe = new FileSaverOrchestrator().Righe(scritto, new HashSet<AirportInfo>(), new ApSaver());

        Assert.Equal("//@\"LIRN\" magvar=4E magvar.year=2025.0 refcode=4D rff=8 traffic=IFR/VFR pref=24 tailwind=10 ats=H24", righe[0]);
        var riletto = Ap(Righe([.. righe]));
        var metadati = Metadati.Leggi(riletto);
        Assert.Empty(metadati.Problemi);
        Assert.Equal(chiavi.OrderBy(c => c.Key), metadati.Record.Single().Chiavi.OrderBy(c => c.Key));
        Assert.Equal(new[] { Scalo }, new FileSaverOrchestrator().Righe(Metadati.Togli(riletto, riletto.Records[0], r => Metadati.NomeDelRecord(r)),
            new HashSet<AirportInfo>(), new ApSaver()));
    }

    // R2b: gli elenchi (uso, compagnie) stanno in un valore solo, con la virgola.
    [Fact]
    public void LoStandPortaCodiceUsoECompagnie()
    {
        var metadati = Metadati.Leggi(Gts(Righe("//@\"101\" code=C kind=contact use=schengen,cargo airlines=AZA,RYR push=si pushdir=N apron=\"Apron 1\"", UnoStand)));

        Assert.Empty(metadati.Problemi);
        Assert.Equal("Apron 1", Metadati.Testo(metadati.Record.Single().Chiavi["apron"]));
    }

    // La guardia del 21 settembre vale per ogni file: un tag sopra un record con un altro nome è un errore.
    [Fact]
    public void UnTagSopraUnFixDiAltroNomeEUnErrore()
    {
        var metadati = Metadati.Leggi(Fix(Righe("//@\"ABNAT\" note=prova", UnFix)));

        var problema = Assert.Single(metadati.Problemi);
        Assert.Equal((TipoDiProblemaDeiMetadati.NomeNonCombacia, true), (problema.Tipo, problema.EUnErrore));
        Assert.Empty(metadati.Record);
    }

    [Fact]
    public void UnFixPortaLeChiaviComuniESoloQuelle()
    {
        Assert.Empty(Metadati.Leggi(Fix(Righe("//@\"ABBOZ\" locked=si note=\"dall'ENR 4.4\"", UnFix))).Problemi);
        Assert.Equal(TipoDiProblemaDeiMetadati.ChiaveSconosciuta,
            Assert.Single(Metadati.Leggi(Fix(Righe("//@\"ABBOZ\" code=C", UnFix))).Problemi).Tipo);
    }

    [Fact]
    public void IlNomeDiUnaPistaSenzaIlSecondoVersoELaSolaTestata()
    {
        Assert.Equal("LIRR NE", Metadati.NomeDelRecord(new Runway { IcaoCode = "LIRR", Designator1 = "NE" }));
        Assert.Equal("LIRN 06/24", Metadati.NomeDelRecord(new Runway { IcaoCode = "LIRN", Designator1 = "06", Designator2 = "24" }));
    }

    // Il validatore guarda i tag di ogni file che ha un catalogo, e di nessun altro.
    [Fact]
    public void IProblemiDeiTagSiLeggonoPerOgniFileCheLiPorta()
    {
        Assert.NotNull(Metadati.ProblemiDi(Ap(Righe(Scalo))));
        Assert.NotNull(Metadati.ProblemiDi(Rw(Righe("//PISTE", Pista))));
        Assert.Single(Metadati.ProblemiDi(Fix(Righe("//@\"ABNAT\" note=prova", UnFix)))!);
        Assert.Null(Metadati.ProblemiDi(new object()));
    }

    [Fact]
    public void UnRecordDiUnFileSenzaTagNonHaNome()
        => Assert.Throws<NotSupportedException>(() => Metadati.NomeDelRecord(new object()));
}

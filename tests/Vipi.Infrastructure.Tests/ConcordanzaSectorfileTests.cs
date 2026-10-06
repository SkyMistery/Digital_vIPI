using Vipi.Application.Abstractions;
using Vipi.SectorfileProva;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Il motore del sector (<c>Vipi.Sectorfile</c>) e il lettore dell'import di produzione
/// (<c>AuroraSectorfileParser</c>) devono dire la stessa cosa sugli stessi file: stessi punti con le stesse
/// coordinate, stesse SID e STAR per pista (carta F2 §2.4, slice 9). La regola sta in <see cref="Concordanza"/>,
/// condivisa con lo strumento che la misura sull'albero intero; qui gira sui campioni veri del motore, in CI.
/// </summary>
public sealed class ConcordanzaSectorfileTests : IDisposable
{
    private readonly string _cartella = Path.Combine(Path.GetTempPath(), "concordanza-" + Guid.NewGuid().ToString("N"));

    public ConcordanzaSectorfileTests() => Directory.CreateDirectory(_cartella);

    public void Dispose() => Directory.Delete(_cartella, recursive: true);

    [Theory]
    [InlineData("NAVAIDS/itvor.vor", NavaidKind.Vor, "KPT")]      // minuti 75 e secondi 99: nessuno dei due lo legge
    [InlineData("NAVAIDS/itndb.ndb", NavaidKind.Ndb, null)]
    [InlineData("NAVAIDS/APT.fix", NavaidKind.Fix, "MG763")]      // `E008-11.31.443`: il trattino al posto del punto
    [InlineData("NAVAIDS/VFR_NASCOSTI.fix", NavaidKind.Fix, null)]
    public void IPuntiDeiCampioniConcordano(string campione, NavaidKind natura, string? rifiutato)
    {
        var esito = Concordanza.DeiPunti(Campione(campione), natura);

        Assert.True(esito.Pulito, Descrivi(esito));
        Assert.True(esito.Concordi > 0);
        Assert.Equal(rifiutato is null ? Array.Empty<string>() : new[] { rifiutato }, esito.RifiutatiDaEntrambi);
    }

    // Il TACAN di Grosseto (`GRO;;…;35Y`) è la differenza che la concordanza ha trovato: vIPI lo leggeva, il motore lo
    // chiamava malformato. Ora lo leggono tutti e due, accanto al VOR omonimo.
    [Fact]
    public void IlTacanDiGrossetoLoLegganoTuttiEDue()
    {
        var esito = Concordanza.DeiPunti(Scrivi("itvor.vor",
            "GRO;109.85;N042.45.39.200;E011.04.38.300;;;;",
            "GRO;;N042.45.37.200;E011.04.38.600;0;3;35Y"), NavaidKind.Vor);

        Assert.True(esito.Pulito, Descrivi(esito));
        Assert.Equal(2, esito.Concordi);
    }

    // 🔴 Difetto noto di vIPI, NON corretto in F2 (l'import di produzione non cambia, carta §4; lavori aperti):
    // la coppia DECIMALE di un fix (`MIL.fix:235` `TAC-06R;40.98618505;13.75008401;3;`) il motore la legge, il DMS
    // di vIPI no, e il punto resta in catalogo senza posizione. Quando vIPI la leggerà, questo test cade: si capovolge.
    [Fact]
    public void LaCoppiaDecimaleDiUnFixLaLeggeSoloIlMotore()
    {
        var esito = Concordanza.DeiPunti(Scrivi("MIL.fix", "TAC-06R;40.98618505;13.75008401;3;"), NavaidKind.Fix);

        Assert.Contains("TAC-06R: vIPI senza coordinate", Assert.Single(esito.SoloVipi));
        Assert.Empty(esito.SoloMotore);
    }

    [Theory]
    [InlineData("lirf.sid", false)]
    [InlineData("lied.sid", false)]   // i blocchi di partenza a vista: vertici con etichetta, non SID
    [InlineData("lirf.str", true)]    // MAPS, attese, IAP e FAP convivono con le STAR
    public void LeProcedureDeiCampioniConcordano(string campione, bool star)
    {
        var esito = Concordanza.DelleProcedure(Campione(campione), star);

        Assert.True(esito.Pulito, Descrivi(esito));
        Assert.True(esito.Concordi > 0);
    }

    // Gli stessi filtri dalle due parti: la riga di un altro ICAO, il tipo 1 (una shape), MAPS dentro l'elenco piste.
    [Fact]
    public void IFiltriDelleStarSonoGliStessi()
    {
        var esito = Concordanza.DelleProcedure(Scrivi("lirf.str",
            "LIRF;16L:16R;ELKAP1A; ; ;",
            "ELKAP;ELKAP;",
            "LIRA;16L;XIBR5A; ; ;",
            "XIBRO;XIBRO;",
            "LIRF;16L;LIRF CTR; ; ;1;",
            "N041.00.00.000;E012.00.00.000;",
            "LIRF;MAPS:25;RITE1A; ; ;0;",
            "RITEM;RITEM;"), star: true);

        Assert.True(esito.Pulito, Descrivi(esito));
        Assert.Equal(3, esito.Concordi);   // ELKAP1A per 16L e 16R, RITE1A per 25
    }

    // La prova distingue: una STAR che uno dei due non vede si elenca. Un'intestazione di tre campi vIPI la prende
    // (le basta l'ICAO, la pista e il nome), il motore no (vuole i cinque campi di Aurora). Nel sector di oggi non ce
    // n'è nessuna; se un giorno comparisse, lo strumento la direbbe.
    [Fact]
    public void UnaStarCheIlMotoreNonVedeSiElenca()
    {
        var esito = Concordanza.DelleProcedure(Scrivi("lirf.str",
            "LIRF;16L;ELKAP1A;",
            "ELKAP;ELKAP;"), star: true);

        Assert.False(esito.Pulito);
        Assert.Equal("ELKAP1A pista 16L", Assert.Single(esito.SoloVipi));
    }

    // ── Lotto «Subito» slice 19: la lettura di prova dei tag (carta del lotto §5.3; «file per file» §M regola 10) ──
    // Il Lab scrive i metadati nel sector come righe `//@`; vIPI li leggerà per riempire i suoi campi (fix intero,
    // salita iniziale). Nessun cambio al sito: qui si prova che (1) per il lettore di produzione di OGGI un file coi
    // tag è lo stesso file, e (2) ogni SID e STAR che vIPI legge ritrova i suoi tag per nome.

    [Theory]
    [InlineData("lirf.sid")]
    [InlineData("lied.sid")]
    [InlineData("lirf.str")]
    [InlineData("NAVAIDS/APT.fix")]
    [InlineData("NAVAIDS/VFR_NASCOSTI.fix")]
    [InlineData("NAVAIDS/itvor.vor")]
    [InlineData("NAVAIDS/itndb.ndb")]
    [InlineData("OTHER/itfreq.frq")]
    [InlineData("OTHER/itap.ap")]
    [InlineData("OTHER/itrw.rw")]
    [InlineData("ENRMVA/lirr.mva")]
    [InlineData("liba.mva")]
    [InlineData("DYNAMIC_SEC/lirrctr.tfl")]
    [InlineData("DYNAMIC_SEC/twrs.tfl")]
    [InlineData("DYNAMIC_SEC/limmfic.tfl")]
    public void UnCampioneCoiTagVipiLoLeggeComePrima(string campione)
    {
        var esito = Concordanza.DeiTag(Campione(campione));

        Assert.True(esito.Etichettati > 0, "nessun record etichettato: la prova sarebbe verde a vuoto");
        Assert.True(esito.Oggetti > 0, "vIPI non legge niente da questo campione");
        Assert.True(esito.Pulita, string.Join("\n", esito.Cambiati.Concat(esito.ProcedureSenza.Select(p => "senza i suoi tag: " + p))));
    }

    [Theory]
    [InlineData("lirf.sid")]
    [InlineData("lirf.str")]
    public void OgniProceduraDiVipiRitrovaISuoiTagPerNome(string campione)
    {
        var esito = Concordanza.DeiTag(Campione(campione));

        // Tante quante ne legge vIPI: una per pista, come le tiene lui.
        Assert.Equal(esito.Oggetti, esito.ProcedureCoiLoroTag);
        Assert.Empty(esito.ProcedureSenza);
    }

    // La stessa prova scritta per esteso, su un file come lo lascia il Lab (prova 68 di PROVE.md): il blocco col fix
    // intero e la salita iniziale, e un vincolo su un punto.
    [Fact]
    public void UnaSidColBloccoDelLab_VipiLaLeggeUguale_EITagDiconoFixESalita()
    {
        string[] senza =
        [
            "LIRF;16R;EKLO8R;;;0;;1;",
            "N041.48.01.000;E012.14.20.000;",
            "EKLOS;EKLOS;",
            "LIRF;25;OST1E;;;;OST;1;",
            "OST;OST;",
        ];
        string[] coiTag =
        [
            "//@source=AIRAC2610",
            "//@\"EKLO8R\" fix=EKLOS initialclimb=\"COO APP\"",
            "//@START",
            "LIRF;16R;EKLO8R;;;0;;1;",
            "N041.48.01.000;E012.14.20.000;",
            "//@@\"EKLOS\" role=IAF alt=+FL80",
            "EKLOS;EKLOS;",
            "//@END \"EKLO8R\"",
            "//@\"OST1E\" fix=OST initialclimb=5000",
            "LIRF;25;OST1E;;;;OST;1;",
            "OST;OST;",
        ];
        var nessunNome = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var nessunAlias = new Dictionary<string, string>();

        var diVipiSenza = Vipi.Infrastructure.Sectorfile.AuroraSectorfileParser.ParseSids("LIRF", string.Join("\r\n", senza), nessunNome, nessunAlias);
        var diVipiCoiTag = Vipi.Infrastructure.Sectorfile.AuroraSectorfileParser.ParseSids("LIRF", string.Join("\r\n", coiTag), nessunNome, nessunAlias);

        // (1) Oggi vIPI non se ne accorge.
        Assert.Equal(["EKLO8R", "OST1E"], diVipiSenza.Select(p => p.Name));
        Assert.Equal(diVipiSenza, diVipiCoiTag);

        // (2) La lettura di prova: il motore legge i tag, e il nome della procedura di vIPI è la chiave.
        var tag = Concordanza.TagDiUnSid(Scrivi("lirf.sid", coiTag));
        Assert.Empty(tag.Problemi);
        Assert.Equal("AIRAC2610", tag.DelFile["source"]);
        var perNome = tag.Record.ToDictionary(m => m.Nome, m => m.Chiavi);
        Assert.Equal(("EKLOS", "COO APP"), (perNome[diVipiCoiTag[0].Name]["fix"], Vipi.Sectorfile.IO.Metadati.Testo(perNome[diVipiCoiTag[0].Name]["initialclimb"])));
        Assert.Equal(("OST", "5000"), (perNome[diVipiCoiTag[1].Name]["fix"], perNome[diVipiCoiTag[1].Name]["initialclimb"]));
        var vincolo = Assert.Single(tag.Punti);
        Assert.Equal(("EKLOS", "IAF", "+FL80"), (vincolo.Punto, vincolo.Chiavi["role"], vincolo.Chiavi["alt"]));
    }

    private string Scrivi(string nome, params string[] righe)
    {
        string percorso = Path.Combine(_cartella, nome);
        File.WriteAllText(percorso, string.Join("\r\n", righe) + "\r\n");
        return percorso;
    }

    private static string Descrivi(Concordanza.Esito esito) =>
        string.Join("\n", esito.Discordi.Select(d => "discorde " + d)
            .Concat(esito.SoloVipi.Select(d => "solo vIPI " + d))
            .Concat(esito.SoloMotore.Select(d => "solo motore " + d)));

    // I campioni stanno col motore (tests/Vipi.Sectorfile.Tests/Campioni): se ne manca uno il test è ROSSO, non verde a
    // vuoto (la lezione della libreria A).
    private static string Campione(string relativo)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string candidato = Path.Combine(dir.FullName, "Vipi.Sectorfile.Tests", "Campioni", relativo);
            if (File.Exists(candidato))
            {
                return candidato;
            }
        }

        throw new FileNotFoundException($"Campione mancante: {relativo}");
    }
}

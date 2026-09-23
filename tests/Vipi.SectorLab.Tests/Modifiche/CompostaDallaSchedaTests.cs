using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;

namespace Vipi.SectorLab.Tests.Modifiche;

/// <summary>
/// Carta F3-bis, slice 5: «Composta da» nella scheda di una mappa. Spuntare riscrive il tag e rigenera la mappa; la
/// prima volta la forma (troncata o intera) è quella che lascia la mappa com'è; annullare rimette tutto.
/// </summary>
public sealed class CompostaDallaSchedaTests : IDisposable
{
    private const string Lime = "SectorFiles/Include/IT/lime.str";

    private readonly AlberoDiProva _albero = new();
    private readonly ModificheInSospeso _modifiche = new();
    private SessioneAperta? _sessione;

    public void Dispose() => _albero.Dispose();

    private static readonly string[] Procedure =
    [
        "LIME;28:10;ODIN4E;;;;;1;", "ODINA;ODINA;4E;", "ME872;ME872;", "OBFUL;OBFUL;", "TIXUM;TIXUM;", "",
        "LIME;28:10;EKLI4E;;;;;1;", "EKLIB;EKLIB;4E;", "ME768;ME768;", "OBFUL;OBFUL;", "TIXUM;TIXUM;",
    ];

    // La mappa di lime.str, senza tag: ODIN4E intera, EKLI4E ferma su OBFUL.
    private static readonly string[] Troncata =
    [
        "LIME;MAPS;STAR RNAV(ALL);;;;;1;",
        "ODINA;ODINA;<br>", "ODINA;ODINA;4E;", "ME872;ME872;", "OBFUL;OBFUL;", "TIXUM;TIXUM;", "",
        "EKLIB;EKLIB;<br>", "EKLIB;EKLIB;4E;", "ME768;ME768;", "OBFUL;OBFUL;", "",
    ];

    // Come le mappe di lirs.str: le procedure intere anche dove si toccano.
    private static readonly string[] Intera =
    [
        "LIME;MAPS;STAR RNAV(ALL);;;;;1;",
        "ODINA;ODINA;<br>", "ODINA;ODINA;4E;", "ME872;ME872;", "OBFUL;OBFUL;", "TIXUM;TIXUM;", "",
        "EKLIB;EKLIB;<br>", "EKLIB;EKLIB;4E;", "ME768;ME768;", "OBFUL;OBFUL;", "TIXUM;TIXUM;", "",
    ];

    private FileAperto Apri(string[] mappa)
    {
        _albero.Scrivi(Lime, string.Join("\r\n", [.. mappa, .. Procedure]) + "\r\n");
        _sessione = SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);
        return _sessione.File[Lime];
    }

    private static StrRecord Mappa(FileAperto file) => ((FileLetto<StrRecord>)file).Letto.Records[0];

    private static List<string> Punti(FileAperto file) => [.. MappeComposte.PuntiDi(Mappa(file)).Select(p => p.Chiave)];

    private static ProceduraDellaComposta Voce(string nome) => new(null, nome);

    [Fact]
    public void LaSchedaHaUnaCasellaPerProceduraELeSpuntaDallElenco()
    {
        var file = Apri(Troncata);
        _modifiche.CambiaLaComposta(file, 0, [Voce("ODIN4E")]);

        var scheda = SchedaDellaComposta.Di(file, 0)!;

        Assert.Equal(["ODIN4E", "EKLI4E"], scheda.Caselle.Select(c => c.Testo));
        Assert.Equal([true, false], scheda.Caselle.Select(c => c.Scelta));
        Assert.Null(SchedaDellaComposta.Di(file, 1));   // ODIN4E non è una mappa
    }

    // Diventare composta così com'è: solo le tre righe del tag, la mappa non cambia.
    [Fact]
    public void UnaMappaGiaCosiDiventaCompostaColSoloTag()
    {
        var file = Apri(Troncata);

        var voce = Assert.IsType<ModificaDellaDichiarazione>(_modifiche.CambiaLaComposta(file, 0, [Voce("ODIN4E"), Voce("EKLI4E")]));

        Assert.Equal(("—", "ODIN4E, EKLI4E"), (voce.Prima, voce.Dopo));
        Assert.Empty(_modifiche.Voci.OfType<ModificaDellaComposta>());
        var diff = _modifiche.DiffDi(file);
        Assert.Equal((0, 3), (diff.Tolte, diff.Aggiunte));
        Assert.Contains(diff.Pezzi.SelectMany(p => p.Righe), r => r.Testo == "//@\"STAR RNAV(ALL)\" composta=ODIN4E,EKLI4E");
    }

    // La prima volta il Lab sceglie da solo la forma che lascia la mappa com'è: qui intera (come lirs.str).
    [Fact]
    public void LaFormaSiScegliePerLasciareLaMappaComE()
    {
        var file = Apri(Intera);

        var voce = Assert.IsType<ModificaDellaDichiarazione>(_modifiche.CambiaLaComposta(file, 0, [Voce("ODIN4E"), Voce("EKLI4E")]));

        Assert.EndsWith("(intere)", voce.Dopo, StringComparison.Ordinal);
        Assert.True(SchedaDellaComposta.Di(file, 0)!.Intere);
        Assert.Empty(_modifiche.Voci.OfType<ModificaDellaComposta>());
    }

    [Fact]
    public void TogliereUnaCasellaRigeneraLaMappa()
    {
        var file = Apri(Troncata);
        _modifiche.CambiaLaComposta(file, 0, [Voce("ODIN4E"), Voce("EKLI4E")]);

        _modifiche.CambiaLaComposta(file, 0, [Voce("ODIN4E")]);

        Assert.Equal(["ODINA", "ODINA", "ME872", "OBFUL", "TIXUM"], Punti(file));
        Assert.Single(_modifiche.Voci.OfType<ModificaDellaComposta>());
    }

    [Fact]
    public void LaCasellaIntereRidisegnaLeProcedure()
    {
        var file = Apri(Troncata);
        _modifiche.CambiaLaComposta(file, 0, [Voce("ODIN4E"), Voce("EKLI4E")]);

        _modifiche.CambiaLaComposta(file, 0, [Voce("ODIN4E"), Voce("EKLI4E")], intere: true);

        Assert.Equal("TIXUM", Punti(file)[^1]);
        Assert.Equal(10, Punti(file).Count);   // 5 + 5: EKLI4E non si ferma più su OBFUL
    }

    // Annullare la voce dell'elenco toglie il tag e rimette la mappa: il file torna quello del disco.
    [Fact]
    public void AnnullareLElencoRimetteIlFileComEra()
    {
        var file = Apri(Troncata);
        _modifiche.CambiaLaComposta(file, 0, [Voce("ODIN4E"), Voce("EKLI4E")]);
        _modifiche.CambiaLaComposta(file, 0, [Voce("ODIN4E")]);

        var elenco = Assert.Single(_modifiche.Voci.OfType<ModificaDellaDichiarazione>());
        Assert.True(_modifiche.Annulla(file, elenco));

        Assert.False(_modifiche.CEQualcosa);
        Assert.Empty(_modifiche.DiffDi(file).Pezzi);
        Assert.Null(SchedaDellaComposta.Di(file, 0)!.Elenco.FirstOrDefault());
        Assert.Equal(9, Punti(file).Count);   // 5 + 4, com'era sul disco
    }

    // Togliere tutte le caselle è come annullare: il tag se ne va, la mappa torna com'era.
    [Fact]
    public void TogliereTutteLeCaselleTogliIlTag()
    {
        var file = Apri(Troncata);
        _modifiche.CambiaLaComposta(file, 0, [Voce("ODIN4E")]);

        _modifiche.CambiaLaComposta(file, 0, []);

        Assert.False(_modifiche.CEQualcosa);
        Assert.Empty(_modifiche.DiffDi(file).Pezzi);
    }

    // Il tag si aggancia al nome: rinominare una mappa composta riscrive anche il tag, e annullare lo rimette.
    [Fact]
    public void RinominareUnaMappaCompostaPortaConSeIlTag()
    {
        var file = Apri(Troncata);
        _modifiche.CambiaLaComposta(file, 0, [Voce("ODIN4E"), Voce("EKLI4E")]);

        var rinomina = Assert.IsType<ModificaDiCampo>(_modifiche.Cambia(file, 0, "ProcedureId", "STAR ALTRA(ALL)"));

        var letto = ((FileLetto<StrRecord>)file).Letto;
        var metadati = Metadati.Leggi(letto, Metadati.NomeStr);
        Assert.Empty(metadati.Problemi);
        Assert.Equal(("STAR ALTRA(ALL)", "ODIN4E,EKLI4E"), (metadati.Di(Mappa(file))!.Nome, metadati.Di(Mappa(file))!.Chiavi["composta"]));

        _modifiche.Annulla(file, rinomina);
        metadati = Metadati.Leggi(((FileLetto<StrRecord>)file).Letto, Metadati.NomeStr);
        Assert.Empty(metadati.Problemi);
        Assert.Equal("STAR RNAV(ALL)", metadati.Di(Mappa(file))!.Nome);
    }

    // Una mappa nuova: si copia la vicina, si rinomina, si spuntano le procedure. Ha solo le sue.
    [Fact]
    public void UnaMappaNuovaSiFaCopiandoRinominandoESpuntando()
    {
        var file = Apri(Troncata);
        Assert.IsType<ModificaDiStruttura>(_modifiche.AggiungiRecord(file, 0));
        int nuova = _modifiche.UltimoAggiunto!.Value;
        _modifiche.Cambia(file, nuova, "ProcedureId", "STAR EKLI(ALL)");

        Assert.IsType<ModificaDellaDichiarazione>(_modifiche.CambiaLaComposta(file, nuova, [Voce("EKLI4E")]));

        var record = ((FileLetto<StrRecord>)file).Letto.Records;
        Assert.Equal(["EKLIB", "EKLIB", "ME768", "OBFUL", "TIXUM"], MappeComposte.PuntiDi(record[nuova]).Select(p => p.Chiave));
        Assert.Equal(9, MappeComposte.PuntiDi(record[0]).Count);   // la vicina non si tocca
        Assert.Empty(Metadati.Leggi(((FileLetto<StrRecord>)file).Letto, Metadati.NomeStr).Problemi);
    }

    // «Composta da quello che disegna oggi»: l'elenco dei tratti; la mappa diventa composta col solo tag.
    [Fact]
    public void QuelloCheDisegnaOggiLaAdottaSenzaCambiarla()
    {
        var file = Apri(Troncata);
        var scheda = SchedaDellaComposta.Di(file, 0)!;

        Assert.Equal(["ODIN4E", "EKLI4E"], scheda.DaQuelloCheDisegna.Select(v => v.Nome));
        _modifiche.CambiaLaComposta(file, 0, scheda.DaQuelloCheDisegna);
        Assert.Empty(_modifiche.Voci.OfType<ModificaDellaComposta>());
        Assert.Equal((0, 3), (_modifiche.DiffDi(file).Tolte, _modifiche.DiffDi(file).Aggiunte));
    }

    // Un nome con lo spazio (RNP10 UPETI di lica.str) non può stare nell'elenco: casella spenta, e rifiuto, non eccezione.
    [Fact]
    public void UnaProceduraColNomeConSpaziNonSiElenca()
    {
        _albero.Scrivi(Lime, string.Join("\r\n", [.. Troncata, .. Procedure, "", "LIME;10;RNP10 UPETI;;;3;;1;", "UPETI;UPETI;", "ME768;ME768;"]) + "\r\n");
        _sessione = SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);
        var file = _sessione.File[Lime];

        var casella = SchedaDellaComposta.Di(file, 0)!.Caselle.Single(c => c.Voce.Nome == "RNP10 UPETI");
        Assert.False(casella.Elencabile);
        Assert.Equal("avvicinamento", casella.Tipo);
        var rifiutata = Assert.IsType<ModificaRifiutata>(_modifiche.CambiaLaComposta(file, 0, [Voce("ODIN4E"), casella.Voce]));
        Assert.Contains("RNP10 UPETI", rifiutata.Motivo, StringComparison.Ordinal);
        Assert.False(_modifiche.CEQualcosa);
    }

    [Fact]
    public void UnaProceduraNonPuoEssereComposta()
    {
        var file = Apri(Troncata);

        Assert.IsType<ModificaRifiutata>(_modifiche.CambiaLaComposta(file, 1, [Voce("EKLI4E")]));
    }
}

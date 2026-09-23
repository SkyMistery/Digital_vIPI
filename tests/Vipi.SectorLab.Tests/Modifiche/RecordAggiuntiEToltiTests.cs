using Vipi.SectorLab.Core.Ispezione;
using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Core.Sessione;

namespace Vipi.SectorLab.Tests.Modifiche;

/// <summary>
/// Aggiungere e togliere un record (carta F3 §2.3, slice 8): il nuovo esce <b>nella forma dei vicini</b>, il tolto
/// si porta via le sue righe, e annullare rimette i record com'erano.
/// </summary>
public sealed class RecordAggiuntiEToltiTests : IDisposable
{
    private const string Fix = "SectorFiles/Include/IT/NAVAIDS/APT.fix";

    private readonly AlberoDiProva _albero = new();
    private readonly ModificheInSospeso _modifiche = new();

    public void Dispose() => _albero.Dispose();

    private SessioneAperta Apri() => SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);

    private static IReadOnlyList<string> Righe(FileAperto file, ModificheInSospeso modifiche)
        => ((IFileConRecord)file).RigheDelFile(modifiche.SporchiDi(file.Relativo));

    [Fact]
    public void UnRecordAggiuntoEUNARIGAInPiu()
    {
        var sessione = Apri();
        var file = sessione.File[Fix];
        var comErano = ((IFileConRecord)file).RigheDelFile([]).ToList();
        int quanti = file.Record;

        var fatta = _modifiche.AggiungiRecord(file, 3);

        Assert.IsType<ModificaDiStruttura>(fatta);
        Assert.Equal(quanti + 1, file.Record);
        var diff = _modifiche.DiffDi(file);
        Assert.Equal(0, diff.Tolte);
        Assert.Equal(1, diff.Aggiunte);
        Assert.Equal(comErano.Count + 1, Righe(file, _modifiche).Count);
    }

    [Fact]
    public void IlRecordNuovoNASCECOMEILVicino()
    {
        var sessione = Apri();
        var file = sessione.File[Fix];
        var righeDelVicino = ((IFileConRecord)file).RigheDelRecord(3, contesto: 0).Select(r => r.Testo).ToList();

        _modifiche.AggiungiRecord(file, 3);

        // La riga nuova è quella del vicino: i campi di struttura ci sono già, e l'AOD cambia quel che deve.
        var aggiunta = _modifiche.DiffDi(file).Pezzi.Single().Righe.Single(r => r.Segno == SegnoDelDiff.Aggiunta);
        Assert.Equal(righeDelVicino[0], aggiunta.Testo);
    }

    [Fact]
    public void IlRecordNuovoSiModificaSubitoESiVedeUnaRigaSola()
    {
        var sessione = Apri();
        var file = sessione.File[Fix];

        _modifiche.AggiungiRecord(file, 3);
        int nuovo = _modifiche.UltimoAggiunto!.Value;
        var cambiata = _modifiche.Cambia(file, nuovo, "Name", "PROVA1");

        Assert.IsType<ModificaDiCampo>(cambiata);
        var diff = _modifiche.DiffDi(file);
        Assert.Equal(0, diff.Tolte);
        Assert.Equal(1, diff.Aggiunte);
        Assert.Contains("PROVA1", diff.Pezzi.Single().Righe.Single(r => r.Segno == SegnoDelDiff.Aggiunta).Testo, StringComparison.Ordinal);
    }

    [Fact]
    public void IlNUOVOHaUnElencoDiVerticiTUTTOSUO()
    {
        // 🔴 Se i due record si dividessero l'elenco, spostare un vertice del nuovo sposterebbe anche il vicino.
        var sessione = Apri();
        var file = sessione.File["SectorFiles/Include/IT/DYNAMIC_SEC/libb_es_ctr.tfl"];
        var delVicino = ElenchiDiVertici.Uno(file, 0, "Vertices")!.Elenco.Cast<object>().ToList();

        _modifiche.AggiungiRecord(file, 0);
        int nuovo = _modifiche.UltimoAggiunto!.Value;
        _modifiche.CambiaVertice(file, nuovo, "Vertices", 0, "N041.00.00.000 E012.00.00.000");

        Assert.Equal(delVicino, ElenchiDiVertici.Uno(file, 0, "Vertices")!.Elenco.Cast<object>());
        Assert.NotEqual(delVicino[0], ElenchiDiVertici.Uno(file, nuovo, "Vertices")!.Elenco[0]);
    }

    [Fact]
    public void UnRecordToltoEUNARIGAInMeno()
    {
        var sessione = Apri();
        var file = sessione.File[Fix];
        int quanti = file.Record;

        var fatta = _modifiche.TogliRecord(file, 2);

        Assert.IsType<ModificaDiStruttura>(fatta);
        Assert.Equal(quanti - 1, file.Record);
        var diff = _modifiche.DiffDi(file);
        Assert.Equal(1, diff.Tolte);
        Assert.Equal(0, diff.Aggiunte);
    }

    [Fact]
    public void TogliereUnRecordNonScompigliaLeModificheDegliAltri()
    {
        // 🔴 I numeri dei record scorrono: una modifica in sospeso su un record dopo quello tolto deve restare
        // SULLO STESSO record, non su quello che ne prende il posto.
        var sessione = Apri();
        var file = sessione.File[Fix];
        string nomeDelQuinto = Ispettore.Etichette(file, null)[5];
        _modifiche.Cambia(file, 5, "Name", "PROVA5");

        _modifiche.TogliRecord(file, 2);

        var diff = _modifiche.DiffDi(file);
        Assert.Equal(2, diff.Tolte);    // il record tolto, e la riga cambiata
        Assert.Equal(1, diff.Aggiunte);
        var modifica = Assert.IsType<ModificaDiCampo>(_modifiche.Tutte.First(m => m is ModificaDiCampo));
        Assert.Equal(4, modifica.Record);
        Assert.Equal(nomeDelQuinto, Ispettore.Etichette(file, null)[4].Replace("PROVA5", nomeDelQuinto, StringComparison.Ordinal));
    }

    [Fact]
    public void LeModificheDelRecordTOLTOSpariscono()
    {
        var sessione = Apri();
        var file = sessione.File[Fix];
        _modifiche.Cambia(file, 2, "Name", "PROVA2");

        _modifiche.TogliRecord(file, 2);

        Assert.DoesNotContain(_modifiche.Tutte, m => m is ModificaDiCampo);
        var diff = _modifiche.DiffDi(file);
        Assert.Equal(1, diff.Tolte);
        Assert.Equal(0, diff.Aggiunte);
    }

    [Fact]
    public void TogliereUnRecordNonPortaViaIlCommentoSopra()
    {
        // 🔴 Nel sector un commento prima di un record è quasi sempre l'intestazione di una SEZIONE, non la
        // descrizione di quella riga: portarselo via togliendo il primo record del blocco cancellerebbe
        // l'intestazione di tutti gli altri. L'ha mostrato la misura: in itgeo.geo un taglio da una riga ne faceva
        // sparire tre.
        _albero.Scrivi("SectorFiles/Include/IT/GEO/prova.geo", """
            //////COSTE//////
            N041.00.00.000;E012.00.00.000;N041.01.00.000;E012.00.00.000;COAST;
            N041.01.00.000;E012.00.00.000;N041.02.00.000;E012.00.00.000;COAST;

            """);
        var sessione = Apri();
        var file = sessione.File["SectorFiles/Include/IT/GEO/prova.geo"];

        _modifiche.TogliRecord(file, 0);

        var righe = Righe(file, _modifiche);
        Assert.Contains("//////COSTE//////", righe);
        var diff = _modifiche.DiffDi(file);
        Assert.Equal(1, diff.Tolte);
        Assert.Equal(0, diff.Aggiunte);
    }

    [Fact]
    public void LUltimoRecordDiUnFileNonSiToglie()
    {
        var sessione = Apri();
        var file = sessione.File[Fix];
        while (file.Record > 1)
            _modifiche.TogliRecord(file, 0);

        var esito = _modifiche.TogliRecord(file, 0);

        Assert.Contains("ultimo record", Assert.IsType<ModificaRifiutata>(esito).Motivo, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, file.Record);
    }

    [Fact]
    public void AnnullareLaStrutturaRimetteIlFileComEra()
    {
        var sessione = Apri();
        var file = sessione.File[Fix];
        var comErano = ((IFileConRecord)file).RigheDelFile([]).ToList();
        int quanti = file.Record;

        _modifiche.AggiungiRecord(file, 3);
        _modifiche.TogliRecord(file, 6);
        var struttura = _modifiche.Tutte.OfType<ModificaDiStruttura>().Single();
        Assert.Equal(1, struttura.Aggiunti);
        Assert.Equal(1, struttura.Tolti);

        Assert.True(_modifiche.Annulla(file, struttura));

        Assert.Equal(quanti, file.Record);
        Assert.False(_modifiche.CEQualcosa);
        Assert.Equal(comErano, Righe(file, _modifiche));
    }

    [Fact]
    public void AnnullareLaStrutturaAnnullaANCHELeModificheDiQuelFile()
    {
        // La struttura si annulla per ultima: i valori dei campi tornano quelli dell'apertura, poi tornano i record.
        var sessione = Apri();
        var file = sessione.File[Fix];
        var comErano = ((IFileConRecord)file).RigheDelFile([]).ToList();

        _modifiche.AggiungiRecord(file, 3);
        _modifiche.Cambia(file, 0, "Name", "PROVA0");
        var struttura = _modifiche.Tutte.OfType<ModificaDiStruttura>().Single();

        Assert.True(_modifiche.Annulla(file, struttura));

        Assert.False(_modifiche.CEQualcosa);
        Assert.Equal(comErano, Righe(file, _modifiche));
    }

    [Fact]
    public void AnnullaTuttoRimetteAncheIRecord()
    {
        var sessione = Apri();
        var file = sessione.File[Fix];
        var comErano = ((IFileConRecord)file).RigheDelFile([]).ToList();
        _modifiche.AggiungiRecord(file, 1);
        _modifiche.TogliRecord(file, 4);

        _modifiche.AnnullaTutto(f => sessione.File.GetValueOrDefault(f));

        Assert.False(_modifiche.CEQualcosa);
        Assert.Equal(comErano, Righe(file, _modifiche));
    }

    [Fact]
    public void UnFileCheIlMotoreNonInterpretaNonSiTocca()
    {
        _albero.Scrivi("SectorFiles/Include/IT/OTHER/prova.txt", "testo\r\n");
        var sessione = Apri();

        var esito = _modifiche.AggiungiRecord(sessione.File["SectorFiles/Include/IT/OTHER/prova.txt"], 0);

        Assert.IsType<ModificaRifiutata>(esito);
    }
}

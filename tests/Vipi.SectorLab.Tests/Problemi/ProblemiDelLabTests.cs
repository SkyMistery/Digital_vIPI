using Vipi.SectorLab.Core.Disco;
using Vipi.SectorLab.Core.Ispezione;
using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Core.Problemi;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Validazione;

namespace Vipi.SectorLab.Tests.Problemi;

/// <summary>
/// Il pannello dei problemi (carta F3 §2.2 passo 8, slice 10): i numeri del validatore dell'albero, agganciati ai file
/// e ai record della sessione, e quel che le modifiche in sospeso aggiungerebbero.
/// </summary>
public sealed class ProblemiDelLabTests : IDisposable
{
    private const string Fix = "SectorFiles/Include/IT/NAVAIDS/APT.fix";

    private readonly AlberoDiProva _albero = new();

    public void Dispose() => _albero.Dispose();

    private SessioneAperta Apri() => SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);

    [Fact]
    public void SonoIProblemiDelValidatore_TuttiAgganciatiAUnFileDellaSessione()
    {
        var sessione = Apri();

        var problemi = ProblemiDelLab.DellAlbero(sessione);

        var delValidatore = Validatore.ValidaLAlbero(sessione.Cartella.SectorFiles);
        Assert.NotEmpty(problemi);
        Assert.Equal(delValidatore.Count, problemi.Count);
        // 🔴 Il validatore scrive «Include\IT\…» relativo a SectorFiles, la sessione «SectorFiles/Include/IT/…».
        Assert.All(problemi, p => Assert.True(sessione.File.ContainsKey(p.File), p.File));
    }

    [Fact]
    public void IlRecordAgganciatoHaDavveroQuellaRiga()
    {
        var sessione = Apri();

        var conRecord = ProblemiDelLab.DellAlbero(sessione).Where(p => p.Record is not null).ToList();

        Assert.NotEmpty(conRecord);
        Assert.All(conRecord, p =>
        {
            var righe = ((IFileConRecord)sessione.File[p.File]).RigheDelRecord(p.Record!.Value, contesto: 0);
            Assert.Contains(righe, r => r.Numero == p.Problema.Riga && r.Testo == p.Problema.Testo);
        });
    }

    [Fact]
    public void UnaRigaCheIlLettoreNonCapisce_NonHaRecord_EsiVedeDalDisco()
    {
        // APT.fix:294 «MG763;N044.03.11.145;E008-11.31.443;3;» — la longitudine non si legge, la riga resta grezza.
        var sessione = Apri();

        var illeggibile = ProblemiDelLab.DellAlbero(sessione)
            .Single(p => p.File == Fix && p.Problema.Regola == Regola.CoordinataIllegibile);

        Assert.Null(illeggibile.Record);
        var righe = RigheDelDisco.Intorno(_albero.Percorso(Fix), illeggibile.Problema.Riga, contesto: 2);
        var segnata = Assert.Single(righe, r => r.DelRecord);
        Assert.Equal(illeggibile.Problema.Testo, segnata.Testo);
        Assert.Equal(5, righe.Count);
    }

    [Fact]
    public void RecordDellaRiga_ContaComeIlDisco_EUnCommentoNonEUnRecord()
    {
        var sessione = Apri();
        var file = (IFileConRecord)sessione.File[Fix];

        var delQuinto = file.RigheDelRecord(5, contesto: 0);

        Assert.All(delQuinto, r => Assert.Equal(5, file.RecordDellaRiga(r.Numero)));
        // Le prime righe di APT.fix sono l'intestazione «//////…FIX AEROPORTI…».
        Assert.Null(file.RecordDellaRiga(1));
        Assert.Null(file.RecordDellaRiga(0));
        Assert.Null(file.RecordDellaRiga(1_000_000));

        // Un commento IN TESTA a un record (sta nel suo chunk) non è una riga del record: in lirr_tma.lartcc le righe
        // 1 e 2 sono «//CNF1» e «//TW1», la 3 è il primo vertice del record 0.
        var tma = (IFileConRecord)sessione.File["SectorFiles/Include/IT/LOW_AIRSPACE/lirr_tma.lartcc"];
        Assert.Null(tma.RecordDellaRiga(1));
        Assert.Null(tma.RecordDellaRiga(2));
        Assert.Equal(0, tma.RecordDellaRiga(3));
    }

    [Fact]
    public void IlFiltroCercaFileRegolaERiga_ELeGravita()
    {
        var sessione = Apri();
        var problemi = ProblemiDelLab.DellAlbero(sessione);

        var delFix = ProblemiDelLab.Filtra(problemi, "apt.fix", errori: true, avvisi: true).ToList();
        var soloAvvisi = ProblemiDelLab.Filtra(problemi, null, errori: false, avvisi: true).ToList();
        var perTesto = ProblemiDelLab.Filtra(problemi, "MG763", errori: true, avvisi: true).ToList();

        Assert.NotEmpty(delFix);
        Assert.All(delFix, p => Assert.Equal(Fix, p.File));
        Assert.All(soloAvvisi, p => Assert.Equal(Gravita.Avviso, p.Gravita));
        Assert.Equal(problemi.Count(p => p.Gravita == Gravita.Avviso), soloAvvisi.Count);
        Assert.Contains(perTesto, p => p.Problema.Regola == Regola.CoordinataIllegibile);
    }

    [Fact]
    public void LeModificheInSospeso_DiconoIlProblemaCheAggiungerebbero_AgganciatoAlRecord()
    {
        var sessione = Apri();
        var modifiche = new ModificheInSospeso();
        Assert.IsType<ModificaDiCampo>(modifiche.Cambia(sessione.File[Fix], 3, "Name", "A;B"));

        var daProvare = ControlloDelleModifiche.Prepara(sessione, modifiche);
        var (nuovi, fusi) = ControlloDelleModifiche.Prova(sessione.Cartella, daProvare);
        var agganciati = ProblemiDelLab.Aggancia(sessione, nuovi);

        // La riga spezzata non si legge più: riletto, il file avrebbe un record in meno — e lo si dice.
        Assert.Equal(new RecordCheSiFondono(Fix, sessione.File[Fix].Record, sessione.File[Fix].Record - 1), Assert.Single(fusi));
        var nuovo = Assert.Single(agganciati, p => p.Gravita == Gravita.Errore);
        Assert.Equal(Fix, nuovo.File);
        Assert.Contains("A;B", nuovo.Problema.Testo, StringComparison.Ordinal);
        Assert.Equal(3, nuovo.Record);
    }

    [Fact]
    public void SenzaModifiche_NonCeNienteDaProvare()
    {
        var sessione = Apri();

        Assert.Empty(ControlloDelleModifiche.Prepara(sessione, new ModificheInSospeso()));
    }
}

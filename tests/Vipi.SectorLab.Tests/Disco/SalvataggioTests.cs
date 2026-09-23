using System.Text;
using Vipi.SectorLab.Core.Disco;
using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Validazione;

namespace Vipi.SectorLab.Tests.Disco;

/// <summary>
/// Il salvataggio (carta F3 §2.4, slice 9), un passo per volta: confini, conflitto, backup, scrittura atomica,
/// rilettura — e gli errori nuovi che chiedono conferma (§9.6). Al primo intoppo non si scrive niente.
/// </summary>
public sealed class SalvataggioTests : IDisposable
{
    private const string Fix = "SectorFiles/Include/IT/NAVAIDS/APT.fix";
    private const string Vor = "SectorFiles/Include/IT/NAVAIDS/itvor.vor";
    private static readonly DateTime Adesso = new(2026, 9, 23, 10, 30, 0);

    private readonly AlberoDiProva _albero = new();
    private readonly ModificheInSospeso _modifiche = new();
    private readonly string _backup;

    public SalvataggioTests()
        => _backup = Path.Combine(Path.GetTempPath(), "sectorlab-backup-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        _albero.Dispose();
        try
        {
            if (Directory.Exists(_backup))
                Directory.Delete(_backup, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private SessioneAperta Apri() => SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);

    private Salvataggio Salvataggio(SessioneAperta sessione, Action<string, byte[]>? scrivi = null)
        => new(sessione, _modifiche, _backup, scrivi, tentativi: 3, pausa: TimeSpan.Zero);

    private byte[] SulDisco(string relativo) => File.ReadAllBytes(_albero.Percorso(relativo));

    /// <summary>Sposta un fix in un punto qualunque: una modifica vera, una riga, senza problemi nuovi.</summary>
    private void SpostaUnFix(SessioneAperta sessione, int record = 0)
        => Assert.IsType<ModificaDiCampo>(
            _modifiche.Cambia(sessione.File[Fix], record, "Position", $"N041.00.0{record}.000 E012.00.00.000"));

    /// <summary>Cambia la sigla di un VOR: la seconda modifica, in un altro file.</summary>
    private void CambiaUnVor(SessioneAperta sessione)
        => Assert.IsType<ModificaDiCampo>(_modifiche.Cambia(sessione.File[Vor], 0, "Ident", "PRV"));

    // --- il giro buono ----------------------------------------------------------------------------------------

    [Fact]
    public void SiScriveQuelCheDiceIlDiff_ESiTornaPuliti()
    {
        var sessione = Apri();
        byte[] prima = SulDisco(Fix);
        SpostaUnFix(sessione);
        byte[] attesi = ((IFileConRecord)sessione.File[Fix]).ByteDelFile(_modifiche.SporchiDi(Fix));
        var diff = _modifiche.DiffDi(sessione.File[Fix]);

        var esito = Salvataggio(sessione).Salva(confermato: false, Adesso);

        Assert.Equal(StatoDelSalvataggio.Salvato, esito.Stato);
        Assert.Equal([Fix], esito.Salvati);
        Assert.Equal(attesi, SulDisco(Fix));
        Assert.Equal(1, diff.Tolte);
        Assert.Equal(1, diff.Aggiunte);
        // Una riga tolta e una aggiunta: il resto del file è identico byte per byte.
        var righePrima = Encoding.UTF8.GetString(prima).Split("\r\n");
        var righeDopo = Encoding.UTF8.GetString(SulDisco(Fix)).Split("\r\n");
        Assert.Equal(righePrima.Length, righeDopo.Length);
        Assert.Single(righePrima.Zip(righeDopo), r => r.First != r.Second);
        Assert.False(_modifiche.CEQualcosa);
        Assert.False(File.Exists(_albero.Percorso(Fix) + ".tmp"));
    }

    [Fact]
    public void DopoIlSalvataggio_LImprontaNuovaEQuellaDiRiferimento_ESiRisalva()
    {
        // 🔴 Senza la rilettura il secondo salvataggio vedrebbe un «conflitto» col file che ha appena scritto lui.
        var sessione = Apri();
        var vecchia = sessione.File[Fix].Impronta;
        SpostaUnFix(sessione);
        Salvataggio(sessione).Salva(false, Adesso);

        Assert.NotEqual(vecchia, sessione.File[Fix].Impronta);
        Assert.Equal(Impronta.Di(SulDisco(Fix)), sessione.File[Fix].Impronta);
        Assert.Empty(sessione.CambiatiSulDisco());

        SpostaUnFix(sessione, record: 1);
        var secondo = Salvataggio(sessione).Salva(false, Adesso.AddMinutes(1));

        Assert.Equal(StatoDelSalvataggio.Salvato, secondo.Stato);
    }

    [Fact]
    public void IlBackupHaIByteDiPrima_FuoriDalClone_UnaCartellaPerSalvataggio()
    {
        var sessione = Apri();
        byte[] fixPrima = SulDisco(Fix);
        byte[] vorPrima = SulDisco(Vor);
        SpostaUnFix(sessione);
        CambiaUnVor(sessione);

        var esito = Salvataggio(sessione).Salva(false, Adesso);

        Assert.Equal(Path.Combine(_backup, "20260923-103000"), esito.CartellaDelBackup);
        Assert.Equal(fixPrima, File.ReadAllBytes(Path.Combine(esito.CartellaDelBackup!, "SectorFiles", "Include", "IT", "NAVAIDS", "APT.fix")));
        Assert.Equal(vorPrima, File.ReadAllBytes(Path.Combine(esito.CartellaDelBackup!, "SectorFiles", "Include", "IT", "NAVAIDS", "itvor.vor")));
        Assert.False(esito.CartellaDelBackup!.StartsWith(_albero.Radice, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DueSalvataggiNelloStessoSecondo_NonSiMescolano()
    {
        var sessione = Apri();
        SpostaUnFix(sessione);
        var primo = Salvataggio(sessione).Salva(false, Adesso);
        SpostaUnFix(sessione, record: 1);
        var secondo = Salvataggio(sessione).Salva(false, Adesso);

        Assert.NotEqual(primo.CartellaDelBackup, secondo.CartellaDelBackup);
        Assert.EndsWith("-2", secondo.CartellaDelBackup, StringComparison.Ordinal);
    }

    [Fact]
    public void IBackupPiuVecchiDiTrentaGiorniSiTolgono_GliAltriNo()
    {
        Directory.CreateDirectory(Path.Combine(_backup, "20260801-090000"));   // 53 giorni
        Directory.CreateDirectory(Path.Combine(_backup, "20260901-090000"));   // 22 giorni
        Directory.CreateDirectory(Path.Combine(_backup, "non-e-un-backup"));
        var sessione = Apri();
        SpostaUnFix(sessione);

        Salvataggio(sessione).Salva(false, Adesso);

        Assert.False(Directory.Exists(Path.Combine(_backup, "20260801-090000")));
        Assert.True(Directory.Exists(Path.Combine(_backup, "20260901-090000")));
        Assert.True(Directory.Exists(Path.Combine(_backup, "non-e-un-backup")));
    }

    [Fact]
    public void SenzaModifiche_NonSiFaNiente()
    {
        var esito = Salvataggio(Apri()).Salva(false, Adesso);

        Assert.Equal(StatoDelSalvataggio.NienteDaSalvare, esito.Stato);
        Assert.False(Directory.Exists(_backup));
    }

    // --- passo 2: il conflitto --------------------------------------------------------------------------------

    [Fact]
    public void UnFileCambiatoSottoIPiedi_NonSiScrive_ENemmenoGliAltri()
    {
        var sessione = Apri();
        SpostaUnFix(sessione);
        CambiaUnVor(sessione);
        byte[] fixPrima = SulDisco(Fix);
        // Un git pull dopo l'apertura: il .vor sul disco non è più quello letto.
        File.AppendAllText(_albero.Percorso(Vor), "\r\n// da un collega\r\n");
        byte[] vorDelCollega = SulDisco(Vor);

        var esito = Salvataggio(sessione).Salva(confermato: true, Adesso);

        Assert.Equal(StatoDelSalvataggio.Fermo, esito.Stato);
        Assert.Equal([Vor], esito.Controllo.Conflitti);
        // Nemmeno il .fix, che non aveva conflitti: il controllo si fa su TUTTI prima di scriverne uno.
        Assert.Equal(fixPrima, SulDisco(Fix));
        Assert.Equal(vorDelCollega, SulDisco(Vor));
        Assert.False(Directory.Exists(_backup));
        Assert.Equal(2, _modifiche.FileToccati.Count);
    }

    [Fact]
    public void UnFileSparito_EUnConflitto()
    {
        var sessione = Apri();
        SpostaUnFix(sessione);
        File.Delete(_albero.Percorso(Fix));

        var esito = Salvataggio(sessione).Salva(true, Adesso);

        Assert.Equal(StatoDelSalvataggio.Fermo, esito.Stato);
        Assert.Equal([Fix], esito.Controllo.Conflitti);
        Assert.False(File.Exists(_albero.Percorso(Fix)));
    }

    [Fact]
    public void DopoIlConflitto_SiRileggeIlFile_ELeSueModificheSiLascianoAndare()
    {
        var sessione = Apri();
        SpostaUnFix(sessione);
        File.AppendAllText(_albero.Percorso(Fix), "\r\n// da un collega\r\n");

        sessione.Rileggi(Fix);
        _modifiche.Dimentica(Fix);

        Assert.Empty(sessione.CambiatiSulDisco());
        Assert.False(_modifiche.CEQualcosa);
        Assert.Equal(StatoDelSalvataggio.NienteDaSalvare, Salvataggio(sessione).Salva(false, Adesso).Stato);
    }

    // --- errori nuovi: conferma, non blocco (§9.6) ------------------------------------------------------------

    [Fact]
    public void UnErroreNuovo_ChiedeConferma_ESenzaNonSiScrive()
    {
        var sessione = Apri();
        byte[] prima = SulDisco(Fix);
        // Un «;» scritto nel nome di un fix spezza la riga in un campo in più: la latitudine diventa «B», e il
        // validatore lo dice. Il Lab non lo vieta (è un testo), ma al salvataggio lo fa vedere.
        Assert.IsType<ModificaDiCampo>(_modifiche.Cambia(sessione.File[Fix], 0, "Name", "A;B"));

        var controllo = Salvataggio(sessione).Controlla();
        var esito = Salvataggio(sessione).Salva(confermato: false, Adesso);

        Assert.True(controllo.ChiedeConferma);
        Assert.All(controllo.ErroriNuovi, p => Assert.Equal(Fix, p.File));
        Assert.Equal(StatoDelSalvataggio.DaConfermare, esito.Stato);
        Assert.Equal(prima, SulDisco(Fix));
        Assert.False(Directory.Exists(_backup));

        var confermato = Salvataggio(sessione).Salva(confermato: true, Adesso);

        Assert.Equal(StatoDelSalvataggio.Salvato, confermato.Stato);
        Assert.True(confermato.ProblemiDopo >= controllo.ErroriNuovi.Count);
    }

    [Fact]
    public void GliErroriCheCeranoGia_NonSonoNuovi_AncheSeScorronoDiRiga()
    {
        // Un record aggiunto IN TESTA fa scorrere i numeri di riga di tutto il file: gli errori vecchi restano vecchi.
        var sessione = Apri();
        var file = sessione.File[Vor];
        int primaDiTutto = Validatore.ValidaIlFile(_albero.Percorso(Vor), Vor).Count;
        Assert.IsType<ModificaDiStruttura>(_modifiche.AggiungiRecord(file, 0));
        Assert.IsType<ModificaDiCampo>(_modifiche.Cambia(file, 1, "Ident", "NUO"));

        var controllo = Salvataggio(sessione).Controlla();

        Assert.True(primaDiTutto > 0, "il campione deve avere errori suoi, se no la prova non distingue");
        Assert.Empty(controllo.ErroriNuovi);
    }

    [Fact]
    public void UnaRottaCopiataColSuoNumero_RilettaSiFondeColVicino_EChiedeConferma()
    {
        // Nei .vrt le rotte stanno accostate e le separa il NUMERO: la copia del vicino, finché ha il suo numero,
        // riletta dal disco è un pezzo di lui. Il salvataggio lo dice e chiede conferma.
        const string vrt = "SectorFiles/Include/IT/libv.vrt";
        var sessione = Apri();
        var file = sessione.File[vrt];
        int quanti = file.Record;
        Assert.IsType<ModificaDiStruttura>(_modifiche.AggiungiRecord(file, 0));

        var controllo = Salvataggio(sessione).Controlla();

        var fuso = Assert.Single(controllo.RecordCheSiFondono);
        Assert.Equal(new RecordCheSiFondono(vrt, quanti + 1, quanti), fuso);
        Assert.True(controllo.ChiedeConferma);
        Assert.Equal(StatoDelSalvataggio.DaConfermare, Salvataggio(sessione).Salva(false, Adesso).Stato);
    }

    [Fact]
    public void CambiatoIlNumeroDellaRottaCopiata_NonSiFondePiu()
    {
        const string vrt = "SectorFiles/Include/IT/libv.vrt";
        var sessione = Apri();
        var file = sessione.File[vrt];
        Assert.IsType<ModificaDiStruttura>(_modifiche.AggiungiRecord(file, 0));
        Assert.IsType<ModificaDiCampo>(_modifiche.Cambia(file, _modifiche.UltimoAggiunto!.Value, "Numero", "99"));

        var controllo = Salvataggio(sessione).Controlla();

        Assert.Empty(controllo.RecordCheSiFondono);
        Assert.False(controllo.ChiedeConferma);
    }

    [Fact]
    public void UnBloccoCopiatoInUnLartcc_PrendeLaRigaVuotaDeiVicini_ERilettoEUnRecordInPiu()
    {
        // Nei .lartcc un record è un BLOCCO chiuso dalla riga vuota, e i blocchi non stanno mai accostati: la copia
        // prende la riga vuota che separa anche gli altri (misurato sull'albero vero: senza, 63 file su 695 perdevano
        // il record nuovo alla rilettura).
        const string tma = "SectorFiles/Include/IT/LOW_AIRSPACE/lirr_tma.lartcc";
        var sessione = Apri();
        var file = sessione.File[tma];
        int quanti = file.Record;
        Assert.IsType<ModificaDiStruttura>(_modifiche.AggiungiRecord(file, 0));

        var esito = Salvataggio(sessione).Salva(false, Adesso);

        Assert.Equal(StatoDelSalvataggio.Salvato, esito.Stato);
        Assert.Empty(esito.Controllo.RecordCheSiFondono);
        Assert.Equal(quanti + 1, RiletturaDiProva.QuantiRecord(_albero.Percorso(tma)));
        Assert.Equal(quanti + 1, sessione.File[tma].Record);
    }

    [Fact]
    public void UnMvaDiRotta_SiProvaColSuoLettore_ENonConIlLettoreDegliAeroporti()
    {
        // 🔴 Il motore sceglie il lettore anche dalla cartella (/ENRMVA/): la copia di prova col solo nome del file
        // leggeva un .mva di rotta come quello di un aeroporto, e dava problemi «nuovi» che nuovi non erano.
        const string mva = "SectorFiles/Include/IT/ENRMVA/lirr.mva";
        var sessione = Apri();
        Assert.IsType<ModificaDiStruttura>(_modifiche.AggiungiRecord(sessione.File[mva], 0));

        var controllo = Salvataggio(sessione).Controlla();

        Assert.Empty(controllo.ProblemiNuovi);
        Assert.Empty(controllo.RecordCheSiFondono);
        // La copia di prova si legge col lettore di ROTTA: l'etichetta è il campo 5 della riga L (100), non il campo 2
        // (LIRR) come negli .mva degli aeroporti.
        string? etichetta = RiletturaDiProva.Con(mva, SulDisco(mva), copia =>
            Formati.Usa(copia, new RaccoltaDiAvvisi(), new PrimaEtichettaMva(copia), out string? letta) ? letta : null);
        Assert.Equal("100", etichetta);
    }

    private sealed class PrimaEtichettaMva(string percorso) : IUsoDelFormato<string?>
    {
        public string? Usa<T>(IFileParser<T> lettore, IFileSaver<T> scrittore)
            where T : class
            => (lettore.Parse(percorso, new Vipi.Sectorfile.Shared.ColorPalette()).Records.FirstOrDefault()
                as Vipi.Sectorfile.Models.MvaSector)?.AltLabel;
    }

    // --- passo 4: la scrittura, e chi tiene il file --------------------------------------------------------------

    [Fact]
    public void UnFileTenutoPerUnAttimo_SiRiprova()
    {
        var sessione = Apri();
        SpostaUnFix(sessione);
        int giri = 0;
        void Scrivi(string percorso, byte[] byteDelFile)
        {
            if (++giri < 3)
                throw new IOException("The process cannot access the file because it is being used by another process.");
            FileSaverOrchestrator.ScriviAtomico(percorso, byteDelFile);
        }

        var esito = Salvataggio(sessione, Scrivi).Salva(false, Adesso);

        Assert.Equal(StatoDelSalvataggio.Salvato, esito.Stato);
        Assert.Equal(3, giri);
    }

    [Fact]
    public void UnFileTenutoSempre_SiFerma_DicendoQuale_ESulDiscoRestaQuelloDiPrima()
    {
        var sessione = Apri();
        SpostaUnFix(sessione);
        byte[] prima = SulDisco(Fix);

        var esito = Salvataggio(sessione, (_, _) => throw new IOException("in uso")).Salva(false, Adesso);

        Assert.Equal(StatoDelSalvataggio.Interrotto, esito.Stato);
        Assert.Contains(Fix, esito.Errore, StringComparison.Ordinal);
        Assert.Contains("3 tentativi", esito.Errore, StringComparison.Ordinal);
        Assert.Equal([Fix], esito.NonSalvati);
        Assert.Equal(prima, SulDisco(Fix));
        // Le modifiche restano in sospeso: non si perde niente, si riprova quando Aurora lascia il file.
        Assert.True(_modifiche.CEQualcosa);
    }

    [Fact]
    public void UnGuastoAMeta_IFilePrimaRestanoSalvati_GliAltriInSospeso()
    {
        var sessione = Apri();
        SpostaUnFix(sessione);
        CambiaUnVor(sessione);
        void Scrivi(string percorso, byte[] byteDelFile)
        {
            if (percorso.EndsWith("itvor.vor", StringComparison.Ordinal))
                throw new UnauthorizedAccessException("negato");
            FileSaverOrchestrator.ScriviAtomico(percorso, byteDelFile);
        }

        var esito = Salvataggio(sessione, Scrivi).Salva(false, Adesso);

        Assert.Equal(StatoDelSalvataggio.Interrotto, esito.Stato);
        Assert.Equal([Fix], esito.Salvati);
        Assert.Equal([Vor], esito.NonSalvati);
        Assert.Equal([Vor], _modifiche.FileToccati);
    }

    // --- passo 5: la rilettura --------------------------------------------------------------------------------

    [Fact]
    public void SeSulDiscoNonCeQuelCheSiEScritto_SiDice()
    {
        var sessione = Apri();
        SpostaUnFix(sessione);

        // Uno scrittore che scrive ALTRO (o qualcuno che riscrive il file subito dopo).
        var esito = Salvataggio(sessione, (percorso, _) => File.WriteAllText(percorso, "altro")).Salva(false, Adesso);

        Assert.Equal(StatoDelSalvataggio.Interrotto, esito.Stato);
        Assert.Contains("riletto", esito.Errore, StringComparison.Ordinal);
        Assert.NotNull(esito.CartellaDelBackup);
        Assert.True(_modifiche.CEQualcosa);
    }
}

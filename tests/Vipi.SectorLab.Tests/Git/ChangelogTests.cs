using Vipi.SectorLab.Core.Git;

namespace Vipi.SectorLab.Tests.Git;

/// <summary>
/// Carta F4, slice 6 («file per file» C1, C2, C6): le righe del changelog proposte dal ramo, scritte nella sezione
/// della loro FIR (col <c>*NIL</c> tolto), e i controlli del file.
/// </summary>
public sealed class ChangelogTests : IDisposable
{
    // Il changelog del 17 ottobre (A2610), accorciato: la forma è quella vera.
    private static readonly string[] DiOggi =
    [
        "**AIRAC A2610 IN VIGORE DAL 01/10/2026", "", "#############################", "######## NOVITA' #############",
        "#############################", "", "**Generale", "*nuovi FIX:", "-NANKA", "", "**LIBB Brindisi", "*NIL", "",
        "**LIMM Milano", "*NIL", "", "**LIPP Padova", "*LIPY: Implementazione SID MASE5L/8J", "", "**LIRR Roma",
        "*LIRL: Aggiunte le SID ", "", "",
    ];

    private readonly RepoDiProva _repo = new();

    public void Dispose() => _repo.Dispose();

    [Fact]
    public void LIntestazioneDiceCicloEData()
    {
        Assert.Equal(("2610", new DateOnly(2026, 10, 1)), Changelog.Intestazione(DiOggi));
        Assert.Null(Changelog.Intestazione(["# changelog"]));
    }

    [Fact]
    public void UnaRigaNuovaVaInCodaAllaSuaSezione_ENilSeNeVa()
    {
        var dopo = Changelog.Aggiungi(DiOggi, [("LIMM", "LIMJ: revisione minime"), ("LIRR", "*LIRF: aggiunte le SID EKLO8R"),
            ("LIRR", "LIRL: Aggiunte le SID"), ("LIXX", "niente")], out var scartate);

        Assert.Equal(["**LIMM Milano", "*LIMJ: revisione minime", ""], dopo[13..16]);
        Assert.Equal(["**LIRR Roma", "*LIRL: Aggiunte le SID ", "*LIRF: aggiunte le SID EKLO8R", "", ""], dopo[19..]);
        Assert.Equal(["LIXX: niente"], scartate);
        // Il resto del file è com'era.
        Assert.Equal(DiOggi[..13], dopo[..13]);
        Assert.Equal(DiOggi[15..19], dopo[15..19]);
    }

    [Fact]
    public void IControlliDelFile()
    {
        Assert.Empty(Changelog.Controlla(DiOggi, "2610"));
        Assert.Contains(Changelog.Controlla(DiOggi, "2611"), a => a.Testo.Contains("A2610", StringComparison.Ordinal) && a.Riga == 1);

        string[] rotto = [.. DiOggi[..10], "**LIBB Brindisi", "*NIL", "*LIBD: nuova", "", .. DiOggi[13..16], .. DiOggi[19..]];
        var avvisi = Changelog.Controlla(rotto, "2610");
        Assert.Contains(avvisi, a => a.Testo.Contains("*NIL accanto", StringComparison.Ordinal) && a.Riga == 12);
        Assert.Contains(avvisi, a => a.Testo.Contains("Manca la sezione «**LIPP Padova»", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("SectorFiles/Include/IT/lirl.sid", "LIRR")]
    [InlineData("SectorFiles/Include/IT/OTHER/lirr.frq", "LIRR")]
    [InlineData("SectorFiles/Include/IT/AIRWAY/itawlow.lairway", "Generale")]
    [InlineData("SectorFiles/Include/IT/lirq.str", "LIMM,LIPP,LIRR")]
    public void LaSezioneDelFile(string percorso, string attese)
    {
        _repo.Scrivi("SectorFiles/Include/IT/OTHER/lirr.ap", "LIRL;0;6000;N041.00.00.000;E012.00.00.000;LATINA;\r\nLIRQ;142;6000;N043.48.31.000;E011.12.10.000;PERETOLA;\r\n");
        _repo.Scrivi("SectorFiles/Include/IT/OTHER/limm.ap", "LIRQ;142;6000;N043.48.31.000;E011.12.10.000;PERETOLA;\r\n");
        _repo.Scrivi("SectorFiles/Include/IT/OTHER/lipp.ap", "LIRQ;142;6000;N043.48.31.000;E011.12.10.000;PERETOLA;\r\n");

        var fir = RigheProposte.FirDegliScali(_repo.Lavoro);

        Assert.Equal(attese.Split(','), RigheProposte.SezioniDelFile(percorso, fir));
    }

    [Fact]
    public void CosaECambiatoDaiRecord()
    {
        const string Prima = "ABBOZ;N046.02.37.000;E011.07.48.000;0;\r\nADOSA;N045.00.00.000;E010.00.00.000;0;\r\nXENOS;N044.00.00.000;E010.00.00.000;0;\r\n";
        const string Dopo = "ABBOZ;N046.02.38.000;E011.07.48.000;0;\r\nXENOS;N044.00.00.000;E010.00.00.000;0;\r\nNANKA;N043.00.00.000;E010.00.00.000;0;\r\n";

        Assert.Equal("aggiunto Fix NANKA; tolto Fix ADOSA; modificato Fix ABBOZ",
            RigheProposte.CosaECambiato("SectorFiles/Include/IT/NAVAIDS/APT.fix", Prima, Dopo));
        Assert.Equal("tolto il file APT.fix", RigheProposte.CosaECambiato("SectorFiles/Include/IT/NAVAIDS/APT.fix", Prima, null));
        // Slice 8, trovati coi lavori R-10 sul fork: un nome corretto, e un file di un record solo.
        Assert.Equal("corretti Punto VFR SESTO AL REGHENA (era PASE1), CAORLE (era CAORLE - PHE2)",
            RigheProposte.CosaECambiato("SectorFiles/Include/IT/lipa.vfi",
                "PASE1;SESTO AL REGHENA;N0455135000;E0124841000;\r\nCAORLE - PHE2;;N0453600000;E0125300000;\r\n",
                "SESTO AL REGHENA;PASE1;N0455135000;E0124841000;\r\nCAORLE;PHE2;N0453600000;E0125300000;\r\n"));
        Assert.Equal("modificato default.atis", RigheProposte.CosaECambiato("SectorFiles/Include/IT/default.atis", "Runway [ARR]]].\r\n", "Runway [ARR]].\r\n"));
        Assert.Equal("aggiornato note.txt", RigheProposte.CosaECambiato("SectorFiles/Include/IT/note.txt", "a", "b"));
    }

    [Fact]
    public async Task LaProposta_DalleRegistrazioniDelRamo()
    {
        _repo.Scrivi("SectorFiles/Include/IT/OTHER/lirr.ap", "LIRL;0;6000;N041.00.00.000;E012.00.00.000;LATINA;\r\n");
        _repo.Scrivi("SectorFiles/Include/IT/lirl.fix", "LATNA;N041.30.00.000;E012.50.00.000;0;\r\n");
        _repo.Esegui(_repo.Lavoro, "add", "--all");
        _repo.Esegui(_repo.Lavoro, "commit", "--quiet", "-m", "base");
        _repo.Esegui(_repo.Lavoro, "push", "--quiet");
        Assert.Null(await RamiDelSector.NuovoLavoroAsync(_repo.Git, _repo.Lavoro, "aod/704798/latina", "master"));
        _repo.Scrivi("SectorFiles/Include/IT/lirl.fix", "LATNA;N041.30.00.000;E012.50.00.000;0;\r\nLT001;N041.31.00.000;E012.51.00.000;0;\r\n");
        _repo.Scrivi(RepoDiProva.FileDiProva, "ABBOZ;N046.02.38.000;E011.07.48.000;0;\r\n");
        _repo.Esegui(_repo.Lavoro, "commit", "--quiet", "-am", "lavoro");

        var righe = await RigheProposte.ProponiAsync(_repo.Git, _repo.Lavoro, "master", "aod/704798/latina");

        Assert.Equal([("LIRR", "LIRL: aggiunto Fix LT001"), ("Generale", "modificato Fix ABBOZ")],
            righe.Select(r => (r.Sezione, r.Testo)).OrderByDescending(r => r.Sezione, StringComparer.Ordinal));
    }
}

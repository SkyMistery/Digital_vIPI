using Vipi.SectorLab.Core.Git;

namespace Vipi.SectorLab.Tests.Git;

/// <summary>
/// Carta F4, slice 7: il riporto di <c>master</c> nel ramo del ciclo (con un conflitto mostrato e risolto dall'AOD), la
/// fine del ciclo nel changelog (C3), le cancellazioni in <c>delete.upd</c> e negli <c>.isc</c> (C4).
/// </summary>
public sealed class RiportoEFineCicloTests : IDisposable
{
    private const string Ciclo = "airac/2611";

    private readonly RepoDiProva _repo = new();

    public void Dispose() => _repo.Dispose();

    private async Task<StatoDelRamo> Stato() => (await RepoDelSector.LeggiAsync(_repo.Git, _repo.Lavoro)).Stato!;

    private string File_(string relativo) => File.ReadAllText(Path.Combine(_repo.Lavoro, relativo.Replace('/', Path.DirectorySeparatorChar)));

    /// <summary>Il ciclo nasce da master; poi master cambia il file di prova (un aggiornamento immediato).</summary>
    private async Task MasterAvanti(string coordinate = "N046.02.50.000")
    {
        Assert.Null(await RamiDelSector.FaiNascereIlCicloAsync(_repo.Git, _repo.Lavoro, Ciclo));
        string altro = _repo.UnAltroClone();
        _repo.Scrivi(RepoDiProva.FileDiProva, $"ABBOZ;{coordinate};E011.07.48.000;0;\r\n", altro);
        _repo.Esegui(altro, "commit", "--quiet", "-am", "immediato su master");
        _repo.Esegui(altro, "push", "--quiet");
    }

    [Fact]
    public async Task UnRiportoLiscio_EPoiNienteDaRiportare()
    {
        await MasterAvanti();

        Assert.Null(await Riporto.CominciaAsync(_repo.Git, _repo.Lavoro, "704798", "master", Ciclo));

        var stato = await Stato();
        Assert.Equal("aod/704798/riporto-2611", stato.Ramo);
        Assert.Contains("N046.02.50.000", File_(RepoDiProva.FileDiProva), StringComparison.Ordinal);
        Assert.Equal(Ciclo, await RamiDelSector.BaseDiAsync(_repo.Git, _repo.Lavoro, stato.Ramo!));
        Assert.Null(Invio.PercheNonSiManda(stato));   // da mandare, poi la PR verso il ciclo

        // Mandato e fuso nel ciclo, non c'è più niente da riportare.
        _repo.Esegui(_repo.Lavoro, "push", "--quiet", "origin", $"HEAD:{Ciclo}");
        Assert.Contains("Niente da riportare", await Riporto.CominciaAsync(_repo.Git, _repo.Lavoro, "704798", "master", Ciclo), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(SceltaDelConflitto.TieniIlCiclo, "N046.02.60.000")]
    [InlineData(SceltaDelConflitto.PrendiMaster, "N046.02.50.000")]
    public async Task UnConflittoSiMostra_ESiRisolveComeSceglieLAod(SceltaDelConflitto scelta, string resta)
    {
        await MasterAvanti();
        // Nel ciclo, intanto, la stessa riga è cambiata in un altro modo.
        Assert.Null(await RamiDelSector.NuovoLavoroAsync(_repo.Git, _repo.Lavoro, "aod/704798/nel-ciclo", Ciclo));
        _repo.Scrivi(RepoDiProva.FileDiProva, "ABBOZ;N046.02.60.000;E011.07.48.000;0;\r\n");
        _repo.Esegui(_repo.Lavoro, "commit", "--quiet", "-am", "nel ciclo");
        _repo.Esegui(_repo.Lavoro, "push", "--quiet", "origin", $"HEAD:{Ciclo}");

        Assert.Equal("conflitti", await Riporto.CominciaAsync(_repo.Git, _repo.Lavoro, "704798", "master", Ciclo));

        Assert.True(await Riporto.InCorsoAsync(_repo.Git, _repo.Lavoro));
        Assert.Equal([(RepoDiProva.FileDiProva, "in conflitto")], (await Stato()).Cambiati.Select(c => (c.Percorso, c.Come)));
        Assert.Contains("ancora in conflitto", await Riporto.ConcludiAsync(_repo.Git, await Stato()), StringComparison.Ordinal);

        Assert.Null(await Riporto.RisolviAsync(_repo.Git, _repo.Lavoro, RepoDiProva.FileDiProva, scelta));
        Assert.Null(await Riporto.ConcludiAsync(_repo.Git, await Stato()));

        Assert.False(await Riporto.InCorsoAsync(_repo.Git, _repo.Lavoro));
        Assert.Contains(resta, File_(RepoDiProva.FileDiProva), StringComparison.Ordinal);
        Assert.Empty((await Stato()).Cambiati);
    }

    [Fact]
    public async Task SistematoAMano_SoloSenzaSegniDiConflitto_EUnRiportoSiAnnulla()
    {
        await MasterAvanti();
        Assert.Null(await RamiDelSector.NuovoLavoroAsync(_repo.Git, _repo.Lavoro, "aod/704798/nel-ciclo", Ciclo));
        _repo.Scrivi(RepoDiProva.FileDiProva, "ABBOZ;N046.02.60.000;E011.07.48.000;0;\r\n");
        _repo.Esegui(_repo.Lavoro, "commit", "--quiet", "-am", "nel ciclo");
        _repo.Esegui(_repo.Lavoro, "push", "--quiet", "origin", $"HEAD:{Ciclo}");
        Assert.Equal("conflitti", await Riporto.CominciaAsync(_repo.Git, _repo.Lavoro, "704798", "master", Ciclo));

        Assert.Contains("segni del conflitto", await Riporto.RisolviAsync(_repo.Git, _repo.Lavoro, RepoDiProva.FileDiProva, SceltaDelConflitto.SistematoAMano), StringComparison.Ordinal);
        _repo.Scrivi(RepoDiProva.FileDiProva, "ABBOZ;N046.02.55.000;E011.07.48.000;0;\r\n");
        Assert.Null(await Riporto.RisolviAsync(_repo.Git, _repo.Lavoro, RepoDiProva.FileDiProva, SceltaDelConflitto.SistematoAMano));

        Assert.Null(await Riporto.AnnullaAsync(_repo.Git, _repo.Lavoro));

        Assert.False(await Riporto.InCorsoAsync(_repo.Git, _repo.Lavoro));
        Assert.Contains("N046.02.60.000", File_(RepoDiProva.FileDiProva), StringComparison.Ordinal);   // com'è nel ciclo, prima del riporto
        Assert.Empty((await Stato()).Cambiati);
    }

    [Fact]
    public async Task UnLavoroSiAllineaAMaster_ColConflittoSistematoAMano()
    {
        // Slice 8 (trovato coi lavori R-10): due lavori aggiungono una riga nello stesso punto; il primo è fuso in master.
        Assert.Null(await RamiDelSector.NuovoLavoroAsync(_repo.Git, _repo.Lavoro, "aod/704798/c", "master"));
        _repo.Scrivi(RepoDiProva.FileDiProva, "ABBOZ;N046.02.37.000;E011.07.48.000;0;\r\nCCCCC;N046.00.00.000;E011.00.00.000;0;\r\n");
        _repo.Esegui(_repo.Lavoro, "commit", "--quiet", "-am", "c");
        Assert.Null(await RamiDelSector.NuovoLavoroAsync(_repo.Git, _repo.Lavoro, "aod/704798/a", "master"));
        _repo.Scrivi(RepoDiProva.FileDiProva, "ABBOZ;N046.02.37.000;E011.07.48.000;0;\r\nAAAAA;N046.00.00.000;E011.00.00.000;0;\r\n");
        _repo.Esegui(_repo.Lavoro, "commit", "--quiet", "-am", "a");
        _repo.Esegui(_repo.Lavoro, "push", "--quiet", "origin", "HEAD:master");   // la PR di a, fusa
        Assert.Null(await RamiDelSector.PassaAsync(_repo.Git, _repo.Lavoro, "aod/704798/c"));
        _repo.Esegui(_repo.Lavoro, "fetch", "--quiet");
        Assert.Equal(1, await Riporto.IndietroRispettoAsync(_repo.Git, _repo.Lavoro, "master"));

        Assert.Equal("conflitti", await Riporto.AllineaAsync(_repo.Git, _repo.Lavoro, "master"));

        _repo.Scrivi(RepoDiProva.FileDiProva, "ABBOZ;N046.02.37.000;E011.07.48.000;0;\r\nAAAAA;N046.00.00.000;E011.00.00.000;0;\r\nCCCCC;N046.00.00.000;E011.00.00.000;0;\r\n");
        Assert.Null(await Riporto.RisolviAsync(_repo.Git, _repo.Lavoro, RepoDiProva.FileDiProva, SceltaDelConflitto.SistematoAMano));
        Assert.Null(await Riporto.ConcludiAsync(_repo.Git, await Stato()));

        Assert.Equal(0, await Riporto.IndietroRispettoAsync(_repo.Git, _repo.Lavoro, "master"));
        Assert.Contains("AAAAA", File_(RepoDiProva.FileDiProva), StringComparison.Ordinal);
        Assert.Contains("CCCCC", File_(RepoDiProva.FileDiProva), StringComparison.Ordinal);
        Assert.Contains("Niente da allineare", await Riporto.AllineaAsync(_repo.Git, _repo.Lavoro, "master"), StringComparison.Ordinal);
    }

    [Fact]
    public void LaFineDelCiclo_ArchiviaEApreIlNuovo()
    {
        string vecchio = "**AIRAC A2610 IN VIGORE DAL 01/10/2026\r\n\r\n####\r\n\r\n**Generale\r\n*nuovi FIX:\r\n-NANKA\r\n\r\n**LIBB Brindisi\r\n*NIL\r\n\r\n**LIMM Milano\r\n*NIL\r\n\r\n**LIPP Padova\r\n*NIL\r\n\r\n**LIRR Roma\r\n*LIRL: SID\r\n";
        _repo.Scrivi(Changelog.Percorso, vecchio);

        var scritti = FineDelCiclo.Apri(_repo.Lavoro, "2611", new DateOnly(2026, 10, 29), out string? perche);

        Assert.Null(perche);
        Assert.Equal(["SectorFiles/Include/IT/CHANGELOG/2610.txt", Changelog.Percorso], scritti);
        Assert.Equal(vecchio, File_("SectorFiles/Include/IT/CHANGELOG/2610.txt"));
        string nuovo = File_(Changelog.Percorso);
        Assert.StartsWith("**AIRAC A2611 IN VIGORE DAL 29/10/2026\r\n\r\n####\r\n\r\n**Generale\r\n*NIL\r\n\r\n**LIBB Brindisi\r\n*NIL", nuovo, StringComparison.Ordinal);
        Assert.Empty(Changelog.Controlla(nuovo.Split("\r\n"), "2611"));
        // Una seconda volta no: dice già A2611.
        Assert.Empty(FineDelCiclo.Apri(_repo.Lavoro, "2611", new DateOnly(2026, 10, 29), out perche));
        Assert.Contains("già A2611", perche, StringComparison.Ordinal);
    }

    [Fact]
    public void UnFileToltoVaInDeleteUpd_ESparisceDagliIsc()
    {
        _repo.Scrivi("SectorFiles/delete.upd", "IT\\lirg.vfr\r\nIT\\limm.vfr");
        _repo.Scrivi("SectorFiles/ITALY.isc", "[ARTCC]\r\nF;ACC\\FRA.artcc\r\nF;ACC\\test.artcc\r\n");
        _repo.Scrivi("SectorFiles/LIRR.isc", "[ARTCC]\r\nF;ACC\\FRA.artcc\r\n");

        var cambiati = Cancellazioni.Applica(_repo.Lavoro, ["SectorFiles/Include/IT/ACC/test.artcc", "Profiles/mio.cpr"]);

        Assert.Equal(["SectorFiles/delete.upd", "SectorFiles/ITALY.isc"], cambiati);
        Assert.Equal("IT\\lirg.vfr\r\nIT\\limm.vfr\r\nIT\\ACC\\test.artcc", File_("SectorFiles/delete.upd"));
        Assert.Equal("[ARTCC]\r\nF;ACC\\FRA.artcc\r\n", File_("SectorFiles/ITALY.isc"));
        // Una seconda volta non si ripete.
        Assert.Empty(Cancellazioni.Applica(_repo.Lavoro, ["SectorFiles/Include/IT/ACC/test.artcc"]));
        Assert.True(Registrazione.DentroIConfini("SectorFiles/ITALY.isc"));
        Assert.False(Registrazione.DentroIConfini("SectorFiles/update.ini"));
    }
}

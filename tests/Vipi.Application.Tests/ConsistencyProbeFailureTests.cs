using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Diagnostics;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Cosa succede quando è la <b>diagnostica</b> a rompersi.
///
/// <para>Fino al 22 agosto 2026 le cinque parti del report giravano in fila senza protezione, e le
/// conseguenze erano due — la seconda peggiore della prima: una sonda che lanciava uccideva il circuito
/// Blazor della pagina che serve proprio a capire cosa non va; e, presa l'eccezione più in alto, il guasto di
/// <b>una</b> sonda cancellava il lavoro di tutte le altre — un problema del server di database nascondeva
/// una pista orfana già trovata.</para>
///
/// <para>È la lezione di <c>StartupMaintenanceReport</c>, che questo stesso servizio consuma: un guasto non
/// deve uccidere il giro, ma non deve nemmeno restare zitto.</para>
/// </summary>
public class ConsistencyProbeFailureTests
{
    /// <summary>Un dataset con UNA incongruenza vera: è quella che le sonde rotte non devono farci perdere.</summary>
    private static ConsistencyDataset ConUnaPistaOrfana() => new()
    {
        TransferConditions = new[]
        {
            new TransferConditionRow(1, "LIRR", "VALMA", ConditionRefId: 99001, ConditionLabel: null, ConditionAreaLabel: null),
        },
    };

    [Fact]
    public async Task Una_sonda_rotta_non_cancella_i_rilievi_delle_altre()
    {
        var sut = new ConsistencyReportService(
            new RepoFinto(ConUnaPistaOrfana()),
            schema: new SondaRotta("boom schema"),
            admin: null,
            server: new SondaRotta("il server non risponde"),
            startup: null);

        var findings = await sut.RunAsync();

        // L'incongruenza dei dati c'è ancora: è il punto di tutto il giro.
        Assert.Contains(findings, f => f.Category == "Pista orfana");
        // E i due guasti sono DICHIARATI, non inghiottiti.
        var rotte = findings.Where(f => f.Category == ConsistencyReportService.CategoriaSondaRotta).ToList();
        Assert.Equal(2, rotte.Count);
        Assert.All(rotte, f => Assert.Equal(ConsistencySeverity.Error, f.Severity));
        Assert.Contains(rotte, f => f.Detail.Contains("il server non risponde"));
    }

    /// <summary>Se a rompersi è la lettura dei dati, le sonde di contorno devono comunque parlare.</summary>
    [Fact]
    public async Task Anche_il_caricamento_dei_dati_e_protetto()
    {
        var sut = new ConsistencyReportService(new RepoRotto(),
            schema: new SondaBuona("Drift di schema"), admin: null, server: null, startup: null);

        var findings = await sut.RunAsync();

        Assert.Contains(findings, f => f.Category == "Drift di schema");
        Assert.Contains(findings, f => f.Category == ConsistencyReportService.CategoriaSondaRotta
                                       && f.Entity == "incongruenze dei dati");
    }

    /// <summary>
    /// ⚠️ La richiesta annullata non è un guasto della sonda: non diventa un rilievo, perché nessuno lo
    /// leggerebbe — la risposta non parte affatto. Il `catch` della cancellazione va PRIMA di quello generico.
    /// </summary>
    [Fact]
    public async Task Una_richiesta_annullata_non_diventa_un_rilievo()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var sut = new ConsistencyReportService(new RepoFinto(new ConsistencyDataset()),
            schema: new SondaAnnullata(), admin: null, server: null, startup: null);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.RunAsync(cts.Token));
    }

    [Fact]
    public async Task Senza_guasti_non_compare_nessun_rilievo_di_sonda()
    {
        var sut = new ConsistencyReportService(new RepoFinto(new ConsistencyDataset()),
            schema: new SondaBuona(), admin: null, server: null, startup: null);

        Assert.DoesNotContain(await sut.RunAsync(),
            f => f.Category == ConsistencyReportService.CategoriaSondaRotta);
    }

    private sealed class RepoFinto : IConsistencyReportRepository
    {
        private readonly ConsistencyDataset _d;
        public RepoFinto(ConsistencyDataset d) => _d = d;
        public Task<ConsistencyDataset> LoadAsync(CancellationToken ct = default) => Task.FromResult(_d);
    }

    private sealed class RepoRotto : IConsistencyReportRepository
    {
        public Task<ConsistencyDataset> LoadAsync(CancellationToken ct = default) =>
            throw new InvalidOperationException("connessione caduta");
    }

    private sealed class SondaRotta : ISchemaDriftProbe, IServerSettingsProbe
    {
        private readonly string _perche;
        public SondaRotta(string perche) => _perche = perche;
        public Task<IReadOnlyList<ConsistencyFinding>> RunAsync(CancellationToken ct = default) =>
            throw new InvalidOperationException(_perche);
    }

    private sealed class SondaBuona : ISchemaDriftProbe
    {
        private readonly string? _categoria;
        public SondaBuona(string? categoria = null) => _categoria = categoria;
        public Task<IReadOnlyList<ConsistencyFinding>> RunAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ConsistencyFinding>>(_categoria is null
                ? Array.Empty<ConsistencyFinding>()
                : new[] { new ConsistencyFinding(_categoria, ConsistencySeverity.Warning, "x", "y") });
    }

    private sealed class SondaAnnullata : ISchemaDriftProbe
    {
        public Task<IReadOnlyList<ConsistencyFinding>> RunAsync(CancellationToken ct = default) =>
            throw new OperationCanceledException(ct);
    }
}

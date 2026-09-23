using Vipi.Application.Content;
using Vipi.Domain.Entities;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Il raccoglitore delle modifiche e le famiglie: la finestra che diventa la causa delle righe «da ripubblicare».
/// Carta <c>docs/feature/2026-09-23-da-fare-per-cambiamento.md</c> §4.
/// </summary>
public class ModificheInAttesaTests
{
    private static readonly DateTime T0 = new(2026, 9, 23, 21, 4, 0, DateTimeKind.Utc);

    [Fact]
    public void La_finestra_si_chiama_col_suo_primo_istante()
    {
        var f = new FinestraDiModifiche(T0, new[] { FamiglieDiModifica.Coordinamenti, FamiglieDiModifica.Settori });

        Assert.Equal("mod:20260923210400", f.Chiave);
        Assert.Equal(new[] { "2026-09-23T21:04:00.0000000Z", "Coordinamenti", "Settori" }, f.Argomenti);
    }

    [Fact]
    public async Task Le_modifiche_della_stessa_finestra_si_sommano_e_la_prima_da_il_nome()
    {
        // ⚠️ Istanti nel PASSATO: uno fisso (T0) può stare nel futuro rispetto all'orologio di chi fa girare il
        // test, e la presa aspetterebbe fino a lì — è successo, un'ora e undici minuti.
        var prima = DateTime.UtcNow.AddMinutes(-5);
        var a = new ModificheInAttesa();
        a.Segnala(new[] { FamiglieDiModifica.Settori }, prima);
        a.Segnala(new[] { FamiglieDiModifica.Coordinamenti, FamiglieDiModifica.Settori }, prima.AddSeconds(30));

        var f = await a.PrendiAsync(TimeSpan.Zero, CancellationToken.None);

        Assert.Equal(prima, f.DaUtc);
        Assert.Equal(new[] { FamiglieDiModifica.Coordinamenti, FamiglieDiModifica.Settori }, f.Famiglie);
    }

    [Fact]
    public async Task Un_istante_nel_futuro_non_fa_aspettare_piu_della_finestra()
    {
        var a = new ModificheInAttesa();
        a.Segnala(new[] { FamiglieDiModifica.Testo }, DateTime.UtcNow.AddHours(3));

        var f = await a.PrendiAsync(TimeSpan.FromMilliseconds(50), CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(new[] { FamiglieDiModifica.Testo }, f.Famiglie);
    }

    [Fact]
    public async Task Presa_la_finestra_la_prossima_modifica_ne_apre_un_altra()
    {
        var a = new ModificheInAttesa();
        var t = DateTime.UtcNow.AddMinutes(-30);
        a.Segnala(new[] { FamiglieDiModifica.Testo }, t);
        var prima = await a.PrendiAsync(TimeSpan.Zero, CancellationToken.None);

        a.Segnala(new[] { FamiglieDiModifica.Aree }, t.AddMinutes(10));
        var seconda = await a.PrendiAsync(TimeSpan.Zero, CancellationToken.None);

        Assert.NotEqual(prima.Chiave, seconda.Chiave);
        Assert.Equal(new[] { FamiglieDiModifica.Aree }, seconda.Famiglie);
    }

    [Fact]
    public async Task Senza_modifiche_si_aspetta_e_si_sveglia_alla_prima()
    {
        var a = new ModificheInAttesa();
        var presa = a.PrendiAsync(TimeSpan.Zero, CancellationToken.None);

        await Task.Delay(50);
        Assert.False(presa.IsCompleted);

        a.Segnala(new[] { FamiglieDiModifica.Aeroporti }, DateTime.UtcNow);
        var f = await presa.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { FamiglieDiModifica.Aeroporti }, f.Famiglie);
    }

    [Fact]
    public async Task Si_aspetta_dalla_prima_modifica_non_dall_ultima()
    {
        // Con modifiche continue un'attesa «della calma» non finirebbe mai: la finestra si chiude a tempo fisso.
        var a = new ModificheInAttesa();
        a.Segnala(new[] { FamiglieDiModifica.Testo }, DateTime.UtcNow);
        var presa = a.PrendiAsync(TimeSpan.FromMilliseconds(300), CancellationToken.None);

        // Modifiche ogni 100 ms per più del doppio della finestra: la presa deve arrivare lo stesso.
        for (var i = 0; i < 10; i++)
        {
            await Task.Delay(100);
            a.Segnala(new[] { FamiglieDiModifica.Testo }, DateTime.UtcNow);
        }

        Assert.True(presa.IsCompleted);
        await presa;
    }

    [Fact]
    public async Task Un_elenco_vuoto_non_apre_niente()
    {
        var a = new ModificheInAttesa();
        a.Segnala(Array.Empty<string>(), DateTime.UtcNow);

        using var cts = new CancellationTokenSource(100);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => a.PrendiAsync(TimeSpan.Zero, cts.Token));
    }

    [Fact]
    public void Le_famiglie_di_cio_che_finisce_nei_documenti()
    {
        Assert.Equal(FamiglieDiModifica.Coordinamenti, FamiglieDiModifica.Di(new AgreementClause()));
        Assert.Equal(FamiglieDiModifica.Settori, FamiglieDiModifica.Di(new AccSector()));
        Assert.Equal(FamiglieDiModifica.Aeroporti, FamiglieDiModifica.Di(new AirportRunway()));
        Assert.Equal(FamiglieDiModifica.Procedure, FamiglieDiModifica.Di(new AirportProcedure()));
        Assert.Equal(FamiglieDiModifica.Testo, FamiglieDiModifica.Di(new ContentBlock()));
        Assert.Equal(FamiglieDiModifica.Aree, FamiglieDiModifica.Di(new SpecialArea()));
    }

    [Fact]
    public void Quel_che_il_giro_scrive_da_se_non_e_una_modifica()
    {
        // Se contassero, il giro si rilancerebbe da solo: le segnalazioni che apre sarebbero la sua prossima causa.
        Assert.Null(FamiglieDiModifica.Di(new DocumentImpact()));
        Assert.Null(FamiglieDiModifica.Di(new EditorTask()));
        Assert.Null(FamiglieDiModifica.Di(new AuditLog()));
        Assert.Null(FamiglieDiModifica.Di(new DocRelease()));
        Assert.Null(FamiglieDiModifica.Di(new EditResourceLock()));
    }
}

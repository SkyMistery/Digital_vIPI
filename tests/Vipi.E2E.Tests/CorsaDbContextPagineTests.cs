using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Vipi.Application.Abstractions;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.E2E.Tests;

/// <summary>
/// §E9 — le due pagine che il 24 agosto 2026 sono morte in produzione con «A second operation was started
/// on this context instance»: <c>/services/vsop/{acc}</c> e <c>/services/vsop/{acc}/airports</c>, sempre
/// per lo stesso socio <b>senza incarichi</b>, sette richieste.
///
/// <para><b>Perché questo test esiste e i tre tentativi precedenti no.</b> Quelli provavano a riprodurre la
/// corsa con un intercettore registrato <b>in DI</b>, e non scattava mai: in questo assetto l'intercettore
/// si monta sulle OPZIONI del contesto (<c>AddInterceptors</c>, in <c>DependencyInjection</c>), non si
/// prende dal contenitore. Qui il <c>DbContext</c> si ridichiara con dentro un ritardo, così la finestra di
/// sovrapposizione è larga come quella di un database remoto — che è la differenza vera fra questa macchina
/// e <c>atc.it.ivao.aero</c>.</para>
///
/// <para>⚠️ E il <b>controllo</b> conta quanto la prova: senza, «nessuna collisione» direbbe soltanto che il
/// rilevatore tace. La seconda prova fa correre due query LINQ sullo stesso contesto e verifica che il
/// guasto si veda davvero.</para>
///
/// <para><b>Chi era la prima operazione — misurato il 27 agosto 2026, non più dedotto.</b> Rimettendo il
/// difetto (layout con il proprio lavoro in volo <i>e</i> pagina che riprende il contesto della richiesta)
/// questa prova fallisce, e la fotografia dice chi c'era già:
/// <c>EditAuthorizationService.CanEditAnythingAsync → EfEditGrantRepository.HasAnyGrantAsync</c>, cioè la
/// domanda «hai qualcosa da modificare?» del layout. La pagina è la seconda, ed è quella che muore.</para>
///
/// <para>⚠️ Le due guardie di oggi bastano <b>ognuna da sola</b>, ed è stato provato spegnendole una per
/// volta: col layout che conclude prima del render, la pagina regge anche sul contesto della richiesta;
/// con lo scope proprio della pagina, regge anche se il layout lascia qualcosa in volo. Chi ne togliesse
/// una sola non vedrebbe niente rompersi — e per questo la seconda va tolta solo insieme a un test che la
/// sostituisca.</para>
/// </summary>
public sealed class CorsaDbContextPagineTests
{
    [Theory]
    [InlineData("/services/vsop/LIRR")]
    [InlineData("/services/vsop/LIRR/airports")]
    public async Task Le_pagine_del_socio_reggono_un_database_lento(string percorso)
    {
        using var fabbrica = new FabbricaLenta(ritardoMs: 60);

        var res = await fabbrica.CreateClient().GetAsync(percorso);

        var corpo = await res.Content.ReadAsStringAsync();
        // ⚠️ Se fallisce, il messaggio porta la FOTOGRAFIA: chi era già aperto sul contesto. Senza quella
        // metà, «A second operation was started» resta una domanda — è costato un giro di deploy.
        Assert.True(res.StatusCode == HttpStatusCode.OK,
            $"{percorso} -> {(int)res.StatusCode}. Con un database lento la pagina si è sovrapposta a se " +
            $"stessa sul DbContext: è il guasto di §E9.\n" +
            string.Join("\n", Vipi.Application.Diagnostica.CollisioniDbContext.Scatti_()) +
            $"\n{corpo[..Math.Min(1200, corpo.Length)]}");
        Assert.DoesNotContain("second operation", corpo, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Il controllo: una corsa VERA sullo stesso contesto si vede ancora, e lascia la fotografia.</summary>
    [Fact]
    public async Task Il_controllo_una_corsa_vera_si_vede_ancora()
    {
        using var fabbrica = new FabbricaLenta(ritardoMs: 60);
        using var scope = fabbrica.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VipiDbContext>();

        // ⚠️ Query LINQ e non ExecuteSqlRawAsync: il rilevatore di concorrenza di EF non copre i comandi
        // grezzi, ed è la trappola che aveva reso «pulito» un test che non poteva fallire.
        var prima = db.Accs.AsNoTracking().Select(a => a.Code).ToListAsync();
        var seconda = db.Airports.AsNoTracking().Select(a => a.Icao).ToListAsync();

        var ex = await Record.ExceptionAsync(() => Task.WhenAll(prima, seconda));

        Assert.NotNull(ex);
        Assert.Contains("second operation", ex!.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(Vipi.Application.Diagnostica.CollisioniDbContext.Scatti_(), s => s.Contains("erano aperte"));
    }

    /// <summary>Identità di un socio qualunque: nessuna posizione staff ⇒ non admin, nessuna concessione.</summary>
    private sealed class SocioSemplice : ICurrentUserProvider
    {
        public CurrentUser? Get() => new(123456, "Mario Rossi", "LIRR", Array.Empty<string>());
    }

    private sealed class FabbricaLenta : WebApplicationFactory<Program>
    {
        private readonly int _ritardoMs;
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"vipi-corsa-{Guid.NewGuid():N}.db");

        public FabbricaLenta(int ritardoMs) => _ritardoMs = ritardoMs;

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureHostConfiguration(cfg => cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Vipi"] = $"Data Source={_dbPath}",
            }));
            builder.ConfigureServices(s =>
            {
                s.RemoveAll<ICurrentUserProvider>();
                s.AddScoped<ICurrentUserProvider, SocioSemplice>();

                // Il contesto si ridichiara con gli stessi intercettori dell'applicazione più il ritardo:
                // l'intercettore vive sulle OPZIONI, non nel contenitore.
                s.RemoveAll<DbContextOptions<VipiDbContext>>();
                s.AddDbContext<VipiDbContext>(o => o
                    .UseSqlite($"Data Source={_dbPath}",
                        sql => sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
                    .AddInterceptors(new SqliteTuningInterceptor(), new TracciaCollisioniInterceptor(),
                        new RitardoInterceptor(_ritardoMs)));
            });
            Environment.SetEnvironmentVariable("VipiAuth__Enabled", "false");

            var host = base.CreateHost(builder);
            Semina(host.Services);
            return host;
        }

        /// <summary>Il minimo perché le due pagine abbiano davvero qualcosa da leggere: senza, escono dalla
        /// porta «ACC sconosciuta» e non percorrono il ramo che moriva.</summary>
        private static void Semina(IServiceProvider sp)
        {
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VipiDbContext>();
            if (db.Accs.Any()) return;

            var acc = new Acc { Code = "LIRR", Name = "Roma ACC", CountryPrefix = "LI" };
            db.Accs.Add(acc);
            db.SaveChanges();

            var apt = new Airport { AccId = acc.Id, Icao = "LIRF", Name = "Roma Fiumicino" };
            db.Airports.Add(apt);
            db.Sectors.Add(new Sector
            {
                AccId = acc.Id, Callsign = "LIRR_CTR", Type = SectorType.Ctr, Kind = SectorKind.Acc,
                Name = "Roma Radar", IsActive = true, CoverageOrder = 10,
            });
            db.SaveChanges();
            db.Sectors.Add(new Sector
            {
                AccId = acc.Id, AirportId = apt.Id, Callsign = "LIRF_TWR", Type = SectorType.Twr,
                Kind = SectorKind.Airport, Name = "Fiumicino Torre", IsActive = true, CoverageOrder = 20,
            });
            db.SaveChanges();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { /* best-effort */ }
        }
    }

    /// <summary>Ogni comando costa qualcosa, come su un database remoto: è la finestra in cui due
    /// operazioni si incontrano.</summary>
    private sealed class RitardoInterceptor : Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor
    {
        private readonly int _ms;
        public RitardoInterceptor(int ms) => _ms = ms;

        public override async ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command,
            Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(_ms, cancellationToken);
            return result;
        }
    }
}

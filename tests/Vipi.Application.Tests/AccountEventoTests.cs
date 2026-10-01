using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.EventKits;
using Vipi.Application.Live;
using Vipi.Application.Media;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Controllare con un account dell'evento (committente, 1 ottobre 2026; carta
/// <c>docs/feature/2026-10-01-account-evento.md</c>): la lista dei VID dello staff, la richiesta di chi controlla, e la
/// vista live che usa quel VID al posto del proprio.
/// </summary>
public class AccountEventoTests
{
    private static readonly DateTime Adesso = new(2026, 10, 10, 18, 0, 0, DateTimeKind.Utc);
    private const int Mio = 704798;      // il VID con cui si entra nel sito
    private const int DellEvento = 600100;

    // ---- La lista dei VID ---------------------------------------------------------------------------------------

    [Fact]
    public void Si_leggono_un_VID_per_riga_con_la_nota_e_piu_VID_sulla_stessa_riga()
    {
        var (vid, scartati) = EventKitRules.LeggiVid("600100 LIRF_TWR\r\n\r\n600101, 600102; 600103\n  600104\tLIRF APP  ");

        Assert.Empty(scartati);
        Assert.Equal(new[] { 600100, 600101, 600102, 600103, 600104 }, vid.Keys.Order());
        Assert.Equal("LIRF_TWR", vid[600100]);
        Assert.Equal("LIRF APP", vid[600104]);
    }

    /// <summary>⚠️ Una riga che non comincia con un VID si DICE: salvata in silenzio sarebbe un controllore che a evento
    /// iniziato si sente dire «non sei nella lista» senza sapere perché.</summary>
    [Fact]
    public void Le_righe_senza_VID_finiscono_fra_gli_scartati()
    {
        var (vid, scartati) = EventKitRules.LeggiVid("LIRF_TWR 600100\n600101\n12\n6001O2");

        Assert.Equal(new[] { 600101 }, vid.Keys);
        Assert.Equal(new[] { "LIRF_TWR 600100", "12", "6001O2" }, scartati);
    }

    [Fact]
    public async Task Salvare_una_lista_con_righe_illeggibili_si_rifiuta_e_non_scrive()
    {
        var (servizio, repo, _) = Evento(attivo: true, vid: null);

        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() => servizio.SalvaVidAsync("600100\nLIRF_TWR"));
        Assert.Null(repo.Kit.VidEvento);
    }

    /// <summary>🔴 Il cancello vero: chi cambia la lista, o spegne l'evento, toglie SUBITO la vista a chi la stava
    /// usando — non alla fine dell'evento.</summary>
    [Fact]
    public async Task Salvare_la_lista_o_la_testata_manda_fuori_chi_stava_usando_un_account()
    {
        var (servizio, _, registro) = Evento(attivo: true, vid: "600100");
        registro.Usa(Mio, DellEvento, Adesso.AddHours(3));

        Assert.Equal(1, await servizio.SalvaVidAsync("600100 LIRF_TWR"));
        Assert.Null(registro.VidPer(Mio, Adesso));

        registro.Usa(Mio, DellEvento, Adesso.AddHours(3));
        await servizio.SalvaTestataAsync("Italian Night Ops", false, null, null);
        Assert.Null(registro.VidPer(Mio, Adesso));
    }

    // ---- La richiesta di chi controlla ------------------------------------------------------------------------

    [Fact]
    public async Task VID_in_lista_e_online_si_usa_fino_alla_fine_dell_evento()
    {
        var fine = Adesso.AddHours(3);
        var (servizio, registro, traccia) = Richiesta(new AccountDellEvento("Italian Night Ops",
            new Dictionary<int, string> { [DellEvento] = "LIRF_TWR" }, fine), online: (DellEvento, "LIRF_TWR"));

        var r = await servizio.UsaAsync(DellEvento);

        Assert.Equal(EsitoAccountEvento.Usato, r.Esito);
        Assert.Equal("LIRF_TWR", r.Callsign);
        Assert.Equal(DellEvento, registro.VidPer(Mio, fine.AddSeconds(-1)));
        Assert.Null(registro.VidPer(Mio, fine));
        Assert.Equal((Mio, DellEvento, "LIRF_TWR"), Assert.Single(traccia.Righe));
    }

    /// <summary>Un evento senza data di fine non apre una porta per sempre: dodici ore, poi si riscrive il VID.</summary>
    [Fact]
    public async Task Senza_fine_dell_evento_vale_al_massimo_dodici_ore()
    {
        var (servizio, registro, _) = Richiesta(new AccountDellEvento("Italian Night Ops",
            new Dictionary<int, string> { [DellEvento] = "" }, FineUtc: null), online: (DellEvento, "LIRF_TWR"));

        await servizio.UsaAsync(DellEvento);

        Assert.Equal(DellEvento, registro.VidPer(Mio, Adesso.AddHours(11)));
        Assert.Null(registro.VidPer(Mio, Adesso.AddHours(12)));
    }

    [Fact]
    public async Task VID_non_in_lista_non_online_o_senza_evento_non_si_usa()
    {
        var lista = new AccountDellEvento("Italian Night Ops", new Dictionary<int, string> { [DellEvento] = "" }, null);

        var (s1, r1, _) = Richiesta(lista, online: (600999, "LIRF_APP"));
        Assert.Equal(EsitoAccountEvento.NonInLista, (await s1.UsaAsync(600999)).Esito);
        Assert.Null(r1.VidPer(Mio, Adesso));

        var (s2, r2, _) = Richiesta(lista, online: null);
        Assert.Equal(EsitoAccountEvento.NonOnline, (await s2.UsaAsync(DellEvento)).Esito);
        Assert.Null(r2.VidPer(Mio, Adesso));

        var (s3, _, _) = Richiesta(null, online: (DellEvento, "LIRF_TWR"));
        Assert.Equal(EsitoAccountEvento.NessunEvento, (await s3.UsaAsync(DellEvento)).Esito);

        var (s4, _, _) = Richiesta(lista, online: (DellEvento, "LIRF_TWR"), entrato: false);
        Assert.Equal(EsitoAccountEvento.NonEntrato, (await s4.UsaAsync(DellEvento)).Esito);
    }

    // ---- Il riavvio del sito (committente, 1 ottobre 2026) ---------------------------------------------------------

    /// <summary>🔴 Un riavvio durante l'evento non fa riscrivere il VID: la scelta va anche nel database, e all'avvio un
    /// registro NUOVO si rimette in piedi da lì.</summary>
    [Fact]
    public async Task Dopo_un_riavvio_il_registro_si_ricarica_dal_database()
    {
        var archivio = new Archivio();
        var fine = Adesso.AddHours(3);
        var (servizio, _, _) = Richiesta(new AccountDellEvento("Italian Night Ops",
            new Dictionary<int, string> { [DellEvento] = "" }, fine), online: (DellEvento, "LIRF_TWR"), archivio: archivio);
        await servizio.UsaAsync(DellEvento);

        var dopoIlRiavvio = new AccountEventoRegistro();
        dopoIlRiavvio.Carica(await archivio.TuttiAsync(), Adesso.AddMinutes(5));

        Assert.Equal(DellEvento, dopoIlRiavvio.VidPer(Mio, Adesso.AddMinutes(5)));
    }

    /// <summary>Le voci già scadute non tornano: un riavvio il giorno dopo non riapre l'evento di ieri.</summary>
    [Fact]
    public void All_avvio_le_voci_scadute_restano_fuori()
    {
        var registro = new AccountEventoRegistro();
        registro.Carica(new[]
        {
            new AccountEventoSalvato(Mio, DellEvento, Adesso.AddMinutes(-1)),
            new AccountEventoSalvato(123456, 600200, Adesso.AddHours(1)),
        }, Adesso);

        Assert.Null(registro.VidPer(Mio, Adesso));
        Assert.Equal(600200, registro.VidPer(123456, Adesso));
    }

    /// <summary>«Torna al mio VID» e chi cambia l'evento cancellano ANCHE la copia nel database: altrimenti il primo
    /// riavvio rimetterebbe dentro chi era uscito.</summary>
    [Fact]
    public async Task Lasciare_o_cambiare_l_evento_toglie_anche_la_copia_nel_database()
    {
        var archivio = new Archivio();
        var (servizio, _, _) = Richiesta(new AccountDellEvento("Italian Night Ops",
            new Dictionary<int, string> { [DellEvento] = "" }, null), online: (DellEvento, "LIRF_TWR"), archivio: archivio);
        await servizio.UsaAsync(DellEvento);
        Assert.Single(archivio.Righe);
        await servizio.LasciaAsync();
        Assert.Empty(archivio.Righe);

        archivio.Righe[Mio] = new(Mio, DellEvento, Adesso.AddHours(1));
        var (evento, _, _) = Evento(attivo: true, vid: "600100", archivio: archivio);
        await evento.SalvaVidAsync("600100");
        Assert.Empty(archivio.Righe);
    }

    // ---- La vista live --------------------------------------------------------------------------------------------

    /// <summary>🔴 Il punto di tutto: col VID dell'evento in uso la vista live trova la postazione di QUEL VID.</summary>
    [Fact]
    public void La_vista_live_usa_il_VID_dell_evento_e_se_cade_torna_al_proprio()
    {
        var registro = new AccountEventoRegistro();
        registro.Usa(Mio, DellEvento, DateTime.UtcNow.AddHours(1));
        var online = new Online((DellEvento, "LIRF_TWR"), (Mio, "LIRF_GND"));

        Assert.Equal("LIRF_TWR", Live(online, registro).MyCallsign());

        // L'account dell'evento si disconnette: si torna al proprio VID, senza dimenticare la scelta.
        online.Righe = new[] { (Mio, "LIRF_GND") };
        Assert.Equal("LIRF_GND", Live(online, registro).MyCallsign());
        Assert.Equal(DellEvento, registro.VidPer(Mio, DateTime.UtcNow));
    }

    [Fact]
    public void Senza_account_dell_evento_la_vista_live_e_quella_di_sempre()
    {
        var online = new Online((DellEvento, "LIRF_TWR"), (Mio, "LIRF_GND"));
        Assert.Equal("LIRF_GND", Live(online, new AccountEventoRegistro()).MyCallsign());
    }

    // ---- Attrezzi ----------------------------------------------------------------------------------------------

    private static LiveViewService Live(Online online, AccountEventoRegistro registro) =>
        new(stations: null!, structure: null!, topology: null!, online: online, users: new Utente(true),
            registry: null!, authz: new Staff(), sectors: null!, account: registro);

    private static (AccountEventoService, AccountEventoRegistro, Traccia) Richiesta(
        AccountDellEvento? account, (int Vid, string Callsign)? online, bool entrato = true, Archivio? archivio = null)
    {
        var registro = new AccountEventoRegistro();
        var traccia = new Traccia();
        var servizio = new AccountEventoService(new SoloAccount(account),
            online is { } o ? new Online(o) : new Online(), new Utente(entrato), registro, traccia, () => Adesso, archivio);
        return (servizio, registro, traccia);
    }

    /// <summary>La copia nel database, in memoria.</summary>
    private sealed class Archivio : IAccountEventoArchivio
    {
        public Dictionary<int, AccountEventoSalvato> Righe { get; } = new();
        public Task SalvaAsync(int p, int e, DateTime s, CancellationToken ct = default) { Righe[p] = new(p, e, s); return Task.CompletedTask; }
        public Task TogliAsync(int p, CancellationToken ct = default) { Righe.Remove(p); return Task.CompletedTask; }
        public Task SvuotaAsync(CancellationToken ct = default) { Righe.Clear(); return Task.CompletedTask; }
        public Task<IReadOnlyList<AccountEventoSalvato>> TuttiAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AccountEventoSalvato>>(Righe.Values.ToList());
    }

    private static (EventKitService, Repo, AccountEventoRegistro) Evento(bool attivo, string? vid, Archivio? archivio = null)
    {
        var repo = new Repo { Kit = { Name = "Italian Night Ops", IsActive = attivo, VidEvento = vid } };
        var registro = new AccountEventoRegistro();
        var servizio = new EventKitService(repo, new Staff(), new EventKitVisibilityCache(),
            Options.Create(new MediaOptions()), () => Adesso, registro, archivio);
        return (servizio, repo, registro);
    }

    private sealed class Online : IOnlineAtcProvider
    {
        public (int Vid, string Callsign)[] Righe;
        public Online(params (int Vid, string Callsign)[] righe) => Righe = righe;
        public OnlineAtcSnapshot GetCurrent() => new()
        {
            Callsigns = new HashSet<string>(Righe.Select(r => r.Callsign), StringComparer.OrdinalIgnoreCase),
            Details = Righe.Select(r => new OnlineAtc(r.Callsign, UserId: r.Vid, Name: "x", Rating: 5)).ToArray(),
            AsOf = DateTimeOffset.UtcNow,
        };
    }

    private sealed class Utente(bool entrato) : ICurrentUserProvider
    {
        public CurrentUser? Get() => entrato ? new(UserId: Mio, Name: "Tizio", Acc: "LIRR", StaffPositions: Array.Empty<string>()) : null;
    }

    private sealed class Staff : IEditAuthorizationService
    {
        public VipiRole Role => VipiRole.DivisionStaff;
        public bool IsAdmin => false;
        public int? CurrentUserId => Mio;
        public string? CurrentName => "Tizio";
    }

    private sealed class Traccia : IAccountEventoTraccia
    {
        public List<(int, int, string)> Righe { get; } = new();
        public Task RegistraAsync(int vidPersonale, int vidEvento, string callsign, CancellationToken ct = default)
        {
            Righe.Add((vidPersonale, vidEvento, callsign));
            return Task.CompletedTask;
        }
    }

    private sealed class SoloAccount(AccountDellEvento? account) : IEventKitService
    {
        public Task<AccountDellEvento?> AccountAsync(CancellationToken ct = default) => Task.FromResult(account);
        public Task<bool> AccountInCorsoAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> SalvaVidAsync(string? testo, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string?> InCorsoAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<EventKitView?> PubblicoAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<EventKitView> PerStaffAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task SalvaTestataAsync(string nome, bool attivo, DateTime? daUtc, DateTime? aUtc, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> AggiungiFileAsync(string etichetta, string? nota, string fileName, Stream contenuto, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> AggiungiLinkAsync(string etichetta, string? nota, string url, CancellationToken ct = default) => throw new NotSupportedException();
        public Task EliminaAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SpostaAsync(int id, int verso, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<EventKitFile?> FileAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class Repo : IEventKitRepository
    {
        public EventKit Kit { get; } = new();
        public Task<EventKitSnapshot?> LoadAsync(CancellationToken ct = default) =>
            Task.FromResult<EventKitSnapshot?>(new EventKitSnapshot(Kit, Array.Empty<EventKitItemRow>()));
        public Task SaveHeaderAsync(string nome, bool attivo, DateTime? daUtc, DateTime? aUtc, int userId, string userName,
            DateTime adessoUtc, CancellationToken ct = default)
        {
            Kit.Name = nome; Kit.IsActive = attivo; Kit.StartsUtc = daUtc; Kit.EndsUtc = aUtc;
            return Task.CompletedTask;
        }
        public Task SaveVidAsync(string? testo, int userId, string userName, DateTime adessoUtc, CancellationToken ct = default)
        {
            Kit.VidEvento = testo;
            return Task.CompletedTask;
        }
        public Task<int> AddItemAsync(EventKitItem voce, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> CountItemsAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> DeleteItemAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> MoveItemAsync(int id, int verso, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<EventKitFile?> FileAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
    }
}

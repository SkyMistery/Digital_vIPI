using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Application.EventKits;
using Vipi.Application.Media;
using Vipi.Domain;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Il pacchetto dell'evento (committente, 30 settembre 2026; carta <c>docs/feature/2026-09-30-profili-evento.md</c>):
/// una pagina aperta a tutti, accesa dallo staff vicino a un evento, coi profili per postazione e i link di Drive.
/// Sul database vero: servizio e archivio insieme.
/// </summary>
public class ProfiliEventoTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private DateTime _adesso = new(2026, 10, 3, 18, 0, 0, DateTimeKind.Utc);
    private readonly EventKitVisibilityCache _cache = new();

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private static readonly Authz Staff = new(VipiRole.DivisionStaff, 704798, "Carmine");
    private static readonly Authz Pubblico = new(VipiRole.User, null, null);

    private EventKitService Servizio(Authz chi, int maxBytes = 3 * 1024 * 1024) =>
        new(new EfEventKitRepository(_db), chi, _cache, Options.Create(new MediaOptions { MaxUploadBytes = maxBytes }), () => _adesso);

    private static MemoryStream Byte(string testo) => new(Encoding.UTF8.GetBytes(testo));

    [Fact]
    public async Task Acceso_si_vede_con_file_e_link_in_ordine_e_spento_no()
    {
        var s = Servizio(Staff);
        var file = await s.AggiungiFileAsync("LIRF_TWR", "Torre", "C:\\profili\\LIRF_TWR.cpr", Byte("[profilo]"));
        await s.AggiungiLinkAsync("Stand Manager", null, "https://drive.google.com/file/d/abc123/view");

        Assert.Null(await Servizio(Pubblico).PubblicoAsync());               // mai acceso: non c'è
        Assert.Null(await Servizio(Pubblico).FileAsync(file));               // e il file non esce

        await s.SalvaTestataAsync("Italian Night Ops", attivo: true, null, null);
        var vista = await Servizio(Pubblico).PubblicoAsync();
        Assert.Equal("Italian Night Ops", vista!.Name);
        Assert.Equal(new[] { ("LIRF_TWR", EventKitItemKind.File, "LIRF_TWR.cpr"), ("Stand Manager", EventKitItemKind.Link, "") },
            vista.Items.Select(v => (v.Label, v.Kind, v.FileName)));
        var scaricato = await Servizio(Pubblico).FileAsync(file);
        Assert.Equal(("LIRF_TWR.cpr", "[profilo]"), (scaricato!.FileName, Encoding.UTF8.GetString(scaricato.Bytes)));

        await s.SalvaTestataAsync("Italian Night Ops", attivo: false, null, null);
        Assert.Null(await Servizio(Pubblico).PubblicoAsync());
        Assert.Null(await Servizio(Pubblico).FileAsync(file));
        Assert.NotNull(await Servizio(Staff).FileAsync(file));              // lo staff lo prova anche spento
    }

    [Fact]
    public async Task Le_date_aprono_e_chiudono_la_finestra_e_le_voci_restano_per_l_evento_dopo()
    {
        var s = Servizio(Staff);
        await s.AggiungiLinkAsync("Stand Manager", null, "https://drive.google.com/x");
        await s.SalvaTestataAsync("Evento A", true, _adesso.AddHours(2), _adesso.AddHours(6));

        Assert.Null(await Servizio(Pubblico).InCorsoAsync());
        Assert.False((await s.PerStaffAsync()).Visibile);

        _adesso = _adesso.AddHours(3);
        _cache.Svuota();
        Assert.NotNull(await Servizio(Pubblico).InCorsoAsync());

        _adesso = _adesso.AddHours(3);                                        // la fine è esclusa
        _cache.Svuota();
        Assert.Null(await Servizio(Pubblico).InCorsoAsync());

        // L'evento dopo: si cambia il nome, e lo Stand Manager è ancora lì.
        await s.SalvaTestataAsync("Evento B", true, null, null);
        var vista = await Servizio(Pubblico).PubblicoAsync();
        Assert.Equal("Evento B", vista!.Name);
        Assert.Equal("Stand Manager", Assert.Single(vista.Items).Label);
    }

    [Fact]
    public async Task Accendere_si_vede_subito_nell_hub_anche_con_la_risposta_in_memoria()
    {
        Assert.Null(await Servizio(Pubblico).InCorsoAsync());              // ora è in memoria: «no»
        await Servizio(Staff).SalvaTestataAsync("Evento", true, null, null);
        Assert.NotNull(await Servizio(Pubblico).InCorsoAsync());               // il salvataggio l'ha svuotata
    }

    [Fact]
    public async Task Solo_lo_staff_di_divisione_scrive()
    {
        var editorMa = new Authz(VipiRole.User, 111111, "Mario");
        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() => Servizio(editorMa).SalvaTestataAsync("X", true, null, null));
        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() => Servizio(Pubblico).AggiungiLinkAsync("X", null, "https://a.b"));
        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() => Servizio(editorMa).PerStaffAsync());
        Assert.False(await _db.EventKits.AnyAsync());
    }

    [Fact]
    public async Task Si_rifiutano_file_troppo_grandi_di_tipo_sbagliato_o_vuoti_e_link_non_https()
    {
        var s = Servizio(Staff, maxBytes: 10);
        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() => s.AggiungiFileAsync("A", null, "a.cpr", Byte("undici byte")));
        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() => s.AggiungiFileAsync("A", null, "virus.exe", Byte("x")));
        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() => s.AggiungiFileAsync("A", null, "a.cpr", Byte("")));
        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() => s.AggiungiLinkAsync("A", null, "http://drive.google.com/x"));
        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() => s.AggiungiLinkAsync("", null, "https://drive.google.com/x"));
        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() => s.SalvaTestataAsync("", true, null, null));  // acceso senza nome
        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() => s.SalvaTestataAsync("X", true, _adesso, _adesso));
        Assert.False(await _db.EventKitItems.AnyAsync());
    }

    [Fact]
    public async Task Le_voci_si_spostano_e_si_tolgono()
    {
        var s = Servizio(Staff);
        var a = await s.AggiungiLinkAsync("A", null, "https://x.y/a");
        var b = await s.AggiungiFileAsync("", null, "LIBD_APP.cpr", Byte("b"));   // senza titolo: il nome del file
        var c = await s.AggiungiLinkAsync("C", null, "https://x.y/c");

        await s.SpostaAsync(c, -1);
        Assert.Equal(new[] { "A", "C", "LIBD_APP" }, (await s.PerStaffAsync()).Items.Select(v => v.Label));

        await s.SpostaAsync(a, -1);                                           // già in cima: resta
        await s.EliminaAsync(b);
        Assert.Equal(new[] { "A", "C" }, (await s.PerStaffAsync()).Items.Select(v => v.Label));
        Assert.Null(await s.FileAsync(b));
    }

    private sealed class Authz(VipiRole livello, int? vid, string? nome) : IEditAuthorizationService
    {
        public VipiRole Role => livello;
        public bool IsAdmin => Role >= VipiRole.Admin;
        public int? CurrentUserId => vid;
        public string? CurrentName => nome;
        public void EnsureAdmin() { if (!IsAdmin) throw new EditNotAllowedException(); }
    }
}

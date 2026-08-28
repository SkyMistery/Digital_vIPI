using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Domain;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Le promozioni a mano dove vivono davvero. Carta
/// <c>docs/feature/2026-08-28-autorizzazioni-a-livelli.md</c> §5.
///
/// <para>⚠️ <b>Quel che si prova qui non è «i dati si salvano».</b> Sono tre promesse che, cadendo,
/// cadrebbero in silenzio sul permesso più alto del prodotto: che promuovere due volte la stessa persona
/// <b>riscriva</b> invece di lasciare due righe (con due righe a decidere sarebbe l'ordine della query);
/// che il livello vada in colonna come <b>parola</b> e non come numero, perché un giorno qualcuno leggerà
/// quella tabella a mano da un pannello e «3» non dice niente; e che togliere una promozione la tolga
/// davvero.</para>
/// </summary>
public class PromozioniAManoTests : IAsyncLifetime
{
    private const int Promosso = 654321;
    private const int Admin = 704798;

    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private EfRoleOverrideStore Deposito() => new(_db);

    [Fact]
    public async Task Una_promozione_scritta_si_rilegge()
    {
        await Deposito().SetAsync(Promosso, VipiRole.Editor, Admin, "Mario Rossi", "aiuta su Milano");

        var righe = await Deposito().ListAsync();
        var riga = Assert.Single(righe);

        Assert.Equal(Promosso, riga.UserId);
        Assert.Equal(VipiRole.Editor, riga.Level);
        Assert.Equal(Admin, riga.GrantedByUserId);
        Assert.Equal("Mario Rossi", riga.DisplayName);
        Assert.Equal("aiuta su Milano", riga.Note);
        Assert.NotEqual(default, riga.GrantedAtUtc);
    }

    /// <summary>
    /// ⚠️ La chiave è il VID: promuovere di nuovo <b>riscrive</b>. Se accumulasse, due righe contrarie sulla
    /// stessa persona lascerebbero decidere all'ordine della query — cioè al caso.
    /// </summary>
    [Fact]
    public async Task Promuovere_due_volte_riscrive_la_stessa_riga()
    {
        await Deposito().SetAsync(Promosso, VipiRole.DivisionStaff, Admin, "Mario Rossi", "prova");
        await Deposito().SetAsync(Promosso, VipiRole.Admin, Admin, "Mario Rossi", "ora è in direzione");

        var riga = Assert.Single(await Deposito().ListAsync());
        Assert.Equal(VipiRole.Admin, riga.Level);
        Assert.Equal("ora è in direzione", riga.Note);
    }

    /// <summary>Il nome buono viene dal roster e non sempre c'è: riscrivere senza nome non deve cancellarlo.</summary>
    [Fact]
    public async Task Riscrivere_senza_nome_non_cancella_quello_che_cera()
    {
        await Deposito().SetAsync(Promosso, VipiRole.Editor, Admin, "Mario Rossi", null);
        await Deposito().SetAsync(Promosso, VipiRole.Admin, Admin, null, null);

        Assert.Equal("Mario Rossi", Assert.Single(await Deposito().ListAsync()).DisplayName);
    }

    [Fact]
    public async Task Una_nota_di_soli_spazi_non_e_una_nota()
    {
        await Deposito().SetAsync(Promosso, VipiRole.Editor, Admin, null, "   ");
        Assert.Null(Assert.Single(await Deposito().ListAsync()).Note);
    }

    [Fact]
    public async Task Togliere_una_promozione_la_toglie_davvero()
    {
        await Deposito().SetAsync(Promosso, VipiRole.Editor, Admin, null, null);

        Assert.True(await Deposito().RemoveAsync(Promosso, Admin));
        Assert.Empty(await Deposito().ListAsync());
    }

    /// <summary>Cancellare due volte non è un errore: la pagina può ricevere due clic.</summary>
    [Fact]
    public async Task Togliere_una_promozione_che_non_ce_e_un_no_op()
    {
        Assert.False(await Deposito().RemoveAsync(Promosso, Admin));
        Assert.False(await Deposito().RemoveAsync(999999, Admin));
    }

    /// <summary>
    /// ⚠️ Il livello va in colonna come <b>parola</b>, non come numero (SPEC §6, enum → stringa). Un giorno
    /// quella tabella si leggerà a mano da un pannello, e «3» non dice niente a nessuno. Si controlla con
    /// SQL nudo perché passando da EF la conversione renderebbe il test cieco proprio a ciò che prova.
    /// </summary>
    [Fact]
    public async Task Il_livello_sta_in_colonna_come_parola()
    {
        await Deposito().SetAsync(Promosso, VipiRole.Editor, Admin, null, null);

        await using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT Level FROM RoleOverrides WHERE UserId = $vid";
        cmd.Parameters.AddWithValue("$vid", Promosso);

        Assert.Equal("Editor", (string?)await cmd.ExecuteScalarAsync());
    }

    /// <summary>Persone diverse, righe diverse: la chiave è il VID e non c'è nient'altro a distinguerle.</summary>
    [Fact]
    public async Task Persone_diverse_hanno_righe_diverse()
    {
        await Deposito().SetAsync(Promosso, VipiRole.Editor, Admin, null, null);
        await Deposito().SetAsync(111222, VipiRole.DivisionStaff, Admin, null, null);

        var righe = await Deposito().ListAsync();
        Assert.Equal(2, righe.Count);
        Assert.Equal(new[] { 111222, Promosso }, righe.Select(r => r.UserId));   // ordinate per VID
    }
}

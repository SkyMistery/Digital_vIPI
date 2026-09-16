using System.Globalization;
using System.IO.Compression;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vipi.Application;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Application.Diagnostics;
using Vipi.Domain;
using Vipi.Infrastructure.DatabaseCopy;
using Vipi.Infrastructure.Persistence;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// La copia di sicurezza del database (§A47, carta <c>docs/feature/2026-09-16-copia-del-database.md</c>).
///
/// <para>Qui si prova tutto quello che non ha bisogno di un MariaDB: i letterali, il file e la sua riga di
/// chiusura, il verificatore, e il servizio con cancello e registro. Che il file si <b>reimporti</b> davvero lo
/// prova la CI (<c>mariadb-schema</c>) con un'andata e ritorno contro MariaDB 11.4.10.</para>
/// </summary>
public class CopiaDelDatabaseTests
{
    // ---- I letterali ----------------------------------------------------------------------------------

    [Fact]
    public void Una_stringa_esce_su_una_riga_sola_con_gli_escape_di_MariaDB()
    {
        var s = SqlLiteral.Format("l'a\\b\"c\nd\re\0f\x1ag", "T.C");

        Assert.Equal("'l\\'a\\\\b\\\"c\\nd\\re\\0f\\Zg'", s);
        Assert.DoesNotContain('\n', s);
        Assert.DoesNotContain('\r', s);
    }

    [Fact]
    public void I_numeri_non_prendono_la_virgola_della_cultura_italiana()
    {
        var prima = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("it-IT");
        try
        {
            Assert.Equal("1.5", SqlLiteral.Format(1.5d, "T.C"));
            Assert.Equal("0.1", SqlLiteral.Format(0.1f, "T.C"));
            Assert.Equal("1234.5678", SqlLiteral.Format(1234.5678m, "T.C"));
            Assert.Equal("'2026-09-16 21:05:07.123456'",
                SqlLiteral.Format(new DateTime(2026, 9, 16, 21, 5, 7).AddTicks(1234560), "T.C"));
        }
        finally { CultureInfo.CurrentCulture = prima; }
    }

    [Theory]
    [InlineData(null, "NULL")]
    [InlineData(true, "1")]
    [InlineData(false, "0")]
    [InlineData((sbyte)-3, "-3")]
    [InlineData(18446744073709551615UL, "18446744073709551615")]
    public void Scalari(object? valore, string atteso) => Assert.Equal(atteso, SqlLiteral.Format(valore, "T.C"));

    [Fact]
    public void I_binari_escono_in_esadecimale_e_il_vuoto_resta_vuoto()
    {
        Assert.Equal("0x00FF10", SqlLiteral.Format(new byte[] { 0, 255, 16 }, "T.C"));
        Assert.Equal("''", SqlLiteral.Format(Array.Empty<byte>(), "T.C"));
    }

    [Fact]
    public void Un_TIME_oltre_le_24_ore_si_scrive_in_ore_totali()
    {
        Assert.Equal("'30:15:00.000000'", SqlLiteral.Format(TimeSpan.FromHours(30.25), "T.C"));
        Assert.Equal("'-1:00:00.500000'", SqlLiteral.Format(-TimeSpan.FromHours(1) - TimeSpan.FromMilliseconds(500), "T.C"));
    }

    [Fact]
    public void Un_tipo_sconosciuto_ferma_la_copia_col_nome_della_colonna()
    {
        var e = Assert.Throws<NotSupportedException>(() => SqlLiteral.Format(new Uri("https://x"), "Accs.Code"));
        Assert.Contains("Accs.Code", e.Message);
    }

    [Fact]
    public void Un_nome_con_un_backtick_si_raddoppia() =>
        Assert.Equal("`a``b`", SqlLiteral.Identifier("a`b"));

    // ---- Il file e il verificatore ---------------------------------------------------------------------

    private static readonly DumpHeader Testata = new(
        "1.28.1 · abcdef0", new DateTime(2026, 9, 16, 21, 0, 0, DateTimeKind.Utc),
        "11.4.10-MariaDB", "20260915_Qualcosa", new[] { "DataProtectionKeys" });

    /// <summary>Due tabelle, la seconda vuota. Torna i byte del testo SQL.</summary>
    private static async Task<(byte[] Bytes, DatabaseBackupSummary Summary)> ScriviAsync(int righeAccs = 3)
    {
        using var ms = new MemoryStream();
        using var w = new SqlDumpWriter(ms);
        await w.WriteHeaderAsync(Testata);
        await w.BeginTableAsync("Accs", "CREATE TABLE `Accs` (\n  `Id` int NOT NULL,\n  `Code` varchar(8)\n)", new[] { "Id", "Code" });
        for (var i = 0; i < righeAccs; i++) await w.WriteRowAsync(new object?[] { i, i == 1 ? null : $"LI'{i}\n" });
        await w.EndTableAsync();
        await w.BeginTableAsync("Vuota", "CREATE TABLE `Vuota` (`Id` int)", new[] { "Id" });
        await w.EndTableAsync();
        var s = await w.FinishAsync();
        return (ms.ToArray(), s);
    }

    [Fact]
    public async Task Il_file_intero_si_verifica_e_i_conti_tornano()
    {
        var (bytes, summary) = await ScriviAsync();

        Assert.Equal(2, summary.Tables);
        Assert.Equal(3, summary.Rows);

        var v = await SqlDumpVerifier.VerifyAsync(new MemoryStream(bytes));
        Assert.True(v.Ok, v.Problem);
        Assert.Equal(summary, v.Declared);
        Assert.Equal(summary, v.Found);
        Assert.Equal("1.28.1 · abcdef0", v.SiteVersion);
        Assert.Equal("2026-09-16T21:00:00Z", v.CreatedUtc);
    }

    [Fact]
    public async Task La_testata_e_la_chiusura_hanno_la_forma_scritta_nella_carta()
    {
        var (bytes, summary) = await ScriviAsync();
        var testo = Encoding.UTF8.GetString(bytes);
        var righe = testo.Split('\n');

        Assert.False(bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }), "niente BOM: il client mariadb lo legge come SQL");
        Assert.Equal(SqlDumpWriter.Magic, righe[0]);
        Assert.Contains("DROP TABLE IF EXISTS `Accs`;", righe);
        Assert.Contains("-- Escluse di proposito: DataProtectionKeys", righe);
        Assert.Contains("-- vipi-tabella `Accs` righe=3", righe);
        Assert.Contains("-- vipi-tabella `Vuota` righe=0", righe);
        Assert.Equal("", righe[^1]);
        Assert.Equal(SqlDumpWriter.Trailer(summary), righe[^2]);
        // Il CREATE di SHOW CREATE TABLE è su più righe e resta com'è; l'INSERT è UNO, su una riga.
        Assert.Single(righe, r => r.StartsWith("INSERT INTO `Accs` (`Id`,`Code`) VALUES ", StringComparison.Ordinal));
        Assert.DoesNotContain(righe, r => r.StartsWith("INSERT INTO `Vuota`", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Un_download_interrotto_si_riconosce()
    {
        var (bytes, _) = await ScriviAsync();

        // A metà di una riga, e proprio sul confine di una riga: tutti e due senza la chiusura.
        var aMeta = bytes[..(bytes.Length / 2)];
        var ultimoACapo = Array.LastIndexOf(bytes, (byte)'\n', bytes.Length - 2);
        var sulConfine = bytes[..(ultimoACapo + 1)];

        foreach (var troncato in new[] { aMeta, sulConfine })
        {
            var v = await SqlDumpVerifier.VerifyAsync(new MemoryStream(troncato));
            Assert.False(v.Ok);
            Assert.Contains("INCOMPLETA", v.Problem);
        }
    }

    [Fact]
    public async Task Un_byte_cambiato_non_torna_con_l_impronta()
    {
        var (bytes, _) = await ScriviAsync();
        // Si cerca nei BYTE: la testata porta un «·», e un indice di carattere cadrebbe un byte più in là.
        var i = bytes.AsSpan().IndexOf("LI\\'0"u8);
        Assert.True(i > 0);
        bytes[i] = (byte)'X';

        var v = await SqlDumpVerifier.VerifyAsync(new MemoryStream(bytes));

        Assert.False(v.Ok);
        Assert.Contains("impronta", v.Problem);
    }

    [Fact]
    public async Task Un_conteggio_ritoccato_insieme_all_impronta_non_torna_coi_conti()
    {
        // Chi riscrive la chiusura con un'impronta giusta ma conti falsi: i conti li rifà il verificatore.
        var (bytes, s) = await ScriviAsync();
        var testo = Encoding.UTF8.GetString(bytes);
        var falso = testo.Replace(SqlDumpWriter.Trailer(s), SqlDumpWriter.Trailer(s with { Rows = 99 }));

        var v = await SqlDumpVerifier.VerifyAsync(new MemoryStream(Encoding.UTF8.GetBytes(falso)));

        Assert.False(v.Ok);
        Assert.Contains("conti", v.Problem);
    }

    [Fact]
    public async Task Un_file_che_non_e_una_copia_lo_dice()
    {
        var v = await SqlDumpVerifier.VerifyAsync(new MemoryStream(Encoding.UTF8.GetBytes("SELECT 1;\n")));
        Assert.False(v.Ok);
    }

    [Fact]
    public async Task Il_verificatore_apre_da_solo_il_gzip()
    {
        var (bytes, summary) = await ScriviAsync();
        using var gz = new MemoryStream();
        await using (var z = new GZipStream(gz, CompressionLevel.Fastest, leaveOpen: true)) await z.WriteAsync(bytes);
        gz.Position = 0;

        var v = await SqlDumpVerifier.VerifyAsync(gz);

        Assert.True(v.Ok, v.Problem);
        Assert.Equal(summary.Sha256, v.Found.Sha256);
    }

    [Fact]
    public async Task Un_INSERT_lungo_si_spezza_e_ogni_istruzione_sta_su_una_riga()
    {
        using var ms = new MemoryStream();
        using var w = new SqlDumpWriter(ms);
        await w.WriteHeaderAsync(Testata);
        await w.BeginTableAsync("Media", "CREATE TABLE `Media` (`Id` int, `Bytes` longblob)", new[] { "Id", "Bytes" });
        var blob = new byte[300 * 1024];   // 600 KB di esadecimale: due righe per istruzione
        for (var i = 0; i < 5; i++) await w.WriteRowAsync(new object?[] { i, blob });
        await w.EndTableAsync();
        await w.FinishAsync();

        var inserts = Encoding.UTF8.GetString(ms.ToArray()).Split('\n')
            .Where(r => r.StartsWith("INSERT INTO", StringComparison.Ordinal)).ToList();

        Assert.True(inserts.Count >= 2, $"atteso più di un INSERT, trovati {inserts.Count}");
        Assert.All(inserts, r => Assert.EndsWith(");", r));
        Assert.True((await SqlDumpVerifier.VerifyAsync(new MemoryStream(ms.ToArray()))).Ok);
    }

    // ---- Il servizio: cancello, registro, una copia alla volta -------------------------------------------

    [Fact]
    public async Task Chi_non_e_Admin_non_legge_niente_e_non_lascia_righe()
    {
        await using var ctx = await ContestoAsync();
        var fonte = new FonteFinta();
        var svc = Servizio(ctx.Db, VipiRole.Editor, fonte);

        await Assert.ThrowsAsync<EditNotAllowedException>(() => svc.WriteAsync(new MemoryStream()));

        Assert.False(fonte.Aperta);
        Assert.Empty(await ctx.Db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task Senza_una_sorgente_la_copia_non_e_disponibile()
    {
        await using var ctx = await ContestoAsync();
        var svc = Servizio(ctx.Db, VipiRole.Admin, fonte: null);

        Assert.False(svc.IsSupported);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.WriteAsync(new MemoryStream()));
    }

    [Fact]
    public async Task L_Admin_riceve_un_gzip_intero_e_il_registro_ne_porta_l_impronta()
    {
        await using var ctx = await ContestoAsync();
        var svc = Servizio(ctx.Db, VipiRole.Admin, new FonteFinta());
        var uscita = new MemoryStream();

        var s = await svc.WriteAsync(uscita);

        uscita.Position = 0;
        var v = await SqlDumpVerifier.VerifyAsync(uscita);
        Assert.True(v.Ok, v.Problem);
        Assert.Equal(s.Sha256, v.Found.Sha256);
        Assert.Equal("1.28.1 · abcdef0", v.SiteVersion);

        var righe = await ctx.Db.AuditLogs.OrderBy(a => a.Id).ToListAsync();
        Assert.Equal(2, righe.Count);
        Assert.All(righe, r => Assert.Equal(("DatabaseBackup", 704798, AuditAction.View), (r.EntityType, r.UserId, r.Action)));
        Assert.Contains("Inizio", righe[0].DetailsJson);
        Assert.Contains(s.Sha256, righe[1].DetailsJson);

        var ultima = await svc.LastAsync();
        Assert.NotNull(ultima);
        Assert.Equal(704798, ultima!.UserId);
        Assert.Equal(s, ultima.Completed);
    }

    [Fact]
    public async Task Una_copia_interrotta_resta_nel_registro_come_non_completata()
    {
        await using var ctx = await ContestoAsync();
        var svc = Servizio(ctx.Db, VipiRole.Admin, new FonteFinta { Guasto = new IOException("connessione caduta") });

        await Assert.ThrowsAsync<IOException>(() => svc.WriteAsync(new MemoryStream()));

        var ultima = await svc.LastAsync();
        Assert.NotNull(ultima);
        Assert.Null(ultima!.Completed);
    }

    [Fact]
    public async Task Una_seconda_copia_mentre_la_prima_e_in_corso_viene_rifiutata()
    {
        await using var a = await ContestoAsync();
        await using var b = await ContestoAsync();
        var ferma = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entrata = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var prima = Servizio(a.Db, VipiRole.Admin, new FonteFinta { Entrata = entrata, Ferma = ferma.Task });
        var seconda = Servizio(b.Db, VipiRole.Admin, new FonteFinta());

        var inCorso = prima.WriteAsync(new MemoryStream());
        await entrata.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.ThrowsAsync<DatabaseBackupBusyException>(() => seconda.WriteAsync(new MemoryStream()));

        ferma.SetResult();
        await inCorso;
        // Finita la prima, la porta si riapre.
        await seconda.WriteAsync(new MemoryStream());
    }

    [Theory]
    [InlineData("1.28.1 · abcdef0", "vipi-copia-2026-09-16-2105Z-1.28.1-abcdef0.sql.gz")]
    [InlineData(null, "vipi-copia-2026-09-16-2105Z-sviluppo.sql.gz")]
    public async Task Il_nome_del_file_porta_data_e_versione(string? versione, string atteso)
    {
        await using var ctx = await ContestoAsync();
        var svc = new DatabaseBackupService(ctx.Db, new Authz(VipiRole.Admin), Array.Empty<IDatabaseDumpSource>(),
            Options.Create(new VipiChromeOptions { Versione = versione }));

        Assert.Equal(atteso, svc.FileName(new DateTime(2026, 9, 16, 21, 5, 0, DateTimeKind.Utc)));
    }

    // ---- attrezzi -----------------------------------------------------------------------------------------

    private static DatabaseBackupService Servizio(VipiDbContext db, VipiRole ruolo, IDatabaseDumpSource? fonte) =>
        new(db, new Authz(ruolo), fonte is null ? Array.Empty<IDatabaseDumpSource>() : new[] { fonte },
            Options.Create(new VipiChromeOptions { Versione = "1.28.1 · abcdef0" }));

    private sealed class Contesto : IAsyncDisposable
    {
        public required SqliteConnection Conn { get; init; }
        public required VipiDbContext Db { get; init; }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await Conn.DisposeAsync(); }
    }

    private static async Task<Contesto> ContestoAsync()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        await conn.OpenAsync();
        var db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(conn).Options);
        await db.Database.EnsureCreatedAsync();
        return new Contesto { Conn = conn, Db = db };
    }

    /// <summary>Il cancello vero (le implementazioni di default dell'interfaccia): solo il livello è finto.</summary>
    private sealed class Authz(VipiRole ruolo) : IEditAuthorizationService
    {
        public VipiRole Role => ruolo;
        public bool IsAdmin => ruolo >= VipiRole.Admin;
        public int? CurrentUserId => 704798;
        public string? CurrentName => "Prova";
    }

    private sealed class FonteFinta : IDatabaseDumpSource
    {
        public bool Aperta { get; private set; }
        public Exception? Guasto { get; init; }
        public TaskCompletionSource? Entrata { get; init; }
        public Task Ferma { get; init; } = Task.CompletedTask;

        public Task<IDumpSnapshot> OpenAsync(CancellationToken ct = default)
        {
            Aperta = true;
            return Task.FromResult<IDumpSnapshot>(new Fotografia(this));
        }

        private sealed class Fotografia(FonteFinta f) : IDumpSnapshot
        {
            public string ServerVersion => "11.4.10-MariaDB";
            public string? LastMigration => "20260915_Qualcosa";
            public IReadOnlyList<string> Excluded => new[] { "DataProtectionKeys" };

            public async Task WriteTablesAsync(SqlDumpWriter writer, CancellationToken ct = default)
            {
                f.Entrata?.TrySetResult();
                await f.Ferma;
                await writer.BeginTableAsync("Accs", "CREATE TABLE `Accs` (`Id` int)", new[] { "Id" }, ct);
                await writer.WriteRowAsync(new object?[] { 1 }, ct);
                if (f.Guasto is not null) throw f.Guasto;
                await writer.EndTableAsync(ct);
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}

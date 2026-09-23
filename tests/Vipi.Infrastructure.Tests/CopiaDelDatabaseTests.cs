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
        // Senza riga di chiusura, e comunque NON «incompleta»: prima si dice che cosa è, poi se è intera.
        var v = await SqlDumpVerifier.VerifyAsync(new MemoryStream(Encoding.UTF8.GetBytes("SELECT 1;\n")));
        Assert.False(v.Ok);
        Assert.Contains("Non è una copia", v.Problem);
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

    /// <summary>
    /// 🔴 Il difetto trovato dalla revisione del 16 settembre 2026: la riga si misurava DOPO averla aggiunta, e una
    /// riga grande finiva in coda a un INSERT già quasi pieno — un KMZ da 8 MB più il megabyte di prima superava il
    /// max_allowed_packet e il ripristino si fermava lì. Ora un INSERT supera il limite solo se porta UNA riga.
    /// </summary>
    [Fact]
    public async Task Una_riga_grande_non_si_accoda_a_un_INSERT_gia_pieno()
    {
        using var ms = new MemoryStream();
        using var w = new SqlDumpWriter(ms);
        await w.WriteHeaderAsync(Testata);
        await w.BeginTableAsync("Aree", "CREATE TABLE `Aree` (`Id` int, `Content` longblob)", new[] { "Id", "Content" });
        // ~900 KB di righe piccole: l'INSERT in corso è quasi pieno, ma non abbastanza da chiudersi da solo.
        for (var i = 0; i < 45; i++) await w.WriteRowAsync(new object?[] { i, new byte[10 * 1024] });
        await w.WriteRowAsync(new object?[] { 999, new byte[600 * 1024] });   // 1,2 MB di esadecimale
        await w.EndTableAsync();
        var s = await w.FinishAsync();

        var inserts = Encoding.UTF8.GetString(ms.ToArray()).Split('\n')
            .Where(r => r.StartsWith("INSERT INTO", StringComparison.Ordinal)).ToList();

        Assert.Equal(2, inserts.Count);
        Assert.All(inserts, r => Assert.True(
            r.Length + 1 <= SqlDumpWriter.StatementChars || !r.Contains("),(", StringComparison.Ordinal),
            $"un INSERT da {r.Length} caratteri con più di una riga"));
        Assert.StartsWith("INSERT INTO `Aree` (`Id`,`Content`) VALUES (999,", inserts[1]);
        // L'istruzione più lunga è dichiarata in chiusura, e il verificatore la ritrova.
        Assert.Equal(inserts.Max(r => (long)r.Length + 1), s.LongestStatementBytes);
        var v = await SqlDumpVerifier.VerifyAsync(new MemoryStream(ms.ToArray()));
        Assert.True(v.Ok, v.Problem);
        Assert.Equal(s.LongestStatementBytes, v.Found.LongestStatementBytes);
    }

    // ---- Le viste condivise (v_share_, dal 23 settembre 2026) --------------------------------------------

    /// <summary>
    /// La <c>CREATE</c> vera che MariaDB 11.4.10 dà per la vista dell'hub (copiata da <c>SHOW CREATE VIEW</c>).
    /// Ripristinata con un DEFINER che non è chi ripristina, chiede <c>SET USER</c>: provato con un utente che ha
    /// tutto sul suo database e nient'altro, «Access denied; you need … SET USER». Senza, la vista nasce sua.
    /// </summary>
    [Fact]
    public void La_vista_esce_senza_DEFINER_e_con_tutto_il_resto()
    {
        const string vera = "CREATE ALGORITHM=UNDEFINED DEFINER=`itivao_atc`@`%` SQL SECURITY DEFINER VIEW " +
            "`v_share_atc_sessions` AS select `AtcSessions`.`SessionId` AS `session_id` from `AtcSessions`";

        Assert.Equal(
            "CREATE ALGORITHM=UNDEFINED SQL SECURITY DEFINER VIEW `v_share_atc_sessions` AS select " +
            "`AtcSessions`.`SessionId` AS `session_id` from `AtcSessions`",
            MySqlDumpSource.WithoutDefiner(vera));

        // Un utente col backtick nel nome (raddoppiato da MariaDB) non lascia pezzi di clausola.
        Assert.Equal("CREATE SQL SECURITY DEFINER VIEW `v` AS select 1",
            MySqlDumpSource.WithoutDefiner("CREATE DEFINER=`a``b`@`localhost` SQL SECURITY DEFINER VIEW `v` AS select 1"));
    }

    [Theory]
    [InlineData("v_share_atc_sessions", true)]
    [InlineData("v_share_", true)]
    [InlineData("V_SHARE_atc_sessions", false)]
    [InlineData("v_sharex", false)]
    [InlineData("vista_qualunque", false)]
    public void La_copia_porta_solo_le_viste_condivise(string nome, bool portata) =>
        Assert.Equal(portata, MySqlDumpSource.IsSharedView(nome));

    /// <summary>
    /// La vista esce dopo le tabelle, non conta come tabella nella chiusura, e il file resta una copia che il
    /// verificatore dichiara INTERA (formato 1: il verificatore di prima la legge uguale).
    /// </summary>
    [Fact]
    public async Task La_vista_esce_dopo_le_tabelle_e_la_copia_resta_intera()
    {
        using var ms = new MemoryStream();
        using (var w = new SqlDumpWriter(ms))
        {
            await w.WriteHeaderAsync(Testata);
            await w.BeginTableAsync("AtcSessions", "CREATE TABLE `AtcSessions` (`SessionId` bigint)", new[] { "SessionId" });
            await w.WriteRowAsync(new object?[] { 1L });
            await w.EndTableAsync();
            await w.WriteViewAsync("v_share_atc_sessions",
                "CREATE ALGORITHM=UNDEFINED SQL SECURITY DEFINER VIEW `v_share_atc_sessions` AS select 1 AS `x`");
            var s = await w.FinishAsync();
            Assert.Equal(1, s.Tables);
        }

        var righe = Encoding.UTF8.GetString(ms.ToArray()).Split('\n');
        var tabella = Array.FindIndex(righe, r => r.StartsWith("-- vipi-tabella `AtcSessions`", StringComparison.Ordinal));
        var drop = Array.IndexOf(righe, "DROP VIEW IF EXISTS `v_share_atc_sessions`;");
        Assert.True(tabella >= 0 && drop > tabella, "la vista deve uscire dopo la tabella che legge");
        Assert.EndsWith("AS select 1 AS `x`;", righe[drop + 1]);

        var v = await SqlDumpVerifier.VerifyAsync(new MemoryStream(ms.ToArray()));
        Assert.True(v.Ok, v.Problem);
        Assert.Equal(1, v.Found.Tables);
    }

    [Fact]
    public async Task Il_ripristino_gira_in_strict_mode_e_la_testata_dice_come_si_fa()
    {
        var (bytes, _) = await ScriviAsync();
        var righe = Encoding.UTF8.GetString(bytes).Split('\n');

        // Senza STRICT un valore troncato al ripristino è un avviso che il client mariadb non stampa.
        Assert.Contains(righe, r => r.StartsWith("SET @VIPI_OLD_MODE=", StringComparison.Ordinal)
                                    && r.Contains("STRICT_ALL_TABLES") && r.Contains("NO_AUTO_VALUE_ON_ZERO"));
        // E la testata dice come si ripristina: controllo, database vuoto, pacchetto grande.
        Assert.Contains(righe, r => r.Contains("verifica <file>"));
        Assert.Contains(righe, r => r.Contains("VUOTO"));
        Assert.Contains(righe, r => r.Contains("--max-allowed-packet=1G"));
    }

    /// <summary>
    /// 🔴 Un guasto a metà copia NON deve lasciare un gzip ben chiuso: `gunzip -t` lo promuoverebbe e
    /// `gunzip | mariadb` eseguirebbe la metà che c'è. Un gzip regolare finisce con la lunghezza del contenuto
    /// (ISIZE, ultimi 4 byte): dopo il taglio non c'è.
    /// </summary>
    [Fact]
    public async Task Una_copia_fallita_a_meta_non_e_un_gzip_chiuso()
    {
        var uscita = new MemoryStream();
        await Assert.ThrowsAsync<IOException>(() => DatabaseBackupService.WriteGzipAsync(
            new FonteFinta { Guasto = new IOException("connessione caduta") }.Apri(), uscita, "x", DateTime.UtcNow));

        var gz = uscita.ToArray();
        Assert.False(ChiusuraGzipTorna(gz, (await Decomprimi(gz)).Length), "il gzip porta la sua chiusura regolare");
        Assert.False((await SqlDumpVerifier.VerifyAsync(new MemoryStream(gz))).Ok);

        // Controprova: la stessa copia arrivata in fondo è un gzip chiuso, quindi la prova qui sopra distingue.
        var buona = new MemoryStream();
        await DatabaseBackupService.WriteGzipAsync(new FonteFinta().Apri(), buona, "x", DateTime.UtcNow);
        var gzBuono = buona.ToArray();
        Assert.True(ChiusuraGzipTorna(gzBuono, (await Decomprimi(gzBuono)).Length));
    }

    private static async Task<byte[]> Decomprimi(byte[] gz)
    {
        using var fuori = new MemoryStream();
        await using (var z = new GZipStream(new MemoryStream(gz), CompressionMode.Decompress)) await z.CopyToAsync(fuori);
        return fuori.ToArray();
    }

    private static bool ChiusuraGzipTorna(byte[] gz, int lunghezza) =>
        gz.Length >= 18 && BitConverter.ToUInt32(gz, gz.Length - 4) == (uint)lunghezza;

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
            return Task.FromResult(Apri());
        }

        public IDumpSnapshot Apri() => new Fotografia(this);

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

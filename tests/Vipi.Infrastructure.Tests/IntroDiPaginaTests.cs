using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// L'intro di pagina in archivio (carta <c>2026-08-30-intro-di-pagina.md</c>).
///
/// <para>⚠️ Quel che si prova qui non è «i dati si salvano». Sono tre promesse che, cadendo, cadrebbero in
/// silenzio: che l'intro <b>non abbia bisogno di una tabella nuova</b> (vive in <c>SharedBlocks</c>, che
/// esiste dall'<c>InitialCreate</c>), che il cancello stia nel <b>servizio</b> e non nella pagina, e che
/// un'intro svuotata non lasci in giro una riga che dice «ci sono» con dentro il niente.</para>
/// </summary>
public class IntroDiPaginaTests : IAsyncLifetime
{
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

    private EfPageIntroStore Deposito(VipiRole livello = VipiRole.Editor) => new(_db, new Authz(livello));

    private static List<PageIntroSection> Una(string titolo, string testo) => new()
    {
        new PageIntroSection
        {
            Title = titolo,
            Blocks = new List<ExtraBlock> { new() { Format = BlockFormat.Prose, Text = testo } },
        },
    };

    [Fact]
    public async Task Si_salva_e_si_rilegge_senza_una_tabella_nuova()
    {
        await Deposito().SalvaAsync("mil", Una("Documenti generali", "Leggere prima."), "Intro vSOP militari");

        var lette = await Deposito(VipiRole.User).LeggiAsync("mil");

        var sola = Assert.Single(lette);
        Assert.Equal("Documenti generali", sola.Title);
        Assert.Equal("Leggere prima.", Assert.Single(sola.Blocks).Text);
    }

    /// <summary>La riga vive in <c>SharedBlocks</c>, chiavata col prefisso: è ciò che permette alla seconda
    /// pagina di registrare una chiave invece di un secondo meccanismo.</summary>
    [Fact]
    public async Task La_riga_e_un_blocco_condiviso_chiavato_sulla_pagina()
    {
        await Deposito().SalvaAsync("mil", Una("Titolo", "testo"), "Intro vSOP militari");

        var riga = Assert.Single(await _db.SharedBlocks.AsNoTracking().ToListAsync());
        Assert.Equal("page-intro:mil", riga.Key);
        Assert.Equal("Intro vSOP militari", riga.Title);
        Assert.NotNull(riga.BodyJson);
    }

    /// <summary>Due pagine, due chiavi: l'una non vede l'altra.</summary>
    [Fact]
    public async Task Ogni_pagina_ha_la_sua_intro()
    {
        await Deposito().SalvaAsync("mil", Una("Militari", "a"), "Intro mil");
        await Deposito().SalvaAsync("vloa", Una("vLOA", "b"), "Intro vLOA");

        Assert.Equal("Militari", (await Deposito().LeggiAsync("mil"))[0].Title);
        Assert.Equal("vLOA", (await Deposito().LeggiAsync("vloa"))[0].Title);
    }

    /// <summary>Una pagina che l'intro non ce l'ha risponde «niente», non esplode.</summary>
    [Fact]
    public async Task Una_pagina_senza_intro_risponde_vuoto()
    {
        Assert.Empty(await Deposito(VipiRole.User).LeggiAsync("mai-scritta"));
    }

    /// <summary>⚠️ Svuotare CANCELLA la riga: una riga col JSON nullo sarebbe un secondo modo di essere
    /// vuota, e prima o poi qualcuno ne gestisce uno solo.</summary>
    [Fact]
    public async Task Svuotare_l_intro_toglie_la_riga()
    {
        await Deposito().SalvaAsync("mil", Una("Titolo", "testo"), "Intro");
        await Deposito().SalvaAsync("mil", new List<PageIntroSection>(), "Intro");

        Assert.Empty(await _db.SharedBlocks.AsNoTracking().ToListAsync());
        Assert.Empty(await Deposito().LeggiAsync("mil"));
    }

    [Fact]
    public async Task Salvare_due_volte_non_fa_due_righe()
    {
        await Deposito().SalvaAsync("mil", Una("Prima", "a"), "Intro");
        await Deposito().SalvaAsync("mil", Una("Seconda", "b"), "Intro");

        var riga = Assert.Single(await _db.SharedBlocks.AsNoTracking().ToListAsync());
        Assert.Equal("page-intro:mil", riga.Key);
        Assert.Equal("Seconda", (await Deposito().LeggiAsync("mil"))[0].Title);
    }

    /// <summary>⚠️ Il cancello sta nel SERVIZIO. Una zona che si mostra a tutti e si salva da un bottone
    /// nascosto sarebbe protetta dal CSS.</summary>
    [Theory]
    [InlineData(VipiRole.User)]
    [InlineData(VipiRole.DivisionStaff)]
    public async Task Sotto_l_editor_non_si_salva(VipiRole livello)
    {
        await Assert.ThrowsAsync<EditNotAllowedException>(
            () => Deposito(livello).SalvaAsync("mil", Una("Titolo", "testo"), "Intro"));

        Assert.Empty(await _db.SharedBlocks.AsNoTracking().ToListAsync());
    }

    /// <summary>La lettura è pubblica: la pagina la fa anche per chi non è loggato.</summary>
    [Fact]
    public async Task La_lettura_non_chiede_permessi()
    {
        await Deposito().SalvaAsync("mil", Una("Titolo", "testo"), "Intro");

        Assert.Single(await Deposito(VipiRole.User).LeggiAsync("mil"));
    }

    // ---- La traduzione (carta §4) --------------------------------------------------------------------

    /// <summary>
    /// ⚠️ È la prova che l'intro <b>si tradurrà</b>. Senza il pezzo nel corpus la pagina chiederebbe alla
    /// memoria delle frasi che nessuno le ha mai messo dentro: l'intro resterebbe italiana sopra un elenco
    /// inglese e <b>nulla protesterebbe</b>, perché per il riempimento non manca niente.
    /// </summary>
    [Fact]
    public async Task Le_frasi_dell_intro_entrano_nel_giro_della_traduzione()
    {
        await Deposito().SalvaAsync("mil", Una("Documenti generali", "Leggere prima di controllare."), "Intro");

        var segmenti = await new EfTranslatableCorpus(_db).SegmentiAsync("it");

        Assert.Contains("Documenti generali", segmenti);
        Assert.Contains("Leggere prima di controllare.", segmenti);
    }

    /// <summary>L'intro nasce in italiano: nel giro inglese non ci va, o si pagherebbero caratteri per
    /// tradurre l'italiano <i>verso</i> l'italiano.</summary>
    [Fact]
    public async Task Nel_giro_inglese_l_intro_non_entra()
    {
        await Deposito().SalvaAsync("mil", Una("Documenti generali", "Leggere prima di controllare."), "Intro");

        var segmenti = await new EfTranslatableCorpus(_db).SegmentiAsync("en");

        Assert.DoesNotContain("Documenti generali", segmenti);
    }

    /// <summary>⚠️ Il testo di un blocco ALLEGATO è la nota sotto il link, e si legge: deve tradursi. Lo
    /// slug no — è un identificatore, e tradurlo spegnerebbe il link.</summary>
    [Fact]
    public async Task Del_blocco_allegato_si_traduce_la_nota_e_non_lo_slug()
    {
        var sezioni = new List<PageIntroSection>
        {
            new()
            {
                Title = "Documenti",
                Blocks = new List<ExtraBlock>
                {
                    new()
                    {
                        Format = BlockFormat.Attachment,
                        Text = "In vigore dal primo settembre.",
                        AttachmentJson = AttachmentRef.Serialize(new AttachmentRef("circolare-01", null)),
                    },
                },
            },
        };
        await Deposito().SalvaAsync("mil", sezioni, "Intro");

        var segmenti = await new EfTranslatableCorpus(_db).SegmentiAsync("it");

        Assert.Contains("In vigore dal primo settembre.", segmenti);
        Assert.DoesNotContain(segmenti, s => s.Contains("circolare-01", StringComparison.Ordinal));
    }

    private sealed class Authz : IEditAuthorizationService
    {
        private readonly VipiRole _livello;
        public Authz(VipiRole livello) => _livello = livello;
        public VipiRole Role => _livello;
        public bool IsAdmin => _livello >= VipiRole.Admin;
        public int? CurrentUserId => 1;
        public string? CurrentName => "test";
    }
}

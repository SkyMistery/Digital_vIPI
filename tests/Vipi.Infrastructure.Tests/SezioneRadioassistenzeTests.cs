using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// La sezione «Radioassistenze» di un vSOP militare, dal documento all'anagrafica e ritorno (carta
/// <c>2026-08-27-vsop-militari.md</c> §12).
///
/// <para>
/// ⚠️ <b>È anche la prova che il payload scende nei figli.</b> «Radioassistenze» è una <b>sotto-sezione</b>
/// di «Dati generali», e fino al 29 agosto 2026 le due porte del contenuto strutturato cercavano solo fra le
/// sezioni radice: qui il salvataggio sollevava «Sezione assente». Questo test gira sul profilo vero, quindi
/// se quella ricerca tornasse a fermarsi al primo livello lo direbbe subito.
/// </para>
/// </summary>
public class SezioneRadioassistenzeTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;

    private sealed class AllowAuthz : IEditAuthorizationService
    {
        public bool IsAdmin => true;
        public VipiRole Role => VipiRole.Admin;
        public int? CurrentUserId => 42;
        public string? CurrentName => "test";
        public void EnsureAdmin() { }
    }

    private EfNavaidCatalog Anagrafica() => new(_db);

    private EfMilitaryDocumentService Militari() =>
        new(_db, new AiracService(), new AllowAuthz(),
            new EfEditingRepository(_db, new AiracService(), new EfMediaMaintenance(_db)),
            new EfSpecialAreaRepository(_db), Anagrafica());

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();

        var acc = new Acc { Code = "LIBB", Name = "Brindisi" };
        _db.Accs.Add(acc);
        _db.Airports.Add(new Airport
        {
            Icao = "LIBA", Name = "Amendola", Acc = acc, HasMilitaryPresence = true, IsMilitaryOnly = true,
        });
        await _db.SaveChangesAsync();

        // L'anagrafica come la riempie il sectorfile: MNL col canale, AEA senza, AVI che è un NDB.
        await Anagrafica().ImportFromSourceAsync(new[]
        {
            new SourceNavaid("MNL", "VHF", "115.25", "99Y", 41.5476, 15.6898),
            new SourceNavaid("AEA", "VHF", "111.65", "54Y", 40.6382, 8.2918),
            new SourceNavaid("AVI", "NDB", "390.0", null, 45.9243, 12.4285),
        });
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    /// <summary>
    /// Il giro completo: si salva <b>chi</b> e in che ordine, si rilegge <b>quanto vale</b>. ⚠️ La sezione è
    /// una figlia: se il payload smettesse di scendere, qui salterebbe il salvataggio.
    /// </summary>
    [Fact]
    public async Task Il_documento_dice_chi_e_l_anagrafica_dice_quanto_vale()
    {
        var m = Militari();
        await m.CreaAsync("LIBA");

        await m.SaveNavaidsAsync("LIBA", new[]
        {
            new NavaidKey("MNL", "VHF", "99Y"), new NavaidKey("AVI", "NDB", null), new NavaidKey("AEA", "VHF", "54Y"),
        });

        var righe = await m.GetNavaidsAsync("LIBA");

        // L'ordine è quello del DOCUMENTO, non quello dell'archivio (che sarebbe AEA, AVI, MNL).
        Assert.Equal(new[] { "MNL", "AVI", "AEA" }, righe.Select(r => r.Code));
        Assert.Equal("115.25", righe[0].Frequency);
        Assert.Equal("99Y", righe[0].Channel);
        Assert.Null(righe[1].Channel);
    }

    /// <summary>La sezione è davvero annidata: se un giorno diventasse una radice, questo test lo dice — e
    /// la ragione per cui il payload scende sparirebbe con lei.</summary>
    [Fact]
    public async Task La_sezione_delle_radioassistenze_e_una_figlia()
    {
        var docId = await Militari().CreaAsync("LIBA");

        var sezione = await _db.DocumentSections.AsNoTracking()
            .FirstAsync(s => s.DocumentVersion!.DocumentId == docId && s.SectionKey == "navaids");

        Assert.NotNull(sezione.ParentSectionId);
        Assert.Equal(1, sezione.Depth);
    }

    /// <summary>
    /// ⚠️ Il payload convive con la PROSA dei SOP nella stessa sezione, e non la tocca: è l'invariante che
    /// rende sicura tutta la sezione 12, visto che quelle sezioni il caricatore le riempie di testo.
    /// </summary>
    [Fact]
    public async Task Il_payload_non_tocca_la_prosa_della_stessa_sezione()
    {
        var m = Militari();
        var docId = await m.CreaAsync("LIBA");
        var sezione = await _db.DocumentSections
            .FirstAsync(s => s.DocumentVersion!.DocumentId == docId && s.SectionKey == "navaids");
        _db.ContentBlocks.Add(new ContentBlock
        {
            DocumentVersionId = sezione.DocumentVersionId, SectionId = sezione.Id, Order = 1,
            Format = BlockFormat.Prose, Tier = BlockTier.Extended, Visibility = BlockVisibility.Always,
            Body = "Il TACAN di Amendola è soggetto a manutenzione il martedì.",
            RowVersion = Guid.NewGuid().ToByteArray(),
        });
        await _db.SaveChangesAsync();

        await m.SaveNavaidsAsync("LIBA", new[] { new NavaidKey("MNL", "VHF", "99Y") });

        var prosa = await _db.ContentBlocks.AsNoTracking()
            .Where(b => b.SectionId == sezione.Id && b.Body != null).SingleAsync();
        Assert.Equal("Il TACAN di Amendola è soggetto a manutenzione il martedì.", prosa.Body);
        Assert.Single(await m.GetNavaidsAsync("LIBA"));
    }

    /// <summary>Togliere una riga dal documento <b>non</b> tocca l'anagrafica: quella radioassistenza la
    /// citano altri SOP.</summary>
    [Fact]
    public async Task Togliere_una_riga_dal_documento_non_la_toglie_dall_anagrafica()
    {
        var m = Militari();
        await m.CreaAsync("LIBA");
        await m.SaveNavaidsAsync("LIBA", new[] { new NavaidKey("MNL", "VHF", "99Y"), new NavaidKey("AEA", "VHF", "54Y") });

        await m.SaveNavaidsAsync("LIBA", new[] { new NavaidKey("AEA", "VHF", "54Y") });

        Assert.Equal(new[] { "AEA" }, (await m.GetNavaidsAsync("LIBA")).Select(r => r.Code));
        Assert.Equal(3, (await Anagrafica().ListAsync()).Count);
    }

    /// <summary>Un documento che non cita niente ha una tabella vuota, non un errore.</summary>
    [Fact]
    public async Task Senza_righe_la_tabella_e_vuota()
    {
        var m = Militari();
        await m.CreaAsync("LIBA");

        Assert.Empty(await m.GetNavaidsAsync("LIBA"));
    }

    /// <summary>Un campo senza vSOP militare non ha righe da mostrare, e chiederle non è un errore: la
    /// pagina pubblica ci passa prima di sapere se il documento esiste.</summary>
    [Fact]
    public async Task Un_campo_senza_documento_non_esplode()
    {
        Assert.Empty(await Militari().GetNavaidsAsync("LIRF"));
    }

    /// <summary>
    /// ⚠️ <b>Una riga citata da un documento non si elimina dall'anagrafica.</b> Sparirebbe da sotto una
    /// tabella già scritta, e chi legge quel documento vedrebbe una riga in meno senza spiegazione — la
    /// pagina dice <b>chi</b> la cita, perché è l'unica informazione con cui si può rimediare.
    /// </summary>
    [Fact]
    public async Task Una_riga_citata_non_si_elimina_e_si_sa_chi_la_cita()
    {
        var m = Militari();
        await m.CreaAsync("LIBA");
        var anagrafica = Anagrafica();
        var amd = await anagrafica.CreateAsync("AMD", "VHF", userId: 7);
        await m.SaveNavaidsAsync("LIBA", new[] { amd.Key });

        Assert.Equal(NavaidDelete.Citata, await anagrafica.DeleteAsync(amd.Id, userId: 7));

        var citata = Assert.Single(await anagrafica.CitataDaAsync(amd.Id));
        Assert.Contains("Radioassistenze", citata);

        // Tolta dal documento, si elimina.
        await m.SaveNavaidsAsync("LIBA", Array.Empty<NavaidKey>());
        Assert.Equal(NavaidDelete.Ok, await anagrafica.DeleteAsync(amd.Id, userId: 7));
    }

    /// <summary>Vale anche per chi la cita da un <b>aeroporto alternato</b>: è l'altra tabella che le usa.</summary>
    [Fact]
    public async Task Anche_una_citazione_da_un_alternato_conta()
    {
        var m = Militari();
        await m.CreaAsync("LIBA");
        var anagrafica = Anagrafica();
        var amd = await anagrafica.CreateAsync("AMD", "VHF", userId: 7);
        await m.SaveDiversionsAsync("LIBA", new[]
        {
            new MilDiversionPayload.Riga
            {
                Icao = "LIBG",
                Navaids = new[] { new MilDiversionPayload.Nav { Code = "AMD", Kind = "VHF" } },
            },
        });

        Assert.Equal(NavaidDelete.Citata, await anagrafica.DeleteAsync(amd.Id, userId: 7));
    }
}

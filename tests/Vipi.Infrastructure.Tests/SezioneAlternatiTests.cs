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
/// La sezione «Aeroporti alternati» dal documento ai cataloghi e ritorno (carta
/// <c>2026-08-27-vsop-militari.md</c> §12f).
///
/// <para>⚠️ Il caso che conta è l'alternato <b>estero</b>: su un campo di confine è la norma, non sta nel
/// nostro archivio, e la pagina che lo mostra <b>non deve chiamare IVAO</b> per stampare una cella. Per
/// questo il nome si porta dietro nel documento — e per questo, quando lo scalo è dei nostri, vince
/// l'archivio.</para>
/// </summary>
public class SezioneAlternatiTests : IAsyncLifetime
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

    private EfMilitaryDocumentService Militari() =>
        new(_db, new AiracService(), new AllowAuthz(),
            new EfEditingRepository(_db, new AiracService(), new EfMediaMaintenance(_db)),
            new EfSpecialAreaRepository(_db), new EfNavaidCatalog(_db), new EfAirportNameLookup(_db));

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();

        var acc = new Acc { Code = "LIBB", Name = "Brindisi" };
        _db.Accs.Add(acc);
        _db.Airports.AddRange(
            new Airport { Icao = "LIBA", Name = "Amendola", Acc = acc, HasMilitaryPresence = true, IsMilitaryOnly = true },
            new Airport { Icao = "LIBG", Name = "Grottaglie", Acc = acc, HasMilitaryPresence = true });
        await _db.SaveChangesAsync();

        await new EfNavaidCatalog(_db).ImportFromSourceAsync(new[]
        {
            new SourceNavaid("MNL", "VHF", "115.25", "99Y", 41.5476, 15.6898),
            // ⚠️ GRO sta DUE volte fra i VHF, come nell'anagrafica vera: un VOR senza canale e un TACAN col
            // solo canale. È il caso che separa un'identità a tre campi da una a due.
            new SourceNavaid("GRO", "VHF", "109.85", null, 40.5178, 17.4031),
            new SourceNavaid("GRO", "VHF", null, "35Y", 40.5178, 17.4031),
        });
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private static MilDiversionPayload.Riga Riga(string icao, string? nome = null, int? b = null, int? d = null,
        params (string Code, string Kind, string? Channel)[] nav) => new()
    {
        Icao = icao, Name = nome, Bearing = b, Distance = d,
        Navaids = nav.Select(n => new MilDiversionPayload.Nav
        {
            Code = n.Code, Kind = n.Kind, Channel = n.Channel,
        }).ToList(),
    };

    /// <summary>Il giro completo, con dentro i due casi che contano: uno scalo nostro e uno estero.</summary>
    [Fact]
    public async Task Il_documento_dice_le_righe_e_i_cataloghi_le_riempiono()
    {
        var m = Militari();
        await m.CreaAsync("LIBA");

        await m.SaveDiversionsAsync("LIBA", new[]
        {
            Riga("LIBG", "un nome vecchio", 126, 40, ("MNL", "VHF", "99Y")),
            Riga("LGKR", "Kerkyra", 95, 210),
        });

        var righe = await m.GetDiversionsAsync("LIBA");

        Assert.Equal(new[] { "LIBG", "LGKR" }, righe.Select(r => r.Icao));
        // ⚠️ Lo scalo nostro prende il nome dall'ARCHIVIO, non quello salvato nel documento.
        Assert.Equal("Grottaglie", righe[0].Name);
        // ⚠️ Quello estero prende il nome salvato: non è in archivio, e la pagina non chiama la sorgente.
        Assert.Equal("Kerkyra", righe[1].Name);

        Assert.Equal(126, righe[0].Bearing);
        Assert.Equal(40, righe[0].DistanceNm);
        Assert.Equal("MNL", Assert.Single(righe[0].Navaids).Code);
        Assert.Empty(righe[1].Navaids);
    }

    /// <summary>
    /// Due impianti con lo stesso codice citati sulla STESSA riga, contro l'anagrafica vera. ⚠️ È il caso
    /// che il 1° settembre 2026 non reggeva: l'indice della risoluzione si faceva su codice+famiglia, e due
    /// chiavi che ci cadevano dentro insieme non facevano una cella sbagliata — facevano saltare la sezione.
    /// </summary>
    [Fact]
    public async Task Due_impianti_omonimi_sulla_stessa_riga_restano_distinti()
    {
        var m = Militari();
        await m.CreaAsync("LIBA");

        await m.SaveDiversionsAsync("LIBA", new[]
        {
            Riga("LIBG", "Grottaglie", 126, 40, ("GRO", "VHF", null), ("GRO", "VHF", "35Y")),
        });

        var nav = Assert.Single(await m.GetDiversionsAsync("LIBA")).Navaids;

        Assert.Equal(2, nav.Count);
        Assert.Equal(new[] { "GRO", "GRO" }, nav.Select(n => n.Code));
        Assert.Equal(new string?[] { null, "35Y" }, nav.Select(n => n.Channel));
        Assert.Equal(new string?[] { "109.85", null }, nav.Select(n => n.Frequency));
    }

    /// <summary>Il canale sopravvive al giro completo scrittura → lettura: è quello che identifica
    /// l'impianto, e perderlo per strada lo fa sparire dalla tabella senza dire niente.</summary>
    [Fact]
    public async Task Il_canale_sopravvive_al_giro()
    {
        var m = Militari();
        await m.CreaAsync("LIBA");

        await m.SaveDiversionsAsync("LIBA", new[] { Riga("LIBG", null, null, null, ("MNL", "VHF", "99Y")) });

        // Il rimontaggio dell'editor: si rilegge quel che si vede e lo si risalva tale e quale.
        var ritorno = (await m.GetDiversionsAsync("LIBA")).Select(MilDiversionPayload.Da).ToList();
        await m.SaveDiversionsAsync("LIBA", ritorno);

        var nav = Assert.Single(Assert.Single(await m.GetDiversionsAsync("LIBA")).Navaids);
        Assert.Equal("MNL", nav.Code);
        Assert.Equal("99Y", nav.Channel);
    }

    /// <summary>La sezione è una figlia di «Dati generali», come le Radioassistenze: se il payload smettesse
    /// di scendere nei figli, qui salterebbe il salvataggio.</summary>
    [Fact]
    public async Task La_sezione_degli_alternati_e_una_figlia()
    {
        var docId = await Militari().CreaAsync("LIBA");

        var sezione = await _db.DocumentSections.AsNoTracking()
            .FirstAsync(s => s.DocumentVersion!.DocumentId == docId && s.SectionKey == "diversion");

        Assert.NotNull(sezione.ParentSectionId);
        Assert.Equal(1, sezione.Depth);
    }

    /// <summary>Il payload convive con la prosa dei SOP nella stessa sezione, e non la tocca.</summary>
    [Fact]
    public async Task Il_payload_non_tocca_la_prosa_della_stessa_sezione()
    {
        var m = Militari();
        var docId = await m.CreaAsync("LIBA");
        var sezione = await _db.DocumentSections
            .FirstAsync(s => s.DocumentVersion!.DocumentId == docId && s.SectionKey == "diversion");
        _db.ContentBlocks.Add(new ContentBlock
        {
            DocumentVersionId = sezione.DocumentVersionId, SectionId = sezione.Id, Order = 1,
            Format = BlockFormat.Prose, Tier = BlockTier.Extended, Visibility = BlockVisibility.Always,
            Body = "In caso di meteo sotto minimi si dirotta su Gioia del Colle.",
            RowVersion = Guid.NewGuid().ToByteArray(),
        });
        await _db.SaveChangesAsync();

        await m.SaveDiversionsAsync("LIBA", new[] { Riga("LIBG") });

        var prosa = await _db.ContentBlocks.AsNoTracking()
            .Where(b => b.SectionId == sezione.Id && b.Body != null).SingleAsync();
        Assert.Equal("In caso di meteo sotto minimi si dirotta su Gioia del Colle.", prosa.Body);
        Assert.Single(await m.GetDiversionsAsync("LIBA"));
    }

    /// <summary>Un documento che non cita alternati ha una tabella vuota, non un errore; e un campo senza
    /// vSOP nemmeno quello.</summary>
    [Fact]
    public async Task Senza_righe_o_senza_documento_la_tabella_e_vuota()
    {
        var m = Militari();
        await m.CreaAsync("LIBA");

        Assert.Empty(await m.GetDiversionsAsync("LIBA"));
        Assert.Empty(await m.GetDiversionsAsync("LIRF"));
    }

    /// <summary>
    /// L'archivio degli scali suggerisce i <b>nostri</b>, e la ricerca puntuale li trova. ⚠️ Un codice che
    /// non è né nostro né della sorgente torna <c>null</c>: chi aggiunge la riga lo saprà, e la riga si
    /// aggiunge lo stesso senza nome.
    /// </summary>
    [Fact]
    public async Task L_elenco_suggerisce_i_nostri_scali_e_la_ricerca_puntuale_li_trova()
    {
        var lookup = new EfAirportNameLookup(_db);

        Assert.Equal(new[] { "LIBA", "LIBG" }, (await lookup.ListAsync()).Select(a => a.Icao));
        Assert.Equal("Grottaglie", (await lookup.FindAsync("libg"))!.Name);
        Assert.True((await lookup.FindAsync("LIBG"))!.InArchivio);
        Assert.Null(await lookup.FindAsync("LGKR"));   // estero, e nessuna sorgente configurata qui
    }
}

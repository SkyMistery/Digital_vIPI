using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Il ponte per i documenti d'aeroporto già scritti (carta 2026-08-26 §3).
///
/// <para>Fino a quella carta il documento d'aeroporto era una proiezione <b>cotta</b>: le sezioni si
/// riconoscevano per TITOLO e nascevano con una chiave <c>custom:{guid}</c> nuova a ogni rigenerazione, perché
/// <c>BlockSection.Airport</c> non ha una chiave di catalogo e il builder ricadeva su
/// <c>SectionKeys.NewCustom()</c>. Le sezioni editoriali libere, invece, avevano tutte la stessa chiave
/// <c>airportextra</c> — quindi erano indistinguibili, e «nascondi» ne avrebbe nascosta una a caso.</para>
///
/// <para>⚠️ Il caso che questi test presidiano davvero è il <b>trasloco</b> degli extra: la loro versione vera
/// stava nella tabella <c>AirportExtraSection</c> (il pubblico li leggeva da lì, live), non nella copia cotta
/// dentro il documento — che poteva essere vecchia di un rebuild.</para>
/// </summary>
public class ReconcileAirportSectionsTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfDocumentMaintenance _manutenzione = default!;
    private Acc _acc = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _manutenzione = new EfDocumentMaintenance(_db);
        _acc = new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" };
        _db.Accs.Add(_acc);
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    /// <summary>Un aeroporto col suo documento nella forma COTTA: sezioni con chiave casuale, titolo inglese e
    /// tabelle dentro. È la forma in cui stanno oggi tutti i documenti d'aeroporto in produzione.</summary>
    private async Task<(Airport Apt, DocumentVersion Ver)> ScaloCottoAsync(string icao, params (string Titolo, string? Tabella)[] sezioni)
    {
        var apt = new Airport { Icao = icao, Name = icao, Acc = _acc };
        _db.Airports.Add(apt);
        var doc = new Document
        {
            Type = DocumentType.Vipi, Title = $"vIPI — {icao}", Language = Language.It,
            Status = DocumentStatus.Draft, LastUpdatedAiracCycle = "2608",
        };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();

        var ver = new DocumentVersion
        {
            DocumentId = doc.Id, VersionNumber = 1, Status = DocumentStatus.Draft,
            CreatedByUserId = 0, CreatedUtc = DateTime.UtcNow, AiracCycle = "2608",
        };
        _db.DocumentVersions.Add(ver);
        await _db.SaveChangesAsync();
        doc.CurrentVersionId = ver.Id;
        apt.DocumentId = doc.Id;

        var order = 0;
        foreach (var (titolo, tabella) in sezioni)
        {
            var s = new DocumentSection
            {
                DocumentVersionId = ver.Id, Title = titolo, Order = ++order, Depth = 0,
                // La chiave che il builder assegnava davvero: una guid nuova, tranne per Frequencies e SID.
                SectionKey = titolo switch
                {
                    "Frequencies" => "frequencies",
                    "SID" => "sids",
                    _ => SectionKeys.NewCustom(),
                },
                RowVersion = Guid.NewGuid().ToByteArray(),
            };
            _db.DocumentSections.Add(s);
            await _db.SaveChangesAsync();
            if (tabella is not null)
                _db.ContentBlocks.Add(new ContentBlock
                {
                    DocumentVersionId = ver.Id, SectionId = s.Id, Order = 1, Format = BlockFormat.Table,
                    Tier = BlockTier.Reduced, Visibility = BlockVisibility.Always, BodyJson = tabella,
                    RowVersion = Guid.NewGuid().ToByteArray(),
                });
        }
        await _db.SaveChangesAsync();
        return (apt, ver);
    }

    [Fact]
    public async Task Le_sezioni_cotte_prendono_la_chiave_di_catalogo_e_perdono_i_blocchi()
    {
        await ScaloCottoAsync("LIRF",
            ("Runway rules", """{"columns":["Condition"],"rows":[]}"""),
            ("Transition levels", """{"columns":["QNH (hPa)"],"rows":[]}"""),
            ("Frequencies", """{"columns":["Name"],"rows":[]}"""),
            ("Runways", """{"columns":["Runway"],"rows":[]}"""),
            ("SID", null));

        // Cinque toccate: tre rinominate, «Frequencies» che aveva la chiave giusta ma il titolo inglese e la
        // tabella cotta dentro, e «SID» — che di suo aveva solo il titolo da allineare... e infatti il titolo
        // di catalogo È «SID», quindi resta com'è. Quattro, allora: la quinta non ha niente da cambiare.
        Assert.Equal(4, await _manutenzione.ReconcileAirportSectionKeysAsync());
        Assert.Equal("Frequenze", (await _db.DocumentSections.SingleAsync(x => x.SectionKey == "frequencies")).Title);

        var sezioni = await _db.DocumentSections.Include(s => s.Blocks).OrderBy(s => s.Order).ToListAsync();
        Assert.Equal(
            new[] { "runwayrules", "transition", "frequencies", "runways", "sids" },
            sezioni.Select(s => s.SectionKey));
        // Il titolo passa a quello del catalogo: il documento nasce Language.It, e le cotture lo scrivevano in
        // inglese — è il motivo per cui il viewer aveva un heading inglese cablato.
        Assert.Equal("Regole piste", sezioni[0].Title);
        Assert.Equal("Quote di transizione", sezioni[1].Title);
        Assert.Equal("Piste", sezioni[3].Title);

        // ⚠️ E soprattutto: niente più blocchi. Da qui il corpo lo produce la pagina, derivandolo dalle tabelle
        // del profilo; un blocco rimasto sarebbe testo scritto nel DB e invisibile in ogni vista.
        Assert.Empty(await _db.ContentBlocks.ToListAsync());
    }

    [Fact]
    public async Task Runways_e_Runway_rules_non_si_confondono()
    {
        // Il match per sottostringa («runway») le avrebbe scambiate: quale delle due vince dipenderebbe
        // dall'ordine di iterazione. Il titolo si riconosce INTERO.
        await ScaloCottoAsync("LIRF", ("Runways", null), ("Runway rules", null));

        await _manutenzione.ReconcileAirportSectionKeysAsync();

        var sezioni = await _db.DocumentSections.OrderBy(s => s.Order).ToListAsync();
        Assert.Equal("runways", sezioni[0].SectionKey);
        Assert.Equal("runwayrules", sezioni[1].SectionKey);
    }

    [Fact]
    public async Task I_titoli_italiani_legacy_sono_riconosciuti()
    {
        // I documenti generati prima dell'i18n hanno i titoli in italiano: se non li si riconosce restano
        // sezioni libere e il catalogo ne aggiunge otto nuove accanto.
        await ScaloCottoAsync("LIRN", ("Regole piste", null), ("Quote di transizione", null), ("Piste", null));

        Assert.Equal(3, await _manutenzione.ReconcileAirportSectionKeysAsync());

        Assert.Equal(
            new[] { "runwayrules", "transition", "runways" },
            (await _db.DocumentSections.OrderBy(s => s.Order).ToListAsync()).Select(s => s.SectionKey));
    }

    [Fact]
    public async Task Gli_extra_traslocano_dalla_tabella_al_documento_una_chiave_ciascuno()
    {
        var (apt, ver) = await ScaloCottoAsync("LIRF", ("Frequencies", null));

        // La tabella è la versione VERA (il pubblico la leggeva live)...
        _db.AirportExtraSections.Add(new AirportExtraSection
        {
            AirportId = apt.Id, Order = 1, Title = "Hot spot", Body = "Attenzione al **raccordo B**.",
        });
        _db.AirportExtraSections.Add(new AirportExtraSection
        {
            AirportId = apt.Id, Order = 2, Title = "Rumore", Body = "Procedure antirumore notturne.",
        });
        // ...e nel documento c'è una copia COTTA vecchia, con un titolo che nessuno usa più.
        var vecchia = new DocumentSection
        {
            DocumentVersionId = ver.Id, Title = "Titolo di due rebuild fa", Order = 2, Depth = 0,
            SectionKey = "airportextra", RowVersion = Guid.NewGuid().ToByteArray(),
        };
        _db.DocumentSections.Add(vecchia);
        await _db.SaveChangesAsync();
        _db.ContentBlocks.Add(new ContentBlock
        {
            DocumentVersionId = ver.Id, SectionId = vecchia.Id, Order = 1, Format = BlockFormat.Prose,
            Tier = BlockTier.Extended, Visibility = BlockVisibility.Always, Body = "testo vecchio",
            RowVersion = Guid.NewGuid().ToByteArray(),
        });
        await _db.SaveChangesAsync();

        await _manutenzione.ReconcileAirportSectionKeysAsync();

        var libere = await _db.DocumentSections.Include(s => s.Blocks)
            .Where(s => s.SectionKey != "frequencies").OrderBy(s => s.Order).ToListAsync();
        Assert.Equal(new[] { "Hot spot", "Rumore" }, libere.Select(s => s.Title));
        Assert.All(libere, s => Assert.True(SectionKeys.IsCustom(s.SectionKey)));
        // Chiavi DISTINTE: era il difetto di «airportextra», che le rendeva indistinguibili.
        Assert.Equal(2, libere.Select(s => s.SectionKey).Distinct().Count());
        Assert.Contains("raccordo B", libere[0].Blocks.Single().Body!);
        Assert.DoesNotContain(await _db.ContentBlocks.ToListAsync(), b => b.Body == "testo vecchio");

        // È un TRASLOCO: la tabella resta vuota, ed è ciò che rende il passo idempotente.
        Assert.Empty(await _db.AirportExtraSections.ToListAsync());
    }

    [Fact]
    public async Task E_idempotente()
    {
        var (apt, _) = await ScaloCottoAsync("LIRF", ("Runway rules", """{"columns":[],"rows":[]}"""), ("SID", null));
        _db.AirportExtraSections.Add(new AirportExtraSection { AirportId = apt.Id, Order = 1, Title = "Hot spot", Body = "x" });
        await _db.SaveChangesAsync();

        Assert.Equal(2, await _manutenzione.ReconcileAirportSectionKeysAsync());   // 1 cotta + 1 extra traslocato
        Assert.Equal(0, await _manutenzione.ReconcileAirportSectionKeysAsync());   // niente più da fare

        var sezioni = await _db.DocumentSections.OrderBy(s => s.Order).ToListAsync();
        Assert.Equal(3, sezioni.Count);   // nessun doppione
        Assert.Single(sezioni, s => s.SectionKey == "runwayrules");
        Assert.Single(sezioni, s => s.Title == "Hot spot");
    }

    [Fact]
    public async Task Poi_il_catalogo_aggiunge_le_sezioni_che_mancano_al_posto_giusto()
    {
        // I due passi lavorano in fila: prima le chiavi, poi le mancanti. Invertendoli, il secondo non
        // riconoscerebbe nessuna chiave e aggiungerebbe tutte e otto le sezioni accanto a quelle che ci sono già.
        await ScaloCottoAsync("LIRF",
            ("Transition levels", null), ("Frequencies", null), ("Runways", null), ("SID", null));

        await _manutenzione.ReconcileAirportSectionKeysAsync();
        var aggiunte = await _manutenzione.AddMissingCatalogSectionsAsync();

        var chiavi = (await _db.DocumentSections.OrderBy(s => s.Order).ToListAsync()).Select(s => s.SectionKey).ToList();
        Assert.Equal(4, aggiunte);   // weather, runwayrules, operationaltechnique, validity
        Assert.Equal(
            new[] { "weather", "runwayrules", "transition", "frequencies", "runways", "sids", "operationaltechnique", "validity" },
            chiavi);

        // Il meteo non deve nascere Frozen nemmeno arrivando da qui: un METAR congelato è meteo scaduto.
        var meteo = await _db.DocumentSections.SingleAsync(s => s.SectionKey == "weather");
        Assert.Equal(RenderMode.Live, meteo.RenderMode);
    }

    [Fact]
    public async Task Un_documento_che_non_e_di_un_aeroporto_non_viene_toccato()
    {
        // Una vLOA o una vIPI APP possono avere una sezione libera intitolata «Piste»: non è affar nostro.
        var doc = new Document
        {
            Type = DocumentType.Vipi, Title = "vIPI — LIRP_APP", Language = Language.It,
            Status = DocumentStatus.Draft, LastUpdatedAiracCycle = "2608",
        };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        var ver = new DocumentVersion
        {
            DocumentId = doc.Id, VersionNumber = 1, Status = DocumentStatus.Draft,
            CreatedByUserId = 0, CreatedUtc = DateTime.UtcNow, AiracCycle = "2608",
        };
        _db.DocumentVersions.Add(ver);
        await _db.SaveChangesAsync();
        var chiave = SectionKeys.NewCustom();
        _db.DocumentSections.Add(new DocumentSection
        {
            DocumentVersionId = ver.Id, Title = "Piste", Order = 1, Depth = 0,
            SectionKey = chiave, RowVersion = Guid.NewGuid().ToByteArray(),
        });
        await _db.SaveChangesAsync();

        Assert.Equal(0, await _manutenzione.ReconcileAirportSectionKeysAsync());
        Assert.Equal(chiave, (await _db.DocumentSections.SingleAsync()).SectionKey);
    }
}

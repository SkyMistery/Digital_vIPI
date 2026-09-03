using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// I parcheggi passano dalle «Procedure di terra» ai «Dati generali» — 3 settembre 2026.
///
/// <para>Richiesta del committente: un piazzale e i suoi stalli sono un <b>dato</b> del campo, come piste,
/// radioassistenze e frequenze, non una procedura che si esegue.</para>
///
/// <para>⚠️ <b>Perché serve un passo di riconciliazione e non basta cambiare il catalogo.</b> Il catalogo
/// decide la struttura <b>solo alla nascita</b> (<c>DocumentBirth</c>): i vSOP già scritti resterebbero com'erano
/// per sempre, e <b>nessuno potrebbe rimediare a mano</b> — il motore di riordino sposta soltanto fra
/// <b>fratelli</b> (apposta, perché un riordino non diventi una riparentazione silenziosa), quindi in UI non
/// esiste il gesto «portala in un altro gruppo».</para>
///
/// <para>⚠️ Le <b>release già pubblicate non si toccano</b> (doc 13 §9): il pubblico continua a vedere i
/// parcheggi dov'erano finché quel vSOP non viene ripubblicato. È la regola di ogni altra correzione, non
/// un'eccezione fatta qui.</para>
/// </summary>
public class ParcheggiNeiDatiGeneraliTests : IAsyncLifetime
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
        _acc = new Acc { Code = "LIBB", Name = "Brindisi", CountryPrefix = "LI" };
        _db.Accs.Add(_acc);
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    /// <summary>
    /// Un vSOP militare nella forma VECCHIA: «Dati generali» con le sue sei figlie e «Procedure di terra» con
    /// i parcheggi in testa. È la forma in cui stanno i documenti già scritti.
    /// </summary>
    private async Task<(Airport Apt, DocumentVersion Ver)> VsopVecchioAsync(string icao)
    {
        var apt = new Airport { Icao = icao, Name = icao, Acc = _acc };
        _db.Airports.Add(apt);
        var doc = new Document
        {
            Type = DocumentType.Vipi, Title = $"vSOP MIL — {icao}", Language = Language.It,
            Edition = DocumentEdition.Military, Status = DocumentStatus.Draft, LastUpdatedAiracCycle = "2608",
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
        apt.MilDocumentId = doc.Id;

        var generali = Radice(ver, "generaldata", "Dati generali", 1);
        var terra = Radice(ver, "groundprocedures", "Procedure di terra", 2);
        _db.DocumentSections.AddRange(generali, terra);
        await _db.SaveChangesAsync();

        var ordineG = 0;
        foreach (var (k, t) in new[]
        {
            ("navaids", "Radioassistenze"), ("frequencies", "Frequenze ATC/CRC"), ("diversion", "Aeroporti alternati"),
            ("runways", "Piste"), ("transition", "Quote di transizione"), ("callsigns", "Nominativi"),
        })
            _db.DocumentSections.Add(Figlia(ver, generali, k, t, ++ordineG));

        var ordineT = 0;
        foreach (var (k, t) in new[]
        {
            ("parkings", "Parcheggi"), ("enginestart", "Messa in moto"), ("taxiing", "Rullaggio"),
            ("arming", "Armamento/disarmo"),
        })
            _db.DocumentSections.Add(Figlia(ver, terra, k, t, ++ordineT));

        await _db.SaveChangesAsync();
        return (apt, ver);
    }

    private static DocumentSection Radice(DocumentVersion ver, string key, string titolo, int ordine) => new()
    {
        DocumentVersionId = ver.Id, Title = titolo, Order = ordine, Depth = 0, SectionKey = key,
        RowVersion = Guid.NewGuid().ToByteArray(),
    };

    private static DocumentSection Figlia(DocumentVersion ver, DocumentSection padre, string key, string titolo, int ordine) => new()
    {
        DocumentVersionId = ver.Id, ParentSectionId = padre.Id, Title = titolo, Order = ordine, Depth = 1,
        SectionKey = key, RowVersion = Guid.NewGuid().ToByteArray(),
    };

    private List<DocumentSection> Figli(DocumentVersion ver, string chiavePadre)
    {
        var padre = _db.DocumentSections.Single(x => x.DocumentVersionId == ver.Id && x.SectionKey == chiavePadre);
        return _db.DocumentSections.Where(x => x.ParentSectionId == padre.Id).OrderBy(x => x.Order).ToList();
    }

    [Fact]
    public async Task I_parcheggi_finiscono_in_CODA_ai_dati_generali()
    {
        var (_, ver) = await VsopVecchioAsync("LIBG");

        Assert.Equal(1, await _manutenzione.ReparentMilParkingsAsync());

        var generali = Figli(ver, "generaldata");
        Assert.Equal("parkings", generali[^1].SectionKey);
        // Ultima davvero, e con l'ordine dei fratelli intatto: non è «in fondo» per caso di numerazione.
        Assert.Equal(7, generali.Count);
        Assert.Equal(new[] { "navaids", "frequencies", "diversion", "runways", "transition", "callsigns", "parkings" },
            generali.Select(x => x.SectionKey));
        Assert.Equal(1, generali[^1].Depth);
    }

    [Fact]
    public async Task Il_gruppo_che_li_ha_persi_si_richiude()
    {
        var (_, ver) = await VsopVecchioAsync("LIBG");

        await _manutenzione.ReparentMilParkingsAsync();

        var terra = Figli(ver, "groundprocedures");
        Assert.Equal(new[] { "enginestart", "taxiing", "arming" }, terra.Select(x => x.SectionKey));
        // ⚠️ Order è una POSIZIONE fra fratelli: lasciare il buco farebbe partire le Procedure di terra dal due,
        // e la prima freccia «su» dell'editor si troverebbe una numerazione che non torna.
        Assert.Equal(new[] { 1, 2, 3 }, terra.Select(x => x.Order));
    }

    /// <summary>
    /// ⚠️ Il <b>contenuto viaggia con la sezione</b>: è la stessa riga, non una nuova. Blocchi, «nascosta» e
    /// marcatura pilota/ATC restano attaccati — se il passo cancellasse e ricreasse, un vSOP perderebbe la
    /// tabella dei parcheggi scritta a mano.
    /// </summary>
    [Fact]
    public async Task Il_contenuto_dei_parcheggi_resta_attaccato()
    {
        var (_, ver) = await VsopVecchioAsync("LIBG");
        var parcheggi = _db.DocumentSections.Single(x => x.DocumentVersionId == ver.Id && x.SectionKey == "parkings");
        var id = parcheggi.Id;
        parcheggi.IsHidden = true;
        parcheggi.Audience = SectionAudience.Controllers;
        _db.ContentBlocks.Add(new ContentBlock
        {
            DocumentVersionId = ver.Id, SectionId = id, Order = 1, Format = BlockFormat.Table,
            Tier = BlockTier.Extended, Visibility = BlockVisibility.Always,
            BodyJson = """{"variant":"milparkings","rows":[["Piazzale Nord","1-12",""]]}""",
            RowVersion = Guid.NewGuid().ToByteArray(),
        });
        await _db.SaveChangesAsync();

        await _manutenzione.ReparentMilParkingsAsync();

        var dopo = _db.DocumentSections.Single(x => x.SectionKey == "parkings");
        Assert.Equal(id, dopo.Id);                       // la STESSA riga
        Assert.True(dopo.IsHidden);
        Assert.Equal(SectionAudience.Controllers, dopo.Audience);
        Assert.Single(_db.ContentBlocks.Where(b => b.SectionId == id));
    }

    [Fact]
    public async Task Rieseguirlo_non_cambia_niente()
    {
        var (_, ver) = await VsopVecchioAsync("LIBG");

        Assert.Equal(1, await _manutenzione.ReparentMilParkingsAsync());
        Assert.Equal(0, await _manutenzione.ReparentMilParkingsAsync());
        Assert.Equal(7, Figli(ver, "generaldata").Count);
    }

    /// <summary>
    /// ⚠️ Se qualcuno l'ha già portata altrove, quella è una <b>scelta di chi scrive</b> e non si tocca:
    /// spostare la decisione di un altro sarebbe peggio del difetto che si sta correggendo.
    /// </summary>
    [Fact]
    public async Task Una_sezione_gia_spostata_a_mano_resta_dov_e()
    {
        var (_, ver) = await VsopVecchioAsync("LIBG");
        var volo = Radice(ver, "flightprocedures", "Procedure di volo", 3);
        _db.DocumentSections.Add(volo);
        await _db.SaveChangesAsync();
        var parcheggi = _db.DocumentSections.Single(x => x.DocumentVersionId == ver.Id && x.SectionKey == "parkings");
        parcheggi.ParentSectionId = volo.Id;
        await _db.SaveChangesAsync();

        Assert.Equal(0, await _manutenzione.ReparentMilParkingsAsync());
        Assert.Equal(volo.Id, _db.DocumentSections.Single(x => x.SectionKey == "parkings").ParentSectionId);
    }

    /// <summary>Un documento che non è un vSOP militare non viene sfiorato: la chiave <c>parkings</c> esiste
    /// solo nel profilo militare, ma il passo non si fida della chiave — guarda l'elenco dei documenti
    /// militari.</summary>
    [Fact]
    public async Task Un_documento_non_militare_non_viene_toccato()
    {
        var apt = new Airport { Icao = "LIBD", Name = "Bari", Acc = _acc };
        _db.Airports.Add(apt);
        var doc = new Document
        {
            Type = DocumentType.Vipi, Title = "vIPI — LIBD", Language = Language.It,
            Status = DocumentStatus.Draft, LastUpdatedAiracCycle = "2608",
        };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        var ver = new DocumentVersion
        {
            DocumentId = doc.Id, VersionNumber = 1, Status = DocumentStatus.Draft, AiracCycle = "2608",
        };
        _db.DocumentVersions.Add(ver);
        await _db.SaveChangesAsync();
        apt.DocumentId = doc.Id;
        var terra = Radice(ver, "groundprocedures", "Procedure di terra", 1);
        _db.DocumentSections.Add(terra);
        await _db.SaveChangesAsync();
        _db.DocumentSections.Add(Figlia(ver, terra, "parkings", "Parcheggi", 1));
        await _db.SaveChangesAsync();

        Assert.Equal(0, await _manutenzione.ReparentMilParkingsAsync());
        Assert.Equal(terra.Id, _db.DocumentSections.Single(x => x.SectionKey == "parkings").ParentSectionId);
    }

    // ---- Le sezioni di catalogo mancanti arrivano anche ai militari, e nei SOTTO-gruppi ----------------

    /// <summary>
    /// ⚠️ Due buchi dello stesso passo, chiusi insieme: <c>AddMissingCatalogSections</c> non guardava i vSOP
    /// militari, e si fermava al primo livello — cioè proprio dove il profilo militare non ha quasi niente,
    /// avendo ventisei sezioni dentro sei contenitori.
    /// </summary>
    [Fact]
    public async Task Le_sezioni_mancanti_arrivano_dentro_i_contenitori()
    {
        var (_, ver) = await VsopVecchioAsync("LIBG");

        var aggiunte = await _manutenzione.AddMissingCatalogSectionsAsync();

        Assert.True(aggiunte > 0);
        // Le quattro radici che mancavano ci sono, nell'ordine del catalogo.
        var radici = _db.DocumentSections.Where(x => x.DocumentVersionId == ver.Id && x.ParentSectionId == null)
            .OrderBy(x => x.Order).Select(x => x.SectionKey).ToList();
        Assert.Equal(
            new[] { "weather", "generaldata", "groundprocedures", "flightprocedures", "regulated", "charts", "validity" },
            radici);
        // E le figlie: le otto delle procedure di volo, che nessun passo aveva mai portato.
        Assert.Equal(8, Figli(ver, "flightprocedures").Count);
        Assert.Equal(2, Figli(ver, "regulated").Count);
    }

    /// <summary>
    /// ⚠️ La presenza si misura sulla <b>chiave in tutta la versione</b>: un documento non ancora riparentato
    /// ha i parcheggi sotto le Procedure di terra, e un confronto fatto gruppo per gruppo ne creerebbe un
    /// SECONDO dentro i Dati generali — due sezioni con la stessa chiave, e il corpo che ne pesca una a caso.
    /// </summary>
    [Fact]
    public async Task Una_sezione_nel_gruppo_sbagliato_non_viene_duplicata()
    {
        var (_, ver) = await VsopVecchioAsync("LIBG");

        await _manutenzione.AddMissingCatalogSectionsAsync();

        Assert.Single(_db.DocumentSections.Where(x => x.DocumentVersionId == ver.Id && x.SectionKey == "parkings"));
    }

    [Fact]
    public async Task Aggiungere_le_mancanti_ai_militari_e_idempotente()
    {
        await VsopVecchioAsync("LIBG");

        Assert.True(await _manutenzione.AddMissingCatalogSectionsAsync() > 0);
        Assert.Equal(0, await _manutenzione.AddMissingCatalogSectionsAsync());
    }

    /// <summary>I due passi convivono nell'ordine in cui girano all'avvio: prima lo spostamento, poi le
    /// mancanti — e il risultato è il documento che il catalogo descrive oggi.</summary>
    [Fact]
    public async Task I_due_passi_insieme_danno_il_documento_del_catalogo()
    {
        var (_, ver) = await VsopVecchioAsync("LIBG");

        await _manutenzione.ReparentMilParkingsAsync();
        await _manutenzione.AddMissingCatalogSectionsAsync();

        Assert.Equal(
            new[] { "navaids", "frequencies", "diversion", "runways", "transition", "callsigns", "parkings" },
            Figli(ver, "generaldata").Select(x => x.SectionKey));
        Assert.Equal(new[] { "enginestart", "taxiing", "arming" }, Figli(ver, "groundprocedures").Select(x => x.SectionKey));
        // Le sezioni del profilo, né una di più — il numero lo conta il CATALOGO, non questa riga.
        static IEnumerable<SectionDescriptor> Tutte(IEnumerable<SectionDescriptor> d) =>
            d.SelectMany(x => new[] { x }.Concat(Tutte(x.Children ?? Array.Empty<SectionDescriptor>())));
        Assert.Equal(
            Tutte(SectionCatalog.For(SectionProfile.AirportMil)).Count(),
            _db.DocumentSections.Count(x => x.DocumentVersionId == ver.Id));
    }
}

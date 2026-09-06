using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// I vSOP militari già scritti si allineano all'indice chiesto dal SOD — carta
/// <c>docs/feature/2026-09-06-vsop-sezioni-sod.md</c>.
///
/// <para>⚠️ <b>Perché servono passi di manutenzione e non basta cambiare il catalogo.</b> Il catalogo decide
/// la struttura <b>solo alla nascita</b> (<c>DocumentBirth</c>): togliere una chiave dal registro non la
/// toglie da nessun documento già scritto, e un default nuovo non raggiunge le sezioni che ci sono già.</para>
///
/// <para>⚠️ Le <b>release già pubblicate non si toccano</b> (doc 13 §9): il pubblico continua a leggere lo
/// snapshot vecchio — QRA compresa — finché quel vSOP non viene ripubblicato.</para>
/// </summary>
public class IndiceDelSodTests : IAsyncLifetime
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
    /// Un vSOP militare nella forma di PRIMA del 6 settembre 2026: le procedure di volo con «QRA / Scramble»
    /// in coda, e le sezioni che il SOD marca «[PILOTS]» ancora «per tutti».
    /// </summary>
    private async Task<DocumentVersion> VsopVecchioAsync(string icao)
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
        var volo = Radice(ver, "flightprocedures", "Procedure di volo", 2);
        _db.DocumentSections.AddRange(generali, volo);
        await _db.SaveChangesAsync();

        var og = 0;
        foreach (var (k, t) in new[]
        {
            ("navaids", "Radioassistenze"), ("diversion", "Aeroporti alternati"),
            ("callsigns", "Nominativi"), ("parkings", "Parcheggi"),
        })
            _db.DocumentSections.Add(Figlia(ver, generali, k, t, ++og));

        var ov = 0;
        foreach (var (k, t) in new[]
        {
            ("takeoff", "Restrizioni al decollo"), ("sfo", "Circuito SFO/precauzionale"),
            ("gat", "Partenze/arrivi IFR GAT"), ("qra", "QRA / Scramble"),
        })
            _db.DocumentSections.Add(Figlia(ver, volo, k, t, ++ov));

        await _db.SaveChangesAsync();
        return ver;
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

    private DocumentSection Sezione(DocumentVersion ver, string chiave) =>
        _db.DocumentSections.AsNoTracking().Single(x => x.DocumentVersionId == ver.Id && x.SectionKey == chiave);

    private List<DocumentSection> Figli(DocumentVersion ver, string chiavePadre)
    {
        var padre = _db.DocumentSections.AsNoTracking()
            .Single(x => x.DocumentVersionId == ver.Id && x.SectionKey == chiavePadre);
        return _db.DocumentSections.AsNoTracking()
            .Where(x => x.ParentSectionId == padre.Id).OrderBy(x => x.Order).ToList();
    }

    // ---- QRA esce -------------------------------------------------------------------------------------

    [Fact]
    public async Task Una_QRA_vuota_se_ne_va()
    {
        var ver = await VsopVecchioAsync("LIBG");

        Assert.Equal(1, await _manutenzione.RemoveMilQraSectionsAsync());

        Assert.DoesNotContain(_db.DocumentSections.AsNoTracking().ToList(), x => x.SectionKey == "qra");
    }

    [Fact]
    public async Task Il_gruppo_che_l_ha_persa_si_richiude()
    {
        var ver = await VsopVecchioAsync("LIBG");

        await _manutenzione.RemoveMilQraSectionsAsync();

        // ⚠️ Order è una POSIZIONE fra fratelli, non un'etichetta: lasciare il buco non si vedrebbe subito,
        // e poi il primo riordino a mano partirebbe da una numerazione che non torna.
        var volo = Figli(ver, "flightprocedures");
        Assert.Equal(new[] { "takeoff", "sfo", "gat" }, volo.Select(x => x.SectionKey));
        Assert.Equal(new[] { 1, 2, 3 }, volo.Select(x => x.Order));
    }

    [Fact]
    public async Task Una_QRA_SCRITTA_diventa_una_sezione_libera_e_non_si_perde()
    {
        var ver = await VsopVecchioAsync("LIBG");
        var qra = _db.DocumentSections.Single(x => x.DocumentVersionId == ver.Id && x.SectionKey == "qra");
        _db.ContentBlocks.Add(new ContentBlock
        {
            DocumentVersionId = ver.Id, SectionId = qra.Id, Order = 1, Format = BlockFormat.Prose,
            Tier = BlockTier.Extended, Visibility = BlockVisibility.Always,
            Body = "Scramble entro 15 minuti dall'ordine del CRC.",
            RowVersion = Guid.NewGuid().ToByteArray(),
        });
        await _db.SaveChangesAsync();

        Assert.Equal(1, await _manutenzione.RemoveMilQraSectionsAsync());

        // La sezione c'è ancora, col suo titolo e il suo testo: cambia solo che il catalogo non la
        // riconosce più. Su quattro basi di difesa aerea quel testo può esserci davvero, e buttarlo via
        // sarebbe stato peggio del difetto.
        var libera = _db.DocumentSections.AsNoTracking()
            .Single(x => x.DocumentVersionId == ver.Id && x.Title == "QRA / Scramble");
        Assert.True(SectionKeys.IsCustom(libera.SectionKey));
        Assert.False(SectionKeys.IsLegacyCustom(libera.SectionKey));
        Assert.Single(_db.ContentBlocks.AsNoTracking().Where(b => b.SectionId == libera.Id));
    }

    [Fact]
    public async Task Un_blocco_VUOTO_non_salva_la_sezione()
    {
        // ⚠️ Il segnaposto lo mette la NASCITA su ogni sezione resa dalla pagina: se contasse come
        // «contenuto», nessuna QRA sarebbe mai stata tolta e il passo avrebbe girato a vuoto su tutti e
        // quindici i campi — verde, e senza fare niente.
        var ver = await VsopVecchioAsync("LIBG");
        var qra = _db.DocumentSections.Single(x => x.DocumentVersionId == ver.Id && x.SectionKey == "qra");
        _db.ContentBlocks.Add(new ContentBlock
        {
            DocumentVersionId = ver.Id, SectionId = qra.Id, Order = 1, Format = BlockFormat.Table,
            Tier = BlockTier.Extended, Visibility = BlockVisibility.Always,
            RowVersion = Guid.NewGuid().ToByteArray(),
        });
        await _db.SaveChangesAsync();

        await _manutenzione.RemoveMilQraSectionsAsync();

        Assert.Empty(_db.DocumentSections.AsNoTracking().Where(x => x.DocumentVersionId == ver.Id && x.SectionKey == "qra"));
        Assert.Empty(_db.ContentBlocks.AsNoTracking().Where(b => b.DocumentVersionId == ver.Id));
    }

    [Fact]
    public async Task Rieseguirlo_non_cambia_niente()
    {
        var ver = await VsopVecchioAsync("LIBG");

        Assert.Equal(1, await _manutenzione.RemoveMilQraSectionsAsync());
        Assert.Equal(0, await _manutenzione.RemoveMilQraSectionsAsync());
        Assert.Equal(3, Figli(ver, "flightprocedures").Count);
    }

    [Fact]
    public async Task Non_tocca_i_documenti_che_militari_non_sono()
    {
        // Una sezione «qra» su un documento che non è un vSOP militare non è roba di questo passo: la
        // chiave non è riservata, e una sezione libera può chiamarsi come le pare.
        var doc = new Document
        {
            Type = DocumentType.Vipi, Title = "vIPI APP", Language = Language.It,
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
        _db.DocumentSections.Add(Radice(ver, "qra", "QRA", 1));
        await _db.SaveChangesAsync();

        Assert.Equal(0, await _manutenzione.RemoveMilQraSectionsAsync());
        Assert.Equal("qra", Sezione(ver, "qra").SectionKey);
    }

    // ---- Il pubblico di default ----------------------------------------------------------------------

    [Fact]
    public async Task Le_sezioni_che_il_catalogo_vuole_marcate_diventano_per_i_PILOTI()
    {
        var ver = await VsopVecchioAsync("LIBG");

        var marcate = await _manutenzione.ApplyCatalogAudienceDefaultsAsync();

        // Le quattro del documento finto che stanno nell'elenco del SOD: alternati, nominativi, parcheggi,
        // restrizioni al decollo. Le altre — navaids, sfo, gat, i due contenitori — restano «per tutti».
        Assert.Equal(4, marcate);
        foreach (var k in new[] { "diversion", "callsigns", "parkings", "takeoff" })
            Assert.Equal(SectionAudience.Pilots, Sezione(ver, k).Audience);
        foreach (var k in new[] { "navaids", "sfo", "gat", "generaldata", "flightprocedures" })
            Assert.Equal(SectionAudience.Both, Sezione(ver, k).Audience);
    }

    [Fact]
    public async Task Una_scelta_gia_fatta_non_si_sovrascrive()
    {
        // ⚠️ `Pilots`/`Controllers` li può aver scritti solo una persona: il catalogo non ripassa mai sopra
        // una decisione presa a mano. È l'altra metà del limite dichiarato nella carta — `Both` non si
        // distingue da «mai toccata», ma tutto il resto sì.
        var ver = await VsopVecchioAsync("LIBG");
        var nominativi = _db.DocumentSections.Single(x => x.DocumentVersionId == ver.Id && x.SectionKey == "callsigns");
        nominativi.Audience = SectionAudience.Controllers;
        await _db.SaveChangesAsync();

        await _manutenzione.ApplyCatalogAudienceDefaultsAsync();

        Assert.Equal(SectionAudience.Controllers, Sezione(ver, "callsigns").Audience);
    }

    [Fact]
    public async Task Rieseguire_il_pubblico_non_cambia_niente()
    {
        var ver = await VsopVecchioAsync("LIBG");

        Assert.Equal(4, await _manutenzione.ApplyCatalogAudienceDefaultsAsync());
        Assert.Equal(0, await _manutenzione.ApplyCatalogAudienceDefaultsAsync());
    }

    // ---- Le dodici sezioni nuove arrivano da sole ----------------------------------------------------

    [Fact]
    public async Task Le_sezioni_nuove_del_SOD_arrivano_col_passo_che_c_era_gia()
    {
        // ⚠️ Non c'è codice nuovo per aggiungerle: `AddMissingCatalogSectionsAsync` copre già i vSOP
        // militari e scende nelle sotto-sezioni. Quel che NON aveva mai fatto è arrivare a PROFONDITÀ 3 —
        // il ramo più fondo che avesse attraversato era il 2 — ed è la sola cosa che questo test misura
        // davvero.
        var ver = await VsopVecchioAsync("LIBG");

        await _manutenzione.AddMissingCatalogSectionsAsync();

        var tutte = _db.DocumentSections.AsNoTracking().Where(x => x.DocumentVersionId == ver.Id).ToList();
        var vfr = tutte.Single(x => x.SectionKey == SectionKeys.ArrivalProceduresVfr);
        var partenze = tutte.Single(x => x.SectionKey == SectionKeys.ArrivalProcedures);
        var generali = tutte.Single(x => x.SectionKey == "operationaltechnique");

        Assert.Equal(partenze.Id, vfr.ParentSectionId);
        Assert.Equal(generali.Id, partenze.ParentSectionId);
        Assert.Equal(3, vfr.Depth);
    }

    [Fact]
    public async Task E_nascono_gia_col_pubblico_che_il_catalogo_dice()
    {
        // ⚠️ Le sezioni AGGIUNTE A POSTERIORI non passano da `DocumentBirth`: se il pubblico lo scrivesse
        // solo la nascita, le marcature del SOD ci sarebbero sui vSOP nuovi e non su quelli veri.
        var ver = await VsopVecchioAsync("LIBG");

        await _manutenzione.AddMissingCatalogSectionsAsync();

        Assert.Equal(SectionAudience.Pilots, Sezione(ver, SectionKeys.RunwayThresholds).Audience);
        Assert.Equal(SectionAudience.Pilots, Sezione(ver, SectionKeys.ApronFlow).Audience);
        Assert.Equal(SectionAudience.Both, Sezione(ver, SectionKeys.AirportLayout).Audience);
    }

    [Fact]
    public async Task Le_restrizioni_nuove_si_infilano_al_posto_giusto_e_non_in_coda()
    {
        // Accodarle metterebbe «Restrizioni all'arrivo» dopo «Partenze/arrivi IFR GAT»: l'ordine del SOD
        // tiene insieme le tre restrizioni in testa alle procedure di volo.
        // ⚠️ I due passi NELL'ORDINE DELL'AVVIO, aggiungi-poi-togli: e' quello vero
        // (VipiModuleExtensions), e non e' indifferente. QRA non ha piu' un descrittore, quindi finche' e'
        // li' resta in CODA al gruppo -- l'inserimento la scavalca senza sapere che cos'e'.
        var ver = await VsopVecchioAsync("LIBG");

        await _manutenzione.AddMissingCatalogSectionsAsync();
        await _manutenzione.RemoveMilQraSectionsAsync();

        Assert.Equal(
            new[] { "takeoff", SectionKeys.ArrivalRestrictions, SectionKeys.CircuitRestrictions, "sfo",
                    "commfail", "gca", "vfrjet", "ifrsignificant", "gat" },
            Figli(ver, "flightprocedures").Select(x => x.SectionKey));
    }
}

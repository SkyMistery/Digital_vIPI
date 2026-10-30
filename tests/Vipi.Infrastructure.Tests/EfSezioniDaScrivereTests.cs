using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Le sezioni da scrivere lette dal database (carta <c>docs/feature/2026-10-18-sezioni-da-scrivere.md</c>): quale
/// versione si guarda, quale profilo ha un documento, e la vIPI di ACC contata per blocco.
/// </summary>
public class EfSezioniDaScrivereTests : IAsyncLifetime
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

    private EfSezioniDaScrivere Servizio() => new(_db);

    private async Task<(Document Doc, DocumentVersion Versione)> DocumentoAsync(string titolo, DocumentStatus stato)
    {
        var doc = new Document
        {
            Title = titolo, Type = DocumentType.Vipi, Status = stato, Language = Language.It,
            LastUpdatedAiracCycle = "2610", LastUpdatedUtc = DateTime.UtcNow,
        };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        var v = await VersioneAsync(doc, 1, stato);
        if (stato == DocumentStatus.Published) doc.CurrentVersionId = v.Id;
        await _db.SaveChangesAsync();
        return (doc, v);
    }

    private async Task<DocumentVersion> VersioneAsync(Document doc, int numero, DocumentStatus stato)
    {
        var v = new DocumentVersion
        {
            DocumentId = doc.Id, VersionNumber = numero, Status = stato, CreatedByUserId = 1,
            CreatedUtc = DateTime.UtcNow, AiracCycle = "2610",
        };
        _db.DocumentVersions.Add(v);
        await _db.SaveChangesAsync();
        return v;
    }

    private async Task<DocumentSection> SezioneAsync(DocumentVersion v, string chiave, string titolo,
        DocumentSection? madre = null, bool nascosta = false, string? testo = null, string? json = null)
    {
        var s = new DocumentSection
        {
            DocumentVersionId = v.Id, ParentSectionId = madre?.Id, Title = titolo, Order = 1,
            Depth = madre is null ? 0 : madre.Depth + 1, SectionKey = chiave, IsHidden = nascosta,
        };
        _db.DocumentSections.Add(s);
        await _db.SaveChangesAsync();
        if (testo is not null || json is not null)
        {
            _db.ContentBlocks.Add(new ContentBlock
            {
                DocumentVersionId = v.Id, SectionId = s.Id, Order = 1, Format = json is null ? BlockFormat.Prose : BlockFormat.Table,
                Body = testo, BodyJson = json, Visibility = BlockVisibility.Always,
            });
            await _db.SaveChangesAsync();
        }
        return s;
    }

    /// <summary>
    /// La vIPI di ACC: un conto per blocco, col titolo del blocco. Il blocco nascosto non conta; nell'Aerovia le aree
    /// regolamentate (in automatico) nemmeno; il segnaposto vuoto di una sezione resa dalla pagina non è contenuto.
    /// </summary>
    [Fact]
    public async Task La_vIPI_di_ACC_si_conta_per_blocco()
    {
        var (doc, v) = await DocumentoAsync("vIPI Milano", DocumentStatus.Published);

        var aerovia = await SezioneAsync(v, SectionKeys.AccBloccoAerovia, "Settori di aerovia");
        await SezioneAsync(v, "separations", "Separazioni radar", aerovia, json: "[{\"Min\":\"5 NM\"}]");
        await SezioneAsync(v, "regulated", "Aree regolamentate", aerovia);
        await SezioneAsync(v, "operationaltechnique", "Procedure generali", aerovia, json: "{\"columns\":[\"A\"],\"rows\":[]}");
        await SezioneAsync(v, "aor", "AOR", aerovia);

        var app = await SezioneAsync(v, SectionKeys.AccBloccoApp, "Milano APP");
        var gestione = await SezioneAsync(v, SectionKeys.TrafficManagement, "Gestione del traffico", app);
        await SezioneAsync(v, SectionKeys.TrafficManagementIfr, "IFR", gestione, testo: "Arrivi su MXP…");
        await SezioneAsync(v, "vfr", "VFR", gestione);

        var nascosto = await SezioneAsync(v, SectionKeys.AccBloccoApp, "Gruppo spento", nascosta: true);
        await SezioneAsync(v, "separations", "Separazioni", nascosto);

        var stato = Assert.IsType<StatoSezioni>(await Servizio().DocumentoAsync(doc.Id));

        Assert.Equal(new[] { "Settori di aerovia", "Milano APP" }, stato.Parti.Select(p => p.Titolo));
        var a = stato.Parti[0];
        Assert.Equal(1, a.Scritte);
        Assert.Equal("Procedure generali", Assert.Single(a.Vuote).Percorso);   // la tabella senza righe è vuota
        var m = stato.Parti[1];
        Assert.Equal(1, m.Scritte);
        Assert.Equal("Gestione del traffico › VFR", Assert.Single(m.Vuote).Percorso);
        Assert.Equal((2, 2), (stato.Scritte, stato.Vuote));
    }

    /// <summary>S133 (carta 2026-10-28-stato-dei-documenti §1 D4): una sezione dichiarata facoltativa dalla pagina admin
    /// non conta più; tolto l'interruttore, conta di nuovo.</summary>
    [Fact]
    public async Task Una_sezione_facoltativa_non_si_conta_piu()
    {
        var (doc, pubblicata) = await DocumentoAsync("vIPI Roma", DocumentStatus.Published);
        var a1 = await SezioneAsync(pubblicata, SectionKeys.AccBloccoAerovia, "Settori di aerovia");
        await SezioneAsync(pubblicata, "operationaltechnique", "Procedure generali", a1);
        Assert.Equal(1, (await Servizio().DocumentoAsync(doc.Id))!.Vuote);

        var store = new EfSezioniFacoltative(_db);
        await store.ImpostaAsync(SectionProfile.AccAerovia, "operationaltechnique", facoltativa: true, userId: 7);
        await store.ImpostaAsync(SectionProfile.AccAerovia, "operationaltechnique", facoltativa: true, userId: 7);   // idempotente
        Assert.Equal(0, (await Servizio().DocumentoAsync(doc.Id))!.Vuote);
        Assert.Single(await store.TutteAsync());

        await store.ImpostaAsync(SectionProfile.AccAerovia, "operationaltechnique", facoltativa: false, userId: 7);
        Assert.Equal(1, (await Servizio().DocumentoAsync(doc.Id))!.Vuote);
    }

    /// <summary>Si conta la versione di LAVORO: la bozza, se c'è, e non la pubblicata.</summary>
    [Fact]
    public async Task Conta_la_bozza_e_non_la_pubblicata()
    {
        var (doc, pubblicata) = await DocumentoAsync("vIPI Roma", DocumentStatus.Published);
        var a1 = await SezioneAsync(pubblicata, SectionKeys.AccBloccoAerovia, "Settori di aerovia");
        await SezioneAsync(pubblicata, "operationaltechnique", "Procedure generali", a1, testo: "Scritto una volta.");

        var bozza = await VersioneAsync(doc, 2, DocumentStatus.Draft);
        var a2 = await SezioneAsync(bozza, SectionKeys.AccBloccoAerovia, "Settori di aerovia");
        await SezioneAsync(bozza, "operationaltechnique", "Procedure generali", a2);

        var stato = await Servizio().DocumentoAsync(doc.Id);

        Assert.Equal(0, stato!.Scritte);
        Assert.Equal(1, stato.Vuote);
    }

    /// <summary>Il quadro per la pagina Versioni: tutti i documenti con un profilo, in una passata.</summary>
    [Fact]
    public async Task Il_quadro_ha_tutti_i_documenti_con_un_profilo()
    {
        var (acc, va) = await DocumentoAsync("vIPI Padova", DocumentStatus.Published);
        var blocco = await SezioneAsync(va, SectionKeys.AccBloccoAerovia, "Settori di aerovia");
        await SezioneAsync(va, "separations", "Separazioni radar", blocco);

        var (vloa, vv) = await DocumentoAsync("vLOA LIPP-LIRR", DocumentStatus.Published);
        vloa.Type = DocumentType.Vloa;
        await _db.SaveChangesAsync();
        await SezioneAsync(vv, "purpose", "Purpose", testo: "This letter of agreement…");
        await SezioneAsync(vv, "regulated", "Military areas coordination and management");

        var quadro = await Servizio().QuadroAsync();

        Assert.Equal((0, 1), (quadro[acc.Id].Scritte, quadro[acc.Id].Vuote));
        Assert.Equal((1, 1), (quadro[vloa.Id].Scritte, quadro[vloa.Id].Vuote));
        Assert.Null(Assert.Single(quadro[vloa.Id].Parti).Titolo);   // un documento senza blocchi ha una parte sola
    }
}

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Il destinatario di una sezione — pilota, ATC o entrambi (carta
/// <c>2026-08-27-vsop-militari.md</c> §3).
///
/// <para>
/// ⚠️ <b>Non è controllo d'accesso</b>, e va ripetuto ogni volta che se ne parla: il documento è pubblico e
/// la vista ATC la apre chiunque cambi l'indirizzo. È un <b>filtro di lettura</b>, e chi ci scrive dentro
/// deve saperlo.
/// </para>
///
/// <para>
/// ⚠️ <b>Il test che conta è quello sul DEFAULT.</b> Un flag nuovo su una tabella già piena è il punto in
/// cui questo prodotto si è già fatto male: un <c>bool</c> nasce <c>false</c> ovunque, e un enum su colonna
/// di testo nasce con la stringa vuota — che <b>non è un nome di valore</b>, e rende illeggibile ogni riga
/// esistente alla prima <c>SELECT</c>. Qui il default è <see cref="SectionAudience.Both"/> perché è lo zero
/// dell'enum, ed è dichiarato <b>nel modello</b> e non solo nella migrazione: su Postgres la colonna la
/// aggiunge il reconciler, che il valore di backfill lo legge di lì.
/// </para>
/// </summary>
public class DestinatarioSezioneTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfEditingRepository _repo = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _repo = new EfEditingRepository(_db, new Vipi.Domain.Services.AiracService(), new EfMediaMaintenance(_db));
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    /// <summary>Un documento con una versione BOZZA e una sezione: il minimo che il setter pretende.</summary>
    private async Task<int> UnaSezioneAsync()
    {
        var doc = new Document
        {
            Type = DocumentType.Vipi, Title = "T", Language = Language.It,
            LastUpdatedUtc = DateTime.UtcNow, LastUpdatedAiracCycle = "2609",
        };
        var ver = new DocumentVersion
        {
            Document = doc, VersionNumber = 1, Status = DocumentStatus.Draft,
            CreatedByUserId = 1, CreatedUtc = DateTime.UtcNow, AiracCycle = "2609",
        };
        var sez = new DocumentSection
        {
            DocumentVersion = ver, Title = "Procedure generali", Order = 1, Depth = 0,
            SectionKey = "operationaltechnique", RowVersion = Guid.NewGuid().ToByteArray(),
        };
        doc.Versions.Add(ver);
        _db.Documents.Add(doc);
        _db.DocumentSections.Add(sez);
        await _db.SaveChangesAsync();
        return sez.Id;
    }

    // ---- Il default -----------------------------------------------------------------------------------

    [Fact]
    public async Task Una_sezione_nasce_PER_TUTTI()
    {
        // Nessun documento già scritto cambia di una virgola finché qualcuno non marca qualcosa a mano.
        var id = await UnaSezioneAsync();
        var sez = await _db.DocumentSections.AsNoTracking().SingleAsync(s => s.Id == id);
        Assert.Equal(SectionAudience.Both, sez.Audience);
    }

    [Fact]
    public async Task Il_valore_scritto_su_colonna_e_il_NOME_dell_enum_non_un_numero()
    {
        // ⚠️ Gli enum di questo prodotto stanno su colonna di TESTO (SPEC §6): più leggibili e stabili di un
        // ordinale, che si sposta appena qualcuno aggiunge un valore in mezzo. Se questo test cadesse,
        // vorrebbe dire che la convenzione è saltata per questa colonna e non per le altre.
        var id = await UnaSezioneAsync();
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText = "select Audience from DocumentSections where Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        Assert.Equal("Both", (string)(await cmd.ExecuteScalarAsync())!);
    }

    // ---- Il salvataggio -------------------------------------------------------------------------------

    [Theory]
    [InlineData(SectionAudience.Pilots)]
    [InlineData(SectionAudience.Controllers)]
    [InlineData(SectionAudience.Both)]
    public async Task Il_destinatario_si_salva_e_si_rilegge(SectionAudience scelta)
    {
        var id = await UnaSezioneAsync();
        await _repo.SetSectionAudienceAsync(id, scelta);

        var sez = await _db.DocumentSections.AsNoTracking().SingleAsync(s => s.Id == id);
        Assert.Equal(scelta, sez.Audience);
    }

    [Fact]
    public async Task Si_puo_tornare_indietro_a_PER_TUTTI()
    {
        // Marcare è una scelta editoriale, e le scelte editoriali si disfano.
        var id = await UnaSezioneAsync();
        await _repo.SetSectionAudienceAsync(id, SectionAudience.Controllers);
        await _repo.SetSectionAudienceAsync(id, SectionAudience.Both);

        Assert.Equal(SectionAudience.Both,
            (await _db.DocumentSections.AsNoTracking().SingleAsync(s => s.Id == id)).Audience);
    }

    [Fact]
    public async Task Una_sezione_inesistente_lo_dice_invece_di_tacere()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _repo.SetSectionAudienceAsync(999999, SectionAudience.Pilots));
    }
}

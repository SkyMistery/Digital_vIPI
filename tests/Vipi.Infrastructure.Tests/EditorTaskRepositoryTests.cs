using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// L'elenco degli incarichi e le scritture che lo cambiano (carta
/// <c>docs/feature/2026-08-22-incarichi-cosa-sono.md</c>, difetto N6).
///
/// <para>⚠️ Il difetto che questi test presidiano: l'elenco era ordinato per <c>UpdatedUtc</c> discendente e
/// il cambio di stato riscrive <c>UpdatedUtc</c>, quindi la riga su cui si era appena agito <b>saltava in
/// cima</b> e sotto il puntatore ne arrivava un'altra. Su una pagina dove lo stato si cambia da una tendina
/// che scrive subito, senza conferma e senza undo, è il modo più diretto per toccare l'incarico sbagliato.</para>
/// </summary>
public class EditorTaskRepositoryTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfEditorTaskRepository _repo = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        _repo = new EfEditorTaskRepository(_db);
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    [Fact]
    public async Task Cambiare_stato_non_sposta_la_riga_nell_elenco()
    {
        var primo = await Crea("Aggiornare le frequenze");
        var secondo = await Crea("Bozza di vLOA");
        var terzo = await Crea("Controllare le piste");

        await _repo.UpdateStatusAsync(primo, EditorTaskStatus.InProgress);

        var elenco = await _repo.ListAllAsync();
        Assert.Equal(new[] { primo, secondo, terzo }, elenco.Select(t => t.Id));
    }

    [Fact]
    public async Task I_conclusi_vanno_in_fondo_e_le_priorita_alte_davanti()
    {
        var normale = await Crea("Normale");
        var alta = await Crea("Alta", priorita: EditorTaskPriority.High);
        var bassa = await Crea("Bassa", priorita: EditorTaskPriority.Low);
        var concluso = await Crea("Concluso", priorita: EditorTaskPriority.High);
        await _repo.UpdateStatusAsync(concluso, EditorTaskStatus.Done);

        var elenco = await _repo.ListAllAsync();

        Assert.Equal(new[] { alta, normale, bassa, concluso }, elenco.Select(t => t.Id));
    }

    /// <summary>La scadenza è un ciclo AIRAC «YYNN»: l'ordine alfabetico è quello cronologico, e chi non ha
    /// scadenza sta in fondo al proprio gruppo — non davanti a chi ce l'ha.</summary>
    [Fact]
    public async Task A_pari_priorita_ordina_la_scadenza_e_chi_non_ce_l_ha_va_in_fondo()
    {
        var senza = await Crea("Senza scadenza");
        var tardi = await Crea("Tardi", scadenza: "2611");
        var presto = await Crea("Presto", scadenza: "2609");

        var elenco = await _repo.ListAllAsync();

        Assert.Equal(new[] { presto, tardi, senza }, elenco.Select(t => t.Id));
    }

    [Fact]
    public async Task Rimettere_lo_stesso_stato_non_e_un_evento()
    {
        var id = await Crea("Fermo");
        var prima = (await _repo.GetAsync(id))!.UpdatedUtc;
        await Task.Delay(10);

        await _repo.UpdateStatusAsync(id, EditorTaskStatus.Todo);

        Assert.Equal(prima, (await _repo.GetAsync(id))!.UpdatedUtc);
    }

    [Fact]
    public async Task Riassegnare_alla_stessa_persona_non_e_un_evento()
    {
        var id = await Crea("Fermo");
        await _repo.AssignAsync(id, 555001, "Giulia Bianchi");
        var prima = (await _repo.GetAsync(id))!.UpdatedUtc;
        await Task.Delay(10);

        await _repo.AssignAsync(id, 555001, "Giulia Bianchi");

        Assert.Equal(prima, (await _repo.GetAsync(id))!.UpdatedUtc);
    }

    /// <summary>«Fatto» timbra la conclusione; tornare indietro la toglie, altrimenti resterebbe la data di
    /// una conclusione che non c'è più.</summary>
    [Fact]
    public async Task Concludere_timbra_l_ora_e_riaprire_la_cancella()
    {
        var id = await Crea("Con ritorno");

        await _repo.UpdateStatusAsync(id, EditorTaskStatus.Done);
        Assert.NotNull((await _repo.GetAsync(id))!.CompletedUtc);

        await _repo.UpdateStatusAsync(id, EditorTaskStatus.InProgress);
        Assert.Null((await _repo.GetAsync(id))!.CompletedUtc);
    }

    private Task<int> Crea(string titolo, EditorTaskPriority priorita = EditorTaskPriority.Normal,
        string? scadenza = null) =>
        _repo.AddAsync(new EditorTaskInput(titolo, null, 704798, "Chi Lavora", priorita, scadenza, null, null, null), 704798);
}

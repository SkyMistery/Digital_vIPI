using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Translation;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// La memoria di traduzione su database (carta <c>2026-08-27-documenti-bilingue.md</c> §1 e §5).
///
/// <para>
/// ⚠️ <b>Il test che conta è uno solo</b>: <see cref="La_macchina_non_sovrascrive_mai_una_correzione_umana"/>.
/// È la promessa su cui si regge tutta la funzione — «chi rivede corregge una volta e vale per sempre» — e
/// senza di essa il giro notturno cancellerebbe in silenzio il lavoro di un controllore, su ogni documento
/// che contiene quella frase. Gli altri test qui sono contorno.
/// </para>
/// </summary>
public class EfTranslationMemoryTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfTranslationMemory _memoria = default!;

    private const string It = "it";
    private const string En = "en";

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _memoria = new EfTranslationMemory(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private static IReadOnlyList<(string, string)> Una(string sorgente, string bersaglio) =>
        new[] { (sorgente, bersaglio) };

    // ---- La promessa -----------------------------------------------------------------------------------

    [Fact]
    public async Task La_macchina_non_sovrascrive_mai_una_correzione_umana()
    {
        await _memoria.SaveMachineAsync(It, En, "deepl", Una("Riporta sottovento.", "Report downwind leg."));
        await _memoria.SaveHumanAsync(It, En, "Riporta sottovento.", "Report downwind.", reviewerUserId: 123456);

        // Il giro dopo la macchina ripassa sulla stessa frase — come farebbe cambiando motore o versione.
        var scritte = await _memoria.SaveMachineAsync(It, En, "deepl", Una("Riporta sottovento.", "MACCHINA DI NUOVO"));

        var note = await _memoria.LookupAsync(It, En, new[] { TranslationText.Hash("Riporta sottovento.") });
        Assert.Equal("Report downwind.", note.Values.Single().TargetText);
        Assert.Equal(TranslationOrigin.Human, note.Values.Single().Origin);
        Assert.True(note.Values.Single().Reviewed);
        Assert.Equal(0, scritte);   // non ha scritto niente, ed e' giusto cosi'
    }

    [Fact]
    public async Task La_macchina_aggiorna_una_traduzione_automatica_precedente()
    {
        await _memoria.SaveMachineAsync(It, En, "deepl", Una("Contatta la torre.", "Contact tower."));
        await _memoria.SaveMachineAsync(It, En, "deepl", Una("Contatta la torre.", "Contact the tower."));

        var note = await _memoria.LookupAsync(It, En, new[] { TranslationText.Hash("Contatta la torre.") });
        Assert.Equal("Contact the tower.", note.Values.Single().TargetText);
        Assert.Single(_db.TranslationUnits);   // aggiornata, non duplicata
    }

    // ---- La chiave e' la frase, non il documento -------------------------------------------------------

    [Fact]
    public async Task Grafie_diverse_della_stessa_frase_sono_una_voce_sola()
    {
        // E' il dedup che si vede: a-capo Windows contro Unix, spazi doppi, bordi. Se qui nascessero due
        // righe, si pagherebbero due traduzioni e la correzione dell'una non si vedrebbe sull'altra.
        await _memoria.SaveMachineAsync(It, En, "deepl", Una("Contatta   la torre.\r\n", "Contact the tower."));
        await _memoria.SaveMachineAsync(It, En, "deepl", Una("Contatta la torre.", "Contact the tower."));
        Assert.Single(_db.TranslationUnits);
    }

    [Fact]
    public async Task Le_due_direzioni_sono_due_voci_distinte()
    {
        // La vLOA nasce in inglese: per lei l'italiano e' il BERSAGLIO. Stessa macchina, versi invertiti.
        await _memoria.SaveMachineAsync(It, En, "deepl", Una("Torre", "Tower"));
        await _memoria.SaveMachineAsync(En, It, "deepl", Una("Torre", "Qualcos'altro"));
        Assert.Equal(2, _db.TranslationUnits.Count());
    }

    [Fact]
    public async Task Una_correzione_puo_arrivare_prima_che_la_macchina_abbia_mai_tradotto()
    {
        // E' il caso di un segmento che il cancello sui dati personali ha rifiutato: non e' mai passato dal
        // motore, e vuole una persona. La riga deve nascere lo stesso.
        await _memoria.SaveHumanAsync(It, En, "Firmatario: [nome].", "Signatory: [name].", reviewerUserId: 1);
        var note = await _memoria.LookupAsync(It, En, new[] { TranslationText.Hash("Firmatario: [nome].") });
        Assert.Equal("Signatory: [name].", note.Values.Single().TargetText);
        Assert.Equal(TranslationOrigin.Human, note.Values.Single().Origin);
    }

    // ---- Letture -----------------------------------------------------------------------------------

    [Fact]
    public async Task Le_impronte_sconosciute_semplicemente_non_compaiono()
    {
        await _memoria.SaveMachineAsync(It, En, "deepl", Una("Nota", "Note"));
        var note = await _memoria.LookupAsync(It, En, new[] { TranslationText.Hash("Nota"), TranslationText.Hash("Mai vista") });
        Assert.Single(note);
    }

    [Fact]
    public async Task Un_elenco_vuoto_non_interroga_il_database() =>
        Assert.Empty(await _memoria.LookupAsync(It, En, Array.Empty<string>()));

    // ---- La guardia sul budget -------------------------------------------------------------------------

    [Fact]
    public async Task I_caratteri_spesi_contano_solo_la_macchina_e_solo_quel_motore()
    {
        const string pagata = "Contatta la torre.";
        await _memoria.SaveMachineAsync(It, En, "deepl", Una(pagata, "Contact the tower."));
        await _memoria.SaveMachineAsync(It, En, "altro", Una("un altro motore", "another engine"));
        await _memoria.SaveHumanAsync(It, En, "scritta a mano", "written by hand", reviewerUserId: 1);

        // Una correzione umana non e' stata pagata al motore, e un altro motore ha un altro budget.
        // L'attesa e' legata alla LUNGHEZZA VERA e non a un numero battuto a mano: contarlo a occhio e'
        // gia' costato un rosso.
        Assert.Equal(pagata.Length, await _memoria.CaratteriSpesiStimatiAsync("deepl"));
    }
}

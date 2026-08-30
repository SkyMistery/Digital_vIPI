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

    // ---- La pagina di revisione ----------------------------------------------------------------------

    [Fact]
    public async Task L_elenco_mette_in_cima_quelle_che_nessuno_ha_riletto()
    {
        // ⚠️ Chi apre la pagina di revisione vuole vedere cio' che nessuno ha ancora guardato. Ordinare per
        // data di inserimento gli metterebbe in cima le ultime tradotte, che non sono ne' le piu' urgenti
        // ne' le piu' lette.
        await _memoria.SaveMachineAsync(It, En, "azure", Una("Prima.", "First."));
        await _memoria.SaveMachineAsync(It, En, "azure", Una("Seconda.", "Second."));
        await _memoria.SaveHumanAsync(It, En, "Prima.", "First one.", reviewerUserId: 7);

        var righe = await _memoria.ListForReviewAsync(It, En, soloDaRileggere: false, limite: 10);

        Assert.Equal(2, righe.Count);
        Assert.Equal("Seconda.", righe[0].SourceText);          // mai riletta: prima
        Assert.Equal(TranslationOrigin.Human, righe[1].Origin);
    }

    [Fact]
    public async Task Il_filtro_mostra_solo_quelle_da_rileggere()
    {
        await _memoria.SaveMachineAsync(It, En, "azure", Una("Prima.", "First."));
        await _memoria.SaveHumanAsync(It, En, "Seconda.", "Second.", reviewerUserId: 7);

        var righe = await _memoria.ListForReviewAsync(It, En, soloDaRileggere: true, limite: 10);
        Assert.Single(righe);
        Assert.Equal("Prima.", righe[0].SourceText);
    }

    [Fact]
    public async Task Il_conteggio_dice_quante_sono_e_quante_restano()
    {
        await _memoria.SaveMachineAsync(It, En, "azure", Una("A.", "A."));
        await _memoria.SaveMachineAsync(It, En, "azure", Una("B.", "B."));
        await _memoria.SaveHumanAsync(It, En, "A.", "A rivista.", reviewerUserId: 7);

        var (totale, daRileggere) = await _memoria.ContaAsync(It, En);
        Assert.Equal(2, totale);
        Assert.Equal(1, daRileggere);

        // Una coppia di lingue senza niente non e' un errore: e' zero.
        Assert.Equal((0, 0), await _memoria.ContaAsync(En, It));
    }

    [Fact]
    public async Task Correggere_marca_la_voce_come_riletta_e_da_chi()
    {
        await _memoria.SaveMachineAsync(It, En, "azure", Una("Riporta sottovento.", "Bring it back downwind."));
        await _memoria.SaveHumanAsync(It, En, "Riporta sottovento.", "Report downwind.", reviewerUserId: 123456);

        var riga = (await _memoria.ListForReviewAsync(It, En, false, 10)).Single();
        Assert.Equal("Report downwind.", riga.TargetText);
        Assert.Equal(TranslationOrigin.Human, riga.Origin);
        Assert.NotNull(riga.ReviewedUtc);
        Assert.Equal(123456, riga.ReviewedByUserId);
    }

    // ---- La guardia sul budget -------------------------------------------------------------------------
    //
    // ⚠️ Dal 30 agosto 2026 la spesa si CONTA (tabella `TranslationSpends`) invece di dedurla dai testi
    // rimasti in memoria: un segmento tornato rotto non si salva, quindi i suoi caratteri — pagati — erano
    // invisibili al tetto (§Q16b). La deduzione non e' sparita: e' rimasta dov'e' l'unica cosa che sa dire
    // del passato, cioe' la FOTOGRAFIA iniziale, ed e' li' che i due test di prima ora guardano.

    [Fact]
    public async Task La_fotografia_iniziale_conta_QUEL_motore_e_nessun_altro()
    {
        const string pagata = "Contatta la torre.";
        await _memoria.SaveMachineAsync(It, En, "deepl", Una(pagata, "Contact the tower."));
        await _memoria.SaveMachineAsync(It, En, "altro", Una("un altro motore", "another engine"));

        // ⚠️ Una riga nata da una CORREZIONE UMANA senza che nessun motore l'avesse mai tradotta non ha
        // motore, e non conta per nessuno: non è stata pagata.
        await _memoria.SaveHumanAsync(It, En, "scritta a mano", "written by hand", reviewerUserId: 1);

        // L'attesa e' legata alla LUNGHEZZA VERA e non a un numero battuto a mano: contarlo a occhio e'
        // gia' costato un rosso.
        Assert.Equal(3, await _memoria.FotografaSpesaPregressaAsync(
            new[] { "deepl", "altro", "mai-usato" }, DateTime.UtcNow));

        Assert.Equal(pagata.Length, await _memoria.CaratteriSpesiAsync("deepl"));
        Assert.Equal("un altro motore".Length, await _memoria.CaratteriSpesiAsync("altro"));
        Assert.Equal(0, await _memoria.CaratteriSpesiAsync("mai-usato"));
    }

    /// <summary>
    /// ⚠️ <b>Il difetto che questo test chiude, e che il test di prima non poteva vedere.</b> Il conto
    /// filtrava anche su <c>Origin == Machine</c>. Ma quando una persona corregge una resa,
    /// <c>SaveHumanAsync</c> ribalta <c>Origin</c> a <c>Human</c> e lascia intatto <c>Engine</c>: quei
    /// caratteri, <b>spesi davvero</b>, sparivano dal conto. Più si revisionava, più il tetto si
    /// allargava — la difesa si allentava proprio mentre il lavoro andava avanti, e nel verso peggiore:
    /// sottostimare la spesa vuol dire sfondare una franchigia che per DeepL <b>non si rinnova</b>.
    ///
    /// <para>Il test di prima non lo prendeva perché la sua riga «scritta a mano» non era mai passata da
    /// un motore: aveva <c>Engine</c> nullo, e sarebbe uscita dal conto in tutti e due i modi.</para>
    /// </summary>
    [Fact]
    public async Task Una_correzione_umana_NON_toglie_dal_conto_i_caratteri_gia_pagati()
    {
        const string frase = "Riporta sottovento.";
        await _memoria.SaveMachineAsync(It, En, "azure", Una(frase, "Report downwind leg."));

        // Una persona rilegge e corregge: il testo cambia padrone, i caratteri restano spesi.
        await _memoria.SaveHumanAsync(It, En, frase, "Report downwind.", reviewerUserId: 123456);

        await _memoria.FotografaSpesaPregressaAsync(new[] { "azure" }, DateTime.UtcNow);
        Assert.Equal(frase.Length, await _memoria.CaratteriSpesiAsync("azure"));
    }

    // ---- Il registro vero ------------------------------------------------------------------------------

    /// <summary>
    /// ⚠️ <b>Il difetto per cui il registro esiste.</b> I caratteri di un segmento tornato rotto sono stati
    /// pagati, ma quel segmento non entra in memoria — e la spesa dedotta dalla memoria non li vedeva. Il
    /// 30 agosto 2026 una frase tornava rotta a ogni giro: 155 caratteri ogni quindici minuti, invisibili.
    /// </summary>
    [Fact]
    public async Task La_spesa_conta_ANCHE_i_segmenti_tornati_rotti()
    {
        await _memoria.RegistraSpesaAsync("azure", En, It,
            caratteri: 1000, segmenti: 5, scartati: 1, caratteriScartati: 155, DateTime.UtcNow);

        // In memoria non c'e' NIENTE — nessuna resa e' stata salvata — e il conto e' comunque mille.
        Assert.Equal(1000, await _memoria.CaratteriSpesiAsync("azure"));
    }

    [Fact]
    public async Task Le_spedizioni_si_sommano_per_motore()
    {
        await _memoria.RegistraSpesaAsync("azure", En, It, 100, 1, 0, 0, DateTime.UtcNow);
        await _memoria.RegistraSpesaAsync("azure", It, En, 250, 2, 0, 0, DateTime.UtcNow);
        await _memoria.RegistraSpesaAsync("deepl", It, En, 900, 3, 0, 0, DateTime.UtcNow);

        Assert.Equal(350, await _memoria.CaratteriSpesiAsync("azure"));
        Assert.Equal(900, await _memoria.CaratteriSpesiAsync("deepl"));
    }

    /// <summary>
    /// ⚠️ La fotografia si scrive <b>una volta sola</b>, e la domanda si fa al DATABASE: il giro vive in un
    /// processo che si riavvia, e un flag in memoria ricomincerebbe da capo scrivendo una fotografia in più
    /// — cioè gonfiando la spesa, che è il verso opposto ma altrettanto sbagliato.
    /// </summary>
    [Fact]
    public async Task La_fotografia_si_scrive_una_volta_sola()
    {
        await _memoria.SaveMachineAsync(It, En, "azure", Una("Contatta la torre.", "Contact the tower."));

        Assert.Equal(1, await _memoria.FotografaSpesaPregressaAsync(new[] { "azure" }, DateTime.UtcNow));
        var dopoLaPrima = await _memoria.CaratteriSpesiAsync("azure");

        Assert.Equal(0, await _memoria.FotografaSpesaPregressaAsync(new[] { "azure" }, DateTime.UtcNow));
        Assert.Equal(dopoLaPrima, await _memoria.CaratteriSpesiAsync("azure"));
    }

    /// <summary>La fotografia e le spedizioni si sommano: il passato non si perde, il presente si aggiunge.</summary>
    [Fact]
    public async Task Il_passato_e_il_presente_si_sommano()
    {
        const string frase = "Contatta la torre.";
        await _memoria.SaveMachineAsync(It, En, "azure", Una(frase, "Contact the tower."));
        await _memoria.FotografaSpesaPregressaAsync(new[] { "azure" }, DateTime.UtcNow);

        await _memoria.RegistraSpesaAsync("azure", It, En, 500, 2, 0, 0, DateTime.UtcNow);

        Assert.Equal(frase.Length + 500, await _memoria.CaratteriSpesiAsync("azure"));
    }
}

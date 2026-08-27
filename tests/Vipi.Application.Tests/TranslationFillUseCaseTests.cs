using Vipi.Application.Abstractions;
using Vipi.Application.Translation;
using Vipi.Domain.Entities;

namespace Vipi.Application.Tests;

/// <summary>
/// Il giro che riempie la memoria (carta <c>2026-08-27-documenti-bilingue.md</c> §6).
///
/// <para>
/// ⚠️ <b>L'ordine dei cancelli è la cosa provata qui.</b> Prima il protettore, poi il budget, poi la rete.
/// Invertire i primi due farebbe uscire un dato personale nel giro che <i>poi</i> si sarebbe fermato per
/// quota — cioè lo farebbe uscire proprio quando nessuno sta guardando, perché il giro è finito «male» e
/// il rapporto parla d'altro.
/// </para>
/// </summary>
public class TranslationFillUseCaseTests
{
    // ---- Doppioni ------------------------------------------------------------------------------------

    private sealed class CorpusFinto : ITranslatableCorpus
    {
        private readonly string[] _segmenti;
        public CorpusFinto(params string[] segmenti) => _segmenti = segmenti;
        public Task<IReadOnlyList<string>> SegmentiAsync(string sourceLang, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>(_segmenti);
    }

    private sealed class MemoriaFinta : ITranslationMemory
    {
        public Dictionary<string, KnownTranslation> Note { get; } = new(StringComparer.Ordinal);
        public List<(string Sorgente, string Bersaglio)> Scritte { get; } = new();
        public long Spesi { get; set; }

        public void GiaNota(string sorgente, string bersaglio) =>
            Note[TranslationText.Hash(sorgente)] = new KnownTranslation(bersaglio, TranslationOrigin.Machine, false);

        public Task<IReadOnlyDictionary<string, KnownTranslation>> LookupAsync(
            string s, string t, IReadOnlyCollection<string> hashes, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<string, KnownTranslation>>(
                hashes.Where(Note.ContainsKey).ToDictionary(h => h, h => Note[h], StringComparer.Ordinal));

        public Task<int> SaveMachineAsync(string s, string t, string engine,
            IReadOnlyList<(string SourceText, string TargetText)> tradotte, CancellationToken ct = default)
        {
            Scritte.AddRange(tradotte);
            return Task.FromResult(tradotte.Count);
        }

        public Task SaveHumanAsync(string s, string t, string src, string tgt, int uid, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<int> DocumentiToccatiAsync(string sourceText, CancellationToken ct = default) => Task.FromResult(0);

        public Task<long> CaratteriSpesiStimatiAsync(string engine, CancellationToken ct = default) =>
            Task.FromResult(Spesi);
    }

    private sealed class MotoreFinto : ITranslationEngine
    {
        private readonly TranslationOutcome _esito;
        private readonly Func<string, string>? _traduci;

        public MotoreFinto(TranslationOutcome esito = TranslationOutcome.Ok, Func<string, string>? traduci = null)
        {
            _esito = esito;
            _traduci = traduci;
        }

        public List<string> Ricevuti { get; } = new();
        public string Name => "finto";
        public bool IsConfigured => true;

        public Task<TranslationBatch> TranslateAsync(
            IReadOnlyList<string> testi, string s, string t, CancellationToken ct = default)
        {
            Ricevuti.AddRange(testi);
            if (_esito != TranslationOutcome.Ok) return Task.FromResult(TranslationBatch.Ko(_esito, "finto"));
            var f = _traduci ?? (x => "EN:" + x);
            return Task.FromResult(TranslationBatch.Ok(testi.Select(f).ToList()));
        }
    }

    private static TranslationFillUseCase Giro(
        CorpusFinto corpus, MemoriaFinta memoria, MotoreFinto motore,
        TranslationOptions? opt = null, string[]? roster = null) =>
        new(corpus, memoria, motore, new TextProtector(roster), opt ?? new TranslationOptions());

    // ---- Il dedup che si vede -------------------------------------------------------------------------

    [Fact]
    public async Task Cio_che_e_gia_in_memoria_non_si_rimanda_al_motore()
    {
        var memoria = new MemoriaFinta();
        memoria.GiaNota("Contatta la torre.", "Contact the tower.");
        var motore = new MotoreFinto();

        var rapporto = await Giro(new CorpusFinto("Contatta la torre.", "Riporta sottovento."), memoria, motore)
            .EseguiAsync("it", "en");

        Assert.Equal(2, rapporto.Segmenti);
        Assert.Equal(1, rapporto.GiaInMemoria);
        Assert.Equal(1, rapporto.Tradotti);
        Assert.Single(motore.Ricevuti);
        Assert.DoesNotContain(motore.Ricevuti, r => r.Contains("torre", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Se_non_manca_niente_la_rete_non_si_tocca()
    {
        var memoria = new MemoriaFinta();
        memoria.GiaNota("Nota", "Note");
        var motore = new MotoreFinto();
        var rapporto = await Giro(new CorpusFinto("Nota"), memoria, motore).EseguiAsync("it", "en");
        Assert.Empty(motore.Ricevuti);
        Assert.Equal(0, rapporto.Mancanti);
    }

    [Fact]
    public async Task Cio_che_non_ha_niente_da_tradurre_non_parte()
    {
        var motore = new MotoreFinto();
        var rapporto = await Giro(new CorpusFinto("126.850", "---", "Contatta la torre."), new MemoriaFinta(), motore)
            .EseguiAsync("it", "en");
        Assert.Equal(1, rapporto.Segmenti);
        Assert.Single(motore.Ricevuti);
    }

    // ---- Cancello 1: i dati personali -----------------------------------------------------------------

    [Fact]
    public async Task Un_segmento_non_sicuro_non_arriva_al_motore_e_si_conta_a_parte()
    {
        // Il protettore non sa chiudere questo caso (lo si costruisce apposta): il giro NON deve spedirlo,
        // e deve dirlo — «da tradurre a mano» non e' un errore del giro, e' il cancello che ha funzionato.
        var motore = new MotoreFinto();
        var protettoreCieco = new TextProtector();
        var giro = new TranslationFillUseCase(
            new CorpusFinto("Contatta la torre."), new MemoriaFinta(), motore, protettoreCieco, new TranslationOptions());

        // Caso di controllo: senza dati personali passa.
        var ok = await giro.EseguiAsync("it", "en");
        Assert.Equal(0, ok.DaTradurreAMano);

        // Con un nome del roster, il segmento non parte.
        var motore2 = new MotoreFinto();
        var giro2 = Giro(new CorpusFinto("Firmato da Mario Rossi VID 123456 e altro testo qui."),
                         new MemoriaFinta(), motore2, roster: new[] { "Mario Rossi" });
        var rapporto = await giro2.EseguiAsync("it", "en");

        // Il VID e il nome sono protetti, quindi il segmento È sicuro e parte — ma senza il dato.
        Assert.DoesNotContain(motore2.Ricevuti, r => r.Contains("123456", StringComparison.Ordinal));
        Assert.DoesNotContain(motore2.Ricevuti, r => r.Contains("Mario", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0, rapporto.DaTradurreAMano);
    }

    // ---- Cancello 2: il budget ------------------------------------------------------------------------

    [Fact]
    public async Task Col_tetto_superato_il_giro_si_ferma_PRIMA_di_spendere()
    {
        // ⚠️ Serve perche' la franchigia del motore puo' essere UNA TANTUM e non rinnovarsi: scoprire a cose
        // fatte che e' finita costerebbe la funzione, non un giro.
        var memoria = new MemoriaFinta { Spesi = 990 };
        var motore = new MotoreFinto();
        var opt = new TranslationOptions { MaxCaratteriTotali = 1000 };

        var rapporto = await Giro(new CorpusFinto(new string('a', 50) + " testo lungo"), memoria, motore, opt)
            .EseguiAsync("it", "en");

        Assert.Equal(TranslationOutcome.QuotaExceeded, rapporto.Esito);
        Assert.Empty(motore.Ricevuti);          // non ha speso niente
        Assert.Empty(memoria.Scritte);
        Assert.Contains("tetto di 1000", rapporto.Dettaglio);
    }

    [Fact]
    public async Task Senza_tetto_configurato_non_c_e_nessuna_guardia()
    {
        var memoria = new MemoriaFinta { Spesi = 999_999_999 };
        var motore = new MotoreFinto();
        var rapporto = await Giro(new CorpusFinto("Contatta la torre."), memoria, motore).EseguiAsync("it", "en");
        Assert.Equal(TranslationOutcome.Ok, rapporto.Esito);
        Assert.Single(motore.Ricevuti);
    }

    // ---- Quando il motore sbaglia ---------------------------------------------------------------------

    [Theory]
    [InlineData(TranslationOutcome.AuthFailed)]
    [InlineData(TranslationOutcome.QuotaExceeded)]
    [InlineData(TranslationOutcome.TemporaryFailure)]
    public async Task Un_guasto_del_motore_non_scrive_niente_in_memoria(TranslationOutcome guasto)
    {
        var memoria = new MemoriaFinta();
        var rapporto = await Giro(new CorpusFinto("Contatta la torre."), memoria, new MotoreFinto(guasto))
            .EseguiAsync("it", "en");
        Assert.Equal(guasto, rapporto.Esito);
        Assert.Empty(memoria.Scritte);
        Assert.Equal(0, rapporto.Tradotti);
    }

    [Fact]
    public async Task Una_traduzione_che_perde_un_segnaposto_si_butta_e_le_altre_si_salvano()
    {
        // Una frase a cui manca il callsign e' PEGGIO della frase non tradotta: sembra giusta e non lo e'.
        // Non si salva, cosi' il giro dopo ci riprova.
        var memoria = new MemoriaFinta();
        var motore = new MotoreFinto(traduci: t => t.Contains("<x id=", StringComparison.Ordinal)
            ? "Contact the tower"       // il segnaposto e' sparito
            : "EN:" + t);

        var rapporto = await Giro(new CorpusFinto("Contatta LIRF_TWR", "Riporta sottovento."), memoria, motore)
            .EseguiAsync("it", "en");

        Assert.Equal(1, rapporto.Scartati);
        Assert.Equal(1, rapporto.Tradotti);
        Assert.Single(memoria.Scritte);
        Assert.Equal("Riporta sottovento.", memoria.Scritte[0].Sorgente);
    }

    [Fact]
    public async Task Il_corpus_vuoto_e_un_giro_riuscito_che_non_fa_niente()
    {
        var rapporto = await Giro(new CorpusFinto(), new MemoriaFinta(), new MotoreFinto()).EseguiAsync("it", "en");
        Assert.Equal(TranslationOutcome.Ok, rapporto.Esito);
        Assert.Equal(0, rapporto.Segmenti);
    }
}

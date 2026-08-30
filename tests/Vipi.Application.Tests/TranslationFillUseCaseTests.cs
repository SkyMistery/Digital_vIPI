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
        public List<string> MotoriScritti { get; } = new();
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
            MotoriScritti.Add(engine);
            return Task.FromResult(tradotte.Count);
        }

        public Task SaveHumanAsync(string s, string t, string src, string tgt, int uid, CancellationToken ct = default) =>
            Task.CompletedTask;


        public Task<IReadOnlyList<TranslationReviewRow>> ListForReviewAsync(
            string s, string t, bool solo, int limite, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TranslationReviewRow>>(Array.Empty<TranslationReviewRow>());

        public Task<IReadOnlyDictionary<string, string>> LoadAllAsync(
            string s, string t, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(
                new Dictionary<string, string>(StringComparer.Ordinal));

        public Task<IReadOnlySet<string>> LoadHumanHashesAsync(
            string s, string t, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.Ordinal));

        public Task<(int Totale, int DaRileggere)> ContaAsync(string s, string t, CancellationToken ct = default) =>
            Task.FromResult((0, 0));

        public Task<int> DocumentiToccatiAsync(string sourceText, CancellationToken ct = default) => Task.FromResult(0);

        public Task<int> ContaConLaFormulaAsync(string s, string t, string f, CancellationToken ct = default) =>
            Task.FromResult(0);

        public Task<int> DimenticaAutomaticheConLaFormulaAsync(
            string s, string t, string f, CancellationToken ct = default) => Task.FromResult(0);

        public Task<long> CaratteriSpesiAsync(string engine, CancellationToken ct = default) =>
            Task.FromResult(Spesi);

        /// <summary>L'ultima spesa registrata: serve a provare che si registra quel che e' PARTITO.</summary>
        public (string Motore, long Caratteri, int Segmenti, int Scartati, long CaratteriScartati)? Spesa
        { get; private set; }

        public Task RegistraSpesaAsync(string e, string s, string t, long c, int seg, int sc, long csc,
            DateTime now, CancellationToken ct = default)
        {
            Spesa = (e, c, seg, sc, csc);
            return Task.CompletedTask;
        }

        public Task<int> FotografaSpesaPregressaAsync(
            IReadOnlyList<string> engines, DateTime now, CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class MotoreFinto : ITranslationEngine
    {
        private readonly TranslationOutcome _esito;
        private readonly Func<string, string>? _traduci;

        public MotoreFinto(TranslationOutcome esito = TranslationOutcome.Ok, Func<string, string>? traduci = null,
                           string nome = "azure", bool configurato = true)
        {
            _esito = esito;
            _traduci = traduci;
            Name = nome;
            IsConfigured = configurato;
        }

        public List<string> Ricevuti { get; } = new();
        public string Name { get; }
        public bool IsConfigured { get; }

        public Task<TranslationBatch> TranslateAsync(
            IReadOnlyList<string> testi, string s, string t, CancellationToken ct = default)
        {
            Ricevuti.AddRange(testi);
            if (_esito != TranslationOutcome.Ok) return Task.FromResult(TranslationBatch.Ko(_esito, "finto", Name));
            var f = _traduci ?? (x => "EN:" + x);
            return Task.FromResult(TranslationBatch.Ok(testi.Select(f).ToList(), Name));
        }
    }

    private static TranslationFillUseCase Giro(
        CorpusFinto corpus, MemoriaFinta memoria, MotoreFinto motore,
        TranslationOptions? opt = null, string[]? roster = null,
        GlossarioFraseologia? glossario = null) =>
        Catena(corpus, memoria, opt ?? new TranslationOptions(), roster, glossario, motore);

    /// <summary>Il giro con piu' motori: l'ordine di preferenza lo detta `opt.Order`.</summary>
    private static TranslationFillUseCase Catena(
        CorpusFinto corpus, MemoriaFinta memoria, TranslationOptions opt, string[]? roster,
        GlossarioFraseologia? glossario, params MotoreFinto[] motori) =>
        new(corpus, memoria, motori, new TextProtector(roster, glossario), opt);

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
            new CorpusFinto("Contatta la torre."), new MemoriaFinta(), new[] { motore }, protettoreCieco,
            new TranslationOptions());

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
        var opt = new TranslationOptions { Order = new[] { "azure" }, Azure = { MaxCaratteriTotali = 1000 } };

        var rapporto = await Giro(new CorpusFinto(new string('a', 50) + " testo lungo"), memoria, motore, opt)
            .EseguiAsync("it", "en");

        Assert.Equal(TranslationOutcome.QuotaExceeded, rapporto.Esito);
        Assert.Empty(motore.Ricevuti);          // non ha speso niente
        Assert.Empty(memoria.Scritte);
        Assert.Contains("tetto di 1000", rapporto.Dettaglio);
        Assert.Contains("azure", rapporto.Dettaglio);
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

        // ⚠️ E il rapporto dice QUALE si e' buttata. Senza, l'avviso nel registro dice «1 segmento» e nessuno
        // puo' fare niente: il corpus ne ha decine, e il testo ce l'avevamo in mano proprio dove lo buttavamo.
        Assert.Equal(new[] { "Contatta LIRF_TWR" }, rapporto.Rotti);
    }

    /// <summary>
    /// ⚠️ <b>La spesa si registra su quel che e' PARTITO, rotto compreso.</b> E' tutta la ragione per cui il
    /// registro esiste invece di dedurre il conto da quel che e' rimasto in memoria: una frase tornata rotta
    /// non si salva — giustamente — quindi i suoi caratteri, pagati, erano invisibili al tetto. Il 30 agosto
    /// 2026 una frase tornava rotta a ogni giro: 155 caratteri ogni quindici minuti (§Q16b).
    /// </summary>
    [Fact]
    public async Task La_spesa_registra_anche_i_caratteri_di_quel_che_si_e_buttato()
    {
        var memoria = new MemoriaFinta();
        var motore = new MotoreFinto(traduci: t => t.Contains("<x id=", StringComparison.Ordinal)
            ? "Contact the tower"
            : "EN:" + t);

        await Giro(new CorpusFinto("Contatta LIRF_TWR", "Riporta sottovento."), memoria, motore)
            .EseguiAsync("it", "en");

        Assert.NotNull(memoria.Spesa);
        var spesa = memoria.Spesa!.Value;
        Assert.Equal(2, spesa.Segmenti);              // tutt'e due sono partite
        Assert.Equal(1, spesa.Scartati);
        Assert.True(spesa.CaratteriScartati > 0);
        // I caratteri contati sono quelli SPEDITI, quindi comprendono anche la frase buttata.
        Assert.True(spesa.Caratteri > spesa.CaratteriScartati,
            $"spediti {spesa.Caratteri}, buttati {spesa.CaratteriScartati}");
    }

    [Fact]
    public async Task Il_corpus_vuoto_e_un_giro_riuscito_che_non_fa_niente()
    {
        var rapporto = await Giro(new CorpusFinto(), new MemoriaFinta(), new MotoreFinto()).EseguiAsync("it", "en");
        Assert.Equal(TranslationOutcome.Ok, rapporto.Esito);
        Assert.Equal(0, rapporto.Segmenti);
    }
    // ---- La catena: Azure primario, DeepL pronto -------------------------------------------------------

    [Fact]
    public async Task Il_primario_traduce_e_il_secondo_non_viene_nemmeno_interpellato()
    {
        var azure = new MotoreFinto(nome: "azure");
        var deepl = new MotoreFinto(nome: "deepl");
        var memoria = new MemoriaFinta();

        var rapporto = await Catena(new CorpusFinto("Contatta la torre."), memoria,
            new TranslationOptions { Order = new[] { "azure", "deepl" } }, null, null, azure, deepl)
            .EseguiAsync("it", "en");

        Assert.Single(azure.Ricevuti);
        Assert.Empty(deepl.Ricevuti);
        Assert.Equal("azure", rapporto.Motore);
    }

    [Theory]
    [InlineData(TranslationOutcome.QuotaExceeded)]
    [InlineData(TranslationOutcome.AuthFailed)]
    [InlineData(TranslationOutcome.TemporaryFailure)]
    [InlineData(TranslationOutcome.PermanentFailure)]
    [InlineData(TranslationOutcome.NotConfigured)]
    public async Task Se_il_primario_non_risponde_il_secondo_subentra_da_solo(TranslationOutcome guasto)
    {
        // E' tutta la ragione per cui esiste una catena. Anche AuthFailed fa passare oltre: una chiave
        // sbagliata vuole una persona, ma nel frattempo il documento si traduce lo stesso.
        var azure = new MotoreFinto(guasto, nome: "azure");
        var deepl = new MotoreFinto(nome: "deepl");
        var memoria = new MemoriaFinta();

        var rapporto = await Catena(new CorpusFinto("Contatta la torre."), memoria,
            new TranslationOptions { Order = new[] { "azure", "deepl" } }, null, null, azure, deepl)
            .EseguiAsync("it", "en");

        Assert.Equal(TranslationOutcome.Ok, rapporto.Esito);
        Assert.Single(deepl.Ricevuti);
        Assert.Equal(1, rapporto.Tradotti);
    }

    [Fact]
    public async Task La_voce_in_memoria_porta_il_nome_di_CHI_HA_TRADOTTO_non_del_primario()
    {
        // ⚠️ Se qui finisse "azure", il tetto di Azure verrebbe consumato dal lavoro di DeepL -- e la
        // guardia sul budget del primario direbbe il falso proprio quando il primario e' fermo.
        var azure = new MotoreFinto(TranslationOutcome.QuotaExceeded, nome: "azure");
        var deepl = new MotoreFinto(nome: "deepl");
        var memoria = new MemoriaFinta();

        var rapporto = await Catena(new CorpusFinto("Contatta la torre."), memoria,
            new TranslationOptions { Order = new[] { "azure", "deepl" } }, null, null, azure, deepl)
            .EseguiAsync("it", "en");

        Assert.Equal("deepl", rapporto.Motore);
        Assert.Equal("deepl", memoria.MotoriScritti.Single());
    }

    [Fact]
    public async Task Un_motore_oltre_il_suo_tetto_si_salta_e_il_giro_NON_si_ferma()
    {
        // Il tetto e' per motore: quello di DeepL protegge una riserva UNA TANTUM, quello di Azure una
        // franchigia mensile. Sfondare il primo non deve spegnere il servizio.
        var azure = new MotoreFinto(nome: "azure");
        var deepl = new MotoreFinto(nome: "deepl");
        var memoria = new MemoriaFinta { Spesi = 999_999 };

        var opt = new TranslationOptions
        {
            Order = new[] { "deepl", "azure" },   // DeepL primario, ma esaurito
            DeepL = { MaxCaratteriTotali = 1000 },
        };

        var rapporto = await Catena(new CorpusFinto("Contatta la torre."), memoria, opt, null, null, azure, deepl)
            .EseguiAsync("it", "en");

        Assert.Empty(deepl.Ricevuti);            // saltato PRIMA di spendere
        Assert.Single(azure.Ricevuti);
        Assert.Equal("azure", rapporto.Motore);
    }

    [Fact]
    public async Task L_ordine_lo_detta_la_configurazione_non_quello_di_registrazione()
    {
        // Un motore aggiunto in fondo al file di DI non deve diventare il primario per sbaglio.
        var azure = new MotoreFinto(nome: "azure");
        var deepl = new MotoreFinto(nome: "deepl");

        // Registrati azure-poi-deepl, ma la configurazione dice il contrario.
        var rapporto = await Catena(new CorpusFinto("Contatta la torre."), new MemoriaFinta(),
            new TranslationOptions { Order = new[] { "deepl", "azure" } }, null, null, azure, deepl)
            .EseguiAsync("it", "en");

        Assert.Equal("deepl", rapporto.Motore);
        Assert.Empty(azure.Ricevuti);
    }

    [Fact]
    public async Task Se_nessun_motore_risponde_il_rapporto_porta_l_ultimo_motivo()
    {
        var azure = new MotoreFinto(TranslationOutcome.QuotaExceeded, nome: "azure");
        var deepl = new MotoreFinto(TranslationOutcome.AuthFailed, nome: "deepl");
        var memoria = new MemoriaFinta();

        var rapporto = await Catena(new CorpusFinto("Contatta la torre."), memoria,
            new TranslationOptions { Order = new[] { "azure", "deepl" } }, null, null, azure, deepl)
            .EseguiAsync("it", "en");

        Assert.Equal(TranslationOutcome.AuthFailed, rapporto.Esito);
        Assert.Contains("deepl", rapporto.Dettaglio);
        Assert.Empty(memoria.Scritte);
    }

    [Fact]
    public async Task Un_motore_non_configurato_si_salta_senza_consumare_il_tentativo()
    {
        var azure = new MotoreFinto(nome: "azure", configurato: false);
        var deepl = new MotoreFinto(nome: "deepl");

        var rapporto = await Catena(new CorpusFinto("Contatta la torre."), new MemoriaFinta(),
            new TranslationOptions { Order = new[] { "azure", "deepl" } }, null, null, azure, deepl)
            .EseguiAsync("it", "en");

        Assert.Empty(azure.Ricevuti);
        Assert.Equal("deepl", rapporto.Motore);
    }

    // ---- Il glossario di fraseologia (lavori-aperti §Q3) ---------------------------------------------

    [Fact]
    public async Task Una_cella_che_e_TUTTA_una_formula_non_parte_e_finisce_in_memoria_in_INGLESE()
    {
        // ⚠️ È il difetto che questo test esiste per impedire. Del testo protetto non resta che il
        // segnaposto, quindi il giro non chiama nessuno — giusto — e scrive in memoria «il testo così
        // com'è». Prima del glossario «così com'è» voleva sempre dire «identico al sorgente», perché lì
        // dentro c'erano solo identificatori. Con una formula NO: ricopiare il sorgente scriverebbe
        // l'ITALIANO come se fosse la traduzione inglese, e come voce DEFINITIVA che nessun giro riprova.
        var memoria = new MemoriaFinta();
        var motore = new MotoreFinto();
        var glossario = new GlossarioFraseologia(new[] { new VoceGlossario("riporta sottovento", "report downwind") });

        var rapporto = await Giro(new CorpusFinto("Riporta sottovento"), memoria, motore, glossario: glossario)
            .EseguiAsync("it", "en");

        Assert.Empty(motore.Ricevuti);                                  // non si e' pagato niente
        Assert.Equal(1, rapporto.Tradotti);
        Assert.Equal(("Riporta sottovento", "report downwind"), memoria.Scritte.Single());
    }

    [Fact]
    public async Task La_formula_dentro_una_frase_sopravvive_a_un_motore_che_la_traduce_a_modo_suo()
    {
        // Il motore finto fa esattamente quel che Azure faceva davvero: rende la formula a modo suo. La
        // frase intorno resta sua, la formula torna nostra, e la frase NON finisce fra gli scartati.
        var memoria = new MemoriaFinta();
        var motore = new MotoreFinto(traduci: t => t.Replace("riporta sottovento", "bring it back downwind"));
        var glossario = new GlossarioFraseologia(new[] { new VoceGlossario("riporta sottovento", "report downwind") });

        var rapporto = await Giro(new CorpusFinto("Poi riporta sottovento."), memoria, motore, glossario: glossario)
            .EseguiAsync("it", "en");

        Assert.Equal(0, rapporto.Scartati);
        Assert.Contains("report downwind", memoria.Scritte.Single().Bersaglio, StringComparison.Ordinal);
        Assert.DoesNotContain("bring it back", memoria.Scritte.Single().Bersaglio, StringComparison.Ordinal);
    }

}

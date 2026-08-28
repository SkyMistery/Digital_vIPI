using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Application.Translation;
using Vipi.Domain.Entities;

namespace Vipi.Application.Tests;

/// <summary>
/// I testi che stanno <b>fuori dai documenti</b>: le descrizioni delle aree regolamentate, che vivono
/// nell'anagrafica e finiscono dentro sezioni <b>derivate</b> (carta
/// <c>2026-08-27-documenti-bilingue.md</c> §4).
///
/// <para>
/// ⚠️ <b>Perché non bastava il traduttore di documento.</b> Quello lavora sul <c>DocumentView</c>, e le
/// sezioni rese dalla pagina lì non hanno corpo: il contenuto lo compone il servizio di derivazione
/// leggendo l'anagrafica. Senza questo pezzo un lettore italiano vedeva il documento tradotto e dentro le
/// aree regolamentate ancora in inglese — la stessa schermata a metà, solo in un'altra sezione.
/// </para>
///
/// <para>
/// Misurato sul <c>vipi.db</c> reale il 28 agosto 2026: 230 aree per <b>35.056 caratteri</b> — più
/// dell'intero corpus editoriale — ma appena <b>9 descrizioni e 6 attivazioni distinte</b>. Il dedup rende
/// questo pezzo quasi gratuito: una chiamata al motore, una volta.
/// </para>
/// </summary>
public class TestiFuoriDaiDocumentiTests
{
    private sealed class MemoriaFinta : ITranslationMemory
    {
        private readonly Dictionary<string, string> _note = new(StringComparer.Ordinal);
        public int LettureTotali { get; private set; }

        public MemoriaFinta Nota(string sorgente, string bersaglio)
        {
            _note[TranslationText.Hash(sorgente)] = bersaglio;
            return this;
        }

        public Task<IReadOnlyDictionary<string, string>> LoadAllAsync(
            string s, string t, CancellationToken ct = default)
        {
            LettureTotali++;
            return Task.FromResult<IReadOnlyDictionary<string, string>>(_note);
        }

        public Task<IReadOnlySet<string>> LoadHumanHashesAsync(
            string s, string t, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.Ordinal));

        public Task<IReadOnlyDictionary<string, KnownTranslation>> LookupAsync(
            string s, string t, IReadOnlyCollection<string> h, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<string, KnownTranslation>>(
                new Dictionary<string, KnownTranslation>(StringComparer.Ordinal));
        public Task<int> SaveMachineAsync(string s, string t, string e,
            IReadOnlyList<(string SourceText, string TargetText)> v, CancellationToken ct = default) => Task.FromResult(0);
        public Task SaveHumanAsync(string s, string t, string a, string b, int u, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<TranslationReviewRow>> ListForReviewAsync(
            string s, string t, bool solo, int limite, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TranslationReviewRow>>(Array.Empty<TranslationReviewRow>());
        public Task<(int Totale, int DaRileggere)> ContaAsync(string s, string t, CancellationToken ct = default) =>
            Task.FromResult((0, 0));
        public Task<int> DocumentiToccatiAsync(string s, CancellationToken ct = default) => Task.FromResult(0);
        public Task<long> CaratteriSpesiStimatiAsync(string e, CancellationToken ct = default) => Task.FromResult(0L);
    }

    /// <summary>Un testo vero, copiato dal database: le aree le scrive IVAO, in inglese.</summary>
    private const string DescrizioneVera = "Reserved and designated for exclusive use by SO flights only.";
    private const string AttivazioneVera =
        "by connected ATC or announced by NOTAM (requested at least 48hs in advance by IVAO users)";

    private static ReadingLanguageContext Lettore(string lingua)
    {
        var ctx = new ReadingLanguageContext();
        ctx.Rendering(lingua);   // non si chiude: il contesto vive quanto il test
        return ctx;
    }

    private static SpecialAreaDetail Area(string? desc, string? att) =>
        new("LI-R59", "Capo Frasca", "R", desc, att, null, null, null);

    // ---- La traduzione dei testi d'anagrafica --------------------------------------------------------

    [Fact]
    public async Task Un_area_si_legge_nella_lingua_di_chi_guarda()
    {
        var memoria = new MemoriaFinta()
            .Nota(DescrizioneVera, "Riservata all'uso esclusivo dei voli SO.")
            .Nota(AttivazioneVera, "da un ATC connesso o annunciata con NOTAM");

        var traduci = await new TranslationLookup(memoria, Lettore("it")).DallaSorgenteAsync();
        var viste = SpecialAreaProjection.Build(
            new[] { Area(DescrizioneVera, AttivazioneVera) }, new[] { "LI-R59" }, traduci);

        Assert.Equal("Riservata all'uso esclusivo dei voli SO.", viste[0].Description);
        Assert.Equal("da un ATC connesso o annunciata con NOTAM", viste[0].ActivationDetails);
    }

    [Fact]
    public async Task Il_NOME_dell_area_non_si_traduce_mai()
    {
        // ⚠️ «LI-R59 Capo Frasca» è un identificatore: tradurlo renderebbe irriconoscibile la stessa area
        // fra la carta e il documento, che è peggio di lasciarla in inglese.
        var memoria = new MemoriaFinta().Nota("Capo Frasca", "NOME TRADOTTO");
        var traduci = await new TranslationLookup(memoria, Lettore("it")).DallaSorgenteAsync();
        var viste = SpecialAreaProjection.Build(new[] { Area("d", "a") }, new[] { "LI-R59" }, traduci);

        Assert.Equal("Capo Frasca", viste[0].Name);
        Assert.Equal("LI-R59", viste[0].IvaoId);
    }

    [Fact]
    public async Task Quel_che_non_e_tradotto_resta_nella_lingua_della_sorgente()
    {
        // Vale qui come per i documenti: a chiazze si legge male ma si legge; coi buchi mente.
        var traduci = await new TranslationLookup(new MemoriaFinta(), Lettore("it")).DallaSorgenteAsync();
        var viste = SpecialAreaProjection.Build(
            new[] { Area(DescrizioneVera, null) }, new[] { "LI-R59" }, traduci);

        Assert.Equal(DescrizioneVera, viste[0].Description);
        Assert.Null(viste[0].ActivationDetails);
    }

    // ---- Quel che NON deve costare niente -------------------------------------------------------------

    [Fact]
    public async Task Leggere_in_inglese_testi_inglesi_non_tocca_il_database()
    {
        // La sorgente scrive in inglese: per un lettore inglese non c'e' niente da fare, e non deve
        // costare una query.
        var memoria = new MemoriaFinta();
        var traduci = await new TranslationLookup(memoria, Lettore("en")).DallaSorgenteAsync();

        Assert.Equal(0, memoria.LettureTotali);
        Assert.Equal(DescrizioneVera, traduci(DescrizioneVera));
    }

    [Fact]
    public async Task Senza_contesto_di_lettura_non_si_traduce_e_non_si_legge_niente()
    {
        var memoria = new MemoriaFinta().Nota(DescrizioneVera, "TRADOTTA");
        var traduci = await new TranslationLookup(memoria, lingua: null).DallaSorgenteAsync();

        Assert.Equal(0, memoria.LettureTotali);
        Assert.Equal(DescrizioneVera, traduci(DescrizioneVera));
    }

    [Fact]
    public async Task La_coppia_di_lingue_si_carica_UNA_volta_per_richiesta()
    {
        // ⚠️ Chi proietta scopre i testi che gli servono strada facendo, quindi non puo' passare un elenco
        // di impronte prima. La risposta e' caricare la coppia intera una volta -- misurato, 90 righe --
        // invece di una query per area su 230 aree.
        var memoria = new MemoriaFinta();
        var lookup = new TranslationLookup(memoria, Lettore("it"));

        await lookup.DallaSorgenteAsync();
        await lookup.DallaSorgenteAsync();
        await lookup.DallaSorgenteAsync();

        Assert.Equal(1, memoria.LettureTotali);
    }

    /// <summary>
    /// ⚠️ <b>«Scoped» non vuol dire «per richiesta» dappertutto.</b> Sulle pagine pubbliche, SSR statiche,
    /// lo scope è la richiesta e la cache vive un istante. Dentro un <b>circuito Blazor</b> — l'editor — lo
    /// scope vive quanto il circuito, cioè <b>ore</b>: senza scadenza, una correzione fatta nel pannello
    /// Traduzione non si sarebbe vista fino al circuito successivo. Nessun errore, solo un testo vecchio —
    /// che è il modo in cui questa trappola si è già presentata su questo prodotto.
    /// </summary>
    [Fact]
    public async Task Dopo_la_scadenza_la_cache_si_rilegge()
    {
        var memoria = new MemoriaFinta();
        var orologio = new OrologioFinto(DateTimeOffset.UnixEpoch);
        var lookup = new TranslationLookup(memoria, Lettore("it"), orologio);

        await lookup.DallaSorgenteAsync();
        await lookup.DallaSorgenteAsync();
        Assert.Equal(1, memoria.LettureTotali);   // dentro la finestra: una lettura sola

        // Un secondo PRIMA della scadenza: ancora la stessa.
        orologio.Avanza(TranslationLookup.Freschezza - TimeSpan.FromSeconds(1));
        await lookup.DallaSorgenteAsync();
        Assert.Equal(1, memoria.LettureTotali);

        // Oltre la scadenza: si rilegge, ed è così che chi ha appena corretto rivede la sua correzione.
        orologio.Avanza(TimeSpan.FromSeconds(2));
        await lookup.DallaSorgenteAsync();
        Assert.Equal(2, memoria.LettureTotali);
    }

    /// <summary>Un orologio che si sposta a mano: una scadenza provata con un'attesa vera è un test lento
    /// e capriccioso.</summary>
    private sealed class OrologioFinto : TimeProvider
    {
        private DateTimeOffset _adesso;
        public OrologioFinto(DateTimeOffset da) => _adesso = da;
        public override DateTimeOffset GetUtcNow() => _adesso;
        public void Avanza(TimeSpan quanto) => _adesso += quanto;
    }

    [Fact]
    public async Task Senza_traduttore_la_proiezione_si_comporta_come_prima()
    {
        // Retro-compatibilita': il parametro e' opzionale, e i chiamanti vecchi non cambiano di una virgola.
        var viste = SpecialAreaProjection.Build(
            new[] { Area(DescrizioneVera, AttivazioneVera) }, new[] { "LI-R59" });

        Assert.Equal(DescrizioneVera, viste[0].Description);
        Assert.Equal(AttivazioneVera, viste[0].ActivationDetails);
        await Task.CompletedTask;
    }
}

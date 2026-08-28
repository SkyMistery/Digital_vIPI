using Vipi.Application.Abstractions;
using Vipi.Application.Content;

namespace Vipi.Application.Translation;

/// <summary>
/// Il traduttore per i testi che stanno <b>fuori dai documenti</b>: le descrizioni delle aree regolamentate,
/// le note delle piste, le condizioni delle SID — prosa scritta a mano che vive nell'anagrafica e finisce
/// dentro sezioni <b>derivate</b> (carta <c>docs/feature/2026-08-27-documenti-bilingue.md</c> §4).
///
/// <para>
/// ⚠️ <b>Perché non basta il traduttore di documento.</b> Quello lavora sul <c>DocumentView</c>, e le sezioni
/// rese dalla pagina lì non hanno corpo: il loro contenuto lo compone il servizio di derivazione leggendo
/// l'anagrafica. Senza questo pezzo, un lettore italiano vede il documento tradotto e dentro le aree
/// regolamentate ancora in inglese — la stessa schermata a metà di prima, solo in un'altra sezione.
/// </para>
///
/// <para>
/// ⚠️ <b>La lingua sorgente non è quella del documento.</b> Una descrizione d'area non appartiene a nessun
/// documento: appartiene alla <b>sorgente</b>, che è IVAO e scrive in inglese. La stessa area compare in una
/// vIPI italiana e in una vLOA inglese, e il suo testo è lo stesso in entrambe.
/// </para>
///
/// <para>
/// ⚠️ <b>Scoped, e carica la coppia INTERA una volta.</b> Il chiamante scopre quali testi gli servono mentre
/// proietta, quindi non può passare un elenco di impronte prima. Misurato: la memoria intera oggi sono 90
/// righe, e le 230 aree regolamentate del database contengono <b>9 descrizioni distinte e 6 attivazioni</b>.
/// Una lettura sola per richiesta costa meno di una query per area.
/// </para>
///
/// <para>
/// ⚠️ <b>Ma «scoped» non vuol dire «per richiesta» dappertutto.</b> Sulle pagine pubbliche, che sono SSR
/// statiche, lo scope è la richiesta e la cache vive un istante. Dentro un <b>circuito Blazor</b> — l'editor
/// — lo scope vive quanto il circuito, cioè <b>ore</b>: la cache caricata alla prima proiezione resterebbe lì
/// tutto il pomeriggio, e una correzione fatta nel pannello Traduzione non si vedrebbe fino al circuito
/// successivo. È la trappola già pagata su questo prodotto («AddScoped = cache di sessione»), e qui si
/// presenta con la faccia buona: nessun errore, solo un testo vecchio.
/// </para>
///
/// <para>
/// La cura è <see cref="Freschezza"/>: la cache <b>scade</b>. Non è un compromesso di comodo — è la misura
/// giusta della cosa. Chi corregge una resa si aspetta di rivederla «subito», non «entro un millisecondo»; e
/// una lettura ogni mezzo minuto su una tabella di 90 righe non è un costo. ⚠️ L'alternativa — invalidare
/// alla scrittura — vorrebbe dire che la memoria conosce le sue cache, cioè un filo che va all'indietro fra
/// due strati che oggi non si conoscono.
/// </para>
/// </summary>
public sealed class TranslationLookup
{
    /// <summary>
    /// La lingua in cui la <b>sorgente</b> scrive i testi dell'anagrafica.
    /// <para>⚠️ È un'assunzione, e va detta: IVAO scrive in inglese, e le descrizioni misurate il 28 agosto
    /// 2026 lo confermano («Reserved and designated for exclusive use by SO flights only»). Se un giorno
    /// arrivassero in italiano, questa costante è il posto dove si cambia idea.</para>
    /// </summary>
    public const string LinguaDellaSorgente = "en";

    /// <summary>
    /// Quanto vive una coppia di lingue in cache. Mezzo minuto: abbastanza perché una proiezione intera —
    /// che è un lampo — non tocchi il database più di una volta, abbastanza poco perché chi ha appena
    /// corretto una resa nel pannello la riveda ricaricando la pagina.
    /// </summary>
    public static readonly TimeSpan Freschezza = TimeSpan.FromSeconds(30);

    private readonly ITranslationMemory _memoria;
    private readonly ReadingLanguageContext? _lingua;
    private readonly TimeProvider _orologio;
    private readonly Dictionary<string, (IReadOnlyDictionary<string, string> Note, DateTimeOffset Letta)> _cache =
        new(StringComparer.Ordinal);

    /// <param name="orologio">L'orologio. Si inietta per poterlo <b>spostare</b> nei test: una scadenza
    /// provata con un'attesa vera è un test lento e capriccioso.</param>
    public TranslationLookup(
        ITranslationMemory memoria, ReadingLanguageContext? lingua = null, TimeProvider? orologio = null)
    {
        _memoria = memoria;
        _lingua = lingua;
        _orologio = orologio ?? TimeProvider.System;
    }

    /// <summary>
    /// Una funzione che traduce un testo dell'anagrafica nella lingua di chi legge, o lo lascia com'è.
    /// <para>Se il lettore legge nella lingua della sorgente, torna l'identità <b>senza toccare il
    /// database</b>: leggere in inglese testi inglesi non deve costare una query.</para>
    /// </summary>
    public async Task<Func<string?, string?>> DallaSorgenteAsync(CancellationToken ct = default)
    {
        var bersaglio = _lingua?.Corrente;
        if (string.IsNullOrEmpty(bersaglio) ||
            string.Equals(bersaglio, LinguaDellaSorgente, StringComparison.OrdinalIgnoreCase))
            return static t => t;

        var chiave = LinguaDellaSorgente + "|" + bersaglio;
        var adesso = _orologio.GetUtcNow();

        if (!_cache.TryGetValue(chiave, out var voce) || adesso - voce.Letta >= Freschezza)
        {
            var lette = await _memoria.LoadAllAsync(LinguaDellaSorgente, bersaglio, ct).ConfigureAwait(false);
            voce = (lette, adesso);
            _cache[chiave] = voce;
        }

        var note = voce.Note;

        return testo =>
        {
            if (string.IsNullOrWhiteSpace(testo)) return testo;
            // ⚠️ Quel che manca resta com'è: un testo non tradotto si legge nella lingua d'origine, non
            // sparisce. Vale qui come per i documenti — a chiazze si legge male ma si legge.
            return note.TryGetValue(TranslationText.Hash(testo), out var t) ? t : testo;
        };
    }
}

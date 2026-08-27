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

    private readonly ITranslationMemory _memoria;
    private readonly ReadingLanguageContext? _lingua;
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _cache = new(StringComparer.Ordinal);

    public TranslationLookup(ITranslationMemory memoria, ReadingLanguageContext? lingua = null)
    {
        _memoria = memoria;
        _lingua = lingua;
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
        if (!_cache.TryGetValue(chiave, out var note))
        {
            note = await _memoria.LoadAllAsync(LinguaDellaSorgente, bersaglio, ct).ConfigureAwait(false);
            _cache[chiave] = note;
        }

        return testo =>
        {
            if (string.IsNullOrWhiteSpace(testo)) return testo;
            // ⚠️ Quel che manca resta com'è: un testo non tradotto si legge nella lingua d'origine, non
            // sparisce. Vale qui come per i documenti — a chiazze si legge male ma si legge.
            return note.TryGetValue(TranslationText.Hash(testo), out var t) ? t : testo;
        };
    }
}

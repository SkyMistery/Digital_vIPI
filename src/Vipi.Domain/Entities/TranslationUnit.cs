namespace Vipi.Domain.Entities;

/// <summary>
/// Chi ha prodotto una traduzione. <see cref="Human"/> non si sovrascrive <b>mai</b> dalla macchina — nemmeno
/// se il motore cambia versione: una persona ha già deciso come si dice quella frase.
/// </summary>
public enum TranslationOrigin
{
    /// <summary>Prodotta dal motore automatico.</summary>
    Machine,

    /// <summary>Scritta o corretta da una persona. Vince sempre.</summary>
    Human,
}

/// <summary>
/// Una frase tradotta, indicizzata sull'<b>hash del testo sorgente</b> e non sul documento che la contiene
/// (carta <c>docs/feature/2026-08-27-documenti-bilingue.md</c> §1).
///
/// <para>
/// ⚠️ <b>Perché non è un campo del documento.</b> La forma ovvia — <c>BodyEn</c>, <c>TitleEn</c>,
/// <c>DescriptionEn</c> — sono diciannove colonne gemelle sui campi editoriali di oggi, e una in più per ogni
/// campo di domani. È ciò che la domanda 1 del pre-flight vieta.
/// </para>
///
/// <para>Dalla chiave-hash discendono <b>gratis</b> le quattro proprietà che servono:</para>
/// <list type="number">
///   <item><b>Incrementale</b>: cambio una frase e solo quella manca in cache. L'hash <i>è</i> il meccanismo
///   che riconosce «cosa è cambiato»; non ne serve un secondo.</item>
///   <item><b>Dedup</b>: la stessa frase in cinquanta documenti costa <b>una</b> chiamata al motore.</item>
///   <item><b>La correzione umana vale ovunque e per sempre</b> (<see cref="TranslationOrigin.Human"/>).</item>
///   <item><b>Sopravvive a tutto</b>: rinomini la sezione, sposti il blocco, ripubblichi — il testo è lo
///   stesso, la traduzione c'è.</item>
/// </list>
///
/// <para>
/// ⚠️ Il rovescio va detto a chi corregge: poiché la chiave è la frase e non il documento, una correzione
/// fatta sul documento di Roma tocca la stessa frase in quello di Milano. È il superpotere della forma e
/// insieme il suo trabocchetto, ed è limitato dal <b>congelamento nello snapshot</b>: gli altri documenti
/// cambiano solo alla loro prossima ripubblicazione.
/// </para>
/// </summary>
public class TranslationUnit
{
    public int Id { get; set; }

    /// <summary>Lingua del testo sorgente (<c>it</c>, <c>en</c>). ⚠️ Non è sempre l'italiano: la vLOA nasce
    /// <c>En</c>, e per lei l'italiano è il <b>bersaglio</b>. Stessa macchina, versi invertiti.</summary>
    public string SourceLang { get; set; } = default!;

    /// <summary>Lingua della traduzione.</summary>
    public string TargetLang { get; set; } = default!;

    /// <summary>
    /// SHA-256 esadecimale minuscolo del testo sorgente <b>normalizzato</b>
    /// (<c>TranslationText.Normalize</c>). È la chiave vera della memoria.
    /// <para>⚠️ Normalizzato, non grezzo: due normalizzazioni diverse producono due cache che non si parlano,
    /// e il dedup — che è metà del valore di questa tabella — sparirebbe in silenzio.</para>
    /// </summary>
    public string SourceHash { get; set; } = default!;

    /// <summary>Il testo sorgente <b>normalizzato</b> (quello di cui <see cref="SourceHash"/> è l'hash).
    /// Serve a mostrare a chi corregge che cosa sta traducendo, e a ricostruire la memoria se un giorno
    /// cambiasse il motore.</summary>
    public string SourceText { get; set; } = default!;

    /// <summary>La traduzione.</summary>
    public string TargetText { get; set; } = default!;

    /// <summary>Chi l'ha prodotta. <see cref="TranslationOrigin.Human"/> è definitivo.</summary>
    public TranslationOrigin Origin { get; set; }

    /// <summary>Motore che l'ha prodotta (<c>deepl</c>), o null se scritta a mano da subito.</summary>
    public string? Engine { get; set; }

    public DateTime CreatedUtc { get; set; }

    /// <summary>Quando una persona l'ha rivista. null = mai riletta da un umano → la vista la marca
    /// «traduzione automatica, non revisionata».</summary>
    public DateTime? ReviewedUtc { get; set; }

    /// <summary>VID di chi l'ha rivista. ⚠️ Resta <b>qui</b> e non esce mai verso il motore: è un dato
    /// personale, e la carta §3b lo vieta.</summary>
    public int? ReviewedByUserId { get; set; }
}

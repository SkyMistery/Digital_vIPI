namespace Vipi.Domain.Entities;

/// <summary>
/// Un segmento che il motore <b>non riesce a rendere</b>, e che quindi ha smesso di partire.
///
/// <para>🔴 <b>Perché esiste: una perdita misurata, non temuta.</b> Un segmento che torna rotto non si
/// salva in memoria — giustamente, perché una frase a cui manca il callsign è peggio della frase non
/// tradotta — e quindi il giro dopo lo ritrova fra i mancanti e lo rispedisce. Ogni quarto d'ora. Per
/// sempre. Il 30 agosto 2026 erano 155 caratteri ogni quindici minuti (§Q16b); il registro della spesa
/// nacque allora per <b>vederla</b>, quella perdita, e infatti si è vista: nello scarico del 21 settembre
/// 2026 due soli segmenti avevano bruciato <b>170 506 caratteri in cinque giorni</b> — circa un milione al
/// mese — in 410 spedizioni che tornavano rotte tutte e 410. Misurare non basta: questa tabella è il
/// <b>freno</b>.</para>
///
/// <para>⚠️ <b>La causa non è un guasto, è una regola.</b> §A64.3, misurato col motore vero il 18 settembre
/// 2026: <c>SiglaMaiuscola</c> protegge in prosa ogni parola maiuscola di almeno quattro lettere — LIBERO,
/// WING, TRAINING, AREA — Azure la traduce lo stesso, il ripristino non ritrova il segnaposto e la frase si
/// butta. Quindi <b>il ritentativo non può funzionare</b>: il guasto è deterministico, e riprovarlo
/// novantasei volte al giorno costa e basta.</para>
///
/// <para><b>Come si esce di qui</b>, in ordine di quanto succede davvero:</para>
/// <list type="number">
///   <item><b>Una persona scrive la resa</b> (pannello traduzioni, o un seme come <c>FrasiAreeIvao</c>): la
///   frase entra in memoria, il giro non la cerca più fra i mancanti e questa riga diventa inerte. È il
///   rimedio scelto dal committente il 18 settembre, e adesso è anche l'unico che serve.</item>
///   <item><b>Il testo cambia</b>: l'impronta è del testo normalizzato, quindi un testo diverso è un
///   segmento diverso, con i suoi tentativi da zero. La quarantena non si eredita.</item>
///   <item><b>Si svuota a mano</b> (<c>ITranslationQuarantine.SvuotaAsync</c>), quando cambia il motore o la
///   regola del protettore: allora le vecchie condanne non valgono più.</item>
/// </list>
///
/// <para>⚠️ <b>Non è una coda</b>, ed è la stessa ragione per cui <c>ITranslatableCorpus</c> non ne ha una:
/// «che cosa manca da tradurre» resta la differenza fra il corpus e la memoria, si calcola e non si
/// registra. Qui c'è solo <b>quel che abbiamo imparato provando</b>. Una riga il cui segmento non esiste
/// più nel corpus non sbaglia niente: non viene mai interrogata, e il giorno che quel testo tornasse
/// tornerebbe con la sua storia.</para>
/// </summary>
public class TranslationQuarantine
{
    public int Id { get; set; }

    /// <summary>Lingua di partenza. Il verso conta: la stessa frase può rompersi it→en e non en→it.</summary>
    public string SourceLang { get; set; } = default!;

    /// <summary>Lingua d'arrivo.</summary>
    public string TargetLang { get; set; } = default!;

    /// <summary>
    /// SHA-256 esadecimale minuscolo del testo sorgente <b>normalizzato</b>: la stessa chiave di
    /// <see cref="TranslationUnit.SourceHash"/>, e non un'altra.
    /// <para>⚠️ Deve essere la stessa, o il freno frenerebbe un segmento diverso da quello che la memoria
    /// considera mancante — cioè non frenerebbe niente e in più nasconderebbe qualcos'altro.</para>
    /// </summary>
    public string SourceHash { get; set; } = default!;

    /// <summary>
    /// Il testo sorgente normalizzato. ⚠️ Serve a <b>dire quale frase è</b> a chi deve scriverne la resa:
    /// senza, questa tabella direbbe «un segmento» e per trovarlo bisognerebbe interrogare il database a
    /// mano — lo stesso motivo per cui l'avviso del registro porta il testo per esteso.
    /// </summary>
    public string SourceText { get; set; } = default!;

    /// <summary>
    /// Quante volte è tornata rotta. Alla soglia (<c>TranslationQuarantena.Soglia</c>) smette di partire.
    /// <para>⚠️ Si conta, non si deduce dalla data: due giri nello stesso minuto sono due tentativi, e due
    /// tentativi a un mese di distanza sono due tentativi lo stesso.</para>
    /// </summary>
    public int Strikes { get; set; }

    /// <summary>
    /// I caratteri complessivamente pagati per questa frase e buttati. È il numero che dice <b>quanto è
    /// costata</b> prima che il freno la fermasse, ed è l'unico modo di sapere se il freno serve.
    /// </summary>
    public long CharactersWasted { get; set; }

    /// <summary>Il motore che l'ha resa rotta l'ultima volta: se un giorno si cambia motore, si sa quali
    /// condanne appartengono a quello vecchio.</summary>
    public string? Engine { get; set; }

    /// <summary>Il primo tentativo andato male.</summary>
    public DateTime FirstUtc { get; set; }

    /// <summary>L'ultimo. ⚠️ Con <see cref="Strikes"/> sotto soglia dice «ci sta ancora provando»; a soglia
    /// raggiunta è la data della condanna, e non si muove più.</summary>
    public DateTime LastUtc { get; set; }
}

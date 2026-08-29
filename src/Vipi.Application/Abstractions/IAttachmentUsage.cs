using Vipi.Domain;

namespace Vipi.Application.Abstractions;

/// <summary>Da dove arriva una citazione: è ciò che dice a chi legge <b>quanto è grave</b> togliere la voce.</summary>
public enum AttachmentCitationSource
{
    /// <summary>Un blocco di un documento: la bozza che qualcuno sta scrivendo, o la versione corrente.</summary>
    Document,

    /// <summary>
    /// Il payload di una <b>release pubblicata</b>: il documento come lo vede il pubblico adesso.
    /// <para>⚠️ È la citazione che l'occhio non trova: non compare in nessuna bozza, quindi cancellare la voce
    /// lascerebbe un link morto in un documento che nessuno sta guardando — e lo scoprirebbe un lettore.</para>
    /// </summary>
    Release,

    /// <summary>Una sezione extra d'aeroporto (⚠️ storage <b>legacy</b>: nessuno vi scrive più, ma finché il
    /// trasloco non ha girato ovunque una citazione può stare solo lì).</summary>
    AirportExtraSection,

    /// <summary>Un blocco condiviso: non appartiene a un documento solo, quindi toglierlo tocca tutti quelli
    /// che lo montano.</summary>
    SharedBlock,
}

/// <summary>Una citazione: chi nomina l'allegato, e dove si va a metterci le mani.</summary>
/// <param name="Title">Come si chiama il posto che lo cita, per chi legge: il titolo del documento.</param>
/// <param name="Url">Dove si va a correggerlo. <c>null</c> quando il posto non è raggiungibile — e la riga
/// resta lo stesso: sparire sarebbe peggio che comparire senza collegamento.</param>
/// <param name="IsPublished">Il documento è pubblicato: la citazione la sta leggendo qualcuno adesso.</param>
/// <param name="EffectiveCycle">Ciclo AIRAC della release in vigore, se c'è.
/// <para>⚠️ Sono <b>fatti</b>, non una frase: la frase la compone la pagina, che sa in che lingua sta
/// parlando. Una stringa italiana costruita qui uscirebbe italiana anche nella versione inglese.</para></param>
public sealed record AttachmentCitation(
    AttachmentCitationSource Source, string Title, string? Url = null,
    bool IsPublished = false, string? EffectiveCycle = null);

/// <summary>Chi cita una voce di biblioteca, per slug.</summary>
public sealed record AttachmentUsage(string Slug, IReadOnlyList<AttachmentCitation> Citations);

/// <summary>
/// Chi cita cosa, nella biblioteca allegati.
///
/// <para><b>Si RICAVA, non si mantiene.</b> La tentazione è una tabella di join <c>Allegato ↔ Documento</c>
/// aggiornata a ogni salvataggio: si desincronizza al primo percorso di scrittura che dimentica di
/// aggiornarla, e mente <b>proprio quando serve</b> — cioè davanti alla conferma di una cancellazione. Qui si
/// legge invece lo stesso testo che il viewer rende, quindi non può mentire.</para>
///
/// <para>⚠️ <b>Non sta su <c>IAttachmentLibrary</c> apposta.</b> Il redirect <c>/vsop/files/{slug}</c> chiama
/// quella porta a ogni clic: se «chi mi cita» fosse un campo della riga, ogni apertura di un PDF pagherebbe
/// una scansione di tutti i blocchi e di tutte le release. Sono due domande con due costi diversi e due
/// chiamanti diversi.</para>
/// </summary>
public interface IAttachmentUsage
{
    /// <summary>
    /// Tutte le citazioni, per slug, in <b>una passata sola</b> sulle quattro sorgenti.
    /// <para>Uno slug assente dal risultato non lo cita nessuno: è il filtro «mai usata» della biblioteca, che
    /// è anche il modo di tenerla pulita.</para>
    /// </summary>
    Task<IReadOnlyDictionary<string, AttachmentUsage>> AllAsync(CancellationToken ct = default);

    /// <summary>
    /// Chi cita <b>questa</b> voce. Vuoto = nessuno.
    /// <para>È la lettura della guardia alla cancellazione e della conferma di sostituzione: dice <b>quali</b>
    /// documenti cambiano, che è l'unica informazione con cui si decide.</para>
    /// </summary>
    Task<IReadOnlyList<AttachmentCitation>> WhereUsedAsync(string slug, CancellationToken ct = default);
}

/// <summary>
/// I testi in cui può comparire un riferimento, con la loro provenienza. Sono <b>quattro posti</b> e vanno
/// guardati tutti: saltarne uno significa dire «non la cita nessuno» di una voce che invece è citata, e
/// autorizzare una cancellazione che rompe un documento in silenzio.
///
/// <para>Sta qui, e non dentro il servizio, perché <i>dove</i> vivono quei testi lo sa solo la persistenza —
/// mentre <i>che cosa significano</i> lo sa l'applicazione.</para>
/// </summary>
public interface IAttachmentTextSource
{
    Task<IReadOnlyList<AttachmentText>> ReadAllAsync(CancellationToken ct = default);
}

/// <summary>Un testo da scandagliare, con quel che serve per dire da dove viene.</summary>
/// <param name="DocumentId">Il documento a cui risalire per titolo e link; <c>null</c> per i posti che non
/// appartengono a un documento solo (un blocco condiviso) o che non ne hanno uno raggiungibile.</param>
/// <param name="Label">Come chiamarlo quando il documento non c'è: l'ICAO della sezione extra, la chiave del
/// blocco condiviso. Mai una stringa vuota a schermo.</param>
/// <param name="ReleaseTarget">L'altra via per risalire al documento, e serve davvero: una release <b>non
/// porta un DocumentId</b> — è identificata dalla coppia (tipo, chiave), le stesse dei bersagli di
/// pubblicazione. Chi legge questo record deve provare le due strade, non presumerne una.</param>
/// <param name="ReleaseKey">La chiave del bersaglio, insieme a <paramref name="ReleaseTarget"/>.</param>
public sealed record AttachmentText(
    string? Text, AttachmentCitationSource Source, int? DocumentId = null, string? Label = null,
    ReleaseTargetType? ReleaseTarget = null, string? ReleaseKey = null);

using Vipi.Domain.Entities;

namespace Vipi.Application.Abstractions;

/// <summary>Una riga della pagina di revisione: la frase, la sua resa, e chi l'ha prodotta.</summary>
/// <param name="SourceText">Il testo sorgente normalizzato: e' <b>la chiave</b>, e non si modifica da qui.
/// Cambiare cio' che il documento dice e' un'edit del documento, non una revisione della traduzione.</param>
public sealed record TranslationReviewRow(
    int Id, string SourceText, string TargetText, TranslationOrigin Origin,
    DateTime? ReviewedUtc, int? ReviewedByUserId);

/// <summary>Una traduzione già in memoria: il testo e da chi viene.</summary>
/// <param name="TargetText">La traduzione.</param>
/// <param name="Origin">Macchina o persona. <see cref="TranslationOrigin.Human"/> è definitivo.</param>
/// <param name="Reviewed">Se una persona l'ha riletta. Falso → la vista la marca «non revisionata».</param>
public sealed record KnownTranslation(string TargetText, TranslationOrigin Origin, bool Reviewed);

/// <summary>
/// La memoria di traduzione: si interroga e si riempie <b>per impronta</b>, mai per documento (carta
/// <c>docs/feature/2026-08-27-documenti-bilingue.md</c> §1).
///
/// <para>
/// ⚠️ Le letture sono <b>di gruppo</b>, e non è ottimizzazione prematura: una pagina di documento ha decine
/// di segmenti, e una query per segmento sarebbe una corsa sul <c>DbContext</c> del circuito Blazor — il
/// guasto «second operation» già pagato sei volte.
/// </para>
/// </summary>
public interface ITranslationMemory
{
    /// <summary>Le traduzioni note per queste impronte. Le chiavi mancanti semplicemente non compaiono.</summary>
    Task<IReadOnlyDictionary<string, KnownTranslation>> LookupAsync(
        string sourceLang, string targetLang, IReadOnlyCollection<string> hashes, CancellationToken ct = default);

    /// <summary>
    /// Scrive le traduzioni prodotte dalla <b>macchina</b>.
    /// <para>⚠️ Non tocca <b>mai</b> una voce già corretta da una persona
    /// (<see cref="TranslationOrigin.Human"/>): è la promessa su cui si regge tutta la funzione — una
    /// correzione vale ovunque e <b>per sempre</b>, anche quando il motore cambia versione.</para>
    /// </summary>
    Task<int> SaveMachineAsync(
        string sourceLang, string targetLang, string engine,
        IReadOnlyList<(string SourceText, string TargetText)> tradotte, CancellationToken ct = default);

    /// <summary>
    /// Scrive la correzione di una persona: vince sempre, e da qui in avanti la macchina non la tocca.
    /// <para>⚠️ Tocca la FRASE, non il documento: la correzione fatta sul documento di Roma vale per la
    /// stessa frase in quello di Milano. Chi la offre deve dirlo a chi corregge.</para>
    /// </summary>
    Task SaveHumanAsync(
        string sourceLang, string targetLang, string sourceText, string targetText,
        int reviewerUserId, CancellationToken ct = default);

    /// <summary>
    /// Le voci di memoria per la pagina di revisione, le <b>meno riviste per prime</b>: chi apre quella
    /// pagina vuole vedere cio' che nessuno ha ancora guardato, non l'ordine di inserimento.
    /// </summary>
    /// <param name="soloDaRileggere">Solo quelle prodotte dalla macchina e mai riviste.</param>
    Task<IReadOnlyList<TranslationReviewRow>> ListForReviewAsync(
        string sourceLang, string targetLang, bool soloDaRileggere, int limite, CancellationToken ct = default);

    /// <summary>
    /// <b>Tutte</b> le traduzioni di una coppia di lingue: impronta → testo tradotto.
    ///
    /// <para>
    /// ⚠️ Sembra sproporzionato e non lo è: serve ai testi che stanno <b>fuori dai documenti</b> — le
    /// descrizioni delle aree regolamentate, che vivono nell'anagrafica — dove non esiste una lista di
    /// impronte da chiedere prima, perché il chiamante scopre quali testi gli servono mentre proietta.
    /// Misurato: la memoria intera oggi sono 90 righe, e il corpus editoriale completo 23.344 caratteri.
    /// Chiedere la coppia intera una volta per richiesta costa meno di una query per area su 230 aree.
    /// </para>
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> LoadAllAsync(
        string sourceLang, string targetLang, CancellationToken ct = default);

    /// <summary>Quante voci ci sono, e quante ne restano da rileggere. Per il contatore in cima.</summary>
    Task<(int Totale, int DaRileggere)> ContaAsync(
        string sourceLang, string targetLang, CancellationToken ct = default);

    /// <summary>
    /// Quanti documenti contengono la frase che sta dietro questa impronta: il numero che si mostra a chi
    /// corregge <b>prima</b> che salvi («questa correzione tocca N documenti»).
    /// </summary>
    Task<int> DocumentiToccatiAsync(string sourceText, CancellationToken ct = default);

    /// <summary>
    /// Stima dei caratteri già spesi col motore: la somma dei testi sorgente delle voci prodotte dalla
    /// macchina.
    /// <para>⚠️ È una <b>stima</b>, e va detto: non conta i tentativi falliti, e il testo che viaggia è
    /// quello <i>protetto</i>, che è un po' più corto dell'originale. Il dato autorevole lo dà il motore
    /// (<see cref="ITranslationEngine"/>); questa serve come guardia prima di partire, quando il motore non
    /// sa rispondere.</para>
    /// </summary>
    Task<long> CaratteriSpesiStimatiAsync(string engine, CancellationToken ct = default);
}

/// <summary>
/// Da dove escono i testi da tradurre: <b>tutti</b> i segmenti editoriali del corpus, distinti.
///
/// <para>
/// ⚠️ <b>Non esiste una tabella di coda, ed è una scelta.</b> «Che cosa manca da tradurre» è la differenza
/// fra i segmenti del corpus e le impronte già in memoria — si calcola, non si registra. Una coda vera
/// sarebbe un secondo posto dove sapere che una frase esiste, e i due si sarebbero disallineati al primo
/// documento eliminato o ripristinato. Così invece il giro è <b>auto-riparante</b>: qualunque cosa manchi,
/// alla prossima passata si ritrova da sé.
/// </para>
/// <para>
/// Il conto regge perché è stato <b>misurato</b>: il corpus editoriale intero è 23.344 caratteri su 499
/// campi. Scandirlo per intero costa meno di quanto costerebbe tenere sincronizzata una coda.
/// </para>
/// </summary>
public interface ITranslatableCorpus
{
    /// <summary>Ogni testo editoriale distinto dei documenti in questa lingua sorgente, già normalizzato.</summary>
    Task<IReadOnlyList<string>> SegmentiAsync(string sourceLang, CancellationToken ct = default);
}

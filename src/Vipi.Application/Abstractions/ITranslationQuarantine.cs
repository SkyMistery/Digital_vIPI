using Vipi.Domain.Entities;

namespace Vipi.Application.Abstractions;

/// <summary>Un segmento che ha smesso di partire, come lo si racconta a una persona.</summary>
/// <param name="SourceText">La frase, per esteso: è quella di cui va scritta la resa a mano.</param>
/// <param name="Strikes">Quante volte il motore l'ha resa rotta.</param>
/// <param name="CharactersWasted">Quanti caratteri ha bruciato prima che il freno la fermasse.</param>
/// <param name="Engine">Chi l'ha rotta l'ultima volta.</param>
/// <param name="LastUtc">Quando.</param>
public sealed record SegmentoInQuarantena(
    string SourceText, int Strikes, long CharactersWasted, string? Engine, DateTime LastUtc);

/// <summary>Un tentativo andato male, come lo si registra.</summary>
/// <param name="Hash">L'impronta del testo normalizzato: la stessa chiave della memoria.</param>
/// <param name="SourceText">Il testo, per poterlo poi mostrare.</param>
/// <param name="Characters">I caratteri spediti per questo segmento, pagati e buttati.</param>
public sealed record TentativoRotto(string Hash, string SourceText, long Characters);

/// <summary>
/// Il freno dei segmenti che il motore non sa rendere (vedi <see cref="TranslationQuarantine"/>).
///
/// <para>🔴 <b>Perché sta nel database e non in un campo.</b> Il contatore deve sopravvivere al riavvio del
/// processo, e su questo host il processo non vive: si spegne per inattività e si riaccende al primo
/// risveglio. Misurato sullo scarico del 19 settembre 2026 — <b>84 giri di traduzione su 130 processi</b>,
/// cioè meno di un giro per processo. Un contatore in memoria non arriverebbe <b>mai</b> a tre, e il freno
/// sarebbe una funzione che sembra esserci.</para>
///
/// <para>⚠️ Le letture sono <b>di gruppo</b>, per la stessa ragione di <see cref="ITranslationMemory"/>:
/// il giro chiede di tutti i mancanti in una volta, non uno per uno.</para>
/// </summary>
public interface ITranslationQuarantine
{
    /// <summary>
    /// Quanti tentativi falliti risultano per ciascuna di queste impronte. Le impronte senza storia
    /// semplicemente non compaiono — «zero» e «mai provata» qui sono la stessa cosa, e distinguerle
    /// costringerebbe ogni chiamante a gestire un caso che non esiste.
    /// </summary>
    Task<IReadOnlyDictionary<string, int>> StrikeAsync(
        string sourceLang, string targetLang, IReadOnlyCollection<string> hashes, CancellationToken ct = default);

    /// <summary>
    /// Segna che questi segmenti sono tornati rotti: alza il contatore, somma i caratteri buttati, e crea la
    /// riga se è il primo tentativo.
    /// </summary>
    /// <returns>I segmenti che con <b>questo</b> giro hanno raggiunto la soglia, cioè quelli che da adesso
    /// non partono più. ⚠️ Solo quelli che l'hanno <b>appena</b> raggiunta, non tutti quelli in quarantena:
    /// è il passaggio di stato a volere un avviso, non lo stato — un avviso che si ripete a ogni giro è
    /// esattamente il rumore che questa tabella esiste per spegnere.</returns>
    Task<IReadOnlyList<string>> SegnaRottiAsync(
        string sourceLang, string targetLang, IReadOnlyList<TentativoRotto> rotti, string? engine,
        DateTime nowUtc, CancellationToken ct = default);

    /// <summary>
    /// Cancella la storia di questi segmenti: sono tornati interi, quindi quel che sapevamo di loro non vale
    /// più. ⚠️ Serve perché un ripristino può fallire una volta sola per un motivo passeggero, e tre
    /// incidenti sparsi in tre mesi non sono un segmento irrecuperabile.
    /// </summary>
    Task DimenticaAsync(
        string sourceLang, string targetLang, IReadOnlyCollection<string> hashes, CancellationToken ct = default);

    /// <summary>Che cosa è fermo, dal più costoso: l'elenco che serve a una persona per sapere da dove
    /// cominciare a scrivere le rese a mano.</summary>
    Task<IReadOnlyList<SegmentoInQuarantena>> FermiAsync(
        string sourceLang, string targetLang, CancellationToken ct = default);

    /// <summary>
    /// Svuota la quarantena di un verso. ⚠️ È l'uscita di sicurezza per quando cambia il <b>motore</b> o la
    /// regola del protettore: le condanne di prima sono state emesse da un giudice che non c'è più. Quel che
    /// si libera, il giro dopo si ripaga — come <c>DimenticaAutomaticheConLaFormulaAsync</c>, e chi preme
    /// deve saperlo.
    /// </summary>
    /// <returns>Quante righe ha tolto.</returns>
    Task<int> SvuotaAsync(string sourceLang, string targetLang, CancellationToken ct = default);
}

/// <summary>La regola del freno, in un posto solo.</summary>
public static class TranslationQuarantena
{
    /// <summary>
    /// Quanti tentativi rotti prima di smettere.
    ///
    /// <para>⚠️ <b>Tre e non uno</b>: un ripristino può fallire per un motivo passeggero — un lotto tornato
    /// corto, il motore di riserva entrato a metà giro — e condannare una frase al primo incidente vorrebbe
    /// dire chiedere una resa a mano per qualcosa che il giro dopo sarebbe andato bene.</para>
    ///
    /// <para>⚠️ <b>Tre e non trenta</b>: il guasto vero è deterministico (§A64.3), quindi ogni tentativo
    /// dopo il primo è una conferma, non una speranza. A un giro ogni quarto d'ora, tre tentativi sono
    /// quarantacinque minuti e — sui due segmenti misurati il 21 settembre 2026 — circa 1 200 caratteri
    /// invece di 170 506.</para>
    ///
    /// <para>🔴 <b>È una costante e non una chiave di configurazione</b>, ed è una scelta pagata: le opzioni
    /// di traduzione si sommano fra file di configurazione (<c>Translation:Targets</c> divenne
    /// <c>it,en,it,en</c> e il giro fece otto passate invece di due, §A62). Una soglia sbagliata qui non si
    /// vedrebbe: si vedrebbe solo la bolletta.</para>
    /// </summary>
    public const int Soglia = 3;
}

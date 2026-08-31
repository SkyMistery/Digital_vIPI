using Vipi.Domain.Entities;

namespace Vipi.Application.Abstractions;

/// <summary>
/// Dove vivono le voci del glossario di fraseologia (<c>lavori-aperti §Q3</c>).
///
/// <para>
/// ⚠️ <b>Le letture sono di gruppo, come per la memoria di traduzione</b>, e per la stessa ragione: il
/// glossario si carica <b>intero</b> una volta per giro e poi si applica in memoria a ogni segmento. Una
/// query per segmento sarebbe una corsa sul <c>DbContext</c> del circuito Blazor — il guasto «second
/// operation» già pagato sei volte. Le voci sono decine, non migliaia: caricarle tutte costa meno di
/// chiedersi ogni volta quali servono.
/// </para>
/// </summary>
public interface IGlossaryStore
{
    /// <summary>Le voci di una direzione, dalla più recente. Vuoto se non ne è mai stata scritta nessuna.</summary>
    /// <param name="cerca">
    /// Testo da cercare nella formula o nella sua resa, senza distinzione di maiuscole. <c>null</c> o vuoto
    /// = nessun filtro. Sta qui e non nel chiamante per la stessa ragione della memoria: il giorno che le
    /// voci non ci stanno più in una schermata, un filtro a valle risponderebbe sul pezzo caricato.
    /// </param>
    /// <param name="alfabetico">
    /// Vero: in ordine di formula (A→Z). Falso (default): le più recenti in cima, che è quel che serve
    /// subito dopo aver scritto una voce. ⚠️ L'ordine si sceglie <b>qui</b> e non riordinando la lista a
    /// valle: il giorno che le voci non ci stanno in una schermata, un ordinamento a valle ordinerebbe il
    /// pezzo caricato — cioè direbbe «A» a una lettera scelta dal database.
    /// </param>
    Task<IReadOnlyList<GlossaryTerm>> ListAsync(
        string sourceLang, string targetLang, string? cerca = null, bool alfabetico = false,
        CancellationToken ct = default);

    /// <summary>
    /// Scrive o corregge una voce. La chiave è la frase sorgente <b>senza distinzione di maiuscole</b>:
    /// riscrivere «Riporta sottovento» corregge la voce di «riporta sottovento», non ne crea una seconda.
    /// </summary>
    /// <returns>Vero se la voce è nuova, falso se ne ha corretta una che c'era già.</returns>
    Task<bool> UpsertAsync(
        string sourceLang, string targetLang, string sourceText, string targetText,
        int? userId, CancellationToken ct = default);

    /// <summary>Toglie una voce. Falso se non c'era.</summary>
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>Quante voci ci sono in questa direzione. Per il contatore in cima alla pagina.</summary>
    Task<int> ContaAsync(string sourceLang, string targetLang, CancellationToken ct = default);
}

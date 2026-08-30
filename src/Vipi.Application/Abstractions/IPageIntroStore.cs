using Vipi.Application.Content;

namespace Vipi.Application.Abstractions;

/// <summary>
/// Dove vive l'intro di una pagina (carta <c>docs/feature/2026-08-30-intro-di-pagina.md</c> §2).
///
/// <para>
/// ⚠️ <b>Non c'è nessuna tabella nuova dietro questa porta</b>, e non è un ripiego: l'intro è contenuto
/// <b>senza padrone</b> — non è di un aeroporto, non è di un settore, non è di un documento — e una riga
/// chiavata su una stringa è esattamente la forma che le serve.
/// </para>
///
/// <para>
/// ⚠️ <b>Niente release e niente ciclo AIRAC</b>: si pubblica salvando. Vedi <see cref="PageIntro"/> per il
/// perché un <c>Document</c> non andrebbe bene, e per il divieto di metterci contenuto normativo.
/// </para>
/// </summary>
public interface IPageIntroStore
{
    /// <summary>
    /// Le sezioni dell'intro di una pagina, nell'ordine in cui si mostrano. Pagina senza intro → lista vuota.
    /// <para>È una <b>lettura pubblica</b>: la chiama anche chi non è loggato.</para>
    /// </summary>
    Task<IReadOnlyList<PageIntroSection>> LeggiAsync(string pagina, CancellationToken ct = default);

    /// <summary>
    /// Sostituisce l'intro di una pagina con queste sezioni. Elenco vuoto = l'intro non c'è più.
    ///
    /// <para>⚠️ <b>Il cancello è qui</b>, non nella pagina: una zona che si mostra a tutti e si salva da un
    /// bottone nascosto sarebbe protetta dal CSS. Serve almeno l'Editor.</para>
    /// </summary>
    /// <param name="etichetta">Come si chiama questa intro nell'elenco dei contenuti condivisi: serve a chi
    /// un giorno guarderà la tabella e dovrà capire di che pagina è questa riga.</param>
    Task SalvaAsync(string pagina, IReadOnlyList<PageIntroSection> sezioni, string etichetta,
        CancellationToken ct = default);
}

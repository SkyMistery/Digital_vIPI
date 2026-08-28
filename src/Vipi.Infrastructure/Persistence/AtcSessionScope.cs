using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Il confine fra le sessioni <b>di divisione</b> e quelle del resto del mondo, scritto una volta sola.
///
/// <para>Dal 28 agosto 2026 il poller archivia <b>tutte</b> le postazioni ATC aperte, non più le sole
/// italiane (carta <c>docs/feature/2026-08-28-archivio-atc-mondiale.md</c>). Quelle fuori divisione sono
/// archivio e basta: ogni lettura che <i>conta</i> qualcosa — ore, classifica, copertura, riassunto mensile —
/// deve passare di qui, o comincerebbe a dire che la divisione ha fatto le ore del pianeta.</para>
///
/// <para>⚠️ Un metodo condiviso e non un <c>HasQueryFilter</c> globale: il filtro globale sarebbe invisibile
/// a chi legge <c>_db.AtcSessions</c> e andrebbe <b>disattivato</b> proprio nei due punti nuovi (pagina
/// mondo ed endpoint macchina), cioè si dimenticherebbe al contrario — e sbagliando in quella direzione
/// nessuna pagina si accorge di niente.</para>
/// </summary>
public static class AtcSessionScope
{
    /// <summary>Solo le postazioni della divisione: è ciò che le statistiche hanno sempre contato.</summary>
    public static IQueryable<AtcSession> DiDivisione(this IQueryable<AtcSession> q) =>
        q.Where(s => !s.IsOutsideDivision);
}

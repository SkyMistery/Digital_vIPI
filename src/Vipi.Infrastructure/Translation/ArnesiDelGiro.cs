using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Translation;
using Vipi.Infrastructure.Persistence;

namespace Vipi.Infrastructure.Translation;

/// <summary>
/// Gli arnesi comuni a chiunque faccia girare la traduzione: i nomi che non devono uscire e il protettore di
/// un verso.
///
/// <para>
/// ⚠️ <b>Esistono perché i chiamanti sono tre</b> (Regola del 2 del <c>FEATURE-PROCESS</c>): il giro
/// automatico ogni quarto d'ora, il tasto «traduci ora» di chi sta scrivendo, e il conto di quanti segmenti
/// il protettore <b>rifiuta</b> nello stato della traduzione. Tre copie del modo di costruire un
/// <see cref="TextProtector"/> vorrebbero dire che un giorno una delle tre non conosce più i nomi — e
/// quella, in silenzio, spedisce fuori un dato personale.
/// </para>
/// </summary>
internal static class ArnesiDelGiro
{
    /// <summary>
    /// I nomi dello staff, per il protettore. ⚠️ Vanno letti a <b>ogni</b> giro e non una volta all'avvio: il
    /// roster cresce a ogni login nuovo, e un protettore costruito ieri non conosce lo staffista arrivato
    /// stamattina — cioè lascerebbe uscire proprio il nome più recente.
    /// </summary>
    public static async Task<List<string>> NomiDelloStaffAsync(VipiDbContext db, CancellationToken ct) =>
        await db.StaffMembers.AsNoTracking()
            .Where(s => s.DisplayName != null)
            .Select(s => s.DisplayName!)
            .ToListAsync(ct)
            .ConfigureAwait(false);

    /// <summary>
    /// Il protettore di <b>un verso</b>. ⚠️ Uno per verso e non uno solo per tutto: il glossario è
    /// direzionale, e un protettore unico lascerebbe il verso <c>en→it</c> senza glossario per sempre senza
    /// che nessun errore lo dica.
    /// </summary>
    public static async Task<TextProtector> ProtettoreAsync(
        IGlossaryStore deposito, IEnumerable<string> nomi, string sourceLang, string targetLang,
        CancellationToken ct)
    {
        var glossario = await GlossarioFraseologia
            .CaricaAsync(deposito, sourceLang, targetLang, ct).ConfigureAwait(false);
        return new TextProtector(nomi, glossario);
    }
}

using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// La categoria che un aeroporto <b>deve</b> avere, data la riga com'è in archivio (carta
/// <c>docs/feature/2026-09-11-categorie-aeroporto.md</c>). È l'invariante di <see cref="AirportCategories.Normalize"/>
/// più il <b>travaso</b> dal booleano in pensione <c>IsMilitaryOnly</c>.
///
/// <para>⚠️ <b>Perché un posto solo per due chiamanti.</b> La usano la passata d'avvio
/// (<c>EfDocumentMaintenance.ReconcileAirportCategoriesAsync</c>) e il giro dell'anagrafica
/// (<c>SyncAirportSourceFieldsAsync</c>). Se il giro normalizzasse con la sola regola del default, e la passata
/// d'avvio fosse fallita — può: è «isolata», e il sito parte lo stesso — un campo solo militare ancora a
/// <c>Civil</c> diventerebbe «civile con presenza militare» e la scelta di una persona sparirebbe in silenzio.
/// Con la stessa funzione, chiunque arrivi per primo fa il travaso giusto.</para>
///
/// <para>▶ Dopo il 16 settembre 2026, quando una migrazione toglie <c>IsMilitaryOnly</c>, il ramo del travaso se
/// ne va e resta la sola <see cref="AirportCategories.Normalize"/>.</para>
/// </summary>
internal static class AirportCategoryTransfer
{
    /// <summary>La categoria attesa. Uguale a quella in archivio = niente da fare.</summary>
    public static AirportCategory Attesa(Airport a)
    {
        if (!a.HasMilitaryPresence) return AirportCategory.Civil;
        if (a.Category != AirportCategory.Civil) return a.Category;

        // Presenza militare con la categoria ancora al valore di nascita della colonna: la riga non è mai stata
        // travasata. Lo specchio dice «solo militare»; chi ha già un vSOP va in 4, così nessun documento
        // esistente finisce fuori categoria per effetto del cambio; gli altri prendono il default.
        if (a.IsMilitaryOnly) return AirportCategory.MilitaryOnly;
        if (a.MilDocumentId is not null) return AirportCategory.MilitaryWithCivilPresence;
        return AirportCategories.DefaultWithMilitaryPresence;
    }
}

using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// La categoria che un aeroporto <b>deve</b> avere, data la riga com'è in archivio (carta
/// <c>docs/feature/2026-09-11-categorie-aeroporto.md</c>). È l'invariante di <see cref="AirportCategories.Normalize"/>
/// più una regola in più: un campo che guadagna la presenza militare e ha <b>già</b> un vSOP va in
/// «militare con presenza civile», così nessun documento esistente finisce fuori categoria.
///
/// <para>⚠️ <b>Perché un posto solo per due chiamanti.</b> La usano la passata d'avvio
/// (<c>EfDocumentMaintenance.ReconcileAirportCategoriesAsync</c>) e il giro dell'anagrafica
/// (<c>SyncAirportSourceFieldsAsync</c>): due regole scritte in due posti finirebbero per dire due cose.</para>
///
/// <para>ℹ️ Il nome viene dal <b>travaso</b> dal booleano <c>IsMilitaryOnly</c>, che questa funzione ha fatto
/// dall'11 al 16 settembre 2026. La colonna non c'è più (migrazione <c>SpecchioSoloMilitareInPensione</c>),
/// misurato prima di toglierla: sulla copia di produzione del 16 settembre nessuna riga restava da travasare.</para>
/// </summary>
internal static class AirportCategoryTransfer
{
    /// <summary>La categoria attesa. Uguale a quella in archivio = niente da fare.</summary>
    public static AirportCategory Attesa(Airport a)
    {
        if (!a.HasMilitaryPresence) return AirportCategory.Civil;
        if (a.Category != AirportCategory.Civil) return a.Category;

        // Presenza militare su un campo ancora Civile: chi ha già un vSOP va in 4, gli altri prendono il default.
        if (a.MilDocumentId is not null) return AirportCategory.MilitaryWithCivilPresence;
        return AirportCategories.DefaultWithMilitaryPresence;
    }
}

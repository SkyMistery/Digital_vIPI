using Microsoft.Extensions.Localization;

namespace Vipi.Ui.Components.Doc;

/// <summary>
/// Le colonne delle tabelle a mano del profilo militare: le decide il PROFILO, non chi compila — è quel che
/// rende una sezione confrontabile fra quindici campi.
///
/// <para>⚠️ Stavano come membri della <b>pagina del viewer</b>, e l'editor militare le citava da lì: un
/// editor che chiede una definizione a un viewer è un legame che si rompe alla prima cosa che si sposta —
/// ed è successo il 3 settembre 2026, spostando il corpo del viewer in un componente. Ora hanno una casa, e
/// i due che le usano la citano allo stesso modo.</para>
///
/// <para>⚠️ Il localizzatore che si passa è quello della lingua del <b>DOCUMENTO</b>, non quello del sito:
/// queste intestazioni stanno dentro il vSOP, e su un documento a lingua bloccata seguono lui (carta
/// <c>2026-08-31-lingua-bloccata.md</c> §4).</para>
/// </summary>
public static class MilProfiloTabelle
{
    public static IReadOnlyList<string> Nominativi(IStringLocalizer<SharedResource> l) =>
        new[] { l["Mil_Squadron"].Value, l["Mil_OatCallsign"].Value, l["Mil_GatCallsign"].Value, l["Mil_QraCallsign"].Value };

    public static IReadOnlyList<string> Parcheggi(IStringLocalizer<SharedResource> l) =>
        new[] { l["Mil_ParkingName"].Value, l["Mil_ParkingNumbers"].Value, l["Mil_ParkingUsedBy"].Value };
}

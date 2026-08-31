using System.Globalization;
using System.Resources;

namespace Vipi.Ui;

/// <summary>
/// Le stringhe di <c>SharedResource</c> lette in una cultura <b>scritta a mano</b>, invece che in quella
/// corrente.
///
/// <para>
/// ⚠️ <b>Non passa da <see cref="Microsoft.Extensions.Localization.IStringLocalizer{T}"/></b> e non può:
/// quello risolve sempre sulla cultura corrente, che è esattamente ciò che qui va ignorato. Serve a tre
/// mestieri diversi che hanno tutti bisogno della stessa cosa — la briciola di pane, che è sempre inglese
/// (<see cref="EnglishStrings"/>); un documento a <b>lingua bloccata</b>, che è sempre nella sua
/// (<see cref="LocalizzatoreDiLingua"/>); e ciò che deve restare nella lingua di chi guarda anche dentro
/// una pagina bloccata (<see cref="StringheDelSito"/>).
/// </para>
///
/// <para>
/// ⚠️ <b>Le chiavi restano quelle, e le stringhe non si duplicano.</b> Un secondo vocabolario di letterali
/// diventerebbe, sei mesi dopo, un posto dove «Aeroporti» è stato rinominato e l'altro no.
/// </para>
/// </summary>
public static class RisorseCondivise
{
    /// <summary>Il <see cref="ResourceManager"/> di <c>SharedResource</c>. Uno solo per tutto il prodotto:
    /// ha già la sua cache dentro, e due istanze la pagherebbero due volte.</summary>
    public static readonly ResourceManager Manager =
        new("Vipi.Ui.Resources.SharedResource", typeof(SharedResource).Assembly);

    /// <summary>
    /// Il testo di questa chiave in questa cultura. Se la chiave non esiste torna <b>la chiave stessa</b>,
    /// come fa il localizzatore standard: a schermo si vede un nome tecnico invece del vuoto, e chi lo vede
    /// capisce subito che manca una riga nel resx.
    /// </summary>
    public static string Testo(string chiave, CultureInfo cultura) =>
        Manager.GetString(chiave, cultura) ?? chiave;
}

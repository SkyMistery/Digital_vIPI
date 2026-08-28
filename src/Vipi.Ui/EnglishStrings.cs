using System.Globalization;
using System.Resources;

namespace Vipi.Ui;

/// <summary>
/// Le stesse stringhe di <c>SharedResource</c>, ma <b>sempre in inglese</b>, qualunque lingua stia leggendo
/// chi guarda (regola R3 di <c>docs/design/regole-lingua.md</c>).
///
/// <para>
/// Serve alla <b>briciola di pane</b>, che per decisione del committente è in inglese in tutte e due le
/// versioni del sito: <c>Home › LIBB › Airports › vIPI — LIBC Crotone</c> anche dentro una pagina italiana.
/// </para>
///
/// <para>
/// ⚠️ <b>Le chiavi restano quelle</b>, e le stringhe non si duplicano. La tentazione era scrivere in un
/// secondo posto «Airports», «Apps», «Home» come letterali della briciola: sarebbero diventati sei mesi
/// dopo un vocabolario parallelo, con «Aeroporti» rinominato da una parte e non dall'altra. Qui la briciola
/// chiede <b>la stessa cosa</b> al <b>solito posto</b>, in un'altra lingua.
/// </para>
///
/// <para>
/// ⚠️ <b>Non passa da <see cref="Microsoft.Extensions.Localization.IStringLocalizer{T}"/></b> e non può: quello
/// risolve sempre sulla cultura corrente, che è esattamente ciò che qui va ignorato. Si legge dal
/// <see cref="ResourceManager"/> con la cultura scritta a mano.
/// </para>
/// </summary>
public sealed class EnglishStrings
{
    /// <summary>La cultura in cui si legge, sempre. Non è la lingua di chi guarda: è una scelta di prodotto.</summary>
    private static readonly CultureInfo Inglese = CultureInfo.GetCultureInfo("en");

    private static readonly ResourceManager Risorse =
        new("Vipi.Ui.Resources.SharedResource", typeof(SharedResource).Assembly);

    /// <summary>
    /// Il testo inglese per questa chiave. Se la chiave non esiste torna <b>la chiave stessa</b>, come fa il
    /// localizzatore standard: a schermo si vede un nome tecnico invece del vuoto, e chi lo vede capisce
    /// subito che manca una riga nel resx.
    /// </summary>
    public string this[string chiave] => Risorse.GetString(chiave, Inglese) ?? chiave;
}

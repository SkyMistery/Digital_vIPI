using System.Globalization;

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
/// <see cref="RisorseCondivise"/> con la cultura scritta a mano.
/// </para>
/// </summary>
public sealed class EnglishStrings
{
    /// <summary>La cultura in cui si legge, sempre. Non è la lingua di chi guarda: è una scelta di prodotto.</summary>
    private static readonly CultureInfo Inglese = CultureInfo.GetCultureInfo("en");

    /// <inheritdoc cref="RisorseCondivise.Testo"/>
    public string this[string chiave] => RisorseCondivise.Testo(chiave, Inglese);
}

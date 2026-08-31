using System.Globalization;

namespace Vipi.Ui;

/// <summary>
/// Le stringhe nella lingua di <b>chi guarda il sito</b>, anche dentro una pagina il cui documento è
/// bloccato in un'altra (carta <c>docs/feature/2026-08-31-lingua-bloccata.md</c> §5).
///
/// <para>
/// ⚠️ <b>Serve perché su una pagina bloccata <c>L["…"]</c> non risponde più nella lingua del sito</b>: il
/// localizzatore è avvolto (<see cref="LocalizzatoreDiLingua"/>) e segue il documento. È quello che si
/// vuole per le intestazioni delle tabelle — sono parte del documento — e quello che NON si vuole per il
/// chrome che <b>parla del</b> documento: la nota «questo è pubblicato solo in inglese» deve leggerla chi
/// sta guardando il sito in italiano, o non le serve a niente.
/// </para>
///
/// <para>
/// Singleton: non ha stato. La cultura la legge a ogni chiamata, come fa il localizzatore standard.
/// </para>
/// </summary>
public sealed class StringheDelSito
{
    /// <inheritdoc cref="RisorseCondivise.Testo"/>
    public string this[string chiave] => RisorseCondivise.Testo(chiave, CultureInfo.CurrentUICulture);

    /// <summary>La stessa cosa, con i segnaposto riempiti nella lingua del sito.</summary>
    public string Formatta(string chiave, params object[] argomenti) =>
        string.Format(CultureInfo.CurrentCulture, this[chiave], argomenti);
}

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
/// ⚠️ <b>LA REGOLA, in una riga: dentro una pagina documentale <c>L</c> è la lingua del DOCUMENTO,
/// <c>Sito</c> è quella di chi guarda.</b> Nelle cinque pagine viewer quasi tutto è arredamento — il tasto
/// «Stampa», la colonna di destra, la fascia dell'anteprima, l'indice — e va a <c>Sito</c>; restano a
/// <c>L</c> le poche stringhe che appartengono al documento mostrato (le intestazioni delle tabelle di un
/// vSOP, «(live · NOAA)» accanto alla testata METAR, il titolo di un blocco della vIPI ACC).
/// </para>
///
/// <para>
/// ⚠️ <b>Il confine NON si può ottenere dall'ordine di render.</b> Il primo tentativo accendeva la lingua
/// nel componente del corpo, contando sul fatto che la pagina rende prima dei figli: a schermo è uscita una
/// pagina a chiazze — «Print / SUMMARY / LINKS» accanto a «Ciclo AIRAC», e un callout «Nota» rimasto
/// italiano dentro un documento inglese. In Blazor una pagina si rende <b>più volte</b>: chi lo dà per
/// scontato scrive una regola che funziona finché qualcuno non aggiunge un ridisegno.
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

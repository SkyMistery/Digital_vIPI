using System.Globalization;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;

namespace Vipi.Ui;

/// <summary>
/// Il localizzatore delle stringhe di interfaccia, che su una pagina a <b>lingua bloccata</b> risponde nella
/// lingua del DOCUMENTO invece che in quella di chi guarda (carta
/// <c>docs/feature/2026-08-31-lingua-bloccata.md</c> §4, strato 3).
///
/// <para>
/// ⚠️ <b>Perché si avvolge la registrazione invece di toccare le pagine.</b> Le etichette che stanno DENTRO
/// un documento — le intestazioni delle tabelle derivate («Frequenza | Nominativo | Note»), le chip, i
/// cartellini — sono <c>L["…"]</c> in <b>126 file razor</b>. Passarle una lingua a mano vorrebbe dire
/// toccarli tutti e sperare che la prossima pagina scritta se ne ricordi: e chi se ne dimenticasse non
/// romperebbe niente, lascerebbe solo una tabella con l'intestazione nella lingua sbagliata in mezzo a un
/// documento nell'altra. Non è un errore, è una sfumatura — cioè il difetto che nessuno segnala.
/// </para>
///
/// <para>
/// ⚠️ <b>Vale per tutto ciò che rende DOPO, e apposta.</b> Il primo tentativo lo accendeva il componente del
/// corpo, contando sull'ordine di render per lasciare fuori l'arredamento della pagina: a schermo è uscita
/// una pagina a chiazze — «Print / SUMMARY / LINKS» inglesi accanto a «Ciclo AIRAC» italiano, e dentro il
/// documento un callout «Nota» rimasto in italiano. In Blazor una pagina può rendersi <b>più volte</b>, e
/// l'ordine fra genitore e figli non è una leva su cui appoggiare una regola di prodotto.
/// L'arredamento resta nella lingua del sito perché lo chiede a <see cref="StringheDelSito"/>, non perché
/// rende prima.
///
/// <para>
/// ⚠️ <b>Fuori da un documento bloccato non fa NIENTE</b>, e non «quasi niente»: senza lingua imposta
/// delega all'oggetto di sempre, senza passare dal <see cref="System.Resources.ResourceManager"/> e senza
/// toccare la cultura. Una funzione spenta deve somigliare a una funzione spenta.
/// </para>
///
/// <para>
/// ⚠️ <b>La lettura è sincrona</b> — nessun <c>await</c> dentro, nessuna cultura ambientale spostata e poi
/// rimessa a posto. Un localizzatore che scrivesse <c>CultureInfo.CurrentUICulture</c> per il tempo di una
/// lettura funzionerebbe lo stesso, ma sarebbe un'aiuola dove prima o poi qualcuno pianta un <c>await</c>.
/// </para>
/// </summary>
public sealed class LocalizzatoreDiLingua : IStringLocalizer<SharedResource>
{
    private readonly IStringLocalizer<SharedResource> _standard;
    private readonly ReadingLanguageContext _lingua;

    public LocalizzatoreDiLingua(IStringLocalizer<SharedResource> standard, ReadingLanguageContext lingua)
    {
        _standard = standard;
        _lingua = lingua;
    }

    /// <summary>La cultura imposta dalla pagina, o <c>null</c> se si segue chi legge.</summary>
    private CultureInfo? Imposta =>
        _lingua.Fissata is { Length: > 0 } l && !string.Equals(l, LinguaDiLettura.DelLettore(), StringComparison.OrdinalIgnoreCase)
            ? CultureInfo.GetCultureInfo(l)
            : null;

    public LocalizedString this[string name]
    {
        get
        {
            try
            {
                if (Imposta is not { } cultura) return _standard[name];
                var testo = RisorseCondivise.Manager.GetString(name, cultura);
                return new LocalizedString(name, testo ?? name, resourceNotFound: testo is null);
            }
            catch (Exception causa) { throw Illeggibile(name, causa); }
        }
    }

    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            try
            {
                if (Imposta is not { } cultura) return _standard[name, arguments];
                var formato = RisorseCondivise.Manager.GetString(name, cultura);
                // ⚠️ Anche i NUMERI seguono la lingua del documento: «1.234,5» dentro una tabella inglese
                // sarebbe la stessa incoerenza dell'intestazione italiana, solo più difficile da vedere.
                var testo = formato is null ? name : string.Format(cultura, formato, arguments);
                return new LocalizedString(name, testo, resourceNotFound: formato is null);
            }
            catch (Exception causa) { throw Illeggibile(name, causa); }
        }
    }

    /// <summary>
    /// 🔴 <b>Un'etichetta che non si legge deve dire QUALE.</b> Il 6 e il 7 settembre 2026 lo stesso utente
    /// ha visto due pagine d'errore sull'editor APP, e in tutt'e due lo stack finiva dentro il
    /// <c>BuildRenderTree</c> del componente con una <c>NullReferenceException</c> e <b>nessun fotogramma
    /// che dicesse dove</b>: in Release i metodi corti finiscono dentro il chiamante, e la riga incolpata
    /// era quella del titolo — cioè il primo <c>L["…"]</c> che quella pagina valuta.
    ///
    /// <para>⚠️ Questo <c>catch</c> non ripara niente, e non deve: rimette in piedi la sola cosa che
    /// mancava per capire, cioè <b>la chiave e le due lingue in gioco</b>. La prossima volta il registro
    /// dirà «etichetta X, lingua imposta Y», e sarà una diagnosi invece di una congettura. Vedi
    /// <c>docs/lavori-aperti.md</c> §CD.</para>
    ///
    /// <para>⚠️ Non si ingoia: si rilancia. Un'etichetta illeggibile è un guasto, e una pagina che al suo
    /// posto scrivesse la chiave sarebbe il difetto che nessuno segnala.</para>
    /// </summary>
    private Exception Illeggibile(string chiave, Exception causa) =>
        new InvalidOperationException(
            $"Etichetta «{chiave}» non leggibile — lingua imposta: {_lingua.Fissata ?? "(nessuna)"}, "
            + $"lingua del lettore: {LinguaDiLettura.DelLettore()}.", causa);

    /// <summary>
    /// Tutte le stringhe. La usa chi enumera le risorse (le prove di completezza dei resx), non le pagine:
    /// resta quella standard, perché una lista di chiavi non ha una lingua di lettura.
    /// </summary>
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
        _standard.GetAllStrings(includeParentCultures);
}

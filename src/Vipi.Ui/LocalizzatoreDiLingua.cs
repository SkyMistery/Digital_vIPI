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
            if (Imposta is not { } cultura) return _standard[name];
            var testo = RisorseCondivise.Manager.GetString(name, cultura);
            return new LocalizedString(name, testo ?? name, resourceNotFound: testo is null);
        }
    }

    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            if (Imposta is not { } cultura) return _standard[name, arguments];
            var formato = RisorseCondivise.Manager.GetString(name, cultura);
            // ⚠️ Anche i NUMERI seguono la lingua del documento: «1.234,5» dentro una tabella inglese
            // sarebbe la stessa incoerenza dell'intestazione italiana, solo più difficile da vedere.
            var testo = formato is null ? name : string.Format(cultura, formato, arguments);
            return new LocalizedString(name, testo, resourceNotFound: formato is null);
        }
    }

    /// <summary>
    /// Tutte le stringhe. La usa chi enumera le risorse (le prove di completezza dei resx), non le pagine:
    /// resta quella standard, perché una lista di chiavi non ha una lingua di lettura.
    /// </summary>
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
        _standard.GetAllStrings(includeParentCultures);
}

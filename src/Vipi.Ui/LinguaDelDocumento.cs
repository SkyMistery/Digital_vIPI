using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Ui;

/// <summary>
/// Il gesto che una pagina documentale fa una volta sola, in cima: <b>decide in che lingua si legge questo
/// documento, e se è bloccato lo dice a tutta la pagina</b> (carta
/// <c>docs/feature/2026-08-31-lingua-bloccata.md</c> §3-4).
///
/// <para>
/// ⚠️ <b>Le due cose stanno insieme apposta.</b> Scegliere la lingua e accendere l'imposizione sono un gesto
/// solo: una pagina che facesse la prima e si dimenticasse la seconda tradurrebbe la prosa del documento e
/// lascerebbe le intestazioni delle tabelle nella lingua del sito — mezza schermata in inglese e mezza in
/// italiano, senza nessun errore. È già successo, con le testate di catalogo di una vIPI d'aeroporto.
/// </para>
/// </summary>
public static class LinguaDelDocumento
{
    /// <summary>
    /// La lingua in cui rendere questo documento. Se è <b>bloccato</b>, la impone anche alla prosa generata
    /// dal backend e alle etichette dei resx per il resto della richiesta.
    /// </summary>
    /// <param name="lingua">Il contesto di lingua della richiesta (scoped).</param>
    /// <param name="bloccato">Vero se il documento si legge sempre nella lingua in cui è scritto.</param>
    /// <param name="sorgente">La lingua del documento; nulla su quelli salvati prima che il campo esistesse.</param>
    /// <param name="predefinita">La lingua in cui nasce questa famiglia, per quando la prima è nulla.</param>
    /// <param name="ancheQui">
    /// ⚠️ Un SECONDO contesto da imporre, per le pagine che possiedono uno scope di DI loro
    /// (<c>OwningComponentBase</c>): lì i servizi presi da <c>ScopedServices</c> vedono un'altra istanza di
    /// <see cref="ReadingLanguageContext"/>, non quella iniettata nella pagina. Imporne una sola vorrebbe dire
    /// documento nella lingua giusta e prosa DERIVATA nell'altra — sulla stessa schermata, senza errore.
    /// </param>
    public static string Prepara(
        ReadingLanguageContext? lingua, bool bloccato, Language? sorgente, Language predefinita,
        ReadingLanguageContext? ancheQui = null)
    {
        var codice = LinguaDiLettura.PerIlDocumento(bloccato, sorgente, predefinita);
        if (bloccato)
        {
            lingua?.Fissa(codice);
            ancheQui?.Fissa(codice);
        }
        return codice;
    }
}

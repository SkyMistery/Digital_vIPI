namespace Vipi.Application.Abstractions;

/// <summary>
/// Ogni quanto gira l'import automatico di una categoria (chiavi di <see cref="ImportCategories"/>), o
/// <c>null</c> se quella categoria <b>non</b> ha un giro automatico.
///
/// <para><b>Perché una porta e non la configurazione.</b> La cadenza sta in <c>IvaoOptions</c> e
/// <c>SectorfileOptions</c>, che vivono in <c>Vipi.Infrastructure</c>: la pagina admin non può leggerle —
/// <c>Vipi.Ui</c> referenzia solo Application e Domain — e non deve, perché sono configurazione della
/// sorgente concreta e la sorgente è sostituibile (seam <c>DataSource:Provider</c>). Alla pagina serve un
/// fatto neutro: «questo giro si ripete ogni tot».</para>
///
/// <para>Serve a dire, accanto all'ultimo successo, <b>quando è atteso il prossimo</b> — e quindi a
/// riconoscere un import fermo, che è l'unico modo in cui la tabella degli stati diventa utile prima che
/// qualcuno si accorga dei dati stantii.</para>
/// </summary>
public interface IImportSchedule
{
    /// <summary>Periodo del giro automatico della categoria; <c>null</c> se l'import è solo su richiesta.</summary>
    TimeSpan? PeriodOf(string category);
}

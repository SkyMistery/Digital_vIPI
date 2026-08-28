using System.Globalization;

namespace Vipi.Application.Tests;

/// <summary>
/// Fissa la lingua per la durata di un test, e la rimette com'era.
///
/// <para>
/// ⚠️ <b>Serve da quando i messaggi dell'applicazione hanno due lingue</b> (<c>Messaggio.Lingua</c>): la
/// lingua la legge dalla cultura ambientale, e in un test quella è la cultura della <b>macchina</b>. Un test
/// che asserisce il testo italiano senza fissarla passa in Italia e cade su una macchina inglese — e il
/// contrario. Non è una fragilità nuova che si aggiunge: è una fragilità vecchia che adesso si vede.
/// </para>
/// </summary>
internal sealed class CulturaDiProva : IDisposable
{
    private readonly CultureInfo _prima;

    private CulturaDiProva(string lingua)
    {
        _prima = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(lingua);
    }

    /// <summary>I messaggi escono in italiano finché non si chiude il blocco.</summary>
    public static CulturaDiProva Italiana() => new("it");

    /// <summary>I messaggi escono in inglese finché non si chiude il blocco.</summary>
    public static CulturaDiProva Inglese() => new("en");

    /// <summary>Una lingua che il sito NON serve: serve a provare il ripiego sull'italiano.</summary>
    public static CulturaDiProva Tedesca() => new("de");

    public void Dispose() => CultureInfo.CurrentUICulture = _prima;
}

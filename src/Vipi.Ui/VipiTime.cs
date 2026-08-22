using System.Globalization;

namespace Vipi.Ui;

/// <summary>
/// Orari a schermo: <b>sempre UTC</b>, marcati con la <c>Z</c>, e l'ora del lettore aggiunta accanto dal browser.
///
/// <para><b>Perché non <c>ToLocalTime()</c>.</b> Era la forma usata in diciannove punti, e in Blazor Server non
/// vuol dire niente di utile: <c>ToLocalTime</c> converte nel fuso del <b>server</b>, non in quello di chi guarda.
/// L'host di produzione gira in UTC, quindi «lock fino alle 14:32» era già UTC — solo senza dirlo, e con la
/// promessa implicita (sbagliata) che fosse l'ora di casa. Un fuso server diverso avrebbe dato a tutti l'ora
/// di quella macchina, che non è l'ora di nessuno.</para>
///
/// <para><b>Secondo strato.</b> Il <c>DateTime</c> che arriva dal database non porta il <see cref="DateTimeKind"/>:
/// Pomelo (MariaDB) lo restituisce <c>Unspecified</c>, e <c>ToLocalTime()</c> su un <c>Unspecified</c> lo tratta
/// come già locale, cioè <b>somma</b> lo scarto invece di sottrarlo. Le stesse scadenze passate per la via in
/// memoria (<c>AcquireAsync</c> costruisce il <c>LockInfo</c> con <c>DateTime.UtcNow</c>, Kind = Utc) prendevano
/// invece la strada giusta: due percorsi, due risultati, uguali solo perché il server sta a UTC.
/// <see cref="AsUtc"/> chiude la faccenda dichiarando il Kind una volta sola.</para>
///
/// <para><b>L'ora locale la mette il browser.</b> Il server non sa in che fuso sta il lettore, e non c'è modo di
/// saperlo senza chiederglielo. Quindi qui si emette solo l'UTC più l'istante ISO in <c>data-utc</c> (testo) o
/// <c>data-utc-title</c> (tooltip); <c>vipi-time.js</c> ci scrive accanto « · 16:32 CEST». Stessa scelta di tema
/// e zoom: quello che dipende dal client lo fa il client, e senza JS resta un orario giusto invece di uno finto.</para>
/// </summary>
public static class VipiTime
{
    /// <summary>
    /// L'istante come UTC, qualunque <see cref="DateTimeKind"/> abbia. <c>Unspecified</c> — cioè tutto ciò che
    /// esce dal database — è per convenzione già UTC: nel modello i campi si chiamano <c>…Utc</c> e ci vengono
    /// scritti con <c>DateTime.UtcNow</c>. Si <b>dichiara</b>, non si converte.
    /// </summary>
    public static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };

    /// <summary>Istante ISO 8601 con la <c>Z</c>, per l'attributo che legge <c>vipi-time.js</c>. Invariante: è un dato, non un testo.</summary>
    public static string Iso(DateTime value) =>
        AsUtc(value).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    /// <summary>
    /// Come <see cref="Iso(DateTime)"/> ma per un istante che può mancare: torna <c>null</c>, e Blazor omette
    /// del tutto l'attributo — così <c>vipi-time.js</c> non trova niente da annotare invece di trovare un vuoto.
    /// </summary>
    public static string? Iso(DateTime? value) => value is { } v ? Iso(v) : null;

    /// <summary>Ora del giorno in UTC: <c>14:32Z</c>. La <c>Z</c> è il punto — senza, è un orario senza fuso.</summary>
    public static string Z(DateTime value) =>
        AsUtc(value).ToString("HH:mm", CultureInfo.InvariantCulture) + "Z";

    /// <summary>Come <see cref="Z"/> ma coi secondi: <c>14:32:05Z</c>. Serve dove i secondi sono l'informazione (meteo).</summary>
    public static string Zs(DateTime value) =>
        AsUtc(value).ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "Z";

    /// <summary>Data sola in UTC, col mese nella lingua corrente: <c>22 ago 2026</c>. Nessuna <c>Z</c>: non è un'ora.</summary>
    public static string Day(DateTime value) =>
        AsUtc(value).ToString("dd MMM yyyy", CultureInfo.CurrentCulture);

    /// <summary>Data e ora in UTC: <c>22 ago 2026 · 14:32Z</c>.</summary>
    public static string DayZ(DateTime value) => Day(value) + " · " + Z(value);
}

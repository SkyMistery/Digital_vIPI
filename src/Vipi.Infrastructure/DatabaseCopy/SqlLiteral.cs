using System.Globalization;
using System.Text;

namespace Vipi.Infrastructure.DatabaseCopy;

/// <summary>
/// Un valore letto da MariaDB, riscritto come letterale SQL che lo reinserisce <b>identico</b>.
///
/// <para>⚠️ <b>Un tipo sconosciuto ferma la copia</b>, non si indovina. Una copia che scrive un valore
/// approssimato si scopre il giorno che serve, cioè il giorno peggiore; una copia che si ferma col nome della
/// colonna si scopre subito, da chi la scarica.</para>
///
/// <para>⚠️ <b>Nessun a capo vero esce da qui</b>: <c>\n</c> e <c>\r</c> diventano sequenze di escape. Ogni
/// <c>INSERT</c> resta così su una riga di testo, ed è quello che permette al verificatore di contare le righe
/// senza fare il parser di SQL.</para>
/// </summary>
public static class SqlLiteral
{
    /// <summary>Il letterale per <paramref name="value"/>. <paramref name="where"/> (tabella.colonna) serve
    /// solo al messaggio d'errore.</summary>
    public static string Format(object? value, string where) => value switch
    {
        null or DBNull => "NULL",
        bool b => b ? "1" : "0",
        sbyte or byte or short or ushort or int or uint or long or ulong =>
            Convert.ToString(value, CultureInfo.InvariantCulture)!,
        decimal m => m.ToString(CultureInfo.InvariantCulture),
        // «R»: il più corto che rilegge lo stesso valore. MariaDB non ha NaN né infiniti, quindi non arrivano.
        double d => d.ToString("R", CultureInfo.InvariantCulture),
        float f => f.ToString("R", CultureInfo.InvariantCulture),
        string s => Quote(s),
        byte[] bytes => bytes.Length == 0 ? "''" : "0x" + Convert.ToHexString(bytes),
        DateTime dt => "'" + dt.ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture) + "'",
        DateTimeOffset dto => "'" + dto.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture) + "'",
        DateOnly d => "'" + d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "'",
        TimeOnly t => "'" + t.ToString("HH:mm:ss.ffffff", CultureInfo.InvariantCulture) + "'",
        TimeSpan ts => Time(ts),
        Guid g => "'" + g.ToString("D") + "'",
        _ => throw new NotSupportedException(
            $"La copia non sa scrivere un valore di tipo {value.GetType().FullName} (colonna {where}). " +
            "Si ferma invece di scriverne uno approssimato."),
    };

    /// <summary>Stringa fra apici con gli escape di MariaDB (<c>NO_BACKSLASH_ESCAPES</c> spento, come lo
    /// imposta la testata del file).</summary>
    public static string Quote(string s)
    {
        var sb = new StringBuilder(s.Length + 2);
        sb.Append('\'');
        foreach (var c in s)
        {
            switch (c)
            {
                case '\0': sb.Append("\\0"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\\': sb.Append("\\\\"); break;
                case '\'': sb.Append("\\'"); break;
                case '"': sb.Append("\\\""); break;
                case '\x1a': sb.Append("\\Z"); break;
                default: sb.Append(c); break;
            }
        }
        sb.Append('\'');
        return sb.ToString();
    }

    /// <summary>Un nome di tabella o colonna fra backtick.</summary>
    public static string Identifier(string name) => "`" + name.Replace("`", "``") + "`";

    /// <summary>Il <c>TIME</c> di MariaDB va oltre le 24 ore (fino a 838): si scrive in ore totali.</summary>
    private static string Time(TimeSpan ts)
    {
        var segno = ts < TimeSpan.Zero ? "-" : "";
        var a = ts.Duration();
        var ore = (long)a.TotalHours;
        var micro = a.Ticks % TimeSpan.TicksPerSecond / 10;
        return string.Create(CultureInfo.InvariantCulture,
            $"'{segno}{ore}:{a.Minutes:00}:{a.Seconds:00}.{micro:000000}'");
    }
}

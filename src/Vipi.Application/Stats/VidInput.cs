using System;
using System.Linq;

namespace Vipi.Application.Stats;

/// <summary>
/// Che cosa ha scritto uno staffista nel campo «cerca un VID».
///
/// <para>Puro, perché è la sola parte del cerca-per-VID che possa sbagliare: il resto è una navigazione.
/// Chi cerca una persona <b>non digita</b> quasi mai il numero nudo — lo incolla da dove ce l'ha, e da lì
/// arriva con l'etichetta davanti («VID 704798»), col punto interrogativo di un indirizzo
/// (<c>https://ivao.aero/Member.aspx?Id=704798</c>) o con uno spazio in mezzo alle migliaia. Rifiutare tutto
/// ciò che non sia il numero nudo vorrebbe dire far ripulire a mano quel che il programma sa ripulire.</para>
/// </summary>
public static class VidInput
{
    /// <summary>Il VID più corto mai emesso da IVAO ha sei cifre; sotto le cinque è certamente altro.</summary>
    private const int MinCifre = 5;

    /// <summary>Oltre le otto cifre non è un VID ma un identificativo di qualcos'altro (un piano di volo, una sessione).</summary>
    private const int MaxCifre = 8;

    /// <summary>
    /// Il VID scritto dall'utente, oppure <c>null</c> se non ce n'è uno riconoscibile.
    ///
    /// <para>⚠️ Si prende la <b>prima</b> sequenza di cifre buona, non l'ultima: in
    /// <c>https://ivao.aero/Member.aspx?Id=704798</c> la sola sequenza lunga è il VID, ma in un indirizzo con
    /// più parametri l'ultima potrebbe essere un numero di pagina.</para>
    ///
    /// <para>⚠️ Gli spazi <b>dentro</b> il numero si tolgono («704 798» → 704798) perché è così che un numero
    /// si incolla da un foglio di calcolo; i separatori delle migliaia con il punto <b>no</b>, o
    /// <c>Member.aspx</c> diventerebbe un VID.</para>
    /// </summary>
    public static int? Parse(string? scritto)
    {
        if (string.IsNullOrWhiteSpace(scritto)) return null;

        // Gli spazi interni spariscono prima di cercare: «704 798» è un numero solo scritto da una persona.
        var testo = new string(scritto.Where(c => !char.IsWhiteSpace(c)).ToArray());

        var i = 0;
        while (i < testo.Length)
        {
            if (!char.IsAsciiDigit(testo[i])) { i++; continue; }

            var inizio = i;
            while (i < testo.Length && char.IsAsciiDigit(testo[i])) i++;

            var lunghezza = i - inizio;
            if (lunghezza is >= MinCifre and <= MaxCifre &&
                int.TryParse(testo.AsSpan(inizio, lunghezza), out var vid) && vid > 0)
                return vid;
        }

        return null;
    }
}

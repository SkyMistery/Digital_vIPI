using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vipi.Application.Import;

/// <summary>
/// Scrive una tabella come CSV. E' il verso opposto dell'import, e chiude il giro: si esporta, si sistema
/// nel foglio di calcolo, si reimporta con «sostituisci».
///
/// <para>
/// ⚠️ Il separatore e' il <b>punto e virgola</b>. Con la virgola, un foglio di calcolo italiano apre il file
/// in una colonna sola — e chi esporta lo fa proprio per aprirlo li'. E' anche il primo separatore che
/// l'import prova, quindi il giro si chiude senza che nessuno debba scegliere niente.
/// </para>
/// <para>
/// ⚠️ Il testo comincia con il <b>segno d'ordine dei byte</b>. Senza, Excel legge un CSV UTF-8 con la
/// codifica di sistema e le lettere accentate diventano segni: e' la differenza fra «Grazzanise» e un
/// nome che sembra corrotto in una tabella che era giusta.
/// </para>
/// </summary>
public static class Csv
{
    /// <summary>Il separatore di colonna, e il primo che l'import riconosce.</summary>
    public const char Separatore = ';';

    /// <summary>Il segno d'ordine dei byte, in testa al file perche' Excel riconosca l'UTF-8.</summary>
    public const string SegnoUtf8 = "\uFEFF";

    /// <summary>La tabella come testo CSV, intestazione compresa quando c'e'.</summary>
    public static string Scrivi(
        IReadOnlyList<string> colonne, IEnumerable<IReadOnlyList<string>> righe)
    {
        var sb = new StringBuilder(SegnoUtf8);
        if (colonne.Count > 0) sb.Append(Riga(colonne)).Append('\n');
        foreach (var r in righe) sb.Append(Riga(r)).Append('\n');
        return sb.ToString();
    }

    private static string Riga(IReadOnlyList<string> celle) =>
        string.Join(Separatore, celle.Select(Cella));

    /// <summary>
    /// Una cella: fra virgolette solo se serve, con l'apice raddoppiato.
    /// <para>⚠️ Anche l'a-capo obbliga alle virgolette, o una cella su due righe diventa due righe di
    /// tabella — e nessuno se ne accorge finche' non riapre il file.</para>
    /// </summary>
    private static string Cella(string? testo)
    {
        var t = testo ?? "";
        var serve = t.IndexOf(Separatore) >= 0 || t.IndexOf('"') >= 0
                    || t.IndexOf('\n') >= 0 || t.IndexOf('\r') >= 0;
        return serve ? "\"" + t.Replace("\"", "\"\"") + "\"" : t;
    }

    /// <summary>
    /// Un nome di file che non fa arrabbiare nessun sistema: solo lettere, cifre, trattini e il punto
    /// dell'estensione.
    /// </summary>
    public static string NomeFile(string titolo)
    {
        var pulito = new string((titolo ?? "tabella")
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray()).Trim('-');
        while (pulito.Contains("--")) pulito = pulito.Replace("--", "-");
        return (pulito.Length > 0 ? pulito : "tabella") + ".csv";
    }
}

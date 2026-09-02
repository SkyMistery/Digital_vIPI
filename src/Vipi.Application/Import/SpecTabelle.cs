using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Vipi.Application.Import;

/// <summary>
/// Le specifiche delle tabelle <b>legate ai cataloghi</b>: quelle in cui una cella non e' testo ma il
/// riferimento a qualcosa che esiste altrove.
/// </summary>
public static class SpecTabelle
{
    /// <summary>La chiave della tabella «Aeroporti alternati» (carta vSOP militari §12f).</summary>
    public const string Alternati = "mildiversion";

    /// <summary>
    /// «Aeroporti alternati»: scalo dall'archivio, radioassistenze dall'anagrafica, rilevamento e distanza
    /// scritti nel documento.
    /// <para>I titoli arrivano gia' tradotti dalla pagina, e fanno anche da nomi per riconoscere
    /// l'intestazione; i sinonimi inglesi sono quelli dei SOP veri.</para>
    /// </summary>
    public static SpecImport AeroportiAlternati(
        string aeroporto, string radioassistenze, string rilevamento, string distanza) =>
        new(Alternati,
            new[]
            {
                new ColonnaSpec("icao", aeroporto, TipoCella.Aeroporto, Obbligatoria: true,
                    Sinonimi: new[] { "AIRPORT", "AIRPORTS", "ICAO", "AERODROME" }),
                new ColonnaSpec("navaids", radioassistenze, TipoCella.Radioassistenza,
                    Sinonimi: new[] { "NAVAIDS", "NAVAID", "RADIOAIDS" }),
                new ColonnaSpec("bearing", rilevamento, TipoCella.Intero,
                    Sinonimi: new[] { "BEARING", "QDR", "RADIAL" }),
                new ColonnaSpec("distance", distanza, TipoCella.Decimale,
                    Sinonimi: new[] { "DISTANCE", "DIST", "NM" }),
            },
            SpezzaRiga: SpezzaAlternato);

    /// <summary>I tipi d'impianto che si scrivono accanto all'ident in un SOP: servono a capire DOVE finisce
    /// il nome dell'aeroporto e comincia la radioassistenza.</summary>
    private static readonly HashSet<string> Impianti = new(StringComparer.OrdinalIgnoreCase)
    {
        "TAC", "TACAN", "VOR", "VORTAC", "VORTACAN", "VOR/DME", "VORDME", "DME", "NDB", "ILS", "LOC", "L",
    };

    /// <summary>
    /// La coda di una riga: rilevamento e distanza, con o senza le loro unita'.
    /// <para>⚠️ Sono <b>ancore</b>, cioe' si cercano dal FONDO: e' l'unica parte della riga la cui forma e'
    /// certa. Il mezzo — nome dello scalo e impianti citati — e' fatto di parole, e contarle non funziona.</para>
    /// </summary>
    private static readonly Regex Coda = new(
        @"^\s*([A-Z]{4})\s+(.*?)\s+(\d{1,3})\s*°?\s+(\d+(?:[.,]\d+)?)\s*(?:NM)?\s*$",
        RegexOptions.IgnoreCase);

    /// <summary>
    /// Spezza una riga di «Aeroporti alternati» copiata da un PDF, dove di separatori non ce n'e' nessuno:
    /// <c>LIBA Amendola MNL TAC - 99Y 115.25 308° 72.2NM</c>.
    ///
    /// <para>
    /// ⚠️ Non si spezza per spazi: darebbe sette colonne e nessuna giusta. Si prendono le due <b>ancore</b>
    /// in coda (i due numeri con le loro unita') e l'ICAO in testa; quel che resta in mezzo e' il nome dello
    /// scalo <i>piu'</i> gli impianti, e i due si separano al primo ident seguito da un tipo d'impianto —
    /// <c>MNL TAC</c>. Senza quel segno, tutto il mezzo va alle radioassistenze e l'anteprima lo mostra:
    /// meglio una cella evidentemente da correggere che un nome tagliato a meta' di nascosto.
    /// </para>
    /// </summary>
    public static string[]? SpezzaAlternato(string riga)
    {
        var m = Coda.Match(TestoTabellare.NormalizzaSegni(riga));
        if (!m.Success) return null;

        var icao = m.Groups[1].Value.ToUpperInvariant();
        var mezzo = m.Groups[2].Value.Trim();
        var rilevamento = m.Groups[3].Value;
        var distanza = m.Groups[4].Value;

        var parole = mezzo.Split(' ').Where(p => p.Length > 0).ToArray();
        var inizio = -1;
        for (var i = 0; i + 1 < parole.Length; i++)
        {
            if (!Ident(parole[i]) || !Impianti.Contains(parole[i + 1])) continue;
            inizio = i;
            break;
        }

        var nome = inizio > 0 ? string.Join(' ', parole.Take(inizio)) : "";
        var impianti = inizio >= 0 ? string.Join(' ', parole.Skip(inizio)) : mezzo;

        return new[]
        {
            nome.Length > 0 ? icao + " " + nome : icao,
            impianti,
            rilevamento,
            distanza,
        };
    }

    /// <summary>Un ident d'impianto: da due a cinque lettere o cifre, tutte maiuscole.</summary>
    private static bool Ident(string parola) =>
        parola.Length is >= 2 and <= 5 && parola.All(c => char.IsDigit(c) || (char.IsLetter(c) && char.IsUpper(c)));
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace Vipi.Application.Import;

/// <summary>
/// Quale colonna della griglia va in quale colonna della tabella, e se la prima riga e' un'intestazione.
/// </summary>
/// <param name="Colonne">
/// Una voce per colonna della specifica, nello stesso ordine: l'indice della colonna della griglia da cui
/// prenderla, oppure <c>-1</c> se non e' stata trovata. ⚠️ L'utente la puo' cambiare a mano — questa e' la
/// <b>proposta</b>, non un verdetto.
/// </param>
/// <param name="Intestazione">Vero se la prima riga della griglia e' un'intestazione, e quindi non un dato.</param>
public sealed record MappaturaColonne(IReadOnlyList<int> Colonne, bool Intestazione)
{
    /// <summary>Quante colonne della specifica hanno trovato posto.</summary>
    public int Trovate => Colonne.Count(i => i >= 0);

    /// <summary>La mappatura in cui la colonna <paramref name="colonnaSpec"/> viene da
    /// <paramref name="colonnaGriglia"/> (o da nessuna, con -1).</summary>
    public MappaturaColonne Con(int colonnaSpec, int colonnaGriglia)
    {
        if (colonnaSpec < 0 || colonnaSpec >= Colonne.Count) return this;
        var copia = Colonne.ToArray();
        copia[colonnaSpec] = colonnaGriglia;
        return this with { Colonne = copia };
    }

    /// <summary>
    /// La mappatura proposta per questa griglia.
    ///
    /// <para>⚠️ <b>L'intestazione si riconosce, non si presume.</b> Chi incolla mezza tabella parte dalla
    /// prima riga di dati, e togliergliela perche' «la prima riga e' sempre l'intestazione» vuol dire
    /// perdere una riga in silenzio — che e' il modo peggiore di perderla. Si considera intestazione solo la
    /// riga che <b>nomina</b> almeno meta' delle colonne dichiarate.</para>
    ///
    /// <para>⚠️ Senza intestazione riconosciuta le colonne si prendono <b>in ordine</b>. Non e' un ripiego
    /// pigro: e' l'unica ipotesi che chi incolla puo' verificare guardando l'anteprima, mentre un
    /// accoppiamento indovinato dai contenuti sarebbe giusto quasi sempre e sbagliato senza dirlo.</para>
    /// </summary>
    public static MappaturaColonne Proponi(SpecImport spec, Griglia griglia)
    {
        if (spec.ColonneLibere || spec.Colonne.Count == 0)
            return new MappaturaColonne(Array.Empty<int>(), SembraIntestazione(spec, griglia.Riga(0)));

        var prima = griglia.Riga(0);
        var intestazione = SembraIntestazione(spec, prima);
        var mappa = new int[spec.Colonne.Count];

        if (intestazione)
        {
            var presi = new HashSet<int>();
            for (var c = 0; c < spec.Colonne.Count; c++)
            {
                mappa[c] = -1;
                for (var g = 0; g < prima.Count; g++)
                {
                    if (presi.Contains(g) || !Combacia(spec.Colonne[c], prima[g])) continue;
                    mappa[c] = g;
                    presi.Add(g);
                    break;
                }
            }
        }
        else
        {
            for (var c = 0; c < spec.Colonne.Count; c++)
                mappa[c] = c < griglia.Colonne ? c : -1;
        }

        return new MappaturaColonne(mappa, intestazione);
    }

    /// <summary>
    /// Vero se questa riga nomina almeno meta' delle colonne dichiarate.
    ///
    /// <para>⚠️ Vale anche per una riga rimasta <b>tutta in una cella</b>, e la verifica dal vivo del
    /// 2 settembre 2026 ha mostrato perche': incollando una tabella copiata da un PDF, la riga
    /// «AIRPORT NAVAIDS BEARING DISTANCE» non si spezza — le ancore cercano un ICAO e due numeri, e
    /// un'intestazione non ne ha — quindi restava una cella sola e finiva fra i dati, <b>rossa</b>. Chi
    /// importava vedeva una riga illeggibile che era solo il titolo della tabella.</para>
    /// </summary>
    private static bool SembraIntestazione(SpecImport spec, IReadOnlyList<string> riga)
    {
        if (spec.ColonneLibere || spec.Colonne.Count == 0 || riga.Count == 0) return false;

        var combacianti = spec.Colonne.Count(c => riga.Any(cella => Combacia(c, cella)));
        if (combacianti * 2 >= spec.Colonne.Count) return true;

        if (riga.Count > 1) return false;
        var parole = (riga[0] ?? "").Split(new[] { ' ', '	' }, StringSplitOptions.RemoveEmptyEntries);
        var nominate = spec.Colonne.Count(c => parole.Any(pa => Combacia(c, pa)));
        return nominate * 2 >= spec.Colonne.Count;
    }

    /// <summary>
    /// Il confronto fra un'intestazione scritta e il nome di una colonna: senza accenti, senza segni,
    /// senza maiuscole. «Rilevamento», «BEARING» e «bearing» sono la stessa colonna.
    /// </summary>
    private static bool Combacia(ColonnaSpec colonna, string? cella)
    {
        var testo = Chiave(cella);
        return testo.Length > 0 && colonna.Nomi.Any(n => Chiave(n) == testo);
    }

    private static string Chiave(string? s) =>
        new string((s ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
}

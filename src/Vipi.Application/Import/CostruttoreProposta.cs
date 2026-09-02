using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vipi.Application.Coordinates;
using Vipi.Domain;

namespace Vipi.Application.Import;

/// <summary>
/// Il terzo stadio: da una griglia di celle a una <b>proposta</b> da guardare in faccia.
///
/// <para>⚠️ Qui non si scrive niente e non si sa nemmeno dove si scriverebbe. Il costruttore mette insieme
/// tre cose che sanno una cosa sola ciascuna — la griglia (le celle), la specifica (che cosa sono), il
/// risolutore (che cosa ne dice il catalogo) — e produce quel che l'anteprima mostra.</para>
/// </summary>
public static class CostruttoreProposta
{
    /// <summary>Quante righe si accetta di leggere in un colpo: oltre, non e' piu' una tabella di documento
    /// ma un travaso, e i travasi hanno i loro importer.</summary>
    public const int MaxRighe = 2000;

    /// <summary>
    /// La proposta per questa griglia, con la mappatura suggerita.
    /// </summary>
    public static Task<Proposta> CostruisciAsync(
        Griglia griglia, SpecImport spec, IRisolutoreCelle? risolutore = null,
        CancellationToken ct = default) =>
        CostruisciAsync(griglia, spec, MappaturaColonne.Proponi(spec, griglia), risolutore, ct);

    /// <summary>
    /// La proposta per questa griglia con una mappatura <b>gia' decisa</b> (l'utente l'ha corretta a mano).
    /// </summary>
    public static async Task<Proposta> CostruisciAsync(
        Griglia griglia, SpecImport spec, MappaturaColonne mappatura,
        IRisolutoreCelle? risolutore = null, CancellationToken ct = default)
    {
        if (!griglia.Piena) return Proposta.Niente(spec);

        var dati = mappatura.Intestazione ? griglia.SenzaPrima() : griglia;
        var saltate = mappatura.Intestazione ? 1 : 0;
        var righeDati = dati.Righe.Take(MaxRighe).ToList();

        var colonne = Colonne(spec, griglia, mappatura);
        var tipi = Tipi(spec, colonne.Count);

        // I valori da cercare sul catalogo, raccolti per tipo PRIMA di costruire le righe: e' l'unico modo
        // di farne una interrogazione per tipo invece di una per cella.
        var daRisolvere = new Dictionary<TipoCella, HashSet<string>>();
        for (var r = 0; r < righeDati.Count; r++)
            for (var c = 0; c < colonne.Count; c++)
            {
                if (!DaCatalogo(tipi[c])) continue;
                var grezzo = Cella(righeDati[r], spec, mappatura, c);
                if (grezzo.Length == 0) continue;
                if (!daRisolvere.TryGetValue(tipi[c], out var insieme))
                    daRisolvere[tipi[c]] = insieme = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                insieme.Add(grezzo);
            }

        var risolti = new Dictionary<TipoCella, IReadOnlyDictionary<string, EsitoRisoluzione>>();
        foreach (var kv in daRisolvere)
            risolti[kv.Key] = risolutore is null
                ? new Dictionary<string, EsitoRisoluzione>()
                : await risolutore.RisolviAsync(kv.Key, kv.Value.ToList(), ct).ConfigureAwait(false);

        var righe = new List<RigaProposta>();
        for (var r = 0; r < righeDati.Count; r++)
        {
            var celle = new List<CellaProposta>();
            for (var c = 0; c < colonne.Count; c++)
                celle.Add(Leggi(Cella(righeDati[r], spec, mappatura, c), tipi[c], risolti));

            righe.Add(new RigaProposta(r + 1 + saltate, string.Join(" | ", righeDati[r]), celle));
        }

        return new Proposta(spec, colonne, mappatura, righe);
    }

    // ---- colonne e tipi ------------------------------------------------------------------------------

    /// <summary>
    /// I titoli delle colonne: quelli della specifica quando ci sono, altrimenti l'intestazione incollata —
    /// e in mancanza d'entrambi un numero, perche' una colonna senza nome resta una colonna.
    /// </summary>
    private static IReadOnlyList<string> Colonne(SpecImport spec, Griglia griglia, MappaturaColonne mappatura)
    {
        if (!spec.ColonneLibere && spec.Colonne.Count > 0)
            return spec.Colonne.Select(c => c.Titolo).ToList();

        var quante = griglia.Colonne;
        if (mappatura.Intestazione)
        {
            var prima = griglia.Riga(0);
            return Enumerable.Range(0, quante)
                .Select(i => i < prima.Count && prima[i].Length > 0 ? prima[i] : Numerata(i))
                .ToList();
        }
        return Enumerable.Range(0, quante).Select(Numerata).ToList();
    }

    private static string Numerata(int i) =>
        "Colonna " + (i + 1).ToString(CultureInfo.InvariantCulture);

    private static IReadOnlyList<TipoCella> Tipi(SpecImport spec, int quante) =>
        !spec.ColonneLibere && spec.Colonne.Count > 0
            ? spec.Colonne.Select(c => c.Tipo).ToList()
            : Enumerable.Repeat(TipoCella.Testo, quante).ToList();

    /// <summary>La cella grezza per la colonna <paramref name="colonna"/>: dalla mappatura quando la
    /// specifica ha colonne proprie, in ordine quando le colonne sono libere.</summary>
    private static string Cella(
        IReadOnlyList<string> riga, SpecImport spec, MappaturaColonne mappatura, int colonna)
    {
        var g = spec.ColonneLibere || mappatura.Colonne.Count == 0
            ? colonna
            : colonna < mappatura.Colonne.Count ? mappatura.Colonne[colonna] : -1;
        return g >= 0 && g < riga.Count ? riga[g].Trim() : "";
    }

    private static bool DaCatalogo(TipoCella tipo) =>
        tipo is TipoCella.Aeroporto or TipoCella.Radioassistenza;

    // ---- lettura di una cella -------------------------------------------------------------------------

    private static CellaProposta Leggi(
        string grezzo, TipoCella tipo,
        IReadOnlyDictionary<TipoCella, IReadOnlyDictionary<string, EsitoRisoluzione>> risolti)
    {
        if (grezzo.Length == 0) return CellaProposta.Vuota();

        if (DaCatalogo(tipo))
        {
            if (risolti.TryGetValue(tipo, out var mappa) && mappa.TryGetValue(grezzo, out var e))
                return new CellaProposta(grezzo, e.Valore, e.Esito, e.Chiave, e.Nota, e.Candidati);

            // Nessuno ha risposto per questo valore: e' sconosciuto al catalogo. ⚠️ Non si crea niente e non
            // si tiene il testo com'e' — una cella che sembra a posto e cita un impianto inesistente e'
            // peggio di una cella rossa.
            return new CellaProposta(grezzo, "", EsitoCella.NonLetta, Nota: "sconosciuto");
        }

        switch (tipo)
        {
            case TipoCella.Intero:
                return TestoTabellare.Numero(grezzo) is { } n
                    ? new CellaProposta(grezzo,
                        decimal.Round(n, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture),
                        EsitoCella.Letta)
                    : new CellaProposta(grezzo, "", EsitoCella.NonLetta, Nota: "non e' un numero");

            case TipoCella.Decimale:
                return TestoTabellare.Numero(grezzo) is { } nm
                    ? new CellaProposta(grezzo, nm.ToString("0.#", CultureInfo.InvariantCulture), EsitoCella.Letta)
                    : new CellaProposta(grezzo, "", EsitoCella.NonLetta, Nota: "non e' un numero");

            case TipoCella.Livello:
                // ⚠️ La rilettura di un livello non fallisce mai: cio' che non e' un livello diventa il
                // livello «speciale», che e' un valore legittimo. Quindi non c'e' un errore da riportare —
                // c'e' da mostrare il livello RESO, cosi' chi rilegge vede che cosa il sistema ha capito.
                var liv = LevelFormatting.Parse(grezzo);
                return new CellaProposta(
                    grezzo,
                    LevelFormatting.Format(liv.Value, liv.Unit, liv.Constraint, liv.Special, liv.Parity,
                        liv.VerticalState),
                    EsitoCella.Letta);

            case TipoCella.Coordinata:
                var lette = CoordinateParser.Parse(grezzo);
                var punti = lette.Aree.Sum(a => a.Punti.Count);
                return punti > 0
                    ? new CellaProposta(grezzo, grezzo, EsitoCella.Letta)
                    : new CellaProposta(grezzo, "", EsitoCella.NonLetta, Nota: "non e' una coordinata");

            default:
                return new CellaProposta(grezzo, grezzo, EsitoCella.Letta);
        }
    }
}

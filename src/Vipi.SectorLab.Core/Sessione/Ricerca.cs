using Vipi.SectorLab.Core.Mappa;

namespace Vipi.SectorLab.Core.Sessione;

/// <summary>Una cosa trovata: un record dentro un file.</summary>
/// <param name="Strato">Lo strato della mappa in cui sta, per accenderlo quando ci si va.</param>
public sealed record Trovato(string File, int Record, string Etichetta, string Strato);

/// <summary>
/// La ricerca per nome (carta F3 §2.2 passo 2, slice 5): fix, VOR, procedure, settori, aree.
/// <para>Cerca fra le <b>forme della mappa</b>, che sono già in memoria con la loro etichetta: la stessa parola che
/// si vede a schermo, e l'aggancio con mappa e ispettore è lo stesso (file + indice del record). ⚠️ Perciò in questa
/// slice i record <b>senza geometria</b> (le posizioni ATC dei <c>.frq</c>, gli ATIS) non si trovano per nome: si
/// arriva a loro sfogliando il file. Quando serviranno, l'indice si allarga qui — non in una seconda ricerca.</para>
/// <para>L'ordine non è alfabetico: prima chi si chiama <b>esattamente</b> così, poi chi <b>comincia</b> così, poi
/// chi lo contiene. Chi cerca «LIRF» vuole LIRF, non LIRF_APP_NORD perché viene prima nell'alfabeto.</para>
/// </summary>
public static class Ricerca
{
    /// <summary>Quanti risultati al massimo: un elenco più lungo non si legge, si raffina la ricerca.</summary>
    public const int Tetto = 60;

    public static IReadOnlyList<Trovato> Cerca(IReadOnlyList<StratoDellaMappa> strati, string? testo, int tetto = Tetto)
    {
        ArgumentNullException.ThrowIfNull(strati);
        string cercato = (testo ?? "").Trim();
        if (cercato.Length < 2)
            return [];

        var trovati = new List<(int Quanto, Trovato Voce)>();
        foreach (var strato in strati)
        {
            foreach (var forma in strato.Forme)
            {
                int quanto = Quanto(forma.Etichetta, cercato);
                if (quanto > 0)
                    trovati.Add((quanto, new Trovato(forma.File, forma.Record, forma.Etichetta, strato.Id)));
            }
        }

        return [.. trovati
            .OrderByDescending(t => t.Quanto)
            .ThenBy(t => t.Voce.Etichetta, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.Voce.File, StringComparer.OrdinalIgnoreCase)
            .Take(tetto)
            .Select(t => t.Voce)];
    }

    /// <summary>
    /// I file per nome (committente, 28 settembre: «in cerca voglio anche poter cercare i file per nome»): il nome con o
    /// senza estensione (<c>lirf</c> trova <c>lirf.sid</c>, <c>lirf.str</c>…), poi il percorso (<c>GND_LAYOUT/rf</c>).
    /// Stesso ordine dei record: uguale, comincia così, lo contiene; a parità, per percorso.
    /// </summary>
    /// <param name="relativi">I percorsi dei file, relativi alla radice del clone.</param>
    public static IReadOnlyList<string> CercaFile(IEnumerable<string> relativi, string? testo, int tetto = 20)
    {
        ArgumentNullException.ThrowIfNull(relativi);
        string cercato = (testo ?? "").Trim().Replace('\\', '/');
        if (cercato.Length < 2)
            return [];

        return [.. relativi
            .Select(r => (Relativo: r, Quanto: QuantoDelFile(r, cercato)))
            .Where(t => t.Quanto > 0)
            .OrderByDescending(t => t.Quanto)
            .ThenBy(t => t.Relativo, StringComparer.OrdinalIgnoreCase)
            .Take(tetto)
            .Select(t => t.Relativo)];
    }

    private static int QuantoDelFile(string relativo, string cercato)
    {
        string nome = relativo[(relativo.LastIndexOf('/') + 1)..];
        int quanto = Math.Max(Quanto(nome, cercato), Quanto(Path.GetFileNameWithoutExtension(nome), cercato));
        return quanto > 0 ? quanto : relativo.Contains(cercato, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }

    /// <summary>3 = uguale, 2 = comincia così, 1 = lo contiene, 0 = no.</summary>
    private static int Quanto(string etichetta, string cercato)
    {
        if (etichetta.Equals(cercato, StringComparison.OrdinalIgnoreCase))
            return 3;
        if (etichetta.StartsWith(cercato, StringComparison.OrdinalIgnoreCase))
            return 2;
        return etichetta.Contains(cercato, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }
}

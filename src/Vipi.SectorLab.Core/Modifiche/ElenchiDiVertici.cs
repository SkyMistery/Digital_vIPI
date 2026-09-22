using System.Collections;
using System.Reflection;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Core.Modifiche;

/// <summary>
/// Un elenco di vertici di un record, comunque il modello lo tenga (slice 7 e 7-bis).
/// <para>Nel sector la stessa cosa — «i punti di questa forma» — è scritta in tre modi: un elenco di
/// <see cref="Coordinate"/> (<c>.pol</c>, <c>.lairway</c>), un elenco di <see cref="Punto"/> che ammette anche i
/// nomi (<c>.tfl</c>, <c>.mva</c>, <c>.vrt</c>), e un elenco di <b>involucri</b> che portano il punto più altro:
/// <see cref="PuntoDelTracciato"/> delle <c>.sid</c> (etichetta e «nuovo tratto») e i <b>segmenti</b> delle zone
/// <c>.str</c>, che sono elenchi dentro un elenco.</para>
/// <para>Chi modifica un vertice non deve sapere quale dei tre: qui si legge e si scrive un punto, e quel che
/// l'involucro porta in più <b>resta</b> — l'etichetta di una SID non si perde spostando il suo punto.</para>
/// </summary>
public sealed class ElencoDiVertici
{
    internal ElencoDiVertici(string chiave, string nome, IList elenco, Type dentro)
    {
        Chiave = chiave;
        Nome = nome;
        Elenco = elenco;
        _dentro = dentro;
    }

    private readonly Type _dentro;

    /// <summary>Come si nomina nell'indirizzo: <c>Vertices</c>, <c>Track</c>, <c>Segments[2].Points</c>.</summary>
    public string Chiave { get; }

    /// <summary>Come si legge a schermo: «Vertici», «Tracciato», «Tratto 3».</summary>
    public string Nome { get; }

    public IList Elenco { get; }

    public int Quanti => Elenco.Count;

    /// <summary>Vero dove un vertice si può scrivere per NOME: il file lo sa riscrivere.</summary>
    public bool AmmetteNomi => _dentro == typeof(Punto) || _dentro == typeof(PuntoDelTracciato);

    /// <summary>Il vertice in posizione data, come si scrive a schermo.</summary>
    public string Scrivi(int posizione)
    {
        object? voce = posizione >= 0 && posizione < Elenco.Count ? Elenco[posizione] : null;
        return voce switch
        {
            Coordinate c => CoordinateConverter.ToDottedDms(c),
            Punto p => ScriviIlPunto(p),
            PuntoDelTracciato t => ScriviIlPunto(t.Punto),
            _ => "",
        };
    }

    private static string ScriviIlPunto(Punto p)
        => p.PerNome
            ? p.Nome == p.NomeLongitudine ? p.Nome! : $"{p.Nome} {p.NomeLongitudine}"
            : CoordinateConverter.ToDottedDms(p.Posizione!.Value);

    /// <summary>
    /// Un vertice nel tipo di questo elenco. <paramref name="vecchio"/> è la voce che si sta sostituendo, se c'è:
    /// quel che l'involucro porta in più (l'etichetta di una SID, il segno di nuovo tratto) si tiene.
    /// </summary>
    public object Fabbrica(Punto punto, object? vecchio)
    {
        if (_dentro == typeof(Coordinate))
        {
            // Un elenco di sole coordinate non può tenere un nome: chi chiama lo ha già rifiutato (AmmetteNomi).
            // 🔴 Niente ternari qui dentro: Punto ha una conversione IMPLICITA da Coordinate, e in un ternario il
            // tipo comune diventa Punto — così anche questo ramo usciva come Punto e ogni .pol cadeva a tempo
            // d'esecuzione (slice 7, trovato dalla misura). Con i rami separati il tipo è quello che si legge.
            return punto.Posizione ?? throw new InvalidOperationException("Qui un vertice va per coordinate.");
        }

        if (_dentro == typeof(Punto))
            return punto;

        return new PuntoDelTracciato
        {
            Punto = punto,
            Etichetta = (vecchio as PuntoDelTracciato)?.Etichetta,
            NuovoTratto = (vecchio as PuntoDelTracciato)?.NuovoTratto ?? false,
        };
    }
}

/// <summary>Dove stanno i vertici di un record: uno, nessuno, o tanti (un tratto per volta).</summary>
public static class ElenchiDiVertici
{
    /// <summary>
    /// Tutti gli elenchi di vertici di un record. Per una zona <c>.str</c> ce n'è uno per <b>tratto</b>: i tratti
    /// sono pezzi diversi della stessa mappa, e si modificano uno alla volta.
    /// </summary>
    public static IReadOnlyList<ElencoDiVertici> Di(FileAperto file, int indice)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file is not IFileConRecord conRecord || indice < 0 || indice >= conRecord.RecordDelModello.Count)
            return [];

        object record = conRecord.RecordDelModello[indice];
        var trovati = new List<ElencoDiVertici>();

        foreach (var proprieta in record.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (proprieta.GetIndexParameters().Length > 0 || proprieta.GetValue(record) is not IList elenco)
                continue;

            var dentro = TipoDentro(proprieta.PropertyType);
            if (dentro is null)
                continue;

            if (EUnPunto(dentro))
            {
                trovati.Add(new ElencoDiVertici(proprieta.Name, NomeDelCampo(proprieta.Name), elenco, dentro));
                continue;
            }

            // Un elenco di elenchi: i segmenti di una zona .str. Un tratto per voce, col suo numero.
            for (int i = 0; i < elenco.Count; i++)
            {
                if (elenco[i] is not { } voce)
                    continue;
                foreach (var interna in dentro.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    // 🔴 Anche qui la guardia sugli indicizzatori: un `this[int]` chiesto senza argomenti fa
                    // «Parameter count mismatch». Nel ciclo di fuori c'era, qui no — l'ha trovato la misura.
                    if (interna.GetIndexParameters().Length > 0)
                        continue;
                    if (interna.GetValue(voce) is not IList dentroIl || TipoDentro(interna.PropertyType) is not { } tipo || !EUnPunto(tipo))
                        continue;
                    trovati.Add(new ElencoDiVertici(
                        $"{proprieta.Name}[{i}].{interna.Name}", $"Tratto {i + 1}", dentroIl, tipo));
                }
            }
        }

        return trovati;
    }

    /// <summary>L'elenco con quella chiave, o null.</summary>
    public static ElencoDiVertici? Uno(FileAperto file, int indice, string chiave)
        => Di(file, indice).FirstOrDefault(e => e.Chiave == chiave);

    private static bool EUnPunto(Type tipo)
        => tipo == typeof(Coordinate) || tipo == typeof(Punto) || tipo == typeof(PuntoDelTracciato);

    private static Type? TipoDentro(Type tipoDellaProprieta)
        => tipoDellaProprieta.IsGenericType ? tipoDellaProprieta.GetGenericArguments().FirstOrDefault() : null;

    private static string NomeDelCampo(string campo) => campo switch
    {
        "Track" => "Tracciato",
        "Vertices" => "Vertici",
        "Punti" => "Punti",
        "Points" => "Punti",
        "Coordinates" => "Punti",
        "LabelAnchors" => "Ancore delle etichette",
        _ => campo,
    };
}

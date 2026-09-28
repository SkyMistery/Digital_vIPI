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
/// <c>.str</c>, che sono elenchi dentro un elenco. Dalla F3-bis (slice 4) anche i punti di una procedura <c>.str</c>
/// (<see cref="ProcedureWaypoint"/>), che sono solo per nome e portano il suffisso e il «nuovo tratto».</para>
/// <para>Chi modifica un vertice non deve sapere quale dei tre: qui si legge e si scrive un punto, e quel che
/// l'involucro porta in più <b>resta</b> — l'etichetta di una SID non si perde spostando il suo punto.</para>
/// <para>Dal lotto «Subito» (slice 5a, B7 «sequenze di punti su tutti i file») anche tre elenchi che prima si
/// leggevano e basta: il <b>tracciato di un'aerovia</b> (le righe <c>T</c>, solo nomi: <see cref="Airway.FixLabels"/>),
/// i <b>vertici di una zona MVA</b> (<see cref="MvaVertex"/>, col gruppo del 5° campo) e quelli di un <b>confine</b>
/// <c>.artcc</c>/<c>.hartcc</c>/<c>.lartcc</c> (<see cref="StaticBoundaryVertex"/>, un poligono per elenco).</para>
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
    public bool AmmetteNomi => _dentro != typeof(Coordinate);

    /// <summary>
    /// Vero dove un vertice si può scrivere per coordinate. Non nei punti di una procedura <c>.str</c>: il record è
    /// fatto di nomi, e una coordinata ne cambierebbe il tipo (F3-bis slice 4). Né nel tracciato di un'aerovia: le
    /// righe <c>T</c> sono <c>T;L613;RIVAM;RIVAM;</c>, un nome ripetuto (le coordinate stanno nelle righe <c>L</c>).
    /// </summary>
    public bool AmmetteCoordinate => _dentro != typeof(ProcedureWaypoint) && _dentro != typeof(string);

    /// <summary>
    /// Perché quel punto qui non si può scrivere, o null. Oltre a nomi e coordinate (<see cref="AmmetteNomi"/>,
    /// <see cref="AmmetteCoordinate"/>): il tracciato di un'aerovia ripete lo STESSO nome nei due campi, quindi due
    /// nomi diversi non ci stanno.
    /// </summary>
    public string? NonVa(Punto punto)
        => _dentro == typeof(string) && punto.PerNome && punto.Nome != punto.NomeLongitudine
            ? "Nel tracciato di un'aerovia un punto è un nome solo (T;L613;RIVAM;RIVAM;): due nomi diversi non si scrivono."
            : null;

    /// <summary>Il vertice in posizione data, come si scrive a schermo.</summary>
    public string Scrivi(int posizione)
        => ScriviLaVoce(posizione >= 0 && posizione < Elenco.Count ? Elenco[posizione] : null);

    /// <summary>Il punto del vertice in posizione data, senza quel che l'involucro porta in più.</summary>
    public Punto PuntoDi(int posizione) => Elenco[posizione] switch
    {
        Coordinate c => Punto.Da(c),
        Punto p => p,
        PuntoDelTracciato t => t.Punto,
        ProcedureWaypoint w => Punto.Nominato(w.FixName, w.DisplayLabel),
        string nome => Punto.Nominato(nome),
        MvaVertex m => m.Position,
        StaticBoundaryVertex { Position: { } c } => Punto.Da(c),
        StaticBoundaryVertex s => Punto.Nominato(s.FixA ?? "", s.FixB),
        var altro => throw new InvalidOperationException($"«{altro?.GetType().Name}» non è un vertice."),
    };

    /// <summary>
    /// Tutto quel che una voce scrive nel file — il punto e quel che l'involucro porta (etichetta, suffisso, segno di
    /// nuovo tratto, gruppo): due voci con la stessa firma danno le stesse righe. Serve a dire «tornato com'era»
    /// quando un gesto ha rifatto gli involucri (inverti due volte, slice 5a).
    /// </summary>
    internal static string Firma(object? voce) => voce switch
    {
        PuntoDelTracciato t => $"{ScriviLaVoce(t)}|{t.Etichetta}|{t.NuovoTratto}",
        ProcedureWaypoint w => $"{ScriviLaVoce(w)}|{w.SuffixCode}|{w.IniziaUnTratto}",
        MvaVertex m => $"{ScriviLaVoce(m)}|{m.ExtraField}",
        StaticBoundaryVertex s => $"{ScriviLaVoce(s)}|{s.Position is null}",
        _ => ScriviLaVoce(voce),
    };

    /// <summary>Una voce di un elenco di vertici, come si scrive a schermo.</summary>
    internal static string ScriviLaVoce(object? voce)
    {
        return voce switch
        {
            Coordinate c => CoordinateConverter.ToDottedDms(c),
            Punto p => ScriviIlPunto(p),
            PuntoDelTracciato t => ScriviIlPunto(t.Punto),
            ProcedureWaypoint w => w.FixName == w.DisplayLabel ? w.FixName : $"{w.FixName} {w.DisplayLabel}",
            string nome => nome,
            MvaVertex m => ScriviIlPunto(m.Position),
            StaticBoundaryVertex s => s.Position is { } c
                ? CoordinateConverter.ToDottedDms(c)
                : s.FixA == s.FixB ? s.FixA ?? "" : $"{s.FixA} {s.FixB}",
            _ => "",
        };
    }

    /// <summary>
    /// Le posizioni dei vertici che ce l'hanno scritta (quelli per nome no: la loro dipende dal catalogo). Servono a
    /// stimare quanto sono fitti gli archi (<see cref="DensitaDegliArchi"/>).
    /// </summary>
    public IReadOnlyList<Coordinate> Posizioni()
        => [.. Elenco.Cast<object>().Select(v => v switch
        {
            Coordinate c => (Coordinate?)c,
            Punto { PerNome: false } p => p.Posizione,
            PuntoDelTracciato { Punto.PerNome: false } t => t.Punto.Posizione,
            MvaVertex { Position.PerNome: false } m => m.Position.Posizione,
            StaticBoundaryVertex s => s.Position,
            _ => null,
        }).OfType<Coordinate>()];

    /// <summary>Un punto come si scrive a schermo: il nome (i due, se diversi) o le coordinate in DMS puntato.</summary>
    internal static string ScriviIlPunto(Punto p)
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

        if (_dentro == typeof(string))
            return punto.Nome ?? throw new InvalidOperationException("Qui un punto va per nome.");

        if (_dentro == typeof(MvaVertex))
        {
            // Il 5° campo (il gruppo della MVA Selection, «file per file» E3) resta quello del vertice, e un vertice
            // nuovo prende quello dei suoi vicini: una T senza gruppo in un file che lo scrive sarebbe un'altra voce.
            string? gruppo = vecchio is MvaVertex prima
                ? prima.ExtraField
                : Elenco.OfType<MvaVertex>().Select(v => v.ExtraField).FirstOrDefault(g => g is not null);
            return new MvaVertex { Position = punto, ExtraField = gruppo };
        }

        if (_dentro == typeof(StaticBoundaryVertex))
        {
            return punto.PerNome
                ? new StaticBoundaryVertex { FixA = punto.Nome, FixB = punto.NomeLongitudine ?? punto.Nome }
                : new StaticBoundaryVertex { Position = punto.Posizione };
        }

        if (_dentro == typeof(ProcedureWaypoint))
        {
            // Il suffisso che fa da etichetta (`4E`) e il «nuovo tratto» restano: si sposta il punto, non la sua forma.
            var prima = vecchio as ProcedureWaypoint;
            return new ProcedureWaypoint
            {
                FixName = punto.Nome ?? throw new InvalidOperationException("Qui un punto va per nome."),
                DisplayLabel = punto.NomeLongitudine ?? punto.Nome,
                SuffixCode = prima?.SuffixCode,
                IniziaUnTratto = prima?.IniziaUnTratto ?? false,
            };
        }

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

            // Un elenco di testi è un tracciato solo nelle aerovie (le righe T): altrove sono altro (le fonti…).
            if (dentro == typeof(string) && !(record is Airway && proprieta.Name == nameof(Airway.FixLabels)))
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
                        $"{proprieta.Name}[{i}].{interna.Name}",
                        (proprieta.Name == "Polygons" ? "Poligono " : "Tratto ") + (i + 1), dentroIl, tipo));
                }
            }
        }

        return trovati;
    }

    /// <summary>L'elenco con quella chiave, o null.</summary>
    public static ElencoDiVertici? Uno(FileAperto file, int indice, string chiave)
        => Di(file, indice).FirstOrDefault(e => e.Chiave == chiave);

    private static bool EUnPunto(Type tipo)
        => tipo == typeof(Coordinate) || tipo == typeof(Punto) || tipo == typeof(PuntoDelTracciato) || tipo == typeof(ProcedureWaypoint)
           || tipo == typeof(string) || tipo == typeof(MvaVertex) || tipo == typeof(StaticBoundaryVertex);

    private static Type? TipoDentro(Type tipoDellaProprieta)
        => tipoDellaProprieta.IsGenericType ? tipoDellaProprieta.GetGenericArguments().FirstOrDefault() : null;

    private static string NomeDelCampo(string campo) => campo switch
    {
        "Track" => "Tracciato",
        "Vertices" => "Vertici",
        "Punti" => "Punti",
        "Points" => "Punti",
        "Waypoints" => "Punti",
        "Coordinates" => "Punti",
        "FixLabels" => "Tracciato",
        "LabelAnchors" => "Ancore delle etichette",
        _ => campo,
    };
}

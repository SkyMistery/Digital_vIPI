using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Core.Sessione;

/// <summary>Un nome del catalogo: dove sta, in che file, a che riga del modello (l'indice del record).</summary>
/// <param name="Nome">Il nome come lo si cita in una riga (<c>AMSOR;AMSOR;</c>).</param>
/// <param name="Catalogo">«fix», «vor», «ndb», «scalo» o «vrp».</param>
/// <param name="Posizione">Le coordinate.</param>
/// <param name="File">Il file che lo dichiara, relativo alla radice del clone.</param>
public readonly record struct PuntoDelCatalogo(string Nome, string Catalogo, Coordinate Posizione, string File);

/// <summary>
/// I punti che si possono citare PER NOME, per ogni <c>.isc</c> (carta F3, slice 3). Aurora risolve un nome solo nei
/// cataloghi che quel master carica: <c>ITALY.isc</c> e <c>LIBB.isc</c> possono rispondere in modo diverso, e la mappa
/// dell'app disegna quel che risolve il master scelto.
/// <para>Chi carica che cosa lo dice il motore (<see cref="CarichiDegliIsc"/>, lo stesso del validatore); i nomi
/// dichiarati pure (<see cref="Cataloghi.Dichiarato"/>). Qui si mettono insieme, usando i record GIÀ LETTI dalla
/// sessione: l'albero non si rilegge.</para>
/// </summary>
public sealed class CatalogoDeiPunti : IFixResolver
{
    private readonly Dictionary<string, PuntoDelCatalogo> _perNome;

    private CatalogoDeiPunti(string isc, Dictionary<string, PuntoDelCatalogo> perNome, IReadOnlyList<string> fileCaricati)
    {
        Isc = isc;
        _perNome = perNome;
        FileCaricati = fileCaricati;
    }

    /// <summary>Il nome del master: <c>ITALY.isc</c>.</summary>
    public string Isc { get; }

    /// <summary>I file che quel master carica, relativi alla radice del clone, in ordine.</summary>
    public IReadOnlyList<string> FileCaricati { get; }

    public int Punti => _perNome.Count;

    /// <summary>
    /// Il punto con quel nome, o nullo. ⚠️ Aurora non guarda le maiuscole; se lo stesso nome è in due file (il
    /// validatore lo dice, carta F2 §3), qui vince il primo in ordine di file, come nell'elenco dei carichi.
    /// </summary>
    public PuntoDelCatalogo? Cerca(string nome)
        => _perNome.TryGetValue(nome.Trim(), out var punto) ? punto : null;

    public bool Risolve(string nome) => Cerca(nome) is not null;

    /// <summary>
    /// Come lo chiede il motore (<see cref="Sectorfile.Shared.Punto.TryRisolvi"/>), che con due nomi diversi fa come
    /// Aurora: la latitudine dal primo, la longitudine dal secondo.
    /// </summary>
    public bool TryResolve(string ident, out Coordinate position)
    {
        if (Cerca(ident) is { } punto)
        {
            position = punto.Posizione;
            return true;
        }

        position = default;
        return false;
    }

    /// <summary>Un catalogo per ogni <c>.isc</c> della cartella, per nome del master.</summary>
    public static IReadOnlyDictionary<string, CatalogoDeiPunti> PerOgniIsc(SessioneAperta sessione)
    {
        ArgumentNullException.ThrowIfNull(sessione);

        // Gli ICAO degli scali li legge dalla sessione: i file sono già in memoria, e il motore non li rilegge.
        IEnumerable<string> ScaliDi(string percorso)
            => sessione.File.GetValueOrDefault(sessione.Cartella.Relativo(percorso)) is FileLetto<AirportInfo> scali
                ? scali.Letto.Records.Select(a => a.IcaoCode)
                : [];

        var cataloghi = new Dictionary<string, CatalogoDeiPunti>(StringComparer.OrdinalIgnoreCase);
        foreach (var carico in CarichiDegliIsc.Leggi(sessione.Cartella.SectorFiles, ScaliDi))
        {
            var perNome = new Dictionary<string, PuntoDelCatalogo>(StringComparer.OrdinalIgnoreCase);
            var caricati = carico.Caricati.Select(sessione.Cartella.Relativo).Order(StringComparer.Ordinal).ToList();

            foreach (string relativo in caricati)
            {
                if (sessione.File.GetValueOrDefault(relativo) is not FileAperto aperto)
                    continue;

                foreach (object record in Record(aperto))
                {
                    if (Cataloghi.Dichiarato(record) is not { } dichiarato)
                        continue;

                    foreach (string nome in dichiarato.Nomi)
                    {
                        string pulito = nome.Trim();
                        if (pulito.Length > 0)
                            perNome.TryAdd(pulito, new PuntoDelCatalogo(pulito, dichiarato.Catalogo, dichiarato.Posizione, relativo));
                    }
                }
            }

            cataloghi[carico.Nome] = new CatalogoDeiPunti(carico.Nome, perNome, caricati);
        }

        return cataloghi;
    }

    /// <summary>I record di un file aperto, qualunque sia il loro tipo.</summary>
    private static IEnumerable<object> Record(FileAperto aperto)
        => aperto is IFileConRecord conRecord ? conRecord.RecordDelModello : [];
}

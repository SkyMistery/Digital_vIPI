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
    /// I nomi da proporre mentre l'AOD scrive un punto (lotto «Subito», slice 3c): prima quelli che cominciano col
    /// testo, poi quelli che lo contengono, in ordine alfabetico, al più <paramref name="quanti"/>. Filtrati qui e non
    /// nella pagina: i nomi di un master sono migliaia, e un elenco intero a ogni campo pesa sulla scheda.
    /// </summary>
    public IReadOnlyList<PuntoDelCatalogo> Suggerisci(string? testo, int quanti = 20)
    {
        string cercato = (testo ?? "").Trim();
        if (cercato.Length == 0 || quanti <= 0)
            return [];
        var ordinati = _perNome.Values.OrderBy(p => p.Nome, StringComparer.OrdinalIgnoreCase);
        return [.. ordinati.Where(p => p.Nome.StartsWith(cercato, StringComparison.OrdinalIgnoreCase))
            .Concat(ordinati.Where(p => !p.Nome.StartsWith(cercato, StringComparison.OrdinalIgnoreCase)
                                        && p.Nome.Contains(cercato, StringComparison.OrdinalIgnoreCase)))
            .Take(quanti)];
    }

    /// <summary>
    /// I nomi da proporre per un punto di una rotta VFR di <paramref name="vfiDelloScalo"/> (slice 16e, «file per file»
    /// S4): prima i punti VFR di quel <c>.vfi</c>, poi quelli degli altri <c>.vfi</c> dal più vicino allo scalo (sul
    /// fork 13 punti di rotta su 123 vengono dal <c>.vfi</c> di uno scalo accanto), poi fix, VOR, NDB e scali. Senza
    /// testo propone i soli punti dello scalo: sono pochi, e sono quelli che servono.
    /// </summary>
    public IReadOnlyList<PuntoDelCatalogo> SuggerisciPerUnaRottaVfr(string? testo, string vfiDelloScalo, int quanti = 20)
    {
        string cercato = (testo ?? "").Trim();
        if (quanti <= 0)
            return [];
        var delloScalo = _perNome.Values.Where(p => p.Catalogo == "vrp" && string.Equals(p.File, vfiDelloScalo, StringComparison.OrdinalIgnoreCase)).ToList();
        if (cercato.Length == 0)
            // Le rotte citano i punti per nome (sul fork tutte): i codici si trovano scrivendoli.
            return [.. delloScalo.Where(p => !Sectorfile.Validazione.ControlloDeiVfr.EUnCodice(p.Nome)).OrderBy(p => p.Nome, StringComparer.OrdinalIgnoreCase).Take(quanti)];

        // Il «centro» dello scalo: la media dei suoi punti VFR. Senza punti suoi, gli altri restano in ordine di nome.
        Coordinate? centro = delloScalo.Count == 0 ? null
            : new Coordinate(delloScalo.Average(p => p.Posizione.LatitudeDeg), delloScalo.Average(p => p.Posizione.LongitudeDeg));
        int Rango(PuntoDelCatalogo p) => p.Catalogo != "vrp" ? 2 : string.Equals(p.File, vfiDelloScalo, StringComparison.OrdinalIgnoreCase) ? 0 : 1;
        double Lontano(PuntoDelCatalogo p) => Rango(p) == 1 && centro is { } c ? Distanze.Nm(c, p.Posizione) : 0;
        return [.. _perNome.Values.Where(p => p.Nome.Contains(cercato, StringComparison.OrdinalIgnoreCase))
            .OrderBy(Rango)
            .ThenBy(Lontano)
            .ThenBy(p => !p.Nome.StartsWith(cercato, StringComparison.OrdinalIgnoreCase))
            .ThenBy(p => p.Nome, StringComparer.OrdinalIgnoreCase)
            .Take(quanti)];
    }

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

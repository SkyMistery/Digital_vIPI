using Vipi.Sectorfile.Models;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Che cosa si fa con un formato, qualunque sia il tipo dei suoi record: <see cref="Formati.Usa{R}"/> chiama
/// <see cref="Usa{T}"/> col lettore e lo scrittore giusti.
/// </summary>
public interface IUsoDelFormato<out R>
{
    R Usa<T>(IFileParser<T> lettore, IFileSaver<T> scrittore)
        where T : class;
}

/// <summary>
/// Il lettore e lo scrittore di ogni file dell'albero, scelti in UN posto (carta F2 §6, dispatch): dall'estensione e,
/// dove serve, dal nome (<c>*fic.tfl</c>) o dalla cartella (<c>ENRMVA/</c>). Lo usano lo strumento di prova e il
/// validatore (slice 8); prima stava nello strumento.
/// </summary>
public static class Formati
{
    /// <summary>
    /// Chiama <paramref name="uso"/> col lettore e lo scrittore di <paramref name="percorso"/>. Falso per i file che il
    /// motore non interpreta (<c>.txt</c>, <c>.cpr</c>, <c>.clr</c>… passano intatti come testo, carta F2 §4).
    /// </summary>
    public static bool Usa<R>(string percorso, IWarningCollector avvisi, IUsoDelFormato<R> uso, out R esito)
    {
        ArgumentException.ThrowIfNullOrEmpty(percorso);
        ArgumentNullException.ThrowIfNull(avvisi);
        ArgumentNullException.ThrowIfNull(uso);

        string estensione = Path.GetExtension(percorso).TrimStart('.').ToLowerInvariant();
        bool enRoute = percorso.Replace('\\', '/').Contains("/ENRMVA/", StringComparison.OrdinalIgnoreCase);
        bool fic = Path.GetFileName(percorso).EndsWith("fic.tfl", StringComparison.OrdinalIgnoreCase);

        (bool, R) Con<T>(IFileParser<T> lettore, IFileSaver<T> scrittore)
            where T : class
            => (true, uso.Usa(lettore, scrittore));

        (bool Letto, R Esito) risultato = estensione switch
        {
            "ap" => Con(new ApParser(avvisi), new ApSaver()),
            // Le aree P/R/D sono segmenti .geo col nome dell'area in più (F2 slice 6).
            "geo" or "restrict" or "prohibit" or "danger" => Con(new GeoParser(avvisi), new GeoSaver()),
            "hold" => Con(new HoldParser(avvisi), new HoldSaver()),
            "vrt" => Con(new VrtParser(avvisi), new VrtSaver()),
            "pol" => Con(new PolParser(avvisi), new PolSaver()),
            "txi" => Con(new TxiParser(avvisi), new TxiSaver()),
            "gts" => Con(new GtsParser(avvisi), new GtsSaver()),
            "sid" => Con(new SidParser(avvisi), new SidSaver()),
            "vfi" => Con(new VfiParser(avvisi), new VfiSaver()),
            "atis" => Con(new AtisParser(avvisi), new AtisSaver()),
            "vor" => Con(new VorParser(avvisi), new VorSaver()),
            "ndb" => Con(new NdbParser(avvisi), new NdbSaver()),
            "fix" => Con(new FixParser(avvisi), new FixSaver()),
            "lairway" or "hairway" => Con(new AirwayParser(avvisi), new AirwaySaver()),
            "frq" => Con(new FrqParser(avvisi), new FrqSaver()),
            "rw" => Con(new RwParser(avvisi), new RwSaver()),
            "str" => Con(new StrParser(avvisi), new StrSaver()),
            "hartcc" => Con(new HartccParser(avvisi), new HartccSaver()),
            "lartcc" => Con(new LartccParser(avvisi), new LartccSaver()),
            "artcc" => Con(new ArtccParser(avvisi), new ArtccSaver()),
            "tfl" when fic => Con(new FicParser(avvisi), new FicSaver()),
            "tfl" => Con(new TflParser(avvisi), new TflSaver()),
            "mva" when enRoute => Con(new MvaEnrouteParser(avvisi), new MvaSaver(enroute: true)),
            "mva" => Con(new MvaAirportParser(avvisi), new MvaSaver(enroute: false)),
            _ => (false, default!),
        };

        esito = risultato.Esito;
        return risultato.Letto;
    }
}

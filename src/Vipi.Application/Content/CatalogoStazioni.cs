namespace Vipi.Application.Content;

/// <summary>
/// Il catalogo delle stazioni — le ACC visibili e la mappa ICAO → aeroporto — tenuto <b>una volta per
/// processo</b> invece che una volta per circuito.
///
/// <para><b>Perché esiste.</b> Sono dati di <b>divisione</b>: uguali per tutti, sette ACC e novantatré
/// aeroporti, e cambiano quando un amministratore tocca la struttura o quando passa il giro notturno.
/// Fino al 31 agosto 2026 li rileggeva dal database <b>ogni circuito</b> — cioè ogni sessione aperta, e
/// ogni richiesta SSR — perché <see cref="IStationResolver"/> è <c>scoped</c>. Quella lettura è la stessa
/// che è finita nello stack di tre guasti in una settimana: sotto latenza cadeva <b>durante il render</b>,
/// sullo stesso <c>DbContext</c> che la pagina stava già usando, e la risposta di EF era «A second
/// operation was started on this context instance».</para>
///
/// <para>⚠️ <b>Il punto non è la velocità, è che la lettura sparisce.</b> Proteggere i chiamanti uno per
/// uno — scaldare le cache prima del render, avvolgere in <c>try/catch</c>, dare uno scope proprio a un
/// componente — funziona, ma va rifatto a ogni pagina nuova e si dimentica. Una lettura che avviene
/// <b>una volta per processo</b> non ha nessuno contro cui correre.</para>
///
/// <para><b>Chi legge davvero.</b> Questa classe non conosce il database: il <b>come si legge</b> lo porta
/// chi chiama (<see cref="StationResolver"/>, che ha l'<see cref="Abstractions.IStationDirectory"/> del suo
/// scope). ⚠️ È di proposito: un singleton che si tenesse un <c>DbContext</c> sarebbe una dipendenza
/// prigioniera, cioè un contesto vivo quanto il processo — esattamente il difetto che questa classe deve
/// togliere di mezzo.</para>
///
/// <para>⚠️ <b>La freschezza è tutta in <see cref="IStationCatalogVersion"/></b>, e da qui in avanti quel
/// contatore è una cosa seria: prima una spinta mancata costava un dato vecchio per il tempo di un
/// circuito, adesso costa un dato vecchio <b>finché qualcuno non riavvia il processo</b>. Per questo la
/// spinta non è più affidata ai servizi che ricordano di chiamarla, ma a
/// <c>BumpCatalogoStazioniInterceptor</c>, che la dà dove avviene la scrittura.</para>
/// </summary>
public interface ICatalogoStazioni
{
    /// <summary>Le ACC visibili. <paramref name="leggi"/> viene chiamato <b>solo</b> se la copia manca o è vecchia.</summary>
    IReadOnlyList<AccInfo> Accs(Func<IReadOnlyList<AccInfo>> leggi);

    /// <summary>ICAO → aeroporto. <paramref name="leggi"/> viene chiamato <b>solo</b> se la copia manca o è vecchia.</summary>
    IReadOnlyDictionary<string, AirportStation> Aeroporti(Func<IReadOnlyList<AirportStation>> leggi);
}

/// <inheritdoc cref="ICatalogoStazioni"/>
public sealed class CatalogoStazioni : ICatalogoStazioni
{
    /// <summary>
    /// La copia e la versione con cui e' stata riempita, in <b>un oggetto solo</b>.
    ///
    /// <para>⚠️ Non sono due campi, e la ragione e' che due campi non si possono leggere insieme: chi
    /// legge potrebbe vedere il dato vecchio e la versione nuova, e allora terrebbe per buona una copia
    /// scaduta — per sempre, perche' da li' in poi il confronto tornerebbe uguale. Scambiando un
    /// riferimento solo la coppia e' sempre coerente.</para>
    /// </summary>
    private sealed record Copia<T>(int Versione, T Dato);

    private readonly IStationCatalogVersion _versione;

    /// <summary>
    /// Una serratura sola per tutte e due le copie.
    ///
    /// <para>⚠️ <b>Tenerla mentre si legge dal database e' voluto.</b> E' il caso della partenza a
    /// freddo: venti circuiti che arrivano insieme su un processo appena nato facevano venti letture
    /// uguali, e con <c>MaximumPoolSize=20</c> era il modo di prendersi il pool intero per un elenco di
    /// sette righe. Adesso ne parte <b>una</b> e le altre aspettano quella.</para>
    /// </summary>
    private readonly object _serratura = new();

    // Letti e scritti con Volatile: il campo non e' `volatile` perche' un campo volatile non si puo'
    // passare per riferimento, e i due metodi qui sotto ne hanno bisogno.
    private Copia<IReadOnlyList<AccInfo>>? _accs;
    private Copia<IReadOnlyDictionary<string, AirportStation>>? _aeroporti;

    public CatalogoStazioni(IStationCatalogVersion versione) => _versione = versione;

    public IReadOnlyList<AccInfo> Accs(Func<IReadOnlyList<AccInfo>> leggi)
    {
        // Il caso normale e' questo: un confronto fra due interi, niente serratura e niente query.
        if (Volatile.Read(ref _accs) is { } pronta && pronta.Versione == _versione.Current) return pronta.Dato;

        lock (_serratura)
        {
            // Ricontrollo dentro: mentre si aspettava, un altro puo' avere gia' riempito la copia.
            if (Volatile.Read(ref _accs) is { } appena && appena.Versione == _versione.Current) return appena.Dato;

            // ⚠️ La versione si legge PRIMA della lettura, non dopo. Se qualcuno scrive mentre la query
            // e' in volo, la copia risulta vecchia e si rilegge; al contrario si terrebbe per buona una
            // fotografia scattata prima di quella scrittura, e nessuno rileggerebbe piu'.
            var primaDi = _versione.Current;
            var dato = leggi();
            Volatile.Write(ref _accs, new Copia<IReadOnlyList<AccInfo>>(primaDi, dato));
            return dato;
        }
    }

    public IReadOnlyDictionary<string, AirportStation> Aeroporti(Func<IReadOnlyList<AirportStation>> leggi)
    {
        if (Volatile.Read(ref _aeroporti) is { } pronta && pronta.Versione == _versione.Current) return pronta.Dato;

        lock (_serratura)
        {
            if (Volatile.Read(ref _aeroporti) is { } appena && appena.Versione == _versione.Current) return appena.Dato;

            var primaDi = _versione.Current;
            // ⚠️ Un ICAO ripetuto e' un difetto della sorgente, non un motivo per far cadere ogni pagina
            // del sito: vince il primo, come faceva la cache di prima.
            IReadOnlyDictionary<string, AirportStation> mappa = leggi()
                .GroupBy(a => a.Icao, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            Volatile.Write(ref _aeroporti, new Copia<IReadOnlyDictionary<string, AirportStation>>(primaDi, mappa));
            return mappa;
        }
    }
}

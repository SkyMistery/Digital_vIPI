using Vipi.Application.Abstractions;

namespace Vipi.Application.Content;

/// <summary>
/// Risolve <b>tutto</b> quel che un testo cita: le procedure (§A73, §A80) e i dati — frequenze e nominativi
/// (carta <c>2026-09-20-riferimenti-ai-dati.md</c>). È la dipendenza sola dei caricatori di documento: due
/// servizi da chiamare in fila sono due occasioni di scordarne uno, e il riferimento dimenticato esce grezzo.
/// </summary>
public interface IRiferimentiResolver
{
    /// <param name="sezioni">Le sezioni che la pagina sta per disegnare: si risolve solo quel che citano.</param>
    /// <param name="pubblica">Vero sulla pagina pubblica e nell'anteprima di una release; falso in bozza.</param>
    /// <param name="proprioIcao">Lo scalo del documento, se ne ha uno.</param>
    /// <param name="propriaTabella">La tabella SID che la pagina mostra per <paramref name="proprioIcao"/>.</param>
    /// <param name="propriaTabellaStar">La tabella STAR che la pagina mostra per <paramref name="proprioIcao"/>.</param>
    Task<RiferimentiRisolti> PerVistaAsync(IEnumerable<SectionView> sezioni, bool pubblica,
        string? proprioIcao = null, AirportSidView? propriaTabella = null,
        AirportSidView? propriaTabellaStar = null, CancellationToken ct = default);

    /// <summary>Come <see cref="PerVistaAsync"/>, su testi qualunque: le anteprime dell'editor, che hanno i
    /// blocchi di lavoro e non una vista. Sempre alla BOZZA — l'editor guarda l'editor.</summary>
    Task<RiferimentiRisolti> PerTestiAsync(IEnumerable<string?> testi, CancellationToken ct = default);
}

/// <summary>
/// Gli enti con la loro frequenza e il loro nominativo: quel poco che serve a risolvere <c>[[FREQ …]]</c> e
/// <c>[[ATC …]]</c>.
/// <para>⚠️ Una porta <b>ristretta</b>, non un secondo modello: i dati sono gli stessi di
/// <see cref="IAirportRepository.ListLinkableFrequenciesAsync"/> e l'implementazione li chiede proprio a lui.
/// Esiste perché chi risolve i riferimenti non ha niente a che fare con le altre trenta scritture
/// dell'anagrafica, e perché una prova di questo servizio non deve implementare trenta metodi per arrivare a
/// uno.</para>
/// </summary>
public interface IFrequenzeDegliEnti
{
    Task<IReadOnlyList<LinkableFrequencyRow>> TutteAsync(CancellationToken ct = default);

    /// <summary>
    /// Gli enti col loro nominativo radio, <b>frequenza o no</b>: è la domanda di <c>[[ATC …]]</c>, e non è
    /// la stessa di <see cref="TutteAsync"/>. Un ente senza frequenza dichiarata ha comunque un nome alla
    /// radio; chiederlo all'elenco delle frequenze lo rendeva incitabile, e trasformava la cancellazione di
    /// una frequenza nella sparizione di un nominativo già scritto in un documento.
    /// </summary>
    Task<IReadOnlyList<EnteRow>> NominativiAsync(CancellationToken ct = default);

    /// <summary>Le soglie degli scali chiesti, per ICAO: quel poco che serve a dire se una pista citata esiste.</summary>
    Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> PisteAsync(
        IReadOnlyCollection<string> icaos, CancellationToken ct = default);

    /// <summary>I nomi del catalogo dei punti, o vuoto se la sorgente non è raggiungibile.</summary>
    Task<IReadOnlySet<string>> PuntiAsync(CancellationToken ct = default);

    /// <summary>
    /// Le aree regolamentate della divisione, per <c>[[AREA …]]</c> (21 settembre 2026). Il corpo di ripiego
    /// («nessuna») serve ai finti dei test che non citano aree: una famiglia che risponde a vuoto non si dichiara
    /// guardata, quindi non segnala niente.
    /// </summary>
    Task<IReadOnlyList<SpecialAreaPick>> AreeAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<SpecialAreaPick>>(Array.Empty<SpecialAreaPick>());
}

/// <inheritdoc cref="IFrequenzeDegliEnti"/>
public sealed class FrequenzeDegliEnti : IFrequenzeDegliEnti
{
    private readonly IAirportRepository _repo;
    private readonly INavaidSource? _punti;
    private readonly ISpecialAreaRepository? _aree;

    public FrequenzeDegliEnti(IAirportRepository repo, INavaidSource? punti = null, ISpecialAreaRepository? aree = null)
    {
        _repo = repo;
        _punti = punti;
        _aree = aree;
    }

    public async Task<IReadOnlyList<SpecialAreaPick>> AreeAsync(CancellationToken ct = default) =>
        _aree is null ? Array.Empty<SpecialAreaPick>() : await _aree.ListAllSpecialAreasAsync(ct);

    public Task<IReadOnlyList<LinkableFrequencyRow>> TutteAsync(CancellationToken ct = default) =>
        _repo.ListLinkableFrequenciesAsync(ct);

    public Task<IReadOnlyList<EnteRow>> NominativiAsync(CancellationToken ct = default) =>
        _repo.ListSectorCallsignsAsync(ct);

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> PisteAsync(
        IReadOnlyCollection<string> icaos, CancellationToken ct = default)
    {
        var dati = await _repo.ListRunwayDataAsync(icaos, ct);
        return dati.ToDictionary(
            d => d.Key,
            d => (IReadOnlyList<string>)d.Value.Runways.Select(p => p.Ident).ToList(),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>⚠️ Catalogo assente = insieme VUOTO, e chi lo riceve non segnala niente: «non lo so» non è
    /// «non c'è» (vedi <see cref="ValoriDato"/>).</summary>
    public async Task<IReadOnlySet<string>> PuntiAsync(CancellationToken ct = default)
    {
        if (_punti is null) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var catalogo = await _punti.GetAsync(ct);
        return catalogo.Names;
    }
}

/// <inheritdoc cref="IRiferimentiResolver"/>
public sealed class RiferimentiResolver : IRiferimentiResolver
{
    private readonly IProcedureReferenceResolver _procedure;
    private readonly IFrequenzeDegliEnti _enti;

    public RiferimentiResolver(IProcedureReferenceResolver procedure, IFrequenzeDegliEnti enti)
    {
        _procedure = procedure;
        _enti = enti;
    }

    public async Task<RiferimentiRisolti> PerVistaAsync(IEnumerable<SectionView> sezioni, bool pubblica,
        string? proprioIcao = null, AirportSidView? propriaTabella = null,
        AirportSidView? propriaTabellaStar = null, CancellationToken ct = default)
    {
        // ⚠️ I testi si materializzano UNA volta: `TestiDi` cammina l'albero delle sezioni, e chiederglielo due
        // volte — una per le procedure e una per i dati — vorrebbe dire percorrerlo due volte.
        var testi = RiferimentiProcedura.TestiDi(sezioni).ToList();
        var procedure = await _procedure.PerVistaAsync(sezioni, pubblica, proprioIcao, propriaTabella, propriaTabellaStar, ct);
        return new RiferimentiRisolti(procedure, await DatiAsync(testi, ct));
    }

    public async Task<RiferimentiRisolti> PerTestiAsync(IEnumerable<string?> testi, CancellationToken ct = default)
    {
        var lista = testi.ToList();
        var procedure = await _procedure.PerTestiAsync(lista, ct);
        return new RiferimentiRisolti(procedure, await DatiAsync(lista, ct));
    }

    /// <summary>
    /// I valori dei dati citati. ⚠️ <b>La via breve prima di tutto</b>: se nessun testo cita un dato non si
    /// chiede niente a nessuno — ed è il caso di quasi ogni pagina.
    /// </summary>
    private async Task<ValoriDato> DatiAsync(IReadOnlyList<string?> testi, CancellationToken ct)
    {
        var citati = RiferimentiDato.Citati(testi);
        if (citati.Count == 0) return ValoriDato.Vuoto;

        var voci = new List<(TipoDato, string, string)>();
        var guardate = new List<TipoDato>();
        var mute = new List<(TipoDato, string)>();

        // ⚠️ Le FREQUENZE e i NOMINATIVI sono due domande diverse, e chiederne una sola è stato un difetto:
        // l'elenco delle frequenze contiene i soli settori che una frequenza ce l'hanno, mentre il nome alla
        // radio ce l'hanno tutti. Si chiede solo quel che il testo cita, quindi quasi sempre una sola delle due.
        if (citati.Any(c => c.Tipo == TipoDato.Frequenza))
        {
            var enti = await _enti.TutteAsync(ct);
            // ⚠️ Un catalogo vuoto non si dichiara guardato: sarebbe «tutte le frequenze sono sparite».
            if (enti.Count > 0) guardate.Add(TipoDato.Frequenza);
            foreach (var f in enti) voci.Add((TipoDato.Frequenza, f.Callsign, f.FrequencyMhz));
        }

        // ⚠️ Nominativo e Postazione sono DUE facce della stessa riga di catalogo, e si chiedono con UNA
        // lettura: citarle tutte e due nello stesso testo — «LIRR_NE (Roma Radar)» — non deve costare due
        // viaggi per la stessa risposta.
        var voglioNominativo = citati.Any(c => c.Tipo == TipoDato.Nominativo);
        var voglioPostazione = citati.Any(c => c.Tipo == TipoDato.Postazione);
        if (voglioNominativo || voglioPostazione)
        {
            var enti = await _enti.NominativiAsync(ct);
            if (enti.Count > 0)
            {
                if (voglioNominativo) guardate.Add(TipoDato.Nominativo);
                if (voglioPostazione) guardate.Add(TipoDato.Postazione);
            }
            foreach (var e in enti)
            {
                // Il nominativo è quello del catalogo IVAO; dove manca vale il callsign, che è sempre vero.
                if (voglioNominativo)
                    voci.Add((TipoDato.Nominativo, e.Callsign,
                        string.IsNullOrWhiteSpace(e.AtcCallsign) ? e.Callsign : e.AtcCallsign!));
                // Il codice esce com'è nel catalogo: la chiave È il valore, e il gettone serve per l'avviso.
                if (voglioPostazione)
                    voci.Add((TipoDato.Postazione, e.Callsign, e.Callsign));
            }
        }

        // Le PISTE: la chiave è «ICAO SOGLIA», e quel che esce è la soglia com'è scritta — un rinomino per
        // deriva magnetica non si indovina. Si guardano solo gli scali citati.
        var scaliCitati = citati.Where(c => c.Tipo == TipoDato.Pista)
            .Select(c => RiferimentiDato.ScopoDi(TipoDato.Pista, c.Chiave))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (scaliCitati.Count > 0)
        {
            var piste = await _enti.PisteAsync(scaliCitati, ct);
            // ⚠️ La famiglia è guardata appena la domanda È STATA FATTA, e il «non lo so» si dichiara per
            // SCALO. Prima era tutto per famiglia, e le due domande sbagliavano insieme: un ICAO inventato
            // — che non torna con nessuna soglia — non veniva segnalato affatto, mentre uno scalo vero senza
            // piste in anagrafica faceva scattare l'avviso appena un ALTRO scalo del testo le aveva.
            guardate.Add(TipoDato.Pista);
            foreach (var (icao, idents) in piste)
            {
                if (idents.Count == 0) mute.Add((TipoDato.Pista, icao));   // scalo senza soglie importate
                foreach (var ident in idents) voci.Add((TipoDato.Pista, $"{icao} {ident}", ident));
            }
        }

        // I PUNTI: stessa regola, dal catalogo del sectorfile — che è tenuto in cache di processo.
        if (citati.Any(c => c.Tipo == TipoDato.Punto))
        {
            var punti = await _enti.PuntiAsync(ct);
            if (punti.Count > 0) guardate.Add(TipoDato.Punto);
            foreach (var (_, chiave) in citati.Where(c => c.Tipo == TipoDato.Punto))
                if (punti.Contains(chiave))
                    voci.Add((TipoDato.Punto, chiave, chiave));
        }

        // Le AREE regolamentate: esce il nome di oggi dall'import IVAO, sotto l'id che non cambia.
        if (citati.Any(c => c.Tipo == TipoDato.Area))
        {
            var aree = await _enti.AreeAsync(ct);
            if (aree.Count > 0) guardate.Add(TipoDato.Area);
            foreach (var a in aree) voci.Add((TipoDato.Area, a.IvaoId, a.Name));
        }

        return new ValoriDato(voci, guardate, mute);
    }
}

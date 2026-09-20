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
}

/// <inheritdoc cref="IFrequenzeDegliEnti"/>
public sealed class FrequenzeDegliEnti : IFrequenzeDegliEnti
{
    private readonly IAirportRepository _repo;
    public FrequenzeDegliEnti(IAirportRepository repo) => _repo = repo;

    public Task<IReadOnlyList<LinkableFrequencyRow>> TutteAsync(CancellationToken ct = default) =>
        _repo.ListLinkableFrequenciesAsync(ct);
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

        // Frequenze e nominativi vengono dalla STESSA domanda — il catalogo dei settori con la loro frequenza
        // — quindi si chiede una volta sola, e solo se il testo cita almeno uno dei due.
        var servonoEnti = citati.Any(c => c.Tipo is TipoDato.Frequenza or TipoDato.Nominativo);
        var voci = new List<(TipoDato, string, string)>();
        if (servonoEnti)
        {
            foreach (var f in await _enti.TutteAsync(ct))
            {
                voci.Add((TipoDato.Frequenza, f.Callsign, f.FrequencyMhz));
                // Il nominativo è quello del catalogo IVAO; dove manca vale il callsign, che è sempre vero.
                voci.Add((TipoDato.Nominativo, f.Callsign, string.IsNullOrWhiteSpace(f.AtcCallsign) ? f.Callsign : f.AtcCallsign!));
            }
        }

        // Piste e punti escono come sono scritti: la chiave È il valore. Qui si dichiarano comunque, così il
        // controllo dell'editor sa distinguere «non l'ho cercato» da «non c'è più» quando arriveranno le loro
        // sorgenti (slice 6c).
        return new ValoriDato(voci);
    }
}

using Vipi.Application.Abstractions;

namespace Vipi.Application.EventKits;

/// <summary>
/// Chi sta controllando con un account dell'evento, e con quale VID (committente, 1 ottobre 2026; carta
/// <c>docs/feature/2026-10-01-account-evento.md</c>). Durante un evento si controlla con account e VID dati
/// dall'organizzazione, e la vista live — che cerca nel feed IVAO il VID con cui si è entrati nel sito — non trova
/// nessuno. Qui si ricorda «il VID personale X sta usando il VID dell'evento Y» fino alla fine dell'evento.
///
/// <para>⚠️ <b>Si legge dalla memoria, si salva anche nel database</b> (<see cref="IAccountEventoArchivio"/>): la vista
/// live chiede a ogni giro del feed, e la risposta deve costare niente; il database serve solo a rimettere in piedi questa
/// copia all'avvio, perché un riavvio durante l'evento non faccia riscrivere il VID a tutti (committente, 1 ottobre 2026).
/// Un singleton e non uno scoped: in Blazor Server lo scoped vive quanto il CIRCUITO, e una seconda scheda o un
/// ricarico perderebbero l'account appena scelto.</para>
///
/// <para>⚠️ Più persone possono usare lo stesso VID dell'evento (scelta del committente): niente esclusiva.</para>
/// </summary>
public sealed class AccountEventoRegistro
{
    /// <summary>Il tetto, anche per un evento senza data di fine: dopo, il VID si riscrive.</summary>
    public static readonly TimeSpan DurataMassima = TimeSpan.FromHours(12);

    private readonly object _lock = new();
    private readonly Dictionary<int, (int VidEvento, DateTime ScadeUtc)> _voci = new();

    public void Usa(int vidPersonale, int vidEvento, DateTime scadeUtc)
    {
        lock (_lock) _voci[vidPersonale] = (vidEvento, scadeUtc);
    }

    /// <summary>Il VID dell'evento che questa persona sta usando adesso, o null. Le voci scadute si tolgono qui.</summary>
    public int? VidPer(int vidPersonale, DateTime adessoUtc)
    {
        lock (_lock)
        {
            if (!_voci.TryGetValue(vidPersonale, out var v)) return null;
            if (adessoUtc < v.ScadeUtc) return v.VidEvento;
            _voci.Remove(vidPersonale);
            return null;
        }
    }

    public void Lascia(int vidPersonale)
    {
        lock (_lock) _voci.Remove(vidPersonale);
    }

    /// <summary>Tutti fuori: lo chiama chi cambia l'evento (spento, date, lista dei VID).</summary>
    public void Svuota()
    {
        lock (_lock) _voci.Clear();
    }

    /// <summary>All'avvio, dal database: le voci ancora valide prendono il posto di quel che c'era.</summary>
    public void Carica(IEnumerable<AccountEventoSalvato> voci, DateTime adessoUtc)
    {
        lock (_lock)
        {
            _voci.Clear();
            foreach (var v in voci.Where(v => v.ScadeUtc > adessoUtc))
                _voci[v.VidPersonale] = (v.VidEvento, v.ScadeUtc);
        }
    }
}

/// <summary>Una voce del registro come sta nel database.</summary>
public sealed record AccountEventoSalvato(int VidPersonale, int VidEvento, DateTime ScadeUtc);

/// <summary>
/// La copia nel database di <see cref="AccountEventoRegistro"/>: si scrive a ogni cambio, si legge solo all'avvio.
/// <para>⚠️ Non lancia: se il database non risponde la vista live funziona lo stesso (dalla memoria), e si perde solo la
/// sopravvivenza a un riavvio.</para>
/// </summary>
public interface IAccountEventoArchivio
{
    Task SalvaAsync(int vidPersonale, int vidEvento, DateTime scadeUtc, CancellationToken ct = default);
    Task TogliAsync(int vidPersonale, CancellationToken ct = default);
    Task SvuotaAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AccountEventoSalvato>> TuttiAsync(CancellationToken ct = default);
}

/// <summary>Come è andata la richiesta di usare un account dell'evento.</summary>
public enum EsitoAccountEvento { Usato, NonEntrato, NessunEvento, NonInLista, NonOnline }

/// <param name="Callsign">La postazione su cui quel VID è connesso, se <see cref="EsitoAccountEvento.Usato"/>.</param>
public sealed record RispostaAccountEvento(EsitoAccountEvento Esito, string? Callsign = null);

/// <summary>La traccia nel registro di audit: chi ha usato quale account dell'evento, e su quale postazione.</summary>
public interface IAccountEventoTraccia
{
    Task RegistraAsync(int vidPersonale, int vidEvento, string callsign, CancellationToken ct = default);
}

/// <summary>Usare un account dell'evento: la pagina «Controlli con un account dell'evento?» dell'hub.</summary>
public interface IAccountEventoService
{
    /// <summary>
    /// Prova a usare <paramref name="vidEvento"/>. Riesce se chi chiede è entrato nel sito, l'evento si vede adesso,
    /// il VID è nella lista dello staff ed è online sul feed IVAO in questo momento.
    /// </summary>
    Task<RispostaAccountEvento> UsaAsync(int vidEvento, CancellationToken ct = default);

    /// <summary>Il VID dell'evento che chi guarda sta usando, o null.</summary>
    int? InUso();

    /// <summary>Torna al proprio VID.</summary>
    Task LasciaAsync(CancellationToken ct = default);
}

/// <inheritdoc cref="IAccountEventoService"/>
public sealed class AccountEventoService : IAccountEventoService
{
    private readonly IEventKitService _evento;
    private readonly IOnlineAtcProvider _online;
    private readonly ICurrentUserProvider _utenti;
    private readonly AccountEventoRegistro _registro;
    private readonly IAccountEventoTraccia? _traccia;
    private readonly IAccountEventoArchivio? _archivio;
    private readonly Func<DateTime> _adesso;

    public AccountEventoService(IEventKitService evento, IOnlineAtcProvider online, ICurrentUserProvider utenti,
        AccountEventoRegistro registro, IAccountEventoTraccia? traccia = null, Func<DateTime>? adesso = null,
        IAccountEventoArchivio? archivio = null)
    {
        _archivio = archivio;
        _evento = evento;
        _online = online;
        _utenti = utenti;
        _registro = registro;
        _traccia = traccia;
        _adesso = adesso ?? (() => DateTime.UtcNow);
    }

    public async Task<RispostaAccountEvento> UsaAsync(int vidEvento, CancellationToken ct = default)
    {
        if (_utenti.Get() is not { } utente) return new(EsitoAccountEvento.NonEntrato);
        if (await _evento.AccountAsync(ct) is not { } account) return new(EsitoAccountEvento.NessunEvento);
        if (!account.Vid.ContainsKey(vidEvento)) return new(EsitoAccountEvento.NonInLista);

        // ⚠️ Online ADESSO, sul feed: chi si è appena connesso può non esserci ancora (il feed gira ogni minuto), e la
        // pagina lo dice. Senza questo controllo la lista basterebbe a guardare la postazione di chiunque vi compaia.
        var callsign = _online.GetCurrent().Details.FirstOrDefault(d => d.UserId == vidEvento)?.Callsign;
        if (callsign is null) return new(EsitoAccountEvento.NonOnline);

        var adesso = _adesso();
        var tetto = adesso + AccountEventoRegistro.DurataMassima;
        var scade = account.FineUtc is DateTime fine && fine < tetto ? fine : tetto;
        _registro.Usa(utente.UserId, vidEvento, scade);
        if (_archivio is not null) await _archivio.SalvaAsync(utente.UserId, vidEvento, scade, ct);
        if (_traccia is not null) await _traccia.RegistraAsync(utente.UserId, vidEvento, callsign, ct);
        return new(EsitoAccountEvento.Usato, callsign);
    }

    public int? InUso() => _utenti.Get() is { } u ? _registro.VidPer(u.UserId, _adesso()) : null;

    public async Task LasciaAsync(CancellationToken ct = default)
    {
        if (_utenti.Get() is not { } u) return;
        _registro.Lascia(u.UserId);
        if (_archivio is not null) await _archivio.TogliAsync(u.UserId, ct);
    }
}

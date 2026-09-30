using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Media;
using Vipi.Domain;
using Vipi.Domain.Entities;
using static Vipi.Application.Messaggio;

namespace Vipi.Application.EventKits;

/// <summary>Il pacchetto come lo mostra una pagina.</summary>
/// <param name="Visibile">Il pubblico lo vede adesso (acceso e dentro le date).</param>
public sealed record EventKitView(string Name, bool IsActive, DateTime? StartsUtc, DateTime? EndsUtc,
    DateTime? UpdatedUtc, string UpdatedByName, IReadOnlyList<EventKitItemRow> Items, bool Visibile)
{
    public static readonly EventKitView Vuoto = new("", false, null, null, null, "", Array.Empty<EventKitItemRow>(), false);
}

/// <summary>
/// Il pacchetto dell'evento (committente, 30 settembre 2026): una pagina aperta a tutti, accesa vicino a un evento,
/// da cui si scaricano i profili per postazione e si aprono i link (lo zip dello Stand Manager su Drive).
/// Lo gestisce lo staff di divisione. Carta <c>docs/feature/2026-09-30-profili-evento.md</c>.
/// </summary>
public interface IEventKitService
{
    /// <summary>Il pacchetto per il pubblico; null se adesso non si vede.</summary>
    Task<EventKitView?> PubblicoAsync(CancellationToken ct = default);

    /// <summary>
    /// Il nome dell'evento se il pacchetto si vede adesso, null altrimenti: la domanda della scheda nell'hub, che il
    /// nome lo scrive (chi arriva sa subito se è l'evento giusto). Tenuta in memoria per pochi secondi.
    /// </summary>
    Task<string?> InCorsoAsync(CancellationToken ct = default);

    /// <summary>Tutto, anche spento: per chi lo gestisce (staff di divisione).</summary>
    Task<EventKitView> PerStaffAsync(CancellationToken ct = default);

    Task SalvaTestataAsync(string nome, bool attivo, DateTime? daUtc, DateTime? aUtc, CancellationToken ct = default);

    /// <summary>Aggiunge un file. Il contenuto si legge fino al tetto più un byte: oltre, si rifiuta senza leggere il resto.</summary>
    Task<int> AggiungiFileAsync(string etichetta, string? nota, string fileName, Stream contenuto, CancellationToken ct = default);

    Task<int> AggiungiLinkAsync(string etichetta, string? nota, string url, CancellationToken ct = default);

    Task EliminaAsync(int id, CancellationToken ct = default);

    Task SpostaAsync(int id, int verso, CancellationToken ct = default);

    /// <summary>Il file di una voce: al pubblico solo mentre il pacchetto si vede, allo staff sempre. Null altrimenti.</summary>
    Task<EventKitFile?> FileAsync(int id, CancellationToken ct = default);
}

/// <summary>
/// La risposta di <see cref="IEventKitService.InCorsoAsync"/> tenuta per qualche secondo. ⚠️ Singleton: l'hub dei
/// servizi è SSR statico, lo apre chiunque arrivi al sito, e senza questo ogni visita costerebbe una lettura.
/// Si svuota a ogni scrittura, quindi chi accende il pacchetto lo vede subito nell'hub.
/// </summary>
public sealed class EventKitVisibilityCache
{
    private readonly object _lock = new();
    private (string? Valore, DateTime Scade)? _voce;

    public static readonly TimeSpan Durata = TimeSpan.FromSeconds(30);

    /// <summary>Il nome dell'evento in corso (null = nessuno), se la risposta tenuta è ancora buona.</summary>
    public bool TryGet(DateTime adessoUtc, out string? valore)
    {
        lock (_lock)
        {
            if (_voce is { } v && adessoUtc < v.Scade) { valore = v.Valore; return true; }
            valore = null;
            return false;
        }
    }

    public void Set(string? valore, DateTime adessoUtc)
    {
        lock (_lock) _voce = (valore, adessoUtc + Durata);
    }

    public void Svuota()
    {
        lock (_lock) _voce = null;
    }
}

/// <inheritdoc cref="IEventKitService"/>
public sealed class EventKitService : IEventKitService
{
    private readonly IEventKitRepository _repo;
    private readonly IEditAuthorizationService _authz;
    private readonly EventKitVisibilityCache _cache;
    private readonly int _maxBytes;
    private readonly Func<DateTime> _adesso;

    public EventKitService(IEventKitRepository repo, IEditAuthorizationService authz, EventKitVisibilityCache cache,
        IOptions<MediaOptions> media, Func<DateTime>? adesso = null)
    {
        _repo = repo;
        _authz = authz;
        _cache = cache;
        _maxBytes = media.Value.MaxUploadBytes;
        _adesso = adesso ?? (() => DateTime.UtcNow);
    }

    public async Task<EventKitView?> PubblicoAsync(CancellationToken ct = default)
    {
        var vista = Vista(await _repo.LoadAsync(ct));
        return vista.Visibile ? vista : null;
    }

    public async Task<string?> InCorsoAsync(CancellationToken ct = default)
    {
        var adesso = _adesso();
        if (_cache.TryGet(adesso, out var v)) return v;
        var snap = await _repo.LoadAsync(ct);
        var nome = EventKitRules.Visibile(snap?.Testata, adesso) ? snap!.Testata.Name : null;
        _cache.Set(nome, adesso);
        return nome;
    }

    public async Task<EventKitView> PerStaffAsync(CancellationToken ct = default)
    {
        Guardia();
        return Vista(await _repo.LoadAsync(ct));
    }

    public async Task SalvaTestataAsync(string nome, bool attivo, DateTime? daUtc, DateTime? aUtc, CancellationToken ct = default)
    {
        var (id, chi) = Guardia();
        nome = EventKitRules.Norm(nome);
        if (nome.Length > EventKitRules.MaxNome)
            throw new Aor.ValidationException(Lingua($"Il nome dell'evento supera i {EventKitRules.MaxNome} caratteri.",
                $"The event name is longer than {EventKitRules.MaxNome} characters."));
        // Acceso senza nome no: il nome è il titolo della pagina, e «Profili per l'evento » con niente dopo non dice
        // a chi arriva se è l'evento giusto.
        if (attivo && nome.Length == 0)
            throw new Aor.ValidationException(Lingua("Scrivi il nome dell'evento prima di accenderlo.",
                "Write the event name before switching it on."));
        if (daUtc is DateTime da && aUtc is DateTime a && a <= da)
            throw new Aor.ValidationException(Lingua("La fine deve venire dopo l'inizio.", "The end must come after the start."));

        await _repo.SaveHeaderAsync(nome, attivo, daUtc, aUtc, id, chi, _adesso(), ct);
        _cache.Svuota();
    }

    public async Task<int> AggiungiFileAsync(string etichetta, string? nota, string fileName, Stream contenuto,
        CancellationToken ct = default)
    {
        var (id, chi) = Guardia();
        var nome = EventKitRules.NomeFileSicuro(fileName);
        if (nome.Length == 0 || !EventKitRules.EstensioneAmmessa(nome))
            throw new Aor.ValidationException(Lingua(
                $"Questo tipo di file non si carica. Si possono caricare: {string.Join(" ", EventKitRules.EstensioniAmmesse.Order())}.",
                $"This kind of file cannot be uploaded. Allowed: {string.Join(" ", EventKitRules.EstensioniAmmesse.Order())}."));

        // Un byte oltre il tetto basta a rifiutare senza tirarsi in casa il resto del file.
        using var buffer = new MemoryStream();
        var blocco = new byte[81920];
        int letti;
        while ((letti = await contenuto.ReadAsync(blocco, ct)) > 0)
        {
            buffer.Write(blocco, 0, letti);
            if (buffer.Length > _maxBytes) throw new Aor.ValidationException(TroppoGrande());
        }
        if (buffer.Length == 0)
            throw new Aor.ValidationException(Lingua("Il file è vuoto.", "The file is empty."));

        var voce = Voce(etichetta, nota, id, chi, etichettaDiRipiego: Path.GetFileNameWithoutExtension(nome));
        voce.Kind = EventKitItemKind.File;
        voce.FileName = nome;
        voce.Bytes = buffer.ToArray();
        voce.ByteSize = voce.Bytes.Length;
        return await AggiungiAsync(voce, ct);
    }

    public async Task<int> AggiungiLinkAsync(string etichetta, string? nota, string url, CancellationToken ct = default)
    {
        var (id, chi) = Guardia();
        url = EventKitRules.Norm(url);
        if (!EventKitRules.LinkValido(url))
            throw new Aor.ValidationException(Lingua("Il link dev'essere un indirizzo completo che comincia con https://.",
                "The link must be a full address starting with https://."));
        if (EventKitRules.Norm(etichetta).Length == 0)
            throw new Aor.ValidationException(Lingua("Scrivi che cos'è il link (per esempio «Stand Manager»).",
                "Write what the link is (for example «Stand Manager»)."));

        var voce = Voce(etichetta, nota, id, chi, etichettaDiRipiego: "");
        voce.Kind = EventKitItemKind.Link;
        voce.Url = url;
        return await AggiungiAsync(voce, ct);
    }

    public async Task EliminaAsync(int id, CancellationToken ct = default)
    {
        Guardia();
        await _repo.DeleteItemAsync(id, ct);
        _cache.Svuota();
    }

    public async Task SpostaAsync(int id, int verso, CancellationToken ct = default)
    {
        Guardia();
        await _repo.MoveItemAsync(id, Math.Sign(verso), ct);
    }

    public async Task<EventKitFile?> FileAsync(int id, CancellationToken ct = default)
    {
        if (!_authz.IsDivisionStaff)
        {
            var snap = await _repo.LoadAsync(ct);
            if (!EventKitRules.Visibile(snap?.Testata, _adesso())) return null;
        }
        return await _repo.FileAsync(id, ct);
    }

    // ---------------------------------------------------------------------------------------------------------

    private EventKitView Vista(EventKitSnapshot? snap)
    {
        if (snap is null) return EventKitView.Vuoto;
        var t = snap.Testata;
        return new EventKitView(t.Name, t.IsActive, t.StartsUtc, t.EndsUtc, t.UpdatedUtc == default ? null : t.UpdatedUtc,
            t.UpdatedByName, snap.Voci, EventKitRules.Visibile(t, _adesso()));
    }

    /// <summary>
    /// Lo staff di divisione, e il VID con cui firma. ⚠️ La guardia sta QUI e non solo nella pagina: la pagina nasconde
    /// il pannello, il servizio rifiuta — nascondere e basta è un cancello che non c'è.
    /// </summary>
    private (int Id, string Nome) Guardia()
    {
        if (!_authz.IsDivisionStaff || _authz.CurrentUserId is not int id)
            throw new Aor.ValidationException(Lingua("Il pacchetto dell'evento lo gestisce lo staff di divisione.",
                "The event package is managed by the division staff."));
        return (id, _authz.CurrentName ?? "");
    }

    private EventKitItem Voce(string etichetta, string? nota, int id, string chi, string etichettaDiRipiego)
    {
        var e = EventKitRules.Norm(etichetta);
        if (e.Length == 0) e = etichettaDiRipiego;
        if (e.Length > EventKitRules.MaxEtichetta)
            throw new Aor.ValidationException(Lingua($"Il titolo della voce supera i {EventKitRules.MaxEtichetta} caratteri.",
                $"The item title is longer than {EventKitRules.MaxEtichetta} characters."));
        var n = EventKitRules.Norm(nota);
        if (n.Length > EventKitRules.MaxNota)
            throw new Aor.ValidationException(Lingua($"La nota supera i {EventKitRules.MaxNota} caratteri.",
                $"The note is longer than {EventKitRules.MaxNota} characters."));
        return new EventKitItem
        {
            Label = e, Note = n, CreatedUtc = _adesso(), CreatedByUserId = id, CreatedByName = chi,
        };
    }

    private async Task<int> AggiungiAsync(EventKitItem voce, CancellationToken ct)
    {
        if (await _repo.CountItemsAsync(ct) >= EventKitRules.MaxVoci)
            throw new Aor.ValidationException(Lingua($"Il pacchetto ha già {EventKitRules.MaxVoci} voci: togline qualcuna.",
                $"The package already has {EventKitRules.MaxVoci} items: remove some."));
        var id = await _repo.AddItemAsync(voce, ct);
        _cache.Svuota();
        return id;
    }

    private string TroppoGrande() => Lingua(
        $"Il file supera {MediaOptions.Human(_maxBytes)}: caricalo su Drive e aggiungi il link.",
        $"The file is larger than {MediaOptions.Human(_maxBytes)}: put it on Drive and add the link.");
}

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Application.Auth;

/// <summary>
/// La forma di una chiave: <c>vipi_</c> + 32 byte casuali in base64url (43 caratteri). Carta
/// <c>docs/feature/2026-09-13-chiavi-api.md</c> §4. Funzioni pure.
/// </summary>
public static class ChiaveApi
{
    public const string Inizio = "vipi_";

    /// <summary>Quanti caratteri della chiave restano in chiaro: l'inizio fisso più sette casuali.</summary>
    public const int LunghezzaPrefisso = 12;

    private const int Lunghezza = 5 + 43;

    public static string Genera() =>
        Inizio + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Vero se il testo ha la forma di una chiave. Una stringa qualunque non va neppure cercata.</summary>
    public static bool BenFormata(string? chiave) =>
        chiave is { Length: Lunghezza }
        && chiave.StartsWith(Inizio, StringComparison.Ordinal)
        && chiave.AsSpan(Inizio.Length).IndexOfAnyExcept(Base64Url) < 0;

    /// <summary>SHA-256 della chiave, esadecimale minuscolo: quello che sta in tabella al posto della chiave.</summary>
    public static string Impronta(string chiave) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(chiave))).ToLowerInvariant();

    public static string Prefisso(string chiave) =>
        chiave.Length <= LunghezzaPrefisso ? chiave : chiave[..LunghezzaPrefisso];

    private static readonly System.Buffers.SearchValues<char> Base64Url =
        System.Buffers.SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_");
}

/// <summary>
/// Chi può emettere, revocare e vedere le chiavi: i <c>Auth:FounderVids</c>, e gli Admin con una posizione di
/// HQ o WD (<c>Auth:ApiKeyIssuerRoles</c>). Carta §5 e §8.
///
/// <para>⚠️ <b>Più stretto di Admin</b>: Admin comprende anche AOC, AOAC, SOC, SOAC, che le chiavi non le
/// vedono. E una promozione a mano ad Admin non basta: serve la posizione.</para>
/// </summary>
public interface IEmittentiChiaviApi
{
    bool PuoEmettere { get; }

    void EnsurePuoEmettere()
    {
        if (!PuoEmettere) throw new EditNotAllowedException();
    }
}

/// <inheritdoc cref="IEmittentiChiaviApi"/>
public sealed class EmittentiChiaviApi : IEmittentiChiaviApi
{
    private readonly ICurrentUserProvider _user;
    private readonly IEditAuthorizationService _authz;
    private readonly RoleResolver _resolver;
    private readonly Regex[] _codici;
    private bool? _puo;

    public EmittentiChiaviApi(ICurrentUserProvider user, IEditAuthorizationService authz, RoleResolver resolver,
        IOptions<AuthOptions> auth, IOptions<DivisionOptions> division)
    {
        _user = user;
        _authz = authz;
        _resolver = resolver;
        _codici = auth.Value.ApiKeyIssuerRoles
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(r => new Regex($"^{Regex.Escape(division.Value.Code)}-{r}$", RegexOptions.IgnoreCase))
            .ToArray();
    }

    public bool PuoEmettere => _puo ??= Calcola();

    public void EnsurePuoEmettere()
    {
        if (!PuoEmettere) throw new EditNotAllowedException();
    }

    private bool Calcola()
    {
        var u = _user.Get();
        if (u is null) return false;
        if (_resolver.Founders.Contains(u.UserId)) return true;
        if (_authz.Role < VipiRole.Admin) return false;
        return u.StaffPositions.Any(c => !string.IsNullOrWhiteSpace(c) && _codici.Any(rx => rx.IsMatch(c.Trim())));
    }
}

/// <summary>Una chiave appena creata: l'unica volta in cui la chiave esiste fuori dal client.</summary>
public sealed record ChiaveEmessa(ApiClientRow Cliente, string Chiave);

/// <summary>La pagina delle chiavi, lato servizio. Ogni metodo rifiuta chi non può emettere, anche la lettura.</summary>
public interface IApiClientService
{
    Task<IReadOnlyList<ApiClientRow>> ListAsync(CancellationToken ct = default);
    Task<ChiaveEmessa> CreaAsync(string nome, IReadOnlyCollection<string> endpoint, CancellationToken ct = default);
    Task<bool> RevocaAsync(int id, CancellationToken ct = default);
}

/// <inheritdoc cref="IApiClientService"/>
public sealed class ApiClientService : IApiClientService
{
    private readonly IApiClientStore _store;
    private readonly IEmittentiChiaviApi _emittenti;
    private readonly IEditAuthorizationService _authz;

    public ApiClientService(IApiClientStore store, IEmittentiChiaviApi emittenti, IEditAuthorizationService authz)
    {
        _store = store;
        _emittenti = emittenti;
        _authz = authz;
    }

    public Task<IReadOnlyList<ApiClientRow>> ListAsync(CancellationToken ct = default)
    {
        _emittenti.EnsurePuoEmettere();
        return _store.ListAsync(ct);
    }

    public async Task<ChiaveEmessa> CreaAsync(string nome, IReadOnlyCollection<string> endpoint, CancellationToken ct = default)
    {
        _emittenti.EnsurePuoEmettere();

        var n = (nome ?? "").Trim();
        if (n.Length == 0)
            throw new Aor.ValidationException(Messaggio.Lingua(
                "Serve un nome: fra sei mesi è l'unica cosa che dice di chi è la chiave.",
                "A name is required: in six months it is the only thing that says whose key it is."));
        if (n.Length > ApiClientLimits.Nome)
            throw new Aor.ValidationException(Messaggio.Lingua(
                $"Nome troppo lungo: al massimo {ApiClientLimits.Nome} caratteri.",
                $"Name too long: at most {ApiClientLimits.Nome} characters."));

        var scelti = (endpoint ?? Array.Empty<string>())
            .Select(e => (e ?? "").Trim().ToLowerInvariant())
            .Where(e => e.Length > 0)
            .Distinct()
            .ToList();
        if (scelti.Count == 0)
            throw new Aor.ValidationException(Messaggio.Lingua(
                "Scegli almeno un'API: una chiave che non apre niente non serve a nessuno.",
                "Pick at least one API: a key that opens nothing is of no use."));
        if (scelti.FirstOrDefault(e => !ApiEndpoints.Tutti.Contains(e)) is { } ignota)
            throw new Aor.ValidationException(Messaggio.Lingua(
                $"API sconosciuta: «{ignota}».", $"Unknown API: «{ignota}»."));

        var chiave = ChiaveApi.Genera();
        var riga = await _store.AddAsync(n, ChiaveApi.Prefisso(chiave), ChiaveApi.Impronta(chiave),
            ApiEndpoints.Tutti.Where(scelti.Contains).ToList(), _authz.CurrentUserId ?? 0, ct).ConfigureAwait(false);
        return new ChiaveEmessa(riga, chiave);
    }

    public Task<bool> RevocaAsync(int id, CancellationToken ct = default)
    {
        _emittenti.EnsurePuoEmettere();
        return _store.RevocaAsync(id, _authz.CurrentUserId ?? 0, ct);
    }
}

/// <summary>Come è andata una chiave presentata a un'API.</summary>
public enum EsitoChiaveApi
{
    /// <summary>Buona, e abilitata a questa API.</summary>
    Valida,

    /// <summary>Malformata, mai emessa, o revocata. Per chi chiama è la stessa cosa: 401.</summary>
    Sconosciuta,

    /// <summary>Buona, ma non per questa API: 403.</summary>
    NonAbilitata,
}

/// <param name="Prefisso">L'inizio della chiave presentata, per i log e per il limitatore. Null se malformata.</param>
public sealed record VerificaChiave(EsitoChiaveApi Esito, string? Prefisso, ApiClientRow? Cliente);

/// <summary>La domanda di ogni chiamata a un'API: questa chiave apre questa porta?</summary>
public interface IVerificaChiaveApi
{
    Task<VerificaChiave> VerificaAsync(string chiave, string endpoint, CancellationToken ct = default);
}

/// <inheritdoc cref="IVerificaChiaveApi"/>
public sealed class VerificaChiaveApi : IVerificaChiaveApi
{
    /// <summary>Ogni quanto al massimo si riscrive l'ultimo uso: un client in polling non deve fare una UPDATE al secondo.</summary>
    public static readonly TimeSpan PassoUltimoUso = TimeSpan.FromMinutes(5);

    private readonly IApiClientStore _store;

    public VerificaChiaveApi(IApiClientStore store) => _store = store;

    public async Task<VerificaChiave> VerificaAsync(string chiave, string endpoint, CancellationToken ct = default)
    {
        // Una stringa che non ha la forma di una chiave non si cerca: niente query per il rumore.
        if (!ChiaveApi.BenFormata(chiave)) return new VerificaChiave(EsitoChiaveApi.Sconosciuta, null, null);

        var prefisso = ChiaveApi.Prefisso(chiave);
        // ⚠️ Si cerca per impronta, non per chiave: l'impronta di una chiave indovinata a metà non dice nulla
        // della chiave vera, quindi il tempo della ricerca non insegna niente a chi prova.
        var cliente = await _store.TrovaPerImprontaAsync(ChiaveApi.Impronta(chiave), ct).ConfigureAwait(false);
        if (cliente is null || !cliente.Attiva) return new VerificaChiave(EsitoChiaveApi.Sconosciuta, prefisso, cliente);
        if (!cliente.Endpoint.Contains(endpoint)) return new VerificaChiave(EsitoChiaveApi.NonAbilitata, prefisso, cliente);

        var adesso = DateTime.UtcNow;
        if (cliente.UltimoUsoUtc is not { } ultimo || adesso - ultimo >= PassoUltimoUso)
            await _store.SegnaUsoAsync(cliente.Id, adesso, ct).ConfigureAwait(false);

        return new VerificaChiave(EsitoChiaveApi.Valida, prefisso, cliente);
    }
}

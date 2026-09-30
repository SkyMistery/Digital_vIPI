using Vipi.Domain.Entities;

namespace Vipi.Application.EventKits;

/// <summary>
/// Le regole del pacchetto dell'evento (carta <c>docs/feature/2026-09-30-profili-evento.md</c>). <b>Pure</b>: le usano
/// il servizio in scrittura, la pagina per dirlo prima di provarci, e l'endpoint che serve i file — devono essere le
/// stesse, o la pagina direbbe «si vede» di un pacchetto che l'endpoint rifiuta.
/// </summary>
public static class EventKitRules
{
    public const int MaxNome = 128;
    public const int MaxEtichetta = 128;
    public const int MaxNota = 300;
    public const int MaxUrl = 1000;
    public const int MaxNomeFile = 200;

    /// <summary>Quante voci al massimo: un evento grande ha qualche decina di postazioni, non centinaia.</summary>
    public const int MaxVoci = 80;

    /// <summary>L'indirizzo della pagina pubblica. Un servizio figlio diretto di <c>/services</c>.</summary>
    public const string Rotta = "/services/event";

    /// <summary>L'indirizzo che serve il file di una voce. Il nome in coda è per chi scarica (e per il browser).</summary>
    public static string UrlFile(int id, string fileName) => $"{Rotta}/file/{id}/{Uri.EscapeDataString(fileName)}";

    /// <summary>
    /// Le estensioni che si possono caricare: i file di Aurora (profili <c>.cpr</c>, colori, settori) e gli
    /// archivi. ⚠️ Una lista che AMMETTE, non una che vieta: un <c>.exe</c> o un <c>.html</c> serviti da questo
    /// dominio a chiunque sarebbero un regalo a chi entrasse con un account staff rubato. Il file esce comunque
    /// come allegato e con <c>nosniff</c>, ma la prima difesa è non accettarlo.
    /// </summary>
    public static readonly IReadOnlySet<string> EstensioniAmmesse = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".cpr", ".clr", ".isc", ".sct", ".ese", ".pof", ".txt", ".xml", ".json", ".ini", ".pdf", ".zip", ".7z", ".rar",
    };

    /// <summary>Per l'attributo <c>accept</c> del campo file: la stessa lista, perché la pagina non proponga altro.</summary>
    public static string Accept => string.Join(",", EstensioniAmmesse.OrderBy(e => e, StringComparer.Ordinal));

    public static string Norm(string? v) => (v ?? "").Trim();

    /// <summary>
    /// Il pacchetto si vede al pubblico: ACCESO e dentro la finestra di date, quando c'è. L'inizio è compreso, la fine
    /// no. ⚠️ Spento vince su tutto: le date sono un di più per non dover stare svegli all'ora giusta, non un secondo
    /// interruttore.
    /// </summary>
    public static bool Visibile(EventKit? kit, DateTime adessoUtc) =>
        kit is { IsActive: true }
        && (kit.StartsUtc is not DateTime da || adessoUtc >= da)
        && (kit.EndsUtc is not DateTime a || adessoUtc < a);

    /// <summary>Un link si accetta solo assoluto e <c>https</c>: è un indirizzo che il sito consiglia a chiunque.</summary>
    public static bool LinkValido(string? url) =>
        Uri.TryCreate(Norm(url), UriKind.Absolute, out var u)
        && u.Scheme == Uri.UriSchemeHttps
        && !string.IsNullOrEmpty(u.Host)
        && Norm(url).Length <= MaxUrl;

    /// <summary>Il dominio del link, da mostrare accanto («drive.google.com»): chi clicca sa dove va.</summary>
    public static string Dominio(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Host : "";

    public static bool EstensioneAmmessa(string? fileName) =>
        EstensioniAmmesse.Contains(Path.GetExtension(Norm(fileName)));

    /// <summary>
    /// Il nome con cui il file si salva e si scarica: solo la parte finale (niente cartelle), senza caratteri di
    /// controllo né virgolette, e non oltre <see cref="MaxNomeFile"/> — tenendo l'estensione.
    /// </summary>
    public static string NomeFileSicuro(string? fileName)
    {
        var nome = Path.GetFileName(Norm(fileName).Replace('\\', '/').Split('/').Last());
        nome = new string(nome.Where(c => !char.IsControl(c) && c != '"').ToArray()).Trim();
        if (nome.Length <= MaxNomeFile) return nome;
        var ext = Path.GetExtension(nome);
        return nome[..(MaxNomeFile - ext.Length)] + ext;
    }
}

using Vipi.Application.Abstractions;
using Vipi.Application.Translation;
using Vipi.Domain.Entities;

namespace Vipi.Application.Tests;

/// <summary>
/// Una memoria di traduzione finta: quel che è stato notato, e niente rete.
///
/// <para>
/// ⚠️ <b>Registra anche la COPPIA DI LINGUE con cui è stata interrogata</b>, e non è un dettaglio da test:
/// dal 28 agosto 2026 la lingua sorgente la dichiara il documento, non la pagina, e l'unico modo di provarlo
/// è guardare che cosa il traduttore è andato a cercare. Chiedere «it→en» per un documento inglese non
/// solleva nessun errore: torna semplicemente una memoria vuota, e il lettore vede il documento intatto
/// senza capire perché.
/// </para>
/// </summary>
internal sealed class MemoriaDiTraduzioneFinta : ITranslationMemory
{
    private readonly Dictionary<string, KnownTranslation> _note = new(StringComparer.Ordinal);

    /// <summary>Quante volte la memoria è stata letta: con le due lingue uguali dev'essere zero.</summary>
    public int Letture { get; private set; }

    /// <summary>La coppia di lingue dell'ultima interrogazione, per provare da dove si è tradotto.</summary>
    public string? UltimaSorgente { get; private set; }

    /// <inheritdoc cref="UltimaSorgente"/>
    public string? UltimoBersaglio { get; private set; }

    public MemoriaDiTraduzioneFinta Nota(string sorgente, string bersaglio, bool riletta = false)
    {
        _note[TranslationText.Hash(sorgente)] =
            new KnownTranslation(bersaglio, riletta ? TranslationOrigin.Human : TranslationOrigin.Machine, riletta);
        return this;
    }

    public Task<IReadOnlyDictionary<string, KnownTranslation>> LookupAsync(
        string s, string t, IReadOnlyCollection<string> hashes, CancellationToken ct = default)
    {
        Letture++;
        UltimaSorgente = s;
        UltimoBersaglio = t;
        return Task.FromResult<IReadOnlyDictionary<string, KnownTranslation>>(
            hashes.Where(_note.ContainsKey).ToDictionary(h => h, h => _note[h], StringComparer.Ordinal));
    }

    public Task<int> SaveMachineAsync(string s, string t, string e,
        IReadOnlyList<(string SourceText, string TargetText)> v, CancellationToken ct = default) => Task.FromResult(0);

    /// <summary>L'ultima correzione salvata: coppia di lingue, testo sorgente, resa e chi l'ha scritta.</summary>
    public (string Da, string A, string Sorgente, string Tradotto, int Utente)? UltimaCorrezione { get; private set; }

    public Task SaveHumanAsync(string s, string t, string a, string b, int u, CancellationToken ct = default)
    {
        UltimaCorrezione = (s, t, a, b, u);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TranslationReviewRow>> ListForReviewAsync(
        string s, string t, bool solo, int limite, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<TranslationReviewRow>>(Array.Empty<TranslationReviewRow>());

    public Task<IReadOnlyDictionary<string, string>> LoadAllAsync(
        string s, string t, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<string, string>>(
            new Dictionary<string, string>(StringComparer.Ordinal));

    public Task<IReadOnlySet<string>> LoadHumanHashesAsync(
        string s, string t, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.Ordinal));

    public Task<(int Totale, int DaRileggere)> ContaAsync(string s, string t, CancellationToken ct = default) =>
        Task.FromResult((0, 0));

    public Task<int> DocumentiToccatiAsync(string s, CancellationToken ct = default) => Task.FromResult(0);

    public Task<long> CaratteriSpesiStimatiAsync(string e, CancellationToken ct = default) => Task.FromResult(0L);
}

using Vipi.Application.Auth;
using static Vipi.Application.Messaggio;

namespace Vipi.Application.Content;

/// <summary>Una sezione di catalogo, per profilo, come la mostra la pagina delle sezioni da scrivere (S133).</summary>
/// <param name="Percorso">I titoli dal primo livello, « › » fra l'uno e l'altro.</param>
/// <param name="Facoltativa">Un admin l'ha dichiarata facoltativa: vuota non conta.</param>
public sealed record SezioneDelCatalogo(SectionProfile Profilo, string Chiave, string Percorso, bool Facoltativa);

/// <summary>Dove stanno le sezioni dichiarate facoltative (<c>OptionalSection</c>).</summary>
public interface ISezioniFacoltativeStore
{
    Task<IReadOnlySet<(SectionProfile Profilo, string Chiave)>> TutteAsync(CancellationToken ct = default);

    /// <summary>Accende o spegne «facoltativa». Idempotente.</summary>
    Task ImpostaAsync(SectionProfile profilo, string chiave, bool facoltativa, int userId, CancellationToken ct = default);
}

public interface ISezioniFacoltativeService
{
    /// <summary>Le sezioni che la regola di partenza conta, profilo per profilo, con l'interruttore di ciascuna.</summary>
    Task<IReadOnlyList<SezioneDelCatalogo>> ElencoAsync(CancellationToken ct = default);

    /// <summary>Dichiara una sezione facoltativa, o di nuovo da scrivere. Solo admin: vale per tutti i documenti di quel
    /// profilo.</summary>
    Task ImpostaAsync(SectionProfile profilo, string chiave, bool facoltativa, CancellationToken ct = default);
}

/// <summary>
/// Le sezioni facoltative (S133, carta 2026-10-28-stato-dei-documenti §1 D4). Il committente: le sezioni da scrivere
/// le decide il catalogo; la regola di oggi resta il punto di partenza e un admin la restringe sezione per sezione.
/// </summary>
public sealed class SezioniFacoltativeService : ISezioniFacoltativeService
{
    private readonly ISezioniFacoltativeStore _store;
    private readonly IEditAuthorizationService _authz;

    public SezioniFacoltativeService(ISezioniFacoltativeStore store, IEditAuthorizationService authz)
    {
        _store = store;
        _authz = authz;
    }

    public async Task<IReadOnlyList<SezioneDelCatalogo>> ElencoAsync(CancellationToken ct = default)
    {
        _authz.EnsureAtLeast(Vipi.Domain.VipiRole.Admin);
        var facoltative = await _store.TutteAsync(ct);
        var elenco = new List<SezioneDelCatalogo>();
        foreach (var profilo in Enum.GetValues<SectionProfile>())
            foreach (var (chiave, percorso) in SezioniDaScrivere.DelCatalogo(profilo, System.Globalization.CultureInfo.CurrentUICulture.Name))
                elenco.Add(new SezioneDelCatalogo(profilo, chiave, percorso, facoltative.Contains((profilo, chiave))));
        return elenco;
    }

    public async Task ImpostaAsync(SectionProfile profilo, string chiave, bool facoltativa, CancellationToken ct = default)
    {
        _authz.EnsureAtLeast(Vipi.Domain.VipiRole.Admin);
        // Solo le sezioni che la regola conta: le altre non sono mai da scrivere, e un interruttore su di loro non
        // cambierebbe niente se non una riga in più da spiegare.
        if (!SezioniDaScrivere.DaScrivere(profilo, chiave))
            throw new Aor.ValidationException(Lingua(
                $"«{chiave}» non è una sezione da scrivere del profilo {profilo}.",
                $"«{chiave}» is not a section to write in profile {profilo}."));
        await _store.ImpostaAsync(profilo, chiave, facoltativa, _authz.CurrentUserId ?? 0, ct);
    }
}

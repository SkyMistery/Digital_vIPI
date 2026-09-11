using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Ui.Components.App;

/// <summary>
/// Uno scalo dell'elenco pubblico di una ACC, e quali dei suoi due documenti si possono aprire: la vIPI
/// civile, il vSOP militare, o tutti e due.
/// </summary>
public sealed record AeroportoInElenco(AirportRow Scalo, bool HaVipi, bool HaVsop)
{
    public string VipiHref(string acc) => $"/services/vsop/{acc.ToLowerInvariant()}/airports?icao={Scalo.Icao}";

    /// <summary>
    /// ⚠️ In vista <b>ATC</b> (committente, 11 settembre 2026): chi arriva dalla documentazione di un'ACC è un
    /// controllore, come chi arriva dall'elenco NAZIONALE è un pilota — lì la vista è <c>pilota</c>. Sta
    /// nell'indirizzo e non è un default nascosto della pagina: resta condivisibile, e la chip in testata
    /// riporta a «Tutto» con un clic. Su un documento senza sezioni marcate non filtra niente.
    /// </summary>
    public string VsopHref(string acc) =>
        $"/services/vsop/{acc.ToLowerInvariant()}/mil?icao={Scalo.Icao}&vista={AudienceFilter.QueryAtc}";

    /// <summary>Dove porta la riga quando c'è posto per UN solo collegamento: la vIPI se c'è, altrimenti il
    /// vSOP. Un campo solo militare ha soltanto il secondo.</summary>
    public string Href(string acc) => HaVipi ? VipiHref(acc) : VsopHref(acc);
}

/// <summary>
/// Quali scali compaiono sotto una ACC, e con quali documenti. Stanno qui e non nelle due pagine che lo
/// chiedono — la landing dell'ACC (conteggio e i tre in evidenza) e l'elenco degli aeroporti — perché due
/// porte che decidono la stessa cosa devono chiedere la stessa cosa.
///
/// <para>⚠️ Dall'11 settembre 2026 il vSOP militare sta QUI, accanto alla vIPI, e la landing non ha più una
/// scheda «vSOP militari» sua: un campo misto era in due schede diverse e un campo solo militare mancava
/// dall'elenco degli aeroporti, come se non fosse un aeroporto.</para>
/// </summary>
public static class AeroportiDellAcc
{
    /// <summary>
    /// Gli scali dell'ACC che hanno <b>almeno un</b> documento da mostrare al pubblico, in ordine di ICAO.
    ///
    /// <para>Il cancello è quello di ogni elenco pubblico (doc 10 §3f): release AIRAC effettiva e documento non
    /// nascosto. Per lo scalo, due regole diverse e non per svista:</para>
    /// <list type="bullet">
    ///   <item>la <b>vIPI</b> pretende <see cref="AirportRow.IsPublic"/>, cioè anche almeno un settore — la
    ///   regola che l'elenco ha sempre avuto;</item>
    ///   <item>il <b>vSOP</b> pretende solo che lo scalo non sia nascosto: un campo solo militare può non avere
    ///   nessun settore, ed è la regola dell'elenco nazionale, che altrimenti mostrerebbe un documento che
    ///   questo nasconde.</item>
    /// </list>
    ///
    /// <para>⚠️ La categoria dello scalo <b>non</b> filtra i documenti: cambiarla non li tocca (carta
    /// 2026-09-11-categorie-aeroporto.md), e un documento pubblicato fuori categoria resta leggibile finché
    /// qualcuno non lo nasconde — lo segnala la Diagnostica. Toglierlo da qui lo renderebbe irraggiungibile
    /// senza spegnerlo.</para>
    /// </summary>
    public static IReadOnlyList<AeroportoInElenco> Elenco(
        IEnumerable<AirportRow> scali, IEnumerable<ManagedDoc> documenti, string acc)
    {
        var docs = documenti
            .Where(m => m.HasEffectiveRelease && !m.IsHidden
                        && string.Equals(m.AccCode, acc, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var vipi = Chiavi(docs, ReleaseTargetType.Airport);
        var vsop = Chiavi(docs, ReleaseTargetType.AirportMil);

        return scali
            .Where(s => !s.IsHidden)
            .Select(s => new AeroportoInElenco(s, s.IsPublic && vipi.Contains(s.Icao), vsop.Contains(s.Icao)))
            .Where(a => a.HaVipi || a.HaVsop)
            .OrderBy(a => a.Scalo.Icao, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>L'ordine dei filtri: dal più civile al più militare. Non è l'ordine dell'enum, dove i valori
    /// si aggiungono in coda.</summary>
    public static readonly IReadOnlyList<AirportCategory> OrdineDelleCategorie = new[]
    {
        AirportCategory.Civil,
        AirportCategory.CivilWithMilitaryPresence,
        AirportCategory.MilitaryWithCivilPresence,
        AirportCategory.MilitaryOnly,
    };

    private static HashSet<string> Chiavi(IEnumerable<ManagedDoc> docs, ReleaseTargetType tipo) =>
        docs.Where(m => m.Kind == tipo).Select(m => m.Scope).ToHashSet(StringComparer.OrdinalIgnoreCase);
}

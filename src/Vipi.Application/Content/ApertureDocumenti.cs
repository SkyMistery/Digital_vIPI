using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Quante volte si apre ogni documento (carta <c>docs/feature/2026-10-01-aperture-documenti.md</c>).
/// </summary>
public interface IApertureDocumenti
{
    /// <summary>
    /// Conta un'apertura della pagina PUBBLICA di un documento. ⚠️ Non lancia mai: un contatore che si rompe non
    /// deve portarsi dietro la pagina che il lettore ha chiesto. L'errore va nel registro e basta.
    /// </summary>
    Task SegnaAsync(ReleaseTargetType tipo, string chiave, CancellationToken ct = default);

    /// <summary>Le aperture di ogni documento (per ID) negli ultimi <see cref="ApertureDocumenti.Finestra"/> giorni;
    /// chi non ne ha non c'è. ⚠️ Anche questa non lancia: senza numeri la pagina ricade sull'ordine di prima.</summary>
    Task<IReadOnlyDictionary<int, int>> RecentiAsync(CancellationToken ct = default);
}

/// <summary>Le regole delle aperture che non dipendono dall'archivio.</summary>
public static class ApertureDocumenti
{
    /// <summary>I giorni che contano per la classifica: oggi e gli 89 prima (committente, 1° ottobre 2026).</summary>
    public const int Finestra = 90;

    /// <summary>
    /// I primi <paramref name="quanti"/> di un gruppo per la pagina dell'ACC: prima quelli messi <b>in evidenza</b> a
    /// mano (nel loro ordine), poi i più aperti, a pari aperture per nome.
    /// <para>⚠️ La scelta manuale vince sui numeri (committente, 1° ottobre 2026): chi ha fissato un documento lo vuole
    /// lì anche se quel mese lo apre poca gente. Senza aperture — contatore nuovo, o archivio irraggiungibile — il
    /// risultato è l'ordine di prima: in evidenza, poi alfabetico.</para>
    /// </summary>
    public static IReadOnlyList<T> Primi<T>(IEnumerable<T> voci, Func<T, int?> inEvidenza, Func<T, int> aperture,
                                            Func<T, string> nome, int quanti = 3) =>
        voci.OrderBy(v => inEvidenza(v) ?? int.MaxValue)
            .ThenByDescending(aperture)
            .ThenBy(nome)
            .Take(quanti)
            .ToList();
}

using System.Text.Json;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Application.Content;

/// <summary>
/// Il <b>testo comune</b> di una sezione: blocchi scritti una volta, che ogni documento con quella sezione mostra in
/// testa ai blocchi suoi (S127, carta <c>docs/feature/2026-10-22-testo-comune.md</c>). Il primo e oggi l'unico è la
/// «Tecnica operativa» delle torri radar: l'obbligo di leggere le linee guida e i cinque usi consentiti del radar.
///
/// <para>⚠️ <b>Si congela con la release</b> (committente, 22 ottobre 2026): ogni documento mostra il testo comune
/// della sua pubblicazione, non quello di adesso. Viaggia nella vista derivata della sezione che lo porta
/// (<see cref="AirportRadarTowerView.Comune"/>), che la release congela già. Quando cambia, la riga «Da rivedere»
/// <see cref="ImpactKind.CommonTextChanged"/> chiede di ripubblicare.</para>
///
/// <para>⚠️ Vive in un <see cref="SharedBlock"/>, come le intro di pagina, e con gli stessi blocchi
/// (<see cref="ExtraBlock"/>): un secondo modello di blocco vorrebbe dire un secondo editor.</para>
/// </summary>
public static class TestoComune
{
    /// <summary>Il prefisso delle chiavi in <c>SharedBlocks</c>: lo riconosce il giro della traduzione.</summary>
    public const string Prefisso = "section-common:";

    /// <summary>La lingua in cui il testo comune è <b>scritto</b>: la divisione redige in italiano, come le intro.</summary>
    public const Language Sorgente = Language.It;

    /// <summary>
    /// L'<c>Order</c> del primo blocco comune nella sezione: lontano sotto qualunque blocco vero (che parte da 1), e
    /// sotto la soglia 0 delle sotto-sezioni «dopo nessun blocco». Così il testo comune sta in testa al corpo.
    /// </summary>
    private const int PrimoOrdine = -100_000;

    /// <summary>La chiave del <see cref="SharedBlock"/> che porta il testo comune di una sezione.</summary>
    public static string Chiave(string sezione) => Prefisso + sezione.Trim().ToLowerInvariant();

    /// <summary>La chiave del lock di modifica, nel vocabolario di <c>ResourceLockKeys</c>.</summary>
    public static string ChiaveLock(string sezione) => "editor:" + Chiave(sezione);

    /// <summary>I blocchi salvati. Vuoto o illeggibile → nessun blocco.</summary>
    public static List<ExtraBlock> Parse(string? json) =>
        string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith('{') ? new() : ExtraBlocks.Parse(json);

    /// <summary>I blocchi in JSON. Niente da salvare → <c>null</c>, che è «il testo comune non c'è».</summary>
    public static string? Serialize(IReadOnlyList<ExtraBlock> blocchi) => ExtraBlocks.Serialize(blocchi);

    /// <summary>
    /// I blocchi come si rendono, in testa alla sezione. Id <b>negativi</b>: nessun blocco vero li ha, e i
    /// componenti li usano solo come chiave di resa.
    /// </summary>
    public static IReadOnlyList<BlockView> Vista(IReadOnlyList<ExtraBlock> blocchi)
    {
        var vista = new List<BlockView>();
        foreach (var b in blocchi)
            if (PageIntro.Blocco(b, -(vista.Count + 1), PrimoOrdine + vista.Count) is { } bv) vista.Add(bv);
        return vista;
    }

    /// <summary>
    /// Il testo comune come una sezione a sé, senza titolo: la forma in cui il giro della traduzione ne ricava le frasi
    /// (<c>DocumentTranslator.SegmentiSezione</c>). Non si mostra mai così.
    /// </summary>
    public static SectionView ComeSezione(IReadOnlyList<ExtraBlock> blocchi) => new()
    {
        Id = "testo-comune",
        Title = "",
        Depth = 0,
        SectionKey = "testo-comune",
        Blocks = Vista(blocchi),
        Children = Array.Empty<SectionView>(),
    };

    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = false };

    /// <summary>
    /// Che cosa il testo comune <b>dice</b>, per confrontare quello congelato in una release con quello di adesso.
    /// Né id né ordine: cambiano senza che cambi il testo. Nessun blocco e <c>null</c> danno la stessa firma.
    /// </summary>
    public static string Firma(IReadOnlyList<BlockView>? blocchi) =>
        blocchi is not { Count: > 0 }
            ? ""
            : JsonSerializer.Serialize(blocchi.Select(b => new { b.Format, b.Body, b.BodyJson, b.CalloutKind }), Opts);
}

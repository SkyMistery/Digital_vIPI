namespace Vipi.Application.Content;

/// <summary>
/// Da chi è prodotto il corpo di una sezione (doc refactor 13 §3a). Ortogonale a <see cref="SectionKind"/>: la
/// <i>natura</i> di una sezione è la stessa ovunque, ma <i>chi ne disegna il corpo</i> dipende dalla famiglia
/// documentale — «Aree regolamentate» è un picker sulla vIPI ACC/APP e testo bilaterale sulla vLOA.
/// </summary>
public enum SectionBodySource
{
    /// <summary>Il corpo sono i <c>ContentBlock</c> della sezione, resi da <c>SectionBody</c>/<c>BlockRenderer</c>.</summary>
    Blocks,

    /// <summary>Il corpo lo produce la pagina ospite: sezioni derivate e editoriali-<b>strutturate</b>
    /// (separazioni, configurazioni, VFR, aree regolamentate), che hanno un editor dedicato.</summary>
    Host,

    /// <summary>
    /// TUTTI E DUE: la pagina disegna una scheda in testa, e sotto restano i <c>ContentBlock</c> della sezione.
    /// <para>
    /// Serve a «Validità e revisione» (26 agosto 2026): il ciclo AIRAC, la data e chi ha pubblicato sono
    /// <b>fatti</b> che nessuno deve ricopiare a mano, ma il resto — «ciclo di revisione bilaterale»,
    /// «firmatario italiano» su una vLOA — è contenuto d'accordo che nessuno può derivare. Le due cose stanno
    /// nella stessa sezione perché rispondono alla stessa domanda: da quando vale, e chi risponde.
    /// </para>
    /// </summary>
    HostAndBlocks,
}

/// <summary>
/// Descrittore di una sezione fissa nel catalogo per un dato profilo: chiave stabile, titolo (localizzato per
/// profilo), ordine di default, natura e sorgente del corpo. Il <see cref="Kind"/> è fonte unica dal
/// <see cref="SectionCatalog"/> (una sezione ha la stessa natura ovunque); <see cref="BodySource"/> no, è
/// per profilo. Doc refactor 08a, esteso dal doc 13 §3a.
/// </summary>
/// <param name="Children">Le sotto-sezioni FISSE di questa sezione, seminate insieme al padre.
/// <para>⚠️ Vuota per quasi tutti i profili, e non è una dimenticanza: fino al 28 agosto 2026 nessun
/// profilo aveva sotto-sezioni fisse, e <c>DocumentBirth</c> seminava il solo primo livello. I SOP militari
/// hanno quattro contenitori con figli — «Dati generali» con dentro radioassistenze, frequenze, alternati —
/// e senza questo campo il documento nascerebbe piatto, con venti sezioni di primo livello al posto di
/// sei.</para></param>
public sealed record SectionDescriptor(
    string Key, string Title, int Order, SectionKind Kind, SectionBodySource BodySource,
    IReadOnlyList<SectionDescriptor>? Children = null);

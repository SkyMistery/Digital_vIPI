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
}

/// <summary>
/// Descrittore di una sezione fissa nel catalogo per un dato profilo: chiave stabile, titolo (localizzato per
/// profilo), ordine di default, natura e sorgente del corpo. Il <see cref="Kind"/> è fonte unica dal
/// <see cref="SectionCatalog"/> (una sezione ha la stessa natura ovunque); <see cref="BodySource"/> no, è
/// per profilo. Doc refactor 08a, esteso dal doc 13 §3a.
/// </summary>
public sealed record SectionDescriptor(string Key, string Title, int Order, SectionKind Kind, SectionBodySource BodySource);

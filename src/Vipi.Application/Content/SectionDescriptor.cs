namespace Vipi.Application.Content;

/// <summary>
/// Descrittore di una sezione fissa nel catalogo per un dato profilo: chiave stabile, titolo (localizzato per
/// profilo), ordine di default e natura. Il <see cref="Kind"/> è fonte unica dal <see cref="SectionCatalog"/>
/// (una sezione ha la stessa natura ovunque). Doc refactor 08a.
/// </summary>
public sealed record SectionDescriptor(string Key, string Title, int Order, SectionKind Kind);

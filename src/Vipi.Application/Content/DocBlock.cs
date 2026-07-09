using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Blocco di contenuto dentro una sezione editoriale (doc refactor 08a): testo (markdown), tabella o callout.
/// <see cref="Body"/> = markdown per Prose/Callout; <see cref="BodyJson"/> = payload strutturato (colonne/righe
/// tabella, titolo callout). Riusa <see cref="BlockFormat"/>/<see cref="CalloutKind"/> del dominio.
/// </summary>
public sealed record DocBlock(
    BlockFormat Format,
    string? Body = null,
    string? BodyJson = null,
    CalloutKind? CalloutKind = null);

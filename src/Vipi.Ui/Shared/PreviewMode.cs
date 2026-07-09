namespace Vipi.Ui.Shared;

/// <summary>Modalità di resa di un viewer documentale.</summary>
public enum PreviewKind
{
    /// <summary>Vista pubblica: release effettiva al ciclo corrente, altrimenti stato pubblicato/live.</summary>
    Public,
    /// <summary>Bozza live in lavorazione (bypassa release/nascondi). Gated: chi può editare l'ACC.</summary>
    Draft,
    /// <summary>Snapshot congelato di una specifica release. Gated: chi può editare l'ACC.</summary>
    Release,
}

/// <summary>
/// Modalità anteprima di un viewer, ricavata dal query param <c>as</c> (uniforme su tutti i tipi di documento):
/// assente → <see cref="PreviewKind.Public"/>; <c>as=draft</c> → <see cref="PreviewKind.Draft"/>;
/// <c>as=rel:{id}</c> → <see cref="PreviewKind.Release"/> con <see cref="ReleaseId"/>.
/// </summary>
public readonly record struct PreviewMode(PreviewKind Kind, int ReleaseId)
{
    public bool IsPreview => Kind != PreviewKind.Public;

    /// <param name="asParam">valore del query param <c>as</c>.</param>
    /// <param name="legacyLive">alias di retrocompatibilità: <c>live=1|true</c> → Draft (vecchi link APP). <c>as</c> esplicito vince.</param>
    public static PreviewMode Parse(string? asParam, string? legacyLive = null)
    {
        var s = (asParam ?? "").Trim();
        if (s.Equals("draft", StringComparison.OrdinalIgnoreCase))
            return new(PreviewKind.Draft, 0);
        if (s.StartsWith("rel:", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(s.AsSpan(4), out var id) && id > 0)
            return new(PreviewKind.Release, id);
        if (legacyLive is "1" or "true")
            return new(PreviewKind.Draft, 0);
        return new(PreviewKind.Public, 0);
    }
}

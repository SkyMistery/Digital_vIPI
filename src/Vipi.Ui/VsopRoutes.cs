namespace Vipi.Ui;

/// <summary>
/// Il prefisso delle pagine vSOP e le poche letture di percorso che ne dipendono.
/// <para>
/// <b>Perché esiste.</b> Il prefisso è cambiato il 22 agosto 2026 (<c>/vsop</c> → <c>/services/vsop</c>) e
/// il layout non se n'è accorto: <c>SopLayout</c> leggeva il codice ACC contando i segmenti a mano e
/// confrontando il primo con <c>"vsop"</c>, che da quel giorno vale <c>"services"</c>. Risultato: nessun ACC
/// è più stato evidenziato in barra, e <c>aria-current="page"</c> non è più stato emesso — un difetto
/// invisibile alla compilazione e ai test, perché una stringa sbagliata compila benissimo.
/// </para>
/// <para>
/// ⚠️ Questa classe sta in <c>Vipi.Ui</c> e non accanto a <c>LegacyRoutes</c> perché il layout è qui e
/// <c>Vipi.Ui</c> non vede <c>Vipi.Host</c>: la dipendenza va nell'altro verso. <c>LegacyRoutes.Prefix</c>
/// rimanda qui, così il prefisso resta scritto in <b>un posto solo</b>.
/// </para>
/// </summary>
public static class VsopRoutes
{
    /// <summary>Il prefisso di oggi, senza barra finale.</summary>
    public const string Prefix = "/services/vsop";

    /// <summary>I segmenti del prefisso, nell'ordine in cui compaiono nel percorso.</summary>
    private static readonly string[] PrefixSegments =
        Prefix.Split('/', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// Il segmento che segue il prefisso in <paramref name="absolutePath"/>, o <c>null</c> se il percorso non
    /// sta sotto il prefisso o non ha nulla dopo.
    /// <para>
    /// ⚠️ Torna il segmento <b>grezzo</b>, non un ACC verificato: su <c>/services/vsop/admin/airports</c>
    /// risponde <c>"admin"</c>. Chi chiama lo confronta con l'elenco degli ACC veri (<c>IStationResolver</c>),
    /// e nessun segmento riservato somiglia a un codice ACC. Filtrare qui vorrebbe dire tenere aggiornato un
    /// secondo elenco di parole riservate accanto alle rotte — cioè la stessa promessa che si è già rotta una
    /// volta.
    /// </para>
    /// </summary>
    public static string? AccFrom(string? absolutePath)
    {
        if (string.IsNullOrEmpty(absolutePath)) return null;

        var segments = absolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length <= PrefixSegments.Length) return null;

        for (var i = 0; i < PrefixSegments.Length; i++)
            if (!segments[i].Equals(PrefixSegments[i], StringComparison.OrdinalIgnoreCase))
                return null;

        return segments[PrefixSegments.Length];
    }
}

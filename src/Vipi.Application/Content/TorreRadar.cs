namespace Vipi.Application.Content;

/// <summary>
/// La torre radar di uno scalo, in forma editoriale (18 ottobre 2026, committente). <b>Zero o una</b>: l'assenza
/// (<c>null</c> dove la si porta) è «nessuno l'ha detto», e non vale «senza radar».
/// </summary>
/// <param name="Equipped">La torre è dotata di radar.</param>
/// <param name="Technique">La tecnica operativa dello scalo, testo a righe. Si mostra solo con
/// <paramref name="Equipped"/> vero, ma resta anche quando è falso: chi sbaglia il sì/no non perde il testo.</param>
public sealed record RadarTowerRow(bool Equipped, string? Technique);

/// <summary>
/// La sezione «Torre radar» e la sua figlia «Tecnica operativa», nella forma che la release congela.
/// <para>⚠️ <b>Una vista per le due sezioni</b>, congelata una volta sola sotto <see cref="SectionKeys.RadarTower"/>:
/// la figlia non ha una derivazione sua, come le soglie delle piste (<see cref="SectionKeys.RunwayThresholds"/>). Con
/// due catture il sì/no e la tecnica potrebbero venire da due cicli diversi, uno sotto l'altro.</para>
/// </summary>
/// <param name="Equipped"><c>null</c> = non indicato.</param>
/// <param name="Technique">La tecnica operativa; vuota = non ancora scritta.</param>
public sealed record AirportRadarTowerView(bool? Equipped, string? Technique)
{
    public static AirportRadarTowerView Empty { get; } = new(null, null);
}

/// <summary>
/// Le regole della sezione «Torre radar» che non dipendono da chi la disegna: la stessa risposta per la vIPI
/// d'aeroporto e per il vSOP militare, che la leggono dallo stesso dato d'anagrafica.
/// </summary>
public static class TorreRadar
{
    /// <summary>
    /// Le sezioni da mostrare: senza una torre dotata di radar la «Tecnica operativa» <b>non c'è</b> — né nel corpo
    /// né nell'indice. Il resto, sotto-sezioni libere comprese, resta com'è.
    ///
    /// <para>⚠️ Si toglie dalla LISTA che il lettore riceve, e non si nasconde nel corpo: la pagina rende l'indice
    /// dalla stessa lista, e un titolo che resta nell'indice e manca nel corpo è un collegamento che non porta da
    /// nessuna parte.</para>
    ///
    /// <para>⚠️ Anche quando il dato <b>non è indicato</b>: la tecnica di una torre di cui non si sa se ha il radar
    /// sarebbe una risposta a una domanda che il documento non ha ancora fatto.</para>
    /// </summary>
    public static IReadOnlyList<SectionView> Filtra(IReadOnlyList<SectionView> sezioni, AirportRadarTowerView vista)
    {
        if (vista.Equipped == true || !Contiene(sezioni)) return sezioni;
        return Senza(sezioni);
    }

    /// <summary>
    /// L'ancora della sezione «Radar di torre» da aprire dalla vista live (committente, 18 ottobre 2026): solo se la
    /// torre è dotata di radar E la sezione si vede nel documento pubblicato. <c>null</c> = niente da segnalare.
    ///
    /// <para>⚠️ Anche la sezione deve essere visibile, non basta il dato: la sezione nasce nascosta e si mostra scalo
    /// per scalo quando è sistemata, e un collegamento verso una sezione nascosta atterrerebbe in cima a una pagina
    /// che non la contiene. Nascosta vale anche se lo è una sua madre (nel vSOP sta dentro i Dati generali).</para>
    ///
    /// <para>⚠️ Il sì/no viene dalla vista DEL DOCUMENTO (<paramref name="vista"/>, congelata con la release), non
    /// dall'anagrafica viva: la marca dice quel che il documento che si apre dice.</para>
    /// </summary>
    public static string? Ancora(IReadOnlyList<SectionView> sezioni, AirportRadarTowerView vista) =>
        vista.Equipped == true ? Visibile(sezioni)?.Id : null;

    private static SectionView? Visibile(IReadOnlyList<SectionView> sezioni)
    {
        foreach (var s in sezioni)
        {
            if (s.IsHidden) continue;   // via lei e tutto quel che ha sotto
            if (string.Equals(s.SectionKey, SectionKeys.RadarTower, StringComparison.OrdinalIgnoreCase)) return s;
            if (Visibile(s.Children) is { } sotto) return sotto;
        }
        return null;
    }

    private static bool Contiene(IReadOnlyList<SectionView> sezioni) =>
        sezioni.Any(s => string.Equals(s.SectionKey, SectionKeys.RadarTowerTechnique, StringComparison.OrdinalIgnoreCase)
                         || Contiene(s.Children));

    private static IReadOnlyList<SectionView> Senza(IReadOnlyList<SectionView> sezioni) =>
        sezioni
            .Where(s => !string.Equals(s.SectionKey, SectionKeys.RadarTowerTechnique, StringComparison.OrdinalIgnoreCase))
            .Select(s => Contiene(s.Children) ? AudienceFilter.ConFigli(s, Senza(s.Children)) : s)
            .ToList();
}

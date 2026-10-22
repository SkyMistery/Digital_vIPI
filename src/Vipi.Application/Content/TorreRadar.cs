using System.Text.Json;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// La torre radar di uno scalo, in forma editoriale (18 ottobre 2026, committente). <b>Zero o una</b>: l'assenza
/// (<c>null</c> dove la si porta) è «nessuno l'ha detto», e non vale «senza radar».
/// </summary>
/// <param name="Equipped">La torre è dotata di radar.</param>
/// <remarks>
/// ⚠️ Dal 21 ottobre 2026 la riga è il solo sì/no: la tecnica operativa è una sezione a blocchi del documento
/// (committente: «non ci deve essere un paragrafo fisso», solo paragrafi, callout… come nel resto).
/// </remarks>
public sealed record RadarTowerRow(bool Equipped);

/// <summary>
/// La sezione «Radar di torre», nella forma che la release congela.
/// </summary>
/// <param name="Equipped"><c>null</c> = non indicato.</param>
/// <param name="Technique">
/// ⚠️ <b>Solo nelle release scattate prima del 21 ottobre 2026</b>, quando la tecnica operativa era un testo a righe
/// dell'anagrafica congelato qui. Da allora è una sezione a blocchi e questo campo nasce sempre vuoto: resta perché
/// quelle release si leggano come sono uscite (<see cref="TorreRadar.Filtra"/> ne fa un paragrafo).
/// </param>
/// <param name="Comune">
/// Il <b>testo comune</b> a tutte le torri radar (S127, <see cref="TestoComune"/>), come era quando la vista è stata
/// fatta: congelato con la release, letto adesso nella bozza. <c>null</c> nelle release scattate prima del 22 ottobre
/// 2026, che non lo mostrano — lo mostrerà la pubblicazione seguente.
/// </param>
public sealed record AirportRadarTowerView(bool? Equipped, string? Technique = null,
    IReadOnlyList<BlockView>? Comune = null)
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
    ///
    /// <para>⚠️ E con la torre radar, su una release scattata prima del 21 ottobre 2026, la tecnica di allora: il
    /// testo a righe congelato nella vista (<see cref="AirportRadarTowerView.Technique"/>) diventa un paragrafo della
    /// sezione, se la sezione non ha blocchi scritti. In quelle release la sezione porta solo il segnaposto delle
    /// sezioni rese dalla pagina, e senza questo passo la pagina pubblica perderebbe il testo fino alla
    /// pubblicazione seguente.</para>
    /// </summary>
    public static IReadOnlyList<SectionView> Filtra(IReadOnlyList<SectionView> sezioni, AirportRadarTowerView vista)
    {
        if (!Contiene(sezioni)) return sezioni;
        if (vista.Equipped != true) return Senza(sezioni);
        return string.IsNullOrWhiteSpace(vista.Technique) ? sezioni : ConTecnicaDiPrima(sezioni, vista.Technique.Trim());
    }

    /// <summary>
    /// Le sezioni con il <b>testo comune</b> delle torri radar in testa alla «Tecnica operativa» (S127). Si chiama
    /// <b>prima della traduzione</b>: i blocchi comuni sono blocchi della sezione come gli altri, e la pagina inglese
    /// li traduce col resto. Chi decide se la sezione si vede resta <see cref="Filtra"/>, dopo.
    ///
    /// <para>⚠️ Il testo è quello della vista <b>derivata</b>, quindi congelato con la release: una pagina pubblica
    /// mostra quello della sua pubblicazione, la bozza quello di adesso.</para>
    /// </summary>
    public static IReadOnlyList<SectionView> ConTestoComune(IReadOnlyList<SectionView> sezioni, AirportRadarTowerView vista) =>
        vista.Comune is not { Count: > 0 } comune || !Contiene(sezioni) ? sezioni : ConComune(sezioni, comune);

    private static IReadOnlyList<SectionView> ConComune(IReadOnlyList<SectionView> sezioni, IReadOnlyList<BlockView> comune) =>
        sezioni.Select(s =>
        {
            var figlie = Contiene(s.Children) ? ConComune(s.Children, comune) : s.Children;
            if (!Tecnica(s)) return ReferenceEquals(figlie, s.Children) ? s : AudienceFilter.ConFigli(s, figlie);
            return new SectionView
            {
                Id = s.Id, Title = s.Title, Depth = s.Depth, SectionKey = s.SectionKey, IsHidden = s.IsHidden,
                BeforeParentBody = s.BeforeParentBody, BodyPosition = s.BodyPosition, LeadSentence = s.LeadSentence,
                Audience = s.Audience, Children = figlie,
                Blocks = comune.Concat(s.Blocks).ToList(),
            };
        }).ToList();

    /// <summary>
    /// Il testo comune (S127) che una release <b>mostra</b>: quello congelato nella sua vista «Radar di torre», e se il
    /// lettore lo vede davvero — torre con radar <b>sì</b>, sezione «Radar di torre» e sua «Tecnica operativa» non
    /// nascoste. È la domanda della riga «Da rivedere» <see cref="ImpactKind.CommonTextChanged"/>: dove il lettore non
    /// lo vede, un testo comune cambiato non è un documento da ripubblicare.
    /// </summary>
    /// <returns><c>Mostrato</c> falso se il lettore non lo vede; <c>Comune</c> null se la release non l'ha congelato
    /// (scattata prima del 22 ottobre 2026).</returns>
    public static (bool Mostrato, IReadOnlyList<BlockView>? Comune) TestoComuneDellaRelease(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson)) return (false, null);
        DocReleasePayload? p;
        try { p = JsonSerializer.Deserialize<DocReleasePayload>(payloadJson); }
        catch (JsonException) { return (false, null); }
        if (p?.Doc?.Roots is null || RadarVisibile(p.Doc.Roots) is not { } radar) return (false, null);
        if (!radar.Children.Any(c => !c.IsHidden
                && string.Equals(c.SectionKey, SectionKeys.RadarTowerTechnique, StringComparison.OrdinalIgnoreCase)))
            return (false, null);
        if (!p.FrozenSections.TryGetValue(radar.Id, out var json)) return (false, null);

        AirportRadarTowerView? vista;
        try { vista = JsonSerializer.Deserialize<AirportRadarTowerView>(json); }
        catch (JsonException) { return (false, null); }
        return (vista?.Equipped == true, vista?.Comune);
    }

    /// <summary>La sezione «Radar di torre» se si vede: nascosta vale anche se lo è una sua madre.</summary>
    private static RawSection? RadarVisibile(IReadOnlyList<RawSection> sezioni)
    {
        foreach (var s in sezioni)
        {
            if (s.IsHidden) continue;
            if (string.Equals(s.SectionKey, SectionKeys.RadarTower, StringComparison.OrdinalIgnoreCase)) return s;
            if (RadarVisibile(s.Children) is { } sotto) return sotto;
        }
        return null;
    }

    private static bool Tecnica(SectionView s) =>
        string.Equals(s.SectionKey, SectionKeys.RadarTowerTechnique, StringComparison.OrdinalIgnoreCase);

    /// <summary>Vero se il blocco porta qualcosa: il segnaposto (né testo né JSON) non conta.</summary>
    private static bool Scritto(BlockView b) =>
        !string.IsNullOrWhiteSpace(b.Body) || !string.IsNullOrWhiteSpace(b.BodyJson);

    private static IReadOnlyList<SectionView> ConTecnicaDiPrima(IReadOnlyList<SectionView> sezioni, string testo) =>
        sezioni.Select(s =>
        {
            var figlie = Contiene(s.Children) ? ConTecnicaDiPrima(s.Children, testo) : s.Children;
            if (!Tecnica(s) || s.Blocks.Any(Scritto))
                return ReferenceEquals(figlie, s.Children) ? s : AudienceFilter.ConFigli(s, figlie);
            return new SectionView
            {
                Id = s.Id, Title = s.Title, Depth = s.Depth, SectionKey = s.SectionKey, IsHidden = s.IsHidden,
                BeforeParentBody = s.BeforeParentBody, BodyPosition = s.BodyPosition, LeadSentence = s.LeadSentence,
                Audience = s.Audience, Children = figlie,
                // Un id che nessun blocco vero ha (sono positivi): il paragrafo non esiste nel database.
                Blocks = new[] { new BlockView { Id = -1, Order = 1, Format = BlockFormat.Prose, State = RenderState.Expanded, Body = testo } },
            };
        }).ToList();

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

namespace Vipi.Application.Content;

/// <summary>
/// Come si legge un documento d'aeroporto scritto PRIMA della carta 2026-08-26, quando era una proiezione
/// <b>cotta</b>: le sezioni si riconoscevano per TITOLO e nascevano con una chiave <c>custom:{guid}</c> nuova a
/// ogni rigenerazione (<c>BlockSection.Airport</c> non ha una chiave di catalogo, e il builder ricadeva su
/// <see cref="SectionKeys.NewCustom"/>).
/// <para>
/// Serve a DUE lettori, e per questo la mappa sta qui e non dentro uno dei due:
/// </para>
/// <list type="number">
///   <item>la riconciliazione d'avvio, che riscrive i <b>documenti di lavoro</b> una volta per tutte;</item>
///   <item>il <b>viewer</b>, che deve leggere anche gli <b>snapshot di release</b> — e quelli non si riscrivono
///     mai (regola del doc 13 §9: le release già pubblicate non si toccano).</item>
/// </list>
/// <para>
/// ⚠️ Senza il secondo lettore la pagina pubblica di ogni aeroporto non ancora ripubblicato perdeva le tabelle
/// vere — piste, quote di transizione e regole tornavano tabelle generiche — e il <b>meteo spariva del tutto</b>,
/// perché nello snapshot vecchio quella sezione non esiste: prima della carta il riquadro METAR/TAF lo disegnava
/// la pagina <i>fuori</i> dal documento, quindi c'era sempre.
/// </para>
/// </summary>
public static class AirportLegacySections
{
    /// <summary>
    /// Titolo cotto → chiave di catalogo. Include i titoli inglesi correnti e quelli italiani legacy, perché fino
    /// all'i18n il documento nasceva in italiano.
    /// <para>⚠️ Il titolo si riconosce <b>INTERO</b>: col confronto per sottostringa «Runways» e «Runway rules»
    /// si scambierebbero, e quale delle due vince dipenderebbe dall'ordine di iterazione.</para>
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> KeyByCookedTitle =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Runway rules"] = "runwayrules",
            ["Regole piste"] = "runwayrules",
            ["Configurazioni pista"] = "runwayrules",
            ["Transition levels"] = "transition",
            ["Quote di transizione"] = "transition",
            ["Quote transizione"] = "transition",
            ["Runways"] = "runways",
            ["Piste"] = "runways",
            ["Frequencies"] = "frequencies",
            ["Frequenze"] = "frequencies",
            ["SID"] = "sids",
        };

    /// <summary>Chiave che il documento cotto dava a TUTTE le sezioni editoriali libere: una sola, quindi
    /// indistinguibili — «nascondi» ne avrebbe nascosta una a caso.</summary>
    public const string ExtraKey = "airportextra";

    /// <summary>Chiave di catalogo per un titolo cotto, o <c>null</c> se quel titolo non è di una sezione cotta.</summary>
    public static string? KeyForCookedTitle(string? title) =>
        title is not null && KeyByCookedTitle.TryGetValue(title.Trim(), out var k) ? k : null;

    /// <summary>
    /// Le sezioni da RENDERE per un documento d'aeroporto: quelle che il documento porta — con le cotte riportate
    /// alla loro chiave e al loro titolo di catalogo — più le sezioni <b>sempre live</b> che mancano.
    /// <para>
    /// ⚠️ Una sezione sempre live (il meteo) non è mai parte della verità di uno snapshot: non si congela, non ha
    /// contenuto salvato, e la sua assenza da un documento vecchio significa solo che quel documento è stato
    /// scritto prima che esistesse. Va mostrata lo stesso, al posto che il catalogo le dà.
    /// </para>
    /// <para>
    /// ⚠️ Le sezioni cotte <b>perdono i loro blocchi</b>: da qui in poi il corpo lo produce la pagina. Le tabelle
    /// che quello snapshot si porta dietro sono le stesse che la derivazione ricalcola dalle tabelle del profilo —
    /// e su una release anteriore alla carta un contenuto congelato per quelle chiavi non esiste, quindi la
    /// derivazione ricade su live. È lo stesso comportamento che la pagina aveva prima: piste, frequenze e
    /// sezioni libere le leggeva <b>dal profilo</b>, non dal documento pubblicato.
    /// </para>
    /// </summary>
    public static IReadOnlyList<SectionView> ForView(IReadOnlyList<SectionView>? sections)
    {
        var risultato = new List<SectionView>();
        var presenti = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var s in sections ?? Array.Empty<SectionView>())
        {
            var cotta = SectionKeys.IsCustom(s.SectionKey) ? KeyForCookedTitle(s.Title) : null;
            var chiave = s.SectionKey;

            if (cotta is not null && presenti.Add(cotta)) chiave = cotta;
            else if (!SectionKeys.IsCustom(s.SectionKey)) presenti.Add(s.SectionKey);

            if (SectionCatalog.Find(SectionProfile.Airport, chiave) is not { } desc)
            {
                // Sezione libera (o chiave che non conosciamo): resta esattamente com'è, blocchi compresi.
                risultato.Add(s);
                continue;
            }

            // ⚠️ Il titolo lo decide il CATALOGO anche quando la chiave era già giusta: «Frequencies» e «SID»
            // non passano dal riconoscimento per titolo, e senza questo ramo il documento resterebbe metà in
            // italiano e metà in inglese. Una sezione fissa non si rinomina a mano, quindi non c'è nessuna
            // scelta editoriale da rispettare.
            risultato.Add(new SectionView
            {
                Id = s.Id,
                Title = desc.Title,
                Depth = s.Depth,
                SectionKey = chiave,
                IsHidden = s.IsHidden,
                BeforeParentBody = s.BeforeParentBody,
                LeadSentence = s.LeadSentence,
                // Il corpo di una sezione di catalogo lo produce la pagina: i blocchi cotti se ne vanno, o si
                // vedrebbe la tabella DUE volte (quella dello snapshot e quella derivata).
                Blocks = desc.BodySource == SectionBodySource.Host ? Array.Empty<BlockView>() : s.Blocks,
                Children = s.Children,
            });
        }

        foreach (var d in SectionCatalog.For(SectionProfile.Airport)
                     .Where(d => SectionCatalog.IsAlwaysLive(d.Key) && !presenti.Contains(d.Key))
                     .OrderBy(d => d.Order))
        {
            // Prima della prima sezione di catalogo che nel catalogo viene DOPO di lei; in coda se non ce n'è.
            var at = risultato.FindIndex(x =>
                SectionCatalog.Find(SectionProfile.Airport, x.SectionKey) is { } f && f.Order > d.Order);
            risultato.Insert(at < 0 ? risultato.Count : at, new SectionView
            {
                // Ancora stabile e senza collisioni: gli id veri sono «s-{numero}».
                Id = $"s-{d.Key}",
                Title = d.Title,
                Depth = 0,
                SectionKey = d.Key,
                Blocks = Array.Empty<BlockView>(),
                Children = Array.Empty<SectionView>(),
            });
        }

        return risultato;
    }
}

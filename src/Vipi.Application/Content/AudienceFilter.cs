using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Il filtro di lettura pilota / ATC (carta <c>docs/feature/2026-08-27-vsop-militari.md</c> §3).
///
/// <para>
/// ⚠️ <b>Non è controllo d'accesso.</b> Il documento è pubblico e la vista ATC la apre chiunque cambi
/// l'indirizzo. Chi scrive dentro una sezione marcata deve saperlo: qui si decide che cosa <i>conviene</i>
/// mostrare, non che cosa si <i>può</i> vedere.
/// </para>
///
/// <para>
/// ⚠️ <b>Le sezioni «per tutti» non si filtrano mai.</b> È la scelta che rende utile la funzione invece di
/// dannosa: il contenuto davvero di uno solo dei due è poco, e nascondere a un pilota il contesto ATC lo
/// farebbe leggere <i>peggio</i>, non meglio. Il filtro toglie solo ciò che qualcuno ha marcato
/// esplicitamente per l'altro.
/// </para>
/// </summary>
public static class AudienceFilter
{
    /// <summary>Il valore di <c>?vista=</c> per la vista pilota.</summary>
    public const string QueryPilota = "pilota";

    /// <summary>Il valore di <c>?vista=</c> per la vista controllore.</summary>
    public const string QueryAtc = "atc";

    /// <summary>
    /// Legge <c>?vista=</c>. Qualunque cosa non riconosciuta — compreso il parametro assente — è «tutto»,
    /// che è la risposta giusta: un indirizzo storpiato deve mostrare il documento intero, non una vista a
    /// caso né una pagina di errore.
    /// </summary>
    public static SectionAudience? Leggi(string? vista) => vista?.Trim().ToLowerInvariant() switch
    {
        QueryPilota => SectionAudience.Pilots,
        QueryAtc => SectionAudience.Controllers,
        _ => null,
    };

    /// <summary>Il valore da mettere in <c>?vista=</c> per questa vista; null = «tutto», cioè niente parametro.</summary>
    public static string? Query(SectionAudience? vista) => vista switch
    {
        SectionAudience.Pilots => QueryPilota,
        SectionAudience.Controllers => QueryAtc,
        _ => null,
    };

    /// <summary>
    /// Le sezioni che questo lettore deve vedere.
    ///
    /// <para>
    /// ⚠️ <b>Una sezione filtrata via porta con sé i suoi FIGLI</b>, e non è un dettaglio d'implementazione:
    /// una sotto-sezione ATC dentro un capitolo ATC sparisce già col padre, ma una sotto-sezione «per
    /// tutti» dentro un capitolo ATC <b>non deve restare orfana</b> in mezzo alla pagina, senza più il
    /// titolo che le dava senso.
    /// </para>
    /// </summary>
    public static IReadOnlyList<SectionView> Filtra(IReadOnlyList<SectionView> sezioni, SectionAudience? vista)
    {
        if (vista is null) return sezioni;   // «tutto»: nessuna copia, nessun lavoro

        var tenute = new List<SectionView>();
        foreach (var s in sezioni)
        {
            if (!Passa(s.Audience, vista.Value)) continue;   // via lei e tutto il suo sottoalbero
            tenute.Add(s.Children.Count == 0 ? s : ConFigli(s, Filtra(s.Children, vista)));
        }
        return tenute;
    }

    /// <summary>
    /// Vero se il documento ha <b>almeno una</b> sezione marcata: solo allora la chip ha senso.
    /// <para>Senza questa domanda, su ogni documento italiano comparirebbe un selettore che non filtra
    /// niente — rumore su tutte le pagine per una funzione che ne riguarda poche.</para>
    /// </summary>
    public static bool HaSezioniMarcate(IReadOnlyList<SectionView> sezioni) =>
        sezioni.Any(s => s.Audience != SectionAudience.Both || HaSezioniMarcate(s.Children));

    /// <summary>
    /// Se una sezione con questo destinatario si mostra a chi guarda in questa vista; <c>vista</c> nulla
    /// («tutto») mostra sempre.
    /// <para>
    /// ⚠️ È pubblica per la vIPI <b>ACC</b>, che non passa da <see cref="Filtra"/>: è l'unica famiglia a
    /// blocchi, il suo ciclo esterno itera i blocchi e le sue sezioni sono <c>AccBlockSection</c>, non
    /// <see cref="SectionView"/>. Ha comunque bisogno della <b>stessa</b> regola — riscriverla lì sarebbe la
    /// quinta copia di una condizione di tre righe, e la prima a divergere.
    /// </para>
    /// </summary>
    public static bool Mostra(SectionAudience sezione, SectionAudience? vista) =>
        vista is null || sezione == SectionAudience.Both || sezione == vista.Value;

    /// <summary>
    /// La stessa sezione con i soli FIGLI che questo lettore deve vedere. Serve a chi filtra la radice per
    /// conto suo (la vIPI ACC) ma vuole la regola sui figli senza riscriverla.
    /// </summary>
    public static SectionView FiltraFigli(SectionView s, SectionAudience? vista) =>
        vista is null || s.Children.Count == 0 ? s : ConFigli(s, Filtra(s.Children, vista));

    /// <summary>Se una sezione con questo destinatario si mostra a chi guarda in questa vista.</summary>
    private static bool Passa(SectionAudience sezione, SectionAudience vista) =>
        sezione == SectionAudience.Both || sezione == vista;

    private static SectionView ConFigli(SectionView s, IReadOnlyList<SectionView> figli) => new()
    {
        Id = s.Id,
        Title = s.Title,
        Depth = s.Depth,
        SectionKey = s.SectionKey,
        IsHidden = s.IsHidden,
        BeforeParentBody = s.BeforeParentBody,
        LeadSentence = s.LeadSentence,
        Audience = s.Audience,
        Blocks = s.Blocks,
        Children = figli,
    };
}

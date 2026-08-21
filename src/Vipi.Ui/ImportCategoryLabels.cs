using Microsoft.Extensions.Localization;
using Vipi.Domain;

namespace Vipi.Ui;

/// <summary>
/// Come si chiamano, a video, le categorie della policy di import — <b>un solo</b> vocabolario.
///
/// <para><b>Perché condiviso.</b> Le stesse cinque cose comparivano con due nomi nella <i>stessa</i>
/// schermata: la tabella della policy diceva «Settori», quella degli stati diceva <c>AirportSector</c>. E il
/// registro di audit, che dal 22 agosto 2026 racconta anche i cambi di policy, aggiungeva un terzo nome se
/// se lo scriveva per conto proprio. Un formattatore per tipo di dato, non uno per pagina — come
/// <see cref="AuditNarrator"/> per gli eventi.</para>
/// </summary>
public static class ImportCategoryLabels
{
    /// <summary>Etichetta della categoria (l'unica che l'utente deve leggere).</summary>
    public static string Etichetta(ImportCategory c, IStringLocalizer L) => L[Chiave(c)].Value;

    /// <summary>Come sopra, partendo dal nome scritto nei dettagli di audit o nella chiave di
    /// <c>ImportState</c>. Ignota (o vocabolario più recente del lettore) ⇒ si mostra il nome grezzo:
    /// mai una riga vuota al posto di un fatto.</summary>
    public static string Etichetta(string nome, IStringLocalizer L) =>
        Categoria(nome) is ImportCategory c ? Etichetta(c, L) : nome;

    /// <summary>
    /// La categoria dietro un nome. ⚠️ Accetta anche le chiavi di <c>ImportCategories</c>, che <b>non</b>
    /// coincidono con quelle di <see cref="ImportCategory"/>: gli stati periodici si chiamano
    /// <c>AirportSector</c>, <c>SpecialArea</c> e <c>Sid</c> al singolare. Sono due enumerazioni nate in
    /// momenti diversi per due scopi diversi (cosa si importa / cosa si è importato), e a video sono la
    /// stessa riga.
    /// </summary>
    public static ImportCategory? Categoria(string? nome) => (nome ?? "").Trim().ToLowerInvariant() switch
    {
        "transitionaltitude" => ImportCategory.TransitionAltitude,
        "runways" or "runway" => ImportCategory.Runways,
        "sectors" or "airportsector" => ImportCategory.Sectors,
        "sids" or "sid" => ImportCategory.Sids,
        "specialareas" or "specialarea" => ImportCategory.SpecialAreas,
        _ => null,
    };

    private static string Chiave(ImportCategory c) => c switch
    {
        ImportCategory.TransitionAltitude => "Sorg_TaLabel",
        ImportCategory.Runways => "Sorg_RunwaysLabel",
        ImportCategory.Sectors => "Sorg_SectorsLabel",
        ImportCategory.Sids => "Sorg_SidLabel",
        _ => "Sorg_SpecialAreasLabel",
    };
}

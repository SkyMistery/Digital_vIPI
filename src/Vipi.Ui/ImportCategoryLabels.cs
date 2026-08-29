using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Ui;

/// <summary>
/// Come si legge <b>una riga intera</b> della pagina Sorgenti: il nome, la frase che dice quali colonne la
/// sorgente possiede, e dove si va a toccare quel dato.
/// </summary>
/// <param name="Dove">I posti da cui l'import si lancia a mano. Mai vuoto: una riga senza un dove è una
/// riga che dice «esiste» e non «vai qui».</param>
public sealed record ImportRowText(string Nome, string Descrizione, IReadOnlyList<(string Testo, string Href)> Dove);

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

    /// <summary>
    /// Nome, descrizione e link di una riga della pagina Sorgenti, in <b>una</b> tabella.
    ///
    /// <para><b>Perché tutt'e tre insieme.</b> Erano tre <c>switch</c> sulla stessa categoria dentro la
    /// pagina, ognuno col suo ramo <c>_ =></c> che voleva dire «l'anagrafica ACC». Con la seconda anagrafica
    /// — quella degli aeroporti — i tre rami sarebbero diventati tre posti da cui dire la stessa cosa, e il
    /// modo in cui due elenchi della stessa cosa divergono è esattamente questo. Aggiungere una riga adesso
    /// è aggiungere <b>un</b> caso qui.</para>
    /// </summary>
    public static ImportRowText Riga(ImportOverviewRow r, IStringLocalizer L)
    {
        var aeroporti = (L["Struct_NavAirports"].Value, "/services/vsop/admin/airports");
        var struttura = (L["Struct_Title"].Value, "/services/vsop/admin/sector-structure");
        var acc = (L["Struct_NavAcc"].Value, "/services/vsop/admin/acc");

        return r.Categoria switch
        {
            ImportCategory.TransitionAltitude => new(L["Sorg_TaLabel"].Value, L["Sorg_TaDesc"].Value, new[] { aeroporti }),
            ImportCategory.Runways => new(L["Sorg_RunwaysLabel"].Value, L["Sorg_RunwaysDesc"].Value, new[] { aeroporti }),
            ImportCategory.Sectors => new(L["Sorg_SectorsLabel"].Value, L["Sorg_SectorsDesc"].Value, new[] { struttura, aeroporti }),
            ImportCategory.Sids => new(L["Sorg_SidLabel"].Value, L["Sorg_SidDesc"].Value, new[] { aeroporti }),
            ImportCategory.SpecialAreas => new(L["Sorg_SpecialAreasLabel"].Value, L["Sorg_SpecialAreasDesc"].Value, new[] { acc }),
            ImportCategory.Navaids => new(L["Sorg_NavaidsLabel"].Value, L["Sorg_NavaidsDesc"].Value, Array.Empty<(string, string)>()),
            ImportCategory.AtcSessions => new(L["Sorg_AtcStatsLabel"].Value, L["Sorg_AtcStatsDesc"].Value, Array.Empty<(string, string)>()),
            _ => r.Anagrafica switch
            {
                ImportAnagrafica.Aeroporti => new(L["Sorg_AptLabel"].Value, L["Sorg_AptDesc"].Value, new[] { aeroporti }),
                _ => new(L["Sorg_AccLabel"].Value, L["Sorg_AccDesc"].Value, new[] { acc }),
            },
        };
    }

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
        "atcsessions" or "atchistory" => ImportCategory.AtcSessions,
        "navaids" or "navaid" => ImportCategory.Navaids,
        _ => null,
    };

    private static string Chiave(ImportCategory c) => c switch
    {
        ImportCategory.TransitionAltitude => "Sorg_TaLabel",
        ImportCategory.Runways => "Sorg_RunwaysLabel",
        ImportCategory.Sectors => "Sorg_SectorsLabel",
        ImportCategory.Sids => "Sorg_SidLabel",
        ImportCategory.Navaids => "Sorg_NavaidsLabel",
        _ => "Sorg_SpecialAreasLabel",
    };
}

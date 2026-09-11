using Vipi.Domain;

namespace Vipi.Ui.Components;

/// <summary>
/// I testi di una <see cref="AirportCategory"/>: l'etichetta, la spiegazione e il colore della pastiglia. Stanno
/// in un posto solo perché li usano l'etichetta dei documenti (<c>MilitaryTag</c>) e il comando della pagina
/// Aeroporti (<c>AirportCategoryPicker</c>), e due elenchi di etichette per la stessa cosa prima o poi dicono
/// due cose diverse.
///
/// <para>⚠️ Chiavi scritte per intero e non composte col nome del valore (<c>"Apt_Cat_" + c</c>): una chiave
/// costruita a pezzi non la trova nessuno cercandola, e sparisce dagli strumenti che contano le chiavi usate.</para>
/// </summary>
public static class AirportCategoryText
{
    /// <summary>La chiave dell'etichetta breve, quella scritta sulla pastiglia.</summary>
    public static string Key(AirportCategory c) => c switch
    {
        AirportCategory.MilitaryOnly => "Apt_Cat_MilitaryOnly",
        AirportCategory.CivilWithMilitaryPresence => "Apt_Cat_CivilWithMilitaryPresence",
        AirportCategory.MilitaryWithCivilPresence => "Apt_Cat_MilitaryWithCivilPresence",
        _ => "Apt_Cat_Civil",
    };

    /// <summary>La chiave della spiegazione: che cosa vuol dire, quali documenti ammette e chi la decide.</summary>
    public static string TitleKey(AirportCategory c) => c switch
    {
        AirportCategory.MilitaryOnly => "Apt_CatTitle_MilitaryOnly",
        AirportCategory.CivilWithMilitaryPresence => "Apt_CatTitle_CivilWithMilitaryPresence",
        AirportCategory.MilitaryWithCivilPresence => "Apt_CatTitle_MilitaryWithCivilPresence",
        _ => "Apt_CatTitle_Civil",
    };

    /// <summary>Il colore della pastiglia: ambra dove c'è solo il militare, blu dove convivono, neutro altrove.</summary>
    public static string PillClass(AirportCategory c) => c switch
    {
        AirportCategory.MilitaryOnly => "amber",
        AirportCategory.MilitaryWithCivilPresence => "blue",
        _ => "neutral",
    };
}

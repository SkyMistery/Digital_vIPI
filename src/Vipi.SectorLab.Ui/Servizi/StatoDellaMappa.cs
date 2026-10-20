using Vipi.SectorLab.Core.Mappa;

namespace Vipi.SectorLab.Ui.Servizi;

/// <summary>
/// Gli spazi di lavoro del Lab (riordino, terza forma, D14): ognuno ha la sua mappa sullo stesso motore (D15).
/// </summary>
public enum SpazioDiLavoro
{
    /// <summary>L'AIP espanso: l'Indice come l'AIP, la sua mappa, la scheda della voce.</summary>
    Aip,
    /// <summary>Il sector: Mappa, Scheda, Problemi, Consegna.</summary>
    Sector,
    /// <summary>I confronti fra AIP espanso e sector, coi due lati sovrapposti sulla stessa mappa (D18).</summary>
    Confronti,
}

/// <summary>
/// Lo stato di una mappa (riordino R4, D15): gli strati accesi, la vista, le voci e i generi spenti. Uno per spazio di
/// lavoro, così cambiando spazio ogni mappa resta com'era. Lo tiene <see cref="SessioneDelLab"/>, che lo cambia; le
/// versioni crescono a ogni cambio, e la mappa confronta le sue.
/// </summary>
public sealed class StatoDellaMappa
{
    public StatoDellaMappa(SpazioDiLavoro spazio)
    {
        Spazio = spazio;
        Accesi.Add(StratiDellaMappa.Sfondo.Id);
    }

    public SpazioDiLavoro Spazio { get; }

    /// <summary>Gli strati accesi: lo sfondo lo è sempre (le coste: senza, non si capisce dove si è).</summary>
    internal HashSet<string> Accesi { get; } = new(StringComparer.Ordinal);

    /// <summary>La vista: solo questi elementi sulla mappa (<c>file#3</c>, <c>file#*</c>), e le coste.</summary>
    internal List<string> InVista { get; } = [];

    /// <summary>Le voci, le parti e i generi spenti (<c>file#3</c>, <c>file#3.1</c>, <c>§geo:TAXI_CENTER</c>).</summary>
    internal HashSet<string> Spenti { get; } = new(StringComparer.Ordinal);

    public int VersioneDellaVista { get; internal set; }

    public int VersioneDeiSpenti { get; internal set; }

    /// <summary>Una cartella chiusa o aperta: la vista e le voci spente erano dell'albero di prima. Gli strati accesi restano.</summary>
    internal void Riparti()
    {
        InVista.Clear();
        VersioneDellaVista++;
        Spenti.Clear();
        VersioneDeiSpenti++;
    }
}

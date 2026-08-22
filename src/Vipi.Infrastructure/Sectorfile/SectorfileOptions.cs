namespace Vipi.Infrastructure.Sectorfile;

/// <summary>Config del sectorfile Aurora su GitHub (sezione "Sectorfile"). Repo pubblico, raw, no auth.</summary>
public sealed class SectorfileOptions
{
    /// <summary>Base raw (deve terminare con "/"). Vuota = import SID disabilitato.</summary>
    public string RawBaseUrl { get; set; } = "";
    /// <summary>Path del file fix relativo a <see cref="RawBaseUrl"/>.</summary>
    public string FixPath { get; set; } = "NAVAIDS/itfix.fix";
    /// <summary>Path del file VOR relativo a <see cref="RawBaseUrl"/>.</summary>
    public string VorPath { get; set; } = "NAVAIDS/itvor.vor";
    /// <summary>Path del file NDB relativo a <see cref="RawBaseUrl"/>. Assente = 404 = catalogo senza NDB.</summary>
    public string NdbPath { get; set; } = "NAVAIDS/itndb.ndb";
    /// <summary>Path del file poligoni TWR (twrs.tfl) relativo a <see cref="RawBaseUrl"/>.</summary>
    public string TwrShapePath { get; set; } = "DYNAMIC_SEC/twrs.tfl";
    /// <summary>Ogni quante ore rilanciare l'import automatico.</summary>
    public int ImportHours { get; set; } = 24;
}

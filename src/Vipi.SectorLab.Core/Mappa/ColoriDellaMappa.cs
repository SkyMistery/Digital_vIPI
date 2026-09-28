using System.Drawing;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Core.Mappa;

/// <summary>
/// Il colore di una forma come lo disegnerebbe Aurora. <paramref name="Tratto"/> e <paramref name="Riempimento"/> nulli
/// = non si disegnano (<c>clNone</c>, un settore dinamico senza riempimento).
/// </summary>
/// <param name="Origine">Da dove viene il colore della linea, per chi guarda: <c>schema TAXIWAY</c>,
/// <c>colors.def GRASS</c>, <c>#2F2F2F</c>, <c>sconosciuto COAST</c>.</param>
/// <param name="Tratteggio">Lo stile della linea dello schema (<c>ACC_HIGH_SOLID=3</c>…): 0 continua.</param>
public sealed record ColoreDellaForma(Color? Tratto, Color? Riempimento, string Origine, int Tratteggio = 0);

/// <summary>
/// I colori della mappa come in Aurora (lotto «Subito» slice 4, D3): lo schema scelto (<c>.clr</c>, fuori dal sector)
/// e i nomi di <c>[DEFINE]</c> del master (<c>colors.def</c>).
/// <para>Le regole, dal manuale IVAO del sector e dal committente:</para>
/// <list type="bullet">
/// <item>i record senza un colore scritto (fix, aerovie, SID, settori ARTCC, MVA…) prendono la chiave dello schema
/// del loro file (<c>.lairway</c> → <c>AIRWAYLOW</c>) o della loro voce (<c>.str</c>: STAR, IAP, GOAROUND…);</item>
/// <item>nelle linee dei <c>.geo</c> (e delle aree P/R/D) un nome che lo schema colora da sé
/// (<see cref="NomiDeiColoriDelGeo"/>) vince su <c>colors.def</c>: i bordi taxiway sono gialli come lo schema, non
/// grigi come il <c>TAXIWAY</c> di <c>colors.def</c> (committente, 28 settembre); poi <c>colors.def</c>, poi il valore;</item>
/// <item>nelle teste di <c>.tfl</c> e <c>.pol</c> un nome è di <c>colors.def</c> (manuale: «you can use defined colors
/// here»), se no è un valore: lo schema lì non conta (l'orfano <c>limw.pol</c> col suo <c>COAST</c> è sconosciuto);</item>
/// <item>un nome che nessuno conosce si disegna magenta, come <see cref="ColorPalette.Resolve"/>: sulla mappa
/// l'errore si vede.</item>
/// </list>
/// </summary>
public sealed class ColoriDellaMappa
{
    /// <summary>Il colore di un nome che nessuno conosce.</summary>
    public static readonly Color Sconosciuto = Color.Magenta;

    private readonly SchemaDeiColori _schema;
    private readonly ColorPalette _definiti;

    public ColoriDellaMappa(SchemaDeiColori schema, ColorPalette definiti)
    {
        _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        _definiti = definiti ?? throw new ArgumentNullException(nameof(definiti));
    }

    /// <summary>Il fondo dello schermo radar (<c>RADARBACK</c>), se lo schema lo ha.</summary>
    public Color? Sfondo => _schema.TryColore("RADARBACK", out Color? colore) ? colore : null;

    /// <summary>La chiave dello schema di un file, dall'estensione; null se il colore lo scrive il record.</summary>
    public static string? ChiaveDelFile(string relativo)
    {
        ArgumentNullException.ThrowIfNull(relativo);
        return Path.GetExtension(relativo).TrimStart('.').ToLowerInvariant() switch
        {
            "artcc" => "ARTCC",
            "hartcc" => "ARTCCHIGH",
            "lartcc" => "ARTCCLOW",
            "hairway" => "AIRWAYHIGH",
            "lairway" => "AIRWAYLOW",
            "sid" => "SID",
            "str" => "STAR",
            "mva" => "MRVA",
            "fix" => "FIX",
            "vor" => "VOR",
            "ndb" => "NDB",
            "ap" => "AIRPORT",
            "vfi" => "VFR",
            "vrt" => "VFRROUTE",
            "hold" => "HOLDINGS",
            "rw" => "RUNWAY",
            "txi" => "TAXILABELS",
            "gts" => "GATES",
            _ => null,
        };
    }

    /// <summary>Lo stile della linea di una chiave (le impostazioni <c>…_SOLID</c> dello schema).</summary>
    private static readonly Dictionary<string, string> StileDellaChiave = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ARTCC"] = "ACC_SOLID",
        ["ARTCCHIGH"] = "ACC_HIGH_SOLID",
        ["ARTCCLOW"] = "ACC_LOW_SOLID",
        ["MRVA"] = "MRVA_SOLID",
        ["AIRWAYHIGH"] = "AW_HIGH_SOLID",
        ["AIRWAYLOW"] = "AW_LOW_SOLID",
        ["SID"] = "SID_SOLID",
        ["STAR"] = "STAR_SOLID",
        ["IAP"] = "IAP_SOLID",
        ["FAP"] = "FAP_SOLID",
        ["HOLDINGS"] = "HOLD_SOLID",
        ["TRANSITIONS"] = "TRANS_SOLID",
        ["GOAROUND"] = "GOAROUND_SOLID",
    };

    /// <summary>Il colore di una forma.</summary>
    public ColoreDellaForma Di(FormaDellaMappa forma)
    {
        ArgumentNullException.ThrowIfNull(forma);

        string estensione = Path.GetExtension(forma.File).TrimStart('.').ToLowerInvariant();
        switch (estensione)
        {
            case "geo" or "restrict" or "prohibit" or "danger":
            {
                var (colore, origine) = DelNome(forma.Tratto, primaLoSchema: true);
                return new ColoreDellaForma(colore, null, origine);
            }

            case "tfl" or "pol":
            {
                var (tratto, origine) = DelNome(forma.Tratto, primaLoSchema: false);
                Color? riempimento = forma.SoloBordo || string.IsNullOrWhiteSpace(forma.Riempimento)
                    ? null
                    : DelNome(forma.Riempimento, primaLoSchema: false).Colore;
                return new ColoreDellaForma(tratto, riempimento, origine);
            }
        }

        string? chiave = forma.Chiave ?? ChiaveDelFile(forma.File);
        if (chiave is null)
            return new ColoreDellaForma(Sconosciuto, null, "nessuna chiave per ." + estensione);

        if (!_schema.TryColore(chiave, out Color? dalloSchema))
            return new ColoreDellaForma(Sconosciuto, null, "schema senza " + chiave);

        int tratteggio = StileDellaChiave.TryGetValue(chiave, out string? stile)
                         && _schema.Altri.TryGetValue(stile, out string? valore)
                         && int.TryParse(valore, out int n)
            ? n
            : 0;
        return new ColoreDellaForma(dalloSchema, null, "schema " + chiave, tratteggio);
    }

    /// <summary>Un colore scritto nel record: un nome (dello schema o di <c>colors.def</c>) o un valore.</summary>
    private (Color? Colore, string Origine) DelNome(string? scritto, bool primaLoSchema)
    {
        string nome = (scritto ?? string.Empty).Trim();

        if (primaLoSchema && DalloSchema(nome) is { } schema)
            return schema;
        if (_definiti.TryResolve(nome, out var definito))
            return (definito.Value, "colors.def " + definito.Name);
        if (ColoreDelSector.TryLeggi(nome, out Color valore))
            return (valore, ColoreDelSector.Scrivi(valore));

        return (Sconosciuto, nome.Length == 0 ? "sconosciuto (vuoto)" : "sconosciuto " + nome);
    }

    private (Color? Colore, string Origine)? DalloSchema(string nome)
        => NomiDeiColoriDelGeo.Chiave(nome) is { } chiave && _schema.TryColore(chiave, out Color? colore)
            ? (colore, "schema " + chiave)
            : null;
}

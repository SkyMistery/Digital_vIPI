using System.Drawing;
using System.Globalization;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Legge uno schema di colori di Aurora (<c>.clr</c>) in uno <see cref="SchemaDeiColori"/>. Come il <c>.def</c>, non si
/// scrive mai: niente round-trip, niente pezzi (lotto «Subito», slice 4).
/// <para>I valori sono nella notazione di Delphi, e il manuale non la descrive: la regola è <b>misurata</b> sugli
/// schemi del fork (4 in <c>ColorSchemes\</c>, più <c>Default.clr</c> e <c>PAR2090.clr</c>):</para>
/// <list type="bullet">
/// <item><c>$00BBGGRR</c> — byte alto a zero: il <c>TColor</c> di Delphi, rosso nel byte BASSO, opaco;</item>
/// <item><c>$AARRGGBB</c> — byte alto diverso da zero: opacità e poi rosso, verde, blu (i colori «a 32 bit» dei
/// disegni GEO, manuale del sector). La prova sono le coppie dello stesso schema che devono dare lo stesso colore:
/// in <c>ITALY_GND.clr</c> <c>DANGER=$00963CAE</c> e <c>SPEC_DANGER=$FFAE3C96</c>, <c>PROHIBITED=$000064C6</c> e
/// <c>SPEC_PROHIBITED=$FFC66400</c>; in <c>LIRR_RDR_V1.0.clr</c> <c>DANGER=$00F06E90</c> e
/// <c>SPEC_DANGER=$FF906EF0</c>, <c>PROHIBITED=$00006DFF</c> e <c>SPEC_PROHIBITED=$FFFF6D00</c>;</item>
/// <item><c>clWhite</c>, <c>clLime</c>… — i nomi dei colori di Delphi; <c>clNone</c> = non si disegna;</item>
/// <item>il resto (<c>ACC_SOLID=0</c>, <c>VORSYMBOL=«</c>) sono impostazioni, tenute come testo.</item>
/// </list>
/// </summary>
public sealed class ClrParser
{
    private readonly IWarningCollector _warnings;

    public ClrParser(IWarningCollector warnings)
        => _warnings = warnings ?? throw new ArgumentNullException(nameof(warnings));

    public SchemaDeiColori Parse(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        return Parse(SectorFileReader.Read(filePath), filePath);
    }

    /// <summary>Righe già lette (senza disco: per i test).</summary>
    public SchemaDeiColori Parse(FileReadResult read, string source)
    {
        var schema = new SchemaDeiColori();

        for (int i = 0; i < read.Lines.Count; i++)
        {
            string riga = read.Lines[i].Trim();
            if (riga.Length == 0 || riga.StartsWith("//", StringComparison.Ordinal) || riga.StartsWith(';'))
                continue;

            int uguale = riga.IndexOf('=', StringComparison.Ordinal);
            if (uguale <= 0)
            {
                _warnings.Add(WarningSeverity.Warning, WarningCategory.Parser, source,
                    "Riga dello schema senza CHIAVE=valore", i + 1, read.Lines[i]);
                continue;
            }

            string chiave = riga[..uguale].Trim();
            string valore = riga[(uguale + 1)..].Trim();

            switch (LeggiIlValore(valore, out Color? colore))
            {
                case ValoreDelloSchema.Colore:
                    schema.Aggiungi(chiave, colore);
                    break;
                case ValoreDelloSchema.Altro:
                    schema.AggiungiAltro(chiave, valore);
                    break;
                default:
                    _warnings.Add(WarningSeverity.Warning, WarningCategory.Parser, source,
                        "Colore dello schema illeggibile", i + 1, read.Lines[i]);
                    break;
            }
        }

        return schema;
    }

    /// <summary>Che cosa è un valore dello schema.</summary>
    public enum ValoreDelloSchema
    {
        /// <summary>Un colore (o <c>clNone</c>, che è un colore «nessuno»).</summary>
        Colore,

        /// <summary>Un'impostazione: un numero, un simbolo.</summary>
        Altro,

        /// <summary>Sembra un colore (<c>$…</c>, <c>cl…</c>) ma non si legge.</summary>
        Illeggibile,
    }

    /// <summary>Legge un valore nella notazione di Delphi (vedi il sommario della classe).</summary>
    public static ValoreDelloSchema LeggiIlValore(string? valore, out Color? colore)
    {
        colore = null;
        string v = (valore ?? string.Empty).Trim();

        if (v.StartsWith('$'))
        {
            if (v.Length != 9
                || !uint.TryParse(v.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint n))
                return ValoreDelloSchema.Illeggibile;

            int alto = (int)(n >> 24) & 0xFF;
            colore = alto == 0
                ? Color.FromArgb(0xFF, (int)n & 0xFF, (int)(n >> 8) & 0xFF, (int)(n >> 16) & 0xFF)
                : Color.FromArgb(alto, (int)(n >> 16) & 0xFF, (int)(n >> 8) & 0xFF, (int)n & 0xFF);
            return ValoreDelloSchema.Colore;
        }

        if (v.Length > 2 && v.StartsWith("cl", StringComparison.Ordinal) && char.IsUpper(v[2]))
        {
            if (string.Equals(v, "clNone", StringComparison.OrdinalIgnoreCase))
                return ValoreDelloSchema.Colore;
            if (!NomiDiDelphi.TryGetValue(v, out int rgb))
                return ValoreDelloSchema.Illeggibile;
            colore = Color.FromArgb(0xFF, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
            return ValoreDelloSchema.Colore;
        }

        return ValoreDelloSchema.Altro;
    }

    /// <summary>I colori con nome di Delphi (unità <c>Graphics</c> della VCL), in RRGGBB.</summary>
    private static readonly Dictionary<string, int> NomiDiDelphi = new(StringComparer.OrdinalIgnoreCase)
    {
        ["clBlack"] = 0x000000,
        ["clMaroon"] = 0x800000,
        ["clGreen"] = 0x008000,
        ["clOlive"] = 0x808000,
        ["clNavy"] = 0x000080,
        ["clPurple"] = 0x800080,
        ["clTeal"] = 0x008080,
        ["clGray"] = 0x808080,
        ["clSilver"] = 0xC0C0C0,
        ["clRed"] = 0xFF0000,
        ["clLime"] = 0x00FF00,
        ["clYellow"] = 0xFFFF00,
        ["clBlue"] = 0x0000FF,
        ["clFuchsia"] = 0xFF00FF,
        ["clAqua"] = 0x00FFFF,
        ["clWhite"] = 0xFFFFFF,
        ["clLtGray"] = 0xC0C0C0,
        ["clDkGray"] = 0x808080,
        ["clMoneyGreen"] = 0xC0DCC0,
        ["clSkyBlue"] = 0xA6CAF0,
        ["clCream"] = 0xFFFBF0,
        ["clMedGray"] = 0xA0A0A4,
    };
}

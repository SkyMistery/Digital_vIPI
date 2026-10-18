using System.Text;

namespace Aip.Aurora;

/// <summary>Un file scritto: il nome, quante aree, le righe.</summary>
public sealed record AuroraFile(string Name, int Areas, IReadOnlyList<string> Lines);

/// <summary>
/// Un documento intero in file di Aurora, un file per tipo: le aree P, R e D nei file che il sector italiano ha
/// già (<c>&lt;stato&gt;.prohibit</c>, <c>.restrict</c>, <c>.danger</c>), ogni altro tipo in un <c>.geo</c> suo
/// (<c>li-ctr.geo</c>, <c>li-tma.geo</c>). Dove ognuno vada nel sector (MAPS degli <c>.str</c>, settori…) lo decide il
/// Lab con «Porta nel sector», oggetto per oggetto (slice 1 e seguenti): qui si vede tutto quel che il modello ha.
/// </summary>
public static class AuroraFiles
{
    /// <summary>Le righe di Aurora si chiudono come su Windows; UTF-8 senza BOM.</summary>
    public const string NewLine = "\r\n";

    public static (IReadOnlyList<AuroraFile> Files, IReadOnlyList<AuroraIssue> Issues) Convert(AipDocument document, AuroraOptions? options = null)
    {
        var file = new List<AuroraFile>();
        var problemi = new List<AuroraIssue>();
        var prefisso = document.State.Trim().ToLowerInvariant();

        foreach (var gruppo in document.Airspaces.GroupBy(a => a.Type).OrderBy(g => g.Key))
        {
            var scritto = AuroraGeo.Write(gruppo, document.State, document.Airac, options);
            file.Add(new AuroraFile(FileName(prefisso, gruppo.Key), gruppo.Count(), scritto.Lines));
            problemi.AddRange(scritto.Issues);
        }

        return (file, problemi);
    }

    public static string FileName(string prefix, AirspaceType type) => type switch
    {
        AirspaceType.Prohibited => prefix + ".prohibit",
        AirspaceType.Restricted => prefix + ".restrict",
        AirspaceType.Danger => prefix + ".danger",
        _ => $"{prefix}-{type.ToString().ToLowerInvariant()}.geo",
    };

    /// <summary>Scrive i file nella cartella (creata se manca), sovrascrivendo quelli con lo stesso nome.</summary>
    public static void Write(IEnumerable<AuroraFile> files, string folder)
    {
        Directory.CreateDirectory(folder);
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        foreach (var f in files)
            File.WriteAllText(Path.Combine(folder, f.Name), string.Join(NewLine, f.Lines) + NewLine, utf8);
    }
}

using System.Xml.Linq;
using Xunit;

namespace Vipi.Hosting.Tests;

/// <summary>
/// Guardie sul file che governa la build di TUTTO il repository.
///
/// <para><b>Perché esistono.</b> L'11 agosto 2026, scrivendo un commento dentro
/// <c>Directory.Build.props</c>, ci è finito un doppio trattino — stavo citando il nome di un'opzione della
/// riga di comando. In XML un <c>--</c> dentro un commento è illegale: MSBuild risponde <c>MSB4024</c> e
/// <b>non applica nessuna</b> delle proprietà del file. Non solo quella nuova: anche
/// <c>TreatWarningsAsErrors</c>, cioè la rete che tiene su tutte le altre garanzie di build.</para>
///
/// <para>L'errore in sé è rumoroso e si vede subito. La cosa che vale la pena presidiare è un'altra: quel
/// file è un <b>punto singolo di fallimento</b>, e le sue proprietà sono facili da cancellare per sbaglio
/// senza che niente lo dica. Un progetto che compila con gli avvisi tollerati non dà alcun segnale — è
/// esattamente com'era prima che le si aggiungesse.</para>
/// </summary>
public class BuildConfigurationTests
{
    /// <summary>
    /// ⚠️ <b>Onestà su questa guardia:</b> se il file è illeggibile, MSBuild non riesce nemmeno a valutare
    /// questo progetto di test, quindi il test non gira — fallisce la build, non l'asserzione. Vale come
    /// documentazione del guasto e per gli strumenti che leggono il file senza MSBuild.
    /// Le guardie che portano davvero peso sono le due sotto: una proprietà cancellata per sbaglio non
    /// rompe niente, e senza il test nessuno se ne accorge.
    /// </summary>
    [Fact]
    public void Directory_Build_props_e_XML_valido()
    {
        var percorso = Path.Combine(RadiceDelRepo(), "Directory.Build.props");
        Assert.True(File.Exists(percorso), "Directory.Build.props è sparito: con lui spariscono tutte le garanzie di build.");

        var eccezione = Record.Exception(() => XDocument.Load(percorso));

        Assert.True(eccezione is null,
            "Directory.Build.props non è XML valido. MSBuild lo segnala con MSB4024 e poi prosegue SENZA " +
            "applicare nessuna delle sue proprietà — TreatWarningsAsErrors compreso. La causa tipica è un " +
            "doppio trattino dentro un commento (per esempio il nome di un'opzione della riga di comando): " +
            "in XML non è ammesso.\n  " + eccezione?.Message);
    }

    /// <summary>
    /// Le proprietà che devono restare, con il valore che devono avere. Non è pignoleria: ognuna di queste
    /// è l'unica cosa che impedisce il ritorno di un guasto già capitato.
    /// </summary>
    [Theory]
    // 14 chiavi duplicate nei .resx hanno mandato rossa la build di produzione mentre la suite era verde:
    // senza questa, l'avviso torna a essere solo un avviso.
    [InlineData("TreatWarningsAsErrors", "true")]
    // Con le wildcard nei csproj (8.0.*, 10.0.*), senza lock file lo stesso commit compilato fra due mesi
    // è un altro binario — e il pacchetto consegnato si rigenera a ogni correzione.
    [InlineData("RestorePackagesWithLockFile", "true")]
    // Ha trovato una vulnerabilità high il giorno stesso in cui è stato acceso.
    [InlineData("NuGetAudit", "true")]
    [InlineData("NuGetAuditMode", "all")]
    public void Le_proprieta_che_reggono_la_build_ci_sono(string nome, string valoreAtteso)
    {
        var doc = XDocument.Load(Path.Combine(RadiceDelRepo(), "Directory.Build.props"));

        var trovate = doc.Descendants()
            .Where(e => e.Name.LocalName == nome)
            .Select(e => e.Value.Trim())
            .ToList();

        Assert.True(trovate.Count > 0, $"{nome} è sparita da Directory.Build.props.");
        Assert.Equal(valoreAtteso, trovate[0]);
    }

    /// <summary>
    /// Ogni progetto della soluzione ha il proprio <c>packages.lock.json</c>. Un progetto nuovo che ne è
    /// privo non dà problemi in locale — il restore glielo crea — ma in CI il restore gira in «locked mode»
    /// e si ferma. Meglio saperlo qui che dopo il push.
    /// </summary>
    [Fact]
    public void Ogni_progetto_ha_il_proprio_lock_file()
    {
        var radice = RadiceDelRepo();
        var senzaLock = Directory.EnumerateFiles(radice, "*.csproj", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                        !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(p => !File.Exists(Path.Combine(Path.GetDirectoryName(p)!, "packages.lock.json")))
            .Select(p => Path.GetRelativePath(radice, p))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        Assert.True(senzaLock.Count == 0,
            $"{senzaLock.Count} progetti senza packages.lock.json: in CI il restore gira in locked mode e si " +
            "ferma. Si generano con `dotnet restore`, e vanno committati.\n  " + string.Join("\n  ", senzaLock));
    }

    /// <summary>Risale dalla cartella dell'assembly fino alla soluzione: fallisce forte se non la trova.</summary>
    private static string RadiceDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Vipi.slnx"))) dir = dir.Parent;
        Assert.True(dir is not null, "Vipi.slnx non trovata risalendo da " + AppContext.BaseDirectory);
        return dir!.FullName;
    }
}

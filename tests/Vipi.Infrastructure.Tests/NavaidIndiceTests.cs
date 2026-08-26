using Vipi.Application.Abstractions;
using Vipi.Infrastructure.Sectorfile;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// ⚠️ I file di punti sono <b>otto</b>, non tre, e quali siano lo dice <c>ITALY.isc</c>. La lista scritta a
/// mano (itfix/itvor/itndb) è costata quattro settori di Milano senza area: <c>GODRA</c>, <c>GIGUS</c> e
/// <c>GEMLA</c> risultavano «non in catalogo» mentre stavano in <c>ESTERNI.fix</c>, che non leggevamo.
/// Le righe qui sotto sono copiate dall'indice vero.
/// </summary>
public class NavaidIndiceTests
{
    private const string Isc = """
        [INFO]
        ITALY
        [ATC]
        F;NAVAIDS\itvor.vor
        F;NAVAIDS\itndb.ndb
        F;NAVAIDS\itfix.fix
        F;NAVAIDS\ESTERNI.fix
        F;NAVAIDS\MIL.fix
        F;NAVAIDS\APT.fix
        F;NAVAIDS\VFR_NASCOSTI.fix
        F;NAVAIDS\secsi.fix
        F;NAVAIDS\itfix.fix
        F;DYNAMIC_SEC\limmctr.tfl
        F;GEO\coste.geo
        [GEO]
        """;

    [Fact]
    public void Prende_tutti_gli_otto_file_di_punti_dall_indice() =>
        Assert.Equal(
            new[]
            {
                "NAVAIDS/itvor.vor", "NAVAIDS/itndb.ndb", "NAVAIDS/itfix.fix", "NAVAIDS/ESTERNI.fix",
                "NAVAIDS/MIL.fix", "NAVAIDS/APT.fix", "NAVAIDS/VFR_NASCOSTI.fix", "NAVAIDS/secsi.fix",
            },
            AuroraNavaidSource.FileDiPunti(Isc).Select(f => f.Path));

    /// <summary>
    /// ⚠️ L'ordine non è estetico: a parità di nome il catalogo tiene la PRIMA occorrenza, e con essa la
    /// natura del punto. VOR e NDB vanno letti prima dei fix, o basterebbe riordinare `ITALY.isc` perché un
    /// omonimo cambi natura.
    /// </summary>
    [Fact]
    public void Vor_e_ndb_si_leggono_prima_dei_fix()
    {
        var kinds = AuroraNavaidSource.FileDiPunti(Isc).Select(f => f.Kind).ToList();

        Assert.Equal(NavaidKind.Vor, kinds[0]);
        Assert.Equal(NavaidKind.Ndb, kinds[1]);
        Assert.All(kinds.Skip(2), k => Assert.Equal(NavaidKind.Fix, k));
    }

    [Fact]
    public void Un_file_citato_due_volte_si_legge_una_volta_sola() =>
        Assert.Single(AuroraNavaidSource.FileDiPunti(Isc), f => f.Path.EndsWith("itfix.fix"));

    [Fact]
    public void Gli_altri_include_non_entrano() =>
        Assert.DoesNotContain(AuroraNavaidSource.FileDiPunti(Isc),
            f => f.Path.Contains("DYNAMIC_SEC") || f.Path.Contains("GEO"));

    /// <summary>Indice senza righe di punti: l'elenco è vuoto, ed è il segnale che fa ripiegare sui tre
    /// percorsi di configurazione — un catalogo ridotto è meglio di nessun catalogo.</summary>
    [Fact]
    public void Un_indice_senza_punti_da_elenco_vuoto() =>
        Assert.Empty(AuroraNavaidSource.FileDiPunti("[INFO]\nITALY\nF;DYNAMIC_SEC\\limmctr.tfl\n"));

    /// <summary>Il caso vero, in piccolo: il punto d'oltreconfine sta nel quarto file, e da lì si risolve.</summary>
    [Fact]
    public void Un_punto_di_ESTERNI_entra_in_catalogo_con_le_sue_coordinate()
    {
        var catalogo = AuroraSectorfileParser.ParseNavaids(new (NavaidKind, string?)[]
        {
            (NavaidKind.Vor, "SRN;113.70;N045.03.44.000;E007.36.44.000;\n"),
            (NavaidKind.Ndb, null),
            (NavaidKind.Fix, "ABADI;N044.00.00.000;E011.00.00.000;3;\n"),
            (NavaidKind.Fix, "//ESTERNI\nGODRA;N046.35.34.000;E007.42.32.000;3;\nGIGUS;N045.23.23.000;E006.26.30.000;3;\n"),
        });

        Assert.True(catalogo.TryGetPoint("GODRA", out var godra));
        Assert.Equal(46.5928, godra.Lat, 3);
        Assert.True(catalogo.TryGetPoint("GIGUS", out _));
        Assert.Contains("ABADI", catalogo.Names);     // i file principali restano dentro
        Assert.Contains("SRN", catalogo.Names);
    }

    /// <summary>A parità di nome vince chi è stato accodato prima: il VOR, non il fix omonimo.</summary>
    [Fact]
    public void Un_nome_presente_due_volte_tiene_la_prima_natura()
    {
        var catalogo = AuroraSectorfileParser.ParseNavaids(new (NavaidKind, string?)[]
        {
            (NavaidKind.Vor, "TOP;114.00;N045.00.00.000;E009.00.00.000;\n"),
            (NavaidKind.Fix, "TOP;N044.00.00.000;E011.00.00.000;3;\n"),
        });

        var voce = Assert.Single(catalogo.Entries);
        Assert.Equal(NavaidKind.Vor, voce.Kind);
    }
}

using Vipi.Application.Airspace;
using Vipi.Application.Aor;
using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// La tabella «spazi aerei» sotto l'AoR (carta 2026-09-17-tabella-spazi-aerei-nell-aor.md): righe SOLO dai pezzi
/// dell'aggancio all'AIP, una per volume anche se due settori del blocco lo condividono, nell'ordine di disegno.
/// </summary>
public class AorAirspaceTableTests
{
    private static ShapePart Pezzo(string nome, string? classe = null) =>
        new("[[1,1],[2,1],[2,2]]", 0, 4500, AirspaceDatum.Gnd, AirspaceDatum.Amsl, "GND", "4500 FT AMSL",
            $"CTR|{nome}|GND|4500 FT AMSL", nome, classe);

    [Fact]
    public void Solo_I_Pezzi_Dell_Aip_Una_Riga_Per_Volume_Con_Le_Correzioni()
    {
        var z1 = Pezzo("PESCARA CTR Z1");
        var z2 = Pezzo("PESCARA CTR Z2", "c");
        var forme = new Dictionary<string, SectorShape>
        {
            ["A_APP"] = new("A_APP", ShapeSource.Aip, new[] { z1, z2 }, Array.Empty<string>()),
            ["B_APP"] = new("B_APP", ShapeSource.Aip, new[] { z2 }, Array.Empty<string>()),   // lo stesso volume
            ["C_APP"] = new("C_APP", ShapeSource.Source, new[] { Pezzo("IVAO") }, Array.Empty<string>()),
        };
        var correzioni = new Dictionary<string, AorAirspaceEdit>
        {
            [z1.SourceRef!] = new() { Class = "D", Note = "nota" },
        };

        var righe = AorAirspaceTable.Build(new[] { "A_APP", "B_APP", "C_APP", "SENZA" }, forme, correzioni);

        Assert.Equal(new[] { "PESCARA CTR Z1", "PESCARA CTR Z2" }, righe.Select(r => r.Name).ToArray());
        Assert.Equal("D", righe[0].Class);
        Assert.Equal("nota", righe[0].Note);
        Assert.Equal("C", righe[1].Class);          // dal file, normalizzata
        Assert.Null(righe[1].EditedClass);
    }
}

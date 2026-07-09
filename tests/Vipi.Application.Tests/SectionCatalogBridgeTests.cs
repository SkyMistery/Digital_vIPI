using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>Ponte BlockSection legacy → chiavi catalogo (doc refactor 08c): mappature 1:1 e non mappate.</summary>
public class SectionCatalogBridgeTests
{
    [Theory]
    [InlineData(BlockSection.Aor, "aor")]
    [InlineData(BlockSection.Frequencies, "frequencies")]
    [InlineData(BlockSection.Coordination, "coordination")]
    [InlineData(BlockSection.OperationalTechnique, "operationaltechnique")]
    [InlineData(BlockSection.Separations, "separations")]
    [InlineData(BlockSection.AreasCorridors, "regulated")]   // Military → Regulated
    [InlineData(BlockSection.Validity, "validity")]
    public void KeyFor_maps_fixed_sections(BlockSection section, string expectedKey)
    {
        Assert.Equal(expectedKey, SectionCatalogBridge.KeyFor(section));
        Assert.True(SectionCatalogBridge.HasCatalogKey(section));
    }

    [Theory]
    [InlineData(BlockSection.Airport)]     // ambiguo (5 sezioni aeroporto per titolo)
    [InlineData(BlockSection.Purpose)]     // rimossa
    [InlineData(BlockSection.Atis)]
    [InlineData(BlockSection.Other)]
    public void KeyFor_returns_null_for_unmapped(BlockSection section)
    {
        Assert.Null(SectionCatalogBridge.KeyFor(section));
        Assert.False(SectionCatalogBridge.HasCatalogKey(section));
    }
}

using Vipi.Application.Content;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>Chiavi delle sezioni libere (doc 11 §3a): univoche, riconoscibili, e comunque editoriali per il catalogo.</summary>
public class SectionKeysTests
{
    [Fact]
    public void NewCustom_Is_Unique_And_Well_Formed()
    {
        var keys = Enumerable.Range(0, 200).Select(_ => SectionKeys.NewCustom()).ToList();

        Assert.Equal(keys.Count, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(keys, k => Assert.StartsWith("custom:", k, StringComparison.Ordinal));
        Assert.All(keys, k => Assert.Equal("custom:".Length + 8, k.Length));
    }

    [Fact]
    public void Generated_Key_Is_Editorial_For_The_Catalog()
    {
        // Il catalogo tratta come Editorial ogni chiave che non conosce: la chiave univoca non cambia natura.
        Assert.Equal(SectionKind.Editorial, SectionCatalog.KindOf(SectionKeys.NewCustom()));
        Assert.False(SectionCatalog.IsRenderModeToggleable(SectionKeys.NewCustom()));
    }

    [Fact]
    public void IsCustom_Covers_Legacy_And_New_Keys()
    {
        Assert.True(SectionKeys.IsCustom(SectionKeys.LegacyCustom));
        Assert.True(SectionKeys.IsCustom(SectionKeys.NewCustom()));
        Assert.False(SectionKeys.IsCustom("aor"));
        Assert.False(SectionKeys.IsCustom(null));
    }

    [Fact]
    public void IsLegacyCustom_Only_The_Ambiguous_One()
    {
        Assert.True(SectionKeys.IsLegacyCustom("custom"));
        Assert.True(SectionKeys.IsLegacyCustom("CUSTOM"));
        Assert.False(SectionKeys.IsLegacyCustom(SectionKeys.NewCustom()));
        Assert.False(SectionKeys.IsLegacyCustom("validity"));
    }
}

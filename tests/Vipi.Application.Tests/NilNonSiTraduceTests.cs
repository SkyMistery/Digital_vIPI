using Vipi.Application.Translation;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// «NIL» e' la stessa parola in italiano e in inglese: non si traduce, e a schermo si legge uguale nelle due
/// lingue perche' quel che non e' tradotto resta com'e' (<see cref="TranslationLookup"/>). Chiesto dal
/// committente il 10 settembre 2026.
/// </summary>
public class NilNonSiTraduceTests
{
    [Theory]
    [InlineData("NIL")]
    [InlineData("nil")]
    [InlineData("  Nil  ")]
    [InlineData("NIL.")]
    [InlineData("nil .")]
    public void Un_NIL_da_solo_non_si_traduce(string testo)
    {
        Assert.True(TranslationText.IsNilOnly(testo));
        Assert.False(TranslationText.HasSomethingToTranslate(testo));
    }

    /// <summary>
    /// ⚠️ Stretto di proposito: la parola DENTRO una frase e' contenuto, e deve passare il cancello. Un
    /// riconoscitore generoso qui vorrebbe dire una riga di documento che non viene mai tradotta.
    /// </summary>
    [Theory]
    [InlineData("NIL for runway 07")]
    [InlineData("Nessuna: NIL")]
    [InlineData("NIL NIL")]
    [InlineData("ANIL")]
    public void La_parola_dentro_una_frase_resta_contenuto(string testo)
    {
        Assert.False(TranslationText.IsNilOnly(testo));
        Assert.True(TranslationText.HasSomethingToTranslate(testo));
    }

    [Fact]
    public void Il_cancello_di_prima_non_cambia()
    {
        Assert.False(TranslationText.HasSomethingToTranslate(null));
        Assert.False(TranslationText.HasSomethingToTranslate("   "));
        Assert.False(TranslationText.HasSomethingToTranslate("126.850"));
        Assert.True(TranslationText.HasSomethingToTranslate("Attendere sul piazzale."));
    }

    /// <summary>
    /// ⚠️ La costante e' UNA: la usa il cancello e la usa il blocco di prosa appena creato. Se divergessero,
    /// un blocco nuovo verrebbe spedito al motore di traduzione.
    /// </summary>
    [Fact]
    public void La_parola_e_una_costante_condivisa()
    {
        Assert.Equal("NIL", TranslationText.Nil);
        Assert.False(TranslationText.HasSomethingToTranslate(TranslationText.Nil));
    }
}

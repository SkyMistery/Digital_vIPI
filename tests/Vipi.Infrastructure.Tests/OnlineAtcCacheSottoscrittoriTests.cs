using Vipi.Application.Abstractions;
using Vipi.Infrastructure.Ivao;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// 🔴 T-072 (revisione del 13 settembre 2026): chi guarda la cache ATC non può rompere chi la scrive. Uno stream
/// SSE chiuso a metà notifica rilasciava un semaforo smaltito, e con <c>Changed?.Invoke()</c> l'eccezione
/// fermava gli altri sottoscrittori e risaliva nel poller.
/// </summary>
public class OnlineAtcCacheSottoscrittoriTests
{
    [Fact]
    public void Un_sottoscrittore_rotto_non_ferma_gli_altri_ne_il_poller()
    {
        var cache = new OnlineAtcCache();
        var chiusa = new SemaphoreSlim(0);
        chiusa.Dispose();
        var avvisati = 0;

        cache.Changed += () => chiusa.Release();   // lo stream SSE che si è chiuso fra la copia e la chiamata
        cache.Changed += () => avvisati++;

        var ex = Record.Exception(() => cache.Set(OnlineAtcSnapshot.Empty));

        Assert.Null(ex);
        Assert.Equal(1, avvisati);
        Assert.Same(OnlineAtcSnapshot.Empty, cache.GetCurrent());
    }
}

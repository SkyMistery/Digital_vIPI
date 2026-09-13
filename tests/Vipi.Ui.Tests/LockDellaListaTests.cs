using System.Reflection;
using Vipi.Application.Content;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// 🔴 T-041 (revisione del 13 settembre 2026): pubblicare o scartare dalla lista prende il lock. Se il gesto
/// fallisce il lock va lasciato — ma solo se l'ha preso quel gesto.
/// </summary>
public class LockDellaListaTests
{
    /// <summary>Un servizio d'editing che risponde solo alle tre domande sul lock e le conta.</summary>
    public class EditingFinto : DispatchProxy
    {
        public bool EraMio { get; set; }
        public List<string> Chiamate { get; } = new();

        protected override object? Invoke(MethodInfo? m, object?[]? a)
        {
            Chiamate.Add(m!.Name);
            return m.Name switch
            {
                nameof(IEditingService.InspectLockAsync) => Task.FromResult(EraMio
                    ? new LockInfo { Locked = true, IsMine = true }
                    : LockInfo.Free()),
                nameof(IEditingService.AcquireLockAsync) => Task.FromResult(new LockInfo { Locked = true, IsMine = true }),
                nameof(IEditingService.ReleaseLockAsync) => Task.CompletedTask,
                _ => throw new NotSupportedException(m.Name),
            };
        }
    }

    private static (IEditingService Servizio, EditingFinto Finto) Editing(bool eraMio)
    {
        var s = DispatchProxy.Create<IEditingService, EditingFinto>();
        var f = (EditingFinto)(object)s;
        f.EraMio = eraMio;
        return (s, f);
    }

    [Fact]
    public async Task Se_il_gesto_fallisce_il_lock_preso_dalla_lista_si_lascia()
    {
        var (s, f) = Editing(eraMio: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            LockDellaLista.EseguiAsync(s, 7, () => throw new InvalidOperationException("solo una bozza")));

        Assert.Contains(nameof(IEditingService.ReleaseLockAsync), f.Chiamate);
    }

    /// <summary>Il lock era già mio: è un editor aperto in un'altra scheda, e non si chiude sotto le mani.</summary>
    [Fact]
    public async Task Se_il_lock_era_gia_mio_non_si_toglie()
    {
        var (s, f) = Editing(eraMio: true);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            LockDellaLista.EseguiAsync(s, 7, () => throw new InvalidOperationException("x")));

        Assert.DoesNotContain(nameof(IEditingService.ReleaseLockAsync), f.Chiamate);
    }

    /// <summary>Il gesto riuscito il lock lo lascia da sé (pubblicare e scartare lo rilasciano nel servizio).</summary>
    [Fact]
    public async Task Se_il_gesto_riesce_non_si_rilascia_qui()
    {
        var (s, f) = Editing(eraMio: false);
        await LockDellaLista.EseguiAsync(s, 7, () => Task.CompletedTask);
        Assert.DoesNotContain(nameof(IEditingService.ReleaseLockAsync), f.Chiamate);
    }
}

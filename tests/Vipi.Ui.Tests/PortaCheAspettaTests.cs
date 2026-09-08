using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.Ui;

namespace Vipi.Ui.Tests;

/// <summary>
/// La <b>terza porta</b> messa alla prova dove è provabile: sul suo comportamento, non a schermo.
///
/// <para>🔴 <b>Perché non basta la verifica live.</b> Guidando l'app in locale l'8 settembre 2026 — trenta
/// uscite a metà caricamento su sei pagine — il registro è rimasto <b>pulito</b>. E lo è rimasto anche
/// rifacendo lo stesso giro sul codice <b>senza</b> la correzione: su SQLite in locale un caricamento finisce
/// prima che si faccia in tempo ad andarsene, quindi quella prova non distingueva le due versioni. Una prova
/// che non ha mai visto il guasto non prova che il guasto non c'è: dice solo che non l'ha visto. Il difetto
/// vero vuole la latenza di MySQL e più gente insieme, che in locale non ci sono.</para>
///
/// <para>✅ Quel che si può provare, e qui si prova, è la <b>garanzia</b>: che la chiusura aspetti chi è
/// dentro, che a porta chiusa non entri più nessuno, che il rientro non si pianti, e che lo scope alla fine
/// venga smaltito <b>davvero</b> — la riga nel <c>finally</c> senza la quale ogni visita lascerebbe in piedi
/// un <c>DbContext</c>.</para>
/// </summary>
public sealed class PortaCheAspettaTests
{
    /// <summary>Un componente finto che si può bloccare a comando: è tutto quel che serve per vedere la porta.</summary>
    private sealed class Sonda : ScopeProprioCheAspetta
    {
        public TaskCompletionSource Blocco { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Entrate;

        public Task CaricaBloccandosi() => InFilaAsync(async () =>
        {
            Interlocked.Increment(ref Entrate);
            await Blocco.Task;
        });

        public Task CaricaSubito() => InFilaAsync(() =>
        {
            Interlocked.Increment(ref Entrate);
            return Task.CompletedTask;
        });

        /// <summary>Un caricamento che ne chiama un altro: senza la guardia del rientro si pianterebbe qui.
        /// ⚠️ Contano <b>tutt'e due</b> i livelli, o il test non distinguerebbe «annidato ed è passato» da
        /// «annidato e il di dentro non è mai girato».</summary>
        public Task CaricaAnnidato() => InFilaAsync(async () =>
        {
            Interlocked.Increment(ref Entrate);
            await CaricaSubito();
        });

        public IServiceProvider Scope => ScopedServices;
    }

    private sealed class Testimone : IDisposable
    {
        public bool Smaltito { get; private set; }
        public void Dispose() => Smaltito = true;
    }

    private static (TestContext Ctx, Sonda S) Sonda_()
    {
        var ctx = new TestContext();
        ctx.Services.AddScoped<Testimone>();
        return (ctx, ctx.RenderComponent<Sonda>().Instance);
    }

    [Fact]
    public async Task La_chiusura_ASPETTA_chi_e_dentro()
    {
        var (ctx, s) = Sonda_();
        using var _ = ctx;

        var caricamento = s.CaricaBloccandosi();
        var chiusura = s.DisposeAsync().AsTask();

        // ⚠️ L'attesa non è una gara sui tempi: finché il caricamento tiene il permesso, la chiusura NON PUO'
        // finire. Questo mezzo secondo serve solo a dare alla chiusura tutte le occasioni di sbagliare.
        var finitaSubito = await Task.WhenAny(chiusura, Task.Delay(500)) == chiusura;
        Assert.False(finitaSubito, "La chiusura non ha aspettato: e' uscita col caricamento ancora dentro.");

        s.Blocco.SetResult();
        await chiusura.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(caricamento.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task A_porta_chiusa_non_entra_piu_nessuno()
    {
        var (ctx, s) = Sonda_();
        using var _ = ctx;

        await s.CaricaSubito();
        Assert.Equal(1, s.Entrate);

        await s.DisposeAsync();

        // ⚠️ E non ASPETTA una porta che non si riapre: torna indietro subito. Se aspettasse, questo test
        // non fallirebbe — resterebbe appeso, che e' il modo peggiore di fallire.
        await s.CaricaSubito().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, s.Entrate);
    }

    [Fact]
    public async Task Un_caricamento_dentro_un_altro_non_si_pianta()
    {
        var (ctx, s) = Sonda_();
        using var _ = ctx;

        await s.CaricaAnnidato().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, s.Entrate);
    }

    /// <summary>
    /// 🔴 La riga nel <c>finally</c>. Quando un componente è <c>IAsyncDisposable</c>, il renderer chiama
    /// <b>solo</b> <c>DisposeAsync</c>: senza quella riga lo scope — e il <c>DbContext</c> dentro — resterebbe
    /// in piedi a ogni visita. È un guasto che non si vede il primo giorno e si vede il quindicesimo, cioè
    /// esattamente quello che un test deve prendere al posto nostro.
    /// </summary>
    [Fact]
    public async Task Alla_fine_lo_scope_viene_smaltito_davvero()
    {
        var (ctx, s) = Sonda_();
        using var _ = ctx;

        var testimone = s.Scope.GetRequiredService<Testimone>();
        Assert.False(testimone.Smaltito);

        await s.DisposeAsync();

        Assert.True(testimone.Smaltito, "Lo scope del componente non e' stato smaltito: manca la riga nel `finally`.");
    }

    [Fact]
    public async Task Chiudere_due_volte_non_fa_danno()
    {
        var (ctx, s) = Sonda_();
        using var _ = ctx;

        await s.DisposeAsync();
        await s.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    }
}

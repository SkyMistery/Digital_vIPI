using Vipi.Hosting;

namespace Vipi.Hosting.Tests;

/// <summary>
/// Caratterizzazione di <see cref="StaffLoginThrottle"/>: la decisione «registro questo login?» deve essere
/// atomica per UserId. Il caso concorrente è realistico — Blazor Server apre più richieste in parallelo per ogni
/// caricamento di pagina, quindi un leggi-poi-scrivi non atomico lascia passare due scritture DB per lo stesso utente.
/// </summary>
public class StaffLoginThrottleTests
{
    [Fact]
    public void Primo_Login_Passa()
    {
        var throttle = new StaffLoginThrottle();
        Assert.True(throttle.ShouldRecord(1001));
    }

    [Fact]
    public void Secondo_Login_Nella_Finestra_Non_Passa()
    {
        var throttle = new StaffLoginThrottle();
        Assert.True(throttle.ShouldRecord(1001));
        Assert.False(throttle.ShouldRecord(1001));
        Assert.False(throttle.ShouldRecord(1001));
    }

    [Fact]
    public void Utenti_Diversi_Sono_Indipendenti()
    {
        var throttle = new StaffLoginThrottle();
        Assert.True(throttle.ShouldRecord(1001));
        Assert.True(throttle.ShouldRecord(1002));
        Assert.False(throttle.ShouldRecord(1001));
    }

    /// <summary>
    /// Thread dedicati (non il thread pool): il rendez-vous su <see cref="Barrier"/> è bloccante, e sul pool
    /// costringerebbe il runtime a iniettare thread uno alla volta — test lentissimo e contesa quasi nulla.
    /// Con thread propri le chiamate partono davvero insieme.
    /// </summary>
    private static IReadOnlyList<(int User, bool Ok)> Race(StaffLoginThrottle throttle, IReadOnlyList<int> userIds)
    {
        var results = new (int User, bool Ok)[userIds.Count];
        using var start = new Barrier(userIds.Count);
        var threads = new Thread[userIds.Count];

        for (var i = 0; i < userIds.Count; i++)
        {
            var slot = i;
            threads[slot] = new Thread(() =>
            {
                start.SignalAndWait();
                results[slot] = (userIds[slot], throttle.ShouldRecord(userIds[slot]));
            }) { IsBackground = true };
            threads[slot].Start();
        }

        foreach (var t in threads) Assert.True(t.Join(TimeSpan.FromSeconds(30)), "thread di corsa non terminato");
        return results;
    }

    [Fact]
    public void Richieste_Concorrenti_Dello_Stesso_Utente_Passano_Una_Sola_Volta()
    {
        var throttle = new StaffLoginThrottle();
        var userIds = Enumerable.Repeat(2001, 32).ToList();

        var granted = Race(throttle, userIds).Count(x => x.Ok);

        Assert.Equal(1, granted);
    }

    [Fact]
    public void Concorrenza_Su_Utenti_Diversi_Concede_Uno_Per_Utente()
    {
        var throttle = new StaffLoginThrottle();
        const int users = 8, perUser = 4;
        var userIds = (from u in Enumerable.Range(3001, users)
                       from _ in Enumerable.Range(0, perUser)
                       select u).ToList();

        var granted = Race(throttle, userIds).Where(x => x.Ok).ToList();

        Assert.Equal(users, granted.Count);
        Assert.Equal(users, granted.Select(x => x.User).Distinct().Count());
    }
}

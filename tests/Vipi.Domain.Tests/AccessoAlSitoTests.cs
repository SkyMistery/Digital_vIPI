using Vipi.Domain.Entities;
using Xunit;

namespace Vipi.Domain.Tests;

/// <summary>Il registro degli accessi (30 settembre 2026): il giorno si conta una volta, i dati si aggiornano senza
/// perdere quelli che un cookie vecchio non porta.</summary>
public class AccessoAlSitoTests
{
    private static readonly DateTime Lunedi = new(2026, 9, 28, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Il_primo_accesso_apre_la_riga_con_un_giorno()
    {
        var a = new AccessoAlSito { UserId = 704798 };
        a.Registra("Carmine Granato", "it", "lirr", Lunedi);

        Assert.Equal(Lunedi, a.PrimoUtc);
        Assert.Equal(Lunedi, a.UltimoUtc);
        Assert.Equal(1, a.Giorni);
        Assert.Equal("IT", a.Divisione);
        Assert.Equal("LIRR", a.Acc);
    }

    [Fact]
    public void Dieci_accessi_nello_stesso_giorno_sono_un_giorno_il_giorno_dopo_sono_due()
    {
        var a = new AccessoAlSito { UserId = 1 };
        for (var i = 0; i < 10; i++) a.Registra("A", "IT", "LIRR", Lunedi.AddHours(i));
        Assert.Equal(1, a.Giorni);

        a.Registra("A", "IT", "LIRR", Lunedi.AddDays(1).AddMinutes(5));
        Assert.Equal(2, a.Giorni);
        Assert.Equal(Lunedi, a.PrimoUtc);
    }

    [Fact]
    public void Un_cookie_senza_divisione_non_cancella_quella_nota()
    {
        var a = new AccessoAlSito { UserId = 1 };
        a.Registra("A", "FR", "LFFF", Lunedi);
        a.Registra("A", null, null, Lunedi.AddDays(1));

        Assert.Equal("FR", a.Divisione);
        Assert.Equal("LFFF", a.Acc);
    }

    [Theory]
    [InlineData("Mario", "Rossi", "Mario R.")]
    [InlineData("Gian  Marco", "Rossi", "Gian Marco R.")]   // il nome resta intero: dal nome unito sarebbe «Gian R.»
    [InlineData("Maria", "de Santis", "Maria D.")]
    [InlineData("Luca", "", "Luca")]                       // senza cognome, il solo nome
    [InlineData("", "Rossi", null)]                        // senza nome, meglio il VID che un'iniziale
    [InlineData(null, null, null)]
    public void Il_nome_breve_e_nome_piu_iniziale_del_cognome(string? nome, string? cognome, string? atteso)
    {
        Assert.Equal(atteso, AccessoAlSito.ComponiNomeBreve(nome, cognome));
    }

    [Fact]
    public void Un_nome_breve_che_manca_non_cancella_quello_noto()
    {
        var a = new AccessoAlSito { UserId = 1 };
        a.Registra("Mario Rossi", "IT", "LIRR", Lunedi, "Mario R.");
        a.Registra("Mario Rossi", "IT", "LIRR", Lunedi.AddDays(1));
        Assert.Equal("Mario R.", a.NomeBreve);
    }

    [Fact]
    public void Un_nome_troppo_lungo_si_taglia_alla_colonna()
    {
        var a = new AccessoAlSito { UserId = 1 };
        a.Registra(new string('x', 500), "IT", "LIRR", Lunedi);
        Assert.Equal(AccessoAlSitoLimits.Nome, a.Nome.Length);
    }
}

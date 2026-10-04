using Vipi.Application.EventKits;
using Vipi.Domain.Entities;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Le regole pure del pacchetto dell'evento (carta <c>docs/feature/2026-09-30-profili-evento.md</c>): quando si vede,
/// quali link e quali file si accettano, con che nome si scarica.
/// </summary>
public class EventKitRulesTests
{
    private static readonly DateTime Ora = new(2026, 10, 3, 18, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(false, null, null, false)]      // spento: mai
    [InlineData(true, null, null, true)]        // acceso senza date: subito
    [InlineData(true, -1, null, true)]          // cominciato
    [InlineData(true, 1, null, false)]          // non ancora
    [InlineData(true, null, 1, true)]           // prima della fine
    [InlineData(true, null, 0, false)]          // la fine è esclusa
    [InlineData(false, -1, 1, false)]           // spento vince sulle date
    public void Si_vede_se_acceso_e_dentro_le_date(bool acceso, int? daOre, int? aOre, bool atteso)
    {
        var kit = new EventKit
        {
            IsActive = acceso,
            StartsUtc = daOre is int d ? Ora.AddHours(d) : null,
            EndsUtc = aOre is int a ? Ora.AddHours(a) : null,
        };
        Assert.Equal(atteso, EventKitRules.Visibile(kit, Ora));
        Assert.False(EventKitRules.Visibile(null, Ora));
    }

    [Theory]
    [InlineData("https://drive.google.com/file/d/abc/view?usp=sharing", true)]
    [InlineData("https://drive.google.com/drive/folders/xyz", true)]
    [InlineData("http://drive.google.com/file/d/abc", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("drive.google.com/file/d/abc", false)]
    [InlineData("", false)]
    public void Un_link_e_assoluto_e_https(string url, bool atteso) =>
        Assert.Equal(atteso, EventKitRules.LinkValido(url));

    [Theory]
    [InlineData("LIRF_TWR.cpr", true)]
    [InlineData("stand-manager.ZIP", true)]
    [InlineData("colori.clr", true)]
    [InlineData("setup.exe", false)]
    [InlineData("pagina.html", false)]
    [InlineData("immagine.svg", false)]
    [InlineData("senza-estensione", false)]
    public void Si_caricano_solo_le_estensioni_ammesse(string nome, bool atteso) =>
        Assert.Equal(atteso, EventKitRules.EstensioneAmmessa(nome));

    [Fact]
    public void Il_nome_del_file_perde_le_cartelle_e_i_caratteri_pericolosi_e_tiene_l_estensione()
    {
        Assert.Equal("LIRF_TWR.cpr", EventKitRules.NomeFileSicuro("C:\\Users\\x\\LIRF_TWR.cpr"));
        Assert.Equal("a.cpr", EventKitRules.NomeFileSicuro("../../a.cpr"));
        Assert.Equal("ab.cpr", EventKitRules.NomeFileSicuro("a\"\nb.cpr"));

        var lungo = EventKitRules.NomeFileSicuro(new string('x', 300) + ".cpr");
        Assert.Equal(EventKitRules.MaxNomeFile, lungo.Length);
        Assert.EndsWith(".cpr", lungo);
    }

    [Fact]
    public void La_risposta_in_memoria_scade_e_si_svuota()
    {
        var cache = new EventKitVisibilityCache();
        Assert.False(cache.TryGet(Ora, out _));
        cache.Set("Evento", Ora);
        Assert.True(cache.TryGet(Ora.AddSeconds(29), out var v) && v == "Evento");
        Assert.False(cache.TryGet(Ora + EventKitVisibilityCache.Durata, out _));
        cache.Set(null, Ora);
        Assert.True(cache.TryGet(Ora, out var nessuno) && nessuno is null);   // «nessun evento» si tiene anche lui
        cache.Svuota();
        Assert.False(cache.TryGet(Ora, out _));
    }
}

using Vipi.Application.Content;
using Vipi.Ui.Components.App;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Le regole della tabella dei livelli di transizione — il **cuore deterministico** dell'editor, estratto
/// dalla pagina perché fosse provabile (invariante #8 del runbook: prima si estrae il cuore senza I/O, poi si
/// usa come rete per le estrazioni successive).
///
/// <para>
/// Prima stava dentro <c>AeroportoEditorPage</c>, componeva le frasi tradotte da sé, e per provarla sarebbe
/// servito montare una pagina da 2000 righe con un localizzatore. Ora entrano righe ed escono problemi.
/// </para>
///
/// <para>
/// ⚠️ Perché conta: un livello di transizione sbagliato lo legge un controllore per dire a un pilota a che
/// quota passare da QNH a standard. La tabella che si accavalla o che salta una fascia è un errore che si
/// vede solo quando qualcuno ci vola dentro.
/// </para>
/// </summary>
public class AirportTlValidationTests
{
    private static TlEdit Riga(int? da, int? a, string? livello) =>
        new() { From = da, To = a, Level = livello };

    private static string[] Chiavi(params TlEdit[] righe) =>
        AirportTlValidation.Issues(righe).Select(i => i.Key).ToArray();

    [Fact]
    public void Una_tabella_sana_non_ha_problemi()
    {
        // Tre fasce contigue che non si toccano: «fino a 1012», «1013–1031», «da 1032 in su».
        Assert.Empty(AirportTlValidation.Issues(new[]
        {
            Riga(null, 1012, "FL80"),
            Riga(1013, 1031, "FL75"),
            Riga(1032, null, "FL70"),
        }));
    }

    [Fact]
    public void Una_riga_senza_livello_si_segnala()
    {
        Assert.Equal(new[] { "Ape_IssueTlMissing" }, Chiavi(Riga(1013, 1031, "  ")));
    }

    [Fact]
    public void Gli_estremi_invertiti_si_segnalano()
    {
        Assert.Contains("Ape_IssueQnhOrder", Chiavi(Riga(1031, 1013, "FL75")));
    }

    [Fact]
    public void Due_fasce_che_si_accavallano_si_segnalano()
    {
        var problemi = AirportTlValidation.Issues(new[]
        {
            Riga(1000, 1020, "FL80"),
            Riga(1015, 1030, "FL75"),   // 1015–1020 sta in tutt'e due: quale livello vale?
        });

        var accavallamento = Assert.Single(problemi, i => i.Key == "Ape_IssueQnhOverlap");
        Assert.Equal(new object[] { 1, 2 }, accavallamento.Args);   // le due righe si nominano per posizione
    }

    [Fact]
    public void Una_fascia_APERTA_si_accavalla_con_quella_che_la_tocca()
    {
        // ⚠️ Il caso che l'estremo assente rende insidioso: «da 1013 in su» e «fino a 1020» si sovrappongono
        // per otto hPa, e a occhio nella tabella non si vede — le due righe non hanno un numero in comune.
        Assert.Contains("Ape_IssueQnhOverlap", Chiavi(
            Riga(1013, null, "FL70"),
            Riga(null, 1020, "FL80")));
    }

    [Fact]
    public void Due_fasce_contigue_NON_si_accavallano()
    {
        // Il confine è chiuso a destra e aperto a sinistra: 1012 e 1013 sono fasce diverse, non un errore.
        Assert.Empty(AirportTlValidation.Issues(new[]
        {
            Riga(null, 1012, "FL80"),
            Riga(1013, null, "FL70"),
        }));
    }

    [Fact]
    public void Una_tabella_vuota_non_e_un_problema()
    {
        // Uno scalo che i livelli non li ha ancora scritti non è uno scalo con un errore.
        Assert.Empty(AirportTlValidation.Issues(Array.Empty<TlEdit>()));
    }

    [Fact]
    public void I_problemi_si_contano_tutti_e_nominano_la_riga_giusta()
    {
        var problemi = AirportTlValidation.Issues(new[]
        {
            Riga(1000, 1020, null),     // manca il livello   → riga 1
            Riga(1015, 1030, "FL75"),   // accavalla la 1     → righe 1 e 2
            Riga(1040, 1035, "FL70"),   // estremi invertiti  → riga 3
        });

        Assert.Equal(3, problemi.Count);
        Assert.Equal(new object[] { 1 }, problemi.Single(i => i.Key == "Ape_IssueTlMissing").Args);
        Assert.Equal(new object[] { 3 }, problemi.Single(i => i.Key == "Ape_IssueQnhOrder").Args);
    }
}

/// <summary>Le regole della tabella piste, e il filtro del picker delle frequenze collegabili — gli altri due
/// cuori deterministici usciti dalla pagina con gli editor (doc 14 §3g).</summary>
public class AirportRunwayEFrequenzeTests
{
    private static RwEdit Pista(string? ident) => new() { Ident = ident };

    [Fact]
    public void Due_piste_con_lo_stesso_identificativo_si_segnalano()
    {
        var problemi = AirportRunwayValidation.Issues(new[] { Pista("16L"), Pista("34R"), Pista("16l") });

        var doppia = Assert.Single(problemi);
        Assert.Equal("Ape_IssueRwDup", doppia.Key);
        Assert.Equal(new object[] { "16l" }, doppia.Args);   // si nomina quella di troppo, non la prima
    }

    [Fact]
    public void Una_riga_appena_aggiunta_non_e_un_errore()
    {
        // ⚠️ Il caso che conta: chi preme «+ Pista» ha una riga vuota sotto il cursore. Segnalarla come
        // duplicata — sono due stringhe vuote uguali — mostrerebbe un avviso rosso a chi sta scrivendo.
        Assert.Empty(AirportRunwayValidation.Issues(new[] { Pista("16L"), Pista(null), Pista("  ") }));
    }

    private static LinkableFrequencyRow Freq(int id, string callsign, string mhz, string? icao = null) =>
        new(id, icao, callsign, mhz);

    private static string[] Filtrate(string? cerca, params LinkableFrequencyRow[] tutte) =>
        AirportFrequencyPicker.Filtra(tutte, cerca).Select(f => f.Callsign).ToArray();

    [Fact]
    public void Il_picker_cerca_per_callsign_frequenza_e_ICAO()
    {
        var tutte = new[]
        {
            Freq(1, "LIRF_TWR", "118.700", "LIRF"),
            Freq(2, "LIMM_CTR", "124.925"),
            Freq(3, "LIPZ_APP", "118.700", "LIPZ"),
        };

        Assert.Equal(new[] { "LIRF_TWR" }, Filtrate("lirf", tutte));           // callsign, senza badare al caso
        Assert.Equal(new[] { "LIRF_TWR", "LIPZ_APP" }, Filtrate("118.7", tutte)); // frequenza condivisa
        Assert.Equal(new[] { "LIPZ_APP" }, Filtrate("LIPZ", tutte));           // ICAO
        Assert.Empty(Filtrate("LFPG", tutte));
    }

    [Fact]
    public void Senza_testo_il_picker_mostra_tutto_ma_non_piu_di_cinquanta()
    {
        // ⚠️ Il tetto non è cosmetico: il catalogo collegabile è l'intera divisione, e senza si
        // disegnerebbero centinaia di righe dentro una tendina a ogni tasto premuto.
        var tante = Enumerable.Range(1, 120).Select(i => Freq(i, $"LI{i:D2}_TWR", "118.000")).ToArray();

        Assert.Equal(50, AirportFrequencyPicker.Filtra(tante, "").Count());
        Assert.Equal(50, AirportFrequencyPicker.Filtra(tante, null).Count());
        Assert.Equal(9, AirportFrequencyPicker.Filtra(tante, "LI0").Count());   // LI01…LI09
    }

    [Theory]
    [InlineData("TWR", "Tower")]
    [InlineData("gnd", "Ground")]
    [InlineData("DEL", "Delivery")]
    [InlineData("FSS", "Information")]
    public void Le_posizioni_note_hanno_un_nome_per_esteso(string posizione, string atteso) =>
        Assert.Equal(atteso, AirportFrequencyPicker.NomePosizione(posizione));

    [Fact]
    public void Una_posizione_sconosciuta_si_scrive_com_e_arrivata()
    {
        Assert.Equal("PLANNER", AirportFrequencyPicker.NomePosizione("PLANNER"));
        Assert.Equal("—", AirportFrequencyPicker.NomePosizione(null));
    }
}

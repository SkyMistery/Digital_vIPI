using System.Globalization;
using Vipi.Application.Weather;
using Vipi.Ui;
using Vipi.Ui.Shared;

namespace Vipi.Ui.Tests;

/// <summary>
/// Il METAR/TAF decodificato <b>parla la lingua della pagina</b>.
///
/// <para>Nasce da un difetto visto dal committente l'11 settembre 2026: una vIPI d'aeroporto in inglese
/// mostrava «leggera pioggia», «foschia», «Calmo». Le etichette (Vento/Visibilità/Nubi) erano tradotte da
/// sempre — erano i <b>valori</b> a non esserlo, perché le parole stavano cablate in italiano dentro
/// <c>MetarParser</c>, cioè in uno strato che la lingua di chi legge non la conosce.</para>
///
/// <para>⚠️ La traduzione arriva come funzione (<c>Func&lt;string,string&gt;</c>) apposta: la sezione METAR
/// di un documento a <b>lingua bloccata</b> non segue la cultura corrente ma quella del documento, ed è
/// un'isola interattiva, dove la cultura della richiesta non arriva comunque.</para>
/// </summary>
public class WxTextTests
{
    private static Func<string, string> In(string lingua) =>
        chiave => RisorseCondivise.Testo(chiave, CultureInfo.GetCultureInfo(lingua));

    private static IReadOnlyList<WeatherGroup> Tempo(string metar) =>
        MetarParser.ParseMetar(metar).Weather;

    [Fact] // il difetto, nella sua forma esatta
    public void Il_tempo_presente_segue_la_lingua()
    {
        var tempo = Tempo("LIML 010620Z 20015G25KT CAVOK M03/M05 Q0998 -RA");

        Assert.Equal("leggera pioggia", WxText.Weather(tempo, In("it")));
        Assert.Equal("light rain", WxText.Weather(tempo, In("en")));
    }

    [Fact] // il vento calmo era l'unica parola dentro ParsedWind.Label
    public void Il_vento_calmo_segue_la_lingua()
    {
        var calmo = MetarParser.ParseMetar("LIRA 010000Z 00000KT 4000 BR 10/09 Q1020").Wind;

        Assert.Equal("Calmo", WxText.Wind(calmo, In("it")));
        Assert.Equal("Calm", WxText.Wind(calmo, In("en")));
    }

    [Fact] // il resto del vento è neutro: non deve cambiare da una lingua all'altra
    public void Il_vento_in_cifre_e_uguale_nelle_due_lingue()
    {
        var m = MetarParser.ParseMetar("LIRF 191250Z 16012KT 9999 FEW035 26/14 Q1015");
        Assert.Equal("160° / 12 kt", WxText.Wind(m.Wind, In("it")));
        Assert.Equal("160° / 12 kt", WxText.Wind(m.Wind, In("en")));

        var vrb = MetarParser.ParseMetar("LIRF 191250Z VRB03KT 9999 FEW035 26/14 Q1015");
        Assert.Equal("VRB / 3 kt", WxText.Wind(vrb.Wind, In("en")));

        Assert.Equal("—", WxText.Wind(null, In("en")));
    }

    [Fact] // più gruppi, intensità diverse, codici composti
    public void Piu_gruppi_si_uniscono_nell_ordine_del_bollettino()
    {
        var tempo = Tempo("LIRF 191250Z 16012KT 3000 +SHRA VCTS FZFG BKN012 10/08 Q1010");

        Assert.Equal("forte rovescio pioggia, in prossimità temporale, congelantesi nebbia",
            WxText.Weather(tempo, In("it")));
        Assert.Equal("heavy shower rain, in the vicinity thunderstorm, freezing fog",
            WxText.Weather(tempo, In("en")));
    }

    [Fact] // niente meteo ⇒ null, così la voce «Tempo» non si scrive affatto invece di scriversi vuota
    public void Senza_tempo_presente_non_ce_niente_da_scrivere()
    {
        var tempo = Tempo("LIRF 191250Z 16012KT 9999 FEW035 26/14 Q1015 NOSIG");
        Assert.Empty(tempo);
        Assert.Null(WxText.Weather(tempo, In("it")));
        Assert.Null(WxText.Weather(null, In("it")));
    }

    [Fact] // ⚠️ la guardia che conta fra sei mesi: un codice nuovo nel parser senza la sua riga nei .resx
    public void Ogni_codice_riconosciuto_ha_le_sue_parole_nelle_due_lingue()
    {
        // I 29 codici del parser, uno per uno: se ne aggiungono uno e scordano il .resx, il localizzatore
        // torna LA CHIAVE («Wx_XX») e a schermo compare un nome tecnico. Qui diventa rosso.
        foreach (var codice in new[]
                 {
                     "RA", "SN", "DZ", "GR", "GS", "SG", "PL", "IC", "SH", "TS", "FZ", "FG", "BR", "HZ", "FU",
                     "DU", "SA", "MI", "BC", "PR", "DR", "BL", "SQ", "FC", "PO", "VA", "SS", "DS", "UP",
                 })
        {
            var gruppo = new WeatherGroup(codice, WxIntensity.Moderate, new[] { codice });
            foreach (var lingua in new[] { "it", "en" })
            {
                var parola = WxText.Weather(new[] { gruppo }, In(lingua));
                Assert.False(parola == "Wx_" + codice, $"manca Wx_{codice} nel resx {lingua}");
            }
        }

        foreach (var chiave in new[] { "Wx_Calm", "Wx_Light", "Wx_Heavy", "Wx_Vicinity" })
            foreach (var lingua in new[] { "it", "en" })
                Assert.NotEqual(chiave, RisorseCondivise.Testo(chiave, CultureInfo.GetCultureInfo(lingua)));
    }
}

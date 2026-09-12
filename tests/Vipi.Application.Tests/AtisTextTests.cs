using Vipi.Application.Stats;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Lettera e ora dall'ATIS. I due bollettini «veri» qui sotto sono copiati dal traffico reale del
/// 12 settembre 2026 (LICC_TWR e LIEA_TWR): è la forma che le divisioni scrivono davvero.
/// </summary>
public class AtisTextTests
{
    private const string Catania =
        "voice.ivao.aero/licc_twr|This is Catania ATIS arrival and departure information ECHO at 1136. " +
        "Runway in use 26 Transition level 080 LICC 121120Z 05012KT 9999 SCT120 31/21 Q1018  " +
        "You have received ATIS information ECHO";

    [Fact]
    public void Legge_Lettera_E_Ora_Da_Un_Atis_Vero()
    {
        var info = AtisText.Leggi(Catania.Split('|'));
        Assert.Equal("E", info.Lettera);
        Assert.Equal("11:36z", info.Orario);
    }

    [Fact] // il nome NATO diventa la sua lettera, nelle due grafie di JULIET
    public void I_Nomi_Nato_Diventano_Lettere()
    {
        Assert.Equal("J", AtisText.Leggi(new[] { "x", "information JULIET at 1200" }).Lettera);
        Assert.Equal("J", AtisText.Leggi(new[] { "x", "information JULIETT at 1200" }).Lettera);
        Assert.Equal("W", AtisText.Leggi(new[] { "x", "INFORMATION WHISKY AT 0900" }).Lettera);
    }

    [Fact] // una lettera nuda vale come il nome per esteso
    public void Anche_La_Lettera_Nuda()
    {
        Assert.Equal("C", AtisText.Leggi(new[] { "x", "ATIS information C at 1010" }).Lettera);
    }

    [Fact] // 🔴 quando la frase non si riconosce non si dice NIENTE: una lettera indovinata è quella che il pilota ripete
    public void Frase_Non_Riconosciuta_Non_Si_Indovina()
    {
        var info = AtisText.Leggi(new[] { "x", "Buonasera, la torre di Cuneo saluta" });
        Assert.True(info.Vuoto);

        // «INFORMATION» seguito da una parola che non è un nome NATO né una lettera: niente lettera.
        Assert.Equal("", AtisText.Leggi(new[] { "x", "INFORMATION AVAILABLE ON REQUEST" }).Lettera);
    }

    [Fact] // un'ora impossibile non passa: 9999 non sono le 99:99
    public void Un_Ora_Impossibile_Non_Passa()
    {
        Assert.Equal("", AtisText.Leggi(new[] { "x", "information ALPHA at 9999" }).Orario);
        Assert.Equal("12:00z", AtisText.Leggi(new[] { "x", "information ALPHA at 1200" }).Orario);
    }

    [Fact] // ⚠️ la PRIMA riga è l'indirizzo del server voce e non è testo ATIS
    public void La_Prima_Riga_Non_Entra_Nel_Testo()
    {
        var testo = AtisText.Testo(Catania.Split('|'));
        Assert.DoesNotContain("voice.ivao.aero", testo);
        Assert.StartsWith("This is Catania", testo);
    }

    [Fact] // una riga sola: si tiene, o il messaggio resterebbe vuoto per una regola pensata per due
    public void Una_Riga_Sola_Si_Tiene()
    {
        Assert.Equal("Runway in use 04", AtisText.Testo(new[] { "Runway in use 04" }));
    }

    [Fact]
    public void Niente_Righe_Niente_Da_Dire()
    {
        Assert.True(AtisText.Leggi(null).Vuoto);
        Assert.True(AtisText.Leggi(Array.Empty<string>()).Vuoto);
        Assert.Equal("", AtisText.Testo(null));
    }
}

using Vipi.Application.Content;
using Vipi.Application.Coordinates;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// La coppia sessagesimale dei SOP militari: <c>N41°32'05.07''E015°43'42.47''</c> (carta vSOP militari §12b).
///
/// <para>⚠️ È una forma <b>diversa</b> dalle tredici del convertitore e dal DMS di Aurora: emisfero davanti,
/// secondi chiusi da due apici, longitudine attaccata alla latitudine. Il committente ha chiesto che nella
/// tabella delle radioassistenze si accetti <b>solo</b> questa.</para>
/// </summary>
public class SexagesimalPairTests
{
    [Fact]
    public void Legge_la_forma_scritta_dal_committente()
    {
        Assert.True(SexagesimalPair.TryParse("N41°32'05.07''E015°43'42.47''", out var lat, out var lon));

        Assert.Equal(41 + 32 / 60.0 + 5.07 / 3600.0, lat, 9);
        Assert.Equal(15 + 43 / 60.0 + 42.47 / 3600.0, lon, 9);
    }

    /// <summary>Chi scrive a mano usa quel che ha sulla tastiera: due apici, il doppio apice, il simbolo
    /// tipografico, o niente. Sono la stessa coordinata, e rifiutarne una è pedanteria che costa tempo.</summary>
    [Theory]
    [InlineData("N41°32'05.07''E015°43'42.47''")]
    [InlineData("N41°32'05.07\"E015°43'42.47\"")]
    [InlineData("N41°32'05.07″E015°43'42.47″")]
    [InlineData("N41°32'05.07E015°43'42.47")]
    [InlineData("N41° 32' 05.07''  E015° 43' 42.47''")]
    [InlineData("N41°32'05.07'', E015°43'42.47''")]
    public void Tollera_le_scritture_equivalenti(string testo)
    {
        Assert.True(SexagesimalPair.TryParse(testo, out var lat, out var lon));
        Assert.Equal(41.53474, lat, 4);
        Assert.Equal(15.72846, lon, 4);
    }

    /// <summary>L'ordine lo dicono le LETTERE, non la posizione: chi copia da un documento guarda quelle.</summary>
    [Fact]
    public void Accetta_la_longitudine_per_prima()
    {
        Assert.True(SexagesimalPair.TryParse("E015°43'42.47''N41°32'05.07''", out var lat, out var lon));
        Assert.Equal(41.53474, lat, 4);
        Assert.Equal(15.72846, lon, 4);
    }

    [Fact]
    public void Emisferi_sud_e_ovest_diventano_negativi()
    {
        Assert.True(SexagesimalPair.TryParse("S41°32'05.07''W015°43'42.47''", out var lat, out var lon));
        Assert.True(lat < 0);
        Assert.True(lon < 0);
    }

    /// <summary>
    /// ⚠️ <b>Sessagesimale soltanto</b>: un decimale valido qui è un NO. Non è pignoleria — è la decisione del
    /// committente, e accettare due forme vorrebbe dire non sapere più, guardando una tabella, in quale delle
    /// due è stata scritta una riga.
    /// </summary>
    [Theory]
    [InlineData("41.53474 15.72846")]
    [InlineData("N041.32.05.070E015.43.42.470")]   // il DMS di Aurora
    [InlineData("4132N01543E")]                    // ARINC
    [InlineData("")]
    [InlineData("qualcosa")]
    public void Rifiuta_tutto_il_resto(string testo)
    {
        Assert.False(SexagesimalPair.TryParse(testo, out _, out _));
    }

    /// <summary>Due latitudini non sono un punto: e senza questo controllo <c>N…N…</c> darebbe una coppia
    /// plausibile in cui la seconda metà finisce nella longitudine.</summary>
    [Fact]
    public void Due_angoli_dello_stesso_asse_non_sono_una_coppia()
    {
        Assert.False(SexagesimalPair.TryParse("N41°32'05.07''N15°43'42.47''", out _, out _));
    }

    /// <summary>61 primi non sono «quasi 62»: sono un refuso, e passarli darebbe un punto plausibile e
    /// sbagliato — il difetto che una coordinata non deve mai avere.</summary>
    [Theory]
    [InlineData("N41°61'05.07''E015°43'42.47''")]
    [InlineData("N41°32'61.00''E015°43'42.47''")]
    public void Rifiuta_primi_e_secondi_fuori_scala(string testo)
    {
        Assert.False(SexagesimalPair.TryParse(testo, out _, out _));
    }

    /// <summary>Gradi su DUE cifre in latitudine e TRE in longitudine: incolonnate si leggono, e una
    /// longitudine a due cifre si scambia per una latitudine a colpo d'occhio.</summary>
    [Fact]
    public void Scrive_nella_forma_del_committente()
    {
        var testo = SexagesimalPair.Format(41 + 32 / 60.0 + 5.07 / 3600.0, 15 + 43 / 60.0 + 42.47 / 3600.0);

        Assert.Equal("N41°32'05.07''E015°43'42.47''", testo);
    }

    /// <summary>Il giro completo: quel che si scrive è quel che si rilegge, ai centesimi di secondo (~30 cm).</summary>
    [Theory]
    [InlineData("N41°32'05.07''E015°43'42.47''")]
    [InlineData("N45°58'24.71''E012°25'42.60''")]
    [InlineData("S12°00'00.00''W003°04'05.06''")]
    public void Andata_e_ritorno(string testo)
    {
        Assert.True(SexagesimalPair.TryParse(testo, out var lat, out var lon));

        Assert.Equal(testo, SexagesimalPair.Format(lat, lon));
    }

    /// <summary>
    /// ⚠️ Il riporto: 59.999 secondi arrotondano a 60, e stampare <c>…59.60''</c> darebbe un sessagesimale che
    /// non esiste. È la stessa trappola già pagata in <c>DmsCoordinate.Format</c> — qui l'arrotondamento è
    /// uno solo, in centesimi di secondo.
    /// </summary>
    [Fact]
    public void Il_riporto_non_produce_sessanta_secondi()
    {
        var testo = SexagesimalPair.Format(41 + 59 / 60.0 + 59.999 / 3600.0, 12.0);

        Assert.Equal("N42°00'00.00''E012°00'00.00''", testo);
        Assert.DoesNotContain("60.00''", testo);
    }

    // ---- La resa in tabella (NavaidText) ---------------------------------------------------------------

    /// <summary>Le due forme chieste dal committente, e il caso che si sbaglia da solo: senza canale <b>non</b>
    /// si stampa «CH» seguito dal vuoto.</summary>
    [Theory]
    [InlineData("MNL", "99Y", "115.25", "MNL - CH 99Y (115.25)")]
    [InlineData("MNL", null, "115.25", "MNL - 115.25")]
    [InlineData("MNL", "", "115.25", "MNL - 115.25")]
    [InlineData("MNL", "99Y", null, "MNL - CH 99Y")]
    [InlineData("MNL", null, null, "MNL")]
    public void La_colonna_freq_si_compone_cosi(string code, string? ch, string? freq, string atteso) =>
        Assert.Equal(atteso, NavaidText.Freq(code, ch, freq));

    /// <summary>La forma con il tipo, per gli aeroporti alternati. ⚠️ Qui il canale va <b>senza</b> «CH».</summary>
    [Theory]
    [InlineData("MNL", "VORTACAN", "99Y", "115.25", "MNL VORTACAN - 99Y (115.25)")]
    [InlineData("MNL", "VOR", null, "115.25", "MNL VOR - 115.25")]
    public void La_colonna_navaids_degli_alternati_si_compone_cosi(
        string code, string tipo, string? ch, string? freq, string atteso) =>
        Assert.Equal(atteso, NavaidText.ConTipo(code, tipo, ch, freq));

    /// <summary>Mezza coppia non è una posizione: o ci sono tutte e due, o non si stampa niente.</summary>
    [Fact]
    public void Mezza_coordinata_non_si_stampa()
    {
        Assert.Equal("", NavaidText.Coordinate(41.5, null));
        Assert.Equal("", NavaidText.Coordinate(null, 15.7));
        Assert.NotEqual("", NavaidText.Coordinate(41.5, 15.7));
    }
}

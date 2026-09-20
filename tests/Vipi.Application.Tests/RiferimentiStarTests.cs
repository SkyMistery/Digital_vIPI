using Vipi.Application.Content;
using Vipi.Domain.Entities;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Gli ARRIVI citati nel testo (§A80): <c>[[STAR LIRF ELKA3A]]</c>. Il meccanismo è quello delle SID — radice
/// del nome, ultimo nome visto come ripiego — con una differenza che vale tutte le altre: <b>il verso sta nel
/// riferimento</b>, e le due famiglie si cercano in due tabelle diverse.
/// </summary>
public class RiferimentiStarTests
{
    private static AirportSidRowView Riga(string nome, string fix = "—") =>
        new("16", fix, nome, "—", "—", "—", "—", "—", "—");

    private static NomiProcedura Nomi(params (ProcedureKind Kind, string Icao, string[] Nomi)[] tabelle) =>
        new(tabelle.ToDictionary(t => (t.Kind, t.Icao),
                                 t => new AirportSidView(t.Nomi.Select(n => Riga(n)).ToList())));

    [Fact]
    public void Il_Riferimento_Di_Un_Arrivo_Si_Scrive_Con_La_Sua_Parola()
    {
        Assert.Equal("[[STAR LIRF ELKA3A]]", RiferimentiProcedura.Scrivi(ProcedureKind.Star, "lirf", "elka3a"));
        Assert.Equal("[[SID LIRF OST1E]]", RiferimentiProcedura.Scrivi(ProcedureKind.Sid, "lirf", "ost1e"));
    }

    [Fact]
    public void L_Arrivo_Prende_Il_Nome_Di_Oggi_Dalla_Tabella_Degli_Arrivi()
    {
        var nomi = Nomi((ProcedureKind.Star, "LIRF", new[] { "ELKA4A" }));

        Assert.Equal("ELKA4A", RiferimentiProcedura.Sostituisci("Attesa su [[STAR LIRF ELKA3A]].", nomi)
            !.Replace("Attesa su ", "").Replace(".", ""));
    }

    /// <summary>
    /// 🔴 La prova che regge tutto: lo stesso scalo, la stessa radice, due famiglie. Senza il verso nella
    /// chiave un riferimento d'arrivo prenderebbe il nome di una PARTENZA — e nessuno se ne accorgerebbe
    /// leggendo, perché il nome sembra giusto.
    /// </summary>
    [Fact]
    public void La_Stessa_Radice_Nei_Due_Versi_Non_Si_Confonde()
    {
        var nomi = Nomi((ProcedureKind.Sid, "LIRF", new[] { "OST2E" }),
                        (ProcedureKind.Star, "LIRF", new[] { "OST7E" }));

        Assert.Equal("parte OST2E, arriva OST7E",
            RiferimentiProcedura.Sostituisci("parte [[SID LIRF OST1E]], arriva [[STAR LIRF OST1E]]", nomi));
    }

    [Fact]
    public void Un_Arrivo_Sparito_Esce_Con_L_Ultimo_Nome_Visto()
    {
        // Nessuna tabella d'arrivi: il riferimento non esce mai grezzo.
        var nomi = Nomi((ProcedureKind.Sid, "LIRF", new[] { "ELKA4A" }));

        Assert.Equal("ELKA3A", RiferimentiProcedura.Sostituisci("[[STAR LIRF ELKA3A]]", nomi));
        Assert.Equal("ELKA3A", RiferimentiProcedura.Sostituisci("[[STAR LIRF ELKA3A]]", null));
    }

    [Fact]
    public void Le_Tabelle_Citate_Sono_Una_Per_Verso()
    {
        var citate = RiferimentiProcedura.TabelleCitate(new[]
        {
            "Parte con [[SID LIRF OST1E]] e arriva con [[STAR LIRF ELKA3A]].",
            """{"rows":[{"cells":["[[STAR LIRA TIBE1A]]"]}]}""",
        });

        Assert.Equal(3, citate.Count);
        Assert.Contains((ProcedureKind.Sid, "LIRF"), citate);
        Assert.Contains((ProcedureKind.Star, "LIRF"), citate);
        Assert.Contains((ProcedureKind.Star, "LIRA"), citate);
    }

    [Fact]
    public void L_Avviso_Dell_Editor_Dice_Anche_Il_Verso()
    {
        var nomi = Nomi((ProcedureKind.Sid, "LIRF", new[] { "OST2E" }));

        var daRivedere = ControlloProcedureCitate.Controlla(new[]
        {
            ("Arrivi", (string?)"[[STAR LIRF ELKA3A]]"),
            ("Partenze", (string?)"[[SID LIRF OST1E]]"),
        }, nomi);

        // La partenza si risolve; l'arrivo no, e la voce lo dice con la sua parola.
        var r = Assert.Single(daRivedere);
        Assert.Equal(ProcedureKind.Star, r.Kind);
        Assert.Equal("STAR", r.Parola);
        Assert.Equal("ELKA3A", r.Codice);
        Assert.Equal(ProceduraDaRivedereTipo.NonTrovata, r.Tipo);
        Assert.Equal(new[] { "Arrivi" }, r.Dove);
    }

    [Fact]
    public void Un_Testo_Senza_Riferimenti_Non_Chiede_Niente()
    {
        // La via breve vale per tutti e due i gettoni: nessun riferimento, nessuna tabella da leggere.
        Assert.Empty(RiferimentiProcedura.TabelleCitate(new[] { "Nessuna procedura qui.", null }));
        Assert.False(RiferimentiProcedura.Contiene("Nessuna procedura qui."));
        Assert.True(RiferimentiProcedura.Contiene("[[STAR LIRF ELKA3A]]"));
    }
}

using Vipi.Application.Content;
using Vipi.Domain.Entities;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Quali NOTAM si mostrano in testa a un documento, e quando (carta
/// <c>docs/feature/2026-10-12-notam-sui-documenti.md</c>). Le regole sono del committente, 12 ottobre 2026; i NOTAM
/// di prova sono i tre veri in vigore quel giorno, con i loro aeroporti e centri.
/// </summary>
public class NotamSuiDocumentiTests
{
    private static readonly DateTime Oggi = new(2026, 10, 12, 15, 0, 0, DateTimeKind.Utc);
    private static int _prossimo = 1000;

    private static Notam Riga(string nome, string aeroporti, string centri, DateTime? inizio = null, DateTime? fine = null,
                              int id = 0) => new()
    {
        IvaoId = id == 0 ? Interlocked.Increment(ref _prossimo) : id, Name = nome, Airports = aeroporti, Centers = centri,
        StartUtc = inizio ?? new DateTime(2025, 11, 27), EndUtc = fine ?? new DateTime(2027, 1, 1), Text = "E) " + nome,
    };

    private static readonly Notam Firenze = Riga("IT-B002/25", "LIRQ", "LIRR");
    private static readonly Notam Bologna = Riga("IT-A003/25", "LIPC LIPE LIPK LIPR", "LIMM LIPP LIRR");
    private static readonly Notam Genova = Riga("IT-A002/26", "LIMG LIMJ", "LIMM",
        inizio: new DateTime(2026, 4, 16), fine: new DateTime(2026, 12, 31));
    private static readonly Notam[] Veri = { Firenze, Bologna, Genova };

    private static string[] Nomi(AmbitoNotam ambito, IEnumerable<Notam>? righe = null, DateTime? adesso = null) =>
        NotamInVista.Scegli(righe ?? Veri, ambito, adesso ?? Oggi).Select(a => a.Nome).ToArray();

    // ---- Quando si mostra ------------------------------------------------------------------------------

    /// <summary>Sette giorni prima dell'inizio compare, un minuto prima di quei sette giorni no.</summary>
    [Fact]
    public void Compare_sette_giorni_prima_di_entrare_in_vigore()
    {
        var inizio = new DateTime(2026, 10, 20, 0, 0, 0);

        Assert.False(NotamInVista.SiMostra(inizio, null, inizio.AddDays(-7).AddMinutes(-1)));
        Assert.True(NotamInVista.SiMostra(inizio, null, inizio.AddDays(-7)));
        Assert.True(NotamInVista.SiMostra(inizio, null, inizio));
        Assert.Equal(7, NotamInVista.GiorniDiAnticipo);
    }

    /// <summary>La fine è ESCLUSA: all'istante della scadenza non c'è più. È ciò che lo fa sparire da solo, anche
    /// se la sorgente lo tiene in elenco (lo fa: non toglie gli scaduti).</summary>
    [Fact]
    public void Alla_scadenza_sparisce()
    {
        var fine = new DateTime(2026, 12, 31, 0, 0, 0);

        Assert.True(NotamInVista.SiMostra(null, fine, fine.AddMinutes(-1)));
        Assert.False(NotamInVista.SiMostra(null, fine, fine));
        Assert.False(NotamInVista.SiMostra(null, fine, fine.AddDays(40)));
    }

    /// <summary>Senza inizio vale da sempre, senza fine non scade: la sorgente scrive così i NOTAM permanenti.</summary>
    [Fact]
    public void Senza_date_vale_sempre()
    {
        Assert.True(NotamInVista.SiMostra(null, null, Oggi));
        Assert.False(NotamInVista.InArrivo(null, Oggi));
    }

    [Fact]
    public void In_arrivo_finche_non_comincia()
    {
        var inizio = Oggi.AddDays(3);

        Assert.True(NotamInVista.InArrivo(inizio, Oggi));
        Assert.False(NotamInVista.InArrivo(inizio, inizio));
    }

    /// <summary>Lo scaduto del 31 agosto, ancora nell'elenco della sorgente a ottobre, non esce.</summary>
    [Fact]
    public void Lo_scaduto_rimasto_in_elenco_non_si_mostra()
    {
        var scaduto = Riga("ITW004/26", "LIBA LIBG LIBN LIBV", "LIBB LIRO LIRR LIVK LIZZ",
            inizio: new DateTime(2026, 8, 31, 8, 41, 0), fine: new DateTime(2026, 8, 31, 23, 0, 0));

        Assert.Empty(Nomi(AmbitoNotam.Aeroporti(new[] { "LIBA" }), new[] { scaduto }));
    }

    // ---- Chi riguarda ----------------------------------------------------------------------------------

    /// <summary>vIPI d'aeroporto, vSOP e APP: il NOTAM riguarda lo scalo.</summary>
    [Theory]
    [InlineData("LIRQ", "IT-B002/25")]
    [InlineData("LIPE", "IT-A003/25")]
    [InlineData("lipk", "IT-A003/25")]      // la rotta porta l'ICAO com'è scritto nell'indirizzo
    [InlineData("LIMJ", "IT-A002/26")]
    public void Lo_scalo_vede_i_suoi(string icao, string atteso) =>
        Assert.Equal(new[] { atteso }, Nomi(AmbitoNotam.Aeroporti(new[] { icao })));

    [Fact]
    public void Uno_scalo_che_nessun_NOTAM_nomina_non_vede_niente() =>
        Assert.Empty(Nomi(AmbitoNotam.Aeroporti(new[] { "LIRF" })));

    /// <summary>⚠️ Un ACC NON è uno scalo: la vIPI di Fiumicino non deve prendersi i NOTAM di Roma ACC solo perché
    /// il codice comincia uguale, e uno scalo non si cerca fra i centri.</summary>
    [Fact]
    public void Lo_scalo_non_si_cerca_fra_i_centri() =>
        Assert.Empty(Nomi(AmbitoNotam.Aeroporti(new[] { "LIRR" })));

    /// <summary>Una pagina unita mostra in un riquadro solo i NOTAM di tutti i suoi scali, senza doppioni.</summary>
    [Fact]
    public void Piu_scali_insieme_un_NOTAM_una_volta()
    {
        var nomi = Nomi(AmbitoNotam.Aeroporti(new[] { "LIPE", "LIPR", "LIRQ" }));

        Assert.Equal(new[] { "IT-A003/25", "IT-B002/25" }, nomi.OrderBy(n => n).ToArray());
    }

    /// <summary>vIPI di ACC: il NOTAM riguarda l'ACC.</summary>
    [Fact]
    public void L_ACC_vede_quelli_che_lo_nominano()
    {
        Assert.Equal(new[] { "IT-A002/26", "IT-A003/25" },
            Nomi(AmbitoNotam.Acc("LIMM", Array.Empty<string>())).OrderBy(n => n).ToArray());
        Assert.Equal(new[] { "IT-A003/25" }, Nomi(AmbitoNotam.Acc("LIPP", Array.Empty<string>())));
    }

    /// <summary>
    /// «Per gli APP nelle vIPI di ACC vale quello detto per gli APP»: lo scalo di un APP che la vIPI contiene
    /// porta i suoi NOTAM anche se l'ACC non è fra i centri. Il caso è vero: IT-B002/25 nomina LIMM nel testo
    /// ma alla sorgente ha fra i centri la sola LIRR.
    /// </summary>
    [Fact]
    public void L_ACC_vede_anche_quelli_degli_scali_dei_suoi_APP()
    {
        var senza = Nomi(AmbitoNotam.Acc("LIBB", Array.Empty<string>()));
        var con = Nomi(AmbitoNotam.Acc("LIBB", new[] { AmbitoNotam.AeroportoDi("LIRQ_NN0_APP") }));

        Assert.Empty(senza);
        Assert.Equal(new[] { "IT-B002/25" }, con);
    }

    /// <summary>
    /// vLOA: tutte e due le parti (scelta del filone). I tre accordi che IT-A003/25 modifica davvero —
    /// LIMM-LIPE, LIPP-LIPE, LIRR-LIPE — lo vedono; un accordo di Milano con chi non c'entra, no.
    /// </summary>
    [Theory]
    [InlineData("LIMM", "LIPE", true)]
    [InlineData("LIPP", "LIPE", true)]
    [InlineData("LIRR", "LIPE", true)]
    [InlineData("LIPE", "LIMM", true)]      // il verso dell'accordo non conta
    [InlineData("LIMM", "LSAS", false)]
    [InlineData("LIMM", "LIMJ", false)]     // quello è di Genova
    public void L_accordo_vuole_tutte_e_due_le_parti(string casa, string vicino, bool atteso) =>
        Assert.Equal(atteso, Nomi(AmbitoNotam.Accordo(casa, vicino)).Contains("IT-A003/25"));

    [Fact]
    public void Un_accordo_senza_una_parte_non_vede_niente()
    {
        Assert.Empty(Nomi(AmbitoNotam.Accordo("LIMM", "")));
        Assert.Empty(Nomi(AmbitoNotam.Accordo(null, "LIPE")));
    }

    [Theory]
    [InlineData("LIRQ_NN0_APP", "LIRQ")]
    [InlineData("limj_ws0_app", "LIMJ")]
    [InlineData("LIRE", "LIRE")]            // il codice di un ente senza suffisso è già lo scalo
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Lo_scalo_di_un_callsign_e_la_sua_testa(string? callsign, string atteso) =>
        Assert.Equal(atteso, AmbitoNotam.AeroportoDi(callsign));

    // ---- In che ordine ---------------------------------------------------------------------------------

    /// <summary>Prima quelli in vigore, il più recente in cima; poi quelli in arrivo, il più vicino in cima.</summary>
    [Fact]
    public void Prima_in_vigore_poi_in_arrivo()
    {
        var righe = new[]
        {
            Riga("ARRIVA-DOPO", "LIRF", "", inizio: Oggi.AddDays(6), id: 1),
            Riga("VECCHIO", "LIRF", "", inizio: Oggi.AddDays(-300), id: 2),
            Riga("ARRIVA-PRIMA", "LIRF", "", inizio: Oggi.AddDays(2), id: 3),
            Riga("RECENTE", "LIRF", "", inizio: Oggi.AddDays(-1), id: 4),
            Riga("TROPPO-PRESTO", "LIRF", "", inizio: Oggi.AddDays(8), id: 5),
        };

        var scelti = NotamInVista.Scegli(righe, AmbitoNotam.Aeroporti(new[] { "LIRF" }), Oggi);

        Assert.Equal(new[] { "RECENTE", "VECCHIO", "ARRIVA-PRIMA", "ARRIVA-DOPO" }, scelti.Select(a => a.Nome));
        Assert.Equal(new[] { false, false, true, true }, scelti.Select(a => a.InArrivo));
    }

    /// <summary>L'avviso porta con sé aeroporti e centri del NOTAM: il riquadro li scrive sotto il testo.</summary>
    [Fact]
    public void L_avviso_porta_aeroporti_e_centri()
    {
        var a = Assert.Single(NotamInVista.Scegli(Veri, AmbitoNotam.Aeroporti(new[] { "LIPE" }), Oggi));

        Assert.Equal(new[] { "LIPC", "LIPE", "LIPK", "LIPR" }, a.Aeroporti);
        Assert.Equal(new[] { "LIMM", "LIPP", "LIRR" }, a.Centri);
    }

    // ---- Come si salvano gli elenchi -------------------------------------------------------------------

    [Fact]
    public void Gli_elenchi_si_salvano_maiuscoli_senza_doppi_e_in_ordine()
    {
        Assert.Equal("LIPC LIPE LIPR", Notam.Unisci(new[] { "lipr", " LIPE ", "LIPC", "LIPE", "" }));
        Assert.Equal(new[] { "LIPC", "LIPE", "LIPR" }, Notam.Dividi("LIPC LIPE LIPR"));
        Assert.Empty(Notam.Dividi(""));
        Assert.Empty(Notam.Dividi(null));
    }
}

using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain.Entities;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// I <b>dati</b> citati nel testo (carta <c>2026-09-20-riferimenti-ai-dati.md</c>): frequenze e nominativi, che
/// cambiano sotto una chiave stabile. La differenza con le procedure sta tutta qui: niente «ultimo valore
/// visto» nel gettone, e il ripiego è <b>la chiave</b>.
/// </summary>
public class RiferimentiDatoTests
{
    private static ValoriDato Valori(params (TipoDato, string, string)[] voci) => new(voci);

    [Fact]
    public void Il_Gettone_Si_Scrive_Col_Tipo_E_La_Chiave()
    {
        Assert.Equal("[[FREQ LIRF_TWR]]", RiferimentiDato.Scrivi(TipoDato.Frequenza, "lirf_twr"));
        Assert.Equal("[[ATC LIRR_CTR]]", RiferimentiDato.Scrivi(TipoDato.Nominativo, "LIRR_CTR"));
    }

    [Fact]
    public void Esce_Il_Valore_Di_Oggi()
    {
        var valori = Valori((TipoDato.Frequenza, "LIRF_TWR", "118.700"),
                            (TipoDato.Nominativo, "LIRR_CTR", "Roma Radar"));

        Assert.Equal("Contatta Roma Radar su 118.700.",
            RiferimentiDato.Sostituisci("Contatta [[ATC LIRR_CTR]] su [[FREQ LIRF_TWR]].", valori));
    }

    /// <summary>
    /// 🔴 Il ripiego è la CHIAVE, non un valore vecchio: `LIRF_TWR` dice sempre qualcosa di vero, e un
    /// riferimento non esce mai grezzo — nemmeno dove nessuno ha risolto niente.
    /// </summary>
    [Fact]
    public void Quel_Che_Non_Si_Trova_Esce_Con_La_Sua_Chiave()
    {
        Assert.Equal("Contatta LIRF_TWR.", RiferimentiDato.Sostituisci("Contatta [[FREQ LIRF_TWR]].", null));
        Assert.Equal("Contatta LIRF_TWR.",
            RiferimentiDato.Sostituisci("Contatta [[FREQ LIRF_TWR]].", Valori((TipoDato.Frequenza, "LIML_TWR", "118.100"))));
    }

    [Fact]
    public void Le_Piste_Hanno_La_Chiave_Di_Due_Pezzi()
    {
        var citati = RiferimentiDato.Citati(new[] { "Pista [[RWY LIRF 16L]] e punto [[FIX OST]]." });

        Assert.Contains((TipoDato.Pista, "LIRF 16L"), citati);
        Assert.Contains((TipoDato.Punto, "OST"), citati);
        // Escono come sono scritti: la chiave è il valore.
        Assert.Equal("Pista LIRF 16L e punto OST.",
            RiferimentiDato.Sostituisci("Pista [[RWY LIRF 16L]] e punto [[FIX OST]].", null));
    }

    [Fact]
    public void Un_Testo_Senza_Riferimenti_Non_Si_Tocca()
    {
        Assert.False(RiferimentiDato.Contiene("Nessun riferimento qui."));
        Assert.False(RiferimentiDato.Contiene("[[SID LIRF OST1E]]"));   // è dell'altra famiglia
        Assert.Empty(RiferimentiDato.Citati(new[] { "niente", null }));
    }

    [Fact]
    public void L_Avviso_Elenca_Solo_Quel_Che_Non_Si_Trova()
    {
        var valori = Valori((TipoDato.Frequenza, "LIRF_TWR", "118.700"));

        var daRivedere = ControlloDatiCitati.Controlla(new[]
        {
            ("Torre", (string?)"[[FREQ LIRF_TWR]] e [[FREQ LIRF_APP]]"),
            ("Note", (string?)"ancora [[FREQ LIRF_APP]]"),
        }, valori);

        var d = Assert.Single(daRivedere);
        Assert.Equal(TipoDato.Frequenza, d.Tipo);
        Assert.Equal("FREQ", d.Parola);
        Assert.Equal("LIRF_APP", d.Chiave);
        Assert.Equal(new[] { "Torre", "Note" }, d.Dove);
    }

    /// <summary>
    /// ⚠️ Piste e punti NON si segnalano: la loro chiave è il valore, esce sempre giusta, e un avviso a ogni
    /// riga sarebbe rumore. Il loro controllo arriva con la loro sorgente.
    /// </summary>
    [Fact]
    public void Piste_E_Punti_Non_Finiscono_Nell_Avviso()
    {
        var daRivedere = ControlloDatiCitati.Controlla(new[]
        {
            ("Piste", (string?)"[[RWY LIRF 16L]] e [[FIX OST]]"),
        }, ValoriDato.Vuoto);

        Assert.Empty(daRivedere);
    }

    // --- la porta sola ---------------------------------------------------------------------------------

    [Fact]
    public void Le_Due_Famiglie_Si_Sostituiscono_Nello_Stesso_Testo()
    {
        var risolti = new RiferimentiRisolti(
            new NomiProcedura(new Dictionary<(ProcedureKind, string), AirportSidView>
            {
                [(ProcedureKind.Sid, "LIRF")] = new(new[] { new AirportSidRowView("16L", "—", "OST2E", "—", "—", "—", "—", "—", "—") }),
            }),
            Valori((TipoDato.Frequenza, "LIRF_TWR", "118.700")));

        Assert.Equal("OST2E poi 118.700",
            Riferimenti.Sostituisci("[[SID LIRF OST1E]] poi [[FREQ LIRF_TWR]]", risolti));
        Assert.True(Riferimenti.Contiene("[[FREQ LIRF_TWR]]"));
        Assert.True(Riferimenti.Contiene("[[STAR LIRF ELKA3A]]"));
        Assert.False(Riferimenti.Contiene("niente"));
    }

    // --- il risolutore ---------------------------------------------------------------------------------

    private sealed class Catalogo : IFrequenzeDegliEnti
    {
        public int Chiamate { get; private set; }

        public Task<IReadOnlyList<LinkableFrequencyRow>> TutteAsync(CancellationToken ct = default)
        {
            Chiamate++;
            return Task.FromResult<IReadOnlyList<LinkableFrequencyRow>>(new[]
            {
                new LinkableFrequencyRow(1, "LIRF", "LIRF_TWR", "118.700", "Fiumicino Tower"),
                new LinkableFrequencyRow(2, null, "LIRR_CTR", "124.850", null),
            });
        }
    }

    private sealed class NienteProcedure : IProcedureReferenceResolver
    {
        public Task<NomiProcedura> PerVistaAsync(IEnumerable<SectionView> sezioni, bool pubblica,
            string? proprioIcao = null, AirportSidView? propriaTabella = null,
            AirportSidView? propriaTabellaStar = null, CancellationToken ct = default) =>
            Task.FromResult(NomiProcedura.Vuoto);

        public Task<NomiProcedura> PerTestiAsync(IEnumerable<string?> testi, CancellationToken ct = default) =>
            Task.FromResult(NomiProcedura.Vuoto);

        public Task<IReadOnlyList<ProceduraCitabile>> ElencoAsync(string icao, ProcedureKind kind = ProcedureKind.Sid,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ProceduraCitabile>>(Array.Empty<ProceduraCitabile>());
    }

    [Fact]
    public async Task Il_Risolutore_Chiede_Il_Catalogo_Una_Volta_Sola()
    {
        var catalogo = new Catalogo();
        var risolti = await new RiferimentiResolver(new NienteProcedure(), catalogo)
            .PerTestiAsync(new[] { "[[FREQ LIRF_TWR]] con [[ATC LIRR_CTR]] e ancora [[FREQ LIRR_CTR]]" });

        Assert.Equal(1, catalogo.Chiamate);
        Assert.Equal("118.700", risolti.Dati.Valore(TipoDato.Frequenza, "LIRF_TWR"));
        Assert.Equal("Fiumicino Tower", risolti.Dati.Valore(TipoDato.Nominativo, "LIRF_TWR"));
        // Senza nominativo IVAO vale il callsign, che è sempre vero.
        Assert.Equal("LIRR_CTR", risolti.Dati.Valore(TipoDato.Nominativo, "LIRR_CTR"));
    }

    /// <summary>La via breve: un testo che non cita dati non fa nessuna domanda.</summary>
    [Fact]
    public async Task Senza_Dati_Citati_Non_Si_Chiede_Niente()
    {
        var catalogo = new Catalogo();
        var risolti = await new RiferimentiResolver(new NienteProcedure(), catalogo)
            .PerTestiAsync(new[] { "Solo prosa, e una [[SID LIRF OST1E]]." });

        Assert.Equal(0, catalogo.Chiamate);
        Assert.True(risolti.Dati.Vuota);
    }
}

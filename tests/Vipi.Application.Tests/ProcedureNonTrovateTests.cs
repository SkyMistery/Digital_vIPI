using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// S5 (23 settembre 2026): le SID/STAR scritte fra i punti di un trasferimento che negli scali della sezione non si
/// trovano più. Regole decise dal committente (carta 2026-09-23-procedure-non-trovate-negli-accordi, §0):
/// manca oggi e nell'entrante → sempre; sparisce col ciclo entrante → solo negli ultimi 3 giorni; nuova
/// nell'entrante → mai.
/// </summary>
public class ProcedureNonTrovateTests
{
    private static readonly DateTime Cambio = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Lontano = Cambio.AddDays(-10);
    private static readonly DateTime Vicino = Cambio.AddDays(-2);

    private static NomiProcedura Archivio(params (ProcedureKind Kind, string Icao, string Nome, string Fix)[] righe) =>
        new(righe.GroupBy(r => (r.Kind, r.Icao)).ToDictionary(
            g => g.Key,
            g => new AirportSidView(g.Select(r => new AirportSidRowView("07", r.Fix, r.Nome, "—", "—", "—", "—", "—", "—")).ToList())));

    private static NomiAlCambio Entrante(NomiProcedura nomi, DateTime? cambio = null) => new(nomi, cambio ?? Cambio);

    private static AgreementRow Accordo(TransferFlowKind kind, string cops, params string[] scali) => new()
    {
        Id = 7, OwnerAccCode = "LIBB", Order = 1, SideA = new(1, "LIBB_ES_CTR"), SideB = new(2, "LIBD_CS0_APP"),
        Sections = new[]
        {
            new AgreementSectionRow
            {
                Id = 3, Kind = kind, Direction = AgreementDirection.AtoB, Order = 1,
                Airports = scali.Select((s, i) => new AgreementAirportRow(s, null, i + 1)).ToList(),
                Clauses = new[]
                {
                    new AgreementClauseRow { Id = 11, SectionId = 3, Order = 1, Cops = cops, LevelUnit = LevelUnit.Fl, LevelConstraint = LevelConstraint.AtOrBelow },
                },
            },
        },
    };

    private static readonly NomiProcedura ConErika = Archivio((ProcedureKind.Star, "LIRN", "ERIK1A", "ERIKA"));

    [Fact]
    public void Trovata_oggi_e_nell_entrante_niente_avviso()
    {
        var a = Accordo(TransferFlowKind.Arrival, "ERIKA 1A", "LIRN");
        Assert.Empty(ProceduraNeiPunti.NonTrovate(new[] { a }, ConErika, Entrante(ConErika), Vicino));
    }

    /// <summary>Cambia solo il numero: la radice c'è, il nome segue da solo, nessun avviso.</summary>
    [Fact]
    public void Stessa_radice_nell_entrante_niente_avviso()
    {
        var entrante = Archivio((ProcedureKind.Star, "LIRN", "ERIK2A", "ERIKA"));
        var a = Accordo(TransferFlowKind.Arrival, "ERIKA 1A", "LIRN");
        Assert.Empty(ProceduraNeiPunti.NonTrovate(new[] { a }, ConErika, Entrante(entrante), Vicino));
    }

    [Fact]
    public void Manca_oggi_e_nell_entrante_avviso_sempre()
    {
        var altra = Archivio((ProcedureKind.Star, "LIRN", "ERIK1B", "ERIKA"));
        var a = Accordo(TransferFlowKind.Arrival, "MAREL, ERIKA 1A", "LIRN");

        var n = Assert.Single(ProceduraNeiPunti.NonTrovate(new[] { a }, altra, Entrante(altra), Lontano));
        Assert.Equal("ERIKA 1A", n.Nome);
        Assert.Null(n.SparisceIl);
        Assert.Equal(7, n.Accordo.Id);
        Assert.Equal(3, n.Sezione.Id);
        Assert.Equal(11, n.Clausola.Id);
    }

    /// <summary>🔴 «2–3 giorni prima, non settimane»: il committente.</summary>
    [Fact]
    public void Sparisce_col_ciclo_entrante_solo_negli_ultimi_tre_giorni()
    {
        var entrante = Archivio((ProcedureKind.Star, "LIRN", "ERIK1B", "ERIKA"));
        var a = new[] { Accordo(TransferFlowKind.Arrival, "ERIKA 1A", "LIRN") };

        Assert.Empty(ProceduraNeiPunti.NonTrovate(a, ConErika, Entrante(entrante), Lontano));
        Assert.Empty(ProceduraNeiPunti.NonTrovate(a, ConErika, Entrante(entrante), Cambio.AddDays(-ProceduraNeiPunti.GiorniDiAnticipo).AddHours(-1)));

        var n = Assert.Single(ProceduraNeiPunti.NonTrovate(a, ConErika, Entrante(entrante), Vicino));
        Assert.Equal(Cambio, n.SparisceIl);
        Assert.Single(ProceduraNeiPunti.NonTrovate(a, ConErika, Entrante(entrante), Cambio.AddDays(-ProceduraNeiPunti.GiorniDiAnticipo)));
    }

    /// <summary>Scelta dai suggerimenti del form (ciclo entrante, S3): non è un errore di chi scrive.</summary>
    [Fact]
    public void Nuova_nell_entrante_mai_avviso()
    {
        var a = Accordo(TransferFlowKind.Arrival, "ERIKA 1A", "LIRN");
        Assert.Empty(ProceduraNeiPunti.NonTrovate(new[] { a }, NomiProcedura.Vuoto, Entrante(ConErika), Lontano));
    }

    /// <summary>Senza il servizio AIRAC non c'è una data: si segnala solo quel che manca già adesso.</summary>
    [Fact]
    public void Senza_data_del_cambio_solo_cio_che_manca_oggi()
    {
        var a = new[] { Accordo(TransferFlowKind.Arrival, "ERIKA 1A", "LIRN") };
        Assert.Empty(ProceduraNeiPunti.NonTrovate(a, ConErika, new NomiAlCambio(NomiProcedura.Vuoto, null), Vicino));
        Assert.Single(ProceduraNeiPunti.NonTrovate(a, NomiProcedura.Vuoto, new NomiAlCambio(NomiProcedura.Vuoto, null), Vicino));
    }

    /// <summary>Un arrivo cerca fra le STAR: una SID omonima non basta.</summary>
    [Fact]
    public void Il_verso_conta()
    {
        var sid = Archivio((ProcedureKind.Sid, "LIRN", "ERIK1A", "ERIKA"));
        Assert.Single(ProceduraNeiPunti.NonTrovate(new[] { Accordo(TransferFlowKind.Arrival, "ERIKA 1A", "LIRN") }, sid, Entrante(sid), Lontano));
        Assert.Empty(ProceduraNeiPunti.NonTrovate(new[] { Accordo(TransferFlowKind.Departure, "ERIKA 1A", "LIRN") }, sid, Entrante(sid), Lontano));
    }

    [Fact]
    public void Basta_uno_degli_scali_della_sezione()
    {
        var a = Accordo(TransferFlowKind.Arrival, "ERIKA 1A", "LIRI", "LIRN");
        Assert.Empty(ProceduraNeiPunti.NonTrovate(new[] { a }, ConErika, Entrante(ConErika), Vicino));
    }

    [Fact]
    public void Sezione_senza_scali_e_fix_non_si_controllano()
    {
        Assert.Empty(ProceduraNeiPunti.NonTrovate(new[] { Accordo(TransferFlowKind.Arrival, "ERIKA 1A") },
            NomiProcedura.Vuoto, Entrante(NomiProcedura.Vuoto), Vicino));
        Assert.Empty(ProceduraNeiPunti.NonTrovate(new[] { Accordo(TransferFlowKind.Arrival, "MAREL, ELB", "LIRN") },
            NomiProcedura.Vuoto, Entrante(NomiProcedura.Vuoto), Vicino));
    }
}

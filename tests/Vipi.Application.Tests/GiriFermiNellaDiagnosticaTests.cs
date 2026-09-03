using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Application.Diagnostics;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// I giri periodici fermi devono comparire nella <b>Diagnostica</b>, non solo nella pagina Sorgenti.
///
/// <para>
/// 🔴 <b>Perché.</b> Il segnale c'era già — <c>ImportHealth.Ferma</c> = ultimo successo più vecchio di due
/// cadenze — ma si vedeva <b>solo aprendo la pagina Sorgenti</b>. La Diagnostica, che è la pagina che si
/// apre per chiedere «c'è qualcosa che non va?», degli import non sapeva niente: il 2 settembre 2026
/// diceva <b>«Avvio 0»</b> mentre metà dei giri periodici non partiva, perché su Plesk+Passenger il
/// processo si spegne per inattività prima che i giri arrivino al loro ritardo d'avvio.
/// </para>
///
/// <para>⚠️ Uno zero che rassicura sul contrario di quel che succede è peggio di nessun numero.</para>
/// </summary>
public class GiriFermiNellaDiagnosticaTests
{
    private static ImportOverviewRow Riga(string chiave, ImportHealth stato, DateTime? successo,
                                          string? errore = null) =>
        new(Categoria: ImportCategory.Sectors, Anagrafica: null, StateKey: chiave, DaSorgente: true,
            Stato: stato, UltimoSuccessoUtc: successo, UltimoTentativoUtc: successo,
            UltimoErrore: errore, Cadenza: TimeSpan.FromHours(24));

    [Fact]
    public void Un_giro_FERMO_diventa_un_rilievo()
    {
        var righe = new[] { Riga("Acc", ImportHealth.Ferma, DateTime.UtcNow.AddDays(-3)) };

        var rilievi = ConsistencyReportService.GiriFermi(righe);

        var r = Assert.Single(rilievi);
        Assert.Equal("Acc", r.Entity);
        Assert.Equal(ConsistencySeverity.Warning, r.Severity);
        // ⚠️ Area Avvio, e la scelta è deliberata: «l'istanza gira, ma non è partita intera» descrive
        // letteralmente questo caso, e il destinatario è chi guarda il processo — non chi apre un editor.
        Assert.Equal(ConsistencyArea.Avvio, r.Area);
        Assert.Contains("3 giorni fa", r.Detail);
    }

    [Fact]
    public void I_giri_che_GIRANO_non_dicono_niente()
    {
        var righe = new[]
        {
            Riga("Acc", ImportHealth.Aggiornata, DateTime.UtcNow.AddHours(-1)),
            Riga("Sid", ImportHealth.Esclusa, null),
            Riga("Nav", ImportHealth.SuRichiesta, null),
        };

        Assert.Empty(ConsistencyReportService.GiriFermi(righe));
    }

    /// <summary>
    /// ⚠️ <c>InErrore</c> resta fuori: ha già il suo messaggio con la <b>causa vera</b>, e ripeterlo qui
    /// direbbe due volte la stessa cosa in due aree diverse. Questo rilievo esiste per il caso muto —
    /// nessun errore, e il giro semplicemente non è mai partito.
    /// </summary>
    [Fact]
    public void Un_giro_IN_ERRORE_non_si_ripete_qui()
    {
        var righe = new[] { Riga("Acc", ImportHealth.InErrore, DateTime.UtcNow.AddDays(-3), "sorgente irraggiungibile") };

        Assert.Empty(ConsistencyReportService.GiriFermi(righe));
    }

    /// <summary>Un giro mai riuscito lo dice, invece di stampare un numero di giorni inventato.</summary>
    [Fact]
    public void Mai_riuscito_lo_dice_a_parole()
    {
        var righe = new[] { Riga("Acc", ImportHealth.Ferma, null) };

        Assert.Contains("mai", Assert.Single(ConsistencyReportService.GiriFermi(righe)).Detail);
    }
}

using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Il cuore della lista «Da fare», senza database: <b>quanto urge</b> una riga, <b>che cosa la chiude</b> e
/// <b>in che ordine</b> compare. Sono le tre decisioni della carta
/// (<c>docs/feature/2026-08-26-da-fare-una-lista-sola.md</c> §2-§3) e stanno qui perché una regola di
/// priorità che nessuno fissa è una regola che cambia da sola alla prima modifica.
/// </summary>
public class WorkListTests
{
    private static readonly DateTime Adesso = new(2026, 8, 26, 22, 0, 0, DateTimeKind.Utc);

    // ── Che cosa chiude una riga ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void La_copia_indietro_si_chiude_ripubblicando_non_spuntando()
    {
        // ⚠️ La decisione D3: un ✓ su una riga calcolata sarebbe un ping-pong col giro notturno, che la
        // riaprirebbe stanotte — e chi l'ha spuntata penserebbe che il tasto sia rotto.
        Assert.Equal(WorkAction.Ripubblica, ImpactKind.ReleaseDrift.AzioneCheChiude());
    }

    [Theory]
    [InlineData(ImpactKind.ReleaseKeyMoved)]
    [InlineData(ImpactKind.BrokenTarget)]
    [InlineData(ImpactKind.SectorStale)]
    public void Le_altre_calcolate_mandano_dove_si_decide(ImpactKind kind)
    {
        Assert.Equal(WorkAction.VaiASistemare, kind.AzioneCheChiude());
        Assert.False(kind.AzioneCheChiude() == WorkAction.SegnaFatto);
    }

    [Theory]
    [InlineData(ImpactKind.SectorGone)]
    [InlineData(ImpactKind.SectorHidden)]
    [InlineData(ImpactKind.SectorReparented)]
    [InlineData(ImpactKind.SectorDetached)]
    [InlineData(ImpactKind.AreaGone)]
    [InlineData(ImpactKind.AreaChanged)]
    public void Quelle_da_rileggere_le_spunta_una_persona(ImpactKind kind)
    {
        Assert.Equal(WorkAction.SegnaFatto, kind.AzioneCheChiude());
    }

    [Fact]
    public void L_azione_non_contraddice_mai_il_dominio()
    {
        // La mappatura CONSULTA i fatti di dominio invece di ridichiararli: se un giorno qualcuno aggiunge
        // un ImpactKind calcolato e si scorda di questo file, il ✓ non deve comparirgli sopra.
        foreach (var kind in Enum.GetValues<ImpactKind>())
            Assert.Equal(!kind.IsCalcolato(), kind.AzioneCheChiude() == WorkAction.SegnaFatto);
    }

    // ── Quanto urge ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Cio_che_e_gia_in_pubblico_batte_tutto()
    {
        // Il pubblico lo sta leggendo ADESSO, senza che nessuno abbia ripubblicato: viene prima di un
        // documento rotto, che almeno è muto.
        Assert.Equal(WorkSeverity.GiaInPubblico, ImpactKind.SectorGone.Severita(giaInPubblico: true));
        Assert.Equal(WorkSeverity.GiaInPubblico, ImpactKind.BrokenTarget.Severita(giaInPubblico: true));
    }

    [Fact]
    public void Le_altre_severita_seguono_la_natura_dell_impatto()
    {
        Assert.Equal(WorkSeverity.Rotto, ImpactKind.BrokenTarget.Severita(false));
        Assert.Equal(WorkSeverity.Rotto, ImpactKind.ReleaseKeyMoved.Severita(false));
        Assert.Equal(WorkSeverity.DaRipubblicare, ImpactKind.ReleaseDrift.Severita(false));
        Assert.Equal(WorkSeverity.DaRileggere, ImpactKind.SectorGone.Severita(false));
    }

    // ── L'ordine ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void In_cima_c_e_cio_che_fa_danno_adesso()
    {
        var righe = WorkOrdering.Ordina(new[]
        {
            Riga("e", WorkSeverity.Normale),
            Riga("d", WorkSeverity.DaRileggere),
            Riga("a", WorkSeverity.GiaInPubblico),
            Riga("c", WorkSeverity.DaRipubblicare),
            Riga("b", WorkSeverity.Rotto),
        });

        Assert.Equal(new[] { "a", "b", "c", "d", "e" }, righe.Select(r => r.Chiave));
    }

    [Fact]
    public void L_incarico_in_ritardo_sale_sopra_il_da_ripubblicare()
    {
        var righe = WorkOrdering.Ordina(new[]
        {
            Riga("drift", WorkSeverity.DaRipubblicare),
            Riga("tardi", WorkSeverity.InRitardo),
        });

        Assert.Equal("tardi", righe[0].Chiave);
    }

    [Fact]
    public void A_parita_di_urgenza_sale_la_piu_VECCHIA()
    {
        // ⚠️ Non la più recente: una segnalazione che nessuno guarda da tre settimane è esattamente quella
        // da vedere, e ordinando per novità scenderebbe in fondo ogni volta che ne arriva un'altra.
        var righe = WorkOrdering.Ordina(new[]
        {
            Riga("nuova", WorkSeverity.DaRileggere, Adesso),
            Riga("vecchia", WorkSeverity.DaRileggere, Adesso.AddDays(-21)),
        });

        Assert.Equal("vecchia", righe[0].Chiave);
    }

    [Fact]
    public void Due_righe_dello_stesso_istante_hanno_un_ordine_stabile()
    {
        // Senza il terzo criterio l'elenco potrebbe cambiare ordine fra due caricamenti identici, e le
        // righe ballerebbero sotto il dito di chi sta per premere.
        var a = WorkOrdering.Ordina(new[] { Riga("imp:2", WorkSeverity.DaRileggere, Adesso), Riga("imp:1", WorkSeverity.DaRileggere, Adesso) });
        var b = WorkOrdering.Ordina(new[] { Riga("imp:1", WorkSeverity.DaRileggere, Adesso), Riga("imp:2", WorkSeverity.DaRileggere, Adesso) });

        Assert.Equal(a.Select(r => r.Chiave), b.Select(r => r.Chiave));
        Assert.Equal("imp:1", a[0].Chiave);
    }

    // ── La riga ──────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Il_flag_si_offre_solo_dove_ha_senso()
    {
        Assert.True(Riga("x", WorkSeverity.DaRileggere) with { Azione = WorkAction.SegnaFatto } is { SiSpunta: true });
        Assert.False((Riga("x", WorkSeverity.DaRipubblicare) with { Azione = WorkAction.Ripubblica }).SiSpunta);
        Assert.False((Riga("x", WorkSeverity.Normale) with { Azione = WorkAction.CambiaStato }).SiSpunta);
    }

    [Fact]
    public void Una_riga_senza_URL_resta_in_lista()
    {
        // Un documento non raggiungibile è un'informazione, non un difetto da nascondere: sparire dalla
        // lista è il modo in cui un lavoro si dimentica.
        var senza = Riga("x", WorkSeverity.Rotto) with { Url = null };
        Assert.False(senza.SiRaggiunge);
    }

    private static WorkItem Riga(string chiave, WorkSeverity severita, DateTime? da = null) =>
        new(WorkOrigin.Sistema, chiave, 1, "vIPI Roma", "LIRR", "/x",
            "Impact_SectorGone", Array.Empty<string>(), severita, WorkAction.SegnaFatto, da ?? Adesso);
}

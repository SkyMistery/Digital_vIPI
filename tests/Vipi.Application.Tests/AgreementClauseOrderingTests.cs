using System.Collections.Generic;
using System.Linq;
using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// L'ordine dichiarato delle clausole di una sezione: alfabetico per punto, o per quota. È ciò che permette a una
/// clausola <b>ospite</b> (condivisa da un altro accordo) di stare al suo posto invece che in coda. Carta
/// <c>docs/feature/2026-10-06-sezioni-condivise.md</c> §11.
/// </summary>
public class AgreementClauseOrderingTests
{
    [Fact]
    public void A_mano_le_righe_restano_come_arrivano()
    {
        var righe = new[] { Riga(1, "VALMA", 130), Riga(2, "BIRSU", 110) };

        Assert.Same(righe, AgreementClauseOrdering.Sort(righe, AgreementClauseOrder.Manual));
    }

    [Fact]
    public void Alfabetico_per_punto_senza_guardare_le_maiuscole_e_coi_vuoti_in_fondo()
    {
        var righe = new[] { Riga(1, "VALMA", 130), Riga(2, "", 90), Riga(3, "birsu", 110), Riga(4, "GIKIN", 150) };

        var ordinate = AgreementClauseOrdering.Sort(righe, AgreementClauseOrder.Points);

        Assert.Equal(new[] { "birsu", "GIKIN", "VALMA", "" }, ordinate.Select(r => r.Cops));
    }

    [Fact]
    public void Il_posto_si_riscrive_perche_chi_legge_riordina_per_posto()
    {
        // AgreementExpansion e la pagina fanno OrderBy(Order): lasciato com'era, l'ordine dichiarato sparirebbe
        // alla prima rilettura.
        var righe = new[] { Riga(1, "VALMA", 130, posto: 1), Riga(2, "BIRSU", 110, posto: 2) };

        var ordinate = AgreementClauseOrdering.Sort(righe, AgreementClauseOrder.Points);

        Assert.Equal(new[] { ("BIRSU", 1), ("VALMA", 2) }, ordinate.Select(r => (r.Cops, r.Order)));
        // …ma il posto SALVATO resta quello: è lui che regge l'outline e che un annulla deve rimettere.
        Assert.Equal(new int?[] { 2, 1 }, ordinate.Select(r => r.StoredOrder));
    }

    [Fact]
    public void Un_gruppo_di_varianti_si_muove_intero_e_dentro_resta_com_era()
    {
        // Capofila, la sua eccezione, l'alternativa: l'ordine DENTRO il gruppo è la struttura.
        var righe = new[]
        {
            Riga(1, "VALMA", 130, gruppo: 7), Riga(2, "VALMA", 110, gruppo: 7, profondita: 1), Riga(3, "VALMA", 150, gruppo: 7),
            Riga(4, "BIRSU", 200),
        };

        var ordinate = AgreementClauseOrdering.Sort(righe, AgreementClauseOrder.Points);

        Assert.Equal(new[] { 4, 1, 2, 3 }, ordinate.Select(r => r.Id));
        Assert.Equal(new[] { 0, 0, 1, 0 }, ordinate.Select(r => r.VariantDepth));
    }

    [Fact]
    public void Per_quota_un_gruppo_conta_per_la_sua_prima_riga_e_non_si_spezza()
    {
        // L'eccezione a FL110 sta SOTTO la capofila a FL130 perché le appartiene: ordinata per conto suo finirebbe
        // prima della clausola a FL120, e non sarebbe più l'eccezione di nessuno.
        var righe = new[]
        {
            Riga(1, "VALMA", 130, gruppo: 7), Riga(2, "VALMA", 110, gruppo: 7, profondita: 1), Riga(3, "VALMA", 150, gruppo: 7),
            Riga(4, "BIRSU", 120),
        };

        var ordinate = AgreementClauseOrdering.Sort(righe, AgreementClauseOrder.Level);

        Assert.Equal(new[] { 4, 1, 2, 3 }, ordinate.Select(r => r.Id));
    }

    [Fact]
    public void Un_gruppo_spezzato_da_altre_righe_torna_insieme()
    {
        // Le ospiti arrivano in coda: una variante condivisa dopo le altre starebbe lontana dalle sorelle.
        var righe = new[] { Riga(1, "VALMA", 130, gruppo: 7), Riga(2, "AAAAA", 90), Riga(3, "VALMA", 150, gruppo: 7) };

        var ordinate = AgreementClauseOrdering.Sort(righe, AgreementClauseOrder.Points);

        Assert.Equal(new[] { 2, 1, 3 }, ordinate.Select(r => r.Id));
    }

    [Fact]
    public void Per_quota_dal_basso_con_i_piedi_e_i_livelli_sulla_stessa_scala_e_chi_non_ha_quota_in_fondo()
    {
        var righe = new[]
        {
            Riga(1, "VALMA", 130),                         // FL130 = 13000 ft
            Riga(2, "BIRSU", null),
            Riga(3, "GIKIN", 5000, LevelUnit.Feet),          // 5000 ft
            Riga(4, "ELKAP", 130),                         // pari quota: decide il punto
        };

        var ordinate = AgreementClauseOrdering.Sort(righe, AgreementClauseOrder.Level);

        Assert.Equal(new[] { "GIKIN", "ELKAP", "VALMA", "BIRSU" }, ordinate.Select(r => r.Cops));
    }

    // ---- Il verso opposto (committente, 9 ottobre 2026) ----

    [Fact]
    public void Dalla_Z_alla_A_capovolge_i_punti_ma_i_vuoti_restano_in_fondo()
    {
        // Il verso opposto capovolge chi un punto ce l'ha: una riga ancora da scrivere non sale in testa.
        var righe = new[] { Riga(1, "VALMA", 130), Riga(2, "", 90), Riga(3, "birsu", 110), Riga(4, "GIKIN", 150) };

        var ordinate = AgreementClauseOrdering.Sort(righe, AgreementClauseOrder.PointsDescending);

        Assert.Equal(new[] { "VALMA", "GIKIN", "birsu", "" }, ordinate.Select(r => r.Cops));
        Assert.Equal(new[] { 1, 2, 3, 4 }, ordinate.Select(r => r.Order));
    }

    [Fact]
    public void Dalla_quota_piu_alta_chi_non_ha_quota_resta_in_fondo_e_a_pari_quota_il_punto_va_dalla_A()
    {
        var righe = new[]
        {
            Riga(1, "VALMA", 130),
            Riga(2, "BIRSU", null),
            Riga(3, "GIKIN", 5000, LevelUnit.Feet),
            Riga(4, "ELKAP", 130),                         // pari quota: lo spareggio non si capovolge
        };

        var ordinate = AgreementClauseOrdering.Sort(righe, AgreementClauseOrder.LevelDescending);

        Assert.Equal(new[] { "ELKAP", "VALMA", "GIKIN", "BIRSU" }, ordinate.Select(r => r.Cops));
    }

    [Fact]
    public void Al_contrario_un_gruppo_di_varianti_si_muove_intero_e_dentro_NON_si_capovolge()
    {
        // L'ordine dentro il gruppo è la struttura: capofila, la sua eccezione, l'alternativa. Capovolto,
        // l'eccezione starebbe sopra la riga di cui è eccezione.
        var righe = new[]
        {
            Riga(4, "BIRSU", 200),
            Riga(1, "VALMA", 130, gruppo: 7), Riga(2, "VALMA", 110, gruppo: 7, profondita: 1), Riga(3, "VALMA", 150, gruppo: 7),
        };

        var ordinate = AgreementClauseOrdering.Sort(righe, AgreementClauseOrder.PointsDescending);

        Assert.Equal(new[] { 1, 2, 3, 4 }, ordinate.Select(r => r.Id));
    }

    [Theory]
    [InlineData(AgreementClauseOrder.Manual, AgreementClauseOrder.Manual, false, AgreementClauseOrder.Manual)]
    [InlineData(AgreementClauseOrder.Points, AgreementClauseOrder.Points, false, AgreementClauseOrder.PointsDescending)]
    [InlineData(AgreementClauseOrder.PointsDescending, AgreementClauseOrder.Points, true, AgreementClauseOrder.Points)]
    [InlineData(AgreementClauseOrder.Level, AgreementClauseOrder.Level, false, AgreementClauseOrder.LevelDescending)]
    [InlineData(AgreementClauseOrder.LevelDescending, AgreementClauseOrder.Level, true, AgreementClauseOrder.Level)]
    public void Chiave_verso_e_ordine_opposto(AgreementClauseOrder ordine, AgreementClauseOrder chiave, bool alContrario,
        AgreementClauseOrder opposto)
    {
        Assert.Equal(chiave, AgreementClauseOrdering.KeyOf(ordine));
        Assert.Equal(alContrario, AgreementClauseOrdering.IsDescending(ordine));
        Assert.Equal(opposto, AgreementClauseOrdering.Reverse(ordine));
    }

    [Fact]
    public void I_nomi_salvati_non_cambiano_e_stanno_nella_colonna()
    {
        // ⚠️ L'ordine è salvato PER NOME in una colonna di testo (32 caratteri su MySQL): rinominare un valore
        // lascerebbe nel database righe che non si leggono più, e un nome più lungo non entrerebbe.
        Assert.Equal(
            new[] { "Manual", "Points", "Level", "PointsDescending", "LevelDescending" },
            System.Enum.GetNames<AgreementClauseOrder>());
        Assert.All(System.Enum.GetNames<AgreementClauseOrder>(), n => Assert.True(n.Length <= 32));
    }

    private static AgreementClauseRow Riga(int id, string cops, int? quota, LevelUnit unita = LevelUnit.Fl,
        int? gruppo = null, int profondita = 0, int? posto = null) => new()
    {
        Id = id, SectionId = 1, Cops = cops, LevelValue = quota, LevelUnit = unita, LevelConstraint = LevelConstraint.Exact,
        VariantGroup = gruppo, VariantDepth = profondita, Order = posto ?? id, StoredOrder = posto ?? id,
    };
}

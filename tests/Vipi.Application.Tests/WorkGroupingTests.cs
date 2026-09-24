using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// La lista «Da fare» letta per cambiamento o per documento, senza database. Carta
/// <c>docs/feature/2026-09-23-da-fare-per-cambiamento.md</c>: le regole §0 (che cosa si raggruppa con che cosa) e
/// §2 (ordine, ✓ di gruppo).
/// </summary>
public class WorkGroupingTests
{
    private static readonly DateTime Adesso = new(2026, 9, 23, 20, 0, 0, DateTimeKind.Utc);
    private static int _id;

    private static WorkItem Impatto(ImpactKind tipo, int doc, string sorgente, params string[] args)
    {
        var id = Interlocked.Increment(ref _id);
        return new WorkItem(WorkOrigin.Sistema, $"imp:{id}", doc, $"Doc {doc}", "LIRR", $"/doc/{doc}",
            $"Impact_{tipo}", args, tipo.Severita(false), tipo.AzioneCheChiude(), Adesso.AddMinutes(-id),
            ImpactId: id, Tipo: tipo, Sorgente: sorgente);
    }

    private static WorkItem Incarico(int? doc, ImpactKind? tipo = null, string? sorgente = null)
    {
        var id = Interlocked.Increment(ref _id);
        return new WorkItem(WorkOrigin.Persona, $"task:{id}", doc, "Incarico", null, null,
            WorkPhrases.Raw, new[] { "Incarico" }, WorkSeverity.Normale, WorkAction.CambiaStato, Adesso,
            TaskId: id, Tipo: tipo, Sorgente: sorgente);
    }

    [Fact]
    public void La_stessa_area_su_sei_documenti_e_un_cambiamento_solo()
    {
        var righe = Enumerable.Range(1, 6)
            .Select(d => Impatto(ImpactKind.AreaChanged, d, "area:11164", "LI-R51"))
            .Append(Impatto(ImpactKind.AreaChanged, 1, "area:11165", "LI-R52"))
            .ToList();

        var gruppi = WorkGrouping.Raggruppa(righe, WorkView.Cambiamento);

        Assert.Equal(2, gruppi.Count);
        Assert.Equal(6, gruppi.Single(g => g.IsGruppo).Righe.Count);
    }

    [Fact]
    public void Stesso_callsign_ma_tipo_diverso_sono_due_cambiamenti()
    {
        var gruppi = WorkGrouping.Raggruppa(new[]
        {
            Impatto(ImpactKind.SectorGone, 1, "LIRR_TS_CTR", "LIRR_TS_CTR"),
            Impatto(ImpactKind.SectorReparented, 2, "LIRR_TS_CTR", "LIRR_TS_CTR"),
        }, WorkView.Cambiamento);

        Assert.Equal(2, gruppi.Count);
    }

    [Fact]
    public void Le_derive_con_la_stessa_frase_si_raggruppano_quelle_diverse_no()
    {
        // I dati del 4 settembre: sei documenti in deriva con lo stesso riassunto delle carte aeroportuali.
        var carte = "Carte aeroportuali, Carte aeroportuali / Aerodromo (+3)";
        var gruppi = WorkGrouping.Raggruppa(new[]
        {
            Impatto(ImpactKind.ReleaseDrift, 1, "LIBR", carte),
            Impatto(ImpactKind.ReleaseDrift, 2, "LIPA", carte),
            Impatto(ImpactKind.ReleaseDrift, 3, "LIRN", carte),
            Impatto(ImpactKind.ReleaseDrift, 4, "LIBD", "LVP, Regole piste"),
        }, WorkView.Cambiamento);

        Assert.Equal(2, gruppi.Count);
        Assert.Equal(3, gruppi.Single(g => g.IsGruppo).Righe.Count);
    }

    [Fact]
    public void Le_derive_con_la_stessa_causa_stanno_insieme_anche_con_sezioni_diverse()
    {
        // Carta §4: un trasferimento cambiato fa andare in deriva Roma (sezione Trasferimenti) e Brindisi (sezione
        // Coordinamenti). Frasi diverse, stessa causa: è UN lavoro su due documenti.
        var gruppi = WorkGrouping.Raggruppa(new[]
        {
            Impatto(ImpactKind.ReleaseDrift, 1, "LIRR", "Trasferimenti") with { Causa = "mod:20260923210400" },
            Impatto(ImpactKind.ReleaseDrift, 2, "LIBB", "Coordinamenti") with { Causa = "mod:20260923210400" },
            Impatto(ImpactKind.ReleaseDrift, 3, "LIMM", "Trasferimenti") with { Causa = "mod:20260923220000" },
            Impatto(ImpactKind.ReleaseDrift, 4, "LIPP", "Trasferimenti"),
        }, WorkView.Cambiamento);

        var perCausa = gruppi.Single(g => g.IsGruppo);
        Assert.True(perCausa.PerCausa);
        Assert.Equal(new[] { 1, 2 }, perCausa.Righe.Select(r => r.DocumentId!.Value).OrderBy(x => x));
        // L'altra causa e la riga senza causa restano per conto loro, anche se la frase è la stessa.
        Assert.Equal(3, gruppi.Count);
    }

    [Fact]
    public void La_causa_non_raggruppa_gli_eventi()
    {
        // Un evento ha già la sua causa nella sorgente: la finestra di modifiche vale solo per le derive.
        var a = Impatto(ImpactKind.AreaChanged, 1, "area:7", "LI-R7") with { Causa = "mod:1" };
        Assert.StartsWith("ev:", WorkGrouping.ChiaveCambiamento(a));
    }

    [Fact]
    public void La_virgola_nelle_sezioni_non_confonde_due_frasi()
    {
        var a = Impatto(ImpactKind.ReleaseDriftNextCycle, 1, "X", "2610", "A, B");
        var b = Impatto(ImpactKind.ReleaseDriftNextCycle, 2, "Y", "2610, A", "B");

        Assert.NotEqual(WorkGrouping.ChiaveCambiamento(a), WorkGrouping.ChiaveCambiamento(b));
    }

    [Theory]
    [InlineData(ImpactKind.BrokenTarget)]
    [InlineData(ImpactKind.ReleaseKeyMoved)]
    public void Un_guasto_di_un_documento_resta_solo(ImpactKind tipo)
    {
        var gruppi = WorkGrouping.Raggruppa(new[]
        {
            Impatto(tipo, 1, "chiave", "x"),
            Impatto(tipo, 2, "chiave", "x"),
        }, WorkView.Cambiamento);

        Assert.Equal(2, gruppi.Count);
        Assert.All(gruppi, g => Assert.False(g.IsGruppo));
    }

    [Fact]
    public void L_incarico_nato_da_una_segnalazione_sta_con_le_sorelle_quello_libero_da_solo()
    {
        var gruppi = WorkGrouping.Raggruppa(new[]
        {
            Impatto(ImpactKind.AreaChanged, 1, "area:7", "LI-R7"),
            Incarico(2, ImpactKind.AreaChanged, "area:7"),
            Incarico(null),
            Incarico(3),
        }, WorkView.Cambiamento);

        Assert.Equal(3, gruppi.Count);
        Assert.Equal(2, gruppi.Single(g => g.IsGruppo).Righe.Count);
    }

    [Fact]
    public void Per_documento_si_raggruppa_per_documento()
    {
        var gruppi = WorkGrouping.Raggruppa(new[]
        {
            Impatto(ImpactKind.AreaChanged, 1, "area:7", "LI-R7"),
            Impatto(ImpactKind.AreaChanged, 1, "area:8", "LI-R8"),
            Impatto(ImpactKind.ReleaseDrift, 1, "LIRR", "Frequenze"),
            Impatto(ImpactKind.AreaChanged, 2, "area:7", "LI-R7"),
            Incarico(null),
        }, WorkView.Documento);

        Assert.Equal(3, gruppi.Count);
        Assert.Equal(3, gruppi.Single(g => g.IsGruppo).Righe.Count);
    }

    [Fact]
    public void Elenco_e_una_riga_per_gruppo()
    {
        var righe = Enumerable.Range(1, 4).Select(d => Impatto(ImpactKind.AreaChanged, d, "area:7", "LI-R7")).ToList();

        Assert.Equal(4, WorkGrouping.Raggruppa(righe, WorkView.Elenco).Count);
    }

    [Fact]
    public void Il_gruppo_prende_l_urgenza_della_riga_piu_urgente_e_sta_sopra()
    {
        var gia = Impatto(ImpactKind.AreaChanged, 2, "area:7", "LI-R7") with { Severita = WorkSeverity.GiaInPubblico };
        var gruppi = WorkGrouping.Raggruppa(new[]
        {
            Impatto(ImpactKind.ReleaseDrift, 9, "LIRR", "Frequenze"),
            Impatto(ImpactKind.AreaChanged, 1, "area:7", "LI-R7"),
            gia,
        }, WorkView.Cambiamento);

        Assert.True(gruppi[0].IsGruppo);
        Assert.Same(gia, gruppi[0].Capo);
    }

    [Fact]
    public void A_parita_di_urgenza_il_gruppo_piu_vecchio_sta_sopra()
    {
        var nuova = Impatto(ImpactKind.AreaChanged, 1, "area:1", "A") with { Da = Adesso };
        var vecchia = Impatto(ImpactKind.AreaChanged, 2, "area:2", "B") with { Da = Adesso.AddDays(-30) };

        var gruppi = WorkGrouping.Raggruppa(new[] { nuova, vecchia }, WorkView.Cambiamento);

        Assert.Same(vecchia, gruppi[0].Capo);
    }

    [Fact]
    public void Il_ok_di_gruppo_c_e_solo_se_ogni_riga_si_spunta()
    {
        var aree = WorkGrouping.Raggruppa(new[]
        {
            Impatto(ImpactKind.AreaChanged, 1, "area:7", "LI-R7"),
            Impatto(ImpactKind.AreaChanged, 2, "area:7", "LI-R7"),
        }, WorkView.Cambiamento).Single();
        Assert.True(aree.SiSpuntanoTutte);

        var misto = WorkGrouping.Raggruppa(new[]
        {
            Impatto(ImpactKind.AreaChanged, 1, "area:7", "LI-R7"),
            Impatto(ImpactKind.ReleaseDrift, 1, "LIRR", "Frequenze"),
        }, WorkView.Documento).Single();
        Assert.False(misto.SiSpuntanoTutte);

        var conIncarico = WorkGrouping.Raggruppa(new[]
        {
            Impatto(ImpactKind.AreaChanged, 1, "area:7", "LI-R7"),
            Incarico(2, ImpactKind.AreaChanged, "area:7"),
        }, WorkView.Cambiamento).Single();
        Assert.False(conIncarico.SiSpuntanoTutte);
    }
}

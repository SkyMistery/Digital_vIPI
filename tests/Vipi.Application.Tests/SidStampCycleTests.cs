using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain.Services;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Da quale ciclo è in vigore una SID appena prelevata (carta 2026-09-02 §AW2). Il servizio AIRAC è quello
/// vero: i cicli sono aritmetica, non c'è niente da simulare.
///
/// <para>Le date usate sono quelle vere del 2026, misurate girando <c>AiracService</c>: <b>2608</b> comincia
/// il 6 agosto, <b>2609</b> il 3 settembre, <b>2610</b> il 1º ottobre.</para>
/// </summary>
public class SidStampCycleTests
{
    private static readonly IAiracService Airac = new AiracService();

    private static readonly DateTime IlDueSettembre = new(2026, 9, 2, 18, 0, 0, DateTimeKind.Utc);    // ciclo 2608
    private static readonly DateTime IlTreSettembre = new(2026, 9, 3, 2, 0, 0, DateTimeKind.Utc);     // ciclo 2609
    private static readonly DateTime IlPrimoSettembre = new(2026, 9, 1, 12, 55, 0, DateTimeKind.Utc); // ciclo 2608

    private static SidSourceRelease Dichiara(string ciclo) => new(ciclo, null);
    private static SidSourceRelease Cambiata(DateTime quando) => new(null, quando);

    /// <summary>
    /// Il dato buono, e nessuno lo chiedeva: la sorgente il proprio ciclo lo <b>dichiara</b>. Misurato sul
    /// repo vero il 2 settembre 2026, il changelog più alto era <c>2608.txt</c> — «AIRAC A2608 IN VIGORE DAL
    /// 06/08/2026». Quel contenuto vale <b>da 2608</b>, non «dal prossimo, chissà quale».
    /// </summary>
    [Fact]
    public void Il_ciclo_dichiarato_dalla_sorgente_vince_su_tutto() =>
        Assert.Equal("2608", SidStampCycle.Scegli(
            Airac, adessoUtc: IlTreSettembre, sorgente: Dichiara("2608"), ultimoGiroRiuscitoUtc: IlPrimoSettembre));

    /// <summary>
    /// E se la sorgente dichiara il ciclo <b>entrante</b>, la riga aspetta: è il verso che rende il
    /// meccanismo indolore quando la divisione pubblica in anticipo, come fa (il changelog del 2608 è stato
    /// scritto il 25 luglio, dodici giorni prima che il ciclo entrasse in vigore).
    /// </summary>
    [Fact]
    public void Un_ciclo_dichiarato_futuro_tiene_la_riga_in_attesa()
    {
        var entrata = SidStampCycle.Scegli(
            Airac, adessoUtc: IlDueSettembre, sorgente: Dichiara("2609"), ultimoGiroRiuscitoUtc: null);

        Assert.Equal("2609", entrata);
        var sid = Sid(entrata);
        Assert.False(sid.IsPublicAt(Airac.GetCycle(IlDueSettembre), Airac));   // il 2 settembre no
        Assert.True(sid.IsPublicAt(Airac.GetCycle(IlTreSettembre), Airac));    // il 3 sì, da sola
    }

    /// <summary>
    /// Il caso che ha originato la carta, nel ripiego. Senza ciclo dichiarato si guarda a quando i dati si
    /// sono mossi — l'1 settembre, dentro il 2608 — e si aggiunge il ciclo di attesa: entrata al 2609. Col
    /// vecchio meccanismo, un giro passato il 3 alle 02:00 avrebbe dato entrata al <b>2610</b>, cioè un mese
    /// di ritardo deciso da un ritentativo slittato.
    /// </summary>
    [Fact]
    public void Senza_ciclo_dichiarato_conta_quando_la_sorgente_e_cambiata() =>
        Assert.Equal("2609", SidStampCycle.Scegli(
            Airac, adessoUtc: IlTreSettembre, sorgente: Cambiata(IlPrimoSettembre), ultimoGiroRiuscitoUtc: null));

    [Fact]
    public void Sorgente_muta_si_appoggia_allultimo_giro_riuscito() =>
        Assert.Equal("2609", SidStampCycle.Scegli(
            Airac, adessoUtc: IlTreSettembre, sorgente: SidSourceRelease.Muta, ultimoGiroRiuscitoUtc: IlDueSettembre));

    /// <summary>
    /// ⚠️ I ripieghi sbagliano <b>per eccesso di fretta</b>, ed è la scelta scritta nella carta: il
    /// cambiamento era osservabile mentre non guardavamo, quindi il ritardo è nostro e non deve diventare del
    /// dato. Qui il giro è fermo dal 2 e il ciclo è girato: entrata al 2609, cioè la SID si vede <b>oggi</b>.
    /// </summary>
    [Fact]
    public void Il_ripiego_fa_uscire_prima_non_dopo()
    {
        var entrata = SidStampCycle.Scegli(
            Airac, adessoUtc: IlTreSettembre, sorgente: SidSourceRelease.Muta, ultimoGiroRiuscitoUtc: IlDueSettembre);

        Assert.True(Sid(entrata).IsPublicAt(Airac.GetCycle(IlTreSettembre), Airac));
    }

    [Fact]
    public void Senza_niente_di_noto_si_aspetta_il_ciclo_dopo_quello_corrente() =>
        Assert.Equal("2609", SidStampCycle.Scegli(
            Airac, adessoUtc: IlDueSettembre, sorgente: SidSourceRelease.Muta, ultimoGiroRiuscitoUtc: null));

    /// <summary>
    /// ⚠️ Una data di cambiamento nel futuro — orologi storti, un commit datato male — spingerebbe l'entrata
    /// in avanti di un ciclo intero. Si taglia ad adesso, e l'attesa resta una sola.
    /// </summary>
    [Fact]
    public void Una_data_nel_futuro_si_taglia_ad_adesso() =>
        Assert.Equal("2609", SidStampCycle.Scegli(
            Airac, adessoUtc: IlDueSettembre,
            sorgente: Cambiata(new DateTime(2027, 5, 1, 0, 0, 0, DateTimeKind.Utc)),
            ultimoGiroRiuscitoUtc: null));

    /// <summary>
    /// ⚠️ Un ciclo dichiarato illeggibile non deve far cadere un import: <c>EffectiveUtcForCycle</c> solleva su
    /// una stringa che non è <c>YYNN</c>, e qualcuno può rinominare un file di changelog. Si scende al gradino dopo.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bozza")]
    [InlineData("26099")]
    public void Un_ciclo_dichiarato_illeggibile_scende_al_ripiego(string spazzatura) =>
        Assert.Equal("2609", SidStampCycle.Scegli(
            Airac, adessoUtc: IlDueSettembre,
            sorgente: new SidSourceRelease(spazzatura, null),
            ultimoGiroRiuscitoUtc: null));

    private static SidRow Sid(string entrata) =>
        new(1, null, "ELB", "ELBA1A", null, null, null, null, null, null,
            IsImported: true, SourceAiracCycle: entrata);
}

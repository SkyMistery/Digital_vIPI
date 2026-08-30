using System.Text.Json;
using Vipi.Application.Media;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Lo scanner dei riferimenti agli allegati: è quello che risponde «chi cita questa voce?», cioè la domanda
/// da cui dipende ogni cancellazione e ogni sostituzione.
///
/// <para>⚠️ I due errori possibili <b>non si equivalgono</b>. Riconoscere una citazione di troppo tiene in
/// vita una voce che nessuno usa più — spazio sprecato, il problema che stiamo già tollerando. Riconoscerne
/// una di meno autorizza a cancellare una voce ancora citata, e rompe in silenzio un documento
/// <b>pubblicato</b>. Da qui la larghezza deliberata, e i confini che invece devono essere esatti.</para>
/// </summary>
public class AttachmentReferenceScannerTests
{
    [Fact]
    public void Trova_il_riferimento_nella_prosa() =>
        Assert.Equal(new[] { "loa-lirr-lfmm" },
            AttachmentReferenceScanner.Scan("Vedi la [LoA Marseille](allegato:loa-lirr-lfmm) per i dettagli."));

    [Fact]
    public void Trova_il_riferimento_nel_json_di_un_blocco() =>
        Assert.Equal(new[] { "loa-lirr-lfmm" },
            AttachmentReferenceScanner.Scan("""{"ref":"allegato:loa-lirr-lfmm","titolo":"LoA"}"""));

    /// <summary>
    /// ⚠️ Il confine a destra è la cosa che si sbaglia. Senza, <c>loa-lirr</c> «vincerebbe» dentro
    /// <c>loa-lirr-bis</c>: la guardia direbbe che la voce sbagliata è citata, e chi cancella si fiderebbe.
    /// </summary>
    [Fact]
    public void Uno_slug_non_si_mangia_quello_che_gli_sta_attaccato() =>
        Assert.Equal(new[] { "loa-lirr-bis" },
            AttachmentReferenceScanner.Scan("[x](allegato:loa-lirr-bis)"));

    /// <summary>Il confine a sinistra: una parola che <i>finisce</i> col token non è una citazione.</summary>
    [Fact]
    public void Una_parola_che_contiene_il_token_non_e_una_citazione() =>
        Assert.Empty(AttachmentReferenceScanner.Scan("vecchio-allegato:loa-lirr-lfmm"));

    [Fact]
    public void Due_citazioni_dello_stesso_slug_contano_una() =>
        Assert.Equal(new[] { "loa-lirr-lfmm" },
            AttachmentReferenceScanner.Scan("allegato:loa-lirr-lfmm e ancora allegato:loa-lirr-lfmm"));

    [Fact]
    public void Piu_slug_diversi_escono_tutti() =>
        Assert.Equal(new[] { "loa-lirr-lfmm", "circolare-01" },
            AttachmentReferenceScanner.Scan("allegato:loa-lirr-lfmm, allegato:circolare-01"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nessun riferimento qui")]
    [InlineData("allegato:")]                 // il token senza slug non è una citazione
    [InlineData("allegato:LOA-LIRR")]         // maiuscole: non è uno slug valido, quindi non è una citazione
    public void Cio_che_non_e_una_citazione_non_esce(string? testo) =>
        Assert.Empty(AttachmentReferenceScanner.Scan(testo));

    /// <summary>
    /// ⚠️ <b>La trappola già pagata dalle immagini.</b> Dentro il payload di una release il JSON di un blocco
    /// è una stringa <i>annidata</i>: le sue virgolette diventano sequenze di escape, e il riferimento si
    /// ritrova incollato a quelle. Se lo scanner non le neutralizza, la citazione dentro un documento
    /// <b>pubblicato</b> non si trova — cioè proprio quella che conta di più.
    /// </summary>
    [Fact]
    public void Dentro_un_payload_di_release_il_riferimento_si_trova_lo_stesso()
    {
        var bodyJson = """{"ref":"allegato:loa-lirr-lfmm"}""";
        var payload = JsonSerializer.Serialize(new { blocchi = new[] { new { bodyJson } } });

        // Il payload contiene il JSON del blocco come stringa annidata: System.Text.Json scrive le sue
        // virgolette come una sequenza di escape, e le sue due cifre finali finiscono attaccate al riferimento.
        Assert.Contains("\\u0022allegato:", payload);
        Assert.Equal(new[] { "loa-lirr-lfmm" }, AttachmentReferenceScanner.Scan(payload));
    }

    /// <summary>Gli escape diventano un separatore, non spariscono: togliendoli, due pezzi di testo ai loro
    /// lati si salderebbero e produrrebbero uno slug che nessuno ha scritto.</summary>
    [Fact]
    public void Un_escape_separa_invece_di_saldare() =>
        Assert.Equal(new[] { "loa-lirr" }, AttachmentReferenceScanner.Scan(@"allegato:loa-lirr\nlfmm"));

    [Fact]
    public void Scan_all_unisce_i_testi_in_un_insieme_solo()
    {
        var slug = AttachmentReferenceScanner.ScanAll(new[]
        {
            "allegato:loa-lirr-lfmm", null, "allegato:circolare-01", "allegato:loa-lirr-lfmm",
        });

        Assert.Equal(2, slug.Count);
        Assert.Contains("loa-lirr-lfmm", slug);
        Assert.Contains("circolare-01", slug);
    }
}

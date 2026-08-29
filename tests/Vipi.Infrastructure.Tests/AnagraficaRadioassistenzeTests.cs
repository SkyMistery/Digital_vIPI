using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// L'anagrafica delle radioassistenze (carta <c>2026-08-27-vsop-militari.md</c> §12b): scritta una volta,
/// esce uguale ovunque.
///
/// <para>Questi test provano le <b>quattro decisioni del committente</b>, che sono la ragione per cui
/// l'anagrafica esiste e non sono deducibili dai nomi dei metodi: la fonte vince sempre, l'assenza non
/// cancella, si scrivono i campi toccati e non la riga, e il registro porta il valore vecchio e quello nuovo.</para>
/// </summary>
public class AnagraficaRadioassistenzeTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfNavaidCatalog _cat = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _cat = new EfNavaidCatalog(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private static SourceNavaid Mnl(string? freq = "115.25", string? ch = "99Y",
        double? lat = 41.5476, double? lon = 15.6898) => new("MNL", "VHF", freq, ch, lat, lon);

    // ---- La fonte vince sempre -------------------------------------------------------------------------

    /// <summary>
    /// ⚠️ La decisione del committente alla lettera: se il campo viene dalla sorgente, la correzione a mano
    /// <b>non va a buon fine</b>. Non «avvisa e scrive»: al primo giro d'import sparirebbe senza spiegazioni,
    /// e chi l'ha scritta penserebbe di aver salvato.
    /// </summary>
    [Fact]
    public async Task Un_campo_della_sorgente_non_si_corregge_a_mano()
    {
        await _cat.ImportFromSourceAsync(new[] { Mnl() });
        var riga = (await _cat.ListAsync()).Single();

        Assert.Equal(NavaidWrite.DallaSorgente, await _cat.SetFrequencyAsync(riga.Id, "118.00", userId: 7));
        Assert.Equal(NavaidWrite.DallaSorgente, await _cat.SetChannelAsync(riga.Id, "12X", userId: 7));
        Assert.Equal(NavaidWrite.DallaSorgente, await _cat.SetCoordinatesAsync(riga.Id, "N41°00'00.00''E015°00'00.00''", userId: 7));

        var dopo = (await _cat.ListAsync()).Single();
        Assert.Equal("115.25", dopo.Frequency);
        Assert.Equal("99Y", dopo.Channel);
    }

    /// <summary>⚠️ Il rifiuto viene <b>prima</b> della validazione: a un campo che non si può toccare non
    /// importa se il valore proposto era buono, e rispondere «non valido» manderebbe a correggere la cosa
    /// sbagliata.</summary>
    [Fact]
    public async Task Il_rifiuto_della_sorgente_viene_prima_del_controllo_di_forma()
    {
        await _cat.ImportFromSourceAsync(new[] { Mnl() });
        var riga = (await _cat.ListAsync()).Single();

        Assert.Equal(NavaidWrite.DallaSorgente, await _cat.SetFrequencyAsync(riga.Id, "non una frequenza", userId: 7));
    }

    /// <summary>
    /// Il tipo MOSTRATO non è mai della sorgente, e questo è il caso vero per cui esiste: MNL sta in
    /// <c>itvor.vor</c>, quindi la sua natura è VOR, ma sul SOP di Amendola si legge <b>VORTACAN</b>. Senza
    /// questo campo l'unico modo di scriverlo sarebbe cambiare la natura, cioè l'identità — e la riga
    /// tornerebbe VOR al primo import.
    /// </summary>
    [Fact]
    public async Task Il_tipo_lo_dice_una_persona_e_non_la_sorgente()
    {
        await _cat.ImportFromSourceAsync(new[] { Mnl() });
        var riga = (await _cat.ListAsync()).Single();

        Assert.Equal(NavaidWrite.Ok, await _cat.SetTypeAsync(riga.Id, "VORTACAN", userId: 7));

        var dopo = (await _cat.ListAsync()).Single();
        Assert.Equal("VHF", dopo.Kind);            // l'identità non si muove
        Assert.Equal("VORTACAN", dopo.Type);       // quel che si stampa, sì
    }

    /// <summary>Un campo che la sorgente comincia a mandare diventa <b>suo</b>, anche se prima l'aveva
    /// scritto una persona: è cosa vuol dire «la fonte vince sempre».</summary>
    [Fact]
    public async Task La_sorgente_si_riprende_un_campo_scritto_a_mano()
    {
        var riga = await _cat.CreateAsync("MNL", "VHF", userId: 7);
        Assert.Equal(NavaidWrite.Ok, await _cat.SetFrequencyAsync(riga.Id, "110.00", userId: 7));

        // ⚠️ Stessa identità: una riga creata a mano non ha canale, quindi la sorgente la ritrova solo se
        // nemmeno lei ne manda uno. Col canale sarebbe un altro impianto — è il caso di Grosseto.
        await _cat.ImportFromSourceAsync(new[] { Mnl(ch: null) });

        var dopo = (await _cat.ListAsync()).Single();
        Assert.Equal("115.25", dopo.Frequency);
        Assert.Equal(NavaidFieldOrigin.Source, dopo.FrequencyOrigin);
        Assert.Equal(NavaidWrite.DallaSorgente, await _cat.SetFrequencyAsync(riga.Id, "110.00", userId: 7));
    }

    /// <summary>
    /// ⚠️ <b>Il caso che ha fatto cadere il modello di prima: GRO.</b> Nello stesso <c>itvor.vor</c> ci sono
    /// due Grosseto — un VOR a 109.85 senza canale e un TACAN puro col solo canale 35Y, venti metri l'uno
    /// dall'altro. Con l'identità «codice + natura» diventavano <b>una riga sola</b>, con la frequenza di uno
    /// e il canale dell'altro: una chimera, e stampata su un SOP.
    /// </summary>
    [Fact]
    public async Task Due_impianti_omonimi_nella_stessa_famiglia_restano_due_righe()
    {
        await _cat.ImportFromSourceAsync(new[]
        {
            new SourceNavaid("GRO", "VHF", "109.85", null, 42.7609, 11.0773),
            new SourceNavaid("GRO", "VHF", null, "35Y", 42.7603, 11.0774),
        });

        var righe = (await _cat.ListAsync()).Where(r => r.Code == "GRO").ToList();
        Assert.Equal(2, righe.Count);
        Assert.Contains(righe, r => r.Frequency == "109.85" && r.Channel is null);
        Assert.Contains(righe, r => r.Channel == "35Y" && r.Frequency is null);
    }

    /// <summary>Sulle righe in kHz il tipo è uno solo, e quello la sorgente lo sa: nascono già NDB. Sulle
    /// VHF no, e restano vuote finché non lo dice qualcuno.</summary>
    [Fact]
    public async Task Il_tipo_nasce_solo_dove_la_sorgente_lo_sa()
    {
        await _cat.ImportFromSourceAsync(new[]
        {
            new SourceNavaid("AVI", "NDB", "390.0", null, 45.9, 12.4),
            Mnl(),
        });

        var righe = await _cat.ListAsync();
        Assert.Equal("NDB", righe.Single(r => r.Kind == "NDB").Type);
        Assert.Null(righe.Single(r => r.Kind == "VHF").Type);
    }

    /// <summary>Ripassare lo stesso giro non crea doppioni: l'identità è stabile fra una passata e l'altra.</summary>
    [Fact]
    public async Task Ripassare_lo_stesso_giro_non_crea_doppioni()
    {
        await _cat.ImportFromSourceAsync(new[] { Mnl() });
        await _cat.ImportFromSourceAsync(new[] { Mnl() });

        Assert.Single(await _cat.ListAsync());
    }

    // ---- L'assenza non cancella ------------------------------------------------------------------------

    /// <summary>
    /// ⚠️ La regola già pagata cara altrove: un giro che non porta un campo <b>lascia il nostro dov'è</b>.
    /// Il sectorfile non manda il canale dei VOR che non ne hanno, e trattare «non lo dico» come
    /// «cancellalo» svuoterebbe l'anagrafica a ogni passata — è lo stesso difetto che azzerò 83 poligoni su 83.
    /// </summary>
    [Fact]
    public async Task Un_giro_che_non_porta_un_campo_non_lo_cancella()
    {
        await _cat.ImportFromSourceAsync(new[] { Mnl() });

        // Secondo giro: la sorgente manda il canale e basta — niente frequenza, niente coordinate.
        // ⚠️ Il canale resta nella coppia perché è IDENTITÀ: toglierlo vorrebbe dire un'altra riga, non
        // la stessa senza canale.
        await _cat.ImportFromSourceAsync(new[] { Mnl(freq: null, lat: null, lon: null) });

        var dopo = (await _cat.ListAsync()).Single();
        Assert.Equal("115.25", dopo.Frequency);
        Assert.NotNull(dopo.Latitude);
    }

    /// <summary>Gli ILS e i TACAN non stanno nel sectorfile: una potatura se li porterebbe via al primo giro,
    /// quindi l'import non pota <b>mai</b>.</summary>
    [Fact]
    public async Task L_import_non_elimina_le_righe_che_la_sorgente_non_conosce()
    {
        await _cat.CreateAsync("AMD", "VHF", userId: 7);

        await _cat.ImportFromSourceAsync(new[] { Mnl() });

        Assert.Equal(2, (await _cat.ListAsync()).Count);
    }

    // ---- Identità, e i campi toccati -------------------------------------------------------------------

    /// <summary>⚠️ L'identità è <b>codice + famiglia + canale</b>: due <c>DEC</c>, uno fra i VHF e uno fra
    /// gli NDB, sono due righe — e sono diciassette i codici che stanno in tutt'e due i file.</summary>
    [Fact]
    public async Task Codice_piu_famiglia_e_l_identita()
    {
        await _cat.ImportFromSourceAsync(new[]
        {
            new SourceNavaid("DEC", "VHF", "108.20", "19X", 39.36, 8.97),
            new SourceNavaid("DEC", "NDB", "331.0", null, 39.36, 8.97),
        });

        var righe = await _cat.ListAsync();
        Assert.Equal(2, righe.Count);
        Assert.Equal(new[] { "NDB", "VHF" }, righe.Select(r => r.Kind).OrderBy(k => k));
    }

    /// <summary>⚠️ Due porte che creano la stessa cosa devono fare la stessa domanda — la lezione delle due
    /// vLOA sulla stessa coppia. Qui la domanda è quella del dominio: codice + natura.</summary>
    [Fact]
    public async Task Creare_due_volte_la_stessa_radioassistenza_da_la_stessa_riga()
    {
        var a = await _cat.CreateAsync("mnl", "vhf", userId: 7);
        var b = await _cat.CreateAsync("MNL", "VHF", userId: 9);

        Assert.Equal(a.Id, b.Id);
        Assert.Equal("MNL", b.Code);   // normalizzato: `mnl` e `MNL` non sono due radioassistenze
        Assert.Single(await _cat.ListAsync());
    }

    /// <summary>
    /// ⚠️ Si scrivono <b>i campi toccati, non la riga</b>: chi cambia la frequenza e chi cambia le coordinate
    /// non si sovrascrivono a vicenda. Se si salvasse tutta la riga, il secondo salvataggio riporterebbe
    /// indietro il primo <i>senza aver toccato la stessa cosa</i>, e il registro racconterebbe una modifica
    /// che nessuno ha fatto.
    /// </summary>
    [Fact]
    public async Task Due_persone_su_campi_diversi_non_si_sovrascrivono()
    {
        var riga = await _cat.CreateAsync("AMD", "VHF", userId: 1);

        await _cat.SetFrequencyAsync(riga.Id, "110.30", userId: 1);
        await _cat.SetCoordinatesAsync(riga.Id, "N41°32'05.07''E015°43'42.47''", userId: 2);

        var dopo = (await _cat.ListAsync()).Single();
        Assert.Equal("110.30", dopo.Frequency);
        Assert.NotNull(dopo.Latitude);
    }

    /// <summary>Sullo STESSO campo invece vince chi arriva per ultimo — decisione del committente — e il
    /// registro è quel che resta per accorgersene.</summary>
    [Fact]
    public async Task Sullo_stesso_campo_vince_chi_arriva_per_ultimo()
    {
        var riga = await _cat.CreateAsync("AMD", "VHF", userId: 1);

        await _cat.SetFrequencyAsync(riga.Id, "110.30", userId: 1);
        await _cat.SetFrequencyAsync(riga.Id, "110.50", userId: 2);

        var dopo = (await _cat.ListAsync()).Single();
        Assert.Equal("110.50", dopo.Frequency);
        Assert.Equal(2, dopo.UpdatedByUserId);
    }

    // ---- Il registro -----------------------------------------------------------------------------------

    /// <summary>
    /// ⚠️ Il registro porta il valore <b>vecchio e nuovo</b>. «Tizio ha modificato MNL» non permette né di
    /// accorgersi di uno scambio né di rimettere a posto — ed è tutto quel che resta, visto che qui vince chi
    /// scrive per ultimo e non c'è nessun lock a fermarlo.
    /// </summary>
    [Fact]
    public async Task Il_registro_porta_il_valore_vecchio_e_quello_nuovo()
    {
        var riga = await _cat.CreateAsync("AMD", "VHF", userId: 1);
        await _cat.SetFrequencyAsync(riga.Id, "110.30", userId: 1);
        await _cat.SetFrequencyAsync(riga.Id, "110.50", userId: 2);

        var ultima = await _db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityType == "Navaid").OrderByDescending(a => a.Id).FirstAsync();

        Assert.Equal(AuditAction.Update, ultima.Action);
        Assert.Equal("AMD|VHF|", ultima.EntityId);
        Assert.Equal(2, ultima.UserId);
        Assert.Contains("110.30", ultima.DetailsJson);   // da
        Assert.Contains("110.50", ultima.DetailsJson);   // a
        Assert.Contains("Frequency", ultima.DetailsJson);
    }

    /// <summary>⚠️ Il non-evento non si scrive: riscrivere «modificata da X oggi» su una decisione presa da
    /// un altro mesi fa è peggio che non scrivere niente.</summary>
    [Fact]
    public async Task Riscrivere_lo_stesso_valore_non_e_un_atto()
    {
        var riga = await _cat.CreateAsync("AMD", "VHF", userId: 1);
        await _cat.SetFrequencyAsync(riga.Id, "110.30", userId: 1);
        var quante = await _db.AuditLogs.CountAsync(a => a.EntityType == "Navaid");

        Assert.Equal(NavaidWrite.Invariato, await _cat.SetFrequencyAsync(riga.Id, "110.30", userId: 2));

        Assert.Equal(quante, await _db.AuditLogs.CountAsync(a => a.EntityType == "Navaid"));
        Assert.Equal(1, (await _cat.ListAsync()).Single().UpdatedByUserId);   // l'autore non cambia
    }

    // ---- Forma dei valori ------------------------------------------------------------------------------

    [Theory]
    [InlineData("centoquindici")]
    [InlineData("1")]
    [InlineData("115,25")]
    public async Task Una_frequenza_che_non_e_una_frequenza_non_si_salva(string v)
    {
        var riga = await _cat.CreateAsync("AMD", "VHF", userId: 1);

        Assert.Equal(NavaidWrite.NonValido, await _cat.SetFrequencyAsync(riga.Id, v, userId: 1));
    }

    /// <summary>Coordinate: <b>sessagesimale soltanto</b>, per decisione del committente. Un decimale
    /// perfettamente valido qui è un no.</summary>
    [Fact]
    public async Task Le_coordinate_si_scrivono_solo_in_sessagesimale()
    {
        var riga = await _cat.CreateAsync("AMD", "VHF", userId: 1);

        Assert.Equal(NavaidWrite.NonValido, await _cat.SetCoordinatesAsync(riga.Id, "41.5347 15.7284", userId: 1));
        Assert.Equal(NavaidWrite.Ok, await _cat.SetCoordinatesAsync(riga.Id, "N41°32'05.07''E015°43'42.47''", userId: 1));
    }

    /// <summary>Svuotare un campo è lecito, e riporta la sua provenienza a «nessuno l'ha scritto».</summary>
    [Fact]
    public async Task Svuotare_un_campo_nostro_si_puo()
    {
        var riga = await _cat.CreateAsync("AMD", "VHF", userId: 1);
        await _cat.SetChannelAsync(riga.Id, "19X", userId: 1);

        Assert.Equal(NavaidWrite.Ok, await _cat.SetChannelAsync(riga.Id, "", userId: 1));

        var dopo = (await _cat.ListAsync()).Single();
        Assert.Null(dopo.Channel);
        Assert.Equal(NavaidFieldOrigin.Empty, dopo.ChannelOrigin);
    }

    [Fact]
    public async Task Una_riga_che_non_c_e_lo_dice()
    {
        Assert.Equal(NavaidWrite.NonTrovata, await _cat.SetFrequencyAsync(999999, "110.30", userId: 1));
    }


    // ---- Eliminare: solo le nostre, e solo se non le cita nessuno --------------------------------------

    /// <summary>⚠️ Una riga che manda la sorgente non si elimina: il giro dopo tornerebbe, e chi l'ha
    /// «eliminata» crederebbe di averlo fatto. Meglio un no adesso che una sorpresa domani.</summary>
    [Fact]
    public async Task Una_riga_della_sorgente_non_si_elimina()
    {
        await _cat.ImportFromSourceAsync(new[] { Mnl() });
        var riga = (await _cat.ListAsync()).Single();

        Assert.Equal(NavaidDelete.DallaSorgente, await _cat.DeleteAsync(riga.Id, userId: 7));
        Assert.Single(await _cat.ListAsync());
    }

    /// <summary>Una riga nostra e non citata si elimina, e il registro se la ricorda.</summary>
    [Fact]
    public async Task Una_riga_nostra_e_non_citata_si_elimina()
    {
        var riga = await _cat.CreateAsync("AMD", "VHF", userId: 7);

        Assert.Equal(NavaidDelete.Ok, await _cat.DeleteAsync(riga.Id, userId: 7));

        Assert.Empty(await _cat.ListAsync());
        var ultima = await _db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityType == "Navaid").OrderByDescending(a => a.Id).FirstAsync();
        Assert.Equal(AuditAction.Delete, ultima.Action);
        Assert.Contains("AMD", ultima.DetailsJson);
    }

    [Fact]
    public async Task Eliminare_una_riga_che_non_c_e_lo_dice()
    {
        Assert.Equal(NavaidDelete.NonTrovata, await _cat.DeleteAsync(999999, userId: 7));
    }

    // ---- Lettura per il documento ----------------------------------------------------------------------

    /// <summary>⚠️ L'ordine è quello CHIESTO, non quello dell'archivio: in una tabella di SOP l'ordine delle
    /// righe è una scelta editoriale, e restituirle ordinate per codice la butterebbe via.</summary>
    [Fact]
    public async Task Le_righe_tornano_nell_ordine_chiesto_e_le_ignote_si_saltano()
    {
        await _cat.ImportFromSourceAsync(new[]
        {
            new SourceNavaid("AEA", "VHF", "111.65", "54Y", 40.6, 8.29),
            Mnl(),
        });

        var righe = await _cat.GetManyAsync(new[]
        {
            new NavaidKey("MNL", "VHF", "99Y"), new NavaidKey("XXX", "VHF", null), new NavaidKey("AEA", "VHF", "54Y"),
        });

        Assert.Equal(new[] { "MNL", "AEA" }, righe.Select(r => r.Code));
    }
}

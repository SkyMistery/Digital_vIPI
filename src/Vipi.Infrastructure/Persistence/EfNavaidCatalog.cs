using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Application.Coordinates;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// EF: l'anagrafica delle radioassistenze (carta vSOP militari §12b, corretta in §12l). Le regole che fa
/// rispettare stanno scritte su <see cref="INavaidCatalog"/>; qui c'è come si applicano.
///
/// <para>⚠️ <b>Nessun controllo di autorizzazione qui dentro.</b> Il cancello sta dove sta per tutte le
/// scritture editoriali, e ripeterlo qui darebbe due cancelli che col tempo dicono cose diverse. Quel che
/// questa classe fa rispettare è un'altra cosa: <b>la fonte vince</b>, e vale anche per un amministratore.</para>
/// </summary>
public sealed class EfNavaidCatalog : INavaidCatalog
{
    private readonly VipiDbContext _db;

    public EfNavaidCatalog(VipiDbContext db) => _db = db;

    /// <summary>
    /// L'identità scritta in una stringa: <c>CODICE|FAMIGLIA|CANALE</c>. ⚠️ Un posto solo — se la chiave si
    /// componesse in due punti, un giorno l'import e la lettura cercherebbero righe diverse.
    /// </summary>
    public static string Chiave(string? code, string? kind, string? channel) =>
        $"{NavaidRules.Norm(code)}|{NavaidRules.Norm(kind)}|{NavaidRules.Norm(channel)}";

    private static string Chiave(NavaidKey k) => Chiave(k.Code, k.Kind, k.Channel);

    public async Task<IReadOnlyList<NavaidRow>> ListAsync(CancellationToken ct = default) =>
        (await _db.Navaids.AsNoTracking()
            .OrderBy(n => n.Code).ThenBy(n => n.Kind).ThenBy(n => n.Channel).ToListAsync(ct))
        .Select(Riga).ToList();

    public async Task<IReadOnlyList<NavaidRow>> GetManyAsync(IReadOnlyList<NavaidKey> keys, CancellationToken ct = default)
    {
        if (keys.Count == 0) return Array.Empty<NavaidRow>();

        var chiavi = keys.Select(Chiave).Distinct().ToList();
        var righe = await _db.Navaids.AsNoTracking().Where(n => chiavi.Contains(n.NaturalKey)).ToListAsync(ct);
        var indice = righe.ToDictionary(n => n.NaturalKey, n => n);

        var esito = new List<NavaidRow>(keys.Count);
        foreach (var k in keys)
            if (indice.TryGetValue(Chiave(k), out var n))
                esito.Add(Riga(n));
        return esito;   // ⚠️ NELL'ORDINE CHIESTO: l'ordine delle righe è una scelta editoriale del documento.
    }

    /// <summary>
    /// Crea una radioassistenza scritta a mano. ⚠️ <paramref name="kind"/> è la <b>famiglia</b>
    /// (<c>VHF</c>/<c>NDB</c>), non il tipo: il tipo lo scrive chi compila, dopo.
    /// </summary>
    public async Task<NavaidRow> CreateAsync(string code, string kind, int userId, CancellationToken ct = default)
    {
        var codice = NavaidRules.Norm(code);
        var famiglia = NavaidRules.Norm(kind);
        if (!NavaidRules.CodiceValido(codice)) throw new ArgumentException($"Codice non valido: '{code}'.", nameof(code));
        if (!NavaidRules.FamigliaValida(famiglia)) throw new ArgumentException($"Famiglia non valida: '{kind}'.", nameof(kind));

        var chiave = Chiave(codice, famiglia, null);
        var esistente = await _db.Navaids.FirstOrDefaultAsync(n => n.NaturalKey == chiave, ct);
        if (esistente is not null) return Riga(esistente);   // idempotente: la stessa domanda dà la stessa riga

        var riga = new Navaid
        {
            Code = codice,
            Kind = famiglia,
            NaturalKey = chiave,
            // Sulle righe in kHz il tipo è uno solo, e quello si sa; sulle VHF lo dirà una persona.
            Type = famiglia == NavaidRules.FamigliaNdb ? NavaidRules.FamigliaNdb : null,
            FrequencyOrigin = NavaidFieldOrigin.Empty,
            ChannelOrigin = NavaidFieldOrigin.Empty,
            CoordinatesOrigin = NavaidFieldOrigin.Empty,
            UpdatedUtc = DateTime.UtcNow,
            UpdatedByUserId = userId,
        };
        _db.Navaids.Add(riga);
        AuditScribe.Write(_db, userId, AuditAction.Create, "Navaid", chiave, new { Codice = codice, Famiglia = famiglia });
        await _db.SaveChangesAsync(ct);
        return Riga(riga);
    }

    public async Task<NavaidDelete> DeleteAsync(int id, int userId, CancellationToken ct = default)
    {
        var n = await _db.Navaids.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (n is null) return NavaidDelete.NonTrovata;

        // ⚠️ Una riga che manda la sorgente non si cancella: il giro dopo torna, e chi l'ha «eliminata»
        // penserebbe di averlo fatto. Meglio un no adesso che una sorpresa domani.
        if (n.ImportedUtc is not null) return NavaidDelete.DallaSorgente;

        // ⚠️ E una riga CITATA nemmeno: sparirebbe da sotto una tabella già scritta, e chi legge quel
        // documento vedrebbe una riga in meno senza spiegazione. Prima si toglie di lì.
        if ((await CitataDaAsync(id, ct)).Count > 0) return NavaidDelete.Citata;

        // L'audit PRIMA della cancellazione, finché il nome è ancora leggibile.
        AuditScribe.Write(_db, userId, AuditAction.Delete, "Navaid", n.NaturalKey,
            new { n.Code, Famiglia = n.Kind, n.Type, n.Frequency, n.Channel });
        _db.Navaids.Remove(n);
        await _db.SaveChangesAsync(ct);
        return NavaidDelete.Ok;
    }

    /// <summary>
    /// Chi cita questa riga. ⚠️ Si cerca nel <b>JSON dei blocchi</b> e non in una tabella di legami: il
    /// documento cita per identità, non per id, e una tabella di legami sarebbe un secondo elenco da tenere
    /// allineato — cioè un secondo elenco che diverge.
    /// </summary>
    public async Task<IReadOnlyList<string>> CitataDaAsync(int id, CancellationToken ct = default)
    {
        var n = await _db.Navaids.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (n is null) return Array.Empty<string>();

        // Il codice restringe in SQL; l'identità intera si controlla in memoria, sulle poche righe rimaste.
        var candidati = await _db.ContentBlocks.AsNoTracking()
            .Where(b => b.BodyJson != null && b.BodyJson.Contains(n.Code))
            .Select(b => new { b.BodyJson, Sezione = b.Section!.Title, Documento = b.DocumentVersion!.Document!.Title })
            .ToListAsync(ct);

        var chiave = new NavaidKey(n.Code, n.Kind, n.Channel);
        var esito = new List<string>();
        foreach (var c in candidati)
        {
            var citata = MilNavaidsPayload.Leggi(c.BodyJson).Contains(chiave)
                || MilDiversionPayload.ChiaviNavaid(MilDiversionPayload.Leggi(c.BodyJson)).Contains(chiave);
            if (citata) esito.Add($"{c.Documento} · {c.Sezione}");
        }
        return esito.Distinct().ToList();
    }

    public Task<NavaidWrite> SetTypeAsync(int id, string? tipo, int userId, CancellationToken ct = default) =>
        ScriviAsync(id, userId, ct,
            // ⚠️ Il TIPO non viene mai dalla sorgente: il sectorfile tiene VOR, TACAN e VORTAC nello stesso
            // file, e nemmeno il canale li distingue. È l'unica cosa che dice sempre una persona.
            origine: _ => NavaidFieldOrigin.Manual,
            valido: () => NavaidRules.TipoValido(tipo),
            leggi: n => n.Type,
            scrivi: (n, _) => n.Type = NavaidRules.Valore(tipo),
            nuovo: NavaidRules.Valore(tipo),
            campo: "Type");

    public Task<NavaidWrite> SetFrequencyAsync(int id, string? frequenza, int userId, CancellationToken ct = default) =>
        ScriviAsync(id, userId, ct,
            origine: n => n.FrequencyOrigin,
            valido: () => NavaidRules.FrequenzaValida(frequenza),
            leggi: n => n.Frequency,
            scrivi: (n, _) =>
            {
                n.Frequency = NavaidRules.ValoreNumerico(frequenza);
                n.FrequencyOrigin = n.Frequency is null ? NavaidFieldOrigin.Empty : NavaidFieldOrigin.Manual;
            },
            nuovo: NavaidRules.ValoreNumerico(frequenza),
            campo: "Frequency");

    /// <summary>
    /// ⚠️ Il canale è nell'IDENTITÀ, quindi cambiarlo cambia la riga: si riscrive anche la chiave. Su una
    /// riga della sorgente non si arriva nemmeno qui — è lei a mandarlo — e su una nostra il rischio è di
    /// scontrarsi con una riga che quella chiave ce l'ha già: lo si dice, non si sovrascrive.
    /// </summary>
    public async Task<NavaidWrite> SetChannelAsync(int id, string? canale, int userId, CancellationToken ct = default)
    {
        var n = await _db.Navaids.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (n is null) return NavaidWrite.NonTrovata;
        if (n.ChannelOrigin == NavaidFieldOrigin.Source) return NavaidWrite.DallaSorgente;
        if (!NavaidRules.CanaleValido(canale)) return NavaidWrite.NonValido;

        var nuovo = NavaidRules.Valore(canale);
        if (string.Equals(n.Channel, nuovo, StringComparison.Ordinal)) return NavaidWrite.Invariato;

        var chiave = Chiave(n.Code, n.Kind, nuovo);
        if (await _db.Navaids.AnyAsync(x => x.NaturalKey == chiave && x.Id != n.Id, ct))
            return NavaidWrite.NonValido;

        var prima = n.Channel;
        n.Channel = nuovo;
        n.ChannelOrigin = nuovo is null ? NavaidFieldOrigin.Empty : NavaidFieldOrigin.Manual;
        n.NaturalKey = chiave;
        n.UpdatedUtc = DateTime.UtcNow;
        n.UpdatedByUserId = userId;
        AuditScribe.Write(_db, userId, AuditAction.Update, "Navaid", chiave,
            new { Campo = "Channel", Da = prima, A = nuovo });
        await _db.SaveChangesAsync(ct);
        return NavaidWrite.Ok;
    }

    public Task<NavaidWrite> SetCoordinatesAsync(int id, string? sessagesimale, int userId, CancellationToken ct = default)
    {
        var vuoto = string.IsNullOrWhiteSpace(sessagesimale);
        double lat = 0, lon = 0;
        var leggibile = vuoto || SexagesimalPair.TryParse(sessagesimale, out lat, out lon);

        return ScriviAsync(id, userId, ct,
            origine: n => n.CoordinatesOrigin,
            valido: () => leggibile,
            // Si confronta il TESTO reso, non i due double: è quel che l'utente vede, ed è alla precisione a
            // cui lo scrive. Confrontando i double, riscrivere la stessa coordinata risulterebbe una modifica
            // ogni volta che l'arrotondamento cade dall'altra parte.
            leggi: n => NavaidText.Coordinate(n.Latitude, n.Longitude) is { Length: > 0 } s ? s : null,
            scrivi: (n, _) =>
            {
                n.Latitude = vuoto ? null : lat;
                n.Longitude = vuoto ? null : lon;
                n.CoordinatesOrigin = vuoto ? NavaidFieldOrigin.Empty : NavaidFieldOrigin.Manual;
            },
            nuovo: vuoto ? null : SexagesimalPair.Format(lat, lon),
            campo: "Coordinates");
    }

    /// <summary>
    /// La scrittura di UN campo, con le quattro regole in fila e nell'ordine che conta: esiste la riga →
    /// il campo è nostro → il valore è scrivibile → è davvero cambiato. Solo alla fine si scrive e si registra.
    /// </summary>
    private async Task<NavaidWrite> ScriviAsync(
        int id, int userId, CancellationToken ct,
        Func<Navaid, NavaidFieldOrigin> origine, Func<bool> valido,
        Func<Navaid, string?> leggi, Action<Navaid, string?> scrivi, string? nuovo, string campo)
    {
        var n = await _db.Navaids.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (n is null) return NavaidWrite.NonTrovata;

        // ⚠️ La fonte vince, e vince PRIMA della validazione: a un campo che non si può toccare non importa
        // se il valore proposto era buono, e dire «non valido» manderebbe a correggere la cosa sbagliata.
        if (origine(n) == NavaidFieldOrigin.Source) return NavaidWrite.DallaSorgente;

        if (!valido()) return NavaidWrite.NonValido;

        var prima = leggi(n);
        if (string.Equals(prima, nuovo, StringComparison.Ordinal)) return NavaidWrite.Invariato;

        scrivi(n, nuovo);
        n.UpdatedUtc = DateTime.UtcNow;
        n.UpdatedByUserId = userId;

        // ⚠️ Il registro porta il valore VECCHIO e quello NUOVO. «Tizio ha modificato MNL» non permette né di
        // accorgersi dello scambio né di rimettere a posto — ed è tutto quel che resta, visto che qui vince
        // chi scrive per ultimo e non c'è nessun lock a fermarlo.
        AuditScribe.Write(_db, userId, AuditAction.Update, "Navaid", n.NaturalKey,
            new { Campo = campo, Da = prima, A = nuovo });

        await _db.SaveChangesAsync(ct);
        return NavaidWrite.Ok;
    }

    public async Task<NavaidImportOutcome> ImportFromSourceAsync(
        IReadOnlyList<SourceNavaid> navaids, CancellationToken ct = default)
    {
        if (navaids.Count == 0) return new NavaidImportOutcome(0, 0, 0);

        var adesso = DateTime.UtcNow;
        var chiavi = navaids.Select(s => Chiave(s.Code, s.Kind, s.Channel)).Distinct().ToList();
        var esistenti = (await _db.Navaids.Where(n => chiavi.Contains(n.NaturalKey)).ToListAsync(ct))
            .ToDictionary(n => n.NaturalKey, n => n);

        int create = 0, aggiornate = 0, invariate = 0;
        foreach (var s in navaids)
        {
            var codice = NavaidRules.Norm(s.Code);
            var famiglia = NavaidRules.Norm(s.Kind);
            if (!NavaidRules.CodiceValido(codice) || !NavaidRules.FamigliaValida(famiglia)) continue;

            var chiave = Chiave(codice, famiglia, s.Channel);
            if (!esistenti.TryGetValue(chiave, out var riga))
            {
                riga = new Navaid
                {
                    Code = codice, Kind = famiglia, NaturalKey = chiave,
                    // ⚠️ Il tipo NON si inventa: sulle VHF resta vuoto finché non lo dice una persona.
                    Type = famiglia == NavaidRules.FamigliaNdb ? NavaidRules.FamigliaNdb : null,
                };
                _db.Navaids.Add(riga);
                esistenti[chiave] = riga;
                create++;
                Applica(riga, s, adesso);
                continue;
            }

            var prima = (riga.Frequency, riga.Channel, riga.Latitude, riga.Longitude);
            Applica(riga, s, adesso);
            if (prima == (riga.Frequency, riga.Channel, riga.Latitude, riga.Longitude)) invariate++;
            else aggiornate++;
        }

        await _db.SaveChangesAsync(ct);
        return new NavaidImportOutcome(create, aggiornate, invariate);
    }

    /// <summary>
    /// I campi che la sorgente <b>manda</b>, e nient'altro.
    ///
    /// <para>⚠️ <b>L'assenza non cancella.</b> Un campo che la sorgente non porta lascia il nostro dov'è: il
    /// sectorfile non conosce gli ILS e non manda un canale per i VOR che non ne hanno, e trattare «non lo
    /// dico» come «cancellalo» svuoterebbe l'anagrafica a ogni giro.</para>
    /// <para>⚠️ Il <b>tipo</b> non è qui, e non è una dimenticanza: la sorgente non lo sa.</para>
    /// </summary>
    private static void Applica(Navaid riga, SourceNavaid s, DateTime adesso)
    {
        if (!string.IsNullOrWhiteSpace(s.Frequency))
        {
            riga.Frequency = s.Frequency!.Trim();
            riga.FrequencyOrigin = NavaidFieldOrigin.Source;
        }
        if (!string.IsNullOrWhiteSpace(s.Channel))
        {
            riga.Channel = NavaidRules.Norm(s.Channel);
            riga.ChannelOrigin = NavaidFieldOrigin.Source;
        }
        if (s.Latitude is { } la && s.Longitude is { } lo)
        {
            riga.Latitude = la;
            riga.Longitude = lo;
            riga.CoordinatesOrigin = NavaidFieldOrigin.Source;
        }
        riga.ImportedUtc = adesso;
    }

    private static NavaidRow Riga(Navaid n) => new(
        n.Id, n.Code, n.Kind, n.Type, n.Frequency, n.Channel, n.Latitude, n.Longitude,
        n.FrequencyOrigin, n.ChannelOrigin, n.CoordinatesOrigin, n.UpdatedUtc, n.UpdatedByUserId);
}

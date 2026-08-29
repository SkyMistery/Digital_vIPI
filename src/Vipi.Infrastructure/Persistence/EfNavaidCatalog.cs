using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Application.Coordinates;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// EF: l'anagrafica delle radioassistenze (carta vSOP militari §12b). Le regole che fa rispettare stanno
/// scritte su <see cref="INavaidCatalog"/>; qui c'è come si applicano.
///
/// <para>⚠️ <b>Nessun controllo di autorizzazione qui dentro.</b> Il cancello sta dove sta per tutte le
/// scritture editoriali — nel servizio del documento che chiama — e ripeterlo qui darebbe due cancelli che
/// col tempo dicono cose diverse. Quel che questa classe fa rispettare è un'altra cosa: <b>la fonte vince</b>,
/// e vale anche per un amministratore.</para>
/// </summary>
public sealed class EfNavaidCatalog : INavaidCatalog
{
    private readonly VipiDbContext _db;

    public EfNavaidCatalog(VipiDbContext db) => _db = db;

    public async Task<IReadOnlyList<NavaidRow>> ListAsync(CancellationToken ct = default) =>
        (await _db.Navaids.AsNoTracking().OrderBy(n => n.Code).ThenBy(n => n.Kind).ToListAsync(ct))
        .Select(Riga).ToList();

    public async Task<IReadOnlyList<NavaidRow>> GetManyAsync(IReadOnlyList<NavaidKey> keys, CancellationToken ct = default)
    {
        if (keys.Count == 0) return Array.Empty<NavaidRow>();

        // Una query sola sui codici, poi si appaia in memoria: l'alternativa è un OR per coppia, che su
        // MariaDB diventa una scansione e qui non porterebbe niente — le righe sono qualche centinaio.
        var codici = keys.Select(k => NavaidRules.Norm(k.Code)).Distinct().ToList();
        var righe = await _db.Navaids.AsNoTracking().Where(n => codici.Contains(n.Code)).ToListAsync(ct);
        var indice = righe.ToDictionary(n => (n.Code, n.Kind), n => n);

        var esito = new List<NavaidRow>(keys.Count);
        foreach (var k in keys)
            if (indice.TryGetValue((NavaidRules.Norm(k.Code), NavaidRules.Norm(k.Kind)), out var n))
                esito.Add(Riga(n));
        return esito;   // ⚠️ NELL'ORDINE CHIESTO: l'ordine delle righe è una scelta editoriale del documento.
    }

    public async Task<NavaidRow> CreateAsync(string code, string kind, int userId, CancellationToken ct = default)
    {
        var codice = NavaidRules.Norm(code);
        var natura = NavaidRules.Norm(kind);
        if (!NavaidRules.CodiceValido(codice)) throw new ArgumentException($"Codice non valido: '{code}'.", nameof(code));
        if (!NavaidRules.TipoValido(natura)) throw new ArgumentException($"Natura non valida: '{kind}'.", nameof(kind));

        var esistente = await _db.Navaids.FirstOrDefaultAsync(n => n.Code == codice && n.Kind == natura, ct);
        if (esistente is not null) return Riga(esistente);   // idempotente: la stessa domanda dà la stessa riga

        var riga = new Navaid
        {
            Code = codice,
            Kind = natura,
            FrequencyOrigin = NavaidFieldOrigin.Empty,
            ChannelOrigin = NavaidFieldOrigin.Empty,
            CoordinatesOrigin = NavaidFieldOrigin.Empty,
            UpdatedUtc = DateTime.UtcNow,
            UpdatedByUserId = userId,
        };
        _db.Navaids.Add(riga);
        AuditScribe.Write(_db, userId, AuditAction.Create, "Navaid", $"{codice}/{natura}", new { Codice = codice, Natura = natura });
        await _db.SaveChangesAsync(ct);
        return Riga(riga);
    }

    public Task<NavaidWrite> SetDisplayTypeAsync(int id, string? tipo, int userId, CancellationToken ct = default) =>
        ScriviAsync(id, userId, ct,
            // ⚠️ Il tipo MOSTRATO non viene mai dalla sorgente: la sorgente dice la NATURA (il file da cui
            // arriva la riga), e questo campo esiste apposta per poter dire «VORTACAN» senza cambiare identità.
            origine: _ => NavaidFieldOrigin.Manual,
            valido: () => NavaidRules.TipoValido(tipo),
            leggi: n => n.DisplayType,
            scrivi: (n, _) => n.DisplayType = NavaidRules.Valore(tipo),
            nuovo: NavaidRules.Valore(tipo),
            campo: "DisplayType");

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

    public Task<NavaidWrite> SetChannelAsync(int id, string? canale, int userId, CancellationToken ct = default) =>
        ScriviAsync(id, userId, ct,
            origine: n => n.ChannelOrigin,
            valido: () => NavaidRules.CanaleValido(canale),
            leggi: n => n.Channel,
            scrivi: (n, _) =>
            {
                n.Channel = NavaidRules.Valore(canale);
                n.ChannelOrigin = n.Channel is null ? NavaidFieldOrigin.Empty : NavaidFieldOrigin.Manual;
            },
            nuovo: NavaidRules.Valore(canale),
            campo: "Channel");

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
        AuditScribe.Write(_db, userId, AuditAction.Update, "Navaid", $"{n.Code}/{n.Kind}",
            new { Campo = campo, Da = prima, A = nuovo });

        await _db.SaveChangesAsync(ct);
        return NavaidWrite.Ok;
    }

    public async Task<NavaidImportOutcome> ImportFromSourceAsync(
        IReadOnlyList<SourceNavaid> navaids, CancellationToken ct = default)
    {
        if (navaids.Count == 0) return new NavaidImportOutcome(0, 0, 0);

        var adesso = DateTime.UtcNow;
        var codici = navaids.Select(n => NavaidRules.Norm(n.Code)).Distinct().ToList();
        var esistenti = (await _db.Navaids.Where(n => codici.Contains(n.Code)).ToListAsync(ct))
            .ToDictionary(n => (n.Code, n.Kind), n => n);

        int create = 0, aggiornate = 0, invariate = 0;
        foreach (var s in navaids)
        {
            var codice = NavaidRules.Norm(s.Code);
            var natura = NavaidRules.Norm(s.Kind);
            if (!NavaidRules.CodiceValido(codice) || !NavaidRules.TipoValido(natura)) continue;

            if (!esistenti.TryGetValue((codice, natura), out var riga))
            {
                riga = new Navaid { Code = codice, Kind = natura };
                _db.Navaids.Add(riga);
                esistenti[(codice, natura)] = riga;
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
    /// dico» come «cancellalo» svuoterebbe l'anagrafica a ogni giro. È la stessa regola che azzerò 83
    /// poligoni su 83 quando non c'era.</para>
    ///
    /// <para>⚠️ Un campo che la sorgente manda diventa <b>suo</b> — anche se prima l'aveva scritto una
    /// persona: «la fonte vince sempre» è la decisione del committente, e da lì in poi quel campo non si
    /// modifica più a mano.</para>
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
        n.Id, n.Code, n.Kind, n.DisplayType, n.Frequency, n.Channel, n.Latitude, n.Longitude,
        n.FrequencyOrigin, n.ChannelOrigin, n.CoordinatesOrigin, n.UpdatedUtc, n.UpdatedByUserId);
}

namespace Vipi.Domain.Entities;

/// <summary>
/// Un programma autorizzato a chiamare le API del sito, con la sua chiave. Carta
/// <c>docs/feature/2026-09-13-chiavi-api.md</c> (T-017): <b>le API non sono mai anonime</b>.
///
/// <para>Non è un utente: non ha VID né livello. La chiave <b>è</b> l'autorizzazione, e chi la porta legge
/// quello che gli endpoint abilitati restituiscono, senza filtri per persona.</para>
///
/// <para>⚠️ <b>La chiave non si conserva.</b> In tabella c'è la sua impronta SHA-256 e il prefisso in chiaro
/// per riconoscerla: la chiave intera si mostra una volta sola, a chi la crea. Una chiave revocata resta in
/// tabella, perché è storia; non scade da sola (decisione del committente, §8).</para>
/// </summary>
public class ApiClient
{
    public int Id { get; set; }

    /// <summary>Chi è il cliente, detto da chi l'ha creato: «Validatore tour IT».</summary>
    public string Nome { get; set; } = "";

    /// <summary>L'inizio della chiave, in chiaro: per riconoscerla in elenco e nei log.</summary>
    public string Prefisso { get; set; } = "";

    /// <summary>SHA-256 della chiave intera, esadecimale minuscolo. Indice unico.</summary>
    public string ImprontaSha256 { get; set; } = "";

    /// <summary>Le API che può chiamare, separate da virgola (<see cref="ApiEndpoints"/>).</summary>
    public string Endpoint { get; set; } = "";

    public int CreataDaUserId { get; set; }
    public DateTime CreataUtc { get; set; }

    public DateTime? RevocataUtc { get; set; }
    public int? RevocataDaUserId { get; set; }

    /// <summary>L'ultima chiamata accettata, aggiornata al più ogni qualche minuto. Dice se un client la usa
    /// davvero senza doverlo chiedere a nessuno (§7, passo 3).</summary>
    public DateTime? UltimoUsoUtc { get; set; }
}

/// <summary>I nomi delle API che una chiave può aprire. Sono le parole scritte in <see cref="ApiClient.Endpoint"/>.</summary>
public static class ApiEndpoints
{
    /// <summary><c>GET /vsop/api/v1/atc/sessions</c>.</summary>
    public const string Archivio = "archivio";

    /// <summary><c>POST /vsop/api/v1/transfers/resolve</c>.</summary>
    public const string Bridge = "bridge";

    /// <summary><c>GET /vsop/api/v1/airports</c> e sotto: scali, schede, SID e STAR (carta 2026-09-30-api-aeroporti.md).</summary>
    public const string Aeroporti = "aeroporti";

    public static readonly IReadOnlyList<string> Tutti = new[] { Archivio, Bridge, Aeroporti };
}

/// <summary>Un indirizzo delle API, come si scrive a chi integra.</summary>
/// <param name="Endpoint">Il permesso della chiave che lo apre (<see cref="ApiEndpoints"/>).</param>
/// <param name="Id">Il nome della voce: la sua descrizione è la stringa <c>ApiKeys_Route_{Id}</c>.</param>
/// <param name="Esempio">L'indirizzo con un esempio dei parametri, da copiare.</param>
public sealed record ApiRotta(string Endpoint, string Id, string Metodo, string Percorso, string Esempio);

/// <summary>
/// Gli indirizzi delle API. <b>Un posto solo</b>: li usano le rotte vere (Hosting) e la pagina delle chiavi che li
/// elenca a chi le emette (30 settembre 2026, committente: «mettili tutti nella pagina chiavi API»). Scritti in due
/// posti, la pagina finirebbe per dare un indirizzo a cui il server non risponde.
/// </summary>
public static class ApiRotte
{
    public const string Sessioni = "/vsop/api/v1/atc/sessions";
    public const string Trasferimenti = "/vsop/api/v1/transfers/resolve";
    public const string Aeroporti = "/vsop/api/v1/airports";

    public static readonly IReadOnlyList<ApiRotta> Tutte = new ApiRotta[]
    {
        new(ApiEndpoints.Aeroporti, "airports", "GET", Aeroporti, Aeroporti),
        new(ApiEndpoints.Aeroporti, "airport", "GET", Aeroporti + "/{icao}", Aeroporti + "/LIRF"),
        new(ApiEndpoints.Aeroporti, "sids", "GET", Aeroporti + "/{icao}/sids", Aeroporti + "/LIRF/sids?runway=16L"),
        new(ApiEndpoints.Aeroporti, "stars", "GET", Aeroporti + "/{icao}/stars", Aeroporti + "/LIRF/stars?runway=16L"),
        new(ApiEndpoints.Archivio, "sessions", "GET", Sessioni, Sessioni + "?callsign=LIRR&from=2026-09-01T00:00:00Z&limit=100"),
        new(ApiEndpoints.Bridge, "resolve", "POST", Trasferimenti, Trasferimenti),
    };
}

/// <summary>Le lunghezze delle colonne, lette dal modello e dal servizio che valida.</summary>
public static class ApiClientLimits
{
    public const int Nome = 120;
    public const int Prefisso = 16;
    public const int Impronta = 64;
    public const int Endpoint = 120;
}

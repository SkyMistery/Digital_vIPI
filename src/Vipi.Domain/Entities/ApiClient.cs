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

    public static readonly IReadOnlyList<string> Tutti = new[] { Archivio, Bridge };
}

/// <summary>Le lunghezze delle colonne, lette dal modello e dal servizio che valida.</summary>
public static class ApiClientLimits
{
    public const int Nome = 120;
    public const int Prefisso = 16;
    public const int Impronta = 64;
    public const int Endpoint = 120;
}

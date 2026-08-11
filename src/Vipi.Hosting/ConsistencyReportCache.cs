using Vipi.Application.Diagnostics;

namespace Vipi.Hosting;

/// <summary>
/// Copia del report di consistenza tenuta da parte per qualche minuto. Singleton.
///
/// <para><b>Perché.</b> <c>/vsop/health</c> è <b>anonimo</b> — deve esserlo, lo interroga chi amministra la
/// macchina — e dietro ci sono scansioni complete delle tabelle, come dice il commento di
/// <see cref="VipiHealthCheck"/>: «costa». Senza cache, chiunque poteva farle girare quante volte voleva, su
/// un database che su <c>atc.it.ivao.aero</c> è condiviso con il sito che ci ospita. Non serviva un avversario:
/// bastava un monitor configurato con un intervallo stretto.</para>
///
/// <para><b>Perché una cache e non l'autenticazione.</b> Chiudere l'endpoint dietro il login lo toglierebbe a
/// chi ha più motivo di guardarlo: un monitor esterno, o chi amministra la macchina senza un account IVAO.
/// E il corpo della risposta non dice nulla di riservato — «Healthy» o «Degraded» e due conteggi. Il problema
/// era il <b>costo</b>, e il costo lo toglie la cache.</para>
///
/// <para><b>Perché TTL e non invalidamento.</b> Le incongruenze soft-ref nascono da import e da modifiche
/// editoriali: eventi rari. Due minuti di ritardo su una diagnosi non cambiano niente per chi la legge; un
/// canale di invalidamento in più sarebbe una cosa da tenere allineata per sempre.</para>
///
/// <para>⚠️ Vale <b>solo</b> per l'health check. <c>/vsop/admin/diagnostica</c> continua a leggere il report
/// fresco: chi apre quella pagina l'ha aperta per vedere adesso, e ha già fatto login.</para>
/// </summary>
public sealed class ConsistencyReportCache
{
    /// <summary>Quanto resta valida una fotografia del report.</summary>
    public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<ConsistencyFinding>? _valore;
    private DateTime _scadenzaUtc;

    /// <summary>Quante volte si è servita la copia in cache invece di rieseguire il report (diagnostica e test).</summary>
    public int Riusi { get; private set; }

    public async Task<IReadOnlyList<ConsistencyFinding>> GetAsync(
        Func<CancellationToken, Task<IReadOnlyList<ConsistencyFinding>>> esegui, CancellationToken ct)
    {
        if (_valore is { } fresco && DateTime.UtcNow < _scadenzaUtc)
        {
            Riusi++;
            return fresco;
        }

        await _gate.WaitAsync(ct);
        try
        {
            // Ricontrollo dentro il lock: fra il primo controllo e qui un'altra richiesta può aver già
            // rieseguito. Senza, N richieste concorrenti su cache scaduta farebbero N scansioni complete —
            // cioè proprio il caso che questa classe esiste per evitare.
            if (_valore is { } appena && DateTime.UtcNow < _scadenzaUtc)
            {
                Riusi++;
                return appena;
            }

            var eseguito = await esegui(ct);
            _valore = eseguito;
            _scadenzaUtc = DateTime.UtcNow.Add(Ttl);
            return eseguito;
        }
        finally { _gate.Release(); }
    }
}

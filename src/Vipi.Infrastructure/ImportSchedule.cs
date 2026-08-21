using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Infrastructure.Ivao;
using Vipi.Infrastructure.Sectorfile;

namespace Vipi.Infrastructure;

/// <summary>
/// La cadenza dei giri automatici, letta dalle stesse opzioni che gli hosted service passano a
/// <see cref="GatedImportLoop"/>.
///
/// <para>⚠️ I <c>Math.Max(1, …)</c> non sono difensivi per caso: sono <b>gli stessi</b> che applicano gli
/// hosted service. Se qui si leggesse il valore grezzo, con una configurazione a zero la pagina
/// annuncerebbe un giro che non esiste con quella cadenza. La regola del giro è che due letture della stessa
/// cosa divergono: qui la difesa è che il numero venga calcolato allo stesso modo.</para>
///
/// <para>SID senza <c>RawBaseUrl</c>: l'hosted service non parte affatto (sorgente non configurata), quindi
/// la cadenza è <c>null</c> — «nessun giro automatico», che è la verità.</para>
/// </summary>
public sealed class ImportSchedule : IImportSchedule
{
    private readonly IvaoOptions _ivao;
    private readonly SectorfileOptions _sectorfile;

    public ImportSchedule(IOptions<IvaoOptions> ivao, IOptions<SectorfileOptions> sectorfile)
    {
        _ivao = ivao.Value;
        _sectorfile = sectorfile.Value;
    }

    public TimeSpan? PeriodOf(string category) => category switch
    {
        ImportCategories.Acc => Ore(_ivao.AccImportHours),
        ImportCategories.SpecialArea => Ore(_ivao.AccImportHours),
        ImportCategories.AirportSector => Ore(_ivao.AirportSectorImportHours),
        ImportCategories.Sid => string.IsNullOrWhiteSpace(_sectorfile.RawBaseUrl) ? null : Ore(_sectorfile.ImportHours),
        _ => null,   // TA e Piste non hanno un giro automatico: arrivano solo su richiesta.
    };

    private static TimeSpan Ore(int h) => TimeSpan.FromHours(Math.Max(1, h));
}

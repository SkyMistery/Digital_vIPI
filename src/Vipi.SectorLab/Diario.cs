using System.Diagnostics;

namespace Vipi.SectorLab;

/// <summary>
/// Il diario d'avvio: una riga per momento, coi millisecondi dall'avvio, riscritto a ogni apertura in
/// <c>%LOCALAPPDATA%\VipiSectorLab\avvio.txt</c>. È la prima cosa da chiedere a un AOD quando «il Lab non parte».
/// </summary>
internal sealed class Diario
{
    private readonly string _percorso;
    private readonly Stopwatch _orologio;
    private readonly object _serratura = new();

    public Diario(string percorso, Stopwatch orologio)
    {
        _percorso = percorso;
        _orologio = orologio;
        File.WriteAllText(_percorso, "");
    }

    public void Scrivi(string riga)
    {
        string testo = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z {_orologio.ElapsedMilliseconds,6} ms  {riga}{Environment.NewLine}";
        lock (_serratura)
        {
            try
            {
                File.AppendAllText(_percorso, testo);
            }
            catch (IOException)
            {
                // Il diario non deve mai far cadere l'app che racconta.
            }
        }
    }
}

/// <summary>La versione mostrata nel titolo e nel diario: quella dell'assieme, senza l'impronta del commit.</summary>
internal static class Versione
{
    public static string Testo { get; } =
        (typeof(Versione).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion ?? "?")
        .Split('+')[0];
}

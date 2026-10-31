using Vipi.SectorLab.Core.PdfAip;

namespace Vipi.SectorLab.Ui.Servizi;

/// <summary>
/// Un legame fra un record del sector e una voce dell'AIP espanso (D11 della carta del riordino), da una riga di un confronto
/// fatto: la sezione, la riga (col suo esito) e la voce dello strato dell'AIP sulla mappa, se c'è.
/// </summary>
public sealed record LegameConLAip(SezioneDelConfronto Sezione, int Riga, RigaDelConfronto DelConfronto, int? Voce);

public sealed partial class SessioneDelLab
{
    /// <summary>
    /// Le righe dei confronti fatti che legano quel record del sector a una voce dell'AIP espanso (D11: «nell'AIP: …» nei
    /// Collegamenti). Solo le sezioni già confrontate: il legame lo fa il confronto, il Lab non lo indovina dal nome.
    /// </summary>
    public IReadOnlyList<LegameConLAip> LegamiDelRecord(string fileRelativo, int record) =>
        Legami(r => r.File is { } f && r.Record == record && string.Equals(f, fileRelativo, StringComparison.OrdinalIgnoreCase));

    /// <summary>Le righe dei confronti fatti che legano quella voce dell'AIP espanso a un record del sector («nel sector: …»).</summary>
    public IReadOnlyList<LegameConLAip> LegamiDellaVoce(int voce)
    {
        if (StratoDellAip is not { } strato)
            return [];
        return [.. Legami(r => r.File is not null && r.Aip is not null).Where(l => l.Voce == voce)];
    }

    private List<LegameConLAip> Legami(Func<RigaDelConfronto, bool> tiene)
    {
        var legami = new List<LegameConLAip>();
        foreach (var sezione in _confronti.Keys.ToList())
        {
            if (RigheDellaSezione(sezione) is not { } confronto)
                continue;
            for (int i = 0; i < confronto.Righe.Count; i++)
            {
                var riga = confronto.Righe[i];
                if (!tiene(riga))
                    continue;
                int? voce = StratoDellAip is { } strato && strato.Ciclo == confronto.Ciclo && riga.Aip is not null
                    ? ConfrontiDelleSezioni.VoceDelNome(strato.Voci, riga.Aip)
                    : null;
                legami.Add(new LegameConLAip(sezione, i, riga, voce));
            }
        }
        return legami;
    }

    /// <summary>«Vai alla voce» (D11): l'Indice dell'AIP espanso con la voce scelta, sulla mappa.</summary>
    public void VaiAllaVoceDellAip(int voce)
    {
        if (StratoDellAip is not { } strato || voce < 0 || voce >= strato.Voci.Count)
            return;
        ScegliLaPagina(PaginaDelLab.AipIndice);
        Scegli(strato.File, voce);
    }

    /// <summary>«Vai al record» (D11): la Mappa del sector col record scelto.</summary>
    public void VaiAlRecord(string fileRelativo, int record)
    {
        ScegliLaPagina(PaginaDelLab.Sector);
        Inquadrature++;
        Scegli(fileRelativo, record);
    }

    /// <summary>«nel Confronto» (D11): lo spazio Confronti, sulla sezione e sulla riga del legame (i due lati insieme).</summary>
    public void VaiAlConfronto(LegameConLAip legame)
    {
        ArgumentNullException.ThrowIfNull(legame);
        ScegliLaPagina(PaginaDelLab.Confronti);
        ApriLaSezioneDelConfronto(legame.Sezione);
        ScegliLaRigaDelConfronto(legame.Riga);
    }
}

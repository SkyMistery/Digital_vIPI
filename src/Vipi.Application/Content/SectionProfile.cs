namespace Vipi.Application.Content;

/// <summary>
/// "Profilo" documentale che seleziona quali sezioni fisse mostra (membership nel <see cref="SectionCatalog"/>).
/// L'ACC ha due profili distinti (blocco Aerovia = settori CTR; blocco APP = gruppo APP). Doc refactor 08a.
/// <para>
/// I primi quattro descrivono <b>posizioni di controllo</b> e condividono le sezioni universali;
/// <see cref="Airport"/> descrive un <b>luogo</b> e per questo non ha AoR, coordinamenti né aree regolamentate
/// — quelli appartengono alla torre e all'avvicinamento, che hanno documenti loro.
/// </para>
/// </summary>
public enum SectionProfile
{
    App,
    AccAerovia,
    AccAppBlock,
    Vloa,

    /// <summary>vIPI d'aeroporto (carta 2026-08-26): meteo, regole piste, quote di transizione, frequenze,
    /// piste, SID. Prima di quella carta l'aeroporto non aveva un profilo: il documento era una proiezione
    /// cotta con sezioni riconosciute per titolo.</summary>
    Airport,
}

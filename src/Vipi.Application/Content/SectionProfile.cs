namespace Vipi.Application.Content;

/// <summary>
/// "Profilo" documentale che seleziona quali sezioni fisse mostra (membership nel <see cref="SectionCatalog"/>).
/// L'ACC ha due profili distinti (blocco Aerovia = settori CTR; blocco APP = gruppo APP). Doc refactor 08a.
/// </summary>
public enum SectionProfile
{
    App,
    AccAerovia,
    AccAppBlock,
    Vloa,
}

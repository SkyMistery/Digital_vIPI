namespace Vipi.Application.Content;

/// <summary>Un aeroporto a cui l'accordo si applica. <paramref name="Name"/> è valorizzato solo per gli scali
/// fuori catalogo (nuovi/esteri): per gli altri il nome arriva dal catalogo, e tenerne una copia qui sarebbe una
/// seconda verità da mantenere.</summary>
public sealed record AgreementAirportRow(string Icao, string? Name, int Order);

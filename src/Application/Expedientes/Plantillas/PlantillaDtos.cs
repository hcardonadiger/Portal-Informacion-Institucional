namespace Diger.TramitesEstado.Application.Expedientes.Plantillas;

public sealed record PlantillaListItemDto(int Id, string Nombre, bool Activa, int NumLegal, int NumRequisitos);

public sealed record PlantillaLegalItemDto(int Id, string Instrumento, string? Articulos, string? Obs);
public sealed record PlantillaRequisitoItemDto(int Id, string Requisito, string? Obs);

public sealed record PlantillaDetalleDto(
    int Id, string Nombre, bool Activa,
    IReadOnlyList<PlantillaLegalItemDto> Legal,
    IReadOnlyList<PlantillaRequisitoItemDto> Requisitos);

public sealed record PlantillaLegalInput(string Instrumento, string? Articulos, string? Obs);
public sealed record PlantillaRequisitoInput(string Requisito, string? Obs);

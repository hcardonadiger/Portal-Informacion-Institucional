namespace Diger.TramitesEstado.Application.Common.Exceptions;

public sealed class NotFoundException(string name, object key)
    : Exception($"'{name}' con clave ({key}) no fue encontrado.");

namespace Diger.TramitesEstado.Application.Siger.Llenado;

/// <summary>
/// Todo lo que las reglas de llenado necesitan mirar de una ficha, y nada más.
/// </summary>
/// <remarks>
/// Existe para que <see cref="ReglasLlenado"/> no toque entidades ni base de datos: las reglas
/// son la parte de esta fase que hay que poder discutir y corregir con calma, y una regla que se
/// prueba armando un registro en tres líneas se corrige; una que necesita levantar un contexto de
/// Entity Framework, no.
/// </remarks>
/// <param name="TiemposDePaso">El <c>TiempoRegistrado</c> de cada paso, en el orden en que están.
/// Se pasan todos —incluidos los vacíos— porque saber <i>cuántos</i> pasos no declararon tiempo
/// es lo que decide si la suma es un dato o una subestimación.</param>
/// <param name="LugaresDeAtencion">Cuántos lugares físicos de atención tiene la ficha.</param>
public sealed record DatosParaLlenado(
    string                  Nombre,
    string?                 Descripcion,
    string?                 Objetivo,
    IReadOnlyList<string?>  TiemposDePaso,
    IReadOnlyList<string>   TextosDePaso,
    IReadOnlyList<string>   TextosDeRequisito,
    int                     LugaresDeAtencion);

/// <summary>Un valor que las reglas proponen, antes de convertirse en fila de la cola.</summary>
public sealed record PropuestaCalculada(
    CampoFicha      Campo,
    string          Valor,
    CertezaLlenado  Certeza,
    string          Justificacion);

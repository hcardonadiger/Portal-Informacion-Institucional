using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// <c>ProyectoInteresados.UsuarioId</c> pasa a ser obligatorio: un interesado es siempre un
    /// usuario del portal, porque el registro es lo que le abre el proyecto fuera de su alcance.
    ///
    /// <para><b>Hay pérdida de filas y es deliberada.</b> Los interesados cargados sin usuario no
    /// se pueden convertir —no existe la cuenta— y dejarlos con un Guid vacío, que es lo que
    /// generaba el andamiaje por omisión, produciría registros que no le abren el proyecto a nadie
    /// y que ensucian el listado. Antes de borrarlos se vuelca cada uno a
    /// <c>BitacoraProyecto</c> con todos sus campos, así que la información no se pierde: queda
    /// donde se puede leer y, si hace falta, volver a cargar una vez creada la cuenta.</para>
    /// </summary>
    public partial class InteresadoUsuarioObligatorio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Se preserva lo que se va a borrar. La bitácora es append-only, así que esta
            //    entrada queda como el registro de que el interesado existió y con qué datos.
            migrationBuilder.Sql(@"
INSERT INTO BitacoraProyecto (ProyectoId, Tipo, Detalle, Actor, Fecha)
SELECT i.ProyectoId,
       N'Interesado',
       N'Interesado retirado al exigirse cuenta de usuario. Datos que tenía: '
         + i.Nombre
         + ISNULL(N' · ' + i.Institucion, N'')
         + ISNULL(N' · ' + i.Cargo, N'')
         + ISNULL(N' · ' + i.Correo, N'')
         + N' · rol ' + i.Rol + N' · influencia ' + i.Influencia
         + ISNULL(N' · ' + i.Notas, N'')
         + N'. Para reponerlo hay que crearle el usuario primero.',
       N'migración InteresadoUsuarioObligatorio',
       SYSUTCDATETIME()
FROM ProyectoInteresados i
WHERE i.UsuarioId IS NULL;");

            migrationBuilder.Sql("DELETE FROM ProyectoInteresados WHERE UsuarioId IS NULL;");

            // 2. Sin defaultValue a propósito: ya no quedan filas nulas, y el default que ponía el
            //    andamiaje dejaba un constraint permanente con el Guid vacío.
            migrationBuilder.AlterColumn<Guid>(
                name: "UsuarioId",
                table: "ProyectoInteresados",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            // La misma persona no puede figurar dos veces: cada fila otorga acceso, y repetirla
            // obligaría a revocar dos veces para sacar a alguien.
            migrationBuilder.CreateIndex(
                name: "IX_ProyectoInteresados_ProyectoId_UsuarioId",
                table: "ProyectoInteresados",
                columns: new[] { "ProyectoId", "UsuarioId" },
                unique: true);

            // Lo usa la rama del filtro de alcance que pregunta de qué proyectos es interesado el
            // usuario actual, que corre en toda consulta de proyectos.
            migrationBuilder.CreateIndex(
                name: "IX_ProyectoInteresados_UsuarioId",
                table: "ProyectoInteresados",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Los interesados borrados en Up no vuelven: su rastro queda en BitacoraProyecto.
            migrationBuilder.DropIndex(
                name: "IX_ProyectoInteresados_ProyectoId_UsuarioId",
                table: "ProyectoInteresados");

            migrationBuilder.DropIndex(
                name: "IX_ProyectoInteresados_UsuarioId",
                table: "ProyectoInteresados");

            migrationBuilder.AlterColumn<Guid>(
                name: "UsuarioId",
                table: "ProyectoInteresados",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");
        }
    }
}

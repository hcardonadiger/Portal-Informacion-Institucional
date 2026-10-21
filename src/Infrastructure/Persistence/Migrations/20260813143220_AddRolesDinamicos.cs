using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Los roles dejan de ser el enum RolUsuario y pasan a la tabla Roles, administrable
    /// desde /Accesos/Roles.
    ///
    /// El andamiaje de EF proponía DropColumn "Rol" + AddColumn "RolId", lo que habría
    /// borrado la matriz de accesos por módulo ya configurada (RolModuloAccesos). Se
    /// reemplazó por RenameColumn + AlterColumn, que conserva los datos: las columnas ya
    /// guardaban el NOMBRE del rol como texto ("JefeArea"), que es exactamente el código que
    /// ahora es la PK de Roles, así que las filas existentes calzan con el seed sin
    /// conversión. El seed va ANTES de las FKs por el mismo motivo.
    /// </summary>
    public partial class AddRolesDinamicos : Migration
    {
        /// <summary>Fecha fija: las migraciones deben ser deterministas (no DateTime.UtcNow).</summary>
        private static readonly DateTime SeedFecha = new(2026, 8, 13, 0, 0, 0, DateTimeKind.Utc);

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Catálogo de roles ──────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Color = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    NivelAlcance = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EsAdministrador = table.Column<bool>(type: "bit", nullable: false),
                    EsSoloLectura = table.Column<bool>(type: "bit", nullable: false),
                    EsSupervisor = table.Column<bool>(type: "bit", nullable: false),
                    EsTecnicoSoporte = table.Column<bool>(type: "bit", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    EsSistema = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Activo",
                table: "Roles",
                column: "Activo");

            // ── 2. Seed de los 6 roles base ───────────────────────────────────
            // Reproduce exactamente el comportamiento que antes estaba hardcodeado:
            // el alcance sale de las ramas de RLS de AppDbContext (JefeInstitucion →
            // Institucion, JefeArea → Area, el resto → Unidad), EsSoloLectura del bloqueo
            // duro del Consultor, EsSupervisor de las tres negaciones de "es jefe" y
            // EsTecnicoSoporte de la lista EsTecnico() que tenía SoporteHub (todos menos
            // Consultor). EsSistema = true: se pueden ajustar y desactivar, no eliminar.
            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "Nombre", "Descripcion", "Color", "NivelAlcance", "EsAdministrador", "EsSoloLectura", "EsSupervisor", "EsTecnicoSoporte", "Activo", "EsSistema", "CreatedAt" },
                values: new object[,]
                {
                    { "Administrador",   "Administrador",       "Gestiona usuarios y todo el portal.",        "#3c3489", "Global",      true,  false, true,  true,  true, true, SeedFecha },
                    { "JefeInstitucion", "Jefe de Institución", "Gestiona toda su institución.",              "#0c447c", "Institucion", false, false, true,  true,  true, true, SeedFecha },
                    { "JefeArea",        "Jefe de Área",        "Gestiona toda su área.",                     "#1a5fa0", "Area",        false, false, true,  true,  true, true, SeedFecha },
                    { "JefeUnidad",      "Jefe de Unidad",      "Gestiona toda su unidad.",                   "#1b5e20", "Unidad",      false, false, true,  true,  true, true, SeedFecha },
                    { "Empleado",        "Empleado",            "Gestiona sus propios datos en su unidad.",   "#085041", "Unidad",      false, false, false, true,  true, true, SeedFecha },
                    { "Consultor",       "Consultor",           "Solo lectura, sin permiso para mutar datos.", "#6d4c00", "Unidad",      false, true,  false, false, true, true, SeedFecha },
                });

            // ── 3. Rol → RolId, conservando los datos ─────────────────────────
            migrationBuilder.DropIndex(
                name: "IX_RolPermisos_Rol_PermisoClave",
                table: "RolPermisos");

            migrationBuilder.DropIndex(
                name: "IX_RolModuloAccesos_Rol_Modulo",
                table: "RolModuloAccesos");

            migrationBuilder.RenameColumn(name: "Rol", table: "RolPermisos",       newName: "RolId");
            migrationBuilder.RenameColumn(name: "Rol", table: "RolModuloAccesos",  newName: "RolId");
            migrationBuilder.RenameColumn(name: "Rol", table: "PermisosAuditoria", newName: "RolId");

            // 20 → 60: el ancho del código de rol que define Rol.ValidarCodigo.
            migrationBuilder.AlterColumn<string>(
                name: "RolId", table: "RolPermisos",
                type: "nvarchar(60)", maxLength: 60, nullable: false,
                oldClrType: typeof(string), oldType: "nvarchar(20)", oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "RolId", table: "RolModuloAccesos",
                type: "nvarchar(60)", maxLength: 60, nullable: false,
                oldClrType: typeof(string), oldType: "nvarchar(20)", oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "RolId", table: "PermisosAuditoria",
                type: "nvarchar(60)", maxLength: 60, nullable: false,
                oldClrType: typeof(string), oldType: "nvarchar(20)", oldMaxLength: 20);

            // ── 4. Permisos ahora llevan la acción del vocabulario fijo ───────
            // Las claves sincronizadas con el formato viejo ("Accesos.GestionarPermisos")
            // no tienen acción y no volverán a aparecer: se purgan en vez de quedar con un
            // Accion = "" que reventaría al leerlas como enum. PermissionCatalogSyncService
            // vuelve a poblar la tabla desde los [Permission] del código en cada arranque.
            migrationBuilder.DropIndex(
                name: "IX_Permisos_Modulo",
                table: "Permisos");

            migrationBuilder.Sql("DELETE FROM RolPermisos;");
            migrationBuilder.Sql("DELETE FROM Permisos;");

            migrationBuilder.AddColumn<string>(
                name: "Accion",
                table: "Permisos",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            // ── 5. Índices y FKs sobre el modelo nuevo ────────────────────────
            migrationBuilder.CreateIndex(
                name: "IX_RolPermisos_RolId_PermisoClave",
                table: "RolPermisos",
                columns: new[] { "RolId", "PermisoClave" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolModuloAccesos_RolId_Modulo",
                table: "RolModuloAccesos",
                columns: new[] { "RolId", "Modulo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Permisos_Modulo_Accion",
                table: "Permisos",
                columns: new[] { "Modulo", "Accion" });

            migrationBuilder.AddForeignKey(
                name: "FK_RolModuloAccesos_Roles_RolId",
                table: "RolModuloAccesos",
                column: "RolId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RolPermisos_Roles_RolId",
                table: "RolPermisos",
                column: "RolId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RolModuloAccesos_Roles_RolId",
                table: "RolModuloAccesos");

            migrationBuilder.DropForeignKey(
                name: "FK_RolPermisos_Roles_RolId",
                table: "RolPermisos");

            migrationBuilder.DropIndex(
                name: "IX_RolPermisos_RolId_PermisoClave",
                table: "RolPermisos");

            migrationBuilder.DropIndex(
                name: "IX_RolModuloAccesos_RolId_Modulo",
                table: "RolModuloAccesos");

            migrationBuilder.DropIndex(
                name: "IX_Permisos_Modulo_Accion",
                table: "Permisos");

            migrationBuilder.DropColumn(
                name: "Accion",
                table: "Permisos");

            // Los roles creados a mano pueden tener códigos de más de 20 caracteres: se
            // borran sus concesiones antes de estrechar la columna, porque el modelo viejo
            // (enum RolUsuario) no puede representarlos de ninguna forma.
            migrationBuilder.Sql("DELETE FROM RolPermisos WHERE LEN(RolId) > 20;");
            migrationBuilder.Sql("DELETE FROM RolModuloAccesos WHERE LEN(RolId) > 20;");
            migrationBuilder.Sql("DELETE FROM PermisosAuditoria WHERE LEN(RolId) > 20;");

            migrationBuilder.AlterColumn<string>(
                name: "RolId", table: "RolPermisos",
                type: "nvarchar(20)", maxLength: 20, nullable: false,
                oldClrType: typeof(string), oldType: "nvarchar(60)", oldMaxLength: 60);

            migrationBuilder.AlterColumn<string>(
                name: "RolId", table: "RolModuloAccesos",
                type: "nvarchar(20)", maxLength: 20, nullable: false,
                oldClrType: typeof(string), oldType: "nvarchar(60)", oldMaxLength: 60);

            migrationBuilder.AlterColumn<string>(
                name: "RolId", table: "PermisosAuditoria",
                type: "nvarchar(20)", maxLength: 20, nullable: false,
                oldClrType: typeof(string), oldType: "nvarchar(60)", oldMaxLength: 60);

            migrationBuilder.RenameColumn(name: "RolId", table: "RolPermisos",       newName: "Rol");
            migrationBuilder.RenameColumn(name: "RolId", table: "RolModuloAccesos",  newName: "Rol");
            migrationBuilder.RenameColumn(name: "RolId", table: "PermisosAuditoria", newName: "Rol");

            migrationBuilder.CreateIndex(
                name: "IX_RolPermisos_Rol_PermisoClave",
                table: "RolPermisos",
                columns: new[] { "Rol", "PermisoClave" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolModuloAccesos_Rol_Modulo",
                table: "RolModuloAccesos",
                columns: new[] { "Rol", "Modulo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Permisos_Modulo",
                table: "Permisos",
                column: "Modulo");

            migrationBuilder.DropTable(
                name: "Roles");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCamposVentanilla : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CategoriaId",
                table: "TramitesSiger",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CostoEsGratuito",
                table: "TramitesSiger",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CostoTexto",
                table: "TramitesSiger",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EsPopular",
                table: "TramitesSiger",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EstaEnSol",
                table: "TramitesSiger",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Modalidad",
                table: "TramitesSiger",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SolUrl",
                table: "TramitesSiger",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SolVerificadoEl",
                table: "TramitesSiger",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TiempoTexto",
                table: "TramitesSiger",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Modalidad",
                table: "PasosSiger",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Titulo",
                table: "PasosSiger",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NombreCorto",
                table: "Instituciones",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Direccion",
                table: "Instituciones",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Horario",
                table: "Instituciones",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SitioWeb",
                table: "Instituciones",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Telefono",
                table: "Instituciones",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tipo",
                table: "Instituciones",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CategoriasTramite",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Icono = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoriasTramite", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "CategoriasTramite",
                columns: new[] { "Id", "Activo", "CreatedAt", "CreatedBy", "Icono", "Nombre", "Orden", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "HeartPulse", "Salud y Seguridad Social", 10, null, null },
                    { 2, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "GraduationCap", "Educación y Cultura", 20, null, null },
                    { 3, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "CreditCard", "Impuestos y Finanzas", 30, null, null },
                    { 4, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Contact", "Identidad y Ciudadanía", 40, null, null },
                    { 5, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Building2", "Empresas y Negocios", 50, null, null },
                    { 6, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Home", "Vivienda y Propiedad", 60, null, null },
                    { 7, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Car", "Transporte y Vehículos", 70, null, null },
                    { 8, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Leaf", "Medio Ambiente", 80, null, null }
                });

            migrationBuilder.UpdateData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "BANHPROVI",
                columns: new[] { "Direccion", "Horario", "SitioWeb", "Telefono", "Tipo" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "CANATURH",
                columns: new[] { "Direccion", "Horario", "SitioWeb", "Telefono", "Tipo" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "CNBS",
                columns: new[] { "Direccion", "Horario", "SitioWeb", "Telefono", "Tipo" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "CONATEL",
                columns: new[] { "Direccion", "Horario", "SitioWeb", "Telefono", "Tipo" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "CONSUCOOP",
                columns: new[] { "Direccion", "Horario", "SitioWeb", "Telefono", "Tipo" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "CONVIVIENDA",
                columns: new[] { "Direccion", "Horario", "SitioWeb", "Telefono", "Tipo" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "COPECO",
                columns: new[] { "Direccion", "Horario", "SitioWeb", "Telefono", "Tipo" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "DIGER",
                columns: new[] { "Direccion", "Horario", "SitioWeb", "Telefono", "Tipo" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "FOSOVI",
                columns: new[] { "Direccion", "Horario", "SitioWeb", "Telefono", "Tipo" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "IHADFA",
                columns: new[] { "Direccion", "Horario", "SitioWeb", "Telefono", "Tipo" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "IHCINE",
                columns: new[] { "Direccion", "Horario", "SitioWeb", "Telefono", "Tipo" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "IHT",
                columns: new[] { "Direccion", "Horario", "SitioWeb", "Telefono", "Tipo" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "IHTT",
                columns: new[] { "Direccion", "Horario", "SitioWeb", "Telefono", "Tipo" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "INPREMA",
                columns: new[] { "Direccion", "Horario", "SitioWeb", "Telefono", "Tipo" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "INPREUNAH",
                columns: new[] { "Direccion", "Horario", "SitioWeb", "Telefono", "Tipo" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "IP",
                columns: new[] { "Direccion", "Horario", "SitioWeb", "Telefono", "Tipo" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "SAG",
                columns: new[] { "Direccion", "Horario", "SitioWeb", "Telefono", "Tipo" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "SECAPPH",
                columns: new[] { "Direccion", "Horario", "SitioWeb", "Telefono", "Tipo" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "SEN",
                columns: new[] { "Direccion", "Horario", "SitioWeb", "Telefono", "Tipo" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "SENASA",
                columns: new[] { "Direccion", "Horario", "SitioWeb", "Telefono", "Tipo" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "SERNA",
                columns: new[] { "Direccion", "Horario", "SitioWeb", "Telefono", "Tipo" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "SESAL",
                columns: new[] { "Direccion", "Horario", "SitioWeb", "Telefono", "Tipo" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "SGJD",
                columns: new[] { "Direccion", "Horario", "SitioWeb", "Telefono", "Tipo" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "SIT",
                columns: new[] { "Direccion", "Horario", "SitioWeb", "Telefono", "Tipo" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "SRECI",
                columns: new[] { "Direccion", "Horario", "SitioWeb", "Telefono", "Tipo" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_TramitesSiger_Catalogo",
                table: "TramitesSiger",
                columns: new[] { "Publicado", "CategoriaId", "InstitucionId" })
                .Annotation("SqlServer:Include", new[] { "Codigo", "Nombre", "Modalidad", "EsPopular", "CostoEsGratuito" });

            migrationBuilder.CreateIndex(
                name: "IX_TramitesSiger_CategoriaId",
                table: "TramitesSiger",
                column: "CategoriaId",
                filter: "[CategoriaId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TramitesSiger_EstaEnSol",
                table: "TramitesSiger",
                column: "EstaEnSol",
                filter: "[EstaEnSol] = 1")
                .Annotation("SqlServer:Include", new[] { "SolUrl" });

            migrationBuilder.CreateIndex(
                name: "IX_TramitesSiger_Modalidad",
                table: "TramitesSiger",
                column: "Modalidad",
                filter: "[Modalidad] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TramitesSiger_Modalidad",
                table: "TramitesSiger",
                sql: "[Modalidad] IS NULL OR [Modalidad] IN ('Virtual', 'Presencial', 'Hibrido')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TramitesSiger_Sol",
                table: "TramitesSiger",
                sql: "[EstaEnSol] = 0 OR ([SolUrl] IS NOT NULL AND ([SolUrl] LIKE 'http://%' OR [SolUrl] LIKE 'https://%'))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PasosSiger_Modalidad",
                table: "PasosSiger",
                sql: "[Modalidad] IS NULL OR [Modalidad] IN ('Virtual', 'Presencial', 'Hibrido', 'Interno')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Instituciones_SitioWeb",
                table: "Instituciones",
                sql: "[SitioWeb] IS NULL OR [SitioWeb] LIKE 'http://%' OR [SitioWeb] LIKE 'https://%'");

            migrationBuilder.CreateIndex(
                name: "IX_CategoriasTramite_Nombre",
                table: "CategoriasTramite",
                column: "Nombre",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TramitesSiger_CategoriasTramite_CategoriaId",
                table: "TramitesSiger",
                column: "CategoriaId",
                principalTable: "CategoriasTramite",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TramitesSiger_CategoriasTramite_CategoriaId",
                table: "TramitesSiger");

            migrationBuilder.DropTable(
                name: "CategoriasTramite");

            migrationBuilder.DropIndex(
                name: "IX_TramitesSiger_Catalogo",
                table: "TramitesSiger");

            migrationBuilder.DropIndex(
                name: "IX_TramitesSiger_CategoriaId",
                table: "TramitesSiger");

            migrationBuilder.DropIndex(
                name: "IX_TramitesSiger_EstaEnSol",
                table: "TramitesSiger");

            migrationBuilder.DropIndex(
                name: "IX_TramitesSiger_Modalidad",
                table: "TramitesSiger");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TramitesSiger_Modalidad",
                table: "TramitesSiger");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TramitesSiger_Sol",
                table: "TramitesSiger");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PasosSiger_Modalidad",
                table: "PasosSiger");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Instituciones_SitioWeb",
                table: "Instituciones");

            migrationBuilder.DropColumn(
                name: "CategoriaId",
                table: "TramitesSiger");

            migrationBuilder.DropColumn(
                name: "CostoEsGratuito",
                table: "TramitesSiger");

            migrationBuilder.DropColumn(
                name: "CostoTexto",
                table: "TramitesSiger");

            migrationBuilder.DropColumn(
                name: "EsPopular",
                table: "TramitesSiger");

            migrationBuilder.DropColumn(
                name: "EstaEnSol",
                table: "TramitesSiger");

            migrationBuilder.DropColumn(
                name: "Modalidad",
                table: "TramitesSiger");

            migrationBuilder.DropColumn(
                name: "SolUrl",
                table: "TramitesSiger");

            migrationBuilder.DropColumn(
                name: "SolVerificadoEl",
                table: "TramitesSiger");

            migrationBuilder.DropColumn(
                name: "TiempoTexto",
                table: "TramitesSiger");

            migrationBuilder.DropColumn(
                name: "Modalidad",
                table: "PasosSiger");

            migrationBuilder.DropColumn(
                name: "Titulo",
                table: "PasosSiger");

            migrationBuilder.DropColumn(
                name: "Direccion",
                table: "Instituciones");

            migrationBuilder.DropColumn(
                name: "Horario",
                table: "Instituciones");

            migrationBuilder.DropColumn(
                name: "SitioWeb",
                table: "Instituciones");

            migrationBuilder.DropColumn(
                name: "Telefono",
                table: "Instituciones");

            migrationBuilder.DropColumn(
                name: "Tipo",
                table: "Instituciones");

            migrationBuilder.AlterColumn<string>(
                name: "NombreCorto",
                table: "Instituciones",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30,
                oldNullable: true);
        }
    }
}

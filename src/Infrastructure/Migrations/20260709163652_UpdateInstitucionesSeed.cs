using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Diger.TramitesEstado.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateInstitucionesSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "1");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "10");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "11");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "12");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "13");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "14");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "15");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "16");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "17");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "18");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "19");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "2");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "20");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "21");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "22");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "23");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "24");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "3");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "4");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "5");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "6");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "7");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "8");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "9");

            migrationBuilder.UpdateData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "DIGER",
                column: "Nombre",
                value: "Dirección de Gestión por Resultados");

            migrationBuilder.InsertData(
                table: "Instituciones",
                columns: new[] { "Id", "Activo", "CreatedAt", "CreatedBy", "Descripcion", "InfoExtra", "LogoUrl", "Nombre", "NombreCorto", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { "BANHPROVI", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "Banco Hondureño para la Producción y la Vivienda", null, null, null },
                    { "CANATURH", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "Cámara Nacional de Turismo de Honduras", null, null, null },
                    { "CNBS", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "Comisión Nacional de Bancos y Seguros", null, null, null },
                    { "CONATEL", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "Comisión Nacional de Telecomunicaciones", null, null, null },
                    { "CONSUCOOP", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "Consejo Nacional Supervisor de Cooperativas", null, null, null },
                    { "CONVIVIENDA", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "Comisión Nacional de Vivienda y Asentamientos Humanos", null, null, null },
                    { "COPECO", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "Secretaría de Estado en los Despachos de Gestión de Riesgos y Contingencias Nacionales", null, null, null },
                    { "FOSOVI", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "Fondo Social de Vivienda", null, null, null },
                    { "IHADFA", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "Instituto Hondureño para la Prevención del Alcoholismo, Drogadicción y Farmacodependencia", null, null, null },
                    { "IHCINE", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "Instituto Hondureño de Cinematografía", null, null, null },
                    { "IHT", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "Instituto Hondureño de Turismo", null, null, null },
                    { "IHTT", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "Instituto Hondureño del Transporte Terrestre", null, null, null },
                    { "INPREMA", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "Instituto Nacional de Previsión del Magisterio", null, null, null },
                    { "INPREUNAH", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "Instituto de Previsión de la Universidad Nacional Autónoma de Honduras", null, null, null },
                    { "IP", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "Instituto de la Propiedad", null, null, null },
                    { "SAG", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "Secretaría de Agricultura y Ganadería", null, null, null },
                    { "SECAPPH", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "Secretaría de las Culturas, las Artes y los Patrimonios de los Pueblos de Honduras", null, null, null },
                    { "SEN", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "Secretaría de Energía", null, null, null },
                    { "SENASA", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "Servicio Nacional de Sanidad e Inocuidad Agroalimentaria", null, null, null },
                    { "SERNA", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "Secretaría de Recursos Naturales y Ambiente", null, null, null },
                    { "SESAL", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "Secretaría de Salud", null, null, null },
                    { "SGJD", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "Secretaría de Gobernación, Justicia y Descentralización", null, null, null },
                    { "SIT", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "Secretaría de Infraestructura y Transporte", null, null, null },
                    { "SRECI", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "Secretaría de Relaciones Exteriores y Cooperación Internacional", null, null, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "BANHPROVI");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "CANATURH");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "CNBS");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "CONATEL");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "CONSUCOOP");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "CONVIVIENDA");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "COPECO");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "FOSOVI");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "IHADFA");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "IHCINE");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "IHT");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "IHTT");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "INPREMA");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "INPREUNAH");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "IP");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "SAG");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "SECAPPH");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "SEN");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "SENASA");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "SERNA");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "SESAL");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "SGJD");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "SIT");

            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "SRECI");

            migrationBuilder.UpdateData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "DIGER",
                column: "Nombre",
                value: "DIGER (Sistema)");

            migrationBuilder.InsertData(
                table: "Instituciones",
                columns: new[] { "Id", "Activo", "CreatedAt", "CreatedBy", "Descripcion", "InfoExtra", "LogoUrl", "Nombre", "NombreCorto", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { "1", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "CONVIVIENDA", null, null, null },
                    { "10", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "SEN", null, null, null },
                    { "11", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "CONSUCOOP", null, null, null },
                    { "12", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "CONATEL", null, null, null },
                    { "13", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "IHCINE", null, null, null },
                    { "14", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "SAG", null, null, null },
                    { "15", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "SECAPPH", null, null, null },
                    { "16", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "SRECI", null, null, null },
                    { "17", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "SERNA", null, null, null },
                    { "18", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "SGJD", null, null, null },
                    { "19", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "CANATURH / IHT", null, null, null },
                    { "2", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "COPECO", null, null, null },
                    { "20", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "IP", null, null, null },
                    { "21", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "SENASA", null, null, null },
                    { "22", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "SESAL", null, null, null },
                    { "23", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "FOSOVI", null, null, null },
                    { "24", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "IHT", null, null, null },
                    { "3", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "SIT", null, null, null },
                    { "4", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "IHADFA", null, null, null },
                    { "5", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "BANHPROVI", null, null, null },
                    { "6", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "INPREUNAH", null, null, null },
                    { "7", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "CNBS", null, null, null },
                    { "8", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "INPREMA", null, null, null },
                    { "9", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "IHTT", null, null, null }
                });
        }
    }
}

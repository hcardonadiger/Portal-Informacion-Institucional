using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FusionRamaDev : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Migracion de fusion, a proposito vacia.
            //
            // Nace de unir dev (modulo de Proyectos) con Jamil (Siger y fichas publicas) el
            // 27-08-2026. Las dos ramas anadieron migraciones en las mismas fechas, asi que el
            // AppDbContextModelSnapshot quedo en conflicto y hubo que regenerarlo.
            //
            // Al regenerarlo, EF calculo la diferencia contra el snapshot de dev y propuso crear
            // las tablas de Jamil (FotosTramiteSiger, PropuestasLlenado, ExpedienteTramiteLugares,
            // ExpedienteTramiteEntregables) con sus columnas y semillas. Se comprobo que esa
            // diferencia no tocaba nada de Proyectos: era exactamente el aporte de Jamil.
            //
            // Ese trabajo YA lo hacen las migraciones propias de Jamil, que siguen en la lista.
            // Ejecutarlo aqui otra vez fallaria por objetos duplicados. Lo que hacia falta no era
            // el SQL sino el snapshot: por eso el cuerpo va vacio y el Designer de este archivo
            // guarda el modelo unificado, que es contra el que se generara la proxima migracion.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nada que deshacer: Up no hace nada.
        }
    }
}

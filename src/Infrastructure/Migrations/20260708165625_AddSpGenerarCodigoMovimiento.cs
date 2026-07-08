using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSpGenerarCodigoMovimiento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
            CREATE OR ALTER PROCEDURE SP_GenerarCodigoMovimiento
                @Institucion VARCHAR(50),
                @Movimiento VARCHAR(50),
                @NuevoCodigo VARCHAR(100) OUTPUT
            AS
            BEGIN
                SET NOCOUNT ON;
                
                BEGIN TRANSACTION;
                
                DECLARE @ActualValor INT = 0;
                
                -- UPDLOCK para evitar concurrencia
                SELECT @ActualValor = UltimoValor
                FROM Prefijos WITH (UPDLOCK, HOLDLOCK)
                WHERE PrefijoInstitucion = @Institucion AND PrefijoMovimiento = @Movimiento;
                
                IF @@ROWCOUNT = 0
                BEGIN
                    SET @ActualValor = 1;
                    SET @NuevoCodigo = @Institucion + '-' + @Movimiento + '-1';
                    
                    INSERT INTO Prefijos (PrefijoInstitucion, PrefijoMovimiento, UltimoValor, UltimoCodigo)
                    VALUES (@Institucion, @Movimiento, @ActualValor, @NuevoCodigo);
                END
                ELSE
                BEGIN
                    SET @ActualValor = @ActualValor + 1;
                    SET @NuevoCodigo = @Institucion + '-' + @Movimiento + '-' + CAST(@ActualValor AS VARCHAR(20));
                    
                    UPDATE Prefijos
                    SET UltimoValor = @ActualValor,
                        UltimoCodigo = @NuevoCodigo
                    WHERE PrefijoInstitucion = @Institucion AND PrefijoMovimiento = @Movimiento;
                END
                
                COMMIT TRANSACTION;
            END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS SP_GenerarCodigoMovimiento;");
        }
    }
}

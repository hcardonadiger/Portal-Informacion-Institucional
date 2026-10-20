<#
.SYNOPSIS
    Crea o refresca el sandbox: la copia a la que apunta el sistema al encenderlo.

.DESCRIPTION
    A partir de este guion, arrancar los sistemas en Development NO conecta con las bases
    reales sino con DigerTramitesEstado_Sandbox y VentanillaDigital_Sandbox. Eso esta escrito
    en los appsettings.Development.json de los tres proyectos, asi que vale igual si se
    arranca desde Visual Studio, desde dotnet run o desde Iniciar-Todo.ps1.

    El sandbox es para ensuciarlo. Se puede crear, editar, publicar y borrar lo que sea. Si en
    algun momento quedo tan revuelto que estorba, se vuelve a correr este guion con -Rehacer y
    queda como recien copiado.

    Ademas de copiar, siembra. La base real es un volcado crudo de SIGER: ningun tramite
    publicado, ninguno con categoria, modalidad, costo ni tiempo. Contra una copia tal cual,
    HondurasSimple no muestra absolutamente nada y no hay nada que probar. El sembrado publica
    los tramites que tienen con que llenar una ficha y les pone categoria, modalidad, costo y
    tiempo de relleno. Ese relleno es inventado y esta puesto para que las pantallas tengan
    variedad: lo que se prueba es el comportamiento, no la exactitud del dato.

.EXAMPLE
    .\Refrescar-Sandbox.ps1              # lo crea si no existe; si existe, avisa y no lo pisa
    .\Refrescar-Sandbox.ps1 -Rehacer     # lo tira y lo vuelve a copiar desde las reales
    .\Refrescar-Sandbox.ps1 -SoloSembrar # no copia; solo vuelve a sembrar lo que ya hay
#>

[CmdletBinding()]
param(
    [string] $Instancia = 'LP-GD-JAGM\SQLEXPRESS',

    [string] $BasePortalReal     = 'DigerTramitesEstado_Unificada',
    [string] $BaseVentanillaReal = 'VentanillaDigital_Net',

    [switch] $Rehacer,
    [switch] $SoloSembrar
)

. (Join-Path $PSScriptRoot '_Comun.ps1')

$BasePortalSandbox     = $BasePortalReal + $script:SufijoSandbox
$BaseVentanillaSandbox = $BaseVentanillaReal + $script:SufijoSandbox

Assert-EsCopiaDesechable $BasePortalSandbox
Assert-EsCopiaDesechable $BaseVentanillaSandbox

Write-Host ''
Write-Host 'Sandbox permanente' -ForegroundColor White
Write-Host "  $BasePortalReal -> $BasePortalSandbox"
Write-Host "  $BaseVentanillaReal -> $BaseVentanillaSandbox"
Write-Host ''
if (-not $SoloSembrar) {
    New-CopiaDeBase -Instancia $Instancia -Origen $BasePortalReal     -Copia $BasePortalSandbox     -Rehacer:$Rehacer
    New-CopiaDeBase -Instancia $Instancia -Origen $BaseVentanillaReal -Copia $BaseVentanillaSandbox -Rehacer:$Rehacer
}

if (-not (Test-BaseExiste -Instancia $Instancia -Base $BasePortalSandbox)) {
    throw "No existe $BasePortalSandbox. Corra el guion sin -SoloSembrar."
}

# El sembrado se salta si el sandbox ya tiene tramites publicados: correrlo de nuevo no rompe
# nada -es idempotente, la semilla es el Id- pero pisaria lo que se haya publicado a mano
# durante las pruebas, y eso si sorprende.
$publicados = [int]((Invoke-Sql -Instancia $Instancia -Consulta "SET NOCOUNT ON; USE [$BasePortalSandbox]; SELECT COUNT(*) FROM TramitesSiger WHERE Publicado = 1;" |
    Where-Object { $_ -match '^\d+$' } | Select-Object -First 1))

if ($publicados -gt 0 -and -not $Rehacer -and -not $SoloSembrar) {
    Write-Aviso "$BasePortalSandbox ya tiene $publicados tramites publicados; no se vuelve a sembrar."
} else {
    Write-Paso 'Sembrando el sandbox...'

    # -f 65001 obligatorio: el guion trae tildes y sin UTF-8 entran rotas a la base.
    $guion = Join-Path $PSScriptRoot 'sql\sembrar-sandbox.sql'
    $salida = & sqlcmd -S $Instancia -E -b -d $BasePortalSandbox -f 65001 -i $guion 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "El sembrado fallo:`n$($salida -join [Environment]::NewLine)"
    }
    $salida | Where-Object { $_ -match '\S' } | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray }
    Write-Ok 'Sandbox sembrado'
}

# La replica del portal ciudadano se vacia a proposito. HondurasSimple no es dueño de estos
# datos: los copia de la API. Si se dejara la replica vieja, se veria el catalogo anterior
# hasta que termine la primera reconciliacion, y eso confunde a quien esta probando.
Write-Paso 'Vaciando la replica de HondurasSimple para que se reconstruya desde la API...'
$sqlVaciar = @"
USE [$BaseVentanillaSandbox];
DELETE FROM PortalTramitePasos;      DELETE FROM PortalTramiteRequisitos;
DELETE FROM PortalTramiteEntregables; DELETE FROM PortalTramiteLugares;
DELETE FROM PortalTramiteEnlaces;    DELETE FROM PortalTramites;
DELETE FROM PortalInstituciones;     DELETE FROM PortalCategorias;
DELETE FROM PortalEstadoSincronizacion;
"@
Invoke-Sql -Instancia $Instancia -Consulta $sqlVaciar | Out-Null
Write-Ok 'Replica vaciada'

Write-Host ''
Write-Ok 'Sandbox listo.'
Write-Host ''
Write-Host '  Los tres sistemas en Development ya apuntan aca. Para levantarlos:' -ForegroundColor White
Write-Host '    ..\Iniciar-Todo.ps1' -ForegroundColor White
Write-Host ''
Write-Host '  La primera sincronizacion de HondurasSimple tarda un momento: arranca sola' -ForegroundColor DarkGray
Write-Host '  al levantar el sitio y vuelve a pasar cada 10 segundos.' -ForegroundColor DarkGray
Write-Host ''

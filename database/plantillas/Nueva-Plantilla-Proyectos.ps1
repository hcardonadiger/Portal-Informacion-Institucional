<#
.SYNOPSIS
    Genera la plantilla Excel de captura de proyectos, con los catálogos vivos de la base.

.DESCRIPTION
    La plantilla es el formato que se le entrega a las áreas para que llenen sus proyectos.
    Se regenera —en vez de editarse a mano— porque las listas desplegables salen de la base:
    usuarios activos, áreas, unidades e instituciones cambian, y una plantilla con catálogos
    viejos produce importaciones que fallan por un correo o un código que ya no existe.

    El archivo que produce lo consume Generar-SQL-Proyectos.ps1.

.EXAMPLE
    .\Nueva-Plantilla-Proyectos.ps1
    .\Nueva-Plantilla-Proyectos.ps1 -Usuario sa -Clave '***' -Salida 'C:\temp\plantilla.xlsx'
#>
[CmdletBinding()]
param(
    [string] $Servidor = 'localhost\SQL2025',
    [string] $BaseDatos = 'GestionGD_TEST',
    [string] $Usuario,
    [string] $Clave,
    [string] $Salida = (Join-Path $PSScriptRoot 'Plantilla_Importacion_Proyectos.xlsx'),

    # Contraseña para desproteger las hojas. Por omisión no lleva: la protección está para
    # evitar el borrado accidental de los catálogos, no para impedirle nada a quien sepa lo que
    # hace, y una contraseña olvidada deja la plantilla inservible. Se puede poner igual.
    [string] $ClaveProteccion
)

$ErrorActionPreference = 'Stop'

# ── Catálogos ────────────────────────────────────────────────────────────────
function Get-Catalogo([string] $Consulta) {
    # Ojo: no llamar $args a esta variable — es automática en PowerShell y se pisa sola.
    $sqlArgs = @('-S', $Servidor, '-d', $BaseDatos, '-h', '-1', '-W', '-s', '|', '-Q', "SET NOCOUNT ON; $Consulta")
    if ($Usuario) { $sqlArgs += @('-U', $Usuario, '-P', $Clave) } else { $sqlArgs += '-E' }

    $salida = & sqlcmd.exe @sqlArgs
    if ($LASTEXITCODE -ne 0) {
        # El caso frecuente: el usuario de Windows no tiene login en la instancia, así que la
        # autenticación integrada que este script usa por omisión no sirve. El error de sqlcmd
        # («Login failed for user DOMINIO\usuario») no sugiere la salida, así que se dice acá.
        # No se filtra por el texto del error: sqlcmd lo manda a stderr, así que $salida viene
        # vacío y no hay nada que buscarle.
        if (-not $Usuario) {
            throw "No se pudo entrar con la cuenta de Windows. Vuelva a correrlo indicando el " +
                  "usuario de SQL Server:`n`n" +
                  "    .\Nueva-Plantilla-Proyectos.ps1 -Usuario sa -Clave '<la clave>'`n"
        }
        throw "sqlcmd falló consultando el catálogo: $salida"
    }

    # sqlcmd cierra con una línea de conteo y a veces con vacías; se descartan.
    $salida | Where-Object { $_ -and $_.Trim() -and $_ -notmatch '^\(\d+ (rows|filas)' } | ForEach-Object { $_.Trim() }
}

Write-Host 'Leyendo catálogos de la base...' -ForegroundColor Cyan

$instituciones = Get-Catalogo "SELECT Id FROM Instituciones ORDER BY CASE WHEN Id='DIGER' THEN 0 ELSE 1 END, Id"
$areas         = Get-Catalogo "SELECT a.Id + ' — ' + a.Nombre FROM Areas a ORDER BY a.InstitucionId, a.Nombre"
$unidades      = Get-Catalogo "SELECT u.Id + ' — ' + u.Nombre FROM Unidades u ORDER BY u.Nombre"
$usuarios      = Get-Catalogo "SELECT u.Correo + ' — ' + u.Nombre FROM Usuarios u WHERE u.Activo = 1 ORDER BY u.Nombre"

# 2026-09-18: la prioridad dejó de ser un enum del código y pasó a ser el catálogo administrable
# PrioridadesProyecto, que se edita en Catálogos › Prioridades de proyectos. Por eso se lee de la
# base como las instituciones y no se escribe acá: si alguien agrega «Q3», la próxima plantilla
# que se genere ya la ofrece, sin tocar este archivo.
$prioridades   = Get-Catalogo "SELECT Nombre FROM PrioridadesProyecto WHERE Activo = 1 ORDER BY Orden, Nombre"

if (-not $instituciones) { throw 'El catálogo de instituciones vino vacío: revise la conexión.' }
if (-not $prioridades)   { throw 'El catálogo de prioridades vino vacío: ¿ya corrió la migración CatalogoDePrioridadesDeProyecto?' }

# Enums del dominio. Van literales a propósito: son parte del contrato con el código
# (src/Domain/Enums/Enums.cs, RiesgoProyecto.cs, InteresadoProyecto.cs), no datos de la base,
# y si alguno cambia tiene que cambiar acá también.
$estadosProyecto   = @('Planificado', 'EnEjecucion', 'Suspendido', 'Cerrado', 'Cancelado')
$estadosEntregable = @('Pendiente', 'EnProceso', 'Completado', 'Cancelado')
# La actividad no reusa los del entregable por concordancia: «Actividad: Completado» se lee como
# un error de tipeo. Es la misma razón por la que el dominio tiene dos enums.
$estadosActividad  = @('Pendiente', 'EnProceso', 'Completada', 'Cancelada')
# Qué hace DIGER en el proyecto. Sin tilde: el valor se guarda como texto y ese texto es el
# identificador de C#; el acento se lo pone el portal al mostrarlo.
$acciones          = @('Acompanamiento', 'Digitalizacion', 'Soporte', 'Desarrollo')
$niveles          = @('Alta', 'Media', 'Baja')
$rolesInteresado  = @('Patrocinador', 'Ejecutor', 'ContraparteTecnica', 'Beneficiario', 'Regulador')
$categoriasRiesgo = @('Tecnico', 'Institucional', 'Normativo', 'Financiero', 'Operativo', 'Externo')
$estrategias      = @('Evitar', 'Mitigar', 'Transferir', 'Aceptar')
$estadosRiesgo    = @('Abierto', 'EnTratamiento', 'Materializado', 'Cerrado')

# ── Excel ────────────────────────────────────────────────────────────────────
$AZUL     = 6970168    # BGR de #1E3A5F, el azul del portal
$AMARILLO = 14804223   # #FFE699 — obligatorio
$GRIS     = 15921906   # #F2F2F2 — fila de ejemplo
$BLANCO   = 16777215
$TEXTOGRIS = 8421504

$FILAS_VALIDACION = 500   # hasta dónde llegan las listas desplegables

<# El destino se comprueba ANTES de abrir Excel.

   Antes se comprobaba al final, al momento de guardar: si el archivo estaba abierto —cosa que
   pasa seguido, porque uno lo tiene en pantalla mientras lo revisa— el script moría con Excel ya
   levantado y dejaba un proceso EXCEL.EXE huérfano, invisible y con el archivo tomado. #>
function Test-ArchivoLibre([string] $Ruta) {
    if (-not (Test-Path $Ruta)) { return $true }
    try {
        $fs = [IO.File]::Open($Ruta, 'Open', 'ReadWrite', 'None')
        $fs.Close(); $fs.Dispose()
        return $true
    } catch { return $false }
}

if (-not (Test-ArchivoLibre $Salida)) {
    throw "La plantilla está abierta en otro programa y no se puede sobrescribir:`n`n" +
          "    $Salida`n`n" +
          "Ciérrela en Excel y vuelva a correr el script, o mande la nueva a otro lado con -Salida.`n"
}

$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false
$libro = $excel.Workbooks.Add()

# Desde acá Excel está levantado: si algo falla, hay que cerrarlo igual o queda un proceso
# huérfano reteniendo archivos.
try {

# La hoja que trae el libro nuevo se llama «Hoja1» o «Sheet1» según el idioma de Excel:
# se guarda la referencia ahora y se borra al final, en vez de buscarla por nombre.
$hojaInicial = $libro.Worksheets.Item(1)

function New-Hoja([string] $Nombre) {
    $h = $libro.Worksheets.Add([System.Reflection.Missing]::Value, $libro.Worksheets.Item($libro.Worksheets.Count))
    $h.Name = $Nombre
    $h.Cells.Font.Name = 'Arial'
    $h.Cells.Font.Size = 10
    return $h
}

function Set-Fila($Hoja, [int] $Fila, [string[]] $Valores) {
    for ($i = 0; $i -lt $Valores.Count; $i++) { $Hoja.Cells.Item($Fila, $i + 1).Value2 = $Valores[$i] }
}

<# Encabezado de una hoja de captura: título en A1, cabeceras en la fila 3.
   Las obligatorias se pintan de amarillo, que es la misma señal que usa la leyenda. #>
function Set-Encabezado($Hoja, [string] $Titulo, [string] $Ayuda, [string[]] $Columnas, [int[]] $Obligatorias, [int[]] $Anchos) {
    $Hoja.Cells.Item(1, 1).Value2 = $Titulo
    $Hoja.Cells.Item(1, 1).Font.Size = 14
    $Hoja.Cells.Item(1, 1).Font.Bold = $true
    $Hoja.Cells.Item(1, 1).Font.Color = $AZUL

    $Hoja.Cells.Item(2, 1).Value2 = $Ayuda
    $Hoja.Cells.Item(2, 1).Font.Italic = $true
    $Hoja.Cells.Item(2, 1).Font.Color = $TEXTOGRIS

    Set-Fila $Hoja 3 $Columnas
    $rango = $Hoja.Range($Hoja.Cells.Item(3, 1), $Hoja.Cells.Item(3, $Columnas.Count))
    $rango.Font.Bold = $true
    $rango.Font.Color = $BLANCO
    $rango.Interior.Color = $AZUL
    $rango.HorizontalAlignment = -4131
    $rango.VerticalAlignment = -4108
    $rango.WrapText = $true
    $Hoja.Rows.Item(3).RowHeight = 30

    foreach ($c in $Obligatorias) {
        $Hoja.Cells.Item(3, $c).Interior.Color = $AMARILLO
        $Hoja.Cells.Item(3, $c).Font.Color = 0
    }

    for ($i = 0; $i -lt $Anchos.Count; $i++) { $Hoja.Columns.Item($i + 1).ColumnWidth = $Anchos[$i] }

    # FreezePanes actúa sobre la ventana activa, así que la hoja tiene que estar al frente.
    $Hoja.Activate()
    $Hoja.Application.ActiveWindow.FreezePanes = $false
    $Hoja.Range('A4').Select() | Out-Null
    $Hoja.Application.ActiveWindow.FreezePanes = $true
    $rango.AutoFilter() | Out-Null
}

function Set-Ejemplo($Hoja, [string[]] $Valores) {
    Set-Fila $Hoja 4 $Valores
    $r = $Hoja.Range($Hoja.Cells.Item(4, 1), $Hoja.Cells.Item(4, $Valores.Count))
    $r.Font.Italic = $true
    $r.Font.Color = $TEXTOGRIS
    $r.Interior.Color = $GRIS
}

<# Lista desplegable sobre una columna entera de captura.
   xlValidateList = 3, xlValidAlertStop = 1: un valor fuera de la lista se rechaza en vez de
   avisar, porque el importador no tiene forma de adivinar a qué enum quiso referirse. #>
function Set-Lista($Hoja, [int] $Columna, [string] $Nombre) {
    $col = $Hoja.Range($Hoja.Cells.Item(4, $Columna), $Hoja.Cells.Item($FILAS_VALIDACION, $Columna))
    $col.Validation.Delete()
    $col.Validation.Add(3, 1, 1, "=$Nombre") | Out-Null
    $col.Validation.IgnoreBlank = $true
    $col.Validation.InCellDropdown = $true
    $col.Validation.ErrorTitle = 'Valor no permitido'
    $col.Validation.ErrorMessage = 'Elija uno de la lista. El importador solo reconoce esos valores.'
}

<#
    Deja editable solo la zona de captura y cierra el resto.

    En Excel todas las celdas nacen con Locked = True, pero eso no hace nada hasta que la hoja se
    protege: por eso hay que desbloquear primero lo que sí se llena y proteger después, y no al
    revés. Las filas 1 a 3 —título, ayuda y encabezados— quedan bloqueadas porque el importador
    busca las columnas por nombre y por posición: si alguien mueve una, el archivo deja de servir.

    La protección va sin contraseña salvo que se pida una. Lo que evita es el accidente, no al
    usuario decidido; quien de verdad necesite reacomodar algo entra por Revisar > Desproteger.
#>
function Protect-Hoja($Hoja, [switch] $TodoBloqueado) {
    $sinClave = [System.Type]::Missing
    $clave    = if ($ClaveProteccion) { $ClaveProteccion } else { $sinClave }

    if (-not $TodoBloqueado) {
        $Hoja.Cells.Locked = $false
        $Hoja.Range($Hoja.Rows.Item(1), $Hoja.Rows.Item(3)).Locked = $true
    }

    # Posicionales de Worksheet.Protect: se permiten formato, insertar y borrar filas, ordenar y
    # usar el autofiltro; se prohíbe tocar columnas, que es donde vive el contrato con el importador.
    $Hoja.Protect($clave, $true, $true, $false, $false,
                  $true,   # AllowFormattingCells
                  $true,   # AllowFormattingColumns  (ancho, no contenido)
                  $true,   # AllowFormattingRows
                  $false,  # AllowInsertingColumns
                  $true,   # AllowInsertingRows
                  $false,  # AllowInsertingHyperlinks
                  $false,  # AllowDeletingColumns
                  $true,   # AllowDeletingRows
                  $true,   # AllowSorting
                  $true,   # AllowFiltering
                  $false)  # AllowUsingPivotTables
}

function Set-Fecha($Hoja, [int] $Columna) {
    $col = $Hoja.Range($Hoja.Cells.Item(4, $Columna), $Hoja.Cells.Item($FILAS_VALIDACION, $Columna))
    $col.NumberFormat = 'yyyy-mm-dd'
    $col.HorizontalAlignment = -4108
}

# ── Hoja: Catálogos ──────────────────────────────────────────────────────────
# Va primero porque los rangos con nombre tienen que existir antes de que las otras hojas
# los referencien en sus validaciones.
$cat = New-Hoja 'Catalogos'
$cat.Cells.Item(1, 1).Value2 = 'Catálogos — no editar'
$cat.Cells.Item(1, 1).Font.Size = 14
$cat.Cells.Item(1, 1).Font.Bold = $true
$cat.Cells.Item(1, 1).Font.Color = $AZUL
$cat.Cells.Item(2, 1).Value2 = 'Los alimenta la base. Si falta un usuario o una unidad, créela primero en el portal y vuelva a generar la plantilla.'
$cat.Cells.Item(2, 1).Font.Italic = $true
$cat.Cells.Item(2, 1).Font.Color = $TEXTOGRIS

$listas = [ordered]@{
    'lstPrioridad'    = @{ Titulo = 'Prioridad';        Datos = $prioridades }
    'lstAccion'       = @{ Titulo = 'Acción';           Datos = $acciones }
    'lstEstadoProy'   = @{ Titulo = 'Estado proyecto';  Datos = $estadosProyecto }
    'lstEstadoEntr'   = @{ Titulo = 'Estado entregable'; Datos = $estadosEntregable }
    'lstEstadoActiv'  = @{ Titulo = 'Estado actividad'; Datos = $estadosActividad }
    'lstNivel'        = @{ Titulo = 'Nivel';            Datos = $niveles }
    'lstRolInt'       = @{ Titulo = 'Rol interesado';   Datos = $rolesInteresado }
    'lstCatRiesgo'    = @{ Titulo = 'Categoría riesgo'; Datos = $categoriasRiesgo }
    'lstEstrategia'   = @{ Titulo = 'Estrategia';       Datos = $estrategias }
    'lstEstadoRiesgo' = @{ Titulo = 'Estado riesgo';    Datos = $estadosRiesgo }
    'lstInstitucion'  = @{ Titulo = 'Institución';      Datos = $instituciones }
    'lstArea'         = @{ Titulo = 'Área';             Datos = $areas }
    'lstUnidad'       = @{ Titulo = 'Unidad';           Datos = $unidades }
    'lstUsuario'      = @{ Titulo = 'Usuario (correo)'; Datos = $usuarios }
}

$col = 1
foreach ($nombre in $listas.Keys) {
    $titulo = $listas[$nombre].Titulo
    $datos  = @($listas[$nombre].Datos)

    $cat.Cells.Item(4, $col).Value2 = $titulo
    $cat.Cells.Item(4, $col).Font.Bold = $true
    $cat.Cells.Item(4, $col).Font.Color = $BLANCO
    $cat.Cells.Item(4, $col).Interior.Color = $AZUL

    for ($i = 0; $i -lt $datos.Count; $i++) { $cat.Cells.Item(5 + $i, $col).Value2 = $datos[$i] }
    $cat.Columns.Item($col).ColumnWidth = 34

    $letra = [char]([int][char]'A' + $col - 1)   # 14 listas: no se pasa de la columna Z
    $libro.Names.Add($nombre, "=Catalogos!`$$letra`$5:`$$letra`$$(4 + $datos.Count)") | Out-Null
    $col++
}
$cat.Activate()
$cat.Application.ActiveWindow.FreezePanes = $false
$cat.Range('A5').Select() | Out-Null
$cat.Application.ActiveWindow.FreezePanes = $true

# ── Hoja: Proyectos ──────────────────────────────────────────────────────────
$hp = New-Hoja 'Proyectos'
Set-Encabezado $hp 'Proyectos' `
    'Normalmente una sola fila: se reparte un archivo por proyecto. La Ref («P1») amarra las demás hojas con esta, y no se guarda en el sistema; si llena varios proyectos acá, déle una Ref distinta a cada uno.' `
    @('Ref *', 'Nombre *', 'Objetivo', 'Institución ejecutora *', 'Área', 'Unidad', 'Responsable (correo)', 'Prioridad *', 'Acción', 'Estado *', 'Inicio planificado', 'Fin planificado', 'Inicio real', 'Fin real') `
    @(1, 2, 4, 8, 10) `
    @(8, 42, 52, 20, 26, 26, 30, 11, 16, 14, 15, 15, 14, 14)

Set-Ejemplo $hp @('EJEMPLO', 'SOL — Secretaría de Finanzas', 'Habilitar en la plataforma SOL los 6 trámites de mayor demanda de SEFIN.', 'DIGER', 'GOBDIG — GOBIERNO DIGITAL', 'DITRA — DIGITALIZACION DE TRAMITES', 'hcardona@diger.gob.hn', 'Alta', 'Digitalizacion', 'EnEjecucion', '2026-03-02', '2026-11-30', '2026-03-09', '')

Set-Lista $hp 4  'lstInstitucion'
Set-Lista $hp 5  'lstArea'
Set-Lista $hp 6  'lstUnidad'
Set-Lista $hp 7  'lstUsuario'
Set-Lista $hp 8  'lstPrioridad'
Set-Lista $hp 9  'lstAccion'
Set-Lista $hp 10 'lstEstadoProy'
Set-Fecha $hp 11; Set-Fecha $hp 12; Set-Fecha $hp 13; Set-Fecha $hp 14

# El avance del proyecto ya no se declara acá: desde la reestructuración de entregables y
# actividades lo calcula el árbol —promedio de las actividades, subido por los entregables— y
# pedirlo en la plantilla solo daba pie a que el número escrito contradijera al calculado.
# Se captura por actividad, en la hoja Actividades.

# Las tres hojas siguientes validan su «Ref proyecto» contra esta columna, así que el nombre
# tiene que existir antes de que alguna lo mencione: Excel rechaza una validación que apunte
# a un nombre que todavía no definió.
$libro.Names.Add('lstRefProyecto', "=Proyectos!`$A`$4:`$A`$$FILAS_VALIDACION") | Out-Null

# ── Hoja: Entregables ────────────────────────────────────────────────────────
# Se llamaba «Hitos» hasta 2026-09-18. El nombre cambió con el modelo: un entregable ya no es un
# punto en el calendario sino algo que se entrega, y cuelga de él una lista de actividades.
$hh = New-Hoja 'Entregables'
Set-Encabezado $hh 'Entregables' `
    'QUÉ se entrega. La Ref proyecto tiene que existir en la hoja Proyectos. Si deja el Orden vacío se numeran en el orden en que aparecen acá. El avance del entregable no se escribe: sale de sus actividades.' `
    @('Ref proyecto *', 'Orden', 'Entregable *', 'Descripción', 'Fecha planificada', 'Fecha real', 'Estado *', 'Responsable (correo)') `
    @(1, 3, 7) `
    @(14, 8, 46, 54, 17, 15, 13, 30)

Set-Ejemplo $hh @('EJEMPLO','1', 'Levantamiento de los 6 trámites', 'Fichas técnicas validadas con la contraparte de SEFIN.', '2026-04-15', '2026-04-22', 'Completado', 'hcardona@diger.gob.hn')

Set-Lista $hh 1 'lstRefProyecto'
Set-Lista $hh 7 'lstEstadoEntr'
Set-Lista $hh 8 'lstUsuario'
Set-Fecha $hh 5; Set-Fecha $hh 6

# La hoja Actividades valida su «Entregable» contra esta columna, así que el nombre tiene que
# existir antes de que aquélla lo mencione.
$libro.Names.Add('lstEntregable', "=Entregables!`$C`$4:`$C`$$FILAS_VALIDACION") | Out-Null

# ── Hoja: Actividades ────────────────────────────────────────────────────────
$ha = New-Hoja 'Actividades'
Set-Encabezado $ha 'Actividades' `
    'CÓMO se llega al entregable. Es el nivel donde se reporta el avance: el porcentaje del entregable es el promedio de sus actividades, y el del proyecto el promedio de los entregables. «Depende de» es el nombre de otra actividad del mismo entregable que tiene que terminar antes.' `
    @('Ref proyecto *', 'Entregable *', 'Orden', 'Actividad *', 'Descripción', 'Responsable (correo)', 'Inicio planificado', 'Fin planificado', 'Estado *', 'Avance %', 'Inicio real', 'Fin real', 'Depende de') `
    @(1, 2, 4, 9) `
    @(14, 46, 8, 46, 50, 30, 17, 16, 14, 10, 14, 14, 40)

Set-Ejemplo $ha @('EJEMPLO', 'Levantamiento de los 6 trámites', '1', 'Entrevistas con las ventanillas de SEFIN', 'Tres sesiones, una por ventanilla.', 'hcardona@diger.gob.hn', '2026-04-01', '2026-04-10', 'Completada', '100', '2026-04-01', '2026-04-09', '')

Set-Lista $ha 1 'lstRefProyecto'
Set-Lista $ha 2 'lstEntregable'
Set-Lista $ha 6 'lstUsuario'
Set-Lista $ha 9 'lstEstadoActiv'
Set-Fecha $ha 7; Set-Fecha $ha 8; Set-Fecha $ha 11; Set-Fecha $ha 12

$avance = $ha.Range($ha.Cells.Item(4, 10), $ha.Cells.Item($FILAS_VALIDACION, 10))
$avance.Validation.Delete()
$avance.Validation.Add(1, 1, 1, '0', '100') | Out-Null   # xlValidateWholeNumber, entre 0 y 100
$avance.Validation.ErrorTitle = 'Avance fuera de rango'
$avance.Validation.ErrorMessage = 'El avance es un entero de 0 a 100.'

# ── Hoja: Interesados ────────────────────────────────────────────────────────
$hi = New-Hoja 'Interesados'
Set-Encabezado $hi 'Interesados' `
    'Tienen que ser usuarios del portal: quedar como interesado ES lo que le da acceso al proyecto, aunque la persona sea de otra institución. Si no tiene cuenta, hay que crearla antes.' `
    @('Ref proyecto *', 'Usuario (correo) *', 'Participa por', 'Cargo', 'Rol *', 'Influencia *', 'Notas') `
    @(1, 2, 5, 6) `
    @(14, 34, 26, 30, 20, 13, 50)

Set-Ejemplo $hi @('EJEMPLO', 'hcardona@diger.gob.hn', 'DIGER', 'Coordinador técnico', 'Ejecutor', 'Alta', 'Lleva la relación con la contraparte.')

Set-Lista $hi 1 'lstRefProyecto'
Set-Lista $hi 2 'lstUsuario'
Set-Lista $hi 5 'lstRolInt'
Set-Lista $hi 6 'lstNivel'

# ── Hoja: Riesgos ────────────────────────────────────────────────────────────
$hr = New-Hoja 'Riesgos'
Set-Encabezado $hr 'Riesgos' `
    'La severidad la calcula el portal: probabilidad × impacto (Alta=3, Media=2, Baja=1). 6 o más sale en rojo. Un riesgo que ya ocurrió se registra como Materializado, no como Abierto.' `
    @('Ref proyecto *', 'Riesgo *', 'Categoría *', 'Probabilidad *', 'Impacto *', 'Estrategia *', 'Estado *', 'Mitigación', 'Responsable (correo)', 'Detectado el *', 'Revisar el') `
    @(1, 2, 3, 4, 5, 6, 7, 10) `
    @(14, 50, 16, 14, 12, 13, 15, 50, 30, 14, 14)

Set-Ejemplo $hr @('EJEMPLO', 'SEFIN no designa contraparte técnica y el levantamiento se detiene.', 'Institucional', 'Media', 'Alta', 'Mitigar', 'Abierto', 'Escalar a Secretaría General con nota oficial a los 15 días.', 'hcardona@diger.gob.hn', '2026-03-10', '2026-05-10')

Set-Lista $hr 1  'lstRefProyecto'
Set-Lista $hr 3  'lstCatRiesgo'
Set-Lista $hr 4  'lstNivel'
Set-Lista $hr 5  'lstNivel'
Set-Lista $hr 6  'lstEstrategia'
Set-Lista $hr 7  'lstEstadoRiesgo'
Set-Lista $hr 9  'lstUsuario'
Set-Fecha $hr 10; Set-Fecha $hr 11

# ── Hoja: Instrucciones ──────────────────────────────────────────────────────
$ins = New-Hoja 'Instrucciones'
$ins.Columns.Item(1).ColumnWidth = 3
$ins.Columns.Item(2).ColumnWidth = 108

$lineas = @(
    @('T', 'Plantilla de carga de proyectos — DIGER'),
    @('S', 'Portafolio de Gobierno Digital · generada el ' + (Get-Date -Format 'dd/MM/yyyy')),
    @('', ''),
    @('H', 'Qué llenar'),
    @('P', 'Cinco hojas: Proyectos, Entregables, Actividades, Interesados y Riesgos. Solo la primera es obligatoria — un proyecto sin lo demás se carga igual, aunque queda sin cronograma.'),
    @('P', 'Normalmente se llena UN proyecto por archivo: una sola fila en la hoja Proyectos, y el resto de las hojas referidas a ella.'),
    @('P', 'Los encabezados en amarillo con asterisco son obligatorios. Los demás pueden quedar vacíos.'),
    @('P', 'Cada hoja trae una fila de ejemplo en gris, con la Ref «EJEMPLO». Puede borrarla o dejarla: se ignora toda fila cuya Ref sea EJEMPLO.'),
    @('', ''),
    @('H', 'Entregable y actividad no son lo mismo'),
    @('P', 'El ENTREGABLE es QUÉ se entrega: «Fichas técnicas de los 6 trámites». La ACTIVIDAD es CÓMO se llega a él: «Entrevistas con las ventanillas», «Validación con la contraparte».'),
    @('P', 'Es la distinción que más se confunde. Si lo que escribió se puede entregar y revisar, es un entregable; si es trabajo que hay que hacer para llegar ahí, es una actividad.'),
    @('P', 'El avance se reporta SOLO en las actividades. El del entregable es el promedio de las suyas, y el del proyecto el promedio de los entregables: por eso no hay columna de avance ni en Proyectos ni en Entregables.'),
    @('', ''),
    @('H', 'La columna «Ref»'),
    @('P', 'Es un identificador que usted inventa («P1») para amarrar las demás hojas con su proyecto. No se guarda en el sistema: existe solo dentro de este archivo. Si llena un solo proyecto, use la misma Ref en todas las filas.'),
    @('P', 'El código real del proyecto (PRY-2026-27) lo asigna el portal. No lo escriba usted.'),
    @('', ''),
    @('H', 'Institución ejecutora'),
    @('P', 'Es quién EJECUTA el proyecto, no de quién trata. «SOL — CONSUCOOP» lo ejecuta DIGER, así que va DIGER. Esta columna decide quién puede ver el proyecto en el portal: si pone otra institución, DIGER deja de verlo.'),
    @('P', 'Área y Unidad son opcionales. Vacías = proyecto transversal, visible para toda la institución. Llenarlas lo restringe a esa área o unidad.'),
    @('', ''),
    @('H', 'Formatos'),
    @('P', 'Fechas: año-mes-día (2026-11-30). Las celdas ya vienen con ese formato.'),
    @('P', 'Avance: número entero de 0 a 100, sin el signo de porcentaje. Solo en la hoja Actividades.'),
    @('P', 'Responsable e interesados: elíjalos de la lista desplegable, que trae los correos de los usuarios activos del portal. Si la persona no aparece, hay que crearle el usuario antes de importar.'),
    @('', ''),
    @('H', 'Los interesados dan acceso'),
    @('P', 'Quien figure como interesado PASA A VER ese proyecto completo —ficha, entregables, actividades, bitácora, riesgos y evidencia— aunque sea de otra institución, área o unidad. No es una lista de contactos: es a quién le está abriendo el proyecto.'),
    @('P', 'Por eso solo se admiten usuarios del portal. Un organismo sin cuenta (BID, PNUD, una cámara) no se puede registrar como interesado; si hace falta, primero se le crea el usuario.'),
    @('', ''),
    @('H', 'Listas desplegables'),
    @('P', 'Las columnas con lista solo aceptan los valores del catálogo. No son una sugerencia: el importador no reconoce nada fuera de esa lista y rechaza la fila.'),
    @('P', 'La hoja Catalogos es de consulta y está protegida, igual que esta. Las listas desplegables salen de ahí: si se borra, la plantilla deja de funcionar.'),
    @('', ''),
    @('H', 'Qué se puede y qué no'),
    @('P', 'Se puede: escribir en las filas de captura, agregar y borrar filas, ordenar, filtrar y ajustar el ancho de las columnas.'),
    @('P', 'No se puede: cambiar los encabezados, mover o borrar columnas, ni borrar hojas. El importador busca las columnas por nombre y por posición, así que un cambio ahí inutiliza el archivo.'),
    @('P', 'La protección no tiene contraseña: está para evitar el accidente, no para trabarlo. Si de verdad necesita reacomodar algo, use Revisar > Desproteger hoja — y avísele a quien va a importar el archivo.'),
    @('', ''),
    @('H', 'Al terminar'),
    @('P', 'Devuelva el archivo sin renombrar las hojas ni mover las columnas. El importador las busca por nombre y por posición.')
)

$fila = 1
foreach ($l in $lineas) {
    $tipo = $l[0]; $texto = $l[1]
    if (-not $texto) { $fila++; continue }

    $celda = $ins.Cells.Item($fila, 2)
    $celda.Value2 = $texto
    switch ($tipo) {
        'T' { $celda.Font.Size = 16; $celda.Font.Bold = $true; $celda.Font.Color = $AZUL }
        'S' { $celda.Font.Italic = $true; $celda.Font.Color = $TEXTOGRIS }
        'H' { $celda.Font.Size = 11; $celda.Font.Bold = $true; $celda.Font.Color = $AZUL }
        'P' { $celda.WrapText = $true; $ins.Rows.Item($fila).RowHeight = 26 }
    }
    $fila++
}

# Leyenda de colores, al pie: es la clave para leer las hojas de captura.
$fila += 1
$ins.Cells.Item($fila, 2).Value2 = 'Leyenda'
$ins.Cells.Item($fila, 2).Font.Bold = $true
$ins.Cells.Item($fila, 2).Font.Color = $AZUL
$fila++
$ins.Cells.Item($fila, 1).Interior.Color = $AMARILLO
$ins.Cells.Item($fila, 2).Value2 = 'Encabezado amarillo con asterisco: columna obligatoria.'
$fila++
$ins.Cells.Item($fila, 1).Interior.Color = $GRIS
$ins.Cells.Item($fila, 2).Value2 = 'Fila gris en cursiva con Ref «EJEMPLO»: muestra de formato. El importador la ignora.'

# ── Cierre ───────────────────────────────────────────────────────────────────
# Instrucciones de primera, Catalogos al final; se elimina la hoja vacía que trae el libro nuevo.
$hojaInicial.Delete()
$ins.Move($libro.Worksheets.Item(1))
$cat.Move([System.Reflection.Missing]::Value, $libro.Worksheets.Item($libro.Worksheets.Count))

# Protección, y en este orden: mover hojas es un cambio de estructura, así que el libro tiene que
# seguir desprotegido hasta acá.
Protect-Hoja $hp
Protect-Hoja $hh
Protect-Hoja $hi
Protect-Hoja $hr
Protect-Hoja $ins -TodoBloqueado
Protect-Hoja $cat -TodoBloqueado

# Lo que de verdad cuida los catálogos: sin esto se puede borrar la hoja Catalogos entera —y con
# ella todas las listas desplegables— aunque sus celdas estén bloqueadas.
if ($ClaveProteccion) { $libro.Protect($ClaveProteccion, $true, $false) }
else                  { $libro.Protect([System.Type]::Missing, $true, $false) }

$ins.Activate()
$ins.Range('A1').Select() | Out-Null

$dir = Split-Path -Parent $Salida
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
if (Test-Path $Salida) { Remove-Item $Salida -Force }

$libro.SaveAs($Salida, 51)   # 51 = xlOpenXMLWorkbook
$libro.Close($false)

}
finally {
    # Corre haya salido bien o mal. Sin esto, cualquier error entre medio deja EXCEL.EXE corriendo
    # sin ventana, y el usuario no tiene cómo darse cuenta salvo por el administrador de tareas.
    try { $excel.Quit() } catch { }
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null
    [GC]::Collect(); [GC]::WaitForPendingFinalizers()
}

Write-Host "Plantilla generada: $Salida" -ForegroundColor Green
Write-Host ("Catálogos: {0} instituciones, {1} áreas, {2} unidades, {3} usuarios activos." -f `
    $instituciones.Count, $areas.Count, $unidades.Count, $usuarios.Count)

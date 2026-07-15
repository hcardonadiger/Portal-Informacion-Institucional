$baseUrl = "https://gnpvqiyantdtksfvqtqk.supabase.co"
$apiKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImducHZxaXlhbnRkdGtzZnZxdHFrIiwicm9sZSI6ImFub24iLCJpYXQiOjE3ODA1ODE5NDIsImV4cCI6MjA5NjE1Nzk0Mn0.u7GxYKoQ_G3gokOg2VarZ707GEGc0dnjjYJNl5n5lDw"

$headers = @{
    "apikey" = $apiKey
    "Authorization" = "Bearer $apiKey"
}

# List all tables via OpenAPI schema
Write-Host "=== Discovering Supabase tables ==="
try {
    $schema = Invoke-RestMethod -Uri "$baseUrl/rest/v1/" -Headers $headers -Method Get
    Write-Host "Tables found:" ($schema | ConvertTo-Json -Depth 1 -Compress)
} catch {
    Write-Host "Schema error: $($_.Exception.Message)"
}

# Try to get instituciones
Write-Host "`n=== Instituciones ==="
try {
    $inst = Invoke-RestMethod -Uri "$baseUrl/rest/v1/instituciones?select=*&limit=5" -Headers $headers -Method Get
    Write-Host ($inst | ConvertTo-Json -Depth 3)
} catch {
    Write-Host "Instituciones error: $($_.Exception.Message)"
}

# Try tickets table
Write-Host "`n=== Tickets ==="
try {
    $tickets = Invoke-RestMethod -Uri "$baseUrl/rest/v1/tickets?select=*&limit=2" -Headers $headers -Method Get
    Write-Host ($tickets | ConvertTo-Json -Depth 3)
} catch {
    Write-Host "Tickets error: $($_.Exception.Message)"
}

# Try contactos table
Write-Host "`n=== Contactos ==="
try {
    $contactos = Invoke-RestMethod -Uri "$baseUrl/rest/v1/contactos?select=*&limit=2" -Headers $headers -Method Get
    Write-Host ($contactos | ConvertTo-Json -Depth 3)
} catch {
    Write-Host "Contactos error: $($_.Exception.Message)"
}

# Try diger_tram (expedientes)
Write-Host "`n=== diger_tram (expedientes) ==="
try {
    $exp = Invoke-RestMethod -Uri "$baseUrl/rest/v1/diger_tram?key=eq.expedientes&select=key" -Headers $headers -Method Get
    Write-Host "diger_tram rows: $($exp.Count)"
} catch {
    Write-Host "diger_tram error: $($_.Exception.Message)"
}

# Try reuniones
Write-Host "`n=== Reuniones ==="
try {
    $reuniones = Invoke-RestMethod -Uri "$baseUrl/rest/v1/reuniones?select=id,titulo,fecha&limit=3&order=fecha" -Headers $headers -Method Get
    Write-Host ($reuniones | ConvertTo-Json -Depth 3)
} catch {
    Write-Host "Reuniones error: $($_.Exception.Message)"
}

# Try categorias_ticket / temas_ticket
Write-Host "`n=== categorias_ticket ==="
try {
    $cats = Invoke-RestMethod -Uri "$baseUrl/rest/v1/categorias_ticket?select=*&limit=5" -Headers $headers -Method Get
    Write-Host ($cats | ConvertTo-Json -Depth 3)
} catch {
    Write-Host "categorias_ticket error: $($_.Exception.Message)"
}

Write-Host "`n=== temas_ticket ==="
try {
    $temas = Invoke-RestMethod -Uri "$baseUrl/rest/v1/temas_ticket?select=*&limit=5" -Headers $headers -Method Get
    Write-Host ($temas | ConvertTo-Json -Depth 3)
} catch {
    Write-Host "temas_ticket error: $($_.Exception.Message)"
}

# Try tramites_definicion
Write-Host "`n=== tramites_definicion ==="
try {
    $td = Invoke-RestMethod -Uri "$baseUrl/rest/v1/tramites_definicion?select=*&limit=5" -Headers $headers -Method Get
    Write-Host ($td | ConvertTo-Json -Depth 3)
} catch {
    Write-Host "tramites_definicion error: $($_.Exception.Message)"
}

$BASE_URL = "http://localhost"
$ErrorActionPreference = "Continue"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "TESTS DE AUTENTICACION JWT" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Test 1: Producto SIN token -> 401
Write-Host "Test 1: Crear producto SIN token" -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "${BASE_URL}:5002/products" -Method POST -ContentType "application/json" -Body '{"name":"Test","price":100,"stock":10}' -ErrorAction Continue
    $code = $response.StatusCode
    Write-Host "   Codigo: $code" -ForegroundColor Gray
    if ($code -eq 401) { Write-Host "   [PASS]" -ForegroundColor Green } 
    else { Write-Host "   [FAIL] - Esperado 401, obtuvo $code" -ForegroundColor Red }
} catch {
    $code = $_.Exception.Response.StatusCode.Value__
    Write-Host "   Codigo: $code" -ForegroundColor Gray
    if ($code -eq 401) { Write-Host "   [PASS]" -ForegroundColor Green } 
    else { Write-Host "   [FAIL] - Esperado 401, obtuvo $code" -ForegroundColor Red }
}
Write-Host ""

# Test 2: GET productos SIN token -> 200 (publico)
Write-Host "Test 2: GET productos SIN token (catalogo publico)" -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "${BASE_URL}:5002/products" -Method GET -ErrorAction Continue
    $code = $response.StatusCode
    Write-Host "   Codigo: $code" -ForegroundColor Gray
    if ($code -eq 200) { Write-Host "   [PASS]" -ForegroundColor Green } 
    else { Write-Host "   [FAIL] - Esperado 200, obtuvo $code" -ForegroundColor Red }
} catch {
    $code = $_.Exception.Response.StatusCode.Value__
    Write-Host "   Codigo: $code" -ForegroundColor Gray
    if ($code -eq 200) { Write-Host "   [PASS]" -ForegroundColor Green } 
    else { Write-Host "   [FAIL] - Esperado 200, obtuvo $code" -ForegroundColor Red }
}
Write-Host ""

# Registro de usuario (si no existe)
Write-Host "Registrando usuario test..." -ForegroundColor Cyan
try {
    $registerResponse = Invoke-WebRequest -Uri "${BASE_URL}:5001/auth/register" -Method POST -ContentType "application/json" -Body '{"email":"test@test.com","passwordHash":"test123"}' -ErrorAction Continue
    Write-Host "   Usuario registrado" -ForegroundColor Green
} catch {
    # Si ya existe, no importa
    Write-Host "   Usuario ya existe o error: $_" -ForegroundColor Gray
}
Write-Host ""

# Test 3: Obtener token
Write-Host "Test 3: Obtener token de auth-service" -ForegroundColor Yellow
$token = $null
try {
    $loginResponse = Invoke-WebRequest -Uri "${BASE_URL}:5001/auth/login" -Method POST -ContentType "application/json" -Body '{"email":"test@test.com","passwordHash":"test123"}' -ErrorAction Continue
    $loginData = $loginResponse.Content | ConvertFrom-Json
    $token = $loginData.token
    
    if ($token) {
        Write-Host "   Token obtenido: $($token.Substring(0, [Math]::Min(30, $token.Length)))..." -ForegroundColor Green
    } else {
        Write-Host "   No se encontro token en la respuesta" -ForegroundColor Red
    }
} catch {
    Write-Host "   ERROR al obtener token: $_" -ForegroundColor Red
}
Write-Host ""

if ($token) {
    $headers = @{
        "Content-Type" = "application/json"
        "Authorization" = "Bearer $token"
    }

    # Test 4: Producto CON token -> 201
    Write-Host "Test 4: Crear producto CON token" -ForegroundColor Yellow
    try {
        $response = Invoke-WebRequest -Uri "${BASE_URL}:5002/products" -Method POST -ContentType "application/json" -Headers $headers -Body '{"name":"TestProduct","price":100,"stock":10}' -ErrorAction Continue
        $code = $response.StatusCode
        Write-Host "   Codigo: $code" -ForegroundColor Gray
        if ($code -eq 201) { Write-Host "   [PASS]" -ForegroundColor Green } 
        else { Write-Host "   [FAIL] - Esperado 201, obtuvo $code" -ForegroundColor Red }
    } catch {
        $code = $_.Exception.Response.StatusCode.Value__
        Write-Host "   Codigo: $code" -ForegroundColor Gray
        if ($code -eq 201) { Write-Host "   [PASS]" -ForegroundColor Green } 
        else { Write-Host "   [FAIL] - Esperado 201, obtuvo $code" -ForegroundColor Red }
    }
    Write-Host ""

    # Test 5: Orden SIN token -> 401
    Write-Host "Test 5: Crear orden SIN token" -ForegroundColor Yellow
    try {
        $response = Invoke-WebRequest -Uri "${BASE_URL}:5003/orders" -Method POST -ContentType "application/json" -Body '{"userId":1,"items":[{"productId":1,"quantity":2,"price":100}]}' -ErrorAction Continue
        $code = $response.StatusCode
        Write-Host "   Codigo: $code" -ForegroundColor Gray
        if ($code -eq 401) { Write-Host "   [PASS]" -ForegroundColor Green } 
        else { Write-Host "   [FAIL] - Esperado 401, obtuvo $code" -ForegroundColor Red }
    } catch {
        $code = $_.Exception.Response.StatusCode.Value__
        Write-Host "   Codigo: $code" -ForegroundColor Gray
        if ($code -eq 401) { Write-Host "   [PASS]" -ForegroundColor Green } 
        else { Write-Host "   [FAIL] - Esperado 401, obtuvo $code" -ForegroundColor Red }
    }
    Write-Host ""

    # Test 6: Orden CON token -> 201
    Write-Host "Test 6: Crear orden CON token" -ForegroundColor Yellow
    try {
        $response = Invoke-WebRequest -Uri "${BASE_URL}:5003/orders" -Method POST -ContentType "application/json" -Headers $headers -Body '{"userId":1,"items":[{"productId":1,"quantity":2,"price":100}]}' -ErrorAction Continue
        $code = $response.StatusCode
        Write-Host "   Codigo: $code" -ForegroundColor Gray
        if ($code -eq 201) { Write-Host "   [PASS]" -ForegroundColor Green } 
        else { Write-Host "   [FAIL] - Esperado 201, obtuvo $code" -ForegroundColor Red }
    } catch {
        $code = $_.Exception.Response.StatusCode.Value__
        Write-Host "   Codigo: $code" -ForegroundColor Gray
        if ($code -eq 201) { Write-Host "   [PASS]" -ForegroundColor Green } 
        else { Write-Host "   [FAIL] - Esperado 201, obtuvo $code" -ForegroundColor Red }
    }
    Write-Host ""
} else {
    Write-Host "No hay token, saltando tests autenticados" -ForegroundColor Yellow
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "TESTS COMPLETADOS" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
    
#!/bin/sh

echo "=========================================="
echo "E-Commerce Microservices - Starting..."
echo "=========================================="

# Puerto que Render asigna - USAR EXACTAMENTE LO QUE RENDER PASA
# Render pasa el puerto como variable PORT - NO hardcodear
if [ -z "$PORT" ]; then
    echo "WARNING: PORT not set, using default 10000"
    export PORT=10000
else
    echo "Render assigned PORT: $PORT"
fi

# Limpiar variables que interfieren
unset HTTP_PORTS
unset HTTPS_PORTS

# JWT Key - .NET espera Jwt__Key (doble underscore = colon en config)
export Jwt__Key="${JWT_KEY:-super_secret_key_that_is_long_enough_for_hmac_sha256_please_change_in_production}"
echo "JWT Key configured: ${Jwt__Key:0:10}..."

# Los servicios parsean DATABASE_URL directamente en sus Program.cs
# No necesitamos hacer nada aquí - solo loguear
if [ -n "$DATABASE_URL" ]; then
    echo "DATABASE_URL detected - each service will parse it"
else
    echo "WARNING: No DATABASE_URL found - services will use default config"
fi

# OCULTAR PUERTOS INTERNOS DE RENDER
# Render detecta puertos internos y reinicia. Vamos a hacer que solo el puerto 10000 sea visible
# Esto bloquea que Render escanee los puertos 8001-8007
echo "Configuring port visibility..."

# Memory optimization: Use Workstation GC (less memory per process)
# Server GC assumes it owns the full machine and allocates huge heaps — with 8 .NET
# processes in a 512MB container this causes OOM kills and container restarts.
export DOTNET_GCServer=0
export DOTNET_GCHeapHardLimit=0x4000000
echo "GC configured: Workstation mode, 64MB heap limit per process"

# Función para esperar a que un puerto esté libre
wait_for_port() {
    local port=$1
    local max_wait=30
    local waited=0
    
    echo "Waiting for port $port to be free..."
    while netstat -tuln 2>/dev/null | grep -q ":$port " || ss -tuln 2>/dev/null | grep -q ":$port "; do
        if [ $waited -ge $max_wait ]; then
            echo "Port $port still in use after ${max_wait}s, killing processes..."
            fuser -k $port/tcp 2>/dev/null || true
            sleep 2
            waited=0
        fi
        sleep 1
        waited=$((waited+1))
    done
    echo "Port $port is free"
}

# Iniciar servicios en background
# IMPORTANTE: Usar 127.0.0.1 en vez de 0.0.0.0 para que Render NO detecte estos puertos
# Render detecta el primer puerto que ve y reinicia el deploy pensando que es el principal
start_service() {
    local name=$1
    local path=$2
    local port=$3
    
    echo "Starting $name on port $port (localhost only)..."
    cd /app/services/$path
    
    # Esperar a que el puerto esté libre
    wait_for_port $port
    
    # Iniciar en background - solo en localhost para que Render no lo detecte
    # Usar solo IPv4 127.0.0.1 para evitar que Render detecte el puerto
    nohup dotnet ${name}Service.dll --urls "http://127.0.0.1:$port" --hosting-offline-timeout 120 > /tmp/$name.log 2>&1 &
    local pid=$!
    echo "$name started with PID: $pid"
    
    # Mostrar logs iniciales
    sleep 3
    echo "$name startup log:"
    tail -10 /tmp/$name.log 2>/dev/null || true
}

# Show environment variables for debugging
echo "=========================================="
echo "ENVIRONMENT VARIABLES:"
if [ -n "$DATABASE_URL" ]; then
    echo "DATABASE_URL: ${DATABASE_URL:0:40}..."
else
    echo "DATABASE_URL: (not set)"
fi
echo "JWT_KEY: ${JWT_KEY:0:20}..."
echo "Jwt__Key: ${Jwt__Key:0:20}..."
echo "PORT: $PORT"
echo "=========================================="

# Iniciar servicios en background
echo "Starting microservices..."
start_service "Auth" "auth" "8001"
start_service "Product" "product" "8002"
start_service "Order" "order" "8003"
start_service "Cart" "cart" "8004"
start_service "Payment" "payment" "8005"
start_service "Notification" "notification" "8006"
start_service "Inventory" "inventory" "8007"

echo "=========================================="
echo "All microservices started!"
echo "=========================================="

# Esperar a que todos los servicios estén completamente inicializados
# Esto es crítico para evitar que el Gateway intente conectar a servicios que aún no están listos
echo "Waiting for services to initialize..."
sleep 15

# Iniciar API Gateway en FOREGROUND (proceso principal en el puerto de Render)
echo "Starting API Gateway on port $PORT..."
cd /app/services/gateway
exec dotnet ApiGateway.dll --urls "http://0.0.0.0:$PORT" 2>&1

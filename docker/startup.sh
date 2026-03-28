#!/bin/sh

echo "=========================================="
echo "E-Commerce Microservices - Starting..."
echo "=========================================="

# Puerto que Render asigna (externo)
export PORT=${PORT:-10000}
echo "Render assigned PORT: $PORT"

# Limpiar variables que interfieren
unset HTTP_PORTS
unset HTTPS_PORTS

# JWT Key
export JWT_KEY="${JWT_KEY:-super_secret_key_that_is_long_enough_for_hmac_sha256_please_change_in_production}"

# Si DATABASE_URL está presente, los servicios ya lo manejan
if [ -n "$DATABASE_URL" ]; then
    echo "DATABASE_URL detected - services will parse it"
fi

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

# Iniciar servicios en background (excepto Gateway)
start_service() {
    local name=$1
    local path=$2
    local port=$3
    
    echo "Starting $name on port $port..."
    cd /app/services/$path
    
    # Esperar a que el puerto esté libre
    wait_for_port $port
    
    # Iniciar en background
    nohup dotnet ${name}Service.dll --urls "http://0.0.0.0:$port" > /tmp/$name.log 2>&1 &
    echo "$name started with PID: $!"
    
    # Mostrar logs iniciales
    sleep 3
    echo "$name startup log:"
    tail -10 /tmp/$name.log 2>/dev/null || true
}

# Iniciar servicios en background
echo "=========================================="
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

# Iniciar API Gateway en FOREGROUND (proceso principal en el puerto de Render)
echo "Starting API Gateway on port $PORT..."
cd /app/services/gateway
exec dotnet ApiGateway.dll --urls "http://0.0.0.0:$PORT"

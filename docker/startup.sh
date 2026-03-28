#!/bin/bash

echo "Starting E-Commerce Microservices..."

# Puerto que Render asigna
export PORT=${PORT:-10000}

# Limpiar variables que interfieren
unset HTTP_PORTS
unset HTTPS_PORTS

# Connection string
export ConnectionStrings__DefaultConnection="Host=ep-falling-art-a8l470fw-pooler.eastus2.azure.neon.tech;Port=5432;Database=neondb;Username=neondb_owner;Password=npg_U3ToW0RsVOSc;sslmode=require;Timeout=120"

export JWT_KEY="${JWT_KEY:-super_secret_key_that_is_long_enough_for_hmac_sha256_please_change_in_production}"

echo "Using PORT: $PORT"

# Función para esperar a que un puerto esté libre
wait_for_port() {
    local port=$1
    local max_wait=30
    local waited=0
    
    while netstat -tuln 2>/dev/null | grep -q ":$port " || ss -tuln 2>/dev/null | grep -q ":$port "; do
        if [ $waited -ge $max_wait ]; then
            echo "Port $port still in use after ${max_wait}s, killing processes..."
            fuser -k $port/tcp 2>/dev/null || true
            sleep 2
        fi
        sleep 1
        waited=$((waited+1))
    done
    echo "Port $port is free"
}

# Iniciar API Gateway en puerto 8080
echo "Starting API Gateway on port 8080..."
cd /app/services/gateway
nohup dotnet ApiGateway.dll --urls "http://0.0.0.0:8080" > /tmp/gateway.log 2>&1 &
GATEWAY_PID=$!
echo "API Gateway PID: $GATEWAY_PID"
sleep 10

# Iniciar servicios en secuencia
start_service() {
    local name=$1
    local path=$2
    local port=$3
    
    echo "Starting $name on port $port..."
    cd /app/services/$path
    
    # Esperar a que el puerto esté libre
    wait_for_port $port
    
    nohup dotnet *.dll --urls "http://0.0.0.0:$port" > /tmp/$name.log 2>&1 &
    echo "$name started with PID $!"
    sleep 15
}

# Iniciar servicios uno por uno
start_service "Auth" "auth" "8001"
start_service "Product" "product" "8002"
start_service "Order" "order" "8003"
start_service "Cart" "cart" "8004"
start_service "Payment" "payment" "8005"
start_service "Notification" "notification" "8006"
start_service "Inventory" "inventory" "8007"

echo "All services started. Starting nginx..."
nginx -g 'daemon off;'

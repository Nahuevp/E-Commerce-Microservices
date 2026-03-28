#!/bin/bash
set -e

echo "Starting E-Commerce Microservices..."

# Puerto que Render asigna
export PORT=${PORT:-10000}

# Connection string
export ConnectionStrings__DefaultConnection="Host=ep-falling-art-a8l470fw-pooler.eastus2.azure.neon.tech;Port=5432;Database=neondb;Username=neondb_owner;Password=npg_U3ToW0RsVOSc;sslmode=require;Timeout=60"

export JWT_KEY="${JWT_KEY:-super_secret_key_that_is_long_enough_for_hmac_sha256_please_change_in_production}"

echo "Using PORT: $PORT"

# Función para iniciar un servicio con retry infinito
start_service() {
    local name=$1
    local path=$2
    local port=$3
    
    echo "Starting $name on port $port..."
    cd /app/services/$path
    
    while true; do
        dotnet *.dll --urls "http://0.0.0.0:$port"
        echo "$name crashed, restarting in 5s..."
        sleep 5
    done &
}

# Iniciar servicios en puertos únicos
start_service "Auth Service" "auth" "8001" &
start_service "Product Service" "product" "8002" &
start_service "Order Service" "order" "8003" &
start_service "Cart Service" "cart" "8004" &
start_service "Payment Service" "payment" "8005" &
start_service "Notification Service" "notification" "8006" &
start_service "Inventory Service" "inventory" "8007" &

echo "Waiting for services to initialize..."
sleep 60

echo "Starting API Gateway on port 8080..."
cd /app/services/gateway
while true; do
    dotnet ApiGateway.dll --urls "http://0.0.0.0:8080"
    echo "API Gateway crashed, restarting in 5s..."
    sleep 5
done &

echo "Starting nginx..."
nginx -g 'daemon off;'

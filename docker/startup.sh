#!/bin/bash
set -e

echo "Starting E-Commerce Microservices..."

# Puerto que Render asigna (para el API Gateway)
export PORT=${PORT:-10000}

# Use direct connection string (sslmode only, no channel_binding)
export ConnectionStrings__DefaultConnection="Host=ep-falling-art-a8l470fw-pooler.eastus2.azure.neon.tech;Port=5432;Database=neondb;Username=neondb_owner;Password=npg_U3ToW0RsVOSc;sslmode=require"

export JWT_KEY="${JWT_KEY:-super_secret_key_that_is_long_enough_for_hmac_sha256_please_change_in_production}"

echo "Using PORT: $PORT"

# Start all services (usamos puertos internos 8001-8007)
echo "Starting Auth Service on port 8001..."
cd /app/services/auth
dotnet AuthService.dll --urls "http://0.0.0.0:8001" &

echo "Starting Product Service on port 8002..."
cd /app/services/product
dotnet ProductService.dll --urls "http://0.0.0.0:8002" &

echo "Starting Order Service on port 8003..."
cd /app/services/order
dotnet OrderService.dll --urls "http://0.0.0.0:8003" &

echo "Starting Cart Service on port 8004..."
cd /app/services/cart
dotnet CartService.dll --urls "http://0.0.0.0:8004" &

echo "Starting Payment Service on port 8005..."
cd /app/services/payment
dotnet PaymentService.dll --urls "http://0.0.0.0:8005" &

echo "Starting Notification Service on port 8006..."
cd /app/services/notification
dotnet NotificationService.dll --urls "http://0.0.0.0:8006" &

echo "Starting Inventory Service on port 8007..."
cd /app/services/inventory
dotnet InventoryService.dll --urls "http://0.0.0.0:8007" &

# API Gateway escucha en el puerto de Render (10000)
echo "Starting API Gateway on port $PORT..."
cd /app/services/gateway
dotnet ApiGateway.dll --urls "http://0.0.0.0:$PORT" &

echo "Waiting for services to start..."
sleep 25

echo "All services started. Starting nginx..."
nginx -g 'daemon off;'

#!/bin/bash
set -e

echo "Starting E-Commerce Microservices..."

# Puerto que Render asigna
export PORT=${PORT:-10000}

# Connection string
export ConnectionStrings__DefaultConnection="Host=ep-falling-art-a8l470fw-pooler.eastus2.azure.neon.tech;Port=5432;Database=neondb;Username=neondb_owner;Password=npg_U3ToW0RsVOSc;sslmode=require;Timeout=120"

export JWT_KEY="${JWT_KEY:-super_secret_key_that_is_long_enough_for_hmac_sha256_please_change_in_production}"

echo "Using PORT: $PORT"

# Iniciar API Gateway primero en puerto 8080
echo "Starting API Gateway on port 8080..."
cd /app/services/gateway
dotnet ApiGateway.dll --urls "http://0.0.0.0:8080" &
sleep 10

# Iniciar servicios uno por uno con espera
echo "Starting Auth Service on port 8001..."
cd /app/services/auth
timeout 180 dotnet AuthService.dll --urls "http://0.0.0.0:8001" || echo "Auth service timed out, continuing..."

echo "Starting Product Service on port 8002..."
cd /app/services/product
timeout 180 dotnet ProductService.dll --urls "http://0.0.0.0:8002" || echo "Product service timed out, continuing..."

echo "Starting Order Service on port 8003..."
cd /app/services/order
timeout 180 dotnet OrderService.dll --urls "http://0.0.0.0:8003" || echo "Order service timed out, continuing..."

echo "Starting Cart Service on port 8004..."
cd /app/services/cart
timeout 180 dotnet CartService.dll --urls "http://0.0.0.0:8004" || echo "Cart service timed out, continuing..."

echo "Starting Payment Service on port 8005..."
cd /app/services/payment
timeout 180 dotnet PaymentService.dll --urls "http://0.0.0.0:8005" || echo "Payment service timed out, continuing..."

echo "Starting Notification Service on port 8006..."
cd /app/services/notification
timeout 180 dotnet NotificationService.dll --urls "http://0.0.0.0:8006" || echo "Notification service timed out, continuing..."

echo "Starting Inventory Service on port 8007..."
cd /app/services/inventory
timeout 180 dotnet InventoryService.dll --urls "http://0.0.0.0:8007" || echo "Inventory service timed out, continuing..."

echo "All services started. Starting nginx..."
nginx -g 'daemon off;'

#!/bin/bash
set -e

echo "Starting E-Commerce Microservices..."

# Use direct connection string (sslmode only, no channel_binding)
export ConnectionStrings__DefaultConnection="Host=ep-falling-art-a8l470fw-pooler.eastus2.azure.neon.tech;Port=5432;Database=neondb;Username=neondb_owner;Password=npg_U3ToW0RsVOSc;sslmode=require"

export JWT_KEY="${JWT_KEY:-super_secret_key_that_is_long_enough_for_hmac_sha256_please_change_in_production}"

echo "Using direct connection string"

# Start all services
echo "Starting Auth Service on port 5001..."
cd /app/services/auth
dotnet AuthService.dll --urls "http://0.0.0.0:5001" &

echo "Starting Product Service on port 5002..."
cd /app/services/product
dotnet ProductService.dll --urls "http://0.0.0.0:5002" &

echo "Starting Order Service on port 5003..."
cd /app/services/order
dotnet OrderService.dll --urls "http://0.0.0.0:5003" &

echo "Starting Cart Service on port 5004..."
cd /app/services/cart
dotnet CartService.dll --urls "http://0.0.0.0:5004" &

echo "Starting Payment Service on port 5005..."
cd /app/services/payment
dotnet PaymentService.dll --urls "http://0.0.0.0:5005" &

echo "Starting Notification Service on port 5006..."
cd /app/services/notification
dotnet NotificationService.dll --urls "http://0.0.0.0:5006" &

echo "Starting Inventory Service on port 5007..."
cd /app/services/inventory
dotnet InventoryService.dll --urls "http://0.0.0.0:5007" &

echo "Starting API Gateway on port 5000..."
cd /app/services/gateway
dotnet ApiGateway.dll --urls "http://0.0.0.0:5000" &

echo "Waiting for services to start..."
sleep 15

echo "All services started. Starting nginx..."
nginx -g 'daemon off;'

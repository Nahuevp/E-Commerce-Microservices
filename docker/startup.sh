#!/bin/bash
set -e

echo "Starting E-Commerce Microservices..."

# Configuration
export JWT_KEY="${JWT_KEY:-super_secret_key_that_is_long_enough_for_hmac_sha256_please_change_in_production}"
export ConnectionStrings__DefaultConnection="${DATABASE_URL:-Host=localhost;Port=5432;Database=ecommerce;Username=postgres;Password=password123}"

# Function to run a service in background
run_service() {
    local name=$1
    local path=$2
    local port=$3
    
    echo "Starting $name on port $port..."
    cd /app/services/$path
    dotnet .dll --urls "http://0.0.0.0:$port" &
}

# Wait a bit for postgres if using internal DB
sleep 2

# Start all services in background
run_service "Auth Service" "auth" "5001" &
run_service "Product Service" "product" "5002" &
run_service "Order Service" "order" "5003" &
run_service "Cart Service" "cart" "5004" &
run_service "Payment Service" "payment" "5005" &
run_service "Notification Service" "notification" "5006" &
run_service "Inventory Service" "inventory" "5007" &
run_service "API Gateway" "gateway" "5000" &

# Wait for services to start
sleep 5

echo "All services started. Starting nginx..."

# Start nginx in foreground
nginx -g 'daemon off;'

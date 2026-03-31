#!/bin/sh
# E-Commerce Microservices Startup Script

echo "Starting E-Commerce Microservices..."

# Puerto para API Gateway
PORT=${PORT:-10000}

# Iniciar servicios en background
echo "Starting Auth Service..."
cd /app/services/auth && nohup dotnet AuthService.dll --urls "http://0.0.0.0:8001" > /dev/null 2>&1 &

echo "Starting Product Service..."
cd /app/services/product && nohup dotnet ProductService.dll --urls "http://0.0.0.0:8002" > /dev/null 2>&1 &

echo "Starting Order Service..."
cd /app/services/order && nohup dotnet OrderService.dll --urls "http://0.0.0.0:8003" > /dev/null 2>&1 &

echo "Starting Cart Service..."
cd /app/services/cart && nohup dotnet CartService.dll --urls "http://0.0.0.0:8004" > /dev/null 2>&1 &

echo "Starting Payment Service..."
cd /app/services/payment && nohup dotnet PaymentService.dll --urls "http://0.0.0.0:8005" > /dev/null 2>&1 &

echo "Starting Notification Service..."
cd /app/services/notification && nohup dotnet NotificationService.dll --urls "http://0.0.0.0:8006" > /dev/null 2>&1 &

echo "Starting Inventory Service..."
cd /app/services/inventory && nohup dotnet InventoryService.dll --urls "http://0.0.0.0:8007" > /dev/null 2>&1 &

# Esperar a que arranquen los servicios
echo "Waiting for services to start..."
sleep 5

# Iniciar API Gateway
echo "Starting API Gateway on port $PORT..."
cd /app/services/gateway && dotnet ApiGateway.dll --urls "http://0.0.0.0:$PORT"

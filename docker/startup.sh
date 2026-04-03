#!/bin/sh
# E-Commerce Microservices Startup Script
# Optimized: parallel startup with health checks

echo "========================================"
echo "E-Commerce Microservices - Starting"
echo "========================================"

# Puerto para API Gateway
PORT=${PORT:-10000}

# Función para esperar a que un servicio responda
wait_for_service() {
    local name=$1
    local port=$2
    local max_attempts=30
    local attempt=1

    echo "  Waiting for $name (port $port)..."
    while [ $attempt -le $max_attempts ]; do
        if curl -s -o /dev/null -w "%{http_code}" "http://localhost:$port/health" 2>/dev/null | grep -q "200"; then
            echo "  ✓ $name is ready ($attempt attempts)"
            return 0
        fi
        # Fallback: check if port is open (some services may not have /health)
        if curl -s -o /dev/null "http://localhost:$port/" 2>/dev/null; then
            echo "  ✓ $name is ready ($attempt attempts)"
            return 0
        fi
        attempt=$((attempt + 1))
        sleep 1
    done
    echo "  ✗ $name failed to start after $max_attempts seconds"
    return 1
}

# Iniciar TODOS los servicios en background simultáneamente
echo ""
echo "Starting all services in parallel..."
echo "----------------------------------------"

cd /app/services/auth
nohup dotnet AuthService.dll --urls "http://0.0.0.0:8001" > /tmp/auth.log 2>&1 &
AUTH_PID=$!
echo "  Auth Service started (PID: $AUTH_PID)"

cd /app/services/product
nohup dotnet ProductService.dll --urls "http://0.0.0.0:8002" > /tmp/product.log 2>&1 &
PRODUCT_PID=$!
echo "  Product Service started (PID: $PRODUCT_PID)"

cd /app/services/order
nohup dotnet OrderService.dll --urls "http://0.0.0.0:8003" > /tmp/order.log 2>&1 &
ORDER_PID=$!
echo "  Order Service started (PID: $ORDER_PID)"

cd /app/services/cart
nohup dotnet CartService.dll --urls "http://0.0.0.0:8004" > /tmp/cart.log 2>&1 &
CART_PID=$!
echo "  Cart Service started (PID: $CART_PID)"

cd /app/services/payment
nohup dotnet PaymentService.dll --urls "http://0.0.0.0:8005" > /tmp/payment.log 2>&1 &
PAYMENT_PID=$!
echo "  Payment Service started (PID: $PAYMENT_PID)"

cd /app/services/notification
nohup dotnet NotificationService.dll --urls "http://0.0.0.0:8006" > /tmp/notification.log 2>&1 &
NOTIFICATION_PID=$!
echo "  Notification Service started (PID: $NOTIFICATION_PID)"

cd /app/services/inventory
nohup dotnet InventoryService.dll --urls "http://0.0.0.0:8007" > /tmp/inventory.log 2>&1 &
INVENTORY_PID=$!
echo "  Inventory Service started (PID: $INVENTORY_PID)"

echo "----------------------------------------"
echo ""
echo "Waiting for all services to be ready..."
echo "----------------------------------------"

# Esperar a todos los servicios en paralelo (máximo 45 segundos)
SERVICES_READY=0
TOTAL_SERVICES=7
MAX_WAIT=45
WAITED=0

while [ $SERVICES_READY -lt $TOTAL_SERVICES ] && [ $WAITED -lt $MAX_WAIT ]; do
    CURRENT_READY=0
    
    # Verificar cada servicio
    for svc_info in "Auth:8001" "Product:8002" "Order:8003" "Cart:8004" "Payment:8005" "Notification:8006" "Inventory:8007"; do
        svc_name=$(echo $svc_info | cut -d: -f1)
        svc_port=$(echo $svc_info | cut -d: -f2)
        
        if curl -s -o /dev/null "http://localhost:$svc_port/health" 2>/dev/null || curl -s -o /dev/null "http://localhost:$svc_port/" 2>/dev/null; then
            CURRENT_READY=$((CURRENT_READY + 1))
        fi
    done
    
    if [ $CURRENT_READY -gt $SERVICES_READY ]; then
        echo "  Services ready: $CURRENT_READY/$TOTAL_SERVICES"
        SERVICES_READY=$CURRENT_READY
    fi
    
    WAITED=$((WAITED + 1))
    sleep 1
done

echo "----------------------------------------"

if [ $SERVICES_READY -eq $TOTAL_SERVICES ]; then
    echo "✓ All $TOTAL_SERVICES services are ready (${WAITED}s)"
else
    echo "⚠ Only $SERVICES_READY/$TOTAL_SERVICES services ready after ${WAITED}s"
    echo "  Starting gateway anyway (some services may still be initializing)"
fi

# Iniciar API Gateway
echo ""
echo "Starting API Gateway on port $PORT..."
echo "========================================"
cd /app/services/gateway
exec dotnet ApiGateway.dll --urls "http://0.0.0.0:$PORT"

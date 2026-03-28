#!/bin/bash

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

# Si DATABASE_URL está presente, convertirla (los servicios ya lo hacen en código, pero por seguridad)
if [ -n "$DATABASE_URL" ]; then
    echo "DATABASE_URL detected - services will parse it"
fi

# Generar nginx.conf dinámico con el puerto correcto
# Usamos un placeholder para evitar expansión de variables prematura
GATEWAY_PORT_PLACEHOLDER="__GATEWAY_PORT__"

cat > /etc/nginx/nginx.conf << NGINXCONF
events {
    worker_connections 1024;
}

http {
    include /etc/nginx/mime.types;
    default_type application/octet-stream;
    
    error_log /dev/stderr;
    access_log /dev/stdout;
    
    proxy_buffering off;
    proxy_http_version 1.1;
    
    server {
        listen 80;
        server_name _;
        
        # Static files
        location / {
            root /usr/share/nginx/html;
            try_files \$uri \$uri/ /index.html;
        }
        
        # Health check
        location /health {
            return 200 'OK';
            add_header Content-Type text/plain;
        }
        
        # API - proxy al puerto del API Gateway
        location ^~ /api {
            proxy_pass http://127.0.0.1:${GATEWAY_PORT_PLACEHOLDER};
            proxy_set_header Host \$host;
            proxy_set_header X-Real-IP \$remote_addr;
            proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto \$scheme;
            proxy_connect_timeout 120s;
            proxy_send_timeout 120s;
            proxy_read_timeout 120s;
        }
    }
}
NGINXCONF

# Reemplazar el placeholder con el puerto real
sed -i "s/${GATEWAY_PORT_PLACEHOLDER}/${PORT}/g" /etc/nginx/nginx.conf
echo "nginx.conf generated with PORT: $PORT"

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

# Iniciar API Gateway en el puerto que Render asigna
echo "=========================================="
echo "Starting API Gateway on port $PORT..."
cd /app/services/gateway
nohup dotnet ApiGateway.dll --urls "http://0.0.0.0:$PORT" > /tmp/gateway.log 2>&1 &
echo "API Gateway PID: $!"
sleep 10

# Mostrar logs del gateway para debugging
echo "Gateway startup log:"
cat /tmp/gateway.log

# Iniciar servicios en secuencia
start_service() {
    local name=$1
    local path=$2
    local port=$3
    
    echo "=========================================="
    echo "Starting $name on port $port..."
    cd /app/services/$path
    
    # Esperar a que el puerto esté libre
    wait_for_port $port
    
    # Usar el DLL específico en vez de *.dll
    nohup dotnet ${name}Service.dll --urls "http://0.0.0.0:$port" > /tmp/$name.log 2>&1 &
    echo "$name started with PID $!"
    sleep 5
    
    # Mostrar logs para debugging
    echo "$name startup log:"
    tail -20 /tmp/$name.log 2>/dev/null || true
}

# Iniciar servicios uno por uno (rutas coinciden con Dockerfile)
start_service "Auth" "auth" "8001"
start_service "Product" "product" "8002"
start_service "Order" "order" "8003"
start_service "Cart" "cart" "8004"
start_service "Payment" "payment" "8005"
start_service "Notification" "notification" "8006"
start_service "Inventory" "inventory" "8007"

echo "=========================================="
echo "All services started!"
echo "=========================================="
echo "Starting nginx on port 80..."
echo ""

# Iniciar nginx en foreground
nginx -g 'daemon off;'

#!/bin/bash

BASE_URL="http://localhost"

echo "========================================"
echo "🧪 TESTS DE AUTENTICACIÓN JWT"
echo "========================================"
echo ""

# Test 1: Producto SIN token → 401
echo "📝 Test 1: Crear producto SIN token"
RESPONSE=$(curl -s -w "%{http_code}" -o /tmp/response1.txt -X POST "$BASE_URL:5002/api/product" \
  -H "Content-Type: application/json" \
  -d '{"name":"Test","price":100,"stock":10}')
echo "   Código: $RESPONSE"
if [ "$RESPONSE" = "401" ]; then echo "   ✅ PASS"; else echo "   ❌ FAIL - Esperado 401"; fi
echo ""

# Test 2: GET productos SIN token → 200 (público)
echo "📝 Test 2: GET productos SIN token (catálogo público)"
RESPONSE=$(curl -s -w "%{http_code}" -o /tmp/response2.txt "$BASE_URL:5002/api/product")
echo "   Código: $RESPONSE"
if [ "$RESPONSE" = "200" ]; then echo "   ✅ PASS"; else echo "   ❌ FAIL - Esperado 200"; fi
echo ""

# Test 3: Obtener token
echo "📝 Test 3: Obtener token de auth-service"
TOKEN_RESPONSE=$(curl -s -X POST "$BASE_URL:5001/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin123"}')
echo "   Respuesta: $TOKEN_RESPONSE"

# Extraer token (ajustar según formato de respuesta)
TOKEN=$(echo $TOKEN_RESPONSE | grep -o '"token":"[^"]*"' | cut -d'"' -f4)
if [ -z "$TOKEN" ]; then
  TOKEN=$(echo $TOKEN_RESPONSE | grep -o 'token[^,}]*' | head -1 | cut -d'"' -f3)
fi
echo "   Token: ${TOKEN:0:20}..."
echo ""

if [ -z "$TOKEN" ]; then
  echo "❌ No se pudo obtener token. Saltando tests con auth."
else
  # Test 4: Producto CON token → 201
  echo "📝 Test 4: Crear producto CON token"
  RESPONSE=$(curl -s -w "%{http_code}" -o /tmp/response4.txt -X POST "$BASE_URL:5002/api/product" \
    -H "Content-Type: application/json" \
    -H "Authorization: Bearer $TOKEN" \
    -d '{"name":"TestProduct","price":100,"stock":10}')
  echo "   Código: $RESPONSE"
  if [ "$RESPONSE" = "201" ]; then echo "   ✅ PASS"; else echo "   ❌ FAIL - Esperado 201"; fi
  echo ""

  # Test 5: Orden SIN token → 401
  echo "📝 Test 5: Crear orden SIN token"
  RESPONSE=$(curl -s -w "%{http_code}" -o /tmp/response5.txt -X POST "$BASE_URL:5003/api/order" \
    -H "Content-Type: application/json" \
    -d '{"userId":1,"items":[{"productId":1,"quantity":2,"price":100}]}')
  echo "   Código: $RESPONSE"
  if [ "$RESPONSE" = "401" ]; then echo "   ✅ PASS"; else echo "   ❌ FAIL - Esperado 401"; fi
  echo ""

  # Test 6: Orden CON token → 201
  echo "📝 Test 6: Crear orden CON token"
  RESPONSE=$(curl -s -w "%{http_code}" -o /tmp/response6.txt -X POST "$BASE_URL:5003/api/order" \
    -H "Content-Type: application/json" \
    -H "Authorization: Bearer $TOKEN" \
    -d '{"userId":1,"items":[{"productId":1,"quantity":2,"price":100}]}')
  echo "   Código: $RESPONSE"
  if [ "$RESPONSE" = "201" ]; then echo "   ✅ PASS"; else echo "   ❌ FAIL - Esperado 201"; fi
  echo ""
fi

echo "========================================"
echo "🏁 TESTS COMPLETADOS"
echo "========================================"

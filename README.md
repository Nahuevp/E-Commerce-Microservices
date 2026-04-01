# E-Commerce Microservices

Aplicación de e-commerce con arquitectura de microservicios en .NET 10, PostgreSQL, JWT Auth, API Gateway con YARP y frontend vanilla con Tailwind CSS.

**Live demo**: https://ecommerce-microservices-ow4d.onrender.com

> Render free tier entra en sleep tras 15 min de inactividad. El primer request puede tardar ~30s.

---

## Arquitectura

```
Browser → API Gateway (YARP) → Auth / Products / Orders / Cart / Payment / Inventory / Notification
                                    ↓
                              PostgreSQL (7 databases)
```

| Servicio | Puerto | Descripción |
|----------|--------|-------------|
| api-gateway | 5000 | YARP reverse proxy |
| auth-service | 5001 | Registro, login, JWT |
| product-service | 5002 | CRUD productos |
| order-service | 5003 | Gestión de órdenes |
| cart-service | 5004 | Carrito de compras |
| payment-service | 5005 | Procesamiento de pagos (simulado) |
| inventory-service | 5007 | Stock y reservas |
| notification-service | 5006 | Notificaciones |

---

## Stack

- **Backend**: .NET 10, ASP.NET Core, EF Core + Npgsql
- **Database**: PostgreSQL 15
- **Gateway**: YARP
- **Frontend**: HTML5, Tailwind CSS (CDN), JavaScript vanilla
- **Auth**: JWT Bearer Tokens, BCrypt
- **Testing**: xUnit, Moq, EF Core InMemory
- **Containers**: Docker, Docker Compose

---

## Quick Start

### Docker Compose (recomendado)

```bash
docker-compose up -d
# Esperar ~30s a que inicialicen las bases de datos
# Abrir http://localhost
```

### Local (sin Docker)

Requiere PostgreSQL corriendo en puerto 5432.

```bash
# 1. Crear las 7 bases de datos
CREATE DATABASE "AuthDb";
CREATE DATABASE "ProductDb";
CREATE DATABASE "OrderDb";
CREATE DATABASE "CartDb";
CREATE DATABASE "PaymentDb";
CREATE DATABASE "NotificationDb";
CREATE DATABASE "InventoryDb";

# 2. Configurar ConnectionStrings en appsettings.json de cada servicio
# "ConnectionStrings": { "DefaultConnection": "Host=localhost;Port=5432;Database=AuthDb;Username=postgres;Password=tu_password" }

# 3. Ejecutar cada servicio en terminal separada
dotnet run --project services/auth-service --urls http://localhost:5001
dotnet run --project services/product-service --urls http://localhost:5002
dotnet run --project services/order-service --urls http://localhost:5003
dotnet run --project services/cart-service --urls http://localhost:5004
dotnet run --project services/payment-service --urls http://localhost:5005
dotnet run --project services/notification-service --urls http://localhost:5006
dotnet run --project services/inventory-service --urls http://localhost:5007
dotnet run --project api-gateway --urls http://localhost:5000
```

### Tests

```bash
dotnet test
```

---

## Estructura

```
E-Commerce-Microservices/
├── api-gateway/           # YARP API Gateway
├── client/                # Frontend (HTML/CSS/JS)
├── docker/                # Scripts de deploy
├── services/              # 7 microservicios
├── tests/                 # Tests unitarios
├── docker-compose.yml
└── Dockerfile.all-in-one  # Build para Render
```

---

## Deploy

### Render (all-in-one container)

El `Dockerfile.all-in-one` compila y ejecuta todos los servicios en un solo contenedor Alpine. El gateway sirve los archivos estáticos del frontend directamente.

Variables de entorno necesarias:
- `DATABASE_URL` — URL de PostgreSQL (formato `postgres://user:pass@host:port/db`)
- `JWT_KEY` — Clave secreta para JWT (mínimo 32 bytes)

---

MIT License

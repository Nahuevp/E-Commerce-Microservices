# E-Commerce Microservices

Una aplicación de e-commerce full-stack construida con arquitectura de microservicios en .NET 10, usando PostgreSQL, JWT Authentication, API Gateway con YARP, y nginx como reverse proxy.

![License](https://img.shields.io/badge/License-MIT-blue.svg)
![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)
![Build](https://img.shields.io/badge/Build-Local-success.svg)

---

## Arquitectura

```
┌─────────────────────────────────────────────────────────────────┐
│                         USUARIO (Browser)                        │
└──────────────────────────────┬──────────────────────────────────┘
                               │ :80
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│                        nginx (Reverse Proxy)                     │
│  • Sirve archivos estáticos (HTML/CSS/JS)                        │
│  • Forward /api/* al API Gateway                                 │
└──────────────────────────────┬──────────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│                    API Gateway (YARP) :5000                       │
│                  Puerta de entrada unificada                     │
└─────┬───────┬───────┬───────┬───────┬───────┬───────┬─────────┘
      │       │       │       │       │       │       │
      ▼       ▼       ▼       ▼       ▼       ▼       ▼
┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐
│  Auth   │ │Product  │ │ Order  │ │  Cart   │ │Payment │ │Inventory│
│ :5001   │ │ :5002   │ │ :5003  │ │ :5004   │ │ :5005  │ │ :5007   │
└────┬────┘ └────┬────┘ └───┬────┘ └────┬────┘ └───┬────┘ └───┬────┘
     │           │          │           │          │          │
     └───────────┴──────────┴──────────┴──────────┴──────────┘
                               │
                               ▼
                    ┌───────────────────┐
                    │   PostgreSQL :5432 │
                    │  (7 bases de datos) │
                    └───────────────────┘
```

---

## Servicios

| Servicio | Puerto | Descripción | Base de datos |
|----------|--------|-------------|---------------|
| nginx | 80 | Reverse proxy + archivos estáticos | - |
| api-gateway | 5000 | Puerta de entrada, routing con YARP | - |
| auth-service | 5001 | Registro y autenticación JWT (BCrypt) | AuthDb |
| product-service | 5002 | CRUD de productos | ProductDb |
| order-service | 5003 | Gestión de órdenes | OrderDb |
| cart-service | 5004 | Carrito de compras | CartDb |
| payment-service | 5005 | Procesamiento de pagos | PaymentDb |
| notification-service | 5006 | Sistema de notificaciones | NotificationDb |
| inventory-service | 5007 | Control de stock y reservas | InventoryDb |

---

## Tecnologías

| Categoría | Tecnología |
|-----------|------------|
| **Backend** | .NET 10, ASP.NET Core Minimal APIs |
| **Base de datos** | PostgreSQL 15 (via Npgsql + EF Core) |
| **API Gateway** | YARP (Yet Another Reverse Proxy) |
| **Reverse Proxy** | nginx (Alpine) |
| **Frontend** | HTML5, Tailwind CSS (CDN), JavaScript vanilla |
| **Auth** | JWT Bearer Tokens, BCrypt hashing |
| **Testing** | xUnit, Moq, EntityFrameworkCore InMemory |
| **Contenedores** | Docker, Docker Compose |

---

## Características Principales

- ✅ Autenticación JWT con BCrypt
- ✅ CRUD completo de productos
- ✅ Sistema de carrito de compras
- ✅ Procesamiento de pagos (simulación)
- ✅ Control de inventario con reservas
- ✅ Notificaciones del sistema
- ✅ Health checks por servicio
- ✅ Tests unitarios
- ✅ Docker Compose para desarrollo
- ✅ API Gateway centralizado (YARP)
- ✅ nginx como reverse proxy

---

## Getting Started

### Requisitos Previos

- .NET 10 SDK
- Docker Desktop (para PostgreSQL)
- Git

### Instalación Local (sin Docker)

```bash
# 1. Clonar el repositorio
git clone <repo-url>
cd E-Commerce-Microservices

# 2. Crear bases de datos en PostgreSQL
# Ejecutar en psql o PgAdmin:
CREATE DATABASE "AuthDb";
CREATE DATABASE "ProductDb";
CREATE DATABASE "OrderDb";
CREATE DATABASE "CartDb";
CREATE DATABASE "PaymentDb";
CREATE DATABASE "NotificationDb";
CREATE DATABASE "InventoryDb";

# 3. Configurar connection strings en appsettings.json de cada servicio

# 4. Ejecutar servicios (en terminals separadas)
cd services/auth-service && dotnet run
cd services/product-service && dotnet run
cd services/order-service && dotnet run
cd services/cart-service && dotnet run
cd services/payment-service && dotnet run
cd services/notification-service && dotnet run
cd services/inventory-service && dotnet run
cd api-gateway && dotnet run

# 5. Abrir el cliente
# Ir a http://localhost/client/index.html (si nginx está corriendo)
# O http://localhost:5000 (si accedés directo al gateway)
```

### Con Docker Compose (Recomendado)

```bash
# Levantar todos los servicios
docker-compose up -d

# Ver logs
docker-compose logs -f

# Bajar todo
docker-compose down

# Rebuild si hay cambios
docker-compose up -d --build
```

---

## Tests

```bash
cd tests/EcommerceMicroservices.Tests
dotnet test

# Con coverage
dotnet test --collect:"XPlat Code Coverage"
```

**Cobertura actual**: Tests para PaymentController, InventoryController y AuthController.

---

## API Endpoints

### Auth Service (puerto 5001)

| Método | Endpoint | Descripción |
|--------|----------|-------------|
| `POST` | `/auth/register` | Registrar usuario nuevo |
| `POST` | `/auth/login` | Login (retorna JWT) |
| `GET` | `/auth/users` | Listar usuarios (admin) |

### Product Service (puerto 5002)

| Método | Endpoint | Descripción |
|--------|----------|-------------|
| `GET` | `/products` | Listar productos |
| `GET` | `/products/{id}` | Ver producto |
| `POST` | `/products` | Crear producto (auth) |
| `PUT` | `/products/{id}` | Editar producto (auth) |
| `DELETE` | `/products/{id}` | Eliminar producto (auth) |
| `GET` | `/products/health` | Health check |

### Order Service (puerto 5003)

| Método | Endpoint | Descripción |
|--------|----------|-------------|
| `GET` | `/orders` | Listar órdenes |
| `GET` | `/orders/{id}` | Ver orden |
| `POST` | `/orders` | Crear orden (auth) |
| `DELETE` | `/orders/{id}` | Cancelar orden (auth) |

### Cart Service (puerto 5004)

| Método | Endpoint | Descripción |
|--------|----------|-------------|
| `GET` | `/carts/{userId}` | Obtener carrito |
| `POST` | `/carts` | Agregar item al carrito (auth) |
| `POST` | `/carts/{id}/checkout` | Finalizar compra (auth) |
| `PUT` | `/carts/{cartId}/items/{itemId}` | Actualizar cantidad (auth) |
| `DELETE` | `/carts/{cartId}/items/{itemId}` | Remover item (auth) |

### Payment Service (puerto 5005)

| Método | Endpoint | Descripción |
|--------|----------|-------------|
| `POST` | `/api/payments` | Procesar pago (auth) |
| `GET` | `/api/payments/{id}` | Ver estado de pago (auth) |
| `GET` | `/api/payments` | Listar pagos (auth) |

> **Nota**: Tarjetas que empiezan con "4000" son declinadas (simulación).

### Inventory Service (puerto 5007)

| Método | Endpoint | Descripción |
|--------|----------|-------------|
| `GET` | `/api/inventory/{productId}/availability` | Ver stock disponible |
| `POST` | `/api/inventory/reserve` | Reservar stock (auth) |
| `DELETE` | `/api/inventory/reserve/{id}` | Liberar reserva (auth) |
| `POST` | `/api/inventory/confirm/{id}` | Confirmar reserva (auth) |

### Notification Service (puerto 5006)

| Método | Endpoint | Descripción |
|--------|----------|-------------|
| `GET` | `/notifications` | Listar notificaciones |
| `POST` | `/notifications` | Crear notificación |

---

## Environment Variables

Cada servicio acepta estas variables de entorno:

| Variable | Descripción | Ejemplo |
|----------|-------------|---------|
| `ConnectionStrings__DefaultConnection` | Connection string de PostgreSQL | `Host=postgres;Port=5432;Database=AuthDb;...` |
| `Jwt__Key` | Clave secreta para JWT (min 32 bytes) | `super_secret_key_...` |
| `ASPNETCORE_ENVIRONMENT` | Entorno (Development/Production) | `Development` |

---

## Seguridad

- Contraseñas hasheadas con BCrypt
- Tokens JWT con expiración de 7 días
- Rate limiting (para implementar)
- Validación de inputs en todos los endpoints
- Sanitización de queries (EF Core previene SQL injection)
- Cards nunca se almacenan completas (masking automático)

---

## Roadmap / Mejoras Futuras

- [ ] Implementar event-driven communication (RabbitMQ/Kafka)
- [ ] Agregar rate limiting por usuario
- [ ] Tests de integración con PostgreSQL real
- [ ] CI/CD con GitHub Actions
- [ ] Kubernetes deployment
- [ ] Implementar Refresh Tokens
- [ ] Agregar logging centralizado (ELK/Seq)
- [ ] Cache con Redis
- [ ] WebSocket para notificaciones real-time

---

## Estructura del Proyecto

```
E-Commerce-Microservices/
├── api-gateway/           # YARP API Gateway
├── client/                # Frontend (HTML/CSS/JS)
├── nginx/                 # Configuración nginx
├── services/              # Microservicios
│   ├── auth-service/      # Autenticación
│   ├── cart-service/      # Carrito
│   ├── inventory-service/ # Inventario
│   ├── notification-service/ # Notificaciones
│   ├── order-service/     # Órdenes
│   ├── payment-service/   # Pagos
│   └── product-service/   # Productos
├── tests/                 # Tests unitarios
│   └── EcommerceMicroservices.Tests/
├── docker-compose.yml     # Orquestación Docker
└── README.md
```

---

## License

MIT License - ver archivo LICENSE.

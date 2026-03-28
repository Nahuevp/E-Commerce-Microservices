# Deploy a Railway

## Prerrequisitos
- Cuenta en [Railway](https://railway.app) (gratis)
- GitHub account
- Docker instalado localmente (para testing)

## Metodo 1: Deploy desde GitHub (Recomendado)

### 1. Subir el proyecto a GitHub
```bash
git init
git add .
git commit -m "Initial commit"
git branch -M main
git remote add origin https://github.com/TU_USER/E-Commerce-Microservices.git
git push -u origin main
```

### 2. Crear proyecto en Railway
1. Ir a [railway.app](https://railway.app)
2. Click "New Project" -> "Deploy from GitHub repo"
3. Seleccionar el repositorio
4. Railway detectara el `docker-compose.yml` automaticamente

### 3. Configurar servicios

Railway creara servicios para cada contenedor en docker-compose.yml. Configurar:

#### PostgreSQL
1. Crear plugin PostgreSQL en Railway
2. Copiar la DATABASE_URL

#### Cada microservicio
Para cada servicio, configurar:
- `Jwt__Key`: Una clave secreta de al menos 32 caracteres
- `ConnectionStrings__DefaultConnection`: Usar la variable de Railway `${{Postgres.DATABASE_URL}}`

### 4. Esperar deploy
Railway buildea y deploya automaticamente en cada push a main.

## Metodo 2: Deploy manual con Docker

### 1. Instalar Railway CLI
```bash
npm install -g @railway/cli
railway login
```

### 2. Inicializar proyecto
```bash
cd E-Commerce-Microservices
railway init
```

### 3. Deploy
```bash
railway up
```

## Verificacion

Despues del deploy:
1. Verificar logs: `railway logs -f`
2. Abrir la URL del API Gateway
3. Probar health checks de cada servicio
4. Verificar el frontend

## Troubleshooting

### Puerto en uso
Si Railway reporta error de puerto, verificar que todos los servicios usen `PORT` env var.

### Connection string
Railway provee `DATABASE_URL`, los servicios deben convertirla al formato Npgsql.

### Builds fallidos
Revisar los logs de build en el dashboard de Railway.

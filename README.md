# Banco API — Prueba técnica Backend .NET 8

API REST que expone clientes, cuentas y movimientos desde SQLite, y la situación crediticia de cada cliente consultando la **Central de Deudores del BCRA**. La respuesta del BCRA se guarda en cache y la integración tiene timeout, retry, circuit breaker y fallback.

> 🎯 El **porqué de cada decisión técnica** (base de datos, resiliencia, cache, observabilidad) está más abajo, en [Decisiones de diseño](#decisiones-de-diseño).

| | |
|---|---|
| Runtime | .NET 8 · ASP.NET Core Minimal APIs |
| Datos | SQLite + **Dapper** (esquema manejado por scripts SQL, sin migraciones) |
| BCRA | `HttpClient` tipado + `Microsoft.Extensions.Http.Resilience` (Polly v8) |
| Cache | `IMemoryCache`, expiración configurable (`CacheOptions`) |
| Logging | Serilog (consola + archivo) con `CorrelationId` en cada línea |
| Tests | xUnit + Moq (+ `FakeTimeProvider`, `WebApplicationFactory`) |
| Contenedor | Dockerfile multi-stage, usuario no root, configuración por variables de entorno |

---

## Índice

- [Estructura](#estructura)
- [Cómo ejecutar](#cómo-ejecutar) — [base de datos](#1-base-de-datos) · [API](#2-api) · [tests](#3-tests) · [Docker](#4-docker)
- [Endpoints](#endpoints)
- [Decisiones de diseño](#decisiones-de-diseño) — [base de datos](#base-de-datos-y-versionado-del-esquema) · [BCRA](#integración-con-el-bcra) · [resiliencia](#resiliencia) · [cache](#cache) · [observabilidad](#observabilidad) · [tests](#tests)
- [Configuración](#configuración-appsettingsjson)
- [Colección Postman](#colección-postman)
- [Problemas conocidos](#problemas-conocidos)
- [Notas](#notas)

---

## Estructura

```
src/
  Banco.Domain          Entidades (Cliente, Cuenta, Movimiento), value object Cuit, situación crediticia. Sin dependencias.
  Banco.Application     Casos de uso (ClientesService, CuentasService), DTOs, contratos (repositorios, IBcraClient, ...).
  Banco.Infrastructure  Dapper + SQLite, cliente BCRA + resiliencia, cache con fallback, Options, health checks.
  Banco.Api             Endpoints, middlewares (CorrelationId, logging), ProblemDetails, Serilog, Swagger.
tests/
  Banco.UnitTests       Dominio, servicios, mapeo BCRA, cache (hit/miss/expiración/fallback) y resiliencia (retry/timeout/breaker).
  Banco.Api.Tests       Integración: API real en memoria contra SQLite creado con los scripts + BCRA mockeado.
database/
  masterSQL.sql            Esquema SQLite (idempotente). ÚNICO dueño de la estructura.
  seed.sql                 Datos iniciales (idempotente).
  schema.sql               Foto del esquema final (referencia, generado con `.schema`).
  masterSQL.sqlserver.sql  El mismo master para SQL Server, con ALTERs condicionales por catálogo.
docker/entrypoint.sh    Aplica master + seed y arranca la API dentro del contenedor.
tools/Banco.DbInit      Aplica master + seed sin necesitar el sqlite3 CLI (dotnet run --project tools/Banco.DbInit).
tools/probar-bcra.*     Scripts para probar a mano la API del BCRA (PowerShell y bash).
postman/                Colección Postman con tests.
```

Dependencias entre capas: `Api → Application + Infrastructure`, `Infrastructure → Application → Domain`.

---

## Cómo ejecutar

### Requisitos

- .NET SDK 8 o superior (un SDK 9 o 10 compila `net8.0` sin problemas) y el **runtime de .NET 8** para ejecutar.
- Visual Studio: **2022 actualizado o 2026**. VS 2019 no soporta .NET 8. Si al abrir aparece "targeting a version of .NET which is not installed", abrí el `.sln` desde una versión más nueva de Visual Studio.
- `sqlite3` CLI para crear la base (Windows: `winget install SQLite.SQLite`; Debian/Ubuntu: `apt install sqlite3`), o en su lugar `tools/Banco.DbInit` (ver abajo).
- Docker, si se va a usar el contenedor.

### 1. Base de datos

Desde la raíz del repositorio, **en este orden**:

```bash
mkdir -p data
sqlite3 data/banco.db < database/masterSQL.sql   # 1. esquema (idempotente)
sqlite3 data/banco.db < database/seed.sql        # 2. datos   (idempotente)
```

En PowerShell (`<` no existe):

```powershell
New-Item -ItemType Directory -Force data | Out-Null
Get-Content database/masterSQL.sql -Raw | sqlite3 data/banco.db
Get-Content database/seed.sql -Raw | sqlite3 data/banco.db
```

Los dos scripts se pueden volver a ejecutar sin errores y sin duplicar nada.

**Sin `sqlite3` instalado**: el repo incluye `tools/Banco.DbInit`, que ejecuta los mismos scripts con Microsoft.Data.Sqlite (no genera ni modifica SQL: solo los corre).

```bash
dotnet run --project tools/Banco.DbInit            # crea/actualiza data/banco.db (esquema + seed)
dotnet run --project tools/Banco.DbInit -- --sin-seed
```

### 2. API

```bash
dotnet run --project src/Banco.Api
```

- Swagger: <http://localhost:5080/swagger>
- Health: <http://localhost:5080/health>
- Logs: consola y `src/Banco.Api/logs/banco-YYYYMMDD.log`

La connection string por defecto (`Data Source=../../data/banco.db`) es relativa al proyecto de la API y apunta a `data/banco.db` en la raíz. Al arrancar, la API verifica la base y loguea un warning si falta el esquema; **nunca lo crea ni lo modifica**.

### 3. Tests

```bash
dotnet test
```

No se conectan a la API real del BCRA ni necesitan una base preparada: los de integración crean una SQLite temporal ejecutando `masterSQL.sql` y `seed.sql`.

### 4. Docker

```bash
docker build -t banco-api .

docker run --rm -p 8080:8080 \
  -v banco-data:/app/data \
  -e BcraOptions__BaseAddress="https://api.bcra.gob.ar/" \
  -e BcraOptions__TimeoutSeconds=5 \
  -e CacheOptions__BcraExpirationSeconds=3600 \
  banco-api
```

- API en <http://localhost:8080> (Swagger en `/swagger`, health en `/health`).
- Al arrancar, el contenedor aplica `masterSQL.sql` y `seed.sql`. Son idempotentes, así que es seguro reiniciar con el mismo volumen.
- Toda la configuración se pasa por variables de entorno con el formato `Seccion__Clave`, por ejemplo `DatabaseOptions__ConnectionString`, `BcraOptions__BaseAddress` o `CacheOptions__BcraExpirationSeconds`. Nada queda horneado en la imagen.
- Variables propias del entrypoint: `DB_PATH` (por defecto `/app/data/banco.db`), `DB_INIT=false` para no tocar el esquema y `DB_SEED=false` para no cargar datos.
- La imagen corre con el usuario no root `app` de la imagen oficial. Si se monta una carpeta del host en `/app/data`, tiene que tener permisos de escritura para ese usuario (UID 1654); con un volumen con nombre, como en el ejemplo, no hace falta nada.

---

## Endpoints

| Método | Ruta | Descripción |
|---|---|---|
| GET | `/api/clientes?page=1&pageSize=20` | Listado paginado (`pageSize` de 1 a 100). |
| GET | `/api/clientes/{cuit}` | Cliente + cuentas + situación BCRA. Acepta el CUIT con o sin guiones. 400 si es inválido (incluye dígito verificador), 404 si no es cliente. |
| GET | `/api/cuentas/{id}?desde=yyyy-MM-dd&hasta=yyyy-MM-dd` | Cuenta, titular y movimientos (más recientes primero). `hasta` es inclusivo. |
| GET | `/health` | Estado de SQLite, del circuito hacia el BCRA y de la cache. |

Ejemplo de `situacionBcra` dentro de `/api/clientes/{cuit}`:

```jsonc
"situacionBcra": {
  "estado": "Ok",                 // Ok | SinRegistros | NoDisponible
  "fuente": "cache",              // bcra | cache | cache-vencido | null
  "desactualizado": false,
  "consultadoEn": "2026-11-11T15:40:00+00:00",
  "detalle": null,                // motivo de la falla, si la hubo
  "denominacion": "YPF SA",
  "periodo": "202409",
  "peorSituacion": 1,
  "descripcionPeorSituacion": "Normal",
  "deudaTotalPesos": 1500500.00,  // el BCRA informa en MILES de pesos; acá ya está convertido
  "entidades": [ { "entidad": "BANCO ...", "situacion": 1, "montoPesos": 1500500.00, "diasAtrasoPago": null, ... } ]
}
```

Los errores se devuelven como **ProblemDetails** (`application/problem+json`) con un campo `correlationId`.

Datos de prueba: CUITs de personas sintéticos (el BCRA responde "sin registros") y de empresas públicas (YPF `30546689979`, BNA `30500010912`, Telecom `30639453738`, MercadoLibre `30703088534`) para ver datos reales del BCRA.

---

## Decisiones de diseño

### Base de datos y versionado del esquema

- **Sin EF Migrations, `Migrate()` ni `EnsureCreated()`.** El esquema vive en `database/masterSQL.sql`, revisable por DBAs, y la API solo lee.
- **Idempotencia**: `CREATE TABLE/INDEX IF NOT EXISTS` + `INSERT OR IGNORE` sobre `SchemaVersion`. El seed usa `id` explícito con `INSERT OR IGNORE`, así que no duplica nada, ni siquiera los movimientos, que no tienen clave natural.
- **ALTERs condicionales**: SQLite **no tiene DDL condicional en SQL puro** (no existe `ADD COLUMN IF NOT EXISTS` ni bloques `IF`). Por eso `database/masterSQL.sqlserver.sql` muestra el mismo master para el motor productivo:
  - Cada tabla se crea si no existe (`sys.tables`).
  - Cada columna se asegura con `IF NOT EXISTS (SELECT 1 FROM sys.columns ...) ALTER TABLE ... ADD`.
  - Índices y FKs se chequean contra `sys.indexes` y `sys.foreign_keys`.

  Así el mismo script lleva tanto una base vacía como una existente al estado esperado, y un cambio futuro es un bloque más con su chequeo y su registro en `SchemaVersion`.
- Las tablas respetan el diagrama del enunciado. Además se agregan FKs para las relaciones del diagrama, `UNIQUE` en `Clientes.cuit` y en `Cuentas(codSucursal, numero)`, índices de búsqueda, un `CHECK` de formato del CUIT y la tabla de control `SchemaVersion`.
- **`importe` con signo**: positivo para créditos, negativo para débitos. `Cuentas.saldo` es una columna persistida; en el seed es igual a la suma de los movimientos (hay un test que lo verifica).
- **Dapper** en lugar de EF: el esquema lo manejan los DBAs (database-first) y las queries son de lectura. Todas pasan por `SqlExecutor`, que loguea SQL, parámetros, filas y duración.
- Detalles de SQLite: `DECIMAL(18,2)` se declara igual que en el diagrama, pero SQLite lo guarda como INTEGER si el valor es redondo y como REAL si no. Como Dapper fija el tipo de la columna con la primera fila, las queries leen `CAST(saldo AS REAL)` y `CAST(importe AS REAL)` para que todas las filas lleguen con el mismo tipo. `DATETIME` se guarda como texto ISO-8601. Las FKs se activan por conexión (`Foreign Keys=True`). `Mode=ReadWrite` evita que se cree un archivo vacío si la ruta está mal.

### Integración con el BCRA

`GET {BaseAddress}CentralDeDeudores/v1.0/Deudas/{cuit}` (ruta configurable en `BcraOptions:DeudasPath`).

| Respuesta del BCRA | Interpretación | Retry | Cuenta para el breaker | Cache |
|---|---|---|---|---|
| 200 | Situación crediticia | — | No | Sí (`BcraExpirationSeconds`) |
| 404 con formato BCRA | **Sin registros: no es un error** | No | No | Sí (`BcraNotFoundExpirationSeconds`) |
| 404 sin formato BCRA | Ruta mal configurada | No | No | No |
| 400 | No debería ocurrir (el CUIT se valida antes) | No | No | No |
| 408 / 429 / 5xx / error de red / timeout | Falla transitoria | Sí | Sí | — |

**Cada entidad informa su propio último período**: la respuesta real puede traer varios períodos (por ejemplo, `202609` con casi todas las entidades y `202608` con las que todavía no informaron septiembre). Se toman las entidades de todos los períodos, cada una con su `periodo`, y si una apareciera repetida se queda la más reciente. `situacionBcra.periodo` es el más reciente.

El CUIT se valida localmente (11 dígitos + dígito verificador módulo 11) antes de llamar al BCRA. Los montos del BCRA vienen en **miles de pesos** y se convierten a pesos en el dominio. `diasAtrasoPago = 0` significa "no aplica" y se devuelve como `null`.

### Resiliencia

Pipeline del `HttpClient`, de afuera hacia adentro:

```
Timeout total → Retry (exponencial + jitter) → Circuit breaker → Timeout por intento → HTTP
```

- **Timeout por intento** = `BcraOptions:TimeoutSeconds`. El `HttpClient.Timeout` por defecto (100 s) se anula para que no interfiera.
- **Retry**: `RetryCount` reintentos con backoff exponencial desde `RetryBaseDelayMs`, con jitter. Respeta `Retry-After` en 429/503.
- **Circuit breaker**: el retry está afuera del breaker, así que cada intento fallido cuenta para abrirlo, y con el circuito abierto ya no se reintenta. Polly v8 no cuenta "N fallos consecutivos": abre por porcentaje de fallos dentro de una ventana. `CircuitBreakerFailures: 5` se mapea a `MinimumThroughput = 5` + `FailureRatio = 1.0` (al menos 5 llamadas en la ventana `CircuitBreakerSamplingSeconds` y todas fallidas). El circuito queda abierto `CircuitBreakerDurationSeconds` y después prueba una llamada (half-open).
- **Fallback**, documentado como estrategia explícita:
  1. Dato fresco en cache → se devuelve (`fuente: "cache"`).
  2. Si no hay, se consulta al BCRA → se guarda en cache (`fuente: "bcra"`).
  3. Si el BCRA falla o el circuito está abierto y hay un dato vencido de menos de `BcraStaleRetentionSeconds` → se devuelve con `fuente: "cache-vencido"` y `desactualizado: true`.
  4. Si no hay nada → `estado: "NoDisponible"` con el motivo en `detalle`.

  En todos los casos `/api/clientes/{cuit}` responde **200 con los datos locales**: el cliente y sus cuentas son válidos aunque el BCRA esté caído, y el bloque `situacionBcra` dice explícitamente qué pasó. Se eligió esto en lugar de un 503 porque la caída de una dependencia externa no invalida el resto de la respuesta.

### Cache

- `IMemoryCache`, clave `bcra:deudas:{cuit}`, `SizeLimit` configurable.
- `IMemoryCache` borra la entrada al expirar, y entonces no quedaría dato vencido para el fallback. Por eso la entrada se guarda `BcraStaleRetentionSeconds` y la vigencia "fresca" (`BcraExpirationSeconds`) se calcula con la hora en que se obtuvo el dato. El TTL configurable que pide la consigna sigue siendo `BcraExpirationSeconds`.
- Requests simultáneos al mismo CUIT hacen **una sola** llamada al BCRA: hay un semáforo por clave con doble chequeo.
- Se loguea `Cache HIT`, `MISS`, `STALE` y `SET`.

### Observabilidad

- **CorrelationId**: el middleware lee `X-Correlation-ID`. Si no viene, o trae caracteres inválidos o más de 64 caracteres (para evitar inyección en logs), genera un GUID. Después:
  - lo agrega a todos los logs del request con `LogContext`;
  - lo propaga al BCRA con un `DelegatingHandler`;
  - lo devuelve en la respuesta, incluso en errores;
  - lo incluye en los ProblemDetails.

  Para pasarlo al handler se usa `AsyncLocal`, porque `IHttpClientFactory` crea los handlers en su propio scope de DI.
- **Logs**: los eventos de resiliencia los loguea `BcraResiliencia` (reintentos, apertura y cierre del circuito); la telemetría propia de Polly está silenciada en `appsettings.json` (`"Polly": "Fatal"`) para no duplicarlos. Se loguean request/response de cada endpoint (método, ruta, status y duración; el body con nivel Debug, activo en Development), queries SQL, cada intento al BCRA (request, status, duración y body truncado), eventos de retry y de circuito, cache hit/miss y excepciones.
- **Health** (`/health`):
  - `sqlite`: conexión + versión de esquema.
  - `bcra-circuit`: Closed → Healthy; Open/HalfOpen → **Degraded**.
  - `memory-cache`: entradas, hits y misses.

  Degraded responde 200: un BCRA caído no debe hacer que un orquestador reinicie la API. El health **no llama al BCRA**, porque tiene rate limit por IP.

### Tests

| Suite | Qué cubre |
|---|---|
| `Domain/*` | `Cuit` (formatos, dígito verificador), conversión de miles a pesos, peor situación, deuda total |
| `Application/*` | Servicios con repositorios mockeados: validaciones, 404 sin consultar al BCRA, BCRA caído, paginación, rango de fechas |
| `Bcra/BcraMapperTests` | JSON del manual del BCRA → dominio |
| `Bcra/SituacionCrediticiaProviderTests` | **Cache**: miss → hit sin segunda llamada (`Times.Once`), claves por CUIT, expiración con `FakeTimeProvider`, 404 cacheado, fallback a dato vencido, `NoDisponible`, concurrencia |
| `Bcra/BcraResilienciaTests` | **Resiliencia** con el *mismo* pipeline que producción y `HttpMessageHandler` mockeado: retry ante 5xx, error de red, timeout por intento, 404/400 sin retry, apertura del circuito, que no se llame al BCRA con el circuito abierto, recuperación (half-open → closed), health check, propagación del CorrelationId |
| `Banco.Api.Tests` | Endpoints end to end, ProblemDetails, CorrelationId entrante/generado/propagado, health, idempotencia de los scripts y consistencia de saldos |

---

## Configuración (`appsettings.json`)

```json
"DatabaseOptions": { "ConnectionString": "Data Source=../../data/banco.db;Mode=ReadWrite;Foreign Keys=True", "ExpectedSchemaVersion": 1 },
"BcraOptions": {
  "BaseAddress": "https://api.bcra.gob.ar/", "DeudasPath": "CentralDeDeudores/v1.0/Deudas/{0}",
  "TimeoutSeconds": 5, "TotalTimeoutSeconds": 30, "RetryCount": 3, "RetryBaseDelayMs": 200,
  "CircuitBreakerFailures": 5, "CircuitBreakerDurationSeconds": 30, "CircuitBreakerSamplingSeconds": 60,
  "LogResponseBody": true
},
"CacheOptions": { "BcraExpirationSeconds": 3600, "BcraNotFoundExpirationSeconds": 600, "BcraStaleRetentionSeconds": 86400, "SizeLimit": 10000 }
```

Todas las opciones se validan al arrancar (`ValidateOnStart`): una configuración inválida hace fallar el inicio, no el primer request.

---

## Colección Postman

Importar `postman/Banco.postman_collection.json` y ajustar la variable `baseUrl`: `http://localhost:5080` en local, `http://localhost:8080` en Docker. Cada request tiene tests, y a nivel colección se verifica que siempre venga el `X-Correlation-ID`.

## Problemas conocidos

**Windows sin la raíz ISRG Root X1** (`detalle`: "Certificado TLS del BCRA rechazado: UntrustedRoot"). El certificado de `api.bcra.gob.ar` es de Let's Encrypt y su cadena termina en *ISRG Root X1*. Windows descarga esa raíz bajo demanda desde Windows Update. En una máquina sin salida directa a internet (por ejemplo, detrás de un proxy corporativo) puede no tenerla, y entonces .NET rechaza el certificado. Edge sí abre la URL porque trae su propio almacén de raíces, lo que confunde el diagnóstico. La API loguea `TLS: certificado del BCRA rechazado` con el emisor y la cadena completa. Solución, sin permisos de administrador:

```powershell
# 1. Descargar desde Edge: https://letsencrypt.org/certs/isrgrootx1.der
# 2. Instalar para el usuario actual (verificar huella SHA1 CABD2A79A1076A31F21D253635CB039D4329A5E8)
certutil -user -addstore Root "$HOME\Downloads\isrgrootx1.der"
```

La API nunca desactiva la validación de certificados. En Docker no pasa, porque la imagen Debian ya trae la raíz.

## Notas

- La API del BCRA tiene rate limit por IP, sin límites publicados. El retry respeta `Retry-After` y la cache reduce las llamadas.
- Si el BCRA cambiara mayúsculas o minúsculas de la ruta, se ajusta en `BcraOptions:DeudasPath`. Un 404 sin el formato del BCRA se reporta como ruta mal configurada, no como "sin registros".

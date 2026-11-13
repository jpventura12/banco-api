-- =============================================================================
-- masterSQL.sql  -  Esquema de la base Banco (SQLite)
-- -----------------------------------------------------------------------------
-- Versión de esquema: 1
--
-- Script maestro IDEMPOTENTE: se puede ejecutar N veces sobre la misma base
-- sin errores y sin duplicar objetos.
--   * Tablas e índices: CREATE ... IF NOT EXISTS.
--   * Registro de versión: INSERT OR IGNORE sobre SchemaVersion.
--
-- La aplicación NUNCA crea ni modifica el esquema (no hay EF Migrations,
-- Database.Migrate() ni EnsureCreated()): este archivo es el único dueño
-- de la estructura.
--
-- ALTERs condicionales: SQLite no tiene DDL condicional en SQL puro
-- (no existe ALTER TABLE ... ADD COLUMN IF NOT EXISTS ni bloques IF).
-- Ver database/masterSQL.sqlserver.sql para el mismo master en SQL Server,
-- donde cada cambio de estructura se aplica solo si el catálogo
-- (sys.tables / sys.columns / sys.indexes) indica que todavía no existe.
--
-- Ejecución:  sqlite3 data/banco.db < database/masterSQL.sql
-- =============================================================================

PRAGMA foreign_keys = ON;

BEGIN TRANSACTION;

-- Control de versión del esquema (lo lee el health check de la API).
CREATE TABLE IF NOT EXISTS SchemaVersion (
    version      INTEGER      PRIMARY KEY,
    descripcion  VARCHAR(200) NOT NULL,
    aplicado_at  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- -----------------------------------------------------------------------------
-- Tablas según el diagrama del enunciado.
-- Nota: el diagrama dice "INT PK". En SQLite se declara INTEGER PRIMARY KEY
-- para que la PK sea el rowid (autoincremental); "INT PRIMARY KEY" no lo es.
-- -----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS Clientes (
    id          INTEGER       PRIMARY KEY,
    nombre      VARCHAR(255)  NOT NULL,
    apellido    VARCHAR(255)  NULL,          -- NULL para personas jurídicas
    cuit        VARCHAR(11)   NOT NULL
                CHECK (length(cuit) = 11 AND cuit NOT GLOB '*[^0-9]*'),
    created_at  DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at  DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS Cuentas (
    id           INTEGER        PRIMARY KEY,
    idCliente    INTEGER        NOT NULL REFERENCES Clientes (id),
    numero       VARCHAR(12)    NOT NULL,
    codSucursal  VARCHAR(4)     NOT NULL,
    saldo        DECIMAL(18,2)  NOT NULL DEFAULT 0,
    created_at   DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at   DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS Movimientos (
    id           INTEGER        PRIMARY KEY,
    idCuenta     INTEGER        NOT NULL REFERENCES Cuentas (id),
    fecha        DATETIME       NOT NULL,
    tipo         VARCHAR(50)    NOT NULL,
    descripcion  VARCHAR(50)    NULL,
    importe      DECIMAL(18,2)  NOT NULL,     -- con signo: + crédito / - débito
    created_at   DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at   DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- -----------------------------------------------------------------------------
-- Índices
-- -----------------------------------------------------------------------------
CREATE UNIQUE INDEX IF NOT EXISTS UX_Clientes_Cuit            ON Clientes (cuit);
CREATE UNIQUE INDEX IF NOT EXISTS UX_Cuentas_Sucursal_Numero  ON Cuentas (codSucursal, numero);
CREATE        INDEX IF NOT EXISTS IX_Cuentas_IdCliente        ON Cuentas (idCliente);
CREATE        INDEX IF NOT EXISTS IX_Movimientos_IdCuenta_Fecha ON Movimientos (idCuenta, fecha);

-- -----------------------------------------------------------------------------
-- Versión aplicada
-- -----------------------------------------------------------------------------
INSERT OR IGNORE INTO SchemaVersion (version, descripcion)
VALUES (1, 'Esquema inicial: Clientes, Cuentas, Movimientos');

COMMIT;

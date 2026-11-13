-- =============================================================================
-- schema.sql  -  Foto del esquema final (SQLite), solo de referencia.
-- Generado con:  sqlite3 data/banco.db .schema
-- Para crear o actualizar la base usar masterSQL.sql (idempotente).
-- =============================================================================

CREATE TABLE SchemaVersion (
    version      INTEGER      PRIMARY KEY,
    descripcion  VARCHAR(200) NOT NULL,
    aplicado_at  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE Clientes (
    id          INTEGER       PRIMARY KEY,
    nombre      VARCHAR(255)  NOT NULL,
    apellido    VARCHAR(255)  NULL,          -- NULL para personas jurídicas
    cuit        VARCHAR(11)   NOT NULL
                CHECK (length(cuit) = 11 AND cuit NOT GLOB '*[^0-9]*'),
    created_at  DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at  DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE Cuentas (
    id           INTEGER        PRIMARY KEY,
    idCliente    INTEGER        NOT NULL REFERENCES Clientes (id),
    numero       VARCHAR(12)    NOT NULL,
    codSucursal  VARCHAR(4)     NOT NULL,
    saldo        DECIMAL(18,2)  NOT NULL DEFAULT 0,
    created_at   DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at   DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE Movimientos (
    id           INTEGER        PRIMARY KEY,
    idCuenta     INTEGER        NOT NULL REFERENCES Cuentas (id),
    fecha        DATETIME       NOT NULL,
    tipo         VARCHAR(50)    NOT NULL,
    descripcion  VARCHAR(50)    NULL,
    importe      DECIMAL(18,2)  NOT NULL,     -- con signo: + crédito / - débito
    created_at   DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at   DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE UNIQUE INDEX UX_Clientes_Cuit            ON Clientes (cuit);

CREATE UNIQUE INDEX UX_Cuentas_Sucursal_Numero  ON Cuentas (codSucursal, numero);

CREATE INDEX IX_Cuentas_IdCliente        ON Cuentas (idCliente);

CREATE INDEX IX_Movimientos_IdCuenta_Fecha ON Movimientos (idCuenta, fecha);


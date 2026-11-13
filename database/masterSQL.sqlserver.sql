-- =============================================================================
-- masterSQL.sqlserver.sql  -  El mismo master, para el motor productivo (SQL Server)
-- -----------------------------------------------------------------------------
-- REFERENCIA: muestra cómo se traslada el patrón idempotente a SQL Server,
-- donde sí hay DDL condicional. No se ejecuta en la prueba (que usa SQLite).
--
-- Patrón:
--   1. Cada tabla se crea solo si no existe (chequeo en sys.tables).
--   2. Cada columna se asegura con un ALTER condicional (chequeo en sys.columns):
--      así el mismo script lleva una base vacía O una base existente/vieja
--      al estado esperado, sin errores y sin duplicar objetos.
--   3. Índices y FKs: chequeo en sys.indexes / sys.foreign_keys.
--   4. Cada bloque es re-ejecutable; GO separa lotes.
-- =============================================================================

SET XACT_ABORT ON;
GO

-- ---------------------------------------------------------------- SchemaVersion
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'SchemaVersion' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.SchemaVersion (
        version      INT          NOT NULL CONSTRAINT PK_SchemaVersion PRIMARY KEY,
        descripcion  VARCHAR(200) NOT NULL,
        aplicado_at  DATETIME     NOT NULL CONSTRAINT DF_SchemaVersion_aplicado_at DEFAULT (GETDATE())
    );
END
GO

-- --------------------------------------------------------------------- Clientes
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'Clientes' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.Clientes (
        id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Clientes PRIMARY KEY
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Clientes') AND name = N'nombre')
    ALTER TABLE dbo.Clientes ADD nombre VARCHAR(255) NOT NULL CONSTRAINT DF_Clientes_nombre DEFAULT ('');
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Clientes') AND name = N'apellido')
    ALTER TABLE dbo.Clientes ADD apellido VARCHAR(255) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Clientes') AND name = N'cuit')
    ALTER TABLE dbo.Clientes ADD cuit VARCHAR(11) NOT NULL CONSTRAINT DF_Clientes_cuit DEFAULT ('00000000000');
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Clientes') AND name = N'created_at')
    ALTER TABLE dbo.Clientes ADD created_at DATETIME NOT NULL CONSTRAINT DF_Clientes_created_at DEFAULT (GETDATE());
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Clientes') AND name = N'updated_at')
    ALTER TABLE dbo.Clientes ADD updated_at DATETIME NOT NULL CONSTRAINT DF_Clientes_updated_at DEFAULT (GETDATE());
GO

-- ---------------------------------------------------------------------- Cuentas
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'Cuentas' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.Cuentas (
        id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Cuentas PRIMARY KEY
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Cuentas') AND name = N'idCliente')
    ALTER TABLE dbo.Cuentas ADD idCliente INT NOT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Cuentas') AND name = N'numero')
    ALTER TABLE dbo.Cuentas ADD numero VARCHAR(12) NOT NULL CONSTRAINT DF_Cuentas_numero DEFAULT ('');
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Cuentas') AND name = N'codSucursal')
    ALTER TABLE dbo.Cuentas ADD codSucursal VARCHAR(4) NOT NULL CONSTRAINT DF_Cuentas_codSucursal DEFAULT ('0000');
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Cuentas') AND name = N'saldo')
    ALTER TABLE dbo.Cuentas ADD saldo DECIMAL(18,2) NOT NULL CONSTRAINT DF_Cuentas_saldo DEFAULT (0);
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Cuentas') AND name = N'created_at')
    ALTER TABLE dbo.Cuentas ADD created_at DATETIME NOT NULL CONSTRAINT DF_Cuentas_created_at DEFAULT (GETDATE());
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Cuentas') AND name = N'updated_at')
    ALTER TABLE dbo.Cuentas ADD updated_at DATETIME NOT NULL CONSTRAINT DF_Cuentas_updated_at DEFAULT (GETDATE());
GO

-- ------------------------------------------------------------------ Movimientos
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'Movimientos' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.Movimientos (
        id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Movimientos PRIMARY KEY
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Movimientos') AND name = N'idCuenta')
    ALTER TABLE dbo.Movimientos ADD idCuenta INT NOT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Movimientos') AND name = N'fecha')
    ALTER TABLE dbo.Movimientos ADD fecha DATETIME NOT NULL CONSTRAINT DF_Movimientos_fecha DEFAULT (GETDATE());
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Movimientos') AND name = N'tipo')
    ALTER TABLE dbo.Movimientos ADD tipo VARCHAR(50) NOT NULL CONSTRAINT DF_Movimientos_tipo DEFAULT ('');
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Movimientos') AND name = N'descripcion')
    ALTER TABLE dbo.Movimientos ADD descripcion VARCHAR(50) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Movimientos') AND name = N'importe')
    ALTER TABLE dbo.Movimientos ADD importe DECIMAL(18,2) NOT NULL CONSTRAINT DF_Movimientos_importe DEFAULT (0);
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Movimientos') AND name = N'created_at')
    ALTER TABLE dbo.Movimientos ADD created_at DATETIME NOT NULL CONSTRAINT DF_Movimientos_created_at DEFAULT (GETDATE());
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Movimientos') AND name = N'updated_at')
    ALTER TABLE dbo.Movimientos ADD updated_at DATETIME NOT NULL CONSTRAINT DF_Movimientos_updated_at DEFAULT (GETDATE());
GO

-- ------------------------------------------------------------- Claves foráneas
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Cuentas_Clientes')
    ALTER TABLE dbo.Cuentas ADD CONSTRAINT FK_Cuentas_Clientes
        FOREIGN KEY (idCliente) REFERENCES dbo.Clientes (id);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Movimientos_Cuentas')
    ALTER TABLE dbo.Movimientos ADD CONSTRAINT FK_Movimientos_Cuentas
        FOREIGN KEY (idCuenta) REFERENCES dbo.Cuentas (id);
GO

-- ---------------------------------------------------------------------- Índices
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Clientes_Cuit' AND object_id = OBJECT_ID(N'dbo.Clientes'))
    CREATE UNIQUE INDEX UX_Clientes_Cuit ON dbo.Clientes (cuit);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Cuentas_Sucursal_Numero' AND object_id = OBJECT_ID(N'dbo.Cuentas'))
    CREATE UNIQUE INDEX UX_Cuentas_Sucursal_Numero ON dbo.Cuentas (codSucursal, numero);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Cuentas_IdCliente' AND object_id = OBJECT_ID(N'dbo.Cuentas'))
    CREATE INDEX IX_Cuentas_IdCliente ON dbo.Cuentas (idCliente);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Movimientos_IdCuenta_Fecha' AND object_id = OBJECT_ID(N'dbo.Movimientos'))
    CREATE INDEX IX_Movimientos_IdCuenta_Fecha ON dbo.Movimientos (idCuenta, fecha);
GO

-- --------------------------------------------------------------- Versión aplicada
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersion WHERE version = 1)
    INSERT INTO dbo.SchemaVersion (version, descripcion)
    VALUES (1, 'Esquema inicial: Clientes, Cuentas, Movimientos');
GO

-- Ejemplo de un cambio futuro (versión 2): se agrega como un bloque más,
-- con su propio chequeo de catálogo, y queda registrado en SchemaVersion.
--
-- IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Cuentas') AND name = N'moneda')
--     ALTER TABLE dbo.Cuentas ADD moneda CHAR(3) NOT NULL CONSTRAINT DF_Cuentas_moneda DEFAULT ('ARS');
-- GO
-- IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersion WHERE version = 2)
--     INSERT INTO dbo.SchemaVersion (version, descripcion) VALUES (2, 'Cuentas.moneda');
-- GO

using Microsoft.Data.Sqlite;

var sinSeed = args.Contains("--sin-seed");

var rutaBase = Path.GetFullPath(args.FirstOrDefault(a => !a.StartsWith("--")) ?? Path.Combine("data", "banco.db"));

var carpetaScripts = BuscarCarpetaDatabase(Directory.GetCurrentDirectory())
    ?? BuscarCarpetaDatabase(AppContext.BaseDirectory);
if (carpetaScripts is null)
{
    Console.Error.WriteLine("No se encontró la carpeta 'database' con masterSQL.sql. Ejecutar desde la raíz del repositorio.");
    return 1;
}

Directory.CreateDirectory(Path.GetDirectoryName(rutaBase)!);
Console.WriteLine($"Base:    {rutaBase}");
Console.WriteLine($"Scripts: {carpetaScripts}");

var scripts = sinSeed ? new[] { "masterSQL.sql" } : new[] { "masterSQL.sql", "seed.sql" };

try
{
    using var conexion = new SqliteConnection($"Data Source={rutaBase};Mode=ReadWriteCreate");
    conexion.Open();

    foreach (var script in scripts)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText = File.ReadAllText(Path.Combine(carpetaScripts, script));
        comando.ExecuteNonQuery();
        Console.WriteLine($"  OK  {script}");
    }

    foreach (var tabla in new[] { "Clientes", "Cuentas", "Movimientos" })
    {
        using var conteo = conexion.CreateCommand();
        conteo.CommandText = $"SELECT COUNT(*) FROM {tabla}";

        Console.WriteLine($"  {tabla,-12} {conteo.ExecuteScalar()} filas");
    }

    using var version = conexion.CreateCommand();
    version.CommandText = "SELECT MAX(version) FROM SchemaVersion";
    Console.WriteLine($"  Versión de esquema: {version.ExecuteScalar()}");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"ERROR: {ex.Message}");
    return 1;
}

static string? BuscarCarpetaDatabase(string desde)
{
    for (var dir = new DirectoryInfo(desde); dir is not null; dir = dir.Parent)
    {
        var candidata = Path.Combine(dir.FullName, "database");
        if (File.Exists(Path.Combine(candidata, "masterSQL.sql")))
        {
            return candidata;
        }
    }

    return null;
}

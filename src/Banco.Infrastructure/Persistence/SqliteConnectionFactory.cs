using Banco.Infrastructure.Options;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Banco.Infrastructure.Persistence;

public sealed class SqliteConnectionFactory(IOptions<DatabaseOptions> options)
{
    public SqliteConnection Crear() => new(options.Value.ConnectionString);
}

internal sealed class ResolverRutaSqlite(IHostEnvironment env) : IPostConfigureOptions<DatabaseOptions>
{
    public void PostConfigure(string? name, DatabaseOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return;
        }

        var builder = new SqliteConnectionStringBuilder(options.ConnectionString);
        var dataSource = builder.DataSource;

        if (string.IsNullOrEmpty(dataSource)
            || dataSource == ":memory:"
            || dataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
            || Path.IsPathRooted(dataSource))
        {
            return;
        }

        builder.DataSource = Path.GetFullPath(Path.Combine(env.ContentRootPath, dataSource));
        options.ConnectionString = builder.ToString();
    }
}

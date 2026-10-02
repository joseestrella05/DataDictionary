using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using DataDictionaryWeb.Models;

namespace DataDictionaryWeb.Services
{
    public class DatabaseDiscoveryService
    {
        private const int DefaultTimeoutSeconds = 180;

        public async Task<(bool Success, string Message, string? ServerVersion)> TestConnectionAsync(ConnectionModel model)
        {
            try
            {
                var connStr = model.BuildConnectionString();
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                using var cmd = new SqlCommand("SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED; SELECT @@SERVERNAME AS ServerName, @@VERSION AS Version;", conn)
                {
                    CommandTimeout = DefaultTimeoutSeconds
                };

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    string serverName = reader["ServerName"]?.ToString() ?? model.Server;
                    string version = reader["Version"]?.ToString() ?? "SQL Server";
                    return (true, $"Conexión exitosa a {serverName}", version);
                }

                return (true, "Conexión exitosa.", null);
            }
            catch (Exception ex)
            {
                return (false, $"Error al conectar: {ex.Message}", null);
            }
        }

        public async Task<List<DatabaseItem>> GetDatabasesAsync(ConnectionModel model)
        {
            var databases = new List<DatabaseItem>();
            var connStr = model.BuildConnectionString();

            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            const string sql = @"
            SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
            SELECT 
                database_id,
                name,
                create_date
            FROM sys.databases
            WHERE database_id > 4 
              AND name NOT IN ('master', 'tempdb', 'model', 'msdb', 'distribution')
              AND state = 0
              AND HAS_DBACCESS(name) = 1
            ORDER BY name ASC;";

            using var cmd = new SqlCommand(sql, conn)
            {
                CommandTimeout = DefaultTimeoutSeconds
            };

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                databases.Add(new DatabaseItem
                {
                    DatabaseId = Convert.ToInt32(reader["database_id"]),
                    Name = reader["name"].ToString()!,
                    CreateDate = Convert.ToDateTime(reader["create_date"])
                });
            }

            return databases;
        }

        public async Task<List<SchemaGroupSelection>> GetTablesForDatabaseAsync(ConnectionModel model, string databaseName)
        {
            var groups = new Dictionary<string, SchemaGroupSelection>(StringComparer.OrdinalIgnoreCase);
            var connStr = model.BuildConnectionString(databaseName);

            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            const string sql = @"
            SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
            SELECT 
                s.name AS SchemaName,
                t.name AS TableName,
                COUNT(c.column_id) AS ColumnCount,
                MAX(CASE WHEN i.is_primary_key = 1 THEN 1 ELSE 0 END) AS HasPrimaryKey
            FROM sys.tables t
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            INNER JOIN sys.columns c ON t.object_id = c.object_id
            LEFT JOIN sys.indexes i ON t.object_id = i.object_id AND i.is_primary_key = 1
            WHERE t.is_ms_shipped = 0
            GROUP BY s.name, t.name
            ORDER BY s.name ASC, t.name ASC;";

            using var cmd = new SqlCommand(sql, conn)
            {
                CommandTimeout = DefaultTimeoutSeconds
            };

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string schemaName = reader["SchemaName"].ToString()!;
                string tableName = reader["TableName"].ToString()!;
                int colCount = Convert.ToInt32(reader["ColumnCount"]);
                bool hasPk = Convert.ToInt32(reader["HasPrimaryKey"]) == 1;

                if (!groups.TryGetValue(schemaName, out var group))
                {
                    group = new SchemaGroupSelection { SchemaName = schemaName };
                    groups[schemaName] = group;
                }

                group.Tables.Add(new TableSelectionItem
                {
                    SchemaName = schemaName,
                    TableName = tableName,
                    ColumnCount = colCount,
                    HasPrimaryKey = hasPk,
                    IsSelected = true
                });
            }

            return new List<SchemaGroupSelection>(groups.Values);
        }
    }
}

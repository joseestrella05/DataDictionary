using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using DataDictionaryWeb.Models;

namespace DataDictionaryWeb.Services
{
    public class MetadataExtractionService
    {
        private const int DefaultTimeoutSeconds = 180;

        public async Task<DatabaseDictionary> ExtractEnrichedMetadataAsync(
            ConnectionModel connectionModel,
            string databaseName,
            IEnumerable<string> selectedTableFullNames)
        {
            var selectedList = selectedTableFullNames?.ToList() ?? new List<string>();
            var dictionary = new DatabaseDictionary
            {
                DatabaseName = databaseName
            };

            var connStr = connectionModel.BuildConnectionString(databaseName);
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            // 1. Obtener servidor en modo sin bloqueos
            using (var cmdInfo = new SqlCommand("SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED; SELECT @@SERVERNAME AS ServerName;", conn) { CommandTimeout = DefaultTimeoutSeconds })
            using (var readerInfo = await cmdInfo.ExecuteReaderAsync())
            {
                if (await readerInfo.ReadAsync())
                {
                    dictionary.ServerName = readerInfo["ServerName"]?.ToString() ?? connectionModel.Server;
                }
            }

            // 2. Optimización Server-Side para bases masivas:
            // Si hay filtro de tablas, creamos y poblamos una tabla temporal indexada #SelectedTables
            // para que SQL Server solo procese metadatos de las tablas elegidas en el motor de base de datos.
            bool filterServerSide = selectedList.Count > 0;
            if (filterServerSide)
            {
                using var cmdCreateTemp = new SqlCommand(
                    "CREATE TABLE #SelectedTables (FullName NVARCHAR(260) COLLATE DATABASE_DEFAULT PRIMARY KEY);", conn)
                {
                    CommandTimeout = DefaultTimeoutSeconds
                };
                await cmdCreateTemp.ExecuteNonQueryAsync();

                // Insertar en lotes de hasta 500 registros para evitar límites de parámetros
                const int batchSize = 500;
                for (int i = 0; i < selectedList.Count; i += batchSize)
                {
                    var batch = selectedList.Skip(i).Take(batchSize).ToList();
                    var sbInsert = new StringBuilder("INSERT INTO #SelectedTables (FullName) VALUES ");
                    using var cmdInsert = new SqlCommand { Connection = conn, CommandTimeout = DefaultTimeoutSeconds };

                    for (int j = 0; j < batch.Count; j++)
                    {
                        if (j > 0) sbInsert.Append(", ");
                        string paramName = $"@p{j}";
                        sbInsert.Append($"({paramName})");
                        cmdInsert.Parameters.Add(paramName, SqlDbType.NVarChar, 260).Value = batch[j];
                    }

                    cmdInsert.CommandText = sbInsert.ToString();
                    await cmdInsert.ExecuteNonQueryAsync();
                }
            }

            // 3. Consulta unificada enriquecida (PK, FK, UDT, Unicode, Extended Properties)
            string filterJoin = filterServerSide
                ? "INNER JOIN #SelectedTables st ON st.FullName = CONCAT(s.name, '.', t.name)"
                : string.Empty;

            string sql = $@"
            SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

            SELECT 
                s.name AS SchemaName,
                t.name AS TableName,
                ISNULL(CAST(ep_t.value AS NVARCHAR(MAX)), 'Sin descripción disponible') AS TableDescription,
                c.column_id AS ColumnId,
                c.name AS ColumnName,
                ty.name AS DataType,
                CASE 
                    -- Unicode: 2 bytes por carácter en SQL Server
                    WHEN COALESCE(TYPE_NAME(ty.system_type_id), ty.name) IN ('nvarchar', 'nchar') THEN 
                        CASE WHEN c.max_length = -1 THEN 'MAX' ELSE CAST(c.max_length / 2 AS VARCHAR(10)) END
                    -- No Unicode y binarios
                    WHEN COALESCE(TYPE_NAME(ty.system_type_id), ty.name) IN ('varchar', 'char', 'varbinary', 'binary') THEN 
                        CASE WHEN c.max_length = -1 THEN 'MAX' ELSE CAST(c.max_length AS VARCHAR(10)) END
                    -- Numéricos con escala y precisión
                    WHEN COALESCE(TYPE_NAME(ty.system_type_id), ty.name) IN ('decimal', 'numeric') THEN 
                        CONCAT(c.precision, ',', c.scale)
                    -- Tipos temporales con escala de fracciones de segundo
                    WHEN COALESCE(TYPE_NAME(ty.system_type_id), ty.name) IN ('datetime2', 'time', 'datetimeoffset') THEN 
                        CAST(c.scale AS VARCHAR(10))
                    -- Tipos espaciales / BLOB / XML
                    WHEN c.max_length = -1 THEN 'MAX'
                    -- Tipos primitivos estándar
                    ELSE CAST(c.max_length AS VARCHAR(10))
                END AS DisplayLength,
                c.is_nullable AS IsNullable,
                c.is_identity AS IsIdentity,
                CASE WHEN pk.column_id IS NOT NULL THEN 1 ELSE 0 END AS IsPrimaryKey,
                fk.RefTarget AS ForeignKeyReference,
                ISNULL(CAST(ep_c.value AS NVARCHAR(MAX)), '') AS ColumnDescription
            FROM sys.tables t
            INNER JOIN sys.schemas s 
                ON t.schema_id = s.schema_id
            {filterJoin}
            INNER JOIN sys.columns c 
                ON t.object_id = c.object_id
            INNER JOIN sys.types ty 
                ON c.user_type_id = ty.user_type_id
            -- Identificación de Claves Primarias (PK)
            LEFT JOIN (
                SELECT ic.object_id, ic.column_id
                FROM sys.indexes i
                INNER JOIN sys.index_columns ic 
                    ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                WHERE i.is_primary_key = 1
            ) pk ON pk.object_id = c.object_id AND pk.column_id = c.column_id
            -- Identificación de Claves Foráneas (FK) y referencias cruzadas
            LEFT JOIN (
                SELECT 
                    fkc.parent_object_id,
                    fkc.parent_column_id,
                    CONCAT(s_ref.name, '.', t_ref.name, '(', c_ref.name, ')') AS RefTarget
                FROM sys.foreign_key_columns fkc
                INNER JOIN sys.tables t_ref ON fkc.referenced_object_id = t_ref.object_id
                INNER JOIN sys.schemas s_ref ON t_ref.schema_id = s_ref.schema_id
                INNER JOIN sys.columns c_ref ON fkc.referenced_object_id = c_ref.object_id 
                    AND fkc.referenced_column_id = c_ref.column_id
            ) fk ON fk.parent_object_id = c.object_id AND fk.parent_column_id = c.column_id
            -- Descripción de Tabla (MS_Description)
            LEFT JOIN sys.extended_properties ep_t 
                ON ep_t.major_id = t.object_id 
                AND ep_t.minor_id = 0 
                AND ep_t.name = 'MS_Description' 
                AND ep_t.class = 1
            -- Descripción de Columna (MS_Description)
            LEFT JOIN sys.extended_properties ep_c 
                ON ep_c.major_id = c.object_id 
                AND ep_c.minor_id = c.column_id 
                AND ep_c.name = 'MS_Description' 
                AND ep_c.class = 1
            WHERE t.is_ms_shipped = 0
            ORDER BY s.name ASC, t.name ASC, c.column_id ASC;";

            using var cmd = new SqlCommand(sql, conn)
            {
                CommandTimeout = DefaultTimeoutSeconds
            };

            using var reader = await cmd.ExecuteReaderAsync();

            var schemaMap = new Dictionary<string, SchemaMetadata>(StringComparer.OrdinalIgnoreCase);
            var tableMap = new Dictionary<string, TableMetadata>(StringComparer.OrdinalIgnoreCase);

            while (await reader.ReadAsync())
            {
                string schemaName = reader["SchemaName"].ToString()!;
                string tableName = reader["TableName"].ToString()!;
                string fullTableName = $"{schemaName}.{tableName}";
                string tableDesc = reader["TableDescription"].ToString()!;
                int colId = Convert.ToInt32(reader["ColumnId"]);
                string colName = reader["ColumnName"].ToString()!;
                string dataType = reader["DataType"].ToString()!;
                string length = reader["DisplayLength"].ToString()!;
                bool isNullable = Convert.ToBoolean(reader["IsNullable"]);
                bool isIdentity = Convert.ToBoolean(reader["IsIdentity"]);
                bool isPrimaryKey = Convert.ToInt32(reader["IsPrimaryKey"]) == 1;
                string? fkRef = reader["ForeignKeyReference"] == DBNull.Value ? null : reader["ForeignKeyReference"].ToString();
                string colDesc = reader["ColumnDescription"].ToString()!;

                if (!schemaMap.TryGetValue(schemaName, out var schemaMeta))
                {
                    schemaMeta = new SchemaMetadata { SchemaName = schemaName };
                    schemaMap[schemaName] = schemaMeta;
                    dictionary.Schemas.Add(schemaMeta);
                }

                if (!tableMap.TryGetValue(fullTableName, out var tableMeta))
                {
                    tableMeta = new TableMetadata
                    {
                        SchemaName = schemaName,
                        TableName = tableName,
                        Description = string.IsNullOrWhiteSpace(tableDesc) ? "Sin descripción disponible" : tableDesc.Trim()
                    };
                    tableMap[fullTableName] = tableMeta;
                    schemaMeta.Tables.Add(tableMeta);
                }

                tableMeta.Columns.Add(new ColumnMetadata
                {
                    ColumnId = colId,
                    Name = colName,
                    DataType = dataType,
                    Length = length,
                    IsNullable = isNullable,
                    IsIdentity = isIdentity,
                    IsPrimaryKey = isPrimaryKey,
                    ForeignKeyReference = fkRef,
                    BaseDescription = colDesc
                });
            }

            return dictionary;
        }
    }
}

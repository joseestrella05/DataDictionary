using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using DataDictionaryWeb.Models;

namespace DataDictionaryWeb.Services
{
    public class IntelligentMetadataService
    {
        private const int DefaultTimeoutSeconds = 180;

        // Glosario base en memoria con estructura formal (Tabla, Campo, Tipo, Long, Descripcion)
        private static readonly List<GlossaryItem> DefaultGlossaryItems = new()
        {
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Unico", Tipo = "int", Long = "4", Descripcion = "Identificador único del registro. Su valor se genera automáticamente de forma incremental para garantizar la unicidad y el control de la secuencia de los registros.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Id", Tipo = "int", Long = "4", Descripcion = "Identificador único del registro. Su valor se genera automáticamente de forma incremental para garantizar la unicidad y el control de la secuencia de los registros.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "CreadoPor", Tipo = "nvarchar", Long = "100", Descripcion = "Nombre de usuario o cuenta de sistema responsable de registrar la transacción original en la base de datos.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "CreadaPor", Tipo = "nvarchar", Long = "100", Descripcion = "Nombre de usuario o cuenta de sistema responsable de registrar la transacción original en la base de datos.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Autor", Tipo = "nvarchar", Long = "100", Descripcion = "Nombre de usuario o cuenta de sistema responsable de registrar la transacción original en la base de datos.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "ModificadoPor", Tipo = "nvarchar", Long = "100", Descripcion = "Último usuario o servicio responsable de modificar el registro.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "ActualizadoPor", Tipo = "nvarchar", Long = "100", Descripcion = "Último usuario o servicio responsable de modificar el registro.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "FechaAutorizacion", Tipo = "datetime", Long = "8", Descripcion = "Fecha formal en la que se autorizó o aprobó la solicitud/registro.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "FechaAprobacion", Tipo = "datetime", Long = "8", Descripcion = "Fecha formal en la que se autorizó o aprobó la solicitud/registro.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "FechaNotificada", Tipo = "datetime", Long = "8", Descripcion = "Fecha de calendario en la que se emitió formalmente la notificación al destinatario.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "HoraNotificada", Tipo = "time", Long = "5", Descripcion = "Hora exacta en la que se despachó la notificación correspondiente.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "NombreReq", Tipo = "nvarchar", Long = "200", Descripcion = "Título o denominación breve que identifica el requerimiento registrado.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "DescripcionBreve", Tipo = "nvarchar", Long = "500", Descripcion = "Resumen textual explicativo que detalla el alcance y características del requerimiento.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Habil", Tipo = "bit", Long = "1", Descripcion = "Indicador lógico del estado de la entidad. 1 = Habilitado / Activo, 0 = Inactivo / Deshabilitado.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Activo", Tipo = "bit", Long = "1", Descripcion = "Indicador lógico del estado de la entidad. 1 = Habilitado / Activo, 0 = Inactivo / Deshabilitado.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Fecha", Tipo = "datetime", Long = "8", Descripcion = "Fecha de registro cronológico del evento o transacción en el sistema.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Estado", Tipo = "varchar", Long = "20", Descripcion = "Código o indicador del estado actual de la entidad en su ciclo de vida operativo.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "ModifiedDate", Tipo = "datetime", Long = "8", Descripcion = "Fecha y hora en que el registro fue actualizado por última vez en la base de datos.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "rowguid", Tipo = "uniqueidentifier", Long = "16", Descripcion = "Identificador único global (GUID) utilizado para replicación y sincronización de datos.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Observaciones", Tipo = "nvarchar", Long = "MAX", Descripcion = "Comentarios, notas técnicas o detalles aclaratorios adicionales relacionados al registro.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Descripcion", Tipo = "nvarchar", Long = "MAX", Descripcion = "Resumen textual explicativo que detalla el alcance y características del requerimiento o registro.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Nombre", Tipo = "nvarchar", Long = "150", Descripcion = "Título o denominación breve que identifica el elemento o registro.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Codigo", Tipo = "varchar", Long = "50", Descripcion = "Código alfanumérico o identificador oficial para la clasificación, consulta y validación del registro.", EsReglaFija = true }
        };

        /// <summary>
        /// Verifica la existencia de dbo.Diccionario_Glosario_Maestro con las columnas requeridas (Tabla, Campo, Tipo, Long, Descripcion).
        /// Si existe una versión antigua, la migra o la recrea.
        /// </summary>
        public async Task<bool> EnsureGlossaryTableExistsAsync(ConnectionModel model, string databaseName)
        {
            var connStr = model.BuildConnectionString(databaseName);
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            const string ddl = @"
            SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

            -- Migración si existe la tabla con el esquema antiguo (PatronCampo)
            IF OBJECT_ID('dbo.Diccionario_Glosario_Maestro', 'U') IS NOT NULL
            BEGIN
                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Diccionario_Glosario_Maestro') AND name = 'PatronCampo')
                BEGIN
                    DROP TABLE dbo.Diccionario_Glosario_Maestro;
                END
            END;

            IF OBJECT_ID('dbo.Diccionario_Glosario_Maestro', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Diccionario_Glosario_Maestro (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Esquema NVARCHAR(128) NOT NULL DEFAULT 'dbo',
                    Tabla NVARCHAR(128) NOT NULL DEFAULT 'General',
                    Campo NVARCHAR(128) NOT NULL,
                    Tipo NVARCHAR(128) NOT NULL DEFAULT '',
                    Long VARCHAR(50) NOT NULL DEFAULT '',
                    Descripcion NVARCHAR(MAX) NOT NULL,
                    EsReglaFija BIT NOT NULL DEFAULT 0,
                    FechaCreacion DATETIME NOT NULL DEFAULT GETDATE()
                );
                CREATE INDEX IX_Glosario_Busqueda ON dbo.Diccionario_Glosario_Maestro(Esquema, Tabla, Campo);

                -- 1. Insertar reglas base iniciales
                INSERT INTO dbo.Diccionario_Glosario_Maestro (Esquema, Tabla, Campo, Tipo, Long, Descripcion, EsReglaFija)
                VALUES 
                    ('dbo', 'General', 'Unico', 'int', '4', 'Identificador único del registro. Su valor se genera automáticamente de forma incremental para garantizar la unicidad y el control de la secuencia de los registros.', 1),
                    ('dbo', 'General', 'Id', 'int', '4', 'Identificador único del registro. Su valor se genera automáticamente de forma incremental para garantizar la unicidad y el control de la secuencia de los registros.', 1),
                    ('dbo', 'General', 'CreadoPor', 'nvarchar', '100', 'Nombre de usuario o cuenta de sistema responsable de registrar la transacción original en la base de datos.', 1),
                    ('dbo', 'General', 'CreadaPor', 'nvarchar', '100', 'Nombre de usuario o cuenta de sistema responsable de registrar la transacción original en la base de datos.', 1),
                    ('dbo', 'General', 'Autor', 'nvarchar', '100', 'Nombre de usuario o cuenta de sistema responsable de registrar la transacción original en la base de datos.', 1),
                    ('dbo', 'General', 'ModificadoPor', 'nvarchar', '100', 'Último usuario o servicio responsable de modificar el registro.', 1),
                    ('dbo', 'General', 'ActualizadoPor', 'nvarchar', '100', 'Último usuario o servicio responsable de modificar el registro.', 1),
                    ('dbo', 'General', 'FechaAutorizacion', 'datetime', '8', 'Fecha formal en la que se autorizó o aprobó la solicitud/registro.', 1),
                    ('dbo', 'General', 'FechaAprobacion', 'datetime', '8', 'Fecha formal en la que se autorizó o aprobó la solicitud/registro.', 1),
                    ('dbo', 'General', 'FechaNotificada', 'datetime', '8', 'Fecha de calendario en la que se emitió formalmente la notificación al destinatario.', 1),
                    ('dbo', 'General', 'HoraNotificada', 'time', '5', 'Hora exacta en la que se despachó la notificación correspondiente.', 1),
                    ('dbo', 'General', 'NombreReq', 'nvarchar', '200', 'Título o denominación breve que identifica el requerimiento registrado.', 1),
                    ('dbo', 'General', 'DescripcionBreve', 'nvarchar', '500', 'Resumen textual explicativo que detalla el alcance y características del requerimiento.', 1),
                    ('dbo', 'General', 'Habil', 'bit', '1', 'Indicador lógico del estado de la entidad. 1 = Habilitado / Activo, 0 = Inactivo / Deshabilitado.', 1),
                    ('dbo', 'General', 'Activo', 'bit', '1', 'Indicador lógico del estado de la entidad. 1 = Habilitado / Activo, 0 = Inactivo / Deshabilitado.', 1),
                    ('dbo', 'General', 'Fecha', 'datetime', '8', 'Fecha de registro cronológico del evento o transacción en el sistema.', 1),
                    ('dbo', 'General', 'Estado', 'varchar', '20', 'Código o indicador del estado actual de la entidad en su ciclo de vida operativo.', 1),
                    ('dbo', 'General', 'ModifiedDate', 'datetime', '8', 'Fecha y hora en que el registro fue actualizado por última vez en la base de datos.', 1),
                    ('dbo', 'General', 'rowguid', 'uniqueidentifier', '16', 'Identificador único global (GUID) utilizado para replicación y sincronización de datos.', 1),
                    ('dbo', 'General', 'Observaciones', 'nvarchar', 'MAX', 'Comentarios, notas técnicas o detalles aclaratorios adicionales relacionados al registro.', 1),
                    ('dbo', 'General', 'Descripcion', 'nvarchar', 'MAX', 'Resumen textual explicativo que detalla el alcance y características del requerimiento o registro.', 1),
                    ('dbo', 'General', 'Nombre', 'nvarchar', '150', 'Título o denominación breve que identifica el elemento o registro.', 1),
                    ('dbo', 'General', 'Codigo', 'varchar', '50', 'Código alfanumérico o identificador oficial para la clasificación, consulta y validación del registro.', 1);

                -- 2. Poblar adicionalmente con las columnas ya documentadas en la base de datos
                INSERT INTO dbo.Diccionario_Glosario_Maestro (Esquema, Tabla, Campo, Tipo, Long, Descripcion, EsReglaFija)
                SELECT 
                    s.name AS Esquema,
                    t.name AS Tabla,
                    c.name AS Campo,
                    tp.name AS Tipo,
                    CASE 
                        WHEN tp.name IN ('nchar', 'nvarchar') AND c.max_length = -1 THEN 'MAX'
                        WHEN tp.name IN ('nchar', 'nvarchar') THEN CAST(c.max_length / 2 AS VARCHAR(10))
                        WHEN tp.name IN ('char', 'varchar') AND c.max_length = -1 THEN 'MAX'
                        WHEN tp.name IN ('char', 'varchar') THEN CAST(c.max_length AS VARCHAR(10))
                        ELSE CAST(c.max_length AS VARCHAR(10))
                    END AS Long,
                    CAST(ep.value AS NVARCHAR(MAX)) AS Descripcion,
                    0 AS EsReglaFija
                FROM sys.tables t
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                INNER JOIN sys.columns c ON t.object_id = c.object_id
                INNER JOIN sys.types tp ON c.user_type_id = tp.user_type_id
                INNER JOIN sys.extended_properties ep ON ep.major_id = t.object_id AND ep.minor_id = c.column_id AND ep.name = 'MS_Description' AND ep.class = 1
                WHERE t.is_ms_shipped = 0 AND t.name NOT LIKE 'Diccionario_%'
                  AND CAST(ep.value AS NVARCHAR(MAX)) <> '';
            END;";

            using var cmd = new SqlCommand(ddl, conn) { CommandTimeout = DefaultTimeoutSeconds };
            await cmd.ExecuteNonQueryAsync();
            return true;
        }

        /// <summary>
        /// Obtiene los elementos del glosario maestro desde la BD incluyendo Tabla, Campo, Tipo, Long y Descripción.
        /// </summary>
        public async Task<List<GlossaryItem>> GetGlossaryItemsAsync(ConnectionModel model, string databaseName)
        {
            try
            {
                var connStr = model.BuildConnectionString(databaseName);
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                await EnsureGlossaryTableExistsAsync(model, databaseName);

                const string selectSql = @"
                SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
                SELECT Id, Esquema, Tabla, Campo, Tipo, Long, Descripcion, EsReglaFija, FechaCreacion
                FROM dbo.Diccionario_Glosario_Maestro
                ORDER BY Tabla ASC, Campo ASC;";

                using var cmdSelect = new SqlCommand(selectSql, conn) { CommandTimeout = DefaultTimeoutSeconds };
                using var reader = await cmdSelect.ExecuteReaderAsync();

                var list = new List<GlossaryItem>();
                while (await reader.ReadAsync())
                {
                    list.Add(new GlossaryItem
                    {
                        Id = Convert.ToInt32(reader["Id"]),
                        Esquema = reader["Esquema"].ToString()!,
                        Tabla = reader["Tabla"].ToString()!,
                        Campo = reader["Campo"].ToString()!,
                        Tipo = reader["Tipo"].ToString()!,
                        Long = reader["Long"].ToString()!,
                        Descripcion = reader["Descripcion"].ToString()!,
                        EsReglaFija = Convert.ToBoolean(reader["EsReglaFija"]),
                        FechaCreacion = Convert.ToDateTime(reader["FechaCreacion"])
                    });
                }

                return list.Count > 0 ? list : DefaultGlossaryItems.ToList();
            }
            catch
            {
                // Fallback a los patrones predeterminados si hay restricciones de permisos DDL
                return DefaultGlossaryItems.ToList();
            }
        }

        /// <summary>
        /// Agrega o actualiza un término en el Glosario Maestro con Esquema, Tabla, Campo, Tipo, Long y Descripción.
        /// </summary>
        public async Task<bool> UpsertGlossaryItemAsync(ConnectionModel model, string databaseName, GlossaryItem item)
        {
            var connStr = model.BuildConnectionString(databaseName);
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            await EnsureGlossaryTableExistsAsync(model, databaseName);

            const string sql = @"
            IF EXISTS (SELECT 1 FROM dbo.Diccionario_Glosario_Maestro WHERE Esquema = @Esquema AND Tabla = @Tabla AND Campo = @Campo)
            BEGIN
                UPDATE dbo.Diccionario_Glosario_Maestro 
                SET Tipo = @Tipo, Long = @Long, Descripcion = @Descripcion, EsReglaFija = @EsFija
                WHERE Esquema = @Esquema AND Tabla = @Tabla AND Campo = @Campo;
            END
            ELSE
            BEGIN
                INSERT INTO dbo.Diccionario_Glosario_Maestro (Esquema, Tabla, Campo, Tipo, Long, Descripcion, EsReglaFija)
                VALUES (@Esquema, @Tabla, @Campo, @Tipo, @Long, @Descripcion, @EsFija);
            END;";

            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = DefaultTimeoutSeconds };
            cmd.Parameters.AddWithValue("@Esquema", string.IsNullOrWhiteSpace(item.Esquema) ? "dbo" : item.Esquema.Trim());
            cmd.Parameters.AddWithValue("@Tabla", string.IsNullOrWhiteSpace(item.Tabla) ? "General" : item.Tabla.Trim());
            cmd.Parameters.AddWithValue("@Campo", item.Campo.Trim());
            cmd.Parameters.AddWithValue("@Tipo", string.IsNullOrWhiteSpace(item.Tipo) ? "" : item.Tipo.Trim());
            cmd.Parameters.AddWithValue("@Long", string.IsNullOrWhiteSpace(item.Long) ? "" : item.Long.Trim());
            cmd.Parameters.AddWithValue("@Descripcion", item.Descripcion.Trim());
            cmd.Parameters.AddWithValue("@EsFija", item.EsReglaFija);

            await cmd.ExecuteNonQueryAsync();
            return true;
        }

        /// <summary>
        /// Sincroniza y vuelca todas las columnas documentadas en la base de datos hacia el Glosario Maestro.
        /// </summary>
        public async Task<int> SyncCatalogToGlossaryAsync(ConnectionModel model, string databaseName)
        {
            await EnsureGlossaryTableExistsAsync(model, databaseName);

            var connStr = model.BuildConnectionString(databaseName);
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            const string syncSql = @"
            SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

            MERGE dbo.Diccionario_Glosario_Maestro AS target
            USING (
                SELECT 
                    s.name AS Esquema,
                    t.name AS Tabla,
                    c.name AS Campo,
                    tp.name AS Tipo,
                    CASE 
                        WHEN tp.name IN ('nchar', 'nvarchar') AND c.max_length = -1 THEN 'MAX'
                        WHEN tp.name IN ('nchar', 'nvarchar') THEN CAST(c.max_length / 2 AS VARCHAR(10))
                        WHEN tp.name IN ('char', 'varchar') AND c.max_length = -1 THEN 'MAX'
                        WHEN tp.name IN ('char', 'varchar') THEN CAST(c.max_length AS VARCHAR(10))
                        ELSE CAST(c.max_length AS VARCHAR(10))
                    END AS Long,
                    CAST(ep.value AS NVARCHAR(MAX)) AS Descripcion
                FROM sys.tables t
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                INNER JOIN sys.columns c ON t.object_id = c.object_id
                INNER JOIN sys.types tp ON c.user_type_id = tp.user_type_id
                INNER JOIN sys.extended_properties ep ON ep.major_id = t.object_id AND ep.minor_id = c.column_id AND ep.name = 'MS_Description' AND ep.class = 1
                WHERE t.is_ms_shipped = 0 AND t.name NOT LIKE 'Diccionario_%'
                  AND CAST(ep.value AS NVARCHAR(MAX)) <> ''
            ) AS source
            ON (target.Esquema = source.Esquema AND target.Tabla = source.Tabla AND target.Campo = source.Campo)
            WHEN MATCHED THEN
                UPDATE SET target.Tipo = source.Tipo, target.Long = source.Long, target.Descripcion = source.Descripcion
            WHEN NOT MATCHED THEN
                INSERT (Esquema, Tabla, Campo, Tipo, Long, Descripcion, EsReglaFija, FechaCreacion)
                VALUES (source.Esquema, source.Tabla, source.Campo, source.Tipo, source.Long, source.Descripcion, 0, GETDATE());
            ";

            using var cmd = new SqlCommand(syncSql, conn) { CommandTimeout = DefaultTimeoutSeconds };
            return await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Expresión regular compilada para segmentación de palabras en CamelCase o Snake_case.
        /// </summary>
        private static readonly Regex WordSplitRegex = new Regex(@"([A-Z]+[a-z0-9]*|[a-z0-9]+)", RegexOptions.Compiled);

        /// <summary>
        /// Divide un identificador (en CamelCase, PascalCase, snake_case o kebab-case) en palabras individuales.
        /// </summary>
        public static List<string> SplitWords(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier)) return new List<string>();

            string cleaned = identifier.Replace("_", " ").Replace("-", " ");
            var matches = WordSplitRegex.Matches(cleaned);
            var list = new List<string>();
            foreach (Match m in matches)
            {
                if (!string.IsNullOrWhiteSpace(m.Value))
                    list.Add(m.Value);
            }
            return list;
        }

        private static string ExtractReferencedEntity(string fkRef)
        {
            if (string.IsNullOrWhiteSpace(fkRef)) return string.Empty;
            int parenIdx = fkRef.IndexOf('(');
            string target = parenIdx > 0 ? fkRef.Substring(0, parenIdx).Trim() : fkRef.Trim();
            return target;
        }

        /// <summary>
        /// Ejecuta el motor inteligente de sugerencia y autocompletado de metadatos con jerarquía estricta:
        /// 1. Clave Primaria ([PK]): Garantiza unicidad e incrementalidad, evitando falsos positivos de foráneas.
        /// 2. Clave Foránea ([FK]): Por metadatos explícitos o convención Id (vinculando a la entidad padre).
        /// 3. Glosario Maestro: Coincidencia exacta con dbo.Diccionario_Glosario_Maestro.
        /// 4. Reutilización Semántica: Propagación de descripciones existentes desde otras tablas.
        /// 5. Glosario Maestro por sufijo: Reglas configurables no fijas.
        /// 6. Glosario Semántico y Descomposición del Nombre: Auditoría, fechas/horas específicas, booleanos, cantidades y nombres.
        /// </summary>
        public void EnrichWithSuggestions(DatabaseDictionary dictionary, List<GlossaryItem> glossary)
        {
            // 1. Construir catálogo semántico de campos ya documentados en cualquier tabla de la BD
            var semanticDictionary = new Dictionary<string, (string Description, string TableName)>(StringComparer.OrdinalIgnoreCase);

            foreach (var schema in dictionary.Schemas)
            {
                foreach (var table in schema.Tables)
                {
                    foreach (var col in table.Columns)
                    {
                        bool isDocumented = !string.IsNullOrWhiteSpace(col.BaseDescription) 
                                            && !col.BaseDescription.Equals("Sin descripción disponible", StringComparison.OrdinalIgnoreCase)
                                            && !col.BaseDescription.StartsWith("Dato correspondiente al campo", StringComparison.OrdinalIgnoreCase);

                        if (isDocumented)
                        {
                            if (!semanticDictionary.ContainsKey(col.Name))
                            {
                                semanticDictionary[col.Name] = (col.BaseDescription.Trim(), table.FullName);
                            }
                        }
                    }
                }
            }

            // 2. Mapear glosario a diccionarios (por tabla específica y por nombre de campo general)
            var glossaryTableFieldMap = glossary
                .Where(g => !string.IsNullOrWhiteSpace(g.Tabla) && !g.Tabla.Equals("General", StringComparison.OrdinalIgnoreCase))
                .GroupBy(g => $"{g.Tabla}.{g.Campo}", StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Descripcion, StringComparer.OrdinalIgnoreCase);

            var glossaryExactMap = glossary
                .GroupBy(g => g.Campo, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Descripcion, StringComparer.OrdinalIgnoreCase);

            // 3. Procesar tablas y columnas
            foreach (var schema in dictionary.Schemas)
            {
                foreach (var table in schema.Tables)
                {
                    // Evaluar descripción de la tabla
                    table.HasExistingDescription = !string.IsNullOrWhiteSpace(table.Description)
                                                   && !table.Description.Equals("Sin descripción disponible", StringComparison.OrdinalIgnoreCase)
                                                   && !table.Description.StartsWith("Dato correspondiente a la tabla", StringComparison.OrdinalIgnoreCase);

                    if (table.HasExistingDescription)
                    {
                        table.EditableDescription = table.Description.Trim();
                    }
                    else
                    {
                        table.SuggestedDescription = $"Entidad del esquema '{table.SchemaName}' destinada a almacenar y gestionar los registros correspondientes a '{table.TableName}'.";
                        table.EditableDescription = table.SuggestedDescription;
                    }

                    // Evaluar cada columna
                    foreach (var col in table.Columns)
                    {
                        // Si ya está documentada en SQL Server, respetar la descripción existente
                        col.HasExistingDescription = !string.IsNullOrWhiteSpace(col.BaseDescription)
                                                     && !col.BaseDescription.Equals("Sin descripción disponible", StringComparison.OrdinalIgnoreCase)
                                                     && !col.BaseDescription.StartsWith("Dato correspondiente al campo", StringComparison.OrdinalIgnoreCase);

                        if (col.HasExistingDescription)
                        {
                            col.EditableDescription = col.BaseDescription.Trim();
                            col.SuggestionSource = SuggestionSourceType.ExistingInDatabase;
                            continue;
                        }

                        // =========================================================================
                        // REGLA 1: Clave Primaria ([PK]) - PREVIENE FALSOS POSITIVOS
                        // =========================================================================
                        if (col.IsPrimaryKey)
                        {
                            const string pkDescription = "Identificador único del registro. Su valor se genera automáticamente de forma incremental para garantizar la unicidad y el control de la secuencia de los registros.";
                            col.SuggestedDescription = pkDescription;
                            col.EditableDescription = pkDescription;
                            col.SuggestionSource = SuggestionSourceType.PatternInference;
                            continue;
                        }

                        // =========================================================================
                        // REGLA 2: Clave Foránea ([FK]) detectada por metadatos relacionales
                        // =========================================================================
                        if (!string.IsNullOrWhiteSpace(col.ForeignKeyReference))
                        {
                            string refEntity = ExtractReferencedEntity(col.ForeignKeyReference);
                            string fkDescription = string.IsNullOrWhiteSpace(refEntity)
                                ? "Identificador relacional que vincula el registro actual con la entidad correspondiente para mantener la integridad referencial."
                                : $"Identificador relacional que vincula el registro actual con la entidad correspondiente [{refEntity}] para mantener la integridad referencial.";

                            col.SuggestedDescription = fkDescription;
                            col.EditableDescription = fkDescription;
                            col.SuggestionSource = SuggestionSourceType.ForeignKeyReference;
                            continue;
                        }

                        // =========================================================================
                        // REGLA 3: Glosario Maestro (coincidencia específica por tabla o general)
                        // =========================================================================
                        if (glossaryTableFieldMap.TryGetValue($"{table.TableName}.{col.Name}", out var glosDesc)
                            || glossaryExactMap.TryGetValue(col.Name, out glosDesc))
                        {
                            col.SuggestedDescription = glosDesc;
                            col.EditableDescription = glosDesc;
                            col.SuggestionSource = SuggestionSourceType.MasterGlossary;
                            continue;
                        }

                        // =========================================================================
                        // REGLA 4: Reutilización Semántica (misma columna en otra tabla documentada)
                        // =========================================================================
                        if (semanticDictionary.TryGetValue(col.Name, out var semInfo))
                        {
                            col.SuggestedDescription = semInfo.Description;
                            col.EditableDescription = semInfo.Description;
                            col.SemanticSourceTable = semInfo.TableName;
                            col.SuggestionSource = SuggestionSourceType.SemanticReuse;
                            continue;
                        }

                        // =========================================================================
                        // REGLA 5: Clave Foránea por convención (termina en Id / _Id y NO es PK)
                        // =========================================================================
                        if (col.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase) || col.Name.EndsWith("_Id", StringComparison.OrdinalIgnoreCase))
                        {
                            string entityName = col.Name.Substring(0, col.Name.Length - (col.Name.EndsWith("_Id", StringComparison.OrdinalIgnoreCase) ? 3 : 2));
                            string fkDescription = string.IsNullOrWhiteSpace(entityName)
                                ? "Identificador relacional que vincula el registro actual con la entidad correspondiente para mantener la integridad referencial."
                                : $"Identificador relacional que vincula el registro actual con la entidad correspondiente [{entityName}] para mantener la integridad referencial.";

                            col.SuggestedDescription = fkDescription;
                            col.EditableDescription = fkDescription;
                            col.SuggestionSource = SuggestionSourceType.ForeignKeyReference;
                            continue;
                        }

                        // =========================================================================
                        // REGLA 6: Coincidencia por sufijo común en Glosario Maestro
                        // =========================================================================
                        var suffixMatch = glossary.FirstOrDefault(g => 
                            !g.EsReglaFija && col.Name.EndsWith(g.PatronCampo, StringComparison.OrdinalIgnoreCase));
                        if (suffixMatch != null)
                        {
                            col.SuggestedDescription = suffixMatch.DescripcionSugerida;
                            col.EditableDescription = suffixMatch.DescripcionSugerida;
                            col.SuggestionSource = SuggestionSourceType.MasterGlossary;
                            continue;
                        }

                        // =========================================================================
                        // REGLA 7: Glosario Semántico y Descomposición del Nombre de Campo
                        // =========================================================================
                        string inferred = EvaluateSemanticPattern(col.Name, col.DataType, table.TableName);
                        col.SuggestedDescription = inferred;
                        col.EditableDescription = inferred;
                        col.SuggestionSource = SuggestionSourceType.PatternInference;
                    }
                }
            }
        }

        /// <summary>
        /// Evaluador semántico que descompone el nombre del campo (CamelCase / Snake_case)
        /// y analiza el tipo de dato para generar documentación técnica y de negocio profunda.
        /// </summary>
        public static string EvaluateSemanticPattern(string columnName, string dataType, string? tableName = null)
        {
            if (string.IsNullOrWhiteSpace(columnName)) return "Atributo general del registro.";

            string cleanName = columnName.Trim();
            string lowerName = cleanName.ToLowerInvariant();
            string lowerType = (dataType ?? string.Empty).ToLowerInvariant();

            // Descomposición en palabras individuales
            var words = SplitWords(cleanName);
            var lowerWords = words.Select(w => w.ToLowerInvariant()).ToList();

            // 1. Patrones de Auditoría / Usuarios
            // CreadoPor, CreadaPor, CreatedBy, Creado, Creada, UsuarioCreacion, UserCreated, Autor
            if (lowerName.Contains("creadopor") || lowerName.Contains("creadapor") || lowerName.Contains("createdby") ||
                lowerName.Contains("usuariocreacion") || lowerName.Contains("usercreated") ||
                (lowerWords.Contains("autor") && !lowerName.Contains("fecha") && !lowerName.Contains("date")) ||
                (lowerWords.Contains("creado") && lowerWords.Contains("por")) ||
                (lowerWords.Contains("creada") && lowerWords.Contains("por")) ||
                (lowerWords.Contains("created") && lowerWords.Contains("by")))
            {
                return "Nombre de usuario o cuenta de sistema responsable de registrar la transacción original en la base de datos.";
            }

            // ModificadoPor, ModificadaPor, ActualizadoPor, ActualizadaPor, ModifiedBy, UpdatedBy
            if (lowerName.Contains("modificadopor") || lowerName.Contains("modificadapor") ||
                lowerName.Contains("actualizadopor") || lowerName.Contains("actualizadapor") ||
                lowerName.Contains("modifiedby") || lowerName.Contains("updatedby") ||
                lowerName.Contains("usuariomodificacion") || lowerName.Contains("usermodified") ||
                (lowerWords.Contains("modificado") && lowerWords.Contains("por")) ||
                (lowerWords.Contains("actualizado") && lowerWords.Contains("por")))
            {
                return "Último usuario o servicio responsable de modificar el registro.";
            }

            // 2. Fechas y Horas Específicas
            // FechaAutorizacion / FechaAprobacion
            if (lowerName.Contains("fechaautorizacion") || lowerName.Contains("fechaaprobacion") ||
                lowerName.Contains("approvaldate") || lowerName.Contains("authorizationdate") ||
                (lowerWords.Contains("fecha") && (lowerWords.Contains("autorizacion") || lowerWords.Contains("aprobacion") || lowerWords.Contains("autorizada") || lowerWords.Contains("aprobada"))))
            {
                return "Fecha formal en la que se autorizó o aprobó la solicitud/registro.";
            }

            // FechaNotificada / FechaNotificacion
            if (lowerName.Contains("fechanotificada") || lowerName.Contains("fechanotificacion") || lowerName.Contains("notificationdate") ||
                (lowerWords.Contains("fecha") && (lowerWords.Contains("notificada") || lowerWords.Contains("notificacion"))))
            {
                return "Fecha de calendario en la que se emitió formalmente la notificación al destinatario.";
            }

            // HoraNotificada / HoraNotificacion
            if (lowerName.Contains("horanotificada") || lowerName.Contains("horanotificacion") || lowerName.Contains("notificationtime") ||
                (lowerWords.Contains("hora") && (lowerWords.Contains("notificada") || lowerWords.Contains("notificacion"))))
            {
                return "Hora exacta en la que se despachó la notificación correspondiente.";
            }

            // FechaVencimiento / Expiración
            if (lowerName.Contains("fechavencimiento") || lowerName.Contains("duedate") || lowerName.Contains("expirationdate") || lowerName.Contains("fechaexpiracion") ||
                (lowerWords.Contains("fecha") && (lowerWords.Contains("vencimiento") || lowerWords.Contains("expiracion") || lowerWords.Contains("limite"))))
            {
                return "Fecha límite o de vencimiento fijada para la vigencia del registro o transacción.";
            }

            // FechaNacimiento / BirthDate
            if (lowerName.Contains("fechanacimiento") || lowerName.Contains("birthdate") ||
                (lowerWords.Contains("fecha") && lowerWords.Contains("nacimiento")))
            {
                return "Fecha de nacimiento registrada del individuo o titular.";
            }

            // FechaIngreso / FechaContratación / HireDate
            if (lowerName.Contains("fechaingreso") || lowerName.Contains("hiredate") || lowerName.Contains("fechacontratacion") ||
                (lowerWords.Contains("fecha") && (lowerWords.Contains("ingreso") || lowerWords.Contains("contratacion"))))
            {
                return "Fecha formal de ingreso o contratación en la organización.";
            }

            // FechaEnvio / ShipDate
            if (lowerName.Contains("fechaenvio") || lowerName.Contains("shipdate") || lowerName.Contains("fechadespacho") ||
                (lowerWords.Contains("fecha") && (lowerWords.Contains("envio") || lowerWords.Contains("despacho"))))
            {
                return "Fecha programada o ejecutada del envío o despacho del pedido.";
            }

            // FechaModificacion / ModifiedDate
            if (lowerName.Contains("fechamodificacion") || lowerName.Contains("modifieddate") || lowerName.Contains("fechaactualizacion") || lowerName.Contains("updatedate") ||
                (lowerWords.Contains("fecha") && (lowerWords.Contains("modificacion") || lowerWords.Contains("actualizacion"))))
            {
                return "Fecha y hora en que el registro fue actualizado por última vez en la base de datos.";
            }

            // FechaCreacion / CreateDate / CreationDate / FechaRegistro
            if (lowerName.Contains("fechacreacion") || lowerName.Contains("createdate") || lowerName.Contains("creationdate") || lowerName.Contains("fecharegistro") ||
                (lowerWords.Contains("fecha") && (lowerWords.Contains("creacion") || lowerWords.Contains("registro"))))
            {
                return "Fecha cronológica en que se registró originalmente la transacción en el sistema.";
            }

            // General Fecha* / *Fecha / *Date / Date*
            if (lowerWords.Contains("fecha") || lowerWords.Contains("date") || lowerName.StartsWith("fecha") || lowerName.EndsWith("fecha") || lowerName.EndsWith("date") || lowerType == "date" || lowerType.StartsWith("datetime"))
            {
                var otherWords = words.Where(w => !w.Equals("fecha", StringComparison.OrdinalIgnoreCase) && 
                                                  !w.Equals("date", StringComparison.OrdinalIgnoreCase) &&
                                                  !w.Equals("fec", StringComparison.OrdinalIgnoreCase)).ToList();
                if (otherWords.Count > 0)
                {
                    string concept = string.Join(" ", otherWords);
                    return $"Fecha de registro cronológico del evento o transacción de {concept} en el sistema.";
                }
                return "Fecha de registro cronológico del evento o transacción en el sistema.";
            }

            // General Hora* / *Hora / *Time / Time*
            if (lowerWords.Contains("hora") || lowerWords.Contains("time") || lowerName.StartsWith("hora") || lowerName.EndsWith("hora") || lowerName.EndsWith("time") || lowerType == "time")
            {
                var otherWords = words.Where(w => !w.Equals("hora", StringComparison.OrdinalIgnoreCase) && 
                                                  !w.Equals("time", StringComparison.OrdinalIgnoreCase)).ToList();
                if (otherWords.Count > 0)
                {
                    string concept = string.Join(" ", otherWords);
                    return $"Hora exacta en la que se ejecutó o despachó la operación de {concept}.";
                }
                return "Hora exacta en la que se ejecutó o notificó la operación correspondiente.";
            }

            // 3. Nombres y Descripciones (ej. NombreReq, DescripcionBreve)
            if (lowerName == "nombrereq" || lowerName == "nomreq" || (lowerWords.Contains("nombre") && (lowerWords.Contains("req") || lowerWords.Contains("requerimiento"))))
            {
                return "Título o denominación breve que identifica el requerimiento registrado.";
            }

            if (lowerName == "descripcionbreve" || lowerName == "descbreve" || (lowerWords.Contains("descripcion") && (lowerWords.Contains("breve") || lowerWords.Contains("corta"))))
            {
                return "Resumen textual explicativo que detalla el alcance y características del requerimiento.";
            }

            // Nombres en general
            if (lowerWords.Contains("nombre") || lowerWords.Contains("nom") || lowerWords.Contains("name") || lowerWords.Contains("title") || lowerWords.Contains("titulo"))
            {
                var otherWords = words.Where(w => !w.Equals("nombre", StringComparison.OrdinalIgnoreCase) &&
                                                  !w.Equals("nom", StringComparison.OrdinalIgnoreCase) &&
                                                  !w.Equals("name", StringComparison.OrdinalIgnoreCase) &&
                                                  !w.Equals("title", StringComparison.OrdinalIgnoreCase) &&
                                                  !w.Equals("titulo", StringComparison.OrdinalIgnoreCase)).ToList();
                if (otherWords.Count > 0)
                {
                    string concept = string.Join(" ", otherWords);
                    return $"Título o denominación breve que identifica {concept} en el sistema.";
                }
                return "Título o denominación breve que identifica el elemento o registro.";
            }

            // Descripciones en general
            if (lowerWords.Contains("descripcion") || lowerWords.Contains("desc") || lowerWords.Contains("description") || lowerWords.Contains("detalle"))
            {
                var otherWords = words.Where(w => !w.Equals("descripcion", StringComparison.OrdinalIgnoreCase) &&
                                                  !w.Equals("desc", StringComparison.OrdinalIgnoreCase) &&
                                                  !w.Equals("description", StringComparison.OrdinalIgnoreCase) &&
                                                  !w.Equals("detalle", StringComparison.OrdinalIgnoreCase)).ToList();
                if (otherWords.Count > 0)
                {
                    string concept = string.Join(" ", otherWords);
                    return $"Resumen textual explicativo que detalla el alcance y características de {concept}.";
                }
                return "Resumen textual explicativo que detalla el alcance y características del requerimiento o registro.";
            }

            // 4. Flags y Estados Booleanos / Bit
            if (lowerType.Contains("bit") || lowerType.Contains("flag") || 
                lowerName.StartsWith("es") || lowerName.StartsWith("tiene") || lowerName.StartsWith("habil") || lowerName.StartsWith("activo") ||
                lowerName.StartsWith("is") || lowerName.StartsWith("has") || lowerWords.Contains("flag"))
            {
                return "Indicador lógico del estado de la entidad. 1 = Habilitado / Activo, 0 = Inactivo / Deshabilitado.";
            }

            // 5. Cantidades y Métricas (ej. Cant*, Total, Monto)
            if (lowerName.StartsWith("cant") || lowerName.StartsWith("qty") || lowerName.StartsWith("num") || lowerWords.Contains("cantidad") || lowerWords.Contains("cant"))
            {
                return "Valor numérico acumulado que cuantifica el total de operaciones o elementos asociados al registro.";
            }

            if (lowerName.Contains("total") || lowerName.Contains("monto") || lowerName.Contains("precio") || lowerName.Contains("subtotal") ||
                lowerName.Contains("costo") || lowerName.Contains("importe") || lowerName.Contains("saldo") || lowerType.Contains("money"))
            {
                return "Valor numérico acumulado que cuantifica el total de operaciones o elementos asociados al registro.";
            }

            // 6. Observaciones y Notas
            if (lowerName.Contains("obs") || lowerName.Contains("observacion") || lowerName.Contains("comentario") || lowerName.Contains("comment") || lowerName.Contains("nota"))
            {
                return "Comentarios, notas técnicas o detalles aclaratorios adicionales relacionados al registro.";
            }

            // 7. Códigos y Clasificadores
            if (lowerName.StartsWith("cod") || lowerName.StartsWith("codigo") || lowerName.StartsWith("code") || lowerName.Contains("numdoc"))
            {
                return "Código alfanumérico o identificador oficial para la clasificación, consulta y validación del registro.";
            }

            // 8. Correos, Teléfonos, Direcciones
            if (lowerName.Contains("email") || lowerName.Contains("correo") || lowerName.Contains("mail"))
            {
                return "Dirección de correo electrónico institucional o de contacto asociada a la entidad.";
            }
            if (lowerName.Contains("telefono") || lowerName.Contains("phone") || lowerName.Contains("celular") || lowerName.Contains("mobile"))
            {
                return "Número telefónico o de contacto registrado para comunicación con la entidad.";
            }
            if (lowerName.Contains("direccion") || lowerName.Contains("address") || lowerName.Contains("domicilio"))
            {
                return "Ubicación física o domicilio de contacto registrado para la entidad.";
            }

            // 9. GUID / Rowguid
            if (lowerName.Contains("guid") || lowerName.Contains("rowguid"))
            {
                return "Identificador único global (GUID) utilizado para replicación y sincronización de datos.";
            }

            // 10. Fallback contextualizado con valor técnico y de negocio
            return $"Atributo técnico y de negocio correspondiente al campo '{columnName}'.";
        }

        /// <summary>
        /// Guarda permanentemente las descripciones en SQL Server ejecutando sp_addextendedproperty / sp_updateextendedproperty.
        /// </summary>
        public async Task<(int TablesUpdated, int ColumnsUpdated)> SaveDescriptionsToDatabaseAsync(
            ConnectionModel model, 
            string databaseName, 
            List<TableMetadata> tables)
        {
            var connStr = model.BuildConnectionString(databaseName);
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            int tablesCount = 0;
            int columnsCount = 0;

            const string procTbl = @"
            BEGIN TRY
                IF EXISTS (
                    SELECT 1 FROM sys.extended_properties 
                    WHERE class = 1
                      AND major_id = OBJECT_ID(QUOTENAME(@Schema) + '.' + QUOTENAME(@Table))
                      AND minor_id = 0
                      AND name = 'MS_Description'
                )
                BEGIN
                    EXEC sys.sp_updateextendedproperty 
                        @name = N'MS_Description', 
                        @value = @Description, 
                        @level0type = N'SCHEMA', @level0name = @Schema, 
                        @level1type = N'TABLE',  @level1name = @Table;
                END
                ELSE
                BEGIN
                    EXEC sys.sp_addextendedproperty 
                        @name = N'MS_Description', 
                        @value = @Description, 
                        @level0type = N'SCHEMA', @level0name = @Schema, 
                        @level1type = N'TABLE',  @level1name = @Table;
                END
            END TRY
            BEGIN CATCH
                IF ERROR_NUMBER() = 15135
                BEGIN
                    EXEC sys.sp_addextendedproperty 
                        @name = N'MS_Description', 
                        @value = @Description, 
                        @level0type = N'SCHEMA', @level0name = @Schema, 
                        @level1type = N'TABLE',  @level1name = @Table;
                END
                ELSE IF ERROR_NUMBER() = 15233
                BEGIN
                    EXEC sys.sp_updateextendedproperty 
                        @name = N'MS_Description', 
                        @value = @Description, 
                        @level0type = N'SCHEMA', @level0name = @Schema, 
                        @level1type = N'TABLE',  @level1name = @Table;
                END
                ELSE
                BEGIN
                    THROW;
                END
            END CATCH;";

            const string procCol = @"
            BEGIN TRY
                IF EXISTS (
                    SELECT 1 FROM sys.extended_properties 
                    WHERE class = 1
                      AND major_id = OBJECT_ID(QUOTENAME(@Schema) + '.' + QUOTENAME(@Table))
                      AND minor_id = COLUMNPROPERTY(OBJECT_ID(QUOTENAME(@Schema) + '.' + QUOTENAME(@Table)), @Column, 'ColumnId')
                      AND name = 'MS_Description'
                )
                BEGIN
                    EXEC sys.sp_updateextendedproperty 
                        @name = N'MS_Description', 
                        @value = @Description, 
                        @level0type = N'SCHEMA', @level0name = @Schema, 
                        @level1type = N'TABLE',  @level1name = @Table, 
                        @level2type = N'COLUMN', @level2name = @Column;
                END
                ELSE
                BEGIN
                    EXEC sys.sp_addextendedproperty 
                        @name = N'MS_Description', 
                        @value = @Description, 
                        @level0type = N'SCHEMA', @level0name = @Schema, 
                        @level1type = N'TABLE',  @level1name = @Table, 
                        @level2type = N'COLUMN', @level2name = @Column;
                END
            END TRY
            BEGIN CATCH
                IF ERROR_NUMBER() = 15135
                BEGIN
                    EXEC sys.sp_addextendedproperty 
                        @name = N'MS_Description', 
                        @value = @Description, 
                        @level0type = N'SCHEMA', @level0name = @Schema, 
                        @level1type = N'TABLE',  @level1name = @Table, 
                        @level2type = N'COLUMN', @level2name = @Column;
                END
                ELSE IF ERROR_NUMBER() = 15233
                BEGIN
                    EXEC sys.sp_updateextendedproperty 
                        @name = N'MS_Description', 
                        @value = @Description, 
                        @level0type = N'SCHEMA', @level0name = @Schema, 
                        @level1type = N'TABLE',  @level1name = @Table, 
                        @level2type = N'COLUMN', @level2name = @Column;
                END
                ELSE
                BEGIN
                    THROW;
                END
            END CATCH;";

            foreach (var table in tables)
            {
                // Guardar descripción de tabla si está presente
                if (!string.IsNullOrWhiteSpace(table.EditableDescription))
                {
                    string desc = table.EditableDescription.Trim();
                    if (desc.Length > 3750) desc = desc.Substring(0, 3750);

                    using var cmdTbl = new SqlCommand(procTbl, conn) { CommandTimeout = DefaultTimeoutSeconds };
                    cmdTbl.Parameters.Add("@Schema", SqlDbType.NVarChar, 128).Value = table.SchemaName;
                    cmdTbl.Parameters.Add("@Table", SqlDbType.NVarChar, 128).Value = table.TableName;
                    cmdTbl.Parameters.Add("@Description", SqlDbType.NVarChar, 4000).Value = desc;
                    await cmdTbl.ExecuteNonQueryAsync();
                    tablesCount++;
                    table.Description = table.EditableDescription;
                    table.HasExistingDescription = true;
                    table.IsDirty = false;
                }

                // Guardar descripciones de columnas
                foreach (var col in table.Columns)
                {
                    if (!string.IsNullOrWhiteSpace(col.EditableDescription))
                    {
                        string desc = col.EditableDescription.Trim();
                        if (desc.Length > 3750) desc = desc.Substring(0, 3750);

                        using var cmdCol = new SqlCommand(procCol, conn) { CommandTimeout = DefaultTimeoutSeconds };
                        cmdCol.Parameters.Add("@Schema", SqlDbType.NVarChar, 128).Value = table.SchemaName;
                        cmdCol.Parameters.Add("@Table", SqlDbType.NVarChar, 128).Value = table.TableName;
                        cmdCol.Parameters.Add("@Column", SqlDbType.NVarChar, 128).Value = col.Name;
                        cmdCol.Parameters.Add("@Description", SqlDbType.NVarChar, 4000).Value = desc;
                        await cmdCol.ExecuteNonQueryAsync();
                        columnsCount++;
                        col.BaseDescription = col.EditableDescription;
                        col.HasExistingDescription = true;
                        col.SuggestionSource = SuggestionSourceType.ExistingInDatabase;
                        col.IsDirty = false;
                    }
                }
            }

            return (tablesCount, columnsCount);
        }
    }
}

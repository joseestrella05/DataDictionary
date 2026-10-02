using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using DataDictionaryWeb.Models;

namespace DataDictionaryWeb.Services
{
    public class SchemaDriftService
    {
        private const int DefaultTimeoutSeconds = 180;

        /// <summary>
        /// Determina si una modificación de longitud representa un riesgo de truncamiento de datos (reducción de tamaño).
        /// </summary>
        public static bool IsLengthTruncationRisk(string? oldLen, string? newLen)
        {
            if (string.IsNullOrWhiteSpace(oldLen) || string.IsNullOrWhiteSpace(newLen)) return false;
            string oldClean = oldLen.Trim().ToUpperInvariant();
            string newClean = newLen.Trim().ToUpperInvariant();

            if (oldClean == newClean) return false;

            // Desde MAX a cualquier longitud finita siempre es riesgo de truncamiento
            if (oldClean == "MAX" && newClean != "MAX") return true;
            if (newClean == "MAX") return false;

            // Comparación de números enteros (ej. 50 a 20)
            if (int.TryParse(oldClean, out int oldInt) && int.TryParse(newClean, out int newInt))
            {
                return newInt < oldInt;
            }

            // Comparación de escalas numéricas con precisión (ej. 18,2 a 10,2)
            if (oldClean.Contains(",") && newClean.Contains(","))
            {
                var oldParts = oldClean.Split(',');
                var newParts = newClean.Split(',');
                if (int.TryParse(oldParts[0], out int oldP) && int.TryParse(newParts[0], out int newP))
                {
                    return newP < oldP;
                }
            }

            return false;
        }

        public async Task<bool> EnsureAuditTablesExistAsync(ConnectionModel model, string databaseName)
        {
            var connStr = model.BuildConnectionString(databaseName);
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            const string sql = @"
            SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

            -- 1. Tabla Principal de Snapshot con Auditoría y Versión
            IF NOT EXISTS (
                SELECT 1 FROM sys.tables t 
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id 
                WHERE s.name = 'dbo' AND t.name = 'Diccionario_Snapshot'
            )
            BEGIN
                CREATE TABLE dbo.Diccionario_Snapshot (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Esquema NVARCHAR(128) NOT NULL,
                    Tabla NVARCHAR(128) NOT NULL,
                    Campo NVARCHAR(128) NOT NULL,
                    TipoDato NVARCHAR(128) NOT NULL,
                    Longitud VARCHAR(50) NOT NULL,
                    Descripcion NVARCHAR(MAX) NULL,
                    FechaSnapshot DATETIME NOT NULL DEFAULT GETDATE(),
                    Activo BIT NOT NULL DEFAULT 1,
                    CreadoPor NVARCHAR(100) NOT NULL DEFAULT 'Sistema',
                    FechaCreacion DATETIME NOT NULL DEFAULT GETDATE(),
                    ModificadoPor NVARCHAR(100) NULL,
                    FechaModificacion DATETIME NULL,
                    VersionActual INT NOT NULL DEFAULT 1
                );
                CREATE INDEX IX_Snapshot_Search ON dbo.Diccionario_Snapshot(Esquema, Tabla, Campo, Activo);
            END
            ELSE
            BEGIN
                -- Migración idempotente de columnas de auditoría en caso de que la tabla ya existiese previamente
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Diccionario_Snapshot') AND name = 'CreadoPor')
                    ALTER TABLE dbo.Diccionario_Snapshot ADD CreadoPor NVARCHAR(100) NOT NULL DEFAULT 'Sistema';

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Diccionario_Snapshot') AND name = 'FechaCreacion')
                    ALTER TABLE dbo.Diccionario_Snapshot ADD FechaCreacion DATETIME NOT NULL DEFAULT GETDATE();

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Diccionario_Snapshot') AND name = 'ModificadoPor')
                    ALTER TABLE dbo.Diccionario_Snapshot ADD ModificadoPor NVARCHAR(100) NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Diccionario_Snapshot') AND name = 'FechaModificacion')
                    ALTER TABLE dbo.Diccionario_Snapshot ADD FechaModificacion DATETIME NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Diccionario_Snapshot') AND name = 'VersionActual')
                    ALTER TABLE dbo.Diccionario_Snapshot ADD VersionActual INT NOT NULL DEFAULT 1;
            END;

            -- 2. Tabla de Campos Deprecados / Eliminados Justificados
            IF NOT EXISTS (
                SELECT 1 FROM sys.tables t 
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id 
                WHERE s.name = 'dbo' AND t.name = 'Diccionario_Campos_Deprecados'
            )
            BEGIN
                CREATE TABLE dbo.Diccionario_Campos_Deprecados (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Esquema NVARCHAR(128) NOT NULL,
                    Tabla NVARCHAR(128) NOT NULL,
                    Campo NVARCHAR(128) NOT NULL,
                    TipoDatoOriginal NVARCHAR(128) NOT NULL,
                    LongitudOriginal VARCHAR(50) NOT NULL,
                    UltimaDescripcion NVARCHAR(MAX) NULL,
                    MotivoEliminacion NVARCHAR(MAX) NOT NULL,
                    UsuarioResponsable VARCHAR(100) NULL,
                    FechaEliminacion DATETIME NOT NULL DEFAULT GETDATE()
                );
                CREATE INDEX IX_Deprecados_Search ON dbo.Diccionario_Campos_Deprecados(Esquema, Tabla, Campo);
            END;

            -- 3. Tabla de Control Histórico de Versiones por Columna
            IF NOT EXISTS (
                SELECT 1 FROM sys.tables t 
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id 
                WHERE s.name = 'dbo' AND t.name = 'Diccionario_Historial_Versiones'
            )
            BEGIN
                CREATE TABLE dbo.Diccionario_Historial_Versiones (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Esquema NVARCHAR(128) NOT NULL,
                    Tabla NVARCHAR(128) NOT NULL,
                    Campo NVARCHAR(128) NOT NULL,
                    PropiedadModificada NVARCHAR(50) NOT NULL,
                    ValorAnterior NVARCHAR(MAX) NULL,
                    ValorNuevo NVARCHAR(MAX) NULL,
                    FechaCambio DATETIME NOT NULL DEFAULT GETDATE(),
                    UsuarioResponsable NVARCHAR(100) NOT NULL,
                    MotivoCambio NVARCHAR(MAX) NOT NULL
                );
                CREATE INDEX IX_Historial_Versiones_Search ON dbo.Diccionario_Historial_Versiones(Esquema, Tabla, Campo);
            END;";

            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = DefaultTimeoutSeconds };
            await cmd.ExecuteNonQueryAsync();
            return true;
        }

        public async Task<(bool Exists, DateTime? LastDate, int TotalColumns)> GetSnapshotInfoAsync(ConnectionModel model, string databaseName)
        {
            try
            {
                var connStr = model.BuildConnectionString(databaseName);
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                const string checkSql = @"
                SELECT COUNT(*) FROM sys.tables t 
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id 
                WHERE s.name = 'dbo' AND t.name = 'Diccionario_Snapshot';";

                using var cmdCheck = new SqlCommand(checkSql, conn);
                if (Convert.ToInt32(await cmdCheck.ExecuteScalarAsync()) == 0)
                {
                    return (false, null, 0);
                }

                const string querySql = @"
                SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
                SELECT COUNT(*) AS Total, MAX(FechaSnapshot) AS UltimaFecha 
                FROM dbo.Diccionario_Snapshot WHERE Activo = 1;";

                using var cmd = new SqlCommand(querySql, conn) { CommandTimeout = DefaultTimeoutSeconds };
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    int total = Convert.ToInt32(reader["Total"]);
                    DateTime? lastDate = reader["UltimaFecha"] == DBNull.Value ? null : Convert.ToDateTime(reader["UltimaFecha"]);
                    return (total > 0, lastDate, total);
                }

                return (false, null, 0);
            }
            catch
            {
                return (false, null, 0);
            }
        }

        public async Task<int> TakeSnapshotAsync(ConnectionModel model, string databaseName, List<TableMetadata> tables, string usuarioResponsable = "Sistema")
        {
            await EnsureAuditTablesExistAsync(model, databaseName);

            var connStr = model.BuildConnectionString(databaseName);
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            // Desactivar snapshots anteriores de las tablas involucradas
            var tableList = string.Join(",", tables.Select(t => $"'{t.SchemaName}.{t.TableName}'"));
            string deactivateSql = $@"
            UPDATE dbo.Diccionario_Snapshot 
            SET Activo = 0 
            WHERE CONCAT(Esquema, '.', Tabla) IN ({tableList});";

            using var cmdDeact = new SqlCommand(deactivateSql, conn) { CommandTimeout = DefaultTimeoutSeconds };
            await cmdDeact.ExecuteNonQueryAsync();

            // Insertar nuevas filas en snapshot con auditoría
            int inserted = 0;
            const string insertSql = @"
            INSERT INTO dbo.Diccionario_Snapshot 
                (Esquema, Tabla, Campo, TipoDato, Longitud, Descripcion, FechaSnapshot, Activo, CreadoPor, FechaCreacion, VersionActual)
            VALUES 
                (@Esquema, @Tabla, @Campo, @TipoDato, @Longitud, @Descripcion, GETDATE(), 1, @CreadoPor, GETDATE(), 1);";

            foreach (var table in tables)
            {
                foreach (var col in table.Columns)
                {
                    using var cmdIns = new SqlCommand(insertSql, conn) { CommandTimeout = DefaultTimeoutSeconds };
                    cmdIns.Parameters.AddWithValue("@Esquema", table.SchemaName);
                    cmdIns.Parameters.AddWithValue("@Tabla", table.TableName);
                    cmdIns.Parameters.AddWithValue("@Campo", col.Name);
                    cmdIns.Parameters.AddWithValue("@TipoDato", col.DataType);
                    cmdIns.Parameters.AddWithValue("@Longitud", col.Length);
                    cmdIns.Parameters.AddWithValue("@Descripcion", (object?)col.EditableDescription ?? DBNull.Value);
                    cmdIns.Parameters.AddWithValue("@CreadoPor", string.IsNullOrWhiteSpace(usuarioResponsable) ? "Sistema" : usuarioResponsable.Trim());
                    await cmdIns.ExecuteNonQueryAsync();
                    inserted++;
                }
            }

            return inserted;
        }

        public async Task<List<SchemaDriftItem>> DetectDriftAsync(ConnectionModel model, string databaseName, List<TableMetadata> currentTables)
        {
            await EnsureAuditTablesExistAsync(model, databaseName);

            var deprecatedList = await GetDeprecatedFieldsAsync(model, databaseName);
            var deprecatedMap = deprecatedList.ToDictionary(
                d => $"{d.Esquema}.{d.Tabla}.{d.Campo}", 
                d => d, 
                StringComparer.OrdinalIgnoreCase
            );

            var snapshotItems = new List<(
                string Esquema, 
                string Tabla, 
                string Campo, 
                string TipoDato, 
                string Longitud, 
                string Descripcion,
                string CreadoPor,
                DateTime FechaCreacion,
                string? ModificadoPor,
                DateTime? FechaModificacion,
                int VersionActual)>();

            var connStr = model.BuildConnectionString(databaseName);
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            // Filtrar snapshot únicamente para las tablas bajo análisis
            var tableFilter = string.Join(",", currentTables.Select(t => $"'{t.SchemaName}.{t.TableName}'"));
            string sqlSnapshot = $@"
            SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
            SELECT Esquema, Tabla, Campo, TipoDato, Longitud, ISNULL(Descripcion, '') AS Descripcion,
                   ISNULL(CreadoPor, 'Sistema') AS CreadoPor, FechaCreacion,
                   ModificadoPor, FechaModificacion, ISNULL(VersionActual, 1) AS VersionActual
            FROM dbo.Diccionario_Snapshot
            WHERE Activo = 1 AND CONCAT(Esquema, '.', Tabla) IN ({tableFilter});";

            using var cmd = new SqlCommand(sqlSnapshot, conn) { CommandTimeout = DefaultTimeoutSeconds };
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                snapshotItems.Add((
                    reader["Esquema"].ToString()!,
                    reader["Tabla"].ToString()!,
                    reader["Campo"].ToString()!,
                    reader["TipoDato"].ToString()!,
                    reader["Longitud"].ToString()!,
                    reader["Descripcion"].ToString()!,
                    reader["CreadoPor"].ToString()!,
                    Convert.ToDateTime(reader["FechaCreacion"]),
                    reader["ModificadoPor"] == DBNull.Value ? null : reader["ModificadoPor"].ToString(),
                    reader["FechaModificacion"] == DBNull.Value ? null : Convert.ToDateTime(reader["FechaModificacion"]),
                    Convert.ToInt32(reader["VersionActual"])
                ));
            }

            var currentFieldMap = new Dictionary<string, ColumnMetadata>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in currentTables)
            {
                foreach (var c in t.Columns)
                {
                    currentFieldMap[$"{t.SchemaName}.{t.TableName}.{c.Name}"] = c;
                }
            }

            var driftList = new List<SchemaDriftItem>();

            // 1. Detectar campos eliminados (en Snapshot pero ya no en las tablas físicas)
            foreach (var snap in snapshotItems)
            {
                string key = $"{snap.Esquema}.{snap.Tabla}.{snap.Campo}";
                if (!currentFieldMap.ContainsKey(key))
                {
                    bool isJustified = deprecatedMap.TryGetValue(key, out var justif);
                    driftList.Add(new SchemaDriftItem
                    {
                        Esquema = snap.Esquema,
                        Tabla = snap.Tabla,
                        Campo = snap.Campo,
                        TipoDato = snap.TipoDato,
                        Longitud = snap.Longitud,
                        UltimaDescripcion = snap.Descripcion,
                        DriftType = SchemaDriftType.DeletedFromDatabase,
                        IsJustified = isJustified,
                        Justification = justif,
                        CreadoPor = snap.CreadoPor,
                        FechaCreacion = snap.FechaCreacion,
                        ModificadoPor = snap.ModificadoPor,
                        FechaModificacion = snap.FechaModificacion,
                        VersionActual = snap.VersionActual
                    });
                }
            }

            // 2. Detectar campos modificados (en Snapshot y en BD física, pero difieren en Tipo de Dato o Longitud)
            var snapshotMap = snapshotItems.ToDictionary(
                s => $"{s.Esquema}.{s.Tabla}.{s.Campo}",
                s => s,
                StringComparer.OrdinalIgnoreCase
            );

            foreach (var t in currentTables)
            {
                foreach (var c in t.Columns)
                {
                    string key = $"{t.SchemaName}.{t.TableName}.{c.Name}";
                    if (snapshotMap.TryGetValue(key, out var snap))
                    {
                        // 1. Evaluar cambio de longitud
                        bool lengthChanged = !string.Equals(c.Length, snap.Longitud, StringComparison.OrdinalIgnoreCase);
                        // 2. Evaluar cambio de tipo de dato
                        bool typeChanged = !string.Equals(c.DataType, snap.TipoDato, StringComparison.OrdinalIgnoreCase);
                        // 3. Evaluar cambio de descripción
                        string currentDesc = (c.BaseDescription ?? "").Trim();
                        string snapDesc = (snap.Descripcion ?? "").Trim();
                        bool descChanged = !string.Equals(currentDesc, snapDesc, StringComparison.OrdinalIgnoreCase);

                        if (lengthChanged || typeChanged || descChanged)
                        {
                            bool isTruncation = lengthChanged && IsLengthTruncationRisk(snap.Longitud, c.Length);

                            var changedProps = new List<string>();
                            if (descChanged) changedProps.Add("Descripción");
                            if (typeChanged) changedProps.Add("Tipo de Dato");
                            if (lengthChanged) changedProps.Add("Longitud");

                            string prop = string.Join(" / ", changedProps);

                            var valAntList = new List<string>();
                            var valNueList = new List<string>();

                            if (descChanged)
                            {
                                valAntList.Add(string.IsNullOrWhiteSpace(snapDesc) ? "(Sin descripción previa)" : snapDesc);
                                valNueList.Add(string.IsNullOrWhiteSpace(currentDesc) ? "(Sin descripción actual)" : currentDesc);
                            }
                            if (typeChanged || lengthChanged)
                            {
                                valAntList.Add($"{snap.TipoDato} ({snap.Longitud})");
                                valNueList.Add($"{c.DataType} ({c.Length})");
                            }

                            string valAnt = string.Join(" | ", valAntList);
                            string valNue = string.Join(" | ", valNueList);

                            driftList.Add(new SchemaDriftItem
                            {
                                Esquema = t.SchemaName,
                                Tabla = t.TableName,
                                Campo = c.Name,
                                TipoDato = c.DataType,
                                Longitud = c.Length,
                                UltimaDescripcion = currentDesc,
                                DriftType = SchemaDriftType.ModifiedInDatabase,
                                PropiedadModificada = prop,
                                ValorAnterior = valAnt,
                                ValorNuevo = valNue,
                                IsTruncationRisk = isTruncation,
                                CreadoPor = snap.CreadoPor,
                                FechaCreacion = snap.FechaCreacion,
                                ModificadoPor = snap.ModificadoPor,
                                FechaModificacion = snap.FechaModificacion,
                                VersionActual = snap.VersionActual
                            });
                        }
                    }
                }
            }

            // 3. Detectar campos nuevos (en tablas físicas pero ausentes en Snapshot)
            var snapshotKeySet = new HashSet<string>(
                snapshotItems.Select(s => $"{s.Esquema}.{s.Tabla}.{s.Campo}"), 
                StringComparer.OrdinalIgnoreCase
            );

            if (snapshotItems.Count > 0)
            {
                foreach (var t in currentTables)
                {
                    foreach (var c in t.Columns)
                    {
                        string key = $"{t.SchemaName}.{t.TableName}.{c.Name}";
                        if (!snapshotKeySet.Contains(key))
                        {
                            driftList.Add(new SchemaDriftItem
                            {
                                Esquema = t.SchemaName,
                                Tabla = t.TableName,
                                Campo = c.Name,
                                TipoDato = c.DataType,
                                Longitud = c.Length,
                                UltimaDescripcion = c.EditableDescription,
                                DriftType = SchemaDriftType.NewInDatabase,
                                IsJustified = false,
                                CreadoPor = "Pendiente Snapshot",
                                FechaCreacion = DateTime.Now,
                                VersionActual = 1
                            });
                        }
                    }
                }
            }

            return driftList;
        }

        /// <summary>
        /// Aprueba y sincroniza una modificación estructural en un campo del snapshot, registrando el cambio en el historial de versiones.
        /// </summary>
        public async Task<bool> ApproveColumnModificationAsync(
            ConnectionModel model, 
            string databaseName, 
            SchemaDriftItem item, 
            string usuarioResponsable, 
            string motivoCambio)
        {
            await EnsureAuditTablesExistAsync(model, databaseName);

            var connStr = model.BuildConnectionString(databaseName);
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();
            using var trans = conn.BeginTransaction();

            try
            {
                // 1. Insertar en dbo.Diccionario_Historial_Versiones
                const string sqlHistory = @"
                INSERT INTO dbo.Diccionario_Historial_Versiones 
                    (Esquema, Tabla, Campo, PropiedadModificada, ValorAnterior, ValorNuevo, FechaCambio, UsuarioResponsable, MotivoCambio)
                VALUES 
                    (@Esquema, @Tabla, @Campo, @PropiedadModificada, @ValorAnterior, @ValorNuevo, GETDATE(), @UsuarioResponsable, @MotivoCambio);";

                using var cmdHist = new SqlCommand(sqlHistory, conn, trans) { CommandTimeout = DefaultTimeoutSeconds };
                cmdHist.Parameters.AddWithValue("@Esquema", item.Esquema);
                cmdHist.Parameters.AddWithValue("@Tabla", item.Tabla);
                cmdHist.Parameters.AddWithValue("@Campo", item.Campo);
                cmdHist.Parameters.AddWithValue("@PropiedadModificada", item.PropiedadModificada);
                cmdHist.Parameters.AddWithValue("@ValorAnterior", (object?)item.ValorAnterior ?? DBNull.Value);
                cmdHist.Parameters.AddWithValue("@ValorNuevo", (object?)item.ValorNuevo ?? DBNull.Value);
                cmdHist.Parameters.AddWithValue("@UsuarioResponsable", string.IsNullOrWhiteSpace(usuarioResponsable) ? "Sistema" : usuarioResponsable.Trim());
                cmdHist.Parameters.AddWithValue("@MotivoCambio", string.IsNullOrWhiteSpace(motivoCambio) ? "Sincronización aprobada por administración" : motivoCambio.Trim());
                await cmdHist.ExecuteNonQueryAsync();

                // 2. Actualizar dbo.Diccionario_Snapshot incrementando VersionActual
                const string sqlUpdateSnapshot = @"
                UPDATE dbo.Diccionario_Snapshot 
                SET TipoDato = @TipoDato,
                    Longitud = @Longitud,
                    Descripcion = @Descripcion,
                    VersionActual = ISNULL(VersionActual, 1) + 1,
                    ModificadoPor = @ModificadoPor,
                    FechaModificacion = GETDATE(),
                    FechaSnapshot = GETDATE()
                WHERE Esquema = @Esquema AND Tabla = @Tabla AND Campo = @Campo AND Activo = 1;";

                using var cmdUpdate = new SqlCommand(sqlUpdateSnapshot, conn, trans) { CommandTimeout = DefaultTimeoutSeconds };
                cmdUpdate.Parameters.AddWithValue("@Esquema", item.Esquema);
                cmdUpdate.Parameters.AddWithValue("@Tabla", item.Tabla);
                cmdUpdate.Parameters.AddWithValue("@Campo", item.Campo);
                cmdUpdate.Parameters.AddWithValue("@TipoDato", item.TipoDato);
                cmdUpdate.Parameters.AddWithValue("@Longitud", item.Longitud);
                cmdUpdate.Parameters.AddWithValue("@Descripcion", (object?)item.UltimaDescripcion ?? DBNull.Value);
                cmdUpdate.Parameters.AddWithValue("@ModificadoPor", string.IsNullOrWhiteSpace(usuarioResponsable) ? "Sistema" : usuarioResponsable.Trim());
                await cmdUpdate.ExecuteNonQueryAsync();

                // 3. Sincronizar también en dbo.Diccionario_Glosario_Maestro si el campo está registrado allí
                const string sqlUpdateGlossary = @"
                IF OBJECT_ID('dbo.Diccionario_Glosario_Maestro', 'U') IS NOT NULL
                BEGIN
                    UPDATE dbo.Diccionario_Glosario_Maestro
                    SET Tipo = @TipoDato,
                        Long = @Longitud,
                        Descripcion = @Descripcion
                    WHERE Esquema = @Esquema AND Tabla = @Tabla AND Campo = @Campo;
                END;";

                using var cmdGlossary = new SqlCommand(sqlUpdateGlossary, conn, trans) { CommandTimeout = DefaultTimeoutSeconds };
                cmdGlossary.Parameters.AddWithValue("@Esquema", item.Esquema);
                cmdGlossary.Parameters.AddWithValue("@Tabla", item.Tabla);
                cmdGlossary.Parameters.AddWithValue("@Campo", item.Campo);
                cmdGlossary.Parameters.AddWithValue("@TipoDato", item.TipoDato);
                cmdGlossary.Parameters.AddWithValue("@Longitud", item.Longitud);
                cmdGlossary.Parameters.AddWithValue("@Descripcion", (object?)item.UltimaDescripcion ?? DBNull.Value);
                await cmdGlossary.ExecuteNonQueryAsync();

                trans.Commit();
                return true;
            }
            catch
            {
                trans.Rollback();
                throw;
            }
        }

        public async Task<bool> JustifyDeletedFieldAsync(ConnectionModel model, string databaseName, DeprecatedFieldItem item)
        {
            await EnsureAuditTablesExistAsync(model, databaseName);

            var connStr = model.BuildConnectionString(databaseName);
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            const string sql = @"
            INSERT INTO dbo.Diccionario_Campos_Deprecados 
                (Esquema, Tabla, Campo, TipoDatoOriginal, LongitudOriginal, UltimaDescripcion, MotivoEliminacion, UsuarioResponsable, FechaEliminacion)
            VALUES 
                (@Esquema, @Tabla, @Campo, @TipoDatoOriginal, @LongitudOriginal, @UltimaDescripcion, @MotivoEliminacion, @UsuarioResponsable, GETDATE());

            UPDATE dbo.Diccionario_Snapshot 
            SET Activo = 0 
            WHERE Esquema = @Esquema AND Tabla = @Tabla AND Campo = @Campo;";

            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = DefaultTimeoutSeconds };
            cmd.Parameters.AddWithValue("@Esquema", item.Esquema);
            cmd.Parameters.AddWithValue("@Tabla", item.Tabla);
            cmd.Parameters.AddWithValue("@Campo", item.Campo);
            cmd.Parameters.AddWithValue("@TipoDatoOriginal", item.TipoDatoOriginal);
            cmd.Parameters.AddWithValue("@LongitudOriginal", item.LongitudOriginal);
            cmd.Parameters.AddWithValue("@UltimaDescripcion", (object?)item.UltimaDescripcion ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@MotivoEliminacion", item.MotivoEliminacion.Trim());
            cmd.Parameters.AddWithValue("@UsuarioResponsable", string.IsNullOrWhiteSpace(item.UsuarioResponsable) ? (object)DBNull.Value : item.UsuarioResponsable.Trim());

            await cmd.ExecuteNonQueryAsync();
            return true;
        }

        public async Task<List<DeprecatedFieldItem>> GetDeprecatedFieldsAsync(ConnectionModel model, string databaseName)
        {
            var list = new List<DeprecatedFieldItem>();

            try
            {
                var connStr = model.BuildConnectionString(databaseName);
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                const string checkSql = @"
                SELECT COUNT(*) FROM sys.tables t 
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id 
                WHERE s.name = 'dbo' AND t.name = 'Diccionario_Campos_Deprecados';";

                using var cmdCheck = new SqlCommand(checkSql, conn);
                if (Convert.ToInt32(await cmdCheck.ExecuteScalarAsync()) == 0)
                {
                    return list;
                }

                const string sql = @"
                SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
                SELECT Id, Esquema, Tabla, Campo, TipoDatoOriginal, LongitudOriginal, 
                       ISNULL(UltimaDescripcion, '') AS UltimaDescripcion, 
                       MotivoEliminacion, ISNULL(UsuarioResponsable, '') AS UsuarioResponsable, FechaEliminacion
                FROM dbo.Diccionario_Campos_Deprecados
                ORDER BY FechaEliminacion DESC;";

                using var cmd = new SqlCommand(sql, conn) { CommandTimeout = DefaultTimeoutSeconds };
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new DeprecatedFieldItem
                    {
                        Id = Convert.ToInt32(reader["Id"]),
                        Esquema = reader["Esquema"].ToString()!,
                        Tabla = reader["Tabla"].ToString()!,
                        Campo = reader["Campo"].ToString()!,
                        TipoDatoOriginal = reader["TipoDatoOriginal"].ToString()!,
                        LongitudOriginal = reader["LongitudOriginal"].ToString()!,
                        UltimaDescripcion = reader["UltimaDescripcion"].ToString()!,
                        MotivoEliminacion = reader["MotivoEliminacion"].ToString()!,
                        UsuarioResponsable = reader["UsuarioResponsable"].ToString()!,
                        FechaEliminacion = Convert.ToDateTime(reader["FechaEliminacion"])
                    });
                }
            }
            catch
            {
                // Silencioso si aún no existe
            }

            return list;
        }

        /// <summary>
        /// Obtiene el historial de versiones para un campo específico de una tabla.
        /// </summary>
        public async Task<List<ColumnVersionHistoryItem>> GetColumnVersionHistoryAsync(
            ConnectionModel model, 
            string databaseName, 
            string esquema, 
            string tabla, 
            string campo)
        {
            var list = new List<ColumnVersionHistoryItem>();

            try
            {
                var connStr = model.BuildConnectionString(databaseName);
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                const string checkSql = @"
                SELECT COUNT(*) FROM sys.tables t 
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id 
                WHERE s.name = 'dbo' AND t.name = 'Diccionario_Historial_Versiones';";

                using var cmdCheck = new SqlCommand(checkSql, conn);
                if (Convert.ToInt32(await cmdCheck.ExecuteScalarAsync()) == 0)
                {
                    return list;
                }

                const string sql = @"
                SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
                SELECT Id, Esquema, Tabla, Campo, PropiedadModificada, 
                       ISNULL(ValorAnterior, '') AS ValorAnterior, 
                       ISNULL(ValorNuevo, '') AS ValorNuevo, 
                       FechaCambio, UsuarioResponsable, MotivoCambio
                FROM dbo.Diccionario_Historial_Versiones
                WHERE Esquema = @Esquema AND Tabla = @Tabla AND Campo = @Campo
                ORDER BY FechaCambio DESC;";

                using var cmd = new SqlCommand(sql, conn) { CommandTimeout = DefaultTimeoutSeconds };
                cmd.Parameters.AddWithValue("@Esquema", esquema);
                cmd.Parameters.AddWithValue("@Tabla", tabla);
                cmd.Parameters.AddWithValue("@Campo", campo);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new ColumnVersionHistoryItem
                    {
                        Id = Convert.ToInt32(reader["Id"]),
                        Esquema = reader["Esquema"].ToString()!,
                        Tabla = reader["Tabla"].ToString()!,
                        Campo = reader["Campo"].ToString()!,
                        PropiedadModificada = reader["PropiedadModificada"].ToString()!,
                        ValorAnterior = reader["ValorAnterior"].ToString()!,
                        ValorNuevo = reader["ValorNuevo"].ToString()!,
                        FechaCambio = Convert.ToDateTime(reader["FechaCambio"]),
                        UsuarioResponsable = reader["UsuarioResponsable"].ToString()!,
                        MotivoCambio = reader["MotivoCambio"].ToString()!
                    });
                }
            }
            catch
            {
                // Silencioso si no existe
            }

            return list;
        }

        /// <summary>
        /// Obtiene todas las modificaciones registradas en el historial de versiones para la base de datos (para el reporte Word).
        /// </summary>
        public async Task<List<ColumnVersionHistoryItem>> GetAllVersionHistoryAsync(ConnectionModel model, string databaseName)
        {
            var list = new List<ColumnVersionHistoryItem>();

            try
            {
                var connStr = model.BuildConnectionString(databaseName);
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                const string checkSql = @"
                SELECT COUNT(*) FROM sys.tables t 
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id 
                WHERE s.name = 'dbo' AND t.name = 'Diccionario_Historial_Versiones';";

                using var cmdCheck = new SqlCommand(checkSql, conn);
                if (Convert.ToInt32(await cmdCheck.ExecuteScalarAsync()) == 0)
                {
                    return list;
                }

                const string sql = @"
                SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
                SELECT Id, Esquema, Tabla, Campo, PropiedadModificada, 
                       ISNULL(ValorAnterior, '') AS ValorAnterior, 
                       ISNULL(ValorNuevo, '') AS ValorNuevo, 
                       FechaCambio, UsuarioResponsable, MotivoCambio
                FROM dbo.Diccionario_Historial_Versiones
                ORDER BY FechaCambio DESC;";

                using var cmd = new SqlCommand(sql, conn) { CommandTimeout = DefaultTimeoutSeconds };
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new ColumnVersionHistoryItem
                    {
                        Id = Convert.ToInt32(reader["Id"]),
                        Esquema = reader["Esquema"].ToString()!,
                        Tabla = reader["Tabla"].ToString()!,
                        Campo = reader["Campo"].ToString()!,
                        PropiedadModificada = reader["PropiedadModificada"].ToString()!,
                        ValorAnterior = reader["ValorAnterior"].ToString()!,
                        ValorNuevo = reader["ValorNuevo"].ToString()!,
                        FechaCambio = Convert.ToDateTime(reader["FechaCambio"]),
                        UsuarioResponsable = reader["UsuarioResponsable"].ToString()!,
                        MotivoCambio = reader["MotivoCambio"].ToString()!
                    });
                }
            }
            catch
            {
                // Silencioso si no existe
            }

            return list;
        }
    }
}

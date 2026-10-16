using System;
using System.Collections.Generic;
using System.Linq;

namespace DataDictionaryWeb.Models
{
    public class ConnectionModel
    {
        public string Server { get; set; } = @"NP26-SOPORTE\SQLEXPRESS";
        public bool UseWindowsAuth { get; set; } = true;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool Encrypt { get; set; } = true;
        public bool TrustServerCertificate { get; set; } = true;
        public int ConnectionTimeout { get; set; } = 30;

        public string CustomConnectionString { get; set; } = string.Empty;
        public bool UseRawConnectionString { get; set; } = false;

        public string BuildConnectionString(string? initialCatalog = null)
        {
            if (UseRawConnectionString && !string.IsNullOrWhiteSpace(CustomConnectionString))
            {
                var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(CustomConnectionString);
                if (!string.IsNullOrWhiteSpace(initialCatalog))
                {
                    builder.InitialCatalog = initialCatalog;
                }
                return builder.ConnectionString;
            }

            var csb = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder
            {
                DataSource = Server,
                IntegratedSecurity = UseWindowsAuth,
                Encrypt = Encrypt,
                TrustServerCertificate = TrustServerCertificate,
                ConnectTimeout = ConnectionTimeout,
                ApplicationName = "DataDictionaryWeb"
            };

            if (!UseWindowsAuth)
            {
                csb.UserID = Username;
                csb.Password = Password;
            }

            if (!string.IsNullOrWhiteSpace(initialCatalog))
            {
                csb.InitialCatalog = initialCatalog;
            }

            return csb.ConnectionString;
        }
    }

    public class DatabaseItem
    {
        public string Name { get; set; } = string.Empty;
        public int DatabaseId { get; set; }
        public DateTime CreateDate { get; set; }
    }

    public class TableSelectionItem
    {
        public string SchemaName { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public string FullName => $"{SchemaName}.{TableName}";
        public int ColumnCount { get; set; }
        public bool HasPrimaryKey { get; set; }
        public bool IsSelected { get; set; } = true;
    }

    public class SchemaGroupSelection
    {
        public string SchemaName { get; set; } = string.Empty;
        public List<TableSelectionItem> Tables { get; set; } = new();

        public bool IsSelected
        {
            get => Tables.Count > 0 && Tables.All(t => t.IsSelected);
            set
            {
                foreach (var table in Tables)
                {
                    table.IsSelected = value;
                }
            }
        }

        public int SelectedCount => Tables.Count(t => t.IsSelected);
    }

    public class CustomizationModel
    {
        public string CompanyName { get; set; } = "Nordestana de Préstamos S. A.";
        public string InstitutionTitle { get; set; } = "Departamento de Tecnología de la Información";
        public string DocumentTitle { get; set; } = "Diccionario de Datos de la Base de Datos";
        public string PrimaryColorHex { get; set; } = "#D9EAD3";
        public byte[]? LogoBytes { get; set; }
        public string? LogoFileName { get; set; }
        public string? LogoContentType { get; set; }
    }

    public class GlossaryItem
    {
        public int Id { get; set; }
        public string Esquema { get; set; } = "dbo";
        public string Tabla { get; set; } = "General";
        public string Campo { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public string Long { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public bool EsReglaFija { get; set; } = true;
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        // Propiedades de conveniencia y compatibilidad con código existente
        public string PatronCampo
        {
            get => Campo;
            set => Campo = value;
        }

        public string DescripcionSugerida
        {
            get => Descripcion;
            set => Descripcion = value;
        }

        public string FullTableName => string.IsNullOrWhiteSpace(Esquema) || Esquema.Equals("dbo", StringComparison.OrdinalIgnoreCase) && Tabla.Equals("General", StringComparison.OrdinalIgnoreCase) 
            ? Tabla 
            : $"{Esquema}.{Tabla}";
    }

    public enum SuggestionSourceType
    {
        ExistingInDatabase,
        MasterGlossary,
        SemanticReuse,
        PatternInference,
        ForeignKeyReference,
        ManualEdit
    }

    public class ColumnMetadata
    {
        public int ColumnId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DisplayName => IsPrimaryKey ? $"[PK] {Name}" : Name;
        public string DataType { get; set; } = string.Empty;
        public string DisplayDataType => IsIdentity ? $"{DataType} (Identity)" : DataType;
        public string Length { get; set; } = string.Empty;
        public bool IsNullable { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsIdentity { get; set; }
        public string? ForeignKeyReference { get; set; }

        public string BaseDescription { get; set; } = string.Empty;
        public string EditableDescription { get; set; } = string.Empty;
        public string? SuggestedDescription { get; set; }
        public SuggestionSourceType SuggestionSource { get; set; } = SuggestionSourceType.ExistingInDatabase;
        public string? SemanticSourceTable { get; set; }
        public bool HasExistingDescription { get; set; }
        public bool IsDirty { get; set; }

        // Metadatos de Auditoría y Versión
        public string CreadoPor { get; set; } = "Sistema";
        public DateTime FechaCreacion { get; set; } = DateTime.Now;
        public string? ModificadoPor { get; set; }
        public DateTime? FechaModificacion { get; set; }
        public int VersionActual { get; set; } = 1;

        public string FullDescription
        {
            get
            {
                var desc = string.IsNullOrWhiteSpace(EditableDescription) ? "Sin descripción disponible" : EditableDescription.Trim();
                if (!string.IsNullOrWhiteSpace(ForeignKeyReference) && !desc.Contains("[Referencia:") && !desc.Contains(ForeignKeyReference) && !desc.Contains("hace referencia"))
                {
                    desc += $" [Referencia: {ForeignKeyReference}]";
                }
                return desc;
            }
        }
    }

    public class TableMetadata
    {
        public string SchemaName { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public string FullName => $"{SchemaName}.{TableName}";
        public string Description { get; set; } = string.Empty;
        public string EditableDescription { get; set; } = string.Empty;
        public string? SuggestedDescription { get; set; }
        public bool HasExistingDescription { get; set; }
        public bool IsDirty { get; set; }
        public List<ColumnMetadata> Columns { get; set; } = new();

        public int UndocumentedColumnsCount => Columns.Count(c => !c.HasExistingDescription);
        public int TotalColumnsCount => Columns.Count;
    }

    public class SchemaMetadata
    {
        public string SchemaName { get; set; } = string.Empty;
        public List<TableMetadata> Tables { get; set; } = new();
    }

    public class ColumnVersionHistoryItem
    {
        public int Id { get; set; }
        public string Esquema { get; set; } = string.Empty;
        public string Tabla { get; set; } = string.Empty;
        public string FullTableName => $"{Esquema}.{Tabla}";
        public string Campo { get; set; } = string.Empty;
        public string PropiedadModificada { get; set; } = string.Empty; // 'Longitud', 'TipoDato', 'Nulabilidad', etc.
        public string ValorAnterior { get; set; } = string.Empty;
        public string ValorNuevo { get; set; } = string.Empty;
        public DateTime FechaCambio { get; set; } = DateTime.Now;
        public string UsuarioResponsable { get; set; } = string.Empty;
        public string MotivoCambio { get; set; } = string.Empty;
    }

    public class DatabaseDictionary
    {
        public string ServerName { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
        public List<SchemaMetadata> Schemas { get; set; } = new();

        public int TotalTables => Schemas.Sum(s => s.Tables.Count);
        public int TotalColumns => Schemas.Sum(s => s.Tables.Sum(t => t.Columns.Count));
        public int TotalUndocumentedColumns => Schemas.Sum(s => s.Tables.Sum(t => t.UndocumentedColumnsCount));

        // Campos deprecados/eliminados formalmente justificados
        public List<DeprecatedFieldItem> DeprecatedFields { get; set; } = new();

        // Historial formal de versiones y cambios estructurales
        public List<ColumnVersionHistoryItem> VersionHistory { get; set; } = new();
    }

    public class DeprecatedFieldItem
    {
        public int Id { get; set; }
        public string Esquema { get; set; } = string.Empty;
        public string Tabla { get; set; } = string.Empty;
        public string FullTableName => $"{Esquema}.{Tabla}";
        public string Campo { get; set; } = string.Empty;
        public string TipoDatoOriginal { get; set; } = string.Empty;
        public string LongitudOriginal { get; set; } = string.Empty;
        public string UltimaDescripcion { get; set; } = string.Empty;
        public string MotivoEliminacion { get; set; } = string.Empty;
        public string UsuarioResponsable { get; set; } = string.Empty;
        public DateTime FechaEliminacion { get; set; } = DateTime.Now;
    }

    public enum SchemaDriftType
    {
        DeletedFromDatabase,
        NewInDatabase,
        ModifiedInDatabase
    }

    public class SchemaDriftItem
    {
        public string Esquema { get; set; } = string.Empty;
        public string Tabla { get; set; } = string.Empty;
        public string FullTableName => $"{Esquema}.{Tabla}";
        public string Campo { get; set; } = string.Empty;
        public string TipoDato { get; set; } = string.Empty;
        public string Longitud { get; set; } = string.Empty;
        public string UltimaDescripcion { get; set; } = string.Empty;
        public SchemaDriftType DriftType { get; set; }
        public bool IsJustified { get; set; }
        public DeprecatedFieldItem? Justification { get; set; }

        // Propiedades de Modificación y Auditoría
        public string PropiedadModificada { get; set; } = string.Empty;
        public string ValorAnterior { get; set; } = string.Empty;
        public string ValorNuevo { get; set; } = string.Empty;
        public bool IsTruncationRisk { get; set; }

        // Metadatos de Auditoría del Snapshot
        public string CreadoPor { get; set; } = "Sistema";
        public DateTime FechaCreacion { get; set; } = DateTime.Now;
        public string? ModificadoPor { get; set; }
        public DateTime? FechaModificacion { get; set; }
        public int VersionActual { get; set; } = 1;
    }

    public class MasterCatalogTableEntry
    {
        public int Index { get; set; }
        public string Schema { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Observaciones { get; set; } = string.Empty;
        public int ColumnCount { get; set; }
        public List<MasterCatalogColumnEntry> Columns { get; set; } = new();
    }

    public class MasterCatalogColumnEntry
    {
        public string Campo { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public string Long { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
    }
}

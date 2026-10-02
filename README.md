# SQL Server Data Dictionary Studio & Governance Suite

Plataforma web integral de arquitectura corporativa construida en **Blazor Server (.NET 10)** y **DocumentFormat.OpenXml SDK** para la inspección, autocompletado inteligente, gobierno de cambios de esquema (campos eliminados/deprecados) y generación de **Diccionarios de Datos profesionales en formato Word (.docx)** estándar ISO/IEC 29500.

---

## 1. Módulos y Arquitectura del Sistema

```text
D:\DocumentacionPrueba\
├── Components/
│   ├── Pages/
│   │   └── Home.razor                    # Interfaz 4 Vistas: Exportador Word, Asistente IA, Auditoría/Deprecados, Glosario
│   ├── Layout/
│   │   ├── MainLayout.razor              # Dashboard corporativo (Single Page) sin barra lateral morada
│   │   └── NavMenu.razor                 # Barra de navegación minimalista
│   ├── App.razor                         # Carga de recursos (Inter font, Bootstrap Icons, Scripts)
│   └── Routes.razor                      # Enrutamiento Blazor
├── Models/
│   └── Models.cs                         # Modelos (DeprecatedFieldItem, SchemaDriftItem, GlossaryItem, ColumnMetadata)
├── Services/
│   ├── DatabaseDiscoveryService.cs       # Descubrimiento de BDs de usuario (READ UNCOMMITTED)
│   ├── MetadataExtractionService.cs      # Extracción Server-Side con tabla temporal indexada #SelectedTables
│   ├── IntelligentMetadataService.cs     # Motor de 3 Prioridades de Sugerencia y Persistencia (sp_extendedproperty)
│   ├── SchemaDriftService.cs             # Snapshots, Detección de Drift y Justificación de Campos Deprecados
│   ├── WordDictionaryService.cs          # Motor OpenXML con sección formal de campos eliminados y streaming a disco
│   └── DocumentDownloadManager.cs        # Gestión de tokens y descarga HTTP asíncrona sin saturar memoria RAM
├── wwwroot/
│   ├── css/ & app.css                    # Estilos ejecutivos modernos (cards 12px, badges, scroll fluido)
│   └── js/
│       └── downloadHelper.js             # Disparador de descargas nativas del navegador
├── Program.cs                            # Pipeline HTTP, inyección de dependencias y endpoint /api/download/{token}
├── DataDictionaryWeb.csproj              # Configuración SDK Web y NuGet (.NET 10)
└── README.md                             # Documentación técnica completa
```

---

## 2. Gobierno de Cambios de Esquema y Campos Deprecados

### A. Persistencia en Base de Datos:
El servicio `SchemaDriftService` asegura la creación de las tablas de control:

1. **`dbo.Diccionario_Snapshot`:**
   Almacena la línea base del esquema capturado en un momento del tiempo:
   ```sql
   CREATE TABLE dbo.Diccionario_Snapshot (
       Id INT IDENTITY(1,1) PRIMARY KEY,
       Esquema NVARCHAR(128) NOT NULL,
       Tabla NVARCHAR(128) NOT NULL,
       Campo NVARCHAR(128) NOT NULL,
       TipoDato NVARCHAR(128) NOT NULL,
       Longitud VARCHAR(50) NOT NULL,
       Descripcion NVARCHAR(MAX) NULL,
       FechaSnapshot DATETIME NOT NULL DEFAULT GETDATE(),
       Activo BIT NOT NULL DEFAULT 1
   );
   ```

2. **`dbo.Diccionario_Campos_Deprecados`:**
   Registra formalmente las justificaciones de eliminaciones:
   ```sql
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
   ```

### B. Detección de Discrepancias (Drift):
Al pulsar *"Detectar Diferencias / Cambios"*, el sistema contrasta el estado físico actual de `sys.columns` con el último snapshot activo:
- 🔴 **`CAMPO ELIMINADO EN BD`:** El campo existía en el snapshot pero ya no está en las tablas físicas. Habilita el botón interactivo **`[Justificar Eliminación]`**.
- 🟢 **`NUEVO CAMPO EN BD`:** Campo agregado a la base de datos tras la toma del snapshot.
- 🛡️ **`Eliminación Justificada`:** Campos eliminados que ya cuentan con justificación formal en `dbo.Diccionario_Campos_Deprecados`.

### C. Ventana Modal Interactiva:
Al hacer clic en **`[Justificar Eliminación]`**, se abre una ventana modal que presenta:
- Datos del campo: `Esquema.Tabla -> Campo (Tipo, Longitud)`.
- Última descripción registrada.
- Campo obligatorio: **Motivo / Razón de la eliminación** (ej. *"Reemplazado por nueva tabla de direcciones secundarias"*).
- Campo opcional: **Ticket de Cambio / Usuario Responsable**.
Al guardar, se inserta en `dbo.Diccionario_Campos_Deprecados` y se desactiva el campo en `dbo.Diccionario_Snapshot (Activo = 0)`.

---

## 3. Inclusión en el Documento Word (.docx)

Al final del Diccionario de Datos se incorpora automáticamente una sección formal:
**"HISTORIAL DE MODIFICACIONES Y CAMPOS DEPRECADOS / ELIMINADOS"**

- **Si existen campos eliminados registrados:** Genera una tabla con 6 columnas:
  `Esquema / Tabla` (20%) | `Campo Eliminado` (18%, resaltado en rojo) | `Tipo Original` (14%) | `Fecha` (12%) | `Motivo de Eliminación` (24%) | `Responsable` (12%).
- **Si no hay campos eliminados:** Muestra el texto institucional sutil:
  *"No se registran campos eliminados o deprecados en este período."*

---

## 4. Instrucciones para Ejecutar la Aplicación

1. Abrir terminal en `D:\DocumentacionPrueba`:
   ```powershell
   dotnet run
   ```
2. Acceder en el navegador a:
   👉 **`http://localhost:5106`**

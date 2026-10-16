using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
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
            // Claves e identificadores principales
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Unico", Tipo = "int", Long = "4", Descripcion = "Identificador único del registro. Su valor se genera automáticamente de forma incremental para garantizar la unicidad y el control de la secuencia de los registros.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Id", Tipo = "int", Long = "4", Descripcion = "Identificador único del registro. Su valor se genera automáticamente de forma incremental para garantizar la unicidad y el control de la secuencia de los registros.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Identificador", Tipo = "varchar", Long = "20", Descripcion = "Clave única que identifica al cliente mediante cédula, pasaporte o RNC.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Cliente_Id", Tipo = "varchar", Long = "20", Descripcion = "Identificador único del cliente o entidad puede ser Cédula, RNC y Pasaporte.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Cedula", Tipo = "varchar", Long = "20", Descripcion = "Número de cédula del cliente.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "RNC", Tipo = "varchar", Long = "20", Descripcion = "Número de Registro Nacional de Contribuyentes (RNC) de la empresa.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Pasaporte", Tipo = "varchar", Long = "20", Descripcion = "Número de pasaporte del cliente.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Licencia_No", Tipo = "varchar", Long = "20", Descripcion = "Número de licencia asociada al cliente.", EsReglaFija = true },

            // Datos personales, nombre y ubicación
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Nombres", Tipo = "varchar", Long = "60", Descripcion = "Nombre de la persona, empresa y cliente.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Apellidos", Tipo = "varchar", Long = "30", Descripcion = "Apellido de la persona, empresa y cliente.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Apodo", Tipo = "varchar", Long = "30", Descripcion = "Apodo de la persona, empresa y cliente.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Sexo", Tipo = "varchar", Long = "1", Descripcion = "Identificador del sexo o género de la persona asociada al registro.\nEste valor hace referencia al campo Unico de la tabla Entidad.tentidad_sexo.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Estado_Civil", Tipo = "varchar", Long = "1", Descripcion = "Identificador del estado civil de la persona asociada al registro.\nEste valor hace referencia al campo Estado_Civil de la tabla Entidad.tentidad_estado_civil.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Direccion", Tipo = "varchar", Long = "100", Descripcion = "Dirección de residencia o ubicación de la persona, empresa y cliente.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "OtraDireccion", Tipo = "varchar", Long = "150", Descripcion = "Dirección adicional registrada para el cliente.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "CodPostal", Tipo = "varchar", Long = "20", Descripcion = "Código postal asociado a la dirección del cliente.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Como_Llegar", Tipo = "varchar", Long = "150", Descripcion = "Referencia o indicaciones para llegar a la ubicación del cliente.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Sector_Id", Tipo = "varchar", Long = "20", Descripcion = "Identificador del sector geográfico asociado a la dirección.\nEste valor hace referencia al campo Unico de la tabla Localidad.tsector.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "OtroSector", Tipo = "varchar", Long = "100", Descripcion = "Nombre del sector de residencia del cliente cuando este no se encuentra disponible en el listado de sectores.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Lugar_Nacimiento", Tipo = "varchar", Long = "20", Descripcion = "Identificador del municipio asociado al registro.\nEste valor hace referencia al campo Identificador de la tabla Localidad.tmunicipio.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "OtroNacimiento", Tipo = "varchar", Long = "100", Descripcion = "Nombre del lugar de nacimiento cuando no se encuentra disponible en el listado de localidades registradas.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Zona_Id", Tipo = "varchar", Long = "20", Descripcion = "Identificador de la zona asociada a la ubicación del cliente.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Nacionalidad_Id", Tipo = "varchar", Long = "5", Descripcion = "Identificador del país asociado al registro.\nEste valor hace referencia al campo IdPais de la tabla Localidad.tpais.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Nacionalidad1_Id", Tipo = "varchar", Long = "5", Descripcion = "Identificador del país asociado al registro.\nEste valor hace referencia al campo IdPais de la tabla Localidad.tpais.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Nacionalidad1_No", Tipo = "varchar", Long = "20", Descripcion = "Número de documento asociado a la nacionalidad secundaria del cliente, para mas información verificar la tabla localidad.tpais.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Nacionalidad1_FeVence", Tipo = "datetime", Long = "8", Descripcion = "Fecha de vencimiento del documento asociado a la nacionalidad secundaria.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Nacionalidad1_FeEmision", Tipo = "datetime", Long = "8", Descripcion = "Fecha de vencimiento del documento asociado a la nacionalidad secundaria.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "NacPasaporte", Tipo = "varchar", Long = "5", Descripcion = "Nacionalidad asociada al pasaporte del cliente.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Residente1_No", Tipo = "varchar", Long = "5", Descripcion = "Código de residencia asociado al cliente.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "ResidentePermanente", Tipo = "bit", Long = "1", Descripcion = "Indica si el cliente posee condición de residente permanente.\n0 = No\n1 = Sí", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "NoResidente", Tipo = "bit", Long = "1", Descripcion = "Indica si el cliente es no residente.\n0 = No\n1 = Sí", EsReglaFija = true },

            // Contacto y Comunicación
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "TTel1", Tipo = "smallint", Long = "2", Descripcion = "Identificador del tipo correspondiente al teléfono principal (marcado con 1 en la estructura de teléfonos).", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Telefono1", Tipo = "varchar", Long = "20", Descripcion = "Número del teléfono principal.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "TTel2", Tipo = "smallint", Long = "2", Descripcion = "Identificador del tipo correspondiente al teléfono secundario (marcado con 2 en la estructura de teléfonos).", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Telefono2", Tipo = "varchar", Long = "20", Descripcion = "Número del teléfono secundario.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "TTel3", Tipo = "smallint", Long = "2", Descripcion = "Identificador del tipo correspondiente al teléfono adicional (marcado con 3 en la estructura de teléfonos).", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Telefono3", Tipo = "varchar", Long = "20", Descripcion = "Número del teléfono adicional.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Correo_E", Tipo = "varchar", Long = "150", Descripcion = "Dirección de correo electrónico del cliente.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Foto", Tipo = "varchar", Long = "100", Descripcion = "Ruta o nombre del archivo de la fotografía del cliente.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "VerCorreo", Tipo = "bit", Long = "1", Descripcion = "Indica si se permite visualizar el correo electrónico del cliente.\n0 = No\n1 = Sí", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "VerTelefono", Tipo = "bit", Long = "1", Descripcion = "Indica si se permite visualizar los números telefónicos del cliente.\n0 = No\n1 = Sí", EsReglaFija = true },

            // Clasificación, Tipología y Negocio
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Tipo", Tipo = "varchar", Long = "2", Descripcion = "Código que identifica el tipo de cliente o entidad,E1 - Empresa Nacional, E2 - Empresa Extranjera, P1 - Persona Física Nacional, P2 - Persona Física Extranjera.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Tipo_Entidad_Id", Tipo = "varchar", Long = "5", Descripcion = "Código que identifica el tipo de entidad del clientela SIB P1, P2, P3, P4, P5.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Tipo_NCF", Tipo = "smallint", Long = "2", Descripcion = "Identificador del tipo de comprobante fiscal utilizado por el cliente,1 = Comprobante Fiscal 2 = Persona Física.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Libro_Id", Tipo = "varchar", Long = "1", Descripcion = "Identificador de la clasificación de libro asignada al cliente.\nEste valor hace referencia al campo Unico de la tabla Entidad.tentidad_Libro.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Riesgo_Id", Tipo = "int", Long = "4", Descripcion = "Identificador del nivel de riesgo crediticio asignado al cliente.\nEste valor hace referencia al campo Unico de la tabla Configuracion.tRiesgo.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Rango", Tipo = "varchar", Long = "50", Descripcion = "Nivel de riesgo asignado al cliente en función de su historial de pagos.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "RiesgoLaFt", Tipo = "numeric", Long = "18,2", Descripcion = "Nivel de riesgo asociado a prevención de lavado de activos y financiamiento del terrorismo.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Actividad_Ciuu", Tipo = "varchar", Long = "10", Descripcion = "Código CIIU correspondiente a la actividad económica del cliente.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Estilo_Vida", Tipo = "varchar", Long = "50", Descripcion = "Clasificación del estilo de vida del cliente.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Jovis", Tipo = "varchar", Long = "60", Descripcion = "Información adicional registrada sobre los hobbies o actividades de interés del cliente.", EsReglaFija = true },

            // Referidos y Medios de Contacto
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Medio_Detalle", Tipo = "varchar", Long = "60", Descripcion = "Nombre o descripción específica del medio por el cual el cliente conoció o se acercó a la institución.- Recomendación de un amigo- Facebook- Instagram- Referido por un cliente-Letrero.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Medio_Referido_Id", Tipo = "int", Long = "4", Descripcion = "Identificador único del medio por el que el cliente conoció la institución, este valor hace referencia al campo único de la tabla Entidad.tentidad_medio_referencia.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Medio_Referencia_Id", Tipo = "varchar", Long = "35", Descripcion = "Descripción del medio por el que el cliente conoció la institución, este valor hace referencia al campo descripción de la tabla Entidad.tentidad_medio_referencia.", EsReglaFija = true },

            // Roles y Banderas Institucionales
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "TEmpleado", Tipo = "bit", Long = "1", Descripcion = "Indica si la persona es empleado de la empresa.\n0 = No\n1 = Sí", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "TAcciónista", Tipo = "bit", Long = "1", Descripcion = "Indica si la persona es accionista de la empresa.\n0 = No\n1 = Sí", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "TAccionista", Tipo = "bit", Long = "1", Descripcion = "Indica si la persona es accionista de la empresa.\n0 = No\n1 = Sí", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "TFuncionario", Tipo = "bit", Long = "1", Descripcion = "Indica si la persona es funcionario de la empresa.\n0 = No\n1 = Sí", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "TCliente", Tipo = "bit", Long = "1", Descripcion = "Indica si la persona es cliente de la empresa.\n0 = No\n1 = Sí", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Habil", Tipo = "bit", Long = "1", Descripcion = "Indica si la entidad se encuentra habilitada para procesar transacciones.\n0 = No\n1 = Sí", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Activo", Tipo = "bit", Long = "1", Descripcion = "Indica si el registro se encuentra activo para procesar transacciones.\n0 = No\n1 = Sí", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "NoFirma", Tipo = "bit", Long = "1", Descripcion = "Indica si el cliente no sabe leer ni escribir.\n0 = No\n1 = Sí", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Sincronizado", Tipo = "bit", Long = "1", Descripcion = "Indica si el registro ha sido sincronizado con la base de datos.\n0 = No\n1 = Sí", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Editar", Tipo = "bit", Long = "1", Descripcion = "Indica si el registro puede ser editado.\n0 = No\n1 = Sí", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "AceptaContrato", Tipo = "bit", Long = "1", Descripcion = "Indica si el cliente ha aceptado los términos del contrato.\n0 = No\n1 = Sí", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "AbonoExtraordinario", Tipo = "bit", Long = "1", Descripcion = "Indica si el cliente realiza o tiene autorizado realizar abonos extraordinarios.\n0 = No\n1 = Sí", EsReglaFija = true },

            // Familia, Dependientes y Vínculos
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Parentesco_Id", Tipo = "smallint", Long = "2", Descripcion = "Identificador del tipo de parentesco o relación familiar asociado al registro.\nEste valor hace referencia al campo Unico de la tabla Entidad.tentidad_parentesco_tipo.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Conyugue", Tipo = "varchar", Long = "20", Descripcion = "Identificador único del cónyuge del cliente mediante cédula, pasaporte o RNC.\nEste valor hace referencia al campo Identificador de la tabla Entidad.tentidad.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Junto_Desde", Tipo = "datetime", Long = "8", Descripcion = "Fecha desde la que el cliente está casado con su cónyuge.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Hijos", Tipo = "smallint", Long = "2", Descripcion = "Cantidad de hijo que tiene el cliente.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Personal_Dependientes", Tipo = "smallint", Long = "2", Descripcion = "Cantidad de persona que depende económicamente del cliente.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "HijosMenores10", Tipo = "bit", Long = "1", Descripcion = "Indica si el cliente tiene hijos menores de 10 años.\n0 = No\n1 = Sí", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "HijosDe11A17", Tipo = "bit", Long = "1", Descripcion = "Indica si el cliente tiene hijos entre 11 y 17 años.\n0 = No\n1 = Sí", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "HijosMayoresA18", Tipo = "bit", Long = "1", Descripcion = "Indica si el cliente tiene hijos mayores de 18 años.\n0 = No\n1 = Sí", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "HijosEstudianEn", Tipo = "varchar", Long = "100", Descripcion = "Centro educativo donde estudian los hijos del cliente.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Vinculado_Id", Tipo = "varchar", Long = "20", Descripcion = "Identificador del tipo de vinculación institucional asociado al registro.\nEste valor hace referencia al campo Unico de la tabla Entidad.tentidad_vinculacion_tipo.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Vinculacion_Id", Tipo = "varchar", Long = "5", Descripcion = "Identificador del tipo de vinculación institucional asociado al registro.\nEste valor hace referencia al campo Unico de la tabla Entidad.tentidad_vinculacion_tipo.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Vinculacion_Cedula", Tipo = "varchar", Long = "30", Descripcion = "Número de identificación de la persona vinculada al cliente.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Vinculacion_Nombres", Tipo = "varchar", Long = "150", Descripcion = "Nombre de la persona vinculada al cliente.", EsReglaFija = true },

            // Créditos, Métricas e Historial Financiero
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Ultimo_Prestamo", Tipo = "int", Long = "4", Descripcion = "Número identificador del último préstamo realizado al cliente.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "CantPrestamosDeudor", Tipo = "int", Long = "4", Descripcion = "Cantidad de préstamos en los que el cliente figura como deudor.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "CantPrestamosDeudorConyugue", Tipo = "int", Long = "4", Descripcion = "Cantidad de préstamos en los que el cliente figura como cónyuge del deudor.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "CantPrestamoGarante", Tipo = "int", Long = "4", Descripcion = "Cantidad de préstamos en los que el cliente figura como garante del deudor.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "CantPrestamoGaranteConyugue", Tipo = "int", Long = "4", Descripcion = "Cantidad de préstamos en los que el cliente figura como cónyuge del garante.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "ConAtraso", Tipo = "int", Long = "4", Descripcion = "Cantidad de pagos realizados con atraso.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "SinAtraso", Tipo = "int", Long = "4", Descripcion = "Cantidad de pagos realizados sin atraso.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "CantidadMovimiento", Tipo = "varchar", Long = "10", Descripcion = "Cantidad estimada de movimientos realizados por el cliente.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "VolumenMovimiento", Tipo = "numeric", Long = "18,2", Descripcion = "Monto estimado de los movimientos realizados por el cliente.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "CantidadUltimos30Dias", Tipo = "numeric", Long = "18,2", Descripcion = "Cantidad de movimientos realizados en los últimos 30 días.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "VolumenUltimos30Dias", Tipo = "numeric", Long = "18,2", Descripcion = "Monto total de los movimientos realizados en los últimos 30 días.", EsReglaFija = true },

            // Banca, Cuentas y Beneficiarios
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Banco_Id", Tipo = "int", Long = "4", Descripcion = "Identificador de la entidad bancaria asociada a la cuenta o transacción.\nEste valor hace referencia al campo Unico de la tabla Banco.tbCuentas_Banco.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Tipo_Cuenta", Tipo = "varchar", Long = "2", Descripcion = "Código que identifica el tipo de cuenta bancaria del cliente.AH = Cuenta de ahorro CC = Cuenta corriente.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Numero_Cuenta_Banco", Tipo = "varchar", Long = "17", Descripcion = "Número de cuenta bancaria asociado al cliente.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Cuenta_Oficial", Tipo = "varchar", Long = "100", Descripcion = "Nombre del titular de la cuenta bancaria registrada.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Beneficiario_Id", Tipo = "varchar", Long = "20", Descripcion = "Identificador del beneficiario asociado al cliente.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Beneficiario_Nombre", Tipo = "varchar", Long = "100", Descripcion = "Nombre del beneficiario asociado al cliente.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "CierreFiscal", Tipo = "int", Long = "4", Descripcion = "Mes de cierre fiscal registrado para el cliente o entidad.", EsReglaFija = true },

            // Funcionarios y PEP (Personas Expuestas Políticamente)
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "esFuncionario", Tipo = "bit", Long = "1", Descripcion = "Indica si el cliente es funcionario.\n0 = No\n1 = Sí", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Fun_GobiernoAno", Tipo = "varchar", Long = "100", Descripcion = "Año de vinculación del funcionario con el gobierno.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Fun_Parentesco", Tipo = "varchar", Long = "2", Descripcion = "Identificador del parentesco con el funcionario.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Fun_Nombre", Tipo = "varchar", Long = "100", Descripcion = "Nombre del funcionario relacionado.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Fun_Cargo", Tipo = "varchar", Long = "150", Descripcion = "Cargo desempeñado por el funcionario.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Fun_Institucion", Tipo = "varchar", Long = "100", Descripcion = "Institución en la que labora o ha laborado el funcionario.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "esFamiliarFuncionario", Tipo = "bit", Long = "1", Descripcion = "Indica si el cliente es familiar de un funcionario público.\n0 = No\n1 = Sí", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "FunP_Nombre", Tipo = "varchar", Long = "100", Descripcion = "Nombre del funcionario público relacionado.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "FunP_Cargo", Tipo = "varchar", Long = "100", Descripcion = "Cargo desempeñado por el funcionario público relacionado.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "FunP_Institucion", Tipo = "varchar", Long = "100", Descripcion = "Institución en la que labora el funcionario público relacionado.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "FunP_Ano", Tipo = "varchar", Long = "100", Descripcion = "Año de vinculación del funcionario público relacionado.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "FunP_Parentesco", Tipo = "smallint", Long = "2", Descripcion = "Identificador del parentesco con el funcionario público relacionado.", EsReglaFija = true },

            // Religión
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "EsMiembroReligion", Tipo = "bit", Long = "1", Descripcion = "Indica si el cliente pertenece a una organización religiosa.\n0 = No\n1 = Sí", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "ReligionCargo", Tipo = "varchar", Long = "100", Descripcion = "Cargo desempeñado por el cliente en la organización religiosa.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "ReligionInstitucion", Tipo = "varchar", Long = "100", Descripcion = "Nombre de la organización religiosa a la que pertenece el cliente.", EsReglaFija = true },

            // FATCA / US Person
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "US_Persona", Tipo = "bit", Long = "1", Descripcion = "Indica si el cliente es considerado una persona estadounidense para fines fiscales y regulatorios.\n0 = No\n1 = Sí", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "US_TIN", Tipo = "varchar", Long = "20", Descripcion = "Número de identificación fiscal del cliente en los Estados Unidos.", EsReglaFija = true },

            // Oficina y Localidad
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Oficina_Id", Tipo = "int", Long = "4", Descripcion = "Identificador único de la oficina asociada al registro.\nEste valor hace referencia al campo Unico de la tabla Oficina.toficina.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Unico_Oficina", Tipo = "varchar", Long = "20", Descripcion = "Identificador único de la oficina asociada al registro.\nEste valor hace referencia al campo Unico de la tabla Oficina.toficina.", EsReglaFija = true },

            // Fechas cronológicas
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Fecha", Tipo = "datetime", Long = "8", Descripcion = "Fecha de creación o registro del cliente.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Fecha_Nacimiento", Tipo = "datetime", Long = "8", Descripcion = "Fecha de nacimiento de la persona.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Tiempo_Establecido", Tipo = "datetime", Long = "8", Descripcion = "Fecha desde la cual reside o está establecido en la dirección registrada.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "FechaVencimiento", Tipo = "datetime", Long = "8", Descripcion = "Fecha de vencimiento asociada al registro o documento.", EsReglaFija = true },

            // Auditoría y Control
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Autor", Tipo = "varchar", Long = "35", Descripcion = "Usuario que realizó la creación o modificación del registro.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "AutorFecha", Tipo = "datetime", Long = "8", Descripcion = "Fecha y hora de la última actualización del registro.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "AutorAccion", Tipo = "varchar", Long = "1", Descripcion = "Código de la acción realizada sobre el registro.\nI = Insertado\nM = Modificado\nE = Eliminado\nN = Anulado", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Usuario", Tipo = "varbinary", Long = "300", Descripcion = "Indica la clave del usuario asociado al registro.", EsReglaFija = true },

            // Patrones complementarios transversales
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "CreadoPor", Tipo = "nvarchar", Long = "100", Descripcion = "Nombre de usuario o cuenta de sistema responsable de registrar la transacción original en la base de datos.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "CreadaPor", Tipo = "nvarchar", Long = "100", Descripcion = "Nombre de usuario o cuenta de sistema responsable de registrar la transacción original en la base de datos.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "ModificadoPor", Tipo = "nvarchar", Long = "100", Descripcion = "Último usuario o servicio responsable de modificar el registro.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "ActualizadoPor", Tipo = "nvarchar", Long = "100", Descripcion = "Último usuario o servicio responsable de modificar el registro.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "FechaAutorizacion", Tipo = "datetime", Long = "8", Descripcion = "Fecha formal en la que se autorizó o aprobó la solicitud/registro.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "FechaAprobacion", Tipo = "datetime", Long = "8", Descripcion = "Fecha formal en la que se autorizó o aprobó la solicitud/registro.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "ModifiedDate", Tipo = "datetime", Long = "8", Descripcion = "Fecha y hora en que el registro fue actualizado por última vez en la base de datos.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "rowguid", Tipo = "uniqueidentifier", Long = "16", Descripcion = "Identificador único global (GUID) utilizado para replicación y sincronización de datos.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Observaciones", Tipo = "nvarchar", Long = "MAX", Descripcion = "Comentarios, notas técnicas o detalles aclaratorios adicionales relacionados al registro.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Descripcion", Tipo = "nvarchar", Long = "MAX", Descripcion = "Resumen textual explicativo que detalla el alcance y características del requerimiento o registro.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Nombre", Tipo = "nvarchar", Long = "150", Descripcion = "Título o denominación breve que identifica el elemento o registro.", EsReglaFija = true },
            new GlossaryItem { Esquema = "dbo", Tabla = "General", Campo = "Codigo", Tipo = "varchar", Long = "50", Descripcion = "Código alfanumérico o identificador oficial para la clasificación, consulta y validación del registro.", EsReglaFija = true }
        };

        // =========================================================================
        // CATÁLOGO MAESTRO DE REFERENCIA (137 TABLAS, 3,017 COLUMNAS - CF01)
        // Extraído directamente de Diccionario_de_datos.docx (Estándar de Oro)
        // =========================================================================
        private static readonly object _catalogLock = new object();
        private static bool _catalogLoaded = false;
        private static readonly Dictionary<string, string> _catalogTableDescriptions = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> _catalogTableColumnDescriptions = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> _catalogGlobalColumnDescriptions = new(StringComparer.OrdinalIgnoreCase);
        private static readonly List<GlossaryItem> _catalogGlossaryItems = new();

        public static void EnsureCatalogLoaded()
        {
            if (_catalogLoaded) return;
            lock (_catalogLock)
            {
                if (_catalogLoaded) return;
                try
                {
                    string[] possiblePaths = new[]
                    {
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Catalogo_Maestro_CF01.json"),
                        Path.Combine(Directory.GetCurrentDirectory(), "Catalogo_Maestro_CF01.json"),
                        Path.Combine(AppContext.BaseDirectory, "Catalogo_Maestro_CF01.json"),
                        @"D:\DocumentacionPrueba\Catalogo_Maestro_CF01.json"
                    };

                    string? catalogPath = possiblePaths.FirstOrDefault(File.Exists);
                    if (catalogPath != null)
                    {
                        string json = File.ReadAllText(catalogPath, Encoding.UTF8);
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };
                        var tables = JsonSerializer.Deserialize<List<MasterCatalogTableEntry>>(json, options);

                        if (tables != null)
                        {
                            foreach (var tbl in tables)
                            {
                                string schema = tbl.Schema?.Trim() ?? "";
                                string tableName = tbl.TableName?.Trim() ?? "";
                                string tableDesc = tbl.Description?.Trim() ?? "";

                                if (!string.IsNullOrWhiteSpace(tableName))
                                {
                                    _catalogTableDescriptions[tableName] = tableDesc;
                                    int dotIdx = tableName.IndexOf('.');
                                    if (dotIdx >= 0 && dotIdx < tableName.Length - 1)
                                    {
                                        string bareTable = tableName.Substring(dotIdx + 1).Trim();
                                        _catalogTableDescriptions[bareTable] = tableDesc;
                                    }
                                    if (!string.IsNullOrWhiteSpace(schema))
                                    {
                                        _catalogTableDescriptions[$"{schema}.{tableName}"] = tableDesc;
                                    }
                                }

                                if (tbl.Columns != null)
                                {
                                    foreach (var col in tbl.Columns)
                                    {
                                        string colName = col.Campo?.Trim() ?? "";
                                        string colDesc = col.Descripcion?.Trim() ?? "";
                                        if (string.IsNullOrWhiteSpace(colName)) continue;

                                        if (!string.IsNullOrWhiteSpace(tableName))
                                        {
                                            _catalogTableColumnDescriptions[$"{tableName}.{colName}"] = colDesc;
                                            int dotIdx = tableName.IndexOf('.');
                                            if (dotIdx >= 0 && dotIdx < tableName.Length - 1)
                                            {
                                                string bareTable = tableName.Substring(dotIdx + 1).Trim();
                                                _catalogTableColumnDescriptions[$"{bareTable}.{colName}"] = colDesc;
                                            }
                                            if (!string.IsNullOrWhiteSpace(schema))
                                            {
                                                _catalogTableColumnDescriptions[$"{schema}.{tableName}.{colName}"] = colDesc;
                                            }
                                        }

                                        if (!_catalogGlobalColumnDescriptions.ContainsKey(colName))
                                        {
                                            _catalogGlobalColumnDescriptions[colName] = colDesc;
                                        }

                                        _catalogGlossaryItems.Add(new GlossaryItem
                                        {
                                            Esquema = string.IsNullOrWhiteSpace(schema) ? "dbo" : schema,
                                            Tabla = string.IsNullOrWhiteSpace(tableName) ? "General" : tableName,
                                            Campo = colName,
                                            Tipo = col.Tipo?.Trim() ?? "",
                                            Long = col.Long?.Trim() ?? "",
                                            Descripcion = colDesc,
                                            EsReglaFija = true
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Fallback silencioso si no se encuentra o hay error en deserialización
                }
                finally
                {
                    _catalogLoaded = true;
                }
            }
        }

        public static bool TryGetCatalogTableDescription(string schemaName, string tableName, out string description)
        {
            EnsureCatalogLoaded();
            if (_catalogTableDescriptions.TryGetValue($"{schemaName}.{tableName}", out description!) && !string.IsNullOrWhiteSpace(description))
                return true;
            if (_catalogTableDescriptions.TryGetValue(tableName, out description!) && !string.IsNullOrWhiteSpace(description))
                return true;

            description = string.Empty;
            return false;
        }

        public static bool TryGetCatalogColumnDescription(string schemaName, string tableName, string colName, out string description)
        {
            EnsureCatalogLoaded();
            if (_catalogTableColumnDescriptions.TryGetValue($"{schemaName}.{tableName}.{colName}", out description!) && !string.IsNullOrWhiteSpace(description))
                return true;
            if (_catalogTableColumnDescriptions.TryGetValue($"{tableName}.{colName}", out description!) && !string.IsNullOrWhiteSpace(description))
                return true;

            description = string.Empty;
            return false;
        }

        public static bool TryGetCatalogGlobalColumnDescription(string colName, out string description)
        {
            EnsureCatalogLoaded();
            return _catalogGlobalColumnDescriptions.TryGetValue(colName, out description!) && !string.IsNullOrWhiteSpace(description);
        }

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
                    ('dbo', 'General', 'Autor', 'varchar', '35', 'Usuario que realizó la creación o modificación del registro.', 1),
                    ('dbo', 'General', 'AutorFecha', 'datetime', '8', 'Fecha y hora de la última actualización del registro.', 1),
                    ('dbo', 'General', 'AutorAccion', 'varchar', '1', 'Código de la acción realizada sobre el registro.' + CHAR(10) + 'I = Insertado' + CHAR(10) + 'M = Modificado' + CHAR(10) + 'E = Eliminado' + CHAR(10) + 'N = Anulado', 1),
                    ('dbo', 'General', 'ModificadoPor', 'nvarchar', '100', 'Último usuario o servicio responsable de modificar el registro.', 1),
                    ('dbo', 'General', 'ActualizadoPor', 'nvarchar', '100', 'Último usuario o servicio responsable de modificar el registro.', 1),
                    ('dbo', 'General', 'FechaAutorizacion', 'datetime', '8', 'Fecha formal en la que se autorizó o aprobó la solicitud/registro.', 1),
                    ('dbo', 'General', 'FechaAprobacion', 'datetime', '8', 'Fecha formal en la que se autorizó o aprobó la solicitud/registro.', 1),
                    ('dbo', 'General', 'FechaNotificada', 'datetime', '8', 'Fecha de calendario en la que se emitió formalmente la notificación al destinatario.', 1),
                    ('dbo', 'General', 'HoraNotificada', 'time', '5', 'Hora exacta en la que se despachó la notificación correspondiente.', 1),
                    ('dbo', 'General', 'NombreReq', 'nvarchar', '200', 'Título o denominación breve que identifica el requerimiento registrado.', 1),
                    ('dbo', 'General', 'DescripcionBreve', 'nvarchar', '500', 'Resumen textual explicativo que detalla el alcance y características del requerimiento.', 1),
                    ('dbo', 'General', 'Habil', 'bit', '1', 'Indica si la entidad se encuentra habilitada para procesar transacciones.' + CHAR(10) + '0 = No' + CHAR(10) + '1 = Sí', 1),
                    ('dbo', 'General', 'Activo', 'bit', '1', 'Indica si el registro se encuentra activo para procesar transacciones.' + CHAR(10) + '0 = No' + CHAR(10) + '1 = Sí', 1),
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

                EnsureCatalogLoaded();
                if (list.Count > 0)
                {
                    return list;
                }

                return _catalogGlossaryItems.Count > 0 ? _catalogGlossaryItems.ToList() : DefaultGlossaryItems.ToList();
            }
            catch
            {
                EnsureCatalogLoaded();
                return _catalogGlossaryItems.Count > 0 ? _catalogGlossaryItems.ToList() : DefaultGlossaryItems.ToList();
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
                                            && !col.BaseDescription.StartsWith("Dato correspondiente al campo", StringComparison.OrdinalIgnoreCase)
                                            && !col.BaseDescription.StartsWith("Indicador lógico del estado", StringComparison.OrdinalIgnoreCase)
                                            && !col.BaseDescription.StartsWith("Atributo general", StringComparison.OrdinalIgnoreCase)
                                            && !col.BaseDescription.StartsWith("Atributo técnico", StringComparison.OrdinalIgnoreCase);

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
                        table.SuggestedDescription = GenerateIntelligentTableDescription(table.SchemaName, table.TableName);
                        table.EditableDescription = table.SuggestedDescription;
                    }

                    // Evaluar cada columna
                    foreach (var col in table.Columns)
                    {
                        // Si ya está documentada en SQL Server (y no es un texto genérico heredado), respetar la descripción existente
                        col.HasExistingDescription = !string.IsNullOrWhiteSpace(col.BaseDescription)
                                                     && !col.BaseDescription.Equals("Sin descripción disponible", StringComparison.OrdinalIgnoreCase)
                                                     && !col.BaseDescription.StartsWith("Dato correspondiente al campo", StringComparison.OrdinalIgnoreCase)
                                                     && !col.BaseDescription.StartsWith("Indicador lógico del estado", StringComparison.OrdinalIgnoreCase)
                                                     && !col.BaseDescription.StartsWith("Atributo general", StringComparison.OrdinalIgnoreCase)
                                                     && !col.BaseDescription.StartsWith("Atributo técnico", StringComparison.OrdinalIgnoreCase);

                        if (col.HasExistingDescription)
                        {
                            col.EditableDescription = col.BaseDescription.Trim();
                            col.SuggestionSource = SuggestionSourceType.ExistingInDatabase;
                            continue;
                        }

                        // =========================================================================
                        // REGLA 0: Catálogo Maestro de Referencia (Coincidencia exacta de tabla y columna)
                        // =========================================================================
                        if (TryGetCatalogColumnDescription(table.SchemaName, table.TableName, col.Name, out var catalogColDesc))
                        {
                            col.SuggestedDescription = catalogColDesc;
                            col.EditableDescription = catalogColDesc;
                            col.SuggestionSource = SuggestionSourceType.MasterGlossary;
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
                            string refTable = "";
                            string refCol = "";
                            int pOpen = col.ForeignKeyReference.IndexOf('(');
                            int pClose = col.ForeignKeyReference.IndexOf(')');
                            if (pOpen > 0 && pClose > pOpen)
                            {
                                refTable = col.ForeignKeyReference.Substring(0, pOpen).Trim();
                                refCol = col.ForeignKeyReference.Substring(pOpen + 1, pClose - pOpen - 1).Trim();
                            }
                            else
                            {
                                refTable = col.ForeignKeyReference.Trim();
                            }

                            string colConcept = ResolveFkConcept(col.Name, refTable);
                            string fkDescription = !string.IsNullOrWhiteSpace(refCol)
                                ? $"Identificador {colConcept} asociado al registro.\nEste valor hace referencia al campo {refCol} de la tabla {refTable}."
                                : $"Identificador {colConcept} asociado al registro.\nEste valor hace referencia a la tabla {refTable}.";

                            col.SuggestedDescription = fkDescription;
                            col.EditableDescription = fkDescription;
                            col.SuggestionSource = SuggestionSourceType.ForeignKeyReference;
                            continue;
                        }

                        // =========================================================================
                        // REGLA 3: Glosario Maestro y Catálogo Global (coincidencia de columna)
                        // =========================================================================
                        if (glossaryTableFieldMap.TryGetValue($"{table.TableName}.{col.Name}", out var glosDesc)
                            || glossaryExactMap.TryGetValue(col.Name, out glosDesc))
                        {
                            col.SuggestedDescription = glosDesc;
                            col.EditableDescription = glosDesc;
                            col.SuggestionSource = SuggestionSourceType.MasterGlossary;
                            continue;
                        }

                        if (TryGetCatalogGlobalColumnDescription(col.Name, out var catalogGlobalColDesc))
                        {
                            col.SuggestedDescription = catalogGlobalColDesc;
                            col.EditableDescription = catalogGlobalColDesc;
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
                        // REGLA 5: Identificadores y Claves Foráneas por convención (prefijo Id* o sufijo *_Id / *Id, y NO es PK)
                        // =========================================================================
                        bool isIdSuffix = col.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase) || col.Name.EndsWith("_Id", StringComparison.OrdinalIgnoreCase);
                        bool isIdPrefix = col.Name.StartsWith("Id", StringComparison.OrdinalIgnoreCase) && col.Name.Length > 2 && char.IsLetter(col.Name[2]);

                        if (isIdSuffix || isIdPrefix)
                        {
                            string entityName = isIdPrefix
                                ? col.Name.Substring(2)
                                : col.Name.Substring(0, col.Name.Length - (col.Name.EndsWith("_Id", StringComparison.OrdinalIgnoreCase) ? 3 : 2));

                            string fkDescription = ResolveIdDescription(col.Name, entityName);
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

            // 0. Patrones de Auditoría Institucional y Control Específico
            if (lowerName == "autoraccion" || lowerName == "autor_accion")
                return "Código de la acción realizada sobre el registro.\nI = Insertado\nM = Modificado\nE = Eliminado\nN = Anulado";
            if (lowerName == "autorfecha" || lowerName == "autor_fecha")
                return "Fecha y hora de la última actualización del registro.";
            if (lowerName == "autor")
                return "Usuario que realizó la creación o modificación del registro.";
            if (lowerName == "unico_oficina" || lowerName == "oficina_id")
                return "Identificador único de la oficina asociada al registro.\nEste valor hace referencia al campo Unico de la tabla Oficina.toficina.";
            if (lowerName == "ultimo_prestamo" || lowerName == "ultimoprestamo")
                return "Número identificador del último préstamo realizado al cliente.";
            if (lowerName == "junto_desde" || lowerName == "juntodesde")
                return "Fecha desde la que el cliente está casado con su cónyuge.";
            if (lowerName == "tiempo_establecido" || lowerName == "tiempoestablecido")
                return "Fecha desde la cual reside o está establecido en la dirección registrada.";
            if (lowerName == "cedula")
                return "Número de cédula del cliente.";
            if (lowerName == "rnc")
                return "Número de Registro Nacional de Contribuyentes (RNC) de la empresa.";
            if (lowerName == "pasaporte")
                return "Número de pasaporte del cliente.";
            if (lowerName == "licencia_no" || lowerName == "licenciano")
                return "Número de licencia asociada al cliente.";
            if (lowerName == "cantprestamosdeudor")
                return "Cantidad de préstamos en los que el cliente figura como deudor.";
            if (lowerName == "cantprestamosdeudorconyugue")
                return "Cantidad de préstamos en los que el cliente figura como cónyuge del deudor.";
            if (lowerName == "cantprestamogarante")
                return "Cantidad de préstamos en los que el cliente figura como garante del deudor.";
            if (lowerName == "cantprestamogaranteconyugue")
                return "Cantidad de préstamos en los que el cliente figura como cónyuge del garante.";
            if (lowerName == "conatraso")
                return "Cantidad de pagos realizados con atraso.";
            if (lowerName == "sinatraso")
                return "Cantidad de pagos realizados sin atraso.";
            if (lowerName == "hijos")
                return "Cantidad de hijo que tiene el cliente.";
            if (lowerName == "personal_dependientes" || lowerName == "personaldependientes")
                return "Cantidad de persona que depende económicamente del cliente.";

            // 0.1 Claves e Identificadores (Id*)
            if (lowerName.StartsWith("id") && cleanName.Length > 2 && char.IsLetter(cleanName[2]))
            {
                string entity = cleanName.Substring(2);
                return ResolveIdDescription(cleanName, entity);
            }

            // 1. Patrones de Auditoría / Usuarios
            // CreadoPor, CreadaPor, CreatedBy, Creado, Creada, UsuarioCreacion, UserCreated
            if (lowerName.Contains("creadopor") || lowerName.Contains("creadapor") || lowerName.Contains("createdby") ||
                lowerName.Contains("usuariocreacion") || lowerName.Contains("usercreated") ||
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
                lowerName.StartsWith("is") || lowerName.StartsWith("has") || lowerWords.Contains("flag") ||
                lowerName.StartsWith("tcliente") || lowerName.StartsWith("templeado") || lowerName.StartsWith("tfuncionario") || lowerName.StartsWith("taccionista") ||
                lowerName.StartsWith("nofirma") || lowerName.StartsWith("noresidente"))
            {
                if (lowerName == "habil")
                    return "Indica si la entidad se encuentra habilitada para procesar transacciones.\n0 = No\n1 = Sí";
                if (lowerName == "activo")
                    return "Indica si el registro se encuentra activo para procesar transacciones.\n0 = No\n1 = Sí";
                if (lowerName == "nofirma")
                    return "Indica si el cliente no sabe leer ni escribir.\n0 = No\n1 = Sí";
                if (lowerName == "sincronizado")
                    return "Indica si el registro ha sido sincronizado con la base de datos.\n0 = No\n1 = Sí";
                if (lowerName == "noresidente")
                    return "Indica si el cliente es no residente.\n0 = No\n1 = Sí";
                if (lowerName == "abonoextraordinario")
                    return "Indica si el cliente realiza o tiene autorizado realizar abonos extraordinarios.\n0 = No\n1 = Sí";
                if (lowerName == "us_persona" || lowerName == "uspersona")
                    return "Indica si el cliente es considerado una persona estadounidense para fines fiscales y regulatorios.\n0 = No\n1 = Sí";
                if (lowerName == "editar")
                    return "Indica si el registro puede ser editado.\n0 = No\n1 = Sí";
                if (lowerName == "vercorreo")
                    return "Indica si se permite visualizar el correo electrónico del cliente.\n0 = No\n1 = Sí";
                if (lowerName == "vertelefono")
                    return "Indica si se permite visualizar los números telefónicos del cliente.\n0 = No\n1 = Sí";
                if (lowerName == "aceptacontrato")
                    return "Indica si el cliente ha aceptado los términos del contrato.\n0 = No\n1 = Sí";
                if (lowerName == "residentepermanente")
                    return "Indica si el cliente posee condición de residente permanente.\n0 = No\n1 = Sí";
                if (lowerName.StartsWith("esmiembro") || lowerName.Contains("religion"))
                    return "Indica si el cliente pertenece a una organización religiosa.\n0 = No\n1 = Sí";
                if (lowerName.StartsWith("esfamiliar") || lowerName.Contains("familiarfuncionario"))
                    return "Indica si el cliente es familiar de un funcionario público.\n0 = No\n1 = Sí";
                if (lowerName == "esfuncionario")
                    return "Indica si el cliente es funcionario.\n0 = No\n1 = Sí";
                if (lowerName.StartsWith("hijosmenores") || lowerName.Contains("menores10"))
                    return "Indica si el cliente tiene hijos menores de 10 años.\n0 = No\n1 = Sí";
                if (lowerName.Contains("11a17") || lowerName.Contains("de11a17"))
                    return "Indica si el cliente tiene hijos entre 11 y 17 años.\n0 = No\n1 = Sí";
                if (lowerName.Contains("mayoresa18") || lowerName.Contains("mayores18"))
                    return "Indica si el cliente tiene hijos mayores de 18 años.\n0 = No\n1 = Sí";
                if (lowerName == "templeado")
                    return "Indica si la persona es empleado de la empresa.\n0 = No\n1 = Sí";
                if (lowerName == "taccionista" || lowerName == "tacciónista")
                    return "Indica si la persona es accionista de la empresa.\n0 = No\n1 = Sí";
                if (lowerName == "tfuncionario")
                    return "Indica si la persona es funcionario de la empresa.\n0 = No\n1 = Sí";
                if (lowerName == "tcliente")
                    return "Indica si la persona es cliente de la empresa.\n0 = No\n1 = Sí";

                string condition = HumanizeCondition(cleanName);
                return $"Indica si {condition}.\n0 = No\n1 = Sí";
            }

            // 5. Cantidades y Métricas (ej. Cant*, Total, Monto)
            if (lowerName.StartsWith("cant") || lowerName.StartsWith("qty") || lowerWords.Contains("cantidad") || lowerWords.Contains("cant"))
            {
                var otherWords = words.Where(w => !w.Equals("cant", StringComparison.OrdinalIgnoreCase) && 
                                                  !w.Equals("cantidad", StringComparison.OrdinalIgnoreCase) &&
                                                  !w.Equals("qty", StringComparison.OrdinalIgnoreCase)).ToList();
                if (otherWords.Count > 0)
                {
                    string concept = string.Join(" ", otherWords).ToLowerInvariant();
                    return $"Cantidad de {concept} asociada al registro.";
                }
                return "Cantidad numérica de operaciones o elementos asociados al registro.";
            }

            if (lowerName.Contains("total") || lowerName.Contains("monto") || lowerName.Contains("precio") || lowerName.Contains("subtotal") ||
                lowerName.Contains("costo") || lowerName.Contains("importe") || lowerName.Contains("saldo") || lowerName.Contains("volumen") || lowerType.Contains("money"))
            {
                var otherWords = words.Where(w => !w.Equals("monto", StringComparison.OrdinalIgnoreCase) && 
                                                  !w.Equals("total", StringComparison.OrdinalIgnoreCase) &&
                                                  !w.Equals("importe", StringComparison.OrdinalIgnoreCase) &&
                                                  !w.Equals("volumen", StringComparison.OrdinalIgnoreCase) &&
                                                  !w.Equals("saldo", StringComparison.OrdinalIgnoreCase)).ToList();
                if (otherWords.Count > 0)
                {
                    string concept = string.Join(" ", otherWords).ToLowerInvariant();
                    return $"Monto o valor económico de {concept} registrado.";
                }
                return "Monto o volumen financiero de la transacción registrada.";
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

            // 10. Fallback contextualizado según semántica de tipo y nombre
            var nonTechWords = words.Where(w => !w.Equals("id", StringComparison.OrdinalIgnoreCase) &&
                                                !w.Equals("cod", StringComparison.OrdinalIgnoreCase) &&
                                                !w.Equals("codigo", StringComparison.OrdinalIgnoreCase)).ToList();
            string conceptName = nonTechWords.Count > 0 ? string.Join(" ", nonTechWords).ToLowerInvariant() : cleanName.ToLowerInvariant();

            if (lowerType.Contains("char") || lowerType.Contains("text"))
            {
                return $"Información descriptiva correspondiente a {conceptName} en el sistema.";
            }
            if (lowerType.Contains("int") || lowerType.Contains("numeric") || lowerType.Contains("decimal") || lowerType.Contains("float"))
            {
                return $"Valor numérico correspondiente a {conceptName} registrado en la transacción.";
            }
            if (lowerType.Contains("date") || lowerType.Contains("time"))
            {
                return $"Fecha y hora cronológica registrada para {conceptName} en el sistema.";
            }
            if (lowerType.Contains("bit"))
            {
                return $"Indica si {conceptName} aplica para el registro.\n0 = No\n1 = Sí";
            }
            if (lowerType.Contains("binary") || lowerType.Contains("image"))
            {
                return $"Dato binario o archivo digital correspondiente a {conceptName} almacenado en el registro.";
            }

            return $"Dato operativo correspondiente a {conceptName} registrado en el sistema.";
        }

        private static string HumanizeCondition(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "el registro se encuentra habilitado";
            string lower = name.ToLowerInvariant();
            if (lower.StartsWith("es"))
            {
                var rest = SplitWords(name.Substring(2));
                return "la entidad o registro es " + string.Join(" ", rest).ToLowerInvariant();
            }
            if (lower.StartsWith("tiene"))
            {
                var rest = SplitWords(name.Substring(5));
                return "el cliente o registro posee " + string.Join(" ", rest).ToLowerInvariant();
            }
            if (lower.StartsWith("puede"))
            {
                var rest = SplitWords(name.Substring(5));
                return "se permite " + string.Join(" ", rest).ToLowerInvariant();
            }
            var words = SplitWords(name);
            return string.Join(" ", words).ToLowerInvariant() + " aplica para el registro";
        }

        private static string ResolveFkConcept(string colName, string refTable)
        {
            string lowerCol = colName.ToLowerInvariant();
            if (lowerCol.Contains("parentesco")) return "del tipo de parentesco o relación familiar";
            if (lowerCol.Contains("estado_civil") || lowerCol.Contains("estadocivil")) return "del estado civil de la persona";
            if (lowerCol.Contains("riesgo")) return "del nivel de riesgo crediticio asignado al cliente";
            if (lowerCol.Contains("vinculad") || lowerCol.Contains("vinculacion")) return "del tipo de vinculación institucional";
            if (lowerCol.Contains("libro")) return "de la clasificación de libro asignada al cliente";
            if (lowerCol.Contains("nacionalidad") || lowerCol.Contains("pais")) return "del país";
            if (lowerCol.Contains("banco")) return "de la entidad bancaria asociada a la cuenta o transacción";
            if (lowerCol.Contains("oficina")) return "único de la oficina";
            if (lowerCol.Contains("sector")) return "del sector geográfico asociado a la dirección";
            if (lowerCol.Contains("municipio") || lowerCol.Contains("nacimiento")) return "del municipio";
            if (lowerCol.Contains("conyugue") || lowerCol.Contains("conyuge")) return "único del cónyuge del cliente mediante cédula, pasaporte o RNC";
            if (lowerCol.Contains("cliente")) return "único del cliente o entidad";
            if (lowerCol.Contains("moneda")) return "del tipo de moneda para transacciones";
            if (lowerCol.Contains("sucursal")) return "de la sucursal asignada";
            if (lowerCol.Contains("empleado")) return "del empleado responsable";
            if (lowerCol.Contains("producto")) return "del producto financiero o comercial";
            if (lowerCol.Contains("cuenta")) return "de la cuenta bancaria o contable";
            if (lowerCol.Contains("medio_refer")) return "único del medio por el que el cliente conoció la institución";

            string cleanTable = refTable.Replace("Entidad.", "").Replace("Configuracion.", "").Replace("Localidad.", "").Replace("Oficina.", "").Replace("Banco.", "").Replace("dbo.", "");
            if (cleanTable.StartsWith("t", StringComparison.OrdinalIgnoreCase) && cleanTable.Length > 2)
            {
                cleanTable = cleanTable.Substring(1);
            }
            var words = SplitWords(cleanTable);
            string concept = words.Count > 0 ? string.Join(" ", words).ToLowerInvariant() : "la entidad relacionada";
            return $"de {concept}";
        }

        public static string ResolveIdDescription(string colName, string entityName)
        {
            string lowerCol = colName.ToLowerInvariant();

            if (lowerCol == "idregistro" || lowerCol == "id_registro")
                return "Identificador único del registro o transacción auditada en el sistema.";
            if (lowerCol == "idopcion" || lowerCol == "id_opcion")
                return "Identificador único de la opción o menú del sistema al que se concede o registra el acceso.";
            if (lowerCol == "idcliente" || lowerCol == "cliente_id" || lowerCol == "clienteid")
                return "Identificador único del cliente o entidad puede ser Cédula, RNC y Pasaporte.";
            if (lowerCol == "idsecuencia" || lowerCol == "id_secuencia")
                return "Identificador numérico de la secuencia asignada a la operación en el sistema.";
            if (lowerCol == "idusuario" || lowerCol == "usuario_id" || lowerCol == "usuarioid")
                return "Identificador del usuario que accede o ejecuta la operación en el sistema.";
            if (lowerCol == "idautor" || lowerCol == "autor_id")
                return "Usuario que realizó la última modificación en el registro.";
            if (lowerCol == "idoficina" || lowerCol == "oficina_id" || lowerCol == "oficinaid" || lowerCol == "unico_oficina")
                return "Identificador único de la oficina asociada al registro.\nEste valor hace referencia al campo Unico de la tabla Oficina.toficina.";
            if (lowerCol == "idrama" || lowerCol == "rama_id")
                return "Identificador de la rama o módulo funcional del sistema.";
            if (lowerCol == "idramatrabajo" || lowerCol == "rama_trabajo_id" || lowerCol == "ramatrabajoid")
                return "Identificador de la rama de trabajo o área operativa asignada al usuario en el sistema.";
            if (lowerCol == "idmodulo" || lowerCol == "modulo_id")
                return "Identificador único del módulo del sistema al que pertenece la función.";
            if (lowerCol == "idmenu" || lowerCol == "menu_id")
                return "Identificador único del menú o formulario en la jerarquía del sistema.";
            if (lowerCol == "idperfil" || lowerCol == "perfil_id")
                return "Identificador del perfil de seguridad asignado al usuario.";
            if (lowerCol == "idrol" || lowerCol == "rol_id")
                return "Identificador del rol operativo asignado al usuario.";
            if (lowerCol == "idpermiso" || lowerCol == "permiso_id")
                return "Identificador del permiso de ejecución otorgado sobre la función.";
            if (lowerCol == "idparentesco" || lowerCol == "parentesco_id")
                return "Identificador del tipo de parentesco o relación familiar asociado al registro.\nEste valor hace referencia al campo Unico de la tabla Entidad.tentidad_parentesco_tipo.";
            if (lowerCol == "idestadocivil" || lowerCol == "estado_civil" || lowerCol == "estadocivil_id")
                return "Identificador del estado civil de la persona asociada al registro.\nEste valor hace referencia al campo Estado_Civil de la tabla Entidad.tentidad_estado_civil.";
            if (lowerCol == "idriesgo" || lowerCol == "riesgo_id")
                return "Identificador del nivel de riesgo crediticio asignado al cliente.\nEste valor hace referencia al campo Unico de la tabla Configuracion.tRiesgo.";
            if (lowerCol == "idvinculado" || lowerCol == "vinculado_id" || lowerCol == "vinculacion_id")
                return "Identificador del tipo de vinculación institucional asociado al registro.\nEste valor hace referencia al campo Unico de la tabla Entidad.tentidad_vinculacion_tipo.";
            if (lowerCol == "idlibro" || lowerCol == "libro_id")
                return "Identificador de la clasificación de libro asignada al cliente.\nEste valor hace referencia al campo Unico de la tabla Entidad.tentidad_Libro.";
            if (lowerCol == "idnacionalidad" || lowerCol == "nacionalidad_id" || lowerCol == "nacionalidad1_id" || lowerCol == "idpais" || lowerCol == "pais_id")
                return "Identificador del país asociado al registro.\nEste valor hace referencia al campo IdPais de la tabla Localidad.tpais.";
            if (lowerCol == "idsector" || lowerCol == "sector_id")
                return "Identificador del sector geográfico asociado a la dirección.\nEste valor hace referencia al campo Unico de la tabla Localidad.tsector.";
            if (lowerCol == "idmunicipio" || lowerCol == "municipio_id" || lowerCol == "lugar_nacimiento")
                return "Identificador del municipio asociado al registro.\nEste valor hace referencia al campo Identificador de la tabla Localidad.tmunicipio.";
            if (lowerCol == "idbanco" || lowerCol == "banco_id")
                return "Identificador de la entidad bancaria asociada a la cuenta o transacción.\nEste valor hace referencia al campo Unico de la tabla Banco.tbCuentas_Banco.";
            if (lowerCol == "idprestamo" || lowerCol == "prestamo_id")
                return "Número identificador del préstamo asociado al registro.\nEste valor hace referencia al campo Unico de la tabla Prestamos.tprestamo_otorgado.";
            if (lowerCol == "idsolicitud" || lowerCol == "solicitud_id")
                return "Identificador de la solicitud de crédito asociada a la transacción.";
            if (lowerCol == "idgarantia" || lowerCol == "garantia_id")
                return "Identificador de la garantía registrada y vinculada a la operación.";
            if (lowerCol == "idcaja" || lowerCol == "caja_id")
                return "Identificador único de la caja operativa asociada al registro.\nEste valor hace referencia al campo Unico de la tabla Caja.tcaja.";
            if (lowerCol == "idmoneda" || lowerCol == "moneda_id")
                return "Identificador del tipo de moneda correspondiente a la transacción.";
            if (lowerCol == "idcuenta" || lowerCol == "cuenta_id")
                return "Identificador de la cuenta bancaria o contable vinculada a la operación.";

            string colConcept = ResolveFkConcept(colName, entityName);
            return $"Identificador {colConcept} asociado al registro.\nEste valor hace referencia a la entidad o catálogo [{entityName}].";
        }

        public static string CleanTableName(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName)) return "entidad";
            string clean = tableName.Trim();
            int dotIdx = clean.LastIndexOf('.');
            if (dotIdx >= 0 && dotIdx < clean.Length - 1)
            {
                clean = clean.Substring(dotIdx + 1).Trim();
            }
            if (clean.StartsWith("t", StringComparison.OrdinalIgnoreCase) && clean.Length > 2 && char.IsLetter(clean[1]))
            {
                clean = clean.Substring(1);
            }
            return clean;
        }

        private static string HumanizeTableName(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName)) return "la entidad";
            string clean = CleanTableName(tableName);
            var words = SplitWords(clean);
            return words.Count > 0 ? string.Join(" ", words).ToLowerInvariant() : clean.ToLowerInvariant();
        }

        public static string GenerateIntelligentTableDescription(string schemaName, string tableName)
        {
            if (TryGetCatalogTableDescription(schemaName, tableName, out var catDesc))
                return catDesc;

            string cleanTbl = CleanTableName(tableName);
            string lowerTbl = cleanTbl.ToLowerInvariant();
            string lowerSchema = (schemaName ?? "").ToLowerInvariant();

            // 1. Acceso y Seguridad
            if (lowerSchema.Contains("acceso") || lowerSchema.Contains("seguridad") || lowerTbl.Contains("acceso"))
            {
                if (lowerTbl == "acceso")
                    return "Gestiona y centraliza los accesos, privilegios y auditoría de permisos de los usuarios sobre las opciones y funciones del sistema.";
                if (lowerTbl.Contains("usuario"))
                    return "Almacena la información de los usuarios del sistema, sus credenciales y estado operativo.";
                if (lowerTbl.Contains("rol") || lowerTbl.Contains("perfil"))
                    return "Catálogo de roles y perfiles de seguridad para la administración y control de permisos de usuarios.";
                if (lowerTbl.Contains("opcion") || lowerTbl.Contains("menu"))
                    return "Catálogo de opciones, menús y formularios disponibles en el sistema para la asignación de permisos.";
                if (lowerTbl.Contains("permiso"))
                    return "Control de permisos y autorizaciones asignadas a roles o usuarios por cada opción del sistema.";
                if (lowerTbl.Contains("bitacora") || lowerTbl.Contains("log"))
                    return "Registra el historial cronológico de actividades, accesos y transacciones realizadas por los usuarios en el sistema.";
                return $"Gestiona la seguridad, permisos y control operativo de {HumanizeTableName(cleanTbl)} en el sistema.";
            }

            // 2. Localidad y Geografía
            if (lowerSchema.Contains("localidad") || lowerTbl.Contains("pais") || lowerTbl.Contains("provincia") || lowerTbl.Contains("municipio") || lowerTbl.Contains("sector"))
            {
                if (lowerTbl.Contains("pais"))
                    return "Catálogo oficial de países para la clasificación geográfica y nacionalidad de los clientes y transacciones.";
                if (lowerTbl.Contains("provincia"))
                    return "Catálogo de provincias o divisiones territoriales para la estructuración geográfica del sistema.";
                if (lowerTbl.Contains("municipio"))
                    return "Catálogo de municipios asociados a la estructura geográfica de localización y residencia.";
                if (lowerTbl.Contains("sector"))
                    return "Catálogo de sectores y zonas residenciales utilizados para la ubicación precisa de clientes y garantías.";
                return $"Catálogo geográfico para la administración y consulta de {HumanizeTableName(cleanTbl)}.";
            }

            // 3. Oficina y Sucursales
            if (lowerSchema.Contains("oficina") || lowerTbl.Contains("oficina") || lowerTbl.Contains("sucursal"))
            {
                if (lowerTbl.Contains("oficina"))
                    return "Almacena la información de las oficinas y dependencias de la empresa donde se procesan operaciones comerciales.";
                if (lowerTbl.Contains("sucursal"))
                    return "Gestiona las sucursales habilitadas para la atención al público, desembolso y cobranza de préstamos.";
                return $"Gestiona la estructura operativa y administrativa de {HumanizeTableName(cleanTbl)}.";
            }

            // 4. Banco y Cuentas Bancarias
            if (lowerSchema.Contains("banco") || lowerTbl.Contains("banco"))
            {
                if (lowerTbl.Contains("cuenta"))
                    return "Almacena la información de las cuentas bancarias de la institución y de clientes para transferencias y pagos.";
                return "Catálogo de instituciones financieras y entidades bancarias comerciales vinculadas a la empresa.";
            }

            // 5. Configuración y Parámetros
            if (lowerSchema.Contains("config") || lowerTbl.Contains("riesgo") || lowerTbl.Contains("parametro") || lowerTbl.Contains("moneda"))
            {
                if (lowerTbl.Contains("riesgo"))
                    return "Catálogo de niveles de riesgo crediticio asignados a los clientes en función de su perfil financiero y solvencia.";
                if (lowerTbl.Contains("moneda"))
                    return "Catálogo de monedas e instrumentos cambiarios aplicables a las operaciones crediticias.";
                if (lowerTbl.Contains("parametro"))
                    return "Gestiona los parámetros generales de configuración operativa y reglas de negocio del sistema.";
                return $"Almacena los parámetros y reglas de configuración de {HumanizeTableName(cleanTbl)}.";
            }

            // 6. Contabilidad
            if (lowerSchema.Contains("contab") || lowerTbl.Contains("asiento") || lowerTbl.Contains("cuenta_contable"))
            {
                return $"Gestiona la información contable y financiera de {HumanizeTableName(cleanTbl)} en el sistema.";
            }

            // Reglas generales por sufijo / tipo
            if (lowerTbl.Contains("historico") || lowerTbl.Contains("historial"))
                return $"Almacena el registro histórico y cronológico de {HumanizeTableName(cleanTbl)} en el sistema.";
            if (lowerTbl.Contains("detalle"))
                return $"Almacena el detalle de las transacciones y operaciones vinculadas a {HumanizeTableName(cleanTbl)}.";
            if (lowerTbl.Contains("tipo") || lowerTbl.Contains("categoria") || lowerTbl.Contains("clasificacion"))
                return $"Catálogo de {HumanizeTableName(cleanTbl)} utilizado para clasificar y organizar los registros en el sistema.";

            return $"Gestiona la información operativa de {HumanizeTableName(cleanTbl)}, regulando sus transacciones, validaciones y atributos asociados en el sistema.";
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

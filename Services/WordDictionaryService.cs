using System;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml.Drawing.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using DataDictionaryWeb.Models;

namespace DataDictionaryWeb.Services
{
    public class WordDictionaryService
    {
        private const string PrimaryFont = "Segoe UI";
        private const string CorporateSlate = "4A5568";
        private const string BorderColor = "D0D7DE";
        private const string ZebraBackground = "F8FAFC";
        private const string TextDark = "1E293B";

        // Anchos de columna proporcionales en dxa (Total: 9360 dxa)
        // Campo: 22% (2060) | Tipo: 15% (1404) | Long: 10% (936) | Descripción: 53% (4960)
        private const int ColWidthCampo = 2060;
        private const int ColWidthTipo = 1404;
        private const int ColWidthLong = 936;
        private const int ColWidthDesc = 4960;

        /// <summary>
        /// Genera el documento Word directamente a un archivo temporal en disco usando FileStream.
        /// Esta estrategia previene el consumo excesivo de memoria RAM (LOH fragmentation) en documentos masivos.
        /// </summary>
        public string GenerateDocumentToTempFile(DatabaseDictionary data, CustomizationModel customization)
        {
            string tempFilePath = Path.Combine(Path.GetTempPath(), $"DataDict_{Guid.NewGuid():N}.docx");

            using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 65536))
            {
                BuildWordDocument(fileStream, data, customization);
            }

            return tempFilePath;
        }

        public byte[] GenerateDocumentBytes(DatabaseDictionary data, CustomizationModel customization)
        {
            using var memoryStream = new MemoryStream();
            BuildWordDocument(memoryStream, data, customization);
            return memoryStream.ToArray();
        }

        private void BuildWordDocument(Stream targetStream, DatabaseDictionary data, CustomizationModel customization)
        {
            using var wordDoc = WordprocessingDocument.Create(targetStream, WordprocessingDocumentType.Document, true);
            var mainPart = wordDoc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = new Body();

            string primaryColor = CleanHex(customization.PrimaryColorHex, "1B365D");

            // Configuración de página: márgenes 1 pulgada (1440 dxa)
            var sectionProps = new SectionProperties();
            var pageMargin = new PageMargin
            {
                Top = 1440,
                Bottom = 1440,
                Left = 1440,
                Right = 1440,
                Header = 720,
                Footer = 720
            };
            sectionProps.Append(pageMargin);

            // 1. Portada y Encabezado Institucional
            AddCoverSection(body, mainPart, data, customization, primaryColor);

            // 2. Módulos / Esquemas y Tablas
            bool isFirstSchema = true;
            foreach (var schema in data.Schemas.OrderBy(s => s.SchemaName))
            {
                AddSchemaHeader(body, schema.SchemaName, primaryColor, isFirstSchema);
                isFirstSchema = false;

                foreach (var table in schema.Tables.OrderBy(t => t.TableName))
                {
                    AddTableSubtitle(body, table.SchemaName, table.TableName);
                    string tableDesc = !string.IsNullOrWhiteSpace(table.EditableDescription) ? table.EditableDescription : table.Description;
                    AddTableDescription(body, tableDesc, primaryColor);
                    AddAttributesTable(body, table, primaryColor);

                    body.Append(new Paragraph(
                        new ParagraphProperties(
                            new SpacingBetweenLines { Before = "0", After = "180", Line = "20" }
                        )
                    ));
                }
            }

            // 3. Historial de Modificaciones y Campos Deprecados / Eliminados
            AddDeprecatedFieldsSection(body, data.DeprecatedFields, primaryColor);

            // 4. Historial de Versiones y Modificaciones Estructurales
            AddVersionHistorySection(body, data.VersionHistory, primaryColor);

            body.Append(sectionProps);
            mainPart.Document.Append(body);
            mainPart.Document.Save();
        }

        private void AddCoverSection(Body body, MainDocumentPart mainPart, DatabaseDictionary data, CustomizationModel customization, string primaryColor)
        {
            // Espaciador superior
            body.Append(new Paragraph(new ParagraphProperties(new SpacingBetweenLines { After = "180" })));

            // Logotipo si fue cargado
            if (customization.LogoBytes != null && customization.LogoBytes.Length > 0)
            {
                try
                {
                    var imagePartType = (customization.LogoContentType?.Contains("png", StringComparison.OrdinalIgnoreCase) ?? false)
                        ? ImagePartType.Png
                        : ImagePartType.Jpeg;

                    var imgPart = mainPart.AddImagePart(imagePartType);
                    using (var ms = new MemoryStream(customization.LogoBytes))
                    {
                        imgPart.FeedData(ms);
                    }
                    string relationshipId = mainPart.GetIdOfPart(imgPart);

                    body.Append(CreateImageParagraph(relationshipId, 120, 50));
                }
                catch
                {
                    // Fallback silencioso si el formato de imagen es no compatible
                }
            }

            // Título Institucional
            string inst = string.IsNullOrWhiteSpace(customization.InstitutionTitle)
                ? "DEPARTAMENTO DE TECNOLOGÍA DE LA INFORMACIÓN"
                : customization.InstitutionTitle.ToUpperInvariant();

            body.Append(new Paragraph(
                new ParagraphProperties(
                    new SpacingBetweenLines { After = "100", Line = "240" },
                    new Justification { Val = JustificationValues.Left }
                ),
                new Run(
                    new RunProperties(
                        new RunFonts { Ascii = PrimaryFont, HighAnsi = PrimaryFont },
                        new Bold(),
                        new Color { Val = CorporateSlate },
                        new FontSize { Val = "22" }
                    ),
                    new Text(inst)
                )
            ));

            // Título Principal
            string title = string.IsNullOrWhiteSpace(customization.DocumentTitle)
                ? $"Diccionario de Datos de la Base de Datos {data.DatabaseName}"
                : customization.DocumentTitle;

            body.Append(new Paragraph(
                new ParagraphProperties(
                    new SpacingBetweenLines { Before = "60", After = "240", Line = "280" },
                    new Justification { Val = JustificationValues.Left }
                ),
                new Run(
                    new RunProperties(
                        new RunFonts { Ascii = PrimaryFont, HighAnsi = PrimaryFont },
                        new Bold(),
                        new Color { Val = primaryColor },
                        new FontSize { Val = "42" } // 21pt
                    ),
                    new Text(title)
                )
            ));

            // Barra decorativa con color primario
            var dividerTable = new Table(
                new TableProperties(
                    new TableWidth { Type = TableWidthUnitValues.Dxa, Width = "9360" },
                    new TableBorders(
                        new BottomBorder { Val = BorderValues.Single, Size = 12, Color = primaryColor }
                    )
                ),
                new TableGrid(new GridColumn { Width = "9360" }),
                new TableRow(
                    new TableCell(
                        new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = "9360" }),
                        new Paragraph(new ParagraphProperties(new SpacingBetweenLines { After = "0", Line = "60" }))
                    )
                )
            );
            body.Append(dividerTable);

            body.Append(new Paragraph(new ParagraphProperties(new SpacingBetweenLines { After = "240" })));

            // Tarjeta de Metadatos
            var metadataTable = new Table(
                new TableProperties(
                    new TableWidth { Type = TableWidthUnitValues.Dxa, Width = "9360" },
                    new TableBorders(
                        new TopBorder { Val = BorderValues.Single, Size = 4, Color = BorderColor },
                        new LeftBorder { Val = BorderValues.Single, Size = 4, Color = BorderColor },
                        new BottomBorder { Val = BorderValues.Single, Size = 4, Color = BorderColor },
                        new RightBorder { Val = BorderValues.Single, Size = 4, Color = BorderColor },
                        new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = BorderColor },
                        new InsideVerticalBorder { Val = BorderValues.None }
                    ),
                    new TableCellMarginDefault(
                        new TopMargin { Width = "120", Type = TableWidthUnitValues.Dxa },
                        new TableCellLeftMargin { Width = 180, Type = TableWidthValues.Dxa },
                        new BottomMargin { Width = "120", Type = TableWidthUnitValues.Dxa },
                        new TableCellRightMargin { Width = 180, Type = TableWidthValues.Dxa }
                    )
                ),
                new TableGrid(
                    new GridColumn { Width = "3200" },
                    new GridColumn { Width = "6160" }
                )
            );

            AddSummaryRow(metadataTable, "Servidor de Base de Datos:", data.ServerName, primaryColor);
            AddSummaryRow(metadataTable, "Base de Datos:", data.DatabaseName, primaryColor);
            AddSummaryRow(metadataTable, "Fecha y Hora de Generación:", data.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss"), primaryColor);
            AddSummaryRow(metadataTable, "Total de Esquemas:", data.Schemas.Count.ToString(), primaryColor);
            AddSummaryRow(metadataTable, "Total de Tablas Documentadas:", data.TotalTables.ToString(), primaryColor);
            AddSummaryRow(metadataTable, "Total de Atributos / Columnas:", data.TotalColumns.ToString(), primaryColor);

            body.Append(metadataTable);

            // Salto de página para el cuerpo
            body.Append(new Paragraph(
                new Run(new Break { Type = BreakValues.Page })
            ));
        }

        private void AddSummaryRow(Table table, string label, string value, string primaryColor)
        {
            var row = new TableRow(
                new TableRowProperties(new CantSplit()),
                new TableCell(
                    new TableCellProperties(
                        new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = "3200" },
                        new Shading { Val = ShadingPatternValues.Clear, Fill = "F9FAFC" }
                    ),
                    new Paragraph(
                        new ParagraphProperties(new SpacingBetweenLines { After = "0", Line = "240" }),
                        new Run(
                            new RunProperties(
                                new RunFonts { Ascii = PrimaryFont, HighAnsi = PrimaryFont },
                                new Bold(),
                                new Color { Val = primaryColor },
                                new FontSize { Val = "19" }
                            ),
                            new Text(label)
                        )
                    )
                ),
                new TableCell(
                    new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = "6160" }),
                    new Paragraph(
                        new ParagraphProperties(new SpacingBetweenLines { After = "0", Line = "240" }),
                        new Run(
                            new RunProperties(
                                new RunFonts { Ascii = PrimaryFont, HighAnsi = PrimaryFont },
                                new Color { Val = TextDark },
                                new FontSize { Val = "19" }
                            ),
                            new Text(value)
                        )
                    )
                )
            );
            table.Append(row);
        }

        private void AddSchemaHeader(Body body, string schemaName, string primaryColor, bool isFirst)
        {
            var paraProps = new ParagraphProperties(
                new KeepNext(),
                new SpacingBetweenLines { Before = isFirst ? "120" : "360", After = "140", Line = "280" }
            );

            var para = new Paragraph(
                paraProps,
                new Run(
                    new RunProperties(
                        new RunFonts { Ascii = PrimaryFont, HighAnsi = PrimaryFont },
                        new Bold(),
                        new Color { Val = primaryColor },
                        new FontSize { Val = "30" } // 15pt
                    ),
                    new Text($"MÓDULO / ESQUEMA: {schemaName.ToUpperInvariant()}")
                )
            );

            body.Append(para);
        }

        private void AddTableSubtitle(Body body, string schemaName, string tableName)
        {
            var paraProps = new ParagraphProperties(
                new KeepNext(),
                new SpacingBetweenLines { Before = "180", After = "60", Line = "240" }
            );

            var para = new Paragraph(
                paraProps,
                new Run(
                    new RunProperties(
                        new RunFonts { Ascii = PrimaryFont, HighAnsi = PrimaryFont },
                        new Bold(),
                        new Color { Val = TextDark },
                        new FontSize { Val = "23" } // 11.5pt
                    ),
                    new Text($"{schemaName}.{tableName}:")
                )
            );

            body.Append(para);
        }

        private void AddTableDescription(Body body, string description, string primaryColor)
        {
            var paraProps = new ParagraphProperties(
                new KeepNext(),
                new SpacingBetweenLines { Before = "0", After = "120", Line = "240" }
            );

            var para = new Paragraph(paraProps);

            para.Append(new Run(
                new RunProperties(
                    new RunFonts { Ascii = PrimaryFont, HighAnsi = PrimaryFont },
                    new Bold(),
                    new Color { Val = primaryColor },
                    new FontSize { Val = "19" }
                ),
                new Text("Descripción: ")
            ));

            para.Append(new Run(
                new RunProperties(
                    new RunFonts { Ascii = PrimaryFont, HighAnsi = PrimaryFont },
                    new Color { Val = CorporateSlate },
                    new FontSize { Val = "19" }
                ),
                new Text(description)
            ));

            body.Append(para);
        }

        private void AddAttributesTable(Body body, TableMetadata tableMeta, string primaryColor)
        {
            var table = new Table(
                new TableProperties(
                    new TableWidth { Type = TableWidthUnitValues.Dxa, Width = "9360" },
                    new TableBorders(
                        new TopBorder { Val = BorderValues.Single, Size = 6, Color = BorderColor },
                        new LeftBorder { Val = BorderValues.Single, Size = 4, Color = BorderColor },
                        new BottomBorder { Val = BorderValues.Single, Size = 6, Color = BorderColor },
                        new RightBorder { Val = BorderValues.Single, Size = 4, Color = BorderColor },
                        new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = BorderColor },
                        new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = BorderColor }
                    ),
                    new TableCellMarginDefault(
                        new TopMargin { Width = "100", Type = TableWidthUnitValues.Dxa },
                        new TableCellLeftMargin { Width = 140, Type = TableWidthValues.Dxa },
                        new BottomMargin { Width = "100", Type = TableWidthUnitValues.Dxa },
                        new TableCellRightMargin { Width = 140, Type = TableWidthValues.Dxa }
                    )
                ),
                new TableGrid(
                    new GridColumn { Width = ColWidthCampo.ToString() },
                    new GridColumn { Width = ColWidthTipo.ToString() },
                    new GridColumn { Width = ColWidthLong.ToString() },
                    new GridColumn { Width = ColWidthDesc.ToString() }
                )
            );

            // Fila de encabezado (Campo 22%, Tipo 15%, Long 10%, Descripción 53%)
            string headerBg = "EBF2F7";
            var headerRow = new TableRow(
                new TableRowProperties(
                    new TableHeader(),
                    new CantSplit()
                ),
                CreateHeaderCell("Campo", ColWidthCampo, JustificationValues.Left, headerBg, primaryColor),
                CreateHeaderCell("Tipo", ColWidthTipo, JustificationValues.Left, headerBg, primaryColor),
                CreateHeaderCell("Long", ColWidthLong, JustificationValues.Center, headerBg, primaryColor),
                CreateHeaderCell("Descripción", ColWidthDesc, JustificationValues.Left, headerBg, primaryColor)
            );
            table.Append(headerRow);

            // Filas de datos
            int rowIndex = 0;
            foreach (var col in tableMeta.Columns)
            {
                string rowBg = (rowIndex % 2 == 1) ? ZebraBackground : "FFFFFF";

                var dataRow = new TableRow(
                    new TableRowProperties(new CantSplit()),
                    CreateDataCell(col.DisplayName, ColWidthCampo, JustificationValues.Left, rowBg, isBold: col.IsPrimaryKey, textColor: col.IsPrimaryKey ? primaryColor : TextDark),
                    CreateDataCell(col.DisplayDataType, ColWidthTipo, JustificationValues.Left, rowBg, isBold: false, textColor: CorporateSlate),
                    CreateDataCell(col.Length, ColWidthLong, JustificationValues.Center, rowBg, isBold: false, textColor: CorporateSlate),
                    CreateDataCell(col.FullDescription, ColWidthDesc, JustificationValues.Both, rowBg, isBold: false, textColor: TextDark)
                );

                table.Append(dataRow);
                rowIndex++;
            }

            body.Append(table);
        }

        private TableCell CreateHeaderCell(string text, int widthDxa, JustificationValues align, string bgColor, string primaryColor)
        {
            return new TableCell(
                new TableCellProperties(
                    new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = widthDxa.ToString() },
                    new Shading { Val = ShadingPatternValues.Clear, Fill = bgColor }
                ),
                new Paragraph(
                    new ParagraphProperties(
                        new KeepNext(),
                        new SpacingBetweenLines { Before = "40", After = "40", Line = "220" },
                        new Justification { Val = align }
                    ),
                    new Run(
                        new RunProperties(
                            new RunFonts { Ascii = PrimaryFont, HighAnsi = PrimaryFont },
                            new Bold(),
                            new Color { Val = primaryColor },
                            new FontSize { Val = "19" }
                        ),
                        new Text(text)
                    )
                )
            );
        }

        private TableCell CreateDataCell(string text, int widthDxa, JustificationValues align, string bgColor, bool isBold, string textColor)
        {
            var cellProps = new TableCellProperties(
                new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = widthDxa.ToString() }
            );

            if (!string.IsNullOrEmpty(bgColor) && bgColor != "FFFFFF")
            {
                cellProps.Append(new Shading { Val = ShadingPatternValues.Clear, Fill = bgColor });
            }

            var runProps = new RunProperties(
                new RunFonts { Ascii = PrimaryFont, HighAnsi = PrimaryFont }
            );

            if (isBold)
            {
                runProps.Append(new Bold());
            }

            runProps.Append(new Color { Val = textColor });
            runProps.Append(new FontSize { Val = "18" });

            return new TableCell(
                cellProps,
                new Paragraph(
                    new ParagraphProperties(
                        new SpacingBetweenLines { Before = "30", After = "30", Line = "210" },
                        new Justification { Val = align }
                    ),
                    new Run(runProps, new Text(text))
                )
            );
        }

        private Paragraph CreateImageParagraph(string relationshipId, double widthMm, double heightMm)
        {
            long cx = (long)(widthMm * 36000);
            long cy = (long)(heightMm * 36000);

            var element = new Drawing(
                new DW.Inline(
                    new DW.Extent { Cx = cx, Cy = cy },
                    new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                    new DW.DocProperties { Id = 1U, Name = "Logotipo" },
                    new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                    new A.Graphic(
                        new A.GraphicData(
                            new PIC.Picture(
                                new PIC.NonVisualPictureProperties(
                                    new PIC.NonVisualDrawingProperties { Id = 0U, Name = "Logo.png" },
                                    new PIC.NonVisualPictureDrawingProperties()
                                ),
                                new PIC.BlipFill(
                                    new A.Blip { Embed = relationshipId, CompressionState = A.BlipCompressionValues.Print },
                                    new A.Stretch(new A.FillRectangle())
                                ),
                                new PIC.ShapeProperties(
                                    new A.Transform2D(
                                        new A.Offset { X = 0L, Y = 0L },
                                        new A.Extents { Cx = cx, Cy = cy }
                                    ),
                                    new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }
                                )
                            )
                        )
                        { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }
                    )
                )
                {
                    DistanceFromTop = 0U,
                    DistanceFromBottom = 0U,
                    DistanceFromLeft = 0U,
                    DistanceFromRight = 0U
                }
            );

            return new Paragraph(
                new ParagraphProperties(
                    new SpacingBetweenLines { After = "120" },
                    new Justification { Val = JustificationValues.Left }
                ),
                new Run(element)
            );
        }

        private void AddDeprecatedFieldsSection(Body body, System.Collections.Generic.List<DeprecatedFieldItem> deprecatedFields, string primaryColor)
        {
            // Salto de página para la sección formal de campos deprecados
            body.Append(new Paragraph(
                new Run(new Break { Type = BreakValues.Page })
            ));

            // Título de Sección
            var paraProps = new ParagraphProperties(
                new KeepNext(),
                new SpacingBetweenLines { Before = "120", After = "160", Line = "280" }
            );

            body.Append(new Paragraph(
                paraProps,
                new Run(
                    new RunProperties(
                        new RunFonts { Ascii = PrimaryFont, HighAnsi = PrimaryFont },
                        new Bold(),
                        new Color { Val = primaryColor },
                        new FontSize { Val = "30" } // 15pt
                    ),
                    new Text("HISTORIAL DE MODIFICACIONES Y CAMPOS DEPRECADOS / ELIMINADOS")
                )
            ));

            // Párrafo introductorio
            body.Append(new Paragraph(
                new ParagraphProperties(new SpacingBetweenLines { Before = "0", After = "180", Line = "240" }),
                new Run(
                    new RunProperties(
                        new RunFonts { Ascii = PrimaryFont, HighAnsi = PrimaryFont },
                        new Color { Val = CorporateSlate },
                        new FontSize { Val = "19" }
                    ),
                    new Text("Registro formal de control de cambios y auditoría de esquema sobre columnas que fueron eliminadas o dadas de baja en las tablas físicas de la base de datos.")
                )
            ));

            if (deprecatedFields == null || deprecatedFields.Count == 0)
            {
                // Texto sutil si no hay campos eliminados
                body.Append(new Paragraph(
                    new ParagraphProperties(
                        new SpacingBetweenLines { Before = "60", After = "240", Line = "240" },
                        new Justification { Val = JustificationValues.Left }
                    ),
                    new Run(
                        new RunProperties(
                            new RunFonts { Ascii = PrimaryFont, HighAnsi = PrimaryFont },
                            new Italic(),
                            new Color { Val = "64748B" },
                            new FontSize { Val = "19" }
                        ),
                        new Text("No se registran campos eliminados o deprecados en este período.")
                    )
                ));
                return;
            }

            // Anchos de columna: Esquema/Tabla (1872), Campo (1685), Tipo (1310), Fecha (1123), Motivo (2246), Responsable (1124) = 9360 dxa
            const int wTbl = 1872;
            const int wCol = 1685;
            const int wType = 1310;
            const int wDate = 1123;
            const int wReason = 2246;
            const int wUser = 1124;

            var table = new Table(
                new TableProperties(
                    new TableWidth { Type = TableWidthUnitValues.Dxa, Width = "9360" },
                    new TableBorders(
                        new TopBorder { Val = BorderValues.Single, Size = 6, Color = BorderColor },
                        new LeftBorder { Val = BorderValues.Single, Size = 4, Color = BorderColor },
                        new BottomBorder { Val = BorderValues.Single, Size = 6, Color = BorderColor },
                        new RightBorder { Val = BorderValues.Single, Size = 4, Color = BorderColor },
                        new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = BorderColor },
                        new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = BorderColor }
                    ),
                    new TableCellMarginDefault(
                        new TopMargin { Width = "100", Type = TableWidthUnitValues.Dxa },
                        new TableCellLeftMargin { Width = 140, Type = TableWidthValues.Dxa },
                        new BottomMargin { Width = "100", Type = TableWidthUnitValues.Dxa },
                        new TableCellRightMargin { Width = 140, Type = TableWidthValues.Dxa }
                    )
                ),
                new TableGrid(
                    new GridColumn { Width = wTbl.ToString() },
                    new GridColumn { Width = wCol.ToString() },
                    new GridColumn { Width = wType.ToString() },
                    new GridColumn { Width = wDate.ToString() },
                    new GridColumn { Width = wReason.ToString() },
                    new GridColumn { Width = wUser.ToString() }
                )
            );

            // Fila de encabezado
            string headerBg = "EBF2F7";
            var headerRow = new TableRow(
                new TableRowProperties(
                    new TableHeader(),
                    new CantSplit()
                ),
                CreateHeaderCell("Esquema / Tabla", wTbl, JustificationValues.Left, headerBg, primaryColor),
                CreateHeaderCell("Campo Eliminado", wCol, JustificationValues.Left, headerBg, primaryColor),
                CreateHeaderCell("Tipo Original", wType, JustificationValues.Left, headerBg, primaryColor),
                CreateHeaderCell("Fecha", wDate, JustificationValues.Center, headerBg, primaryColor),
                CreateHeaderCell("Motivo de Eliminación", wReason, JustificationValues.Left, headerBg, primaryColor),
                CreateHeaderCell("Responsable", wUser, JustificationValues.Center, headerBg, primaryColor)
            );
            table.Append(headerRow);

            // Filas de datos
            int rowIndex = 0;
            foreach (var dep in deprecatedFields)
            {
                string rowBg = (rowIndex % 2 == 1) ? ZebraBackground : "FFFFFF";

                string typeFormatted = string.IsNullOrWhiteSpace(dep.LongitudOriginal) 
                    ? dep.TipoDatoOriginal 
                    : $"{dep.TipoDatoOriginal} ({dep.LongitudOriginal})";

                var dataRow = new TableRow(
                    new TableRowProperties(new CantSplit()),
                    CreateDataCell(dep.FullTableName, wTbl, JustificationValues.Left, rowBg, isBold: false, textColor: TextDark),
                    CreateDataCell(dep.Campo, wCol, JustificationValues.Left, rowBg, isBold: true, textColor: "B91C1C"), // Rojo para destacar eliminación
                    CreateDataCell(typeFormatted, wType, JustificationValues.Left, rowBg, isBold: false, textColor: CorporateSlate),
                    CreateDataCell(dep.FechaEliminacion.ToString("yyyy-MM-dd"), wDate, JustificationValues.Center, rowBg, isBold: false, textColor: CorporateSlate),
                    CreateDataCell(dep.MotivoEliminacion, wReason, JustificationValues.Both, rowBg, isBold: false, textColor: TextDark),
                    CreateDataCell(string.IsNullOrWhiteSpace(dep.UsuarioResponsable) ? "-" : dep.UsuarioResponsable, wUser, JustificationValues.Center, rowBg, isBold: false, textColor: CorporateSlate)
                );

                table.Append(dataRow);
                rowIndex++;
            }

            body.Append(table);
        }

        private void AddVersionHistorySection(Body body, System.Collections.Generic.List<ColumnVersionHistoryItem> versionHistory, string primaryColor)
        {
            // Salto de página para la sección formal de control de versiones
            body.Append(new Paragraph(
                new Run(new Break { Type = BreakValues.Page })
            ));

            // Título de Sección
            var paraProps = new ParagraphProperties(
                new KeepNext(),
                new SpacingBetweenLines { Before = "120", After = "160", Line = "280" }
            );

            body.Append(new Paragraph(
                paraProps,
                new Run(
                    new RunProperties(
                        new RunFonts { Ascii = PrimaryFont, HighAnsi = PrimaryFont },
                        new Bold(),
                        new Color { Val = primaryColor },
                        new FontSize { Val = "30" } // 15pt
                    ),
                    new Text("HISTORIAL DE VERSIONES Y MODIFICACIONES ESTRUCTURALES")
                )
            ));

            // Párrafo introductorio
            body.Append(new Paragraph(
                new ParagraphProperties(new SpacingBetweenLines { Before = "0", After = "180", Line = "240" }),
                new Run(
                    new RunProperties(
                        new RunFonts { Ascii = PrimaryFont, HighAnsi = PrimaryFont },
                        new Color { Val = CorporateSlate },
                        new FontSize { Val = "19" }
                    ),
                    new Text("Registro cronológico y formal de auditoría sobre alteraciones estructurales (cambios de tipo de dato, variaciones de longitud, modificaciones de escala) aprobadas en el catálogo de datos.")
                )
            ));

            if (versionHistory == null || versionHistory.Count == 0)
            {
                body.Append(new Paragraph(
                    new ParagraphProperties(
                        new SpacingBetweenLines { Before = "60", After = "240", Line = "240" },
                        new Justification { Val = JustificationValues.Left }
                    ),
                    new Run(
                        new RunProperties(
                            new RunFonts { Ascii = PrimaryFont, HighAnsi = PrimaryFont },
                            new Italic(),
                            new Color { Val = "64748B" },
                            new FontSize { Val = "19" }
                        ),
                        new Text("No se registran modificaciones estructurales aprobadas en el historial de versiones del catálogo.")
                    )
                ));
                return;
            }

            // Anchos de columna: Tabla/Campo (2050), Propiedad (1200), Anterior (1200), Nuevo (1200), Fecha/Resp (1510), Justificación (2200) = 9360 dxa
            const int wColObj = 2050;
            const int wColProp = 1200;
            const int wColOld = 1200;
            const int wColNew = 1200;
            const int wColDateUser = 1510;
            const int wColReason = 2200;

            var table = new Table(
                new TableProperties(
                    new TableWidth { Type = TableWidthUnitValues.Dxa, Width = "9360" },
                    new TableBorders(
                        new TopBorder { Val = BorderValues.Single, Size = 6, Color = BorderColor },
                        new LeftBorder { Val = BorderValues.Single, Size = 4, Color = BorderColor },
                        new BottomBorder { Val = BorderValues.Single, Size = 6, Color = BorderColor },
                        new RightBorder { Val = BorderValues.Single, Size = 4, Color = BorderColor },
                        new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = BorderColor },
                        new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = BorderColor }
                    ),
                    new TableCellMarginDefault(
                        new TopMargin { Width = "100", Type = TableWidthUnitValues.Dxa },
                        new TableCellLeftMargin { Width = 140, Type = TableWidthValues.Dxa },
                        new BottomMargin { Width = "100", Type = TableWidthUnitValues.Dxa },
                        new TableCellRightMargin { Width = 140, Type = TableWidthValues.Dxa }
                    )
                ),
                new TableGrid(
                    new GridColumn { Width = wColObj.ToString() },
                    new GridColumn { Width = wColProp.ToString() },
                    new GridColumn { Width = wColOld.ToString() },
                    new GridColumn { Width = wColNew.ToString() },
                    new GridColumn { Width = wColDateUser.ToString() },
                    new GridColumn { Width = wColReason.ToString() }
                )
            );

            // Fila de encabezado
            string headerBg = "EBF2F7";
            var headerRow = new TableRow(
                new TableRowProperties(
                    new TableHeader(),
                    new CantSplit()
                ),
                CreateHeaderCell("Tabla / Campo", wColObj, JustificationValues.Left, headerBg, primaryColor),
                CreateHeaderCell("Propiedad", wColProp, JustificationValues.Left, headerBg, primaryColor),
                CreateHeaderCell("Valor Anterior", wColOld, JustificationValues.Left, headerBg, primaryColor),
                CreateHeaderCell("Valor Nuevo", wColNew, JustificationValues.Left, headerBg, primaryColor),
                CreateHeaderCell("Fecha / Usuario", wColDateUser, JustificationValues.Center, headerBg, primaryColor),
                CreateHeaderCell("Motivo del Cambio", wColReason, JustificationValues.Left, headerBg, primaryColor)
            );
            table.Append(headerRow);

            int rowIndex = 0;
            foreach (var v in versionHistory)
            {
                string rowBg = (rowIndex % 2 == 1) ? ZebraBackground : "FFFFFF";

                string targetName = $"{v.FullTableName}.{v.Campo}";
                string dateAndUser = $"{v.FechaCambio:yyyy-MM-dd} ({v.UsuarioResponsable})";

                var dataRow = new TableRow(
                    new TableRowProperties(new CantSplit()),
                    CreateDataCell(targetName, wColObj, JustificationValues.Left, rowBg, isBold: false, textColor: TextDark),
                    CreateDataCell(v.PropiedadModificada, wColProp, JustificationValues.Left, rowBg, isBold: true, textColor: primaryColor),
                    CreateDataCell(v.ValorAnterior, wColOld, JustificationValues.Left, rowBg, isBold: false, textColor: "9A3412"),
                    CreateDataCell(v.ValorNuevo, wColNew, JustificationValues.Left, rowBg, isBold: true, textColor: "15803D"),
                    CreateDataCell(dateAndUser, wColDateUser, JustificationValues.Center, rowBg, isBold: false, textColor: CorporateSlate),
                    CreateDataCell(v.MotivoCambio, wColReason, JustificationValues.Both, rowBg, isBold: false, textColor: TextDark)
                );

                table.Append(dataRow);
                rowIndex++;
            }

            body.Append(table);
        }

        private static string CleanHex(string hex, string defaultHex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return defaultHex;
            hex = hex.Trim().TrimStart('#');
            return hex.Length == 6 ? hex : defaultHex;
        }
    }
}

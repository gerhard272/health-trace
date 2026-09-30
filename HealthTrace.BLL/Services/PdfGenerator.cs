using HealthTrace.BLL.Models;
using HealthTrace.BLL.Services.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HealthTrace.BLL.Services
{
    public class PdfGenerator : IPdfGenerator
    {
        static PdfGenerator()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public byte[] GenerateSymptomReport(IReadOnlyList<SymptomModel> symptoms, 
            DateTime generatedAtUtc)
        {
            var document = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Column(col =>
                    {
                        col.Item().Text("Symptom diary - HealthTrace")
                        .SemiBold()
                        .FontSize(18);
                        col.Item().Text($"Generated on {generatedAtUtc:dd/MM/yyyy HH:mm} UTC")
                        .FontSize(9)
                        .FontColor(Colors.Grey.Darken1);
                    });

                    page.Content().PaddingVertical(15).Element(content =>
                    {
                        if (symptoms.Count == 0)
                        {
                            content.Text("No symptoms match the selected criteria.");
                            return;
                        }

                        content.Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(95);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(3);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCell).Text("Date").SemiBold();
                                header.Cell().Element(HeaderCell).Text("Event").SemiBold();
                                header.Cell().Element(HeaderCell).Text("Description").SemiBold();
                            });

                            foreach (var symptom in symptoms)
                            {
                                table.Cell().Element(BodyCell)
                                    .Text(symptom.EventDate.ToString("dd/MM/yyyy HH:mm"));
                                table.Cell().Element(BodyCell)
                                    .Text(symptom.EventName ?? string.Empty);
                                table.Cell().Element(BodyCell)
                                    .Text(symptom.Description ?? string.Empty);
                            }
                        });
                    });

                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("Page ");
                        text.CurrentPageNumber();
                        text.Span(" of ");
                        text.TotalPages();
                    });
                });
            });

            return document.GeneratePdf();
        }

        private static IContainer HeaderCell(IContainer container) =>
            container.Background(Colors.Grey.Lighten3)
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Medium)
            .Padding(5);

        private static IContainer BodyCell(IContainer container) =>
            container.BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(5);
    }
}
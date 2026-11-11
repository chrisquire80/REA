using System.Drawing.Printing;

namespace NotepadREA;

public static class PrintHelper
{
    public static void Print(Document doc, IWin32Window owner, bool isPreview)
    {
        if (doc == null) return;

        using var pd = new PrintDocument();
        pd.DocumentName = doc.DisplayName;

        string text = doc.Editor.Text;
        string[] lines = text.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        int currentLineIndex = 0;
        int pageNumber = 0;

        using var bodyFont = new Font("Consolas", 9.5f);
        using var headerFont = new Font("Segoe UI", 8.5f, FontStyle.Italic);
        using var footerFont = new Font("Segoe UI", 8.5f);

        pd.PrintPage += (_, e) =>
        {
            pageNumber++;
            var g = e.Graphics;
            if (g == null) return;

            var bounds = e.MarginBounds;

            // Header
            string headerText = $"{doc.DisplayName}   -   {DateTime.Now:yyyy-MM-dd HH:mm}";
            g.DrawString(headerText, headerFont, Brushes.Gray, bounds.Left, bounds.Top - 25);
            g.DrawLine(Pens.LightGray, bounds.Left, bounds.Top - 10, bounds.Right, bounds.Top - 10);

            // Footer
            string footerText = $"Pagina {pageNumber}";
            var footerSize = g.MeasureString(footerText, footerFont);
            g.DrawString(footerText, footerFont, Brushes.Gray, bounds.Right - footerSize.Width, bounds.Bottom + 10);
            g.DrawLine(Pens.LightGray, bounds.Left, bounds.Bottom + 5, bounds.Right, bounds.Bottom + 5);

            float y = bounds.Top;
            float lineHeight = bodyFont.GetHeight(g) + 2;

            while (currentLineIndex < lines.Length)
            {
                if (y + lineHeight > bounds.Bottom)
                {
                    e.HasMorePages = true;
                    return;
                }

                string line = lines[currentLineIndex];
                string lineNum = $"{currentLineIndex + 1,4} | ";
                var numSize = g.MeasureString(lineNum, bodyFont);

                g.DrawString(lineNum, bodyFont, Brushes.Gray, bounds.Left, y);
                g.DrawString(line, bodyFont, Brushes.Black, bounds.Left + numSize.Width, y);

                y += lineHeight;
                currentLineIndex++;
            }

            e.HasMorePages = false;
        };

        if (isPreview)
        {
            using var previewDlg = new PrintPreviewDialog
            {
                Document = pd,
                Width = 900,
                Height = 650,
                StartPosition = FormStartPosition.CenterParent
            };
            previewDlg.ShowDialog(owner);
        }
        else
        {
            using var printDlg = new PrintDialog
            {
                Document = pd,
                AllowSomePages = true,
                UseEXDialog = true
            };
            if (printDlg.ShowDialog(owner) == DialogResult.OK)
            {
                pd.Print();
            }
        }
    }
}

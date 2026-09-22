using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using STOW.Infrastructure.Updates;

namespace STOW.App.Views;

public partial class ReleaseNotesWindow : Window
{
    private static readonly Regex MarkdownLink = new(
        @"\[([^\]]+)\]\(([^)]+)\)",
        RegexOptions.Compiled);

    private static readonly Regex NumberedItem = new(
        @"^\s*\d+[.)]\s+",
        RegexOptions.Compiled);

    private readonly ReleaseInfo release;

    public ReleaseNotesWindow(ReleaseInfo release)
    {
        this.release = release;
        InitializeComponent();

        ReleaseTitleText.Text = string.IsNullOrWhiteSpace(release.Name)
            ? $"STOW {release.Version.Original}"
            : release.Name;

        string channel = release.IsPrerelease ? "Preview" : "Stable";
        string published = release.PublishedAt is null
            ? "Published date unavailable"
            : "Published " + release.PublishedAt.Value.ToLocalTime().ToString("g");
        ReleaseMetaText.Text = $"{release.Tag} · {channel} · {published}";

        RenderMarkdown(release.Body);
    }

    private void RenderMarkdown(string? markdown)
    {
        NotesDocument.Blocks.Clear();

        if (string.IsNullOrWhiteSpace(markdown))
        {
            NotesDocument.Blocks.Add(new Paragraph(new Run("No release notes were provided for this release.")));
            return;
        }

        foreach (string raw in markdown.Replace("\r", string.Empty).Split('\n'))
        {
            string line = raw.TrimEnd();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (line.StartsWith("---", StringComparison.Ordinal))
            {
                NotesDocument.Blocks.Add(new Paragraph { Margin = new Thickness(0, 8, 0, 8) });
                continue;
            }

            int headingLevel = HeadingLevel(line);
            if (headingLevel > 0)
            {
                string heading = line[(headingLevel + 1)..].Trim();
                var paragraph = new Paragraph(new Run(CleanInlineMarkdown(heading)))
                {
                    FontWeight = FontWeights.SemiBold,
                    FontSize = headingLevel switch
                    {
                        1 => 22,
                        2 => 18,
                        _ => 15
                    },
                    Margin = new Thickness(0, headingLevel == 1 ? 8 : 14, 0, 6)
                };
                NotesDocument.Blocks.Add(paragraph);
                continue;
            }

            bool bullet = line.TrimStart().StartsWith("- ", StringComparison.Ordinal) ||
                          line.TrimStart().StartsWith("* ", StringComparison.Ordinal);
            bool numbered = NumberedItem.IsMatch(line);

            string text = line.Trim();
            if (bullet)
                text = "• " + text[2..].Trim();
            else if (numbered)
                text = NumberedItem.Replace(text, string.Empty);

            var body = new Paragraph(new Run(CleanInlineMarkdown(text)))
            {
                Margin = new Thickness(bullet || numbered ? 12 : 0, 3, 0, 5),
                LineHeight = 21
            };
            NotesDocument.Blocks.Add(body);
        }
    }

    private static int HeadingLevel(string line)
    {
        string trimmed = line.TrimStart();
        int count = 0;
        while (count < trimmed.Length && trimmed[count] == '#' && count < 3)
            count++;
        return count > 0 && count < trimmed.Length && trimmed[count] == ' '
            ? count
            : 0;
    }

    private static string CleanInlineMarkdown(string value)
    {
        string result = MarkdownLink.Replace(value, "$1");
        result = result.Replace("**", string.Empty)
                       .Replace("__", string.Empty)
                       .Replace(((char)96).ToString(), string.Empty);
        return result;
    }

    private void OpenGitHub_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = release.HtmlUri.AbsoluteUri,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Could not open the release page.\n\n" + ex.Message,
                "STOW",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

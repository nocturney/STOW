using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;

namespace STOW.App.Views;

public partial class LegalDocumentWindow : Window
{
    private readonly string? legalFolder;

    public LegalDocumentWindow(string title, string content, string? legalFolder = null)
    {
        this.legalFolder = legalFolder;
        InitializeComponent();

        Title = "STOW - " + title;
        DocumentTitleText.Text = title;
        DocumentTextBox.Text = content;

        if (!string.IsNullOrWhiteSpace(legalFolder) && Directory.Exists(legalFolder))
            OpenLegalFolderButton.Visibility = Visibility.Visible;
    }

    public static string ReadEmbedded(string resourceName)
    {
        Assembly assembly = typeof(LegalDocumentWindow).Assembly;
        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            return $"Bundled document '{resourceName}' is unavailable in this build.";

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    public static string BuildNoticesBundle()
    {
        string[] names =
        [
            "STOW.Legal.THIRD_PARTY_NOTICES.md",
            "STOW.Legal.CREDITS.md",
            "STOW.Legal.LEGAL_MANIFEST.txt",
            "STOW.Legal.DOTNET_RUNTIME_LICENSE.txt",
            "STOW.Legal.DOTNET_RUNTIME_THIRD_PARTY_NOTICES.txt",
            "STOW.Legal.DOTNET_WINDOWS_DESKTOP_LICENSE.txt",
            "STOW.Legal.MICROSOFT_LICENSE_REFERENCES.md"
        ];

        Assembly assembly = typeof(LegalDocumentWindow).Assembly;
        var sections = new List<string>();

        foreach (string name in names)
        {
            using Stream? stream = assembly.GetManifestResourceStream(name);
            if (stream is null)
                continue;

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            string label = name["STOW.Legal.".Length..];
            sections.Add($"===== {label} ====={Environment.NewLine}{reader.ReadToEnd().Trim()}");
        }

        string? dotnetTerms = ReadEmbeddedHtmlAsText(
            assembly,
            "STOW.Legal.MICROSOFT_DOTNET_LIBRARY_LICENSE.html");
        if (!string.IsNullOrWhiteSpace(dotnetTerms))
            sections.Add(
                "===== MICROSOFT .NET LIBRARY LICENSE =====" +
                Environment.NewLine +
                dotnetTerms);

        string? windowsSdkTerms = ReadEmbeddedHtmlAsText(
            assembly,
            "STOW.Legal.MICROSOFT_WINDOWS_SDK_LICENSE.html");
        if (!string.IsNullOrWhiteSpace(windowsSdkTerms))
            sections.Add(
                "===== MICROSOFT WINDOWS SDK LICENSE =====" +
                Environment.NewLine +
                windowsSdkTerms);

        if (sections.Count == 0)
            return "No bundled third-party notices were found in this development build.";

        return string.Join(Environment.NewLine + Environment.NewLine, sections);
    }

    private static string? ReadEmbeddedHtmlAsText(Assembly assembly, string resourceName)
    {
        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            return null;

        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);
        string html = reader.ReadToEnd();

        html = Regex.Replace(
            html,
            @"(?is)<(script|style)[^>]*>.*?</\1>",
            string.Empty);
        html = Regex.Replace(
            html,
            @"(?i)<br\s*/?>",
            Environment.NewLine);
        html = Regex.Replace(
            html,
            @"(?i)</(p|div|h[1-6]|li|tr)>",
            Environment.NewLine);
        html = Regex.Replace(html, @"(?s)<[^>]+>", " ");
        html = WebUtility.HtmlDecode(html);
        html = Regex.Replace(html, @"[ \t]+", " ");
        html = Regex.Replace(
            html,
            @"(\r?\n\s*){3,}",
            Environment.NewLine + Environment.NewLine);

        return html.Trim();
    }

    private void CopyAll_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(DocumentTextBox.Text);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Could not copy the document.\n\n" + ex.Message,
                "STOW",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OpenLegalFolder_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(legalFolder) || !Directory.Exists(legalFolder))
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = legalFolder,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Could not open the legal folder.\n\n" + ex.Message,
                "STOW",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

using System.Windows;
using STOW.Infrastructure.Configuration;

namespace STOW.App.Views;

public partial class LegalAcceptanceWindow : Window
{
    public LegalAcceptanceWindow()
    {
        InitializeComponent();
        RevisionText.Text = "Terms revision " + LegalAcceptanceStore.CurrentRevision;
    }

    private void Acceptance_Changed(object sender, RoutedEventArgs e)
    {
        ContinueButton.IsEnabled = AcceptCheckBox.IsChecked == true;
    }

    private void License_Click(object sender, RoutedEventArgs e) =>
        ShowDocument(
            "STOW License",
            LegalDocumentWindow.ReadEmbedded("STOW.Legal.STOW_LICENSE.txt"));

    private void Notices_Click(object sender, RoutedEventArgs e) =>
        ShowDocument(
            "Open-source notices & credits",
            LegalDocumentWindow.BuildNoticesBundle());

    private void Privacy_Click(object sender, RoutedEventArgs e) =>
        ShowDocument(
            "Privacy",
            LegalDocumentWindow.ReadEmbedded("STOW.Legal.PRIVACY.md"));

    private void ShowDocument(string title, string content)
    {
        var window = new LegalDocumentWindow(title, content)
        {
            Owner = this
        };
        window.ShowDialog();
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        if (AcceptCheckBox.IsChecked != true)
            return;

        DialogResult = true;
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}

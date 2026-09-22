using System.Windows;

namespace STOW.App.Views;

public partial class FocusPresetNameWindow : Window
{
    public string PresetName => NameTextBox.Text.Trim();

    public FocusPresetNameWindow(string suggestedName)
    {
        InitializeComponent();
        NameTextBox.Text = suggestedName;
        Loaded += (_, _) =>
        {
            NameTextBox.SelectAll();
            NameTextBox.Focus();
        };
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PresetName))
        {
            MessageBox.Show(
                "Give the preset a name.",
                "STOW",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}

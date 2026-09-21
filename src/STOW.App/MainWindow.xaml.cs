using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace STOW.App;

public partial class MainWindow : Window
{
    private Button? activeButton;

    public MainWindow()
    {
        InitializeComponent();
        activeButton = AppsNavButton;
    }

    private void Navigate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string destination)
            return;

        if (activeButton is not null)
        {
            activeButton.Background = Brushes.Transparent;
            activeButton.Foreground = (Brush)FindResource("TextSecondaryBrush");
        }

        activeButton = button;
        activeButton.Background = (Brush)FindResource("SelectionBrush");
        activeButton.Foreground = (Brush)FindResource("TextPrimaryBrush");

        (PageTitleText.Text, PageSubtitleText.Text) = destination switch
        {
            "Apps" => ("Apps", "Choose what STOW keeps quietly out of your way."),
            "Rules" => ("Rules", "Automate when and how apps are stowed."),
            "Focus" => ("Focus", "Create a calmer desktop for the task at hand."),
            "Insights" => ("Insights", "Understand local desktop patterns without telemetry."),
            "Settings" => ("Settings", "Adjust STOW behavior and appearance."),
            "About" => ("About", "Version, updates, privacy, diagnostics and project information."),
            _ => (destination, string.Empty)
        };

        BodyTitleText.Text = destination == "About" ? "About is a standalone destination" : $"{destination} screen scaffold";
        BodyText.Text = "The approved final composition will replace this scaffold. " +
                        "The Trayify v0.3.3 minimize-to-tray engine remains untouched while the new UI is built independently.";
    }
}

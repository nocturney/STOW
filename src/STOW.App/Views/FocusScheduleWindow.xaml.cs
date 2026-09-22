using System.Globalization;
using System.Windows;
using STOW.Engine.Contracts;

namespace STOW.App.Views;

public partial class FocusScheduleWindow : Window
{
    public sealed record PresetChoice(string Id, string Name);

    private readonly FocusScheduleDefinition? existing;

    public FocusScheduleDefinition? Schedule { get; private set; }

    public FocusScheduleWindow(
        IReadOnlyList<FocusPresetDefinition> presets,
        FocusScheduleDefinition? existing = null)
    {
        this.existing = existing;
        InitializeComponent();

        PresetChoice[] choices = presets
            .Select(preset => new PresetChoice(preset.Id, preset.Name))
            .OrderBy(choice => choice.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        PresetComboBox.ItemsSource = choices;

        if (existing is null)
        {
            NameTextBox.Text = "Focus schedule";
            StartTimeTextBox.Text = "09:00";
            DurationTextBox.Text = "60";
            MondayCheckBox.IsChecked = true;
            TuesdayCheckBox.IsChecked = true;
            WednesdayCheckBox.IsChecked = true;
            ThursdayCheckBox.IsChecked = true;
            FridayCheckBox.IsChecked = true;
            if (choices.Length > 0)
                PresetComboBox.SelectedIndex = 0;
        }
        else
        {
            HeadingText.Text = "Edit Focus schedule";
            SaveButton.Content = "Save schedule";
            NameTextBox.Text = existing.Name;
            StartTimeTextBox.Text =
                $"{existing.StartMinutesLocal / 60:00}:{existing.StartMinutesLocal % 60:00}";
            DurationTextBox.Text = existing.DurationMinutes.ToString(
                CultureInfo.InvariantCulture);
            PresetComboBox.SelectedValue = existing.PresetId;
            SetDays(existing.Days);
        }

        Loaded += (_, _) =>
        {
            NameTextBox.SelectAll();
            NameTextBox.Focus();
        };
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string name = NameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowValidation("Give the schedule a name.");
            return;
        }

        if (PresetComboBox.SelectedItem is not PresetChoice preset)
        {
            ShowValidation("Choose a saved Focus preset.");
            return;
        }

        if (!TimeOnly.TryParseExact(
            StartTimeTextBox.Text.Trim(),
            "HH:mm",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out TimeOnly start))
        {
            ShowValidation("Enter the start time as HH:mm, for example 09:30.");
            return;
        }
        if (!int.TryParse(
            DurationTextBox.Text.Trim(),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out int duration) ||
            duration is < 5 or > 720)
        {
            ShowValidation("Duration must be between 5 and 720 minutes.");
            return;
        }

        DayOfWeek[] days = GetSelectedDays();
        if (days.Length == 0)
        {
            ShowValidation("Choose at least one day.");
            return;
        }

        int startMinutes = start.Hour * 60 + start.Minute;
        try
        {
            Schedule = existing is null
                ? FocusScheduleDefinition.Create(
                    name,
                    preset.Id,
                    days,
                    startMinutes,
                    duration)
                : existing with
                {
                    Name = name,
                    PresetId = preset.Id,
                    Days = days,
                    StartMinutesLocal = startMinutes,
                    DurationMinutes = duration
                };
        }
        catch (Exception ex)
        {
            ShowValidation(ex.Message);
            return;
        }

        DialogResult = true;
    }

    private DayOfWeek[] GetSelectedDays()
    {
        var days = new List<DayOfWeek>();
        if (SundayCheckBox.IsChecked == true) days.Add(DayOfWeek.Sunday);
        if (MondayCheckBox.IsChecked == true) days.Add(DayOfWeek.Monday);
        if (TuesdayCheckBox.IsChecked == true) days.Add(DayOfWeek.Tuesday);
        if (WednesdayCheckBox.IsChecked == true) days.Add(DayOfWeek.Wednesday);
        if (ThursdayCheckBox.IsChecked == true) days.Add(DayOfWeek.Thursday);
        if (FridayCheckBox.IsChecked == true) days.Add(DayOfWeek.Friday);
        if (SaturdayCheckBox.IsChecked == true) days.Add(DayOfWeek.Saturday);
        return days.ToArray();
    }

    private void SetDays(IReadOnlyList<DayOfWeek> days)
    {
        HashSet<DayOfWeek> selected = days.ToHashSet();
        SundayCheckBox.IsChecked = selected.Contains(DayOfWeek.Sunday);
        MondayCheckBox.IsChecked = selected.Contains(DayOfWeek.Monday);
        TuesdayCheckBox.IsChecked = selected.Contains(DayOfWeek.Tuesday);
        WednesdayCheckBox.IsChecked = selected.Contains(DayOfWeek.Wednesday);
        ThursdayCheckBox.IsChecked = selected.Contains(DayOfWeek.Thursday);
        FridayCheckBox.IsChecked = selected.Contains(DayOfWeek.Friday);
        SaturdayCheckBox.IsChecked = selected.Contains(DayOfWeek.Saturday);
    }

    private static void ShowValidation(string message)
    {
        MessageBox.Show(
            message,
            "STOW",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}

using System.Windows;
using System.Windows.Controls;
using Radial.Core;
using Radial.Models;
using WpfMessageBox = System.Windows.MessageBox;
using ShortcutModel = Radial.Models.Shortcut;

namespace Radial.UI;

public partial class MacroManagerWindow : Window
{
    private readonly ProfileManager _profiles = new();
    private readonly RunningApplicationService _applicationsService = new();
    private readonly MacroRecorder _recorder = new();
    private readonly MacroPlayer _player = new();
    private Macro? _recording;
    private ShortcutModel? _detected;
    private ApplicationProfile? SelectedProfile => Profiles.SelectedItem as ApplicationProfile;
    private RadialWheel? SelectedWheel => Wheels.SelectedItem as RadialWheel;
    public MacroManagerWindow() { InitializeComponent(); _recorder.ShortcutDetected += shortcut => Dispatcher.Invoke(() => { _detected = shortcut; DetectedShortcut.Text = $"Detected shortcut: {shortcut.DisplayText}"; }); _profiles.Load(); Refresh(); Closed += (_, _) => _recorder.Dispose(); }
    private void Refresh() { var profile = SelectedProfile; var wheel = SelectedWheel; Applications.ItemsSource = _applicationsService.GetVisibleApplications(); Profiles.ItemsSource = null; Profiles.ItemsSource = _profiles.Configuration.ApplicationProfiles; Profiles.SelectedItem = profile ?? Profiles.Items.Cast<ApplicationProfile>().FirstOrDefault(); UpdateWheels(wheel); }
    private void UpdateWheels(RadialWheel? preferred = null) { Wheels.ItemsSource = null; Wheels.ItemsSource = SelectedProfile?.Wheels; Wheels.SelectedItem = preferred is not null && SelectedProfile?.Wheels.Contains(preferred) == true ? preferred : Wheels.Items.Cast<RadialWheel>().FirstOrDefault(); UpdateMacroList(); }
    private void UpdateMacroList() { MacroList.ItemsSource = SelectedWheel?.Macros.ToList() ?? new List<Macro>(); }
    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();
    private void Profile_Changed(object sender, SelectionChangedEventArgs e) { if (IsLoaded) UpdateWheels(); }
    private void Wheel_Changed(object sender, SelectionChangedEventArgs e) => UpdateMacroList();
    private void Wheels_RightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var element = e.OriginalSource as DependencyObject;
        while (element is not null && element is not ListBoxItem) element = System.Windows.Media.VisualTreeHelper.GetParent(element);
        if (element is ListBoxItem item) { item.IsSelected = true; item.Focus(); }
    }
    private void Wheels_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (Wheels.SelectedItem is not RadialWheel) { e.Handled = true; return; }
        var menu = new System.Windows.Controls.ContextMenu(); var rename = new System.Windows.Controls.MenuItem { Header = "Rename" }; rename.Click += RenameWheel_Click; menu.Items.Add(rename); var delete = new System.Windows.Controls.MenuItem { Header = "Delete" }; delete.Click += DeleteWheel_Click; menu.Items.Add(delete); Wheels.ContextMenu = menu;
    }
    private void DeleteWheel_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProfile is null || SelectedWheel is not { } wheel) return;
        if (SelectedProfile.Wheels.Count <= 1) { WpfMessageBox.Show("A profile must have at least one wheel.", "Delete wheel"); return; } SelectedProfile.Wheels.Remove(wheel); _profiles.Save(); UpdateWheels(); Status.Text = $"Deleted {wheel.Name}.";
    }
    private void RenameWheel_Click(object sender, RoutedEventArgs e) { if (SelectedWheel is not { } wheel) return; var name = Microsoft.VisualBasic.Interaction.InputBox("New wheel name:", "Rename Wheel", wheel.Name); if (!string.IsNullOrWhiteSpace(name)) { wheel.Name = name.Trim(); _profiles.Save(); UpdateWheels(wheel); } }
    private void AddWheel_Click(object sender, RoutedEventArgs e) { if (SelectedProfile is null) return; var wheel = new RadialWheel { Name = $"Wheel {SelectedProfile.Wheels.Count + 1}" }; SelectedProfile.Wheels.Add(wheel); _profiles.Save(); UpdateWheels(wheel); }
    private void Record_Click(object sender, RoutedEventArgs e) { if (Applications.SelectedItem is not TargetApplicationMetadata target) { WpfMessageBox.Show("Select a running target application first.", "Record Macro"); return; } var profile = _profiles.GetOrCreate(target); Profiles.ItemsSource = _profiles.Configuration.ApplicationProfiles; Profiles.SelectedItem = profile; if (SelectedWheel is null) UpdateWheels(); if (SelectedWheel is null) return; if (SelectedWheel.Macros.Count >= 12) { WpfMessageBox.Show("Maximum of 12 shortcuts per wheel. Create another wheel.", "Wheel limit"); return; } try { _detected = null; DetectedShortcut.Text = "Detected shortcut: —"; _recording = new Macro { TargetApplication = target }; _recorder.Start(); RecordButton.IsEnabled = false; StopButton.IsEnabled = true; RefreshButton.IsEnabled = false; Status.Text = "● RECORDING — press a keyboard shortcut."; } catch (Exception ex) { WpfMessageBox.Show(ex.Message, "Recording error", MessageBoxButton.OK, MessageBoxImage.Error); } }
    private void Stop_Click(object sender, RoutedEventArgs e) { if (_recording is null) return; _recorder.Stop(); var macro = _recording; _recording = null; RecordButton.IsEnabled = true; StopButton.IsEnabled = false; RefreshButton.IsEnabled = true; if (_detected is null) { Status.Text = "No shortcut detected."; return; } macro.Shortcut = _detected; var name = Microsoft.VisualBasic.Interaction.InputBox($"Detected shortcut: {macro.Shortcut.DisplayText}\nName this macro:", "Save Shortcut", "New Macro"); if (!string.IsNullOrWhiteSpace(name) && SelectedWheel is { } wheel && wheel.Macros.Count < 12) { macro.Name = name.Trim(); wheel.Macros.Add(macro); _profiles.Save(); Refresh(); Status.Text = $"Saved {macro.Shortcut.DisplayText}."; } else Status.Text = "Recording cancelled."; }
    private async void Run_Click(object sender, RoutedEventArgs e) { if (MacroList.SelectedItem is not Macro macro) return; try { Status.Text = "Playing…"; await _player.PlayAsync(macro); Status.Text = "Playback complete."; } catch (Exception ex) { WpfMessageBox.Show(ex.Message, "Playback error", MessageBoxButton.OK, MessageBoxImage.Error); } }
    private void Rename_Click(object sender, RoutedEventArgs e) { if (MacroList.SelectedItem is not Macro macro) return; var name = Microsoft.VisualBasic.Interaction.InputBox("New macro name:", "Rename Macro", macro.Name); if (!string.IsNullOrWhiteSpace(name)) { macro.Name = name.Trim(); _profiles.Save(); Refresh(); } }
    private void Delete_Click(object sender, RoutedEventArgs e) { if (MacroList.SelectedItem is not Macro macro || SelectedWheel is not { } wheel) return; if (WpfMessageBox.Show($"Delete '{macro.Name}'?", "Delete Macro", MessageBoxButton.YesNo) == MessageBoxResult.Yes) { wheel.Macros.Remove(macro); _profiles.Save(); Refresh(); } }
    protected override void OnClosed(EventArgs e) { if (_recorder.IsRecording) _recorder.Stop(); base.OnClosed(e); }
}

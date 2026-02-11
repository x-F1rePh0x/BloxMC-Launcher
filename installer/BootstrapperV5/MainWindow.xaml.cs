using System.Media;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace BloxMC.Bootstrapper;

public partial class MainWindow : Window
{
    private const string DefaultEditPassword = "FirePhox-Installer";
    private readonly string _editPassword =
        Environment.GetEnvironmentVariable("BLOXMC_INSTALLER_EDIT_PASSWORD") ?? DefaultEditPassword;

    public event EventHandler? InstallRequested;
    public event EventHandler? RepairRequested;
    public event EventHandler? UninstallRequested;
    public event EventHandler? RetryRequested;
    public event EventHandler? ViewLogsRequested;
    public event EventHandler? CopyErrorRequested;
    public event EventHandler? FinishRequested;
    public event EventHandler? CloseRequested;

    private bool _isInstalled;
    private Action? _confirmAction;
    private string _activeTab = string.Empty;

    public MainWindow()
    {
        InitializeComponent();
        InstallPathTextBox.Text = "%LOCALAPPDATA%\\BloxMC";
        LogPathTextBox.Text = "%TEMP%\\BloxMC-Install.log";
        ShowReadyMessage();
        SelectTab("Install", false);
    }

    public bool LaunchAfterFinish => LaunchAfterFinishCheckBox.IsChecked == true;
    public bool SoundEffectsEnabled => SoundEffectsCheckBox.IsChecked == true;
    public bool VerboseLogsEnabled => VerboseLogsCheckBox.IsChecked == true;
    public bool ReducedMotionEnabled => ReducedMotionCheckBox.IsChecked == true;

    public void SetInstallPath(string path)
    {
        InstallPathTextBox.Text = path;
    }

    public void SetLogPath(string path)
    {
        LogPathTextBox.Text = path;
    }

    public void SetInstalled(bool installed)
    {
        _isInstalled = installed;
        PrimaryActionButton.Content = installed ? "Reinstall" : "Install";
        RepairButton.Visibility = installed ? Visibility.Visible : Visibility.Collapsed;
        UninstallButton.Visibility = installed ? Visibility.Visible : Visibility.Collapsed;
    }

    public void SetBusy(bool busy)
    {
        PrimaryActionButton.IsEnabled = !busy;
        RepairButton.IsEnabled = !busy;
        UninstallButton.IsEnabled = !busy;
        RetryButton.IsEnabled = !busy;
        FinishButton.IsEnabled = !busy;
        MinimizeButton.IsEnabled = !busy;
        MaximizeButton.IsEnabled = !busy;
        CloseButton.IsEnabled = !busy;
        Cursor = busy ? Cursors.Wait : Cursors.Arrow;
    }

    public void SetStatus(string status)
    {
        StatusText.Text = status;
    }

    public void SetDetail(string detail)
    {
        DetailText.Text = detail;
    }

    public void SetActionHint(string hint)
    {
        ActionHintText.Text = hint;
    }

    public void SetProgress(int percent)
    {
        var safePercent = Math.Max(0, Math.Min(100, percent));
        InstallProgressBar.Value = safePercent;
        ProgressLabel.Text = safePercent + "%";
    }

    public void AppendLog(string line)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        LiveLogTextBox.AppendText($"[{timestamp}] {line}{Environment.NewLine}");
        LiveLogTextBox.ScrollToEnd();
    }

    public void ShowReadyMessage()
    {
        MessageTitleText.Text = "Ready.";
        MessageBodyText.Text = "Everything is set. Pick an action to continue.";
        SupportCodeText.Text = string.Empty;
        RetryButton.Visibility = Visibility.Collapsed;
        ViewLogsButton.Visibility = Visibility.Visible;
        CopyErrorButton.Visibility = Visibility.Collapsed;
        CloseFailureButton.Visibility = Visibility.Collapsed;
        FinishButton.Visibility = Visibility.Collapsed;
        LaunchAfterFinishCheckBox.Visibility = Visibility.Collapsed;
    }

    public void ShowFailure(string summary, string supportCode, string details)
    {
        MessageTitleText.Text = summary;
        MessageBodyText.Text = details;
        SupportCodeText.Text = "Support code: " + supportCode;
        RetryButton.Visibility = Visibility.Visible;
        ViewLogsButton.Visibility = Visibility.Visible;
        CopyErrorButton.Visibility = Visibility.Visible;
        CloseFailureButton.Visibility = Visibility.Visible;
        FinishButton.Visibility = Visibility.Collapsed;
        LaunchAfterFinishCheckBox.Visibility = Visibility.Collapsed;
        SelectTab("Install");
        PlaySystemSound(SystemSoundType.Failure);
    }

    public void ShowSuccess(string message, bool canLaunch)
    {
        MessageTitleText.Text = "Complete.";
        MessageBodyText.Text = message;
        SupportCodeText.Text = string.Empty;
        RetryButton.Visibility = Visibility.Collapsed;
        ViewLogsButton.Visibility = Visibility.Collapsed;
        CopyErrorButton.Visibility = Visibility.Collapsed;
        CloseFailureButton.Visibility = Visibility.Collapsed;
        FinishButton.Visibility = Visibility.Visible;
        LaunchAfterFinishCheckBox.Visibility = canLaunch ? Visibility.Visible : Visibility.Collapsed;
        LaunchAfterFinishCheckBox.IsChecked = canLaunch;
        SelectTab("Install");
        PlaySystemSound(SystemSoundType.Success);
    }

    private void PrimaryActionButton_Click(object sender, RoutedEventArgs e)
    {
        PlaySystemSound(SystemSoundType.Action);
        InstallRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RepairButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isInstalled)
        {
            PlaySystemSound(SystemSoundType.Action);
            RepairRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void UninstallButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isInstalled)
        {
            ShowConfirm(
                "Uninstall BloxMC Launcher?",
                "This removes BloxMC Launcher, local launcher files, and launcher shortcuts from this user profile.",
                () =>
                {
                    PlaySystemSound(SystemSoundType.Action);
                    UninstallRequested?.Invoke(this, EventArgs.Empty);
                });
        }
    }

    private void RetryButton_Click(object sender, RoutedEventArgs e)
    {
        PlaySystemSound(SystemSoundType.Action);
        RetryRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ViewLogsButton_Click(object sender, RoutedEventArgs e)
    {
        ViewLogsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CopyErrorButton_Click(object sender, RoutedEventArgs e)
    {
        CopyErrorRequested?.Invoke(this, EventArgs.Empty);
    }

    private void FinishButton_Click(object sender, RoutedEventArgs e)
    {
        PlaySystemSound(SystemSoundType.Action);
        FinishRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CloseFailureButton_Click(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && ConfirmOverlay.Visibility != Visibility.Visible)
        {
            DragMove();
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (ReducedMotionEnabled)
        {
            return;
        }

        Opacity = 0;
        var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220));
        BeginAnimation(OpacityProperty, anim);
    }

    private void HomeTabButton_Click(object sender, RoutedEventArgs e)
    {
        SelectTab("Home");
    }

    private void InstallTabButton_Click(object sender, RoutedEventArgs e)
    {
        SelectTab("Install");
    }

    private void SettingsTabButton_Click(object sender, RoutedEventArgs e)
    {
        SelectTab("Settings");
    }

    private void EditTabButton_Click(object sender, RoutedEventArgs e)
    {
        SelectTab("Edit");
    }

    private void EditUnlockButton_Click(object sender, RoutedEventArgs e)
    {
        if (EditPasswordBox.Password != _editPassword)
        {
            MessageTitleText.Text = "Edit tab locked.";
            MessageBodyText.Text = "Password did not match. Try again.";
            SupportCodeText.Text = string.Empty;
            PlaySystemSound(SystemSoundType.Failure);
            return;
        }

        EditLockedPanel.Visibility = Visibility.Collapsed;
        EditUnlockedPanel.Visibility = Visibility.Visible;
        MessageTitleText.Text = "Edit tab unlocked.";
        MessageBodyText.Text = "Advanced installer controls are now available in this session.";
        SupportCodeText.Text = string.Empty;
        PlaySystemSound(SystemSoundType.Success);
    }

    private void ConfirmYesButton_Click(object sender, RoutedEventArgs e)
    {
        var action = _confirmAction;
        _confirmAction = null;
        ConfirmOverlay.Visibility = Visibility.Collapsed;
        action?.Invoke();
    }

    private void ConfirmNoButton_Click(object sender, RoutedEventArgs e)
    {
        _confirmAction = null;
        ConfirmOverlay.Visibility = Visibility.Collapsed;
    }

    private void ShowConfirm(string title, string message, Action onConfirm)
    {
        ConfirmTitleText.Text = title;
        ConfirmMessageText.Text = message;
        _confirmAction = onConfirm;
        ConfirmOverlay.Visibility = Visibility.Visible;
        PlaySystemSound(SystemSoundType.Action);
    }

    private void SelectTab(string tabName, bool animate = true)
    {
        if (_activeTab == tabName)
        {
            return;
        }

        _activeTab = tabName;
        HomeTabContent.Visibility = tabName == "Home" ? Visibility.Visible : Visibility.Collapsed;
        InstallTabContent.Visibility = tabName == "Install" ? Visibility.Visible : Visibility.Collapsed;
        SettingsTabContent.Visibility = tabName == "Settings" ? Visibility.Visible : Visibility.Collapsed;
        EditTabContent.Visibility = tabName == "Edit" ? Visibility.Visible : Visibility.Collapsed;

        HomeTabButton.Style = (Style)FindResource(tabName == "Home" ? "TabButtonActiveStyle" : "TabButtonStyle");
        InstallTabButton.Style = (Style)FindResource(tabName == "Install" ? "TabButtonActiveStyle" : "TabButtonStyle");
        SettingsTabButton.Style = (Style)FindResource(tabName == "Settings" ? "TabButtonActiveStyle" : "TabButtonStyle");
        EditTabButton.Style = (Style)FindResource(tabName == "Edit" ? "TabButtonActiveStyle" : "TabButtonStyle");

        if (!animate || ReducedMotionEnabled)
        {
            return;
        }

        var target = tabName switch
        {
            "Home" => HomeTabContent,
            "Install" => InstallTabContent,
            "Settings" => SettingsTabContent,
            _ => EditTabContent
        };

        target.Opacity = 0;
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180));
        target.BeginAnimation(OpacityProperty, fade);
    }

    private void PlaySystemSound(SystemSoundType soundType)
    {
        if (!SoundEffectsEnabled)
        {
            return;
        }

        switch (soundType)
        {
            case SystemSoundType.Action:
                SystemSounds.Beep.Play();
                break;
            case SystemSoundType.Success:
                SystemSounds.Asterisk.Play();
                break;
            case SystemSoundType.Failure:
                SystemSounds.Hand.Play();
                break;
        }
    }

    private enum SystemSoundType
    {
        Action,
        Success,
        Failure
    }
}

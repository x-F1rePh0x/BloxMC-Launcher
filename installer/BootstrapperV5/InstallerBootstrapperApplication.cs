using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using WixToolset.BootstrapperApplicationApi;

namespace BloxMC.Bootstrapper;

internal sealed class InstallerBootstrapperApplication : BootstrapperApplication
{
    private const string MsiPackageId = "BloxMCMsi";
    private const string LauncherFolderName = "BloxMC";

    private readonly string _msiLogPath = Path.Combine(Path.GetTempPath(), "BloxMC-Install.log");
    private string _bundleLogPath = string.Empty;
    private string _lastErrorDetails = string.Empty;
    private string _supportCode = string.Empty;

    private MainWindow? _window;
    private bool _isInstalled;
    private bool _packageRegistered;
    private bool _canLaunchInstalledApp;
    private bool _hadExecutePackage;
    private int _exitCode;
    private LaunchAction _lastRequestedAction = LaunchAction.Install;

    protected override void Run()
    {
        WireBurnEvents();

        var app = new App
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown
        };
        app.InitializeComponent();

        _window = new MainWindow();
        _window.SetStatus("Checking BloxMC Launcher installation...");
        _window.SetDetail("Preparing setup engine.");
        _window.SetInstalled(false);
        _window.SetBusy(true);
        _window.SetActionHint("Install BloxMC Launcher in %LOCALAPPDATA%\\BloxMC");
        _window.SetInstallPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BloxMC"));
        _window.SetLogPath(_msiLogPath);
        _window.AppendLog("UI initialized.");

        _window.InstallRequested += (_, _) => BeginAction(LaunchAction.Install, _isInstalled ? "Planning reinstall..." : "Planning install...");
        _window.RepairRequested += (_, _) => BeginAction(LaunchAction.Repair, "Planning repair...");
        _window.UninstallRequested += (_, _) => BeginAction(LaunchAction.Uninstall, "Planning uninstall...");
        _window.RetryRequested += (_, _) => BeginAction(_lastRequestedAction, "Retrying action...");
        _window.ViewLogsRequested += (_, _) => OpenLogFile();
        _window.CopyErrorRequested += (_, _) => CopyErrorToClipboard();
        _window.FinishRequested += (_, _) => OnFinishRequested();
        _window.CloseRequested += (_, _) => ShutdownApp(0);

        _window.Show();

        TrySetMsiLogVariable();
        _window.AppendLog("Running detection.");
        this.engine.Detect(IntPtr.Zero);

        app.Run();
        this.engine.Quit(_exitCode);
    }

    private void WireBurnEvents()
    {
        DetectPackageComplete += OnDetectPackageComplete;
        DetectComplete += OnDetectComplete;
        PlanComplete += OnPlanComplete;

        PlanBegin += (_, e) => Ui(() => _window?.AppendLog("Plan begin: packageCount=" + e.PackageCount));
        PlanPackageBegin += (_, e) => Ui(() => _window?.AppendLog("Plan package begin: " + e.PackageId));
        PlanPackageComplete += (_, e) => Ui(() => _window?.AppendLog("Plan package complete: " + e.PackageId + " status=" + e.Status));

        ApplyBegin += (_, _) => Ui(() =>
        {
            _hadExecutePackage = false;
            _window?.SetBusy(true);
            _window?.SetStatus("Applying changes...");
            _window?.SetDetail("Running installer actions.");
            _window?.ShowReadyMessage();
            _window?.AppendLog("Apply started.");
        });

        ExecutePackageBegin += (_, e) => Ui(() =>
        {
            _hadExecutePackage = true;
            _window?.SetDetail("Processing package: " + e.PackageId);
            _window?.AppendLog("Package begin: " + e.PackageId);
        });

        ExecutePackageComplete += (_, e) => Ui(() =>
        {
            _window?.AppendLog("Package complete: " + e.PackageId + " status=" + e.Status);
        });

        ExecuteProgress += (_, e) => Ui(() =>
        {
            _window?.SetProgress(e.OverallPercentage);
            _window?.SetStatus("Working... " + e.OverallPercentage + "%");
        });

        ElevateBegin += (_, _) => Ui(() =>
        {
            _window?.SetStatus("Permission required.");
            _window?.SetDetail("Windows requested elevation. Approve UAC to continue this action.");
            _window?.AppendLog("Elevation prompt requested.");
        });

        Error += (_, e) =>
        {
            _lastErrorDetails = "Error " + e.ErrorCode + ": " + e.ErrorMessage;
            Ui(() => _window?.AppendLog(_lastErrorDetails));
        };

        ApplyComplete += OnApplyComplete;
    }

    private void OnDetectPackageComplete(object? sender, DetectPackageCompleteEventArgs e)
    {
        if (!string.Equals(e.PackageId, MsiPackageId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _packageRegistered = e.State is PackageState.Present or PackageState.Superseded or PackageState.Obsolete;
    }

    private void OnDetectComplete(object? sender, DetectCompleteEventArgs e)
    {
        _isInstalled = HasInstalledArtifacts();
        _canLaunchInstalledApp = ResolveLauncherPath() != null;
        TryReadBundleLogPath();
        Ui(() =>
        {
            _window?.SetLogPath(File.Exists(_msiLogPath) ? _msiLogPath : _bundleLogPath);
        });

        Ui(() =>
        {
            _window?.SetInstalled(_isInstalled);
            _window?.SetBusy(false);
            _window?.SetProgress(0);
            _window?.SetStatus(_isInstalled ? "BloxMC Launcher is installed." : "Ready to install BloxMC Launcher.");
            _window?.SetDetail(_isInstalled ? "Choose Reinstall, Repair, or Uninstall." : "Press Install to continue.");
            _window?.SetActionHint("MSI log: %TEMP%\\BloxMC-Install.log");
            _window?.ShowReadyMessage();
            if (_packageRegistered && !_isInstalled)
            {
                _window?.AppendLog("Package is registered but launcher files are missing. Treating as not installed.");
            }
            _window?.AppendLog("Detection complete.");
        });
    }

    private void BeginAction(LaunchAction action, string planningText)
    {
        _lastRequestedAction = action;
        _lastErrorDetails = string.Empty;
        _hadExecutePackage = false;

        Ui(() =>
        {
            _window?.SetBusy(true);
            _window?.SetStatus(planningText);
            _window?.SetDetail("Preparing Burn plan.");
            _window?.ShowReadyMessage();
            _window?.AppendLog(planningText.Replace("...", "."));
        });

        this.engine.Plan(action);
    }

    private void OnPlanComplete(object? sender, PlanCompleteEventArgs e)
    {
        if (e.Status != 0)
        {
            ShowFailure("Planning failed.", "Burn plan returned status " + e.Status + ".");
            return;
        }

        Ui(() => _window?.AppendLog("Plan complete: status=0"));

        var hwnd = IntPtr.Zero;
        if (_window != null)
        {
            hwnd = new WindowInteropHelper(_window).Handle;
        }

        this.engine.Apply(hwnd);
    }

    private void OnApplyComplete(object? sender, ApplyCompleteEventArgs e)
    {
        if (e.Status == 0)
        {
            if (_lastRequestedAction == LaunchAction.Uninstall && !_hadExecutePackage)
            {
                _isInstalled = true;
                ShowFailure(
                    "Uninstall did not run.",
                    "Another registered BloxMC setup entry is still linked to this MSI. Remove older 'BloxMC Launcher Setup' entries, then retry uninstall.");
                _exitCode = 1;
                return;
            }

            _isInstalled = _lastRequestedAction != LaunchAction.Uninstall;
            if (_lastRequestedAction == LaunchAction.Uninstall)
            {
                var cleanupSummary = CleanupUserArtifacts();
                Ui(() => _window?.AppendLog(cleanupSummary));
            }
            _canLaunchInstalledApp = ResolveLauncherPath() != null;

            Ui(() =>
            {
                _window?.SetBusy(false);
                _window?.SetInstalled(_isInstalled);
                _window?.SetProgress(100);
                _window?.SetStatus(_lastRequestedAction == LaunchAction.Uninstall
                    ? "BloxMC Launcher uninstall complete."
                    : "BloxMC Launcher install complete.");
                _window?.SetDetail("Click Finish to close setup.");
                _window?.ShowSuccess("Setup completed successfully.", _isInstalled && _canLaunchInstalledApp);
                _window?.AppendLog("Apply complete: success.");
            });

            _exitCode = 0;
            return;
        }

        var details = string.IsNullOrWhiteSpace(_lastErrorDetails)
            ? "Action returned status " + e.Status + "."
            : _lastErrorDetails;
        ShowFailure("BloxMC Launcher setup failed.", details);
        _exitCode = e.Status;
    }

    private void ShowFailure(string summary, string details)
    {
        _supportCode = "BLX-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        _lastErrorDetails = details;

        Ui(() =>
        {
            _window?.SetBusy(false);
            _window?.SetStatus("Action failed.");
            _window?.SetDetail("Use Retry, View logs, or Copy error.");
            _window?.ShowFailure(summary, _supportCode, details);
            _window?.AppendLog(summary + " " + details);
        });
    }

    private void OnFinishRequested()
    {
        if (_window?.LaunchAfterFinish == true)
        {
            TryLaunchInstalledApp();
        }

        ShutdownApp(0);
    }

    private void ShutdownApp(int code)
    {
        _exitCode = code;
        Application.Current.Shutdown();
    }

    private void TrySetMsiLogVariable()
    {
        try
        {
            this.engine.SetVariableString("BLOXMCMSILOG", _msiLogPath, false);
        }
        catch
        {
        }
    }

    private void TryReadBundleLogPath()
    {
        try
        {
            _bundleLogPath = this.engine.GetVariableString("WixBundleLog");
        }
        catch
        {
            _bundleLogPath = string.Empty;
        }
    }

    private void OpenLogFile()
    {
        if (!File.Exists(_msiLogPath))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(_msiLogPath)
            {
                UseShellExecute = true
            });
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(_bundleLogPath) && File.Exists(_bundleLogPath))
            {
                Process.Start(new ProcessStartInfo(_bundleLogPath) { UseShellExecute = true });
            }
        }
    }

    private void CopyErrorToClipboard()
    {
        var text = "Support code: " + _supportCode + Environment.NewLine +
                   "MSI log: " + _msiLogPath + Environment.NewLine +
                   "Bundle log: " + _bundleLogPath + Environment.NewLine +
                   _lastErrorDetails;

        Ui(() =>
        {
            try
            {
                Clipboard.SetText(text);
            }
            catch
            {
            }
        });
    }

    private static string? ResolveLauncherPath()
    {
        var localRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            LauncherFolderName);

        var candidates = new[]
        {
            Path.Combine(localRoot, "BloxMCLauncher.exe"),
            Path.Combine(localRoot, "BloxMC Launcher.exe"),
            Path.Combine(localRoot, "BloxMCLauncher.jar"),
            Path.Combine(localRoot, "BloxMC Launcher.jar")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool HasInstalledArtifacts()
    {
        if (ResolveLauncherPath() != null)
        {
            return true;
        }

        var localRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            LauncherFolderName);

        try
        {
            return Directory.Exists(localRoot) &&
                   Directory.EnumerateFileSystemEntries(localRoot).Any();
        }
        catch
        {
            return false;
        }
    }

    private static string CleanupUserArtifacts()
    {
        var removedFiles = 0;
        var removedDirs = 0;

        var localRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            LauncherFolderName);

        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var startMenuPrograms = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        var startMenuFolder = Path.Combine(startMenuPrograms, "BloxMC Launcher");

        var fileTargets = new[]
        {
            Path.Combine(desktop, "BloxMC Launcher.lnk"),
            Path.Combine(desktop, "BloxMCLauncher.lnk"),
            Path.Combine(startMenuPrograms, "BloxMC Launcher.lnk"),
            Path.Combine(startMenuPrograms, "BloxMCLauncher.lnk"),
            Path.Combine(startMenuFolder, "BloxMC Launcher.lnk"),
            Path.Combine(startMenuFolder, "Uninstall BloxMC Launcher.lnk")
        };

        foreach (var file in fileTargets)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                    removedFiles++;
                }
            }
            catch
            {
            }
        }

        var dirTargets = new[]
        {
            startMenuFolder,
            localRoot
        };

        foreach (var dir in dirTargets)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                    removedDirs++;
                }
            }
            catch
            {
            }
        }

        return "Post-uninstall cleanup complete. Files removed: " + removedFiles + ", folders removed: " + removedDirs + ".";
    }

    private void TryLaunchInstalledApp()
    {
        var launcherPath = ResolveLauncherPath();
        if (launcherPath == null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(launcherPath)
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(launcherPath) ?? string.Empty
            });
        }
        catch
        {
        }
    }

    private void Ui(Action action)
    {
        if (_window == null)
        {
            return;
        }

        if (_window.Dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            _window.Dispatcher.Invoke(action);
        }
    }
}

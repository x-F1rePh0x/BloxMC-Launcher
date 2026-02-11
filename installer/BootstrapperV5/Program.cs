using System.IO;
using System.Threading;
using System.Windows;
using WixToolset.BootstrapperApplicationApi;

namespace BloxMC.Bootstrapper;

internal static class Program
{
    private const string InstanceMutexName = @"Local\BloxMCLauncherSetup_BA_v1";

    private static int Main()
    {
        using var singleInstance = new Mutex(initiallyOwned: true, InstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            return 0;
        }

        try
        {
            var app = new InstallerBootstrapperApplication();
            ManagedBootstrapperApplication.Run(app);
            return 0;
        }
        catch (Exception ex)
        {
            var crashLog = Path.Combine(Path.GetTempPath(), "BloxMC-Installer-Crash.log");
            try
            {
                File.WriteAllText(crashLog, ex.ToString());
            }
            catch
            {
            }

            MessageBox.Show(
                "BloxMC Launcher Setup hit an error before opening.\n\nLog: " + crashLog,
                "BloxMC Launcher Setup",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return 1;
        }
    }
}

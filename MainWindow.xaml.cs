using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.ServiceProcess;
using System.Windows;

namespace WindowsMender
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            CheckAdminStatus();
        }

        private void CheckAdminStatus()
        {
            if (!IsAdministrator())
            {
                Log("[!] WARNING: Not running as Administrator. DISM and Service repairs require elevation.\n");
                TxtStatus.Text = "Status: Running standard user mode.";
                BtnElevate.Visibility = Visibility.Visible;
            }
            else
            {
                Log("[+] Administrative privileges confirmed.\n");
                TxtStatus.Text = "Status: Ready (Administrator mode active).";
                BtnElevate.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnElevate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (exePath != null)
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = true,
                        Verb = "runas"
                    };
                    Process.Start(psi);
                    Application.Current.Shutdown();
                }
            }
            catch (Exception ex)
            {
                Log($"[!] Failed to restart as administrator: {ex.Message}");
            }
        }

        private void Log(string message)
        {
            TxtOutput.AppendText(message + "\n");
            TxtOutput.ScrollToEnd();
        }

        private void BtnCheckServices_Click(object sender, RoutedEventArgs e)
        {
            Log("\n--- Checking Windows Update Services ---");
            string[] services = { "wuauserv", "bits", "cryptsvc", "msiserver" };
            foreach (string svcName in services)
            {
                try
                {
                    using (ServiceController sc = new ServiceController(svcName))
                    {
                        Log($"{svcName} : {sc.Status}");
                    }
                }
                catch (Exception ex)
                {
                    Log($"{svcName} : Error ({ex.Message})");
                }
            }
        }

        private void BtnShowFailures_Click(object sender, RoutedEventArgs e)
        {
            Log("\n--- Recent Update Failures ---");
            try
            {
                using (EventLog log = new EventLog("System"))
                {
                    int count = 0;
                    for (int i = log.Entries.Count - 1; i >= 0; i--)
                    {
                        EventLogEntry entry = log.Entries[i];
                        if (entry.Source == "Microsoft-Windows-WindowsUpdateClient")
                        {
                            Log($"{entry.TimeWritten}: ID {entry.InstanceId} - {entry.Message}");
                            count++;
                            if (count >= 5) break;
                        }
                    }
                    if (count == 0) Log("No recent Windows Update events found.");
                }
            }
            catch (Exception ex)
            {
                Log($"Error reading event logs: {ex.Message}");
            }
        }

        private void BtnCheckReboot_Click(object sender, RoutedEventArgs e)
        {
            Log("\n--- Pending Reboot Check ---");
            bool pendingCBS = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending") != null;
            bool pendingWU = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired") != null;

            if (pendingCBS || pendingWU)
                Log("[!] A pending reboot is detected on this system.");
            else
                Log("[+] No pending reboot detected.");
        }

        private void BtnClearCaches_Click(object sender, RoutedEventArgs e)
        {
            Log("\n--- Clearing SoftwareDistribution & Catroot2 ---");
            ToggleServices(false);

            string sysRoot = Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows";
            string sdPath = Path.Combine(sysRoot, "SoftwareDistribution");
            string catPath = Path.Combine(sysRoot, "System32", "Catroot2");

            try
            {
                if (Directory.Exists(sdPath)) Directory.Move(sdPath, sdPath + ".bak");
                if (Directory.Exists(catPath)) Directory.Move(catPath, catPath + ".bak");
                Log("Caches cleared successfully.");
            }
            catch (Exception ex)
            {
                Log($"Error clearing caches: {ex.Message}");
            }

            ToggleServices(true);
        }

        private void BtnForceDetection_Click(object sender, RoutedEventArgs e)
        {
            Log("\n--- Forcing Update Detection via UsoClient ---");
            try
            {
                Process.Start(new ProcessStartInfo { FileName = "UsoClient.exe", Arguments = "StartScan", UseShellExecute = false });
                Log("Scan command sent.");
            }
            catch (Exception ex)
            {
                Log($"Failed to trigger scan: {ex.Message}");
            }
        }

        private async void BtnStoreRepair_Click(object sender, RoutedEventArgs e)
        {
            Log("\n--- Running DISM Component Store Repair ---");
            Log("[!] Note: This may take several minutes depending on system health, corruption level, and storage drive speed.");

            try
            {
                Log("[1/3] Stopping update services...");
                ToggleServices(false);

                Log("[2/3] Executing DISM /Online /Cleanup-Image /RestoreHealth...");
                await ExecuteProcessAsync("dism.exe", "/Online /Cleanup-Image /RestoreHealth");
            }
            catch (Exception ex)
            {
                Log($"[!] Process execution error: {ex.Message}");
            }
            finally
            {
                Log("[3/3] Restarting update services...");
                ToggleServices(true);
                Log("Repair sequence completed. Please reboot your PC.");
            }
        }

        private void ToggleServices(bool start)
        {
            string[] services = { "wuauserv", "bits", "cryptsvc" };
            foreach (string name in services)
            {
                try
                {
                    using (ServiceController sc = new ServiceController(name))
                    {
                        if (start && sc.Status != ServiceControllerStatus.Running) { sc.Start(); sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(5)); }
                        else if (!start && sc.Status != ServiceControllerStatus.Stopped) { sc.Stop(); sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(5)); }
                    }
                }
                catch { }
            }
        }

        private bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        private async Task ExecuteProcessAsync(string fileName, string args)
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (Process proc = new Process { StartInfo = psi })
            {
                proc.OutputDataReceived += (s, ev) =>
                {
                    if (ev.Data != null)
                        Dispatcher.InvokeAsync(() => Log(ev.Data));
                };
                proc.ErrorDataReceived += (s, ev) =>
                {
                    if (ev.Data != null)
                        Dispatcher.InvokeAsync(() => Log(ev.Data));
                };

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                await proc.WaitForExitAsync();
            }
        }
    }
}
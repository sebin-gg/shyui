using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ShyUI
{
    static class Logger
    {
        private static readonly string LogFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shyui_log.txt");
        public static void Log(string message)
        {
            try
            {
                File.AppendAllText(LogFile, string.Format("{0:yyyy-MM-dd HH:mm:ss.fff} - {1}\r\n", DateTime.Now, message));
            }
            catch { }
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Logger.Log("Application starting...");
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            try
            {
                using (var appContext = new ShyUIContext())
                {
                    Logger.Log("Context created, running message loop...");
                    Application.Run(appContext);
                }
            }
            catch (Exception ex)
            {
                Logger.Log("FATAL ERROR: " + ex.ToString());
            }
            Logger.Log("Application exiting.");
        }
    }

    public class ShyUIContext : ApplicationContext
    {
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private Timer loopTimer;
        
        private string configPath = "shyui_config.txt";
        private Dictionary<string, int> appConfigs;
        private int defaultTopBarHeight = 40;
        
        private bool isSystemPaused = false;
        private Dictionary<IntPtr, bool> windowRevealedState;
        private HashSet<IntPtr> autoManagedWindows;

        // Hotkey IDs
        private const int HOTKEY_PAUSE = 1;
        private const int HOTKEY_TOGGLE_APP = 2;

        // Hidden form to catch hotkeys
        private HotkeyForm hotkeyForm;

        public ShyUIContext()
        {
            configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configPath);
            appConfigs = new Dictionary<string, int>();
            windowRevealedState = new Dictionary<IntPtr, bool>();
            autoManagedWindows = new HashSet<IntPtr>();

            LoadConfig();

            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Settings", null, ShowSettings);
            trayMenu.Items.Add("Pause (Ctrl+Alt+S)", null, TogglePauseItem);
            trayMenu.Items.Add("-");
            var startupItem = new ToolStripMenuItem("Run at Startup", null, ToggleStartup);
            startupItem.Checked = CheckStartup();
            trayMenu.Items.Add(startupItem);
            trayMenu.Items.Add("-");
            trayMenu.Items.Add("Exit", null, Exit);

            trayIcon = new NotifyIcon();
            trayIcon.Text = "Shy UI Window Manager";
            trayIcon.Icon = SystemIcons.Application;
            trayIcon.ContextMenuStrip = trayMenu;
            trayIcon.Visible = true;

            UpdateTrayIcon();

            hotkeyForm = new HotkeyForm(this);
            hotkeyForm.Show(); // It's invisible

            loopTimer = new Timer();
            loopTimer.Interval = 20; // 50fps
            loopTimer.Tick += LoopTimer_Tick;
            loopTimer.Start();
            
            Logger.Log("ShyUIContext initialized successfully.");
        }

        private void LoadConfig()
        {
            appConfigs.Clear();
            if (File.Exists(configPath))
            {
                var lines = File.ReadAllLines(configPath);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var parts = line.Split('=');
                    int height;
                    if (parts.Length == 2 && int.TryParse(parts[1], out height))
                    {
                        appConfigs[parts[0].Trim().ToLower()] = height;
                    }
                }
                Logger.Log("Config loaded. Apps count: " + appConfigs.Count);
            }
            else
            {
                Logger.Log("Config not found, starting empty.");
            }
        }

        public void SaveConfig()
        {
            try
            {
                var lines = appConfigs.Select(kv => string.Format("{0}={1}", kv.Key, kv.Value)).ToList();
                File.WriteAllLines(configPath, lines);
                Logger.Log("Config saved.");
            }
            catch (Exception ex)
            {
                Logger.Log("Error saving config: " + ex.Message);
            }
        }

        public Dictionary<string, int> GetConfigs()
        {
            return appConfigs;
        }
        
        public void UpdateConfig(Dictionary<string, int> newConfigs)
        {
            appConfigs = newConfigs;
            SaveConfig();
        }

        private void ShowSettings(object sender, EventArgs e)
        {
            var settingsForm = new SettingsForm(this);
            settingsForm.Show();
        }

        private void TogglePauseItem(object sender, EventArgs e)
        {
            TogglePause();
        }

        public void TogglePause()
        {
            isSystemPaused = !isSystemPaused;
            UpdateTrayIcon();
            Logger.Log("System paused: " + isSystemPaused);

            if (isSystemPaused)
            {
                // Restore all managed windows to normal maximized state
                foreach (IntPtr handle in windowRevealedState.Keys.ToList())
                {
                    Win32.ShowWindow(handle, Win32.SW_SHOWMAXIMIZED);
                }
                windowRevealedState.Clear();
                autoManagedWindows.Clear(); // They will auto-re-add when unpaused if still maximized
            }
        }

        private void UpdateTrayIcon()
        {
            trayIcon.Text = isSystemPaused ? "Shy UI (Paused)" : "Shy UI (Active)";
            trayMenu.Items[1].Text = isSystemPaused ? "Resume (Ctrl+Alt+S)" : "Pause (Ctrl+Alt+S)";
        }

        public void ToggleCurrentApp()
        {
            IntPtr hWnd = Win32.GetForegroundWindow();
            if (hWnd == IntPtr.Zero) 
            {
                Logger.Log("Toggle failed: No foreground window.");
                return;
            }

            string procName = GetProcessName(hWnd);
            if (string.IsNullOrEmpty(procName))
            {
                Logger.Log("Toggle failed: Could not read process name (admin rights?).");
                return;
            }
            procName = procName.ToLower();

            if (autoManagedWindows.Contains(hWnd))
            {
                autoManagedWindows.Remove(hWnd);
                Win32.ShowWindow(hWnd, Win32.SW_SHOWMAXIMIZED);
                trayIcon.BalloonTipTitle = "Shy UI";
                trayIcon.BalloonTipText = string.Format("Removed window from Auto-Shy UI");
                trayIcon.ShowBalloonTip(2000);
                Logger.Log("Removed auto-managed window: " + procName);
                return;
            }

            if (appConfigs.ContainsKey(procName))
            {
                appConfigs.Remove(procName);
                Win32.ShowWindow(hWnd, Win32.SW_SHOWMAXIMIZED);
                trayIcon.BalloonTipTitle = "Shy UI";
                trayIcon.BalloonTipText = string.Format("Removed {0} from Shy UI", procName);
                trayIcon.ShowBalloonTip(2000);
                Logger.Log("Removed app: " + procName);
            }
            else
            {
                appConfigs[procName] = defaultTopBarHeight;
                trayIcon.BalloonTipTitle = "Shy UI";
                trayIcon.BalloonTipText = string.Format("Added {0} to Shy UI", procName);
                trayIcon.ShowBalloonTip(2000);
                Logger.Log("Added app: " + procName);
            }
            SaveConfig();
        }

        private void Exit(object sender, EventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to exit Shy UI? Your windows will no longer hide their top bars.", 
                                         "Exit Shy UI", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == DialogResult.Yes)
            {
                Logger.Log("User requested exit.");
                trayIcon.Visible = false;
                Application.Exit();
            }
        }

        private bool CheckStartup()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    return key != null && key.GetValue("ShyUI") != null;
                }
            }
            catch { return false; }
        }

        private void ToggleStartup(object sender, EventArgs e)
        {
            bool isCurrentlyStartup = CheckStartup();
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key != null)
                    {
                        if (isCurrentlyStartup)
                        {
                            key.DeleteValue("ShyUI", false);
                            ((ToolStripMenuItem)sender).Checked = false;
                            Logger.Log("Removed from startup.");
                        }
                        else
                        {
                            key.SetValue("ShyUI", "\"" + Application.ExecutablePath + "\"");
                            ((ToolStripMenuItem)sender).Checked = true;
                            Logger.Log("Added to startup.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to change startup settings: " + ex.Message, "Shy UI");
                Logger.Log("Startup fail: " + ex.Message);
            }
        }

        private void LoopTimer_Tick(object sender, EventArgs e)
        {
            if (isSystemPaused) return;

            IntPtr hWnd = Win32.GetForegroundWindow();
            if (hWnd == IntPtr.Zero || IsMinimized(hWnd)) return;

            string procName = GetProcessName(hWnd);
            if (string.IsNullOrEmpty(procName)) return;
            procName = procName.ToLower();
            
            int topBarHeight;
            bool isInConfig = appConfigs.TryGetValue(procName, out topBarHeight);
            if (!isInConfig) topBarHeight = defaultTopBarHeight;

            bool isMaximized = IsMaximized(hWnd);

            if (isInConfig || isMaximized || autoManagedWindows.Contains(hWnd))
            {
                if (isMaximized && !isInConfig && !autoManagedWindows.Contains(hWnd))
                {
                    Logger.Log("Auto-activating for maximized window: " + procName);
                    autoManagedWindows.Add(hWnd);
                }

                ManageWindow(hWnd, topBarHeight, procName);
            }
        }

        private void ManageWindow(IntPtr hWnd, int topBarHeight, string procName)
        {
            var screen = Screen.FromHandle(hWnd);
            var workArea = screen.WorkingArea; // Auto adjusts for taskbar!

            // Restore if maximized
            if (IsMaximized(hWnd))
            {
                Logger.Log("Restoring maximized window: " + procName);
                Win32.ShowWindow(hWnd, Win32.SW_RESTORE);
            }

            Win32.POINT p;
            Win32.GetCursorPos(out p);

            bool isRevealed = false;
            if (windowRevealedState.ContainsKey(hWnd))
                isRevealed = windowRevealedState[hWnd];

            // Logic for revealing/hiding
            if (!isRevealed)
            {
                // If mouse touches top 2 pixels of the screen bounds
                if (p.Y <= screen.Bounds.Y + 2)
                {
                    isRevealed = true;
                    Logger.Log("Revealed top bar for: " + procName);
                }
            }
            else
            {
                // If mouse moves below the top bar area
                if (p.Y > workArea.Y + topBarHeight)
                {
                    isRevealed = false;
                    Logger.Log("Hid top bar for: " + procName);
                }
            }

            windowRevealedState[hWnd] = isRevealed;

            int targetY = isRevealed ? workArea.Y : workArea.Y - topBarHeight;
            int targetHeight = workArea.Height + topBarHeight;

            Win32.RECT rect;
            Win32.GetWindowRect(hWnd, out rect);
            
            // Only move if bounds are wrong to avoid flickering
            if (rect.Left != workArea.X || rect.Top != targetY || 
                (rect.Right - rect.Left) != workArea.Width || 
                (rect.Bottom - rect.Top) != targetHeight)
            {
                Win32.SetWindowPos(hWnd, IntPtr.Zero, workArea.X, targetY, workArea.Width, targetHeight, 
                    Win32.SWP_NOACTIVATE | Win32.SWP_NOZORDER);
            }
        }

        private bool IsMaximized(IntPtr hWnd)
        {
            Win32.WINDOWPLACEMENT placement = new Win32.WINDOWPLACEMENT();
            placement.length = Marshal.SizeOf(placement);
            Win32.GetWindowPlacement(hWnd, ref placement);
            return placement.showCmd == Win32.SW_SHOWMAXIMIZED;
        }

        private bool IsMinimized(IntPtr hWnd)
        {
            Win32.WINDOWPLACEMENT placement = new Win32.WINDOWPLACEMENT();
            placement.length = Marshal.SizeOf(placement);
            Win32.GetWindowPlacement(hWnd, ref placement);
            return placement.showCmd == Win32.SW_SHOWMINIMIZED;
        }

        private string GetProcessName(IntPtr hWnd)
        {
            uint pid;
            Win32.GetWindowThreadProcessId(hWnd, out pid);
            if (pid == 0) return null;
            
            // Attempt 1: Safe native way without requiring handle read rights
            IntPtr hProcess = Win32.OpenProcess(Win32.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hProcess != IntPtr.Zero)
            {
                try
                {
                    StringBuilder sb = new StringBuilder(1024);
                    int size = sb.Capacity;
                    if (Win32.QueryFullProcessImageName(hProcess, 0, sb, ref size))
                    {
                        return Path.GetFileNameWithoutExtension(sb.ToString());
                    }
                }
                finally
                {
                    Win32.CloseHandle(hProcess);
                }
            }
            
            // Attempt 2: Managed way (often fails for Admin apps)
            try
            {
                var proc = System.Diagnostics.Process.GetProcessById((int)pid);
                return proc.ProcessName;
            }
            catch 
            {
                return null;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (hotkeyForm != null) hotkeyForm.Dispose();
                if (trayIcon != null) trayIcon.Dispose();
                if (trayMenu != null) trayMenu.Dispose();
                if (loopTimer != null) loopTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // Hidden form just to receive Windows messages for global hotkeys
    public class HotkeyForm : Form
    {
        private ShyUIContext context;
        private const int HOTKEY_PAUSE = 1;
        private const int HOTKEY_TOGGLE_APP = 2;
        private const int MOD_ALT = 0x0001;
        private const int MOD_CONTROL = 0x0002;
        private const int VK_S = 0x53;
        private const int VK_T = 0x54;

        public HotkeyForm(ShyUIContext ctx)
        {
            context = ctx;
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;

            // Force handle creation for hotkey registration
            IntPtr h = this.Handle;

            if (!Win32.RegisterHotKey(this.Handle, HOTKEY_PAUSE, MOD_CONTROL | MOD_ALT, VK_S))
                Logger.Log("Failed to register hotkey Ctrl+Alt+S");
            else
                Logger.Log("Registered hotkey Ctrl+Alt+S");

            if (!Win32.RegisterHotKey(this.Handle, HOTKEY_TOGGLE_APP, MOD_CONTROL | MOD_ALT, VK_T))
                Logger.Log("Failed to register hotkey Ctrl+Alt+T");
            else
                Logger.Log("Registered hotkey Ctrl+Alt+T");

            this.FormClosing += (s, e) => {
                Win32.UnregisterHotKey(this.Handle, HOTKEY_PAUSE);
                Win32.UnregisterHotKey(this.Handle, HOTKEY_TOGGLE_APP);
            };
        }

        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(false); // Keep completely hidden
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312) // WM_HOTKEY
            {
                int id = m.WParam.ToInt32();
                if (id == HOTKEY_PAUSE)
                {
                    context.TogglePause();
                }
                else if (id == HOTKEY_TOGGLE_APP)
                {
                    context.ToggleCurrentApp();
                }
            }
            base.WndProc(ref m);
        }
    }

    public class SettingsForm : Form
    {
        private ShyUIContext context;
        private DataGridView grid;
        private Button saveBtn;

        public SettingsForm(ShyUIContext ctx)
        {
            context = ctx;
            this.Text = "Shy UI Settings";
            this.Size = new Size(300, 400);
            this.StartPosition = FormStartPosition.CenterScreen;

            grid = new DataGridView();
            grid.Dock = DockStyle.Fill;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.AllowUserToAddRows = true;
            grid.AllowUserToDeleteRows = true;
            
            grid.Columns.Add("ProcessName", "App Process Name");
            grid.Columns.Add("TopBarHeight", "Top Bar Height");

            var configs = context.GetConfigs();
            foreach (var kvp in configs)
            {
                grid.Rows.Add(kvp.Key, kvp.Value);
            }

            saveBtn = new Button();
            saveBtn.Text = "Save";
            saveBtn.Dock = DockStyle.Bottom;
            saveBtn.Height = 40;
            saveBtn.Click += SaveBtn_Click;

            this.Controls.Add(grid);
            this.Controls.Add(saveBtn);
        }

        private void SaveBtn_Click(object sender, EventArgs e)
        {
            var newConfigs = new Dictionary<string, int>();
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow) continue;
                if (row.Cells[0].Value != null && row.Cells[1].Value != null)
                {
                    string name = row.Cells[0].Value.ToString().Trim().ToLower();
                    int height;
                    if (int.TryParse(row.Cells[1].Value.ToString(), out height) && !string.IsNullOrEmpty(name))
                    {
                        newConfigs[name] = height;
                    }
                }
            }
            context.UpdateConfig(newConfigs);
            this.Close();
        }
    }

    // Win32 API Definitions
    public static class Win32
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool QueryFullProcessImageName(IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref int lpdwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hHandle);

        public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public const int SW_SHOWMAXIMIZED = 3;
        public const int SW_SHOWMINIMIZED = 2;
        public const int SW_RESTORE = 9;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_NOZORDER = 0x0004;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public POINT ptMinPosition;
            public POINT ptMaxPosition;
            public RECT rcNormalPosition;
        }
    }
}

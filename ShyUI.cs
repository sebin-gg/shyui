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
        private Dictionary<IntPtr, long> originalStyles;
        
        private string[] customFrameBlacklist = new string[] { "code", "msedge", "thorium", "antigravity", "chrome", "discord" };

        // Hotkey IDs
        private const int HOTKEY_PAUSE = 1;
        private const int HOTKEY_TOGGLE_APP = 2;

        private HotkeyForm hotkeyForm;

        public ShyUIContext()
        {
            configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configPath);
            appConfigs = new Dictionary<string, int>();
            windowRevealedState = new Dictionary<IntPtr, bool>();
            autoManagedWindows = new HashSet<IntPtr>();
            originalStyles = new Dictionary<IntPtr, long>();

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
            hotkeyForm.Show();

            loopTimer = new Timer();
            loopTimer.Interval = 20; 
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
            }
        }

        public void SaveConfig()
        {
            try
            {
                var lines = appConfigs.Select(kv => string.Format("{0}={1}", kv.Key, kv.Value)).ToList();
                File.WriteAllLines(configPath, lines);
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
                foreach (IntPtr handle in autoManagedWindows.ToList())
                {
                    RestoreWindow(handle);
                }
                windowRevealedState.Clear();
                autoManagedWindows.Clear();
            }
        }

        private void UpdateTrayIcon()
        {
            trayIcon.Text = isSystemPaused ? "Shy UI (Paused)" : "Shy UI (Active)";
            trayMenu.Items[1].Text = isSystemPaused ? "Resume (Ctrl+Alt+S)" : "Pause (Ctrl+Alt+S)";
        }

        private void RestoreWindow(IntPtr hWnd)
        {
            if (originalStyles.ContainsKey(hWnd))
            {
                long style = originalStyles[hWnd];
                Win32.SetWindowLongPtr(hWnd, Win32.GWL_STYLE, new IntPtr(style));
                Win32.SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0, 
                    Win32.SWP_FRAMECHANGED | Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
                originalStyles.Remove(hWnd);
            }
            else
            {
                Win32.ShowWindow(hWnd, Win32.SW_SHOWMAXIMIZED);
            }
        }

        public void ToggleCurrentApp()
        {
            IntPtr hWnd = Win32.GetForegroundWindow();
            if (hWnd == IntPtr.Zero) return;

            string procName = GetProcessName(hWnd);
            if (string.IsNullOrEmpty(procName)) return;
            procName = procName.ToLower();

            if (autoManagedWindows.Contains(hWnd))
            {
                RestoreWindow(hWnd);
                autoManagedWindows.Remove(hWnd);
                trayIcon.BalloonTipTitle = "Shy UI";
                trayIcon.BalloonTipText = "Removed window from Auto-Shy UI";
                trayIcon.ShowBalloonTip(2000);
                Logger.Log("Removed auto-managed window: " + procName);
                return;
            }

            if (appConfigs.ContainsKey(procName))
            {
                appConfigs.Remove(procName);
                RestoreWindow(hWnd);
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
                foreach (IntPtr handle in autoManagedWindows.ToList())
                {
                    RestoreWindow(handle);
                }
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
                        }
                        else
                        {
                            key.SetValue("ShyUI", "\"" + Application.ExecutablePath + "\"");
                            ((ToolStripMenuItem)sender).Checked = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to change startup settings: " + ex.Message, "Shy UI");
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
            bool isManaged = autoManagedWindows.Contains(hWnd);

            if (isInConfig || isMaximized || isManaged)
            {
                if (isMaximized && !isManaged && !isInConfig)
                {
                    Logger.Log("Auto-activating for maximized window: " + procName);
                    autoManagedWindows.Add(hWnd);
                    
                    bool isCustomFrame = customFrameBlacklist.Contains(procName);
                    if (!isCustomFrame)
                    {
                        long currentStyle = (long)Win32.GetWindowLongPtr(hWnd, Win32.GWL_STYLE);
                        originalStyles[hWnd] = currentStyle;
                    }
                }

                ManageWindow(hWnd, topBarHeight, procName);
            }
        }

        private void ManageWindow(IntPtr hWnd, int topBarHeight, string procName)
        {
            bool isCustomFrame = customFrameBlacklist.Contains(procName);

            var screen = Screen.FromHandle(hWnd);
            var workArea = screen.WorkingArea; 

            Win32.POINT p;
            Win32.GetCursorPos(out p);

            bool isRevealed = false;
            if (windowRevealedState.ContainsKey(hWnd))
                isRevealed = windowRevealedState[hWnd];

            if (!isRevealed)
            {
                if (p.Y <= screen.Bounds.Y + 2)
                {
                    isRevealed = true;
                }
            }
            else
            {
                if (p.Y > workArea.Y + topBarHeight + 15) // Hysteresis 15px
                {
                    isRevealed = false;
                }
            }

            windowRevealedState[hWnd] = isRevealed;

            if (isCustomFrame)
            {
                if (IsMaximized(hWnd))
                {
                    Win32.ShowWindow(hWnd, Win32.SW_RESTORE);
                }

                int targetY = isRevealed ? workArea.Y : workArea.Y - topBarHeight;
                int targetHeight = workArea.Height + topBarHeight;

                Win32.RECT rect;
                Win32.GetWindowRect(hWnd, out rect);
                
                if (rect.Left != workArea.X || rect.Top != targetY || 
                    (rect.Right - rect.Left) != workArea.Width || 
                    (rect.Bottom - rect.Top) != targetHeight)
                {
                    Win32.SetWindowPos(hWnd, IntPtr.Zero, workArea.X, targetY, workArea.Width, targetHeight, 
                        Win32.SWP_NOACTIVATE | Win32.SWP_NOZORDER);
                }
            }
            else
            {
                if (!originalStyles.ContainsKey(hWnd)) return;
                
                long originalStyle = originalStyles[hWnd];
                long hiddenStyle = originalStyle & ~Win32.WS_CAPTION & ~Win32.WS_THICKFRAME;
                long targetStyle = isRevealed ? originalStyle : hiddenStyle;
                
                long currentStyle = (long)Win32.GetWindowLongPtr(hWnd, Win32.GWL_STYLE);
                
                if (currentStyle != targetStyle)
                {
                    Win32.SetWindowLongPtr(hWnd, Win32.GWL_STYLE, new IntPtr(targetStyle));
                    Win32.SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0, 
                        Win32.SWP_FRAMECHANGED | Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
                }
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

            IntPtr h = this.Handle;

            Win32.RegisterHotKey(this.Handle, HOTKEY_PAUSE, MOD_CONTROL | MOD_ALT, VK_S);
            Win32.RegisterHotKey(this.Handle, HOTKEY_TOGGLE_APP, MOD_CONTROL | MOD_ALT, VK_T);

            this.FormClosing += (s, e) => {
                Win32.UnregisterHotKey(this.Handle, HOTKEY_PAUSE);
                Win32.UnregisterHotKey(this.Handle, HOTKEY_TOGGLE_APP);
            };
        }

        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(false);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312) 
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

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        public static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size == 8) return GetWindowLongPtr64(hWnd, nIndex);
            else return GetWindowLongPtr32(hWnd, nIndex);
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8) return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            else return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        public const int GWL_STYLE = -16;
        public const long WS_CAPTION = 0x00C00000L;
        public const long WS_THICKFRAME = 0x00040000L;

        public const int SW_SHOWMAXIMIZED = 3;
        public const int SW_SHOWMINIMIZED = 2;
        public const int SW_RESTORE = 9;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_FRAMECHANGED = 0x0020;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOSIZE = 0x0001;

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

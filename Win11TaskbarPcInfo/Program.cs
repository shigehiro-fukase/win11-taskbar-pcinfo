// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using static System.Windows.Forms.Application;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Win11TaskbarPcInfo.Properties;

using System.Text;

namespace Win11TaskbarPcInfo
{
    public static class Win32
    {
        public static (int Major, int Minor, int Build) OSVersion()
        {
            int major = 0, minor = 0, build = 0;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string s = RuntimeInformation.OSDescription;
                string[] r = Regex.Split(s, "[ \\.]");
                if ((r[0] == "Microsoft") && (r[1] == "Windows"))
                {
                    major = int.Parse(r[2]);
                    minor = int.Parse(r[3]);
                    build = int.Parse(r[4]);
                }
                return (major, minor, build);
            }

            // .NET 5
            major = System.Environment.OSVersion.Version.Major;
            minor = System.Environment.OSVersion.Version.Minor;
            build = System.Environment.OSVersion.Version.Build;
            return (major, minor, build);
        }
        public static bool IsWindows11()
        {
            var version = OSVersion();
            return version.Major >= 10 && version.Build >= 21996;
        }

        public static bool IsWindows11_22621()
        {
            var version = OSVersion();
            return version.Major >= 10 && version.Build >= 22621;
        }
        public static bool IsDarkMode()
        {
            try
            {
                const string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
                var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyPath);
                if (key != null)
                {
                    object value = key.GetValue("AppsUseLightTheme");
                    if (value is int intValue)
                        return intValue == 0; // 0 = Dark Mode
                }
            }
            catch { }
            return false; // Light Mode
        }
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20; // Windows 10 1809 or later
        public static void EnableDarkMode(IntPtr handle)
        {
            int useDark = IsDarkMode() ? 1 : 0;
            DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
        }

        [DllImport("user32.dll", EntryPoint = "FindWindowEx", CharSet = CharSet.Auto)] private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)] private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
        [DllImport("user32.dll", SetLastError = false)] public static extern IntPtr GetDesktopWindow();
#if false
        private enum GetWindow_Cmd : uint
        {
            GW_HWNDFIRST = 0,
            GW_HWNDLAST = 1,
            GW_HWNDNEXT = 2,
            GW_HWNDPREV = 3,
            GW_OWNER = 4,
            GW_CHILD = 5,
            GW_ENABLEDPOPUP = 6
        }
        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr GetWindow(IntPtr hWnd, GetWindow_Cmd uCmd);
        [DllImport("user32.dll")] private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
#endif
        [StructLayout(LayoutKind.Sequential)] internal struct RECT
        {
            internal int left;
            internal int top;
            internal int right;
            internal int bottom;
        }
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("User32.dll")] internal static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);
        [DllImport("user32.dll", EntryPoint = "SetParent")] internal static extern IntPtr SetParent(IntPtr windowHandle, IntPtr parentHandle);
        public enum GWLParameter
        {
            GWL_EXSTYLE = -20, //Sets a new extended window style
            GWL_HINSTANCE = -6, //Sets a new application instance handle.
            GWL_HWNDPARENT = -8, //Set window handle as parent
            GWL_ID = -12, //Sets a new identifier of the window.
            GWL_STYLE = -16, // Set new window style
            GWL_USERDATA = -21, //Sets the user data associated with the window. 
                                //This data is intended for use by the application 
                                //that created the window. Its value is initially zero.
            GWL_WNDPROC = -4 //Sets a new address for the window procedure.

        }
        [DllImport("user32.dll", SetLastError = true)] internal static extern uint GetWindowLong(IntPtr hWnd, GWLParameter nIndex);
        [DllImport("user32.dll", SetLastError = true)] internal static extern uint SetWindowLong(IntPtr hWnd, GWLParameter nIndex, uint pos);
        [DllImport("user32.dll")] internal static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);

        public static List<IntPtr> FindChildWindows(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow)
        {
            List<IntPtr> list = new List<IntPtr>();
            IntPtr hWndChild = FindWindowEx(hwndParent, hwndChildAfter, lpszClass, lpszWindow);
            while (hWndChild != IntPtr.Zero)
            {
                list.Add(hWndChild);
                list.AddRange(FindChildWindows(hWndChild, IntPtr.Zero, lpszClass, lpszWindow));

                // Next brother window
                hWndChild = FindWindowEx(hwndParent, hWndChild, lpszClass, lpszWindow);
            }
            return list;
        }
        public static Rectangle GetWindowSize(IntPtr handle)
        {
            RECT rct;

            if (!GetWindowRect(handle, out rct))
            {
                return Rectangle.Empty;
            }
            var myRect = new Rectangle();
            myRect.X = rct.left;
            myRect.Y = rct.top;
            myRect.Width = rct.right - rct.left;
            myRect.Height = rct.bottom - rct.top;
            return myRect;
        }

    };

    public class Taskbar
    {
        public Taskbar(bool ismain = false)
        {
            this.IsMainTaskbar = ismain;
        }
        public bool IsMainTaskbar { get; protected set; }
        public IntPtr TargetWnd = IntPtr.Zero;
        public IntPtr TrayWnd = IntPtr.Zero;
        public IntPtr ClockWnd = IntPtr.Zero;
        public TaskbarInfoControl TaskbarControl;
        public Rectangle PreviousRect = Rectangle.Empty;
    }
    public class TaskbarManager : IDisposable
    {
        List<Taskbar> TaskbarList = new List<Taskbar>();
        Taskbar MainTaskbar = null;

        private TaskbarManager() { }

        private void UpdatePosition(Taskbar taskbar, bool force = false)
        {

            var handle = taskbar.TargetWnd;
            Rectangle rect = Win32.GetWindowSize(handle);
            Rectangle offset = Rectangle.Empty;
            if (taskbar.TaskbarControl != null)
            {
                if (taskbar.IsMainTaskbar)
                {
                    if (taskbar.TrayWnd != IntPtr.Zero)
                    {
                        offset = Win32.GetWindowSize(taskbar.TrayWnd);
                    }
                }
                else
                {
                    if (taskbar.ClockWnd != IntPtr.Zero)
                    {
                        offset = Win32.GetWindowSize(taskbar.ClockWnd);
                    }
                    else if (Win32.IsWindows11_22621())
                    {
                        offset = new Rectangle(0, 0, 100, 0);
                    }
                }
            }

            if (force || taskbar.PreviousRect.Width == 0 || (offset.Width != taskbar.PreviousRect.Width && taskbar.TaskbarControl.IsHandleCreated))
            {
                Debug.WriteLine("UpdatePosition");
                taskbar.TaskbarControl?.Invoke((MethodInvoker)delegate
                {
                    taskbar.TaskbarControl.Visible = true;
                    taskbar.TaskbarControl.Left = (rect.Width - taskbar.TaskbarControl.Width - offset.Width);
                    Win32.RECT recDiff = new Win32.RECT();
                    recDiff.left = rect.Width - taskbar.TaskbarControl.Width - taskbar.PreviousRect.Width;
                    recDiff.top = 0;
                    recDiff.right = rect.Width - taskbar.TaskbarControl.Width - offset.Width;
                    recDiff.bottom = rect.Bottom;

                    int rawsize = Marshal.SizeOf(recDiff);
                    IntPtr ptr = Marshal.AllocHGlobal(rawsize);

                    Marshal.StructureToPtr(recDiff, ptr, true);

                    var ret = Win32.InvalidateRect(taskbar.TargetWnd, ptr, true);
                    Marshal.DestroyStructure(ptr, typeof(Win32.RECT));
                });

            }

            taskbar.PreviousRect = offset;
        }

        public bool AddControlsToTaskbars()
        {
            var Status = true;

            string taskbarClass = "Shell_TrayWnd";
            string trayClass = "TrayNotifyWnd";

            IntPtr hwndDesktopWindow = Win32.GetDesktopWindow();
            IntPtr hwndTaskbarArea = Win32.FindChildWindows(hwndDesktopWindow, IntPtr.Zero, taskbarClass, null).FirstOrDefault();
            if (hwndTaskbarArea == IntPtr.Zero) return false;
            IntPtr hwndTrayArea = Win32.FindChildWindows(hwndTaskbarArea, IntPtr.Zero, trayClass, null).FirstOrDefault();
            if (hwndTrayArea == IntPtr.Zero) return false;
            Status &= AddControlToTaskbar(hwndTaskbarArea, hwndTrayArea, true);

            if (Win32.IsWindows11())
            {
                taskbarClass = "Shell_SecondaryTrayWnd";
                trayClass = "Windows.UI.Composition.DesktopWindowContentBridge";

                List<IntPtr> hwndTaskbarAreaList = Win32.FindChildWindows(hwndDesktopWindow, IntPtr.Zero, taskbarClass, null);
                foreach (var hwndTbArea in hwndTaskbarAreaList)
                {
                    IntPtr hwndClockArea = IntPtr.Zero;
                    if (!Win32.IsWindows11_22621())
                    {
                        hwndClockArea = Win32.FindChildWindows(hwndTbArea, IntPtr.Zero, trayClass, null).LastOrDefault();
                    }
                    else
                    {
                        hwndClockArea = IntPtr.Zero;
                    }
                    Status &= AddControlToTaskbar(hwndTbArea, hwndClockArea, false);
                }
            }
            return Status;
        }

        private bool AddControlToTaskbar(IntPtr taskbarAreaHandle, IntPtr trayAreaHandle, bool isMainTaskbar)
        {
            Debug.WriteLine("AddControlToTaskbar");

            if (TaskbarList.Any(x => x.TaskbarControl?.Name == "taskbarInfoControlFor" + taskbarAreaHandle))
                return true;

            Taskbar tb = TaskbarList.Where(x => x.TargetWnd == taskbarAreaHandle).SingleOrDefault();
            if (tb == null)
            {
                tb = new Taskbar(isMainTaskbar);
                TaskbarList.Add(tb);
                tb.TargetWnd = taskbarAreaHandle;
            }

            if (isMainTaskbar)
            {
                MainTaskbar = tb;
                tb.TrayWnd = trayAreaHandle;
            }
            else if (trayAreaHandle != null)
            {
                tb.ClockWnd = trayAreaHandle;
            }

            var rect = Win32.GetWindowSize(taskbarAreaHandle);
            var rectTray = trayAreaHandle != null ? Win32.GetWindowSize(trayAreaHandle) : Rectangle.Empty;

            var taskbarControl = new TaskbarInfoControl();
            tb.TaskbarControl = taskbarControl;
            taskbarControl.Name = "taskbarInfoControlFor" + taskbarAreaHandle;

            Win32.SetParent(taskbarControl.Handle, taskbarAreaHandle);
            if (Win32.IsWindows11_22621())
            {
                Win32.SetWindowLong(taskbarControl.Handle, Win32.GWLParameter.GWL_EXSTYLE, (uint)(0x00000000L | 0x00010000L | 0x00080000 | 0x02000000L | 0x00000020L));
                Win32.SetLayeredWindowAttributes(taskbarControl.Handle, 0, 255, 0x00000001 | 0x00000002);
            }

            taskbarControl.Show();
            UpdatePosition(tb);

            return true;
        }

        public void RemoveControls()
        {
            Debug.WriteLine("RemoveControls");

            foreach (var taskbar in TaskbarList)
            {
                if (taskbar.TaskbarControl.Created && taskbar.TaskbarControl.IsHandleCreated)
                {
                    taskbar.TaskbarControl?.Invoke((MethodInvoker)delegate
                    {
                        taskbar.TaskbarControl?.Hide();
                        taskbar.TaskbarControl?.Dispose();
                    });
                }

                var ret = Win32.InvalidateRect(taskbar.TargetWnd, IntPtr.Zero, true);
            }
            TaskbarList.Clear();
        }

        public void Dispose()
        {
        }
        private static TaskbarManager _instance = null;
        public static TaskbarManager GetInstance()
        {
            if (_instance == null) _instance = new TaskbarManager();
            return _instance;
        }
    }

    static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        private static TaskbarManager taskbarManager;
        private static MyApplicationContext ctx;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            try
            {
                if (Win32.OSVersion().Major >= 6)
                    SetProcessDPIAware();
                if (!Win32.IsWindows11())
                {
                    MessageBox.Show("Please use this application on Windows 11+ devices only.");
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
#if (!DEBUG)
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);                    
                Application.ThreadException += Application_ThreadException;
#endif
                
                taskbarManager = TaskbarManager.GetInstance();                
                taskbarManager.AddControlsToTaskbars();

                AppDomain.CurrentDomain.ProcessExit += new EventHandler(CurrentDomain_ProcessExit);
             
                using (ctx = new MyApplicationContext())
                {                    
                    Application.Run(ctx);
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally 
            { 
                taskbarManager.Dispose(); 
            }
        }
#if (!DEBUG)
        private static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            MessageBox.Show($"Error: {e.Exception.Message}", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
#endif

        static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {            
            taskbarManager.RemoveControls();
        }

        public class MyApplicationContext : ApplicationContext
        {
            private NotifyIcon trayIcon;

            public MyApplicationContext()
            {
                // Initialize Tray Icon
                trayIcon = new NotifyIcon()
                {
                    Icon = (System.Drawing.Icon)Properties.Resources.icon,
                    ContextMenu = new ContextMenu(new MenuItem[] {
                        new MenuItem("Exit", Exit),
                    }),
                    Visible = true
                };              
            }            
 
            void Exit(object sender, EventArgs e)
            {               
                Application.Exit();
            }

            protected override void Dispose(bool disposing)
            {                
                trayIcon.Visible = false;
                base.Dispose(disposing);
            }
        }
    }
}

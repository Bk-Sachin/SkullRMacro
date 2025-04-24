using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Runtime.InteropServices; // Required for WindowChrome workaround
using System.Windows.Interop; // Required for WindowChrome workaround
using System;

namespace SkullRMacro
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml - Represents the main application window.
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // SourceInitialized += MainWindow_SourceInitialized; // Optional: For advanced custom chrome handling
        }

        // --- Custom Chrome / Window Behavior ---

        /// <summary>
        /// Handles the MouseLeftButtonDown event on the top bar grid to allow dragging the window.
        /// </summary>
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Allow dragging only when clicking on the TopBarGrid background
            if (e.OriginalSource == TopBarGrid && e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        /// <summary>
        /// Handles the Click event for the Minimize button.
        /// </summary>
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
            // NOTE: Standard WindowState.Minimized might not work perfectly with AllowsTransparency=True.
            // A common workaround involves temporarily setting AllowsTransparency=False,
            // minimizing, and then restoring it, or using Win32 API calls.
            // For simplicity, we use the standard way first.
        }

        // --- Event Handlers ---

        /// <summary>
        /// Handles the Click event for the Close button.
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// Handles the Click event for the Settings button (Placeholder).
        /// </summary>
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement settings logic
        }

        // Placeholders for top navigation buttons
        private void ProfileButton_Click(object sender, RoutedEventArgs e) { /* TODO */ }
        private void EditorButton_Click(object sender, RoutedEventArgs e) { /* TODO */ }
        private void MacrosButton_Click(object sender, RoutedEventArgs e) 
        {
             // Create and show the MacroEditor window as a dialog
            MacroEditor macroEditorWindow = new MacroEditor();
            macroEditorWindow.Owner = this; // Optional: Set owner
            macroEditorWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner; // Optional: Center on owner
            macroEditorWindow.ShowDialog(); // Show as modal dialog

            // You can optionally check the DialogResult if the MacroEditor sets it (e.g., on OK/Cancel)
            // if (macroEditorWindow.DialogResult == true)
            // {
            //     // Handle OK
            // }
            // else
            // {
            //     // Handle Cancel or close
            // }
        }

        // Placeholder for central red button
        private void CenterRedButton_Click(object sender, RoutedEventArgs e) { /* TODO */ }

        // Placeholders for Profile section buttons
        private void ProfileFolderButton_Click(object sender, RoutedEventArgs e) { /* TODO */ }
        private void ProfileDownloadButton_Click(object sender, RoutedEventArgs e) { /* TODO */ }
        private void ProfileSettingsButton_Click(object sender, RoutedEventArgs e) { /* TODO */ }
        /// <summary>
        /// Handles the Checked event for the Profile RadioButtons.
        /// </summary>
        private void ProfileRadio_Checked(object sender, RoutedEventArgs e)
        {
            // if (sender is RadioButton rb) { var selectedProfile = rb.Content; }
        }

        // Placeholders for Social Media buttons
        private void DiscordButton_Click(object sender, RoutedEventArgs e) { /* TODO: Open Link */ }
        private void TelegramButton_Click(object sender, RoutedEventArgs e) { /* TODO: Open Link */ }
        private void VkButton_Click(object sender, RoutedEventArgs e) { /* TODO: Open Link */ }
        private void InstagramButton_Click(object sender, RoutedEventArgs e) { /* TODO: Open Link */ }
        private void YoutubeButton_Click(object sender, RoutedEventArgs e) { /* TODO: Open Link */ }

        // Placeholder for handling keyboard key clicks (if needed for visualization/binding)
        private void KeyboardButton_Click(object sender, RoutedEventArgs e)
        {
            // if (sender is Button btn) { var key = btn.Content; }
            // TODO: Implement key press visualization or action
        }

        // Placeholder for handling ToggleButton changes
        private void DriverToggle_Checked(object sender, RoutedEventArgs e) { /* TODO */ }
        private void DriverToggle_Unchecked(object sender, RoutedEventArgs e) { /* TODO */ }
        private void SnapToggle_Checked(object sender, RoutedEventArgs e) { /* TODO */ }
        private void SnapToggle_Unchecked(object sender, RoutedEventArgs e) { /* TODO */ }

        // --- Specific Key Handlers ---
        private void KeyMenu_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement logic for the Menu key press
            // Example: Open a context menu or perform a specific action
            System.Diagnostics.Debug.WriteLine("Menu Key Clicked");
        }

        private void MouseMB4_Click()
        {

        }

        // --- Optional: Advanced Custom Chrome Handling ---
        /*
        // This helps fix issues with maximized window covering taskbar when using WindowStyle=None
        private void MainWindow_SourceInitialized(object sender, EventArgs e)
        {
            IntPtr handle = (new WindowInteropHelper(this)).Handle;
            HwndSource.FromHwnd(handle)?.AddHook(new HwndSourceHook(WindowProc));
        }

        private static IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case 0x0024: // WM_GETMINMAXINFO
                    WmGetMinMaxInfo(hwnd, lParam);
                    handled = true;
                    break;
            }
            return IntPtr.Zero;
        }

        private static void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
        {
            MINMAXINFO mmi = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO));

            // Adjust the maximized size and position to fit the screen, excluding the taskbar
            IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor != IntPtr.Zero)
            {
                MONITORINFO monitorInfo = new MONITORINFO();
                monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                GetMonitorInfo(monitor, ref monitorInfo);
                RECT rcWorkArea = monitorInfo.rcWork;
                RECT rcMonitorArea = monitorInfo.rcMonitor;
                mmi.ptMaxPosition.x = Math.Abs(rcWorkArea.left - rcMonitorArea.left);
                mmi.ptMaxPosition.y = Math.Abs(rcWorkArea.top - rcMonitorArea.top);
                mmi.ptMaxSize.x = Math.Abs(rcWorkArea.right - rcWorkArea.left);
                mmi.ptMaxSize.y = Math.Abs(rcWorkArea.bottom - rcWorkArea.top);
            }
            Marshal.StructureToPtr(mmi, lParam, true);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int left, top, right, bottom; }

        [StructLayout(LayoutKind.Sequential)]
        public struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public class MONITORINFO
        {
            public int cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            public RECT rcMonitor = new RECT();
            public RECT rcWork = new RECT();
            public int dwFlags = 0;
        }

        [DllImport("user32")]
        internal static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("User32")]
        internal static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);

        internal const int MONITOR_DEFAULTTONEAREST = 0x00000002;
        */
    }
}
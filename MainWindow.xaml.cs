using System.Windows;
using Clash_WPF.ViewModels;
using WinForms = System.Windows.Forms;

namespace Clash_WPF
{
    public partial class MainWindow : Window
    {
        private MainViewModel? _viewModel;
        private WinForms.NotifyIcon? _notifyIcon;

        public MainWindow()
        {
            InitializeComponent();
            InitializeTrayIcon();
            StateChanged += MainWindow_StateChanged;
        }

        private void InitializeTrayIcon()
        {
            _notifyIcon = new WinForms.NotifyIcon
            {
                Text = "Clash WPF",
                Visible = true,
            };

            var exePath = Environment.ProcessPath;
            if (exePath is not null)
                _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);

            _notifyIcon.DoubleClick += (_, _) =>
                Dispatcher.BeginInvoke(RestoreFromTray);

            var menu = new WinForms.ContextMenuStrip();
            menu.Items.Add("显示主窗口", null, (_, _) => Dispatcher.BeginInvoke(RestoreFromTray));
            menu.Items.Add("退出", null, (_, _) => Dispatcher.BeginInvoke(Close));
            _notifyIcon.ContextMenuStrip = menu;
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
                ShowInTaskbar = false;
            }
        }

        private void RestoreFromTray()
        {
            Show();
            ShowInTaskbar = true;
            WindowState = WindowState.Normal;
            Activate();
        }

        public void SetViewModel(MainViewModel vm)
        {
            _viewModel = vm;
            DataContext = vm;
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
            => Close();

        protected override void OnClosed(EventArgs e)
        {
            if (_notifyIcon is not null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
            _viewModel?.Cleanup();
            base.OnClosed(e);
        }
    }
}
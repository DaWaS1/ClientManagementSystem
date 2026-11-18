using ClientManagement.Admin.Services;
using ClientManagement.Shared.Dtos;
using System.Runtime.InteropServices;

namespace ClientManagement.Admin;

public partial class Form1 : Form
{
    private const int WM_SETREDRAW = 0x000B;

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(
        IntPtr hWnd,
        int msg,
        IntPtr wParam,
        IntPtr lParam);



    private enum AdminPage
    {
        Dashboard,
        Devices,
        Commands
    }

    private sealed class CommandHistoryItem
    {
        public DateTime CreatedAt { get; init; }
        public required string Hostname { get; init; }
        public required string CommandName { get; init; }
        public required string Status { get; init; }
    }

    private sealed class DeviceChoice
    {
        public required string AgentId { get; init; }
        public required string Hostname { get; init; }
        public bool IsOnline { get; init; }

        public override string ToString()
        {
            return $"{Hostname} ({(IsOnline ? "Online" : "Offline")})";
        }
    }

    private readonly HttpClient _httpClient;
    private readonly AdminApiClient _apiClient;
    private readonly System.Windows.Forms.Timer _refreshTimer;

    private readonly List<CommandHistoryItem> _commandHistory = [];
    private List<DeviceOverviewDto> _devices = [];

    private bool _isLoading;
    private bool _autoRefreshEnabled = true;
    private int _refreshIntervalSeconds = 10;

    private string _deviceSearchText = string.Empty;
    private string _deviceStatusFilter = "Összes";

    private AdminPage _currentPage = AdminPage.Devices;

    private Panel _sidebar = null!;
    private Panel _header = null!;
    private Panel _contentHost = null!;
    private Label _pageTitle = null!;
    private Label _serverStatus = null!;

    private Button _navDashboard = null!;
    private Button _navDevices = null!;
    private Button _navCommands = null!;

    private FlowLayoutPanel? _deviceFlow;
    private TextBox? _deviceSearchBox;
    private ComboBox? _deviceStatusCombo;

    public Form1()
    {
        InitializeComponent();

        DoubleBuffered = true;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer,
            true);
        UpdateStyles();

        Text = "Client Management Console";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1180, 760);
        Size = new Size(1380, 860);
        Font = new Font("Segoe UI", 10F, FontStyle.Regular);

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5080/"),
            Timeout = TimeSpan.FromSeconds(10)
        };

        _apiClient = new AdminApiClient(_httpClient);

        _refreshTimer = new System.Windows.Forms.Timer
        {
            Interval = _refreshIntervalSeconds * 1000
        };

        _refreshTimer.Tick += RefreshTimer_Tick;
        _refreshTimer.Start();

        BuildShell();

        Shown += Form1_Shown;
        FormClosed += Form1_FormClosed;
    }

    // =====================================================
    // SHELL
    // =====================================================

    private void BuildShell()
    {
        SuspendLayout();
        Controls.Clear();

        BackColor = ThemeManager.Background;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = ThemeManager.Background
        };

        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        Controls.Add(root);

        _sidebar = BuildSidebar();
        root.Controls.Add(_sidebar, 0, 0);

        var rightSide = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = ThemeManager.Background
        };

        rightSide.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        rightSide.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(rightSide, 1, 0);

        _header = BuildHeader();
        rightSide.Controls.Add(_header, 0, 0);

        _contentHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ThemeManager.Background,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };

        rightSide.Controls.Add(_contentHost, 0, 1);

        ShowPage(_currentPage);

        ResumeLayout(true);
    }

    private Panel BuildSidebar()
    {
        var sidebar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ThemeManager.Sidebar,
            Padding = new Padding(18, 22, 18, 18)
        };

        var menu = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = ThemeManager.Sidebar,
            Padding = Padding.Empty,
            AutoScroll = false
        };

        sidebar.Controls.Add(menu);

        var brand = new Label
        {
            Text = "Client\nManagement",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 17F, FontStyle.Bold),
            AutoSize = false,
            Width = 174,
            Height = 66
        };

        var subtitle = new Label
        {
            Text = "ADMIN CONSOLE",
            ForeColor = Color.FromArgb(156, 163, 175),
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            AutoSize = false,
            Width = 174,
            Height = 30
        };

        var separator = new Panel
        {
            Width = 174,
            Height = 1,
            BackColor = Color.FromArgb(55, 65, 81),
            Margin = new Padding(0, 4, 0, 18)
        };

        menu.Controls.Add(brand);
        menu.Controls.Add(subtitle);
        menu.Controls.Add(separator);

        _navDashboard = CreateNavigationButton("Dashboard", AdminPage.Dashboard);
        _navDevices = CreateNavigationButton("Eszközök", AdminPage.Devices);
        _navCommands = CreateNavigationButton("Parancsok", AdminPage.Commands);

        menu.Controls.Add(_navDashboard);
        menu.Controls.Add(_navDevices);
        menu.Controls.Add(_navCommands);

        UpdateNavigationStyles();

        return sidebar;
    }

    private Button CreateNavigationButton(string text, AdminPage page)
    {
        var button = new Button
        {
            Text = text,
            Width = 174,
            Height = 46,
            FlatStyle = FlatStyle.Flat,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(14, 0, 0, 0),
            Margin = new Padding(0, 4, 0, 4),
            Cursor = Cursors.Hand,
            ForeColor = Color.FromArgb(209, 213, 219),
            BackColor = ThemeManager.Sidebar
        };

        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = ThemeManager.SidebarHover;

        button.Click += (_, _) => ShowPage(page);

        return button;
    }

    private void UpdateNavigationStyles()
    {
        SetNavigationStyle(_navDashboard, _currentPage == AdminPage.Dashboard);
        SetNavigationStyle(_navDevices, _currentPage == AdminPage.Devices);
        SetNavigationStyle(_navCommands, _currentPage == AdminPage.Commands);
    }

    private static void SetNavigationStyle(Button button, bool active)
    {
        button.BackColor = active ? ThemeManager.Primary : ThemeManager.Sidebar;
        button.ForeColor = Color.White;
        button.Font = new Font("Segoe UI", 10F, active ? FontStyle.Bold : FontStyle.Regular);
    }

    private Panel BuildHeader()
    {
        var header = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ThemeManager.Surface,
            Padding = new Padding(24, 0, 24, 0),
            Margin = Padding.Empty
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = ThemeManager.Surface
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));

        _pageTitle = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 17F, FontStyle.Bold),
            ForeColor = ThemeManager.TextPrimary
        };

        _serverStatus = new Label
        {
            Dock = DockStyle.Fill,
            Text = "● Kapcsolódás...",
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = ThemeManager.TextSecondary,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Padding = new Padding(0, 0, 4, 0)
        };

        layout.Controls.Add(_pageTitle, 0, 0);
        layout.Controls.Add(_serverStatus, 1, 0);
        header.Controls.Add(layout);

        return header;
    }

    // =====================================================
    // PAGE NAVIGATION
    // =====================================================

    private void ShowPage(AdminPage page)
    {
        _currentPage = page;

        UpdateNavigationStyles();

        string title = page switch
        {
            AdminPage.Dashboard => "Dashboard",
            AdminPage.Devices => "Eszközök",
            AdminPage.Commands => "Parancsok",
            _ => "Client Management"
        };

        _pageTitle.Text = title;


        bool redrawDisabled =
            _contentHost.IsHandleCreated;


        if (redrawDisabled)
        {
            SendMessage(
                _contentHost.Handle,
                WM_SETREDRAW,
                IntPtr.Zero,
                IntPtr.Zero);
        }


        _contentHost.SuspendLayout();

        try
        {
            // Régi oldal eltávolítása
            Control[] oldControls =
                _contentHost.Controls
                    .Cast<Control>()
                    .ToArray();

            _contentHost.Controls.Clear();


            // Új oldal felépítése
            Control content =
                page switch
                {
                    AdminPage.Dashboard =>
                        BuildDashboardPage(),

                    AdminPage.Devices =>
                        BuildDevicesPage(),

                    AdminPage.Commands =>
                        BuildCommandsPage(),

                    _ =>
                        BuildDashboardPage()
                };


            _contentHost.Controls.Add(
                content);


            // A régi controlokat felszabadítjuk
            foreach (Control control in oldControls)
            {
                control.Dispose();
            }
        }
        finally
        {
            _contentHost.ResumeLayout(
                true);


            if (redrawDisabled)
            {
                SendMessage(
                    _contentHost.Handle,
                    WM_SETREDRAW,
                    new IntPtr(1),
                    IntPtr.Zero);


                _contentHost.Invalidate(
                    true);

                _contentHost.Update();
            }
        }
    }

    // =====================================================
    // DASHBOARD
    // =====================================================

    private Control BuildDashboardPage()
    {
        var page = CreatePageContainer();

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = ThemeManager.Background,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 48));

        layout.Controls.Add(BuildSummaryCards(), 0, 0);
        layout.Controls.Add(BuildRecentDevicesCard(), 0, 1);
        layout.Controls.Add(BuildAttentionCard(), 0, 2);

        page.Controls.Add(layout);
        return page;
    }

    private Control BuildSummaryCards()
    {
        int total = _devices.Count;
        int online = _devices.Count(device => device.IsOnline);
        int offline = total - online;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = ThemeManager.Background
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));

        layout.Controls.Add(CreateMetricCard("Összes eszköz", total.ToString(), ThemeManager.Primary), 0, 0);
        layout.Controls.Add(CreateMetricCard("Online", online.ToString(), ThemeManager.Success), 1, 0);
        layout.Controls.Add(CreateMetricCard("Offline", offline.ToString(), ThemeManager.Danger), 2, 0);

        return layout;
    }

    private Control CreateMetricCard(string title, string value, Color accent)
    {
        var card = CreateCardPanel(new Padding(18, 14, 18, 14));
        card.Dock = DockStyle.Fill;
        card.Margin = new Padding(0, 0, 12, 12);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = ThemeManager.Surface,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 10));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 55));

        var accentBar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = accent,
            Margin = new Padding(0, 4, 6, 4)
        };

        var titleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            Font = new Font("Segoe UI", 9.5F),
            ForeColor = ThemeManager.TextSecondary,
            Padding = new Padding(8, 0, 0, 1)
        };

        var valueLabel = new Label
        {
            Text = value,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            Font = new Font("Segoe UI", 22F, FontStyle.Bold),
            ForeColor = ThemeManager.TextPrimary,
            Padding = new Padding(8, 0, 0, 0)
        };

        root.Controls.Add(accentBar, 0, 0);
        root.SetRowSpan(accentBar, 2);
        root.Controls.Add(titleLabel, 1, 0);
        root.Controls.Add(valueLabel, 1, 1);

        card.Controls.Add(root);
        return card;
    }

    private Control BuildRecentDevicesCard()
    {
        var card = CreateSectionCard("Legutóbb látott eszközök", out Panel body);

        var list = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = ThemeManager.Surface,
            Padding = new Padding(0, 4, 0, 0)
        };

        body.Controls.Add(list);

        List<DeviceOverviewDto> recent = _devices
            .OrderByDescending(device => device.LastSeenUtc)
            .Take(6)
            .ToList();

        if (recent.Count == 0)
        {
            list.Controls.Add(CreateEmptyState("Még nincs megjeleníthető eszköz."));
        }
        else
        {
            foreach (DeviceOverviewDto device in recent)
            {
                list.Controls.Add(CreateCompactDeviceRow(device));
            }
        }

        return card;
    }

    private Control BuildAttentionCard()
    {
        var card = CreateSectionCard("Figyelmet igényel", out Panel body);

        var list = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = ThemeManager.Surface,
            Padding = new Padding(0, 4, 0, 0)
        };

        body.Controls.Add(list);

        List<DeviceOverviewDto> offline = _devices
            .Where(device => !device.IsOnline)
            .OrderBy(device => device.Hostname)
            .ToList();

        if (offline.Count == 0)
        {
            list.Controls.Add(CreateEmptyState("Nincs offline eszköz. Minden rendben."));
        }
        else
        {
            foreach (DeviceOverviewDto device in offline)
            {
                list.Controls.Add(CreateCompactDeviceRow(device));
            }
        }

        return card;
    }

    private Control CreateCompactDeviceRow(DeviceOverviewDto device)
    {
        var row = new Panel
        {
            Width = 930,
            Height = 54,
            BackColor = ThemeManager.SurfaceAlternative,
            Margin = new Padding(0, 0, 0, 6),
            Padding = new Padding(12, 0, 12, 0),
            Cursor = Cursors.Hand
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = ThemeManager.SurfaceAlternative
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));

        layout.Controls.Add(CreateTextLabel(device.Hostname, true), 0, 0);
        layout.Controls.Add(CreateTextLabel(device.OperatingSystem ?? "-"), 1, 0);
        layout.Controls.Add(CreateTextLabel(device.IpAddress ?? "-"), 2, 0);
        layout.Controls.Add(CreateStatusLabel(device.IsOnline), 3, 0);

        row.Controls.Add(layout);
        AttachClickRecursive(row, () => OpenDeviceDetails(device.AgentId));

        return row;
    }

    // =====================================================
    // DEVICES PAGE
    // =====================================================

    private Control BuildDevicesPage()
    {
        var page = CreatePageContainer();

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = ThemeManager.Background,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };

        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(BuildSummaryCards(), 0, 0);
        layout.Controls.Add(BuildDeviceToolbar(), 0, 1);

        _deviceFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = ThemeManager.Background,
            Padding = new Padding(0, 4, 8, 0),
            Margin = Padding.Empty
        };

        _deviceFlow.SizeChanged += (_, _) => ResizeDeviceCards();

        layout.Controls.Add(_deviceFlow, 0, 2);
        page.Controls.Add(layout);

        RebuildDeviceCards();
        return page;
    }

    private Control BuildDeviceToolbar()
    {
        var card = CreateCardPanel(new Padding(14, 10, 14, 10));
        card.Dock = DockStyle.Fill;
        card.Margin = new Padding(0, 0, 12, 12);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = ThemeManager.Surface
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 12));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));

        _deviceSearchBox = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "Keresés gépnév, IP, OS vagy modell alapján...",
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = ThemeManager.InputBackground,
            ForeColor = ThemeManager.TextPrimary,
            Font = new Font("Segoe UI", 10F),
            Text = _deviceSearchText,
            Margin = new Padding(0, 4, 14, 4)
        };

        _deviceSearchBox.TextChanged += (_, _) =>
        {
            _deviceSearchText = _deviceSearchBox.Text;
            RebuildDeviceCards();
        };

        _deviceStatusCombo = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = ThemeManager.InputBackground,
            ForeColor = ThemeManager.TextPrimary,
            Font = new Font("Segoe UI", 9.5F),
            Margin = new Padding(0, 4, 0, 4)
        };

        _deviceStatusCombo.Items.AddRange(["Összes", "Online", "Offline"]);
        _deviceStatusCombo.SelectedItem = _deviceStatusFilter;

        _deviceStatusCombo.SelectedIndexChanged += (_, _) =>
        {
            _deviceStatusFilter = _deviceStatusCombo.SelectedItem?.ToString() ?? "Összes";
            RebuildDeviceCards();
        };

        Button refreshButton = CreatePrimaryButton("Frissítés", 120);
        refreshButton.Dock = DockStyle.Fill;
        refreshButton.Margin = new Padding(0, 3, 0, 3);
        refreshButton.Click += async (_, _) => await LoadDevicesAsync(showErrors: true);

        layout.Controls.Add(_deviceSearchBox, 0, 0);
        layout.Controls.Add(_deviceStatusCombo, 1, 0);
        layout.Controls.Add(refreshButton, 3, 0);

        card.Controls.Add(layout);
        return card;
    }

    private void RebuildDeviceCards()
    {
        if (_deviceFlow is null ||
            _deviceFlow.IsDisposed)
        {
            return;
        }


        IEnumerable<DeviceOverviewDto> query =
            _devices;


        // =====================================================
        // STÁTUSZ SZŰRÉS
        // =====================================================

        if (_deviceStatusFilter == "Online")
        {
            query =
                query.Where(
                    device =>
                        device.IsOnline);
        }
        else if (_deviceStatusFilter == "Offline")
        {
            query =
                query.Where(
                    device =>
                        !device.IsOnline);
        }


        // =====================================================
        // KERESÉS
        // =====================================================

        if (!string.IsNullOrWhiteSpace(
            _deviceSearchText))
        {
            string search =
                _deviceSearchText.Trim();


            query =
                query.Where(
                    device =>
                        device.Hostname.Contains(
                            search,
                            StringComparison.OrdinalIgnoreCase) ||

                        (device.IpAddress?.Contains(
                            search,
                            StringComparison.OrdinalIgnoreCase)
                            ?? false) ||

                        (device.OperatingSystem?.Contains(
                            search,
                            StringComparison.OrdinalIgnoreCase)
                            ?? false) ||

                        (device.Model?.Contains(
                            search,
                            StringComparison.OrdinalIgnoreCase)
                            ?? false) ||

                        device.AgentId.Contains(
                            search,
                            StringComparison.OrdinalIgnoreCase));
        }


        List<DeviceOverviewDto> result =
            query
                .OrderByDescending(
                    device =>
                        device.IsOnline)
                .ThenBy(
                    device =>
                        device.Hostname)
                .ToList();


        bool redrawDisabled =
            _deviceFlow.IsHandleCreated;


        if (redrawDisabled)
        {
            SendMessage(
                _deviceFlow.Handle,
                WM_SETREDRAW,
                IntPtr.Zero,
                IntPtr.Zero);
        }


        _deviceFlow.SuspendLayout();

        try
        {
            Control[] oldControls =
                _deviceFlow.Controls
                    .Cast<Control>()
                    .ToArray();


            _deviceFlow.Controls.Clear();


            if (result.Count == 0)
            {
                _deviceFlow.Controls.Add(
                    CreateEmptyState(
                        "Nincs a szűrésnek megfelelő eszköz."));
            }
            else
            {
                foreach (DeviceOverviewDto device in result)
                {
                    _deviceFlow.Controls.Add(
                        CreateDeviceCard(
                            device));
                }
            }


            ResizeDeviceCards();


            foreach (Control control in oldControls)
            {
                control.Dispose();
            }
        }
        finally
        {
            _deviceFlow.ResumeLayout(
                true);


            if (redrawDisabled)
            {
                SendMessage(
                    _deviceFlow.Handle,
                    WM_SETREDRAW,
                    new IntPtr(1),
                    IntPtr.Zero);


                _deviceFlow.Invalidate(
                    true);

                _deviceFlow.Update();
            }
        }
    }

    private Control CreateDeviceCard(DeviceOverviewDto device)
    {
        var card = CreateCardPanel(new Padding(18, 14, 18, 14));
        card.Height = 148;
        card.Margin = new Padding(0, 0, 0, 10);
        card.Cursor = Cursors.Hand;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = ThemeManager.Surface,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = ThemeManager.Surface,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115));

        var hostname = new Label
        {
            Text = device.Hostname,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = ThemeManager.TextPrimary,
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            AutoEllipsis = true
        };

        var status = new Label
        {
            Text = device.IsOnline ? "● Online" : "● Offline",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = device.IsOnline ? ThemeManager.Success : ThemeManager.Danger,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
        };

        var openLabel = new Label
        {
            Text = "Részletek  →",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = ThemeManager.Primary,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };

        header.Controls.Add(hostname, 0, 0);
        header.Controls.Add(status, 1, 0);
        header.Controls.Add(openLabel, 2, 0);

        var info = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1,
            BackColor = ThemeManager.Surface,
            Margin = Padding.Empty,
            Padding = new Padding(0, 4, 0, 0)
        };

        info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24));
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16));
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26));
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 13));
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 21));

        string modelText = string.Join(" ", new[] { device.Manufacturer, device.Model }
            .Where(value => !string.IsNullOrWhiteSpace(value)));

        info.Controls.Add(CreateKeyValueBlock(
            "Operációs rendszer",
            $"{device.OperatingSystem ?? "-"} {device.OperatingSystemVersion ?? string.Empty}".Trim()), 0, 0);

        info.Controls.Add(CreateKeyValueBlock("IP-cím", device.IpAddress ?? "-"), 1, 0);
        info.Controls.Add(CreateKeyValueBlock("Gyártó / modell", string.IsNullOrWhiteSpace(modelText) ? "-" : modelText), 2, 0);
        info.Controls.Add(CreateKeyValueBlock("Memória", FormatBytes(device.TotalMemoryBytes)), 3, 0);
        info.Controls.Add(CreateKeyValueBlock(
            "Utolsó kapcsolat",
            device.LastSeenUtc.ToLocalTime().ToString("yyyy. MM. dd. HH:mm:ss")), 4, 0);

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(info, 0, 1);

        card.Controls.Add(root);
        AttachClickRecursive(card, () => OpenDeviceDetails(device.AgentId));

        return card;
    }

    private void ResizeDeviceCards()
    {
        if (_deviceFlow is null)
        {
            return;
        }

        int width = Math.Max(720, _deviceFlow.ClientSize.Width - 26);

        foreach (Control control in _deviceFlow.Controls)
        {
            control.Width = width;
        }
    }

    // =====================================================
    // COMMANDS PAGE
    // =====================================================

    private Control BuildCommandsPage()
    {
        var page = CreatePageContainer();

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = ThemeManager.Background,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };

        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 160));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(BuildCommandComposerCard(), 0, 0);
        layout.Controls.Add(BuildCommandHistoryCard(), 0, 1);

        page.Controls.Add(layout);
        return page;
    }

    private Control BuildCommandComposerCard()
    {
        var card = CreateSectionCard("Parancs küldése", out Panel body);
        card.Margin = new Padding(0, 0, 12, 12);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2,
            BackColor = ThemeManager.Surface,
            Padding = new Padding(0, 8, 0, 0)
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var combo = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = ThemeManager.InputBackground,
            ForeColor = ThemeManager.TextPrimary,
            Font = new Font("Segoe UI", 9.5F),
            Margin = new Padding(0, 3, 12, 3)
        };

        foreach (DeviceOverviewDto device in _devices.OrderBy(device => device.Hostname))
        {
            combo.Items.Add(new DeviceChoice
            {
                AgentId = device.AgentId,
                Hostname = device.Hostname,
                IsOnline = device.IsOnline
            });
        }

        if (combo.Items.Count > 0)
        {
            combo.SelectedIndex = 0;
        }

        var commandType = new Label
        {
            Text = "Adatok frissítése",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = ThemeManager.TextPrimary,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            BackColor = ThemeManager.SurfaceAlternative,
            Padding = new Padding(12, 0, 0, 0),
            Margin = new Padding(0, 3, 12, 3)
        };

        Button sendButton = CreatePrimaryButton("Parancs küldése", 160);
        sendButton.Dock = DockStyle.Fill;
        sendButton.Margin = new Padding(0, 3, 0, 3);

        var note = new Label
        {
            Text = "A RequestSnapshot parancs a Serveren várakozik, majd az Agent a következő polling ciklusban lekéri és végrehajtja.",
            Dock = DockStyle.Fill,
            ForeColor = ThemeManager.TextSecondary,
            Font = new Font("Segoe UI", 9F),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 8, 0, 0)
        };

        layout.Controls.Add(combo, 0, 0);
        layout.Controls.Add(commandType, 1, 0);
        layout.Controls.Add(sendButton, 2, 0);
        layout.Controls.Add(note, 0, 1);
        layout.SetColumnSpan(note, 3);

        sendButton.Click += async (_, _) =>
        {
            if (combo.SelectedItem is not DeviceChoice selected)
            {
                MessageBox.Show(
                    "Nincs kiválasztott eszköz.",
                    "Parancs küldése",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return;
            }

            try
            {
                sendButton.Enabled = false;
                sendButton.Text = "Küldés...";

                await _apiClient.RequestSnapshotAsync(selected.AgentId);

                _commandHistory.Insert(0, new CommandHistoryItem
                {
                    CreatedAt = DateTime.Now,
                    Hostname = selected.Hostname,
                    CommandName = "Adatok frissítése",
                    Status = "Elküldve"
                });

                ShowPage(AdminPage.Commands);
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    "Nem sikerült elküldeni a parancsot.\n\n" + exception.Message,
                    "Kommunikációs hiba",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                sendButton.Enabled = true;
                sendButton.Text = "Parancs küldése";
            }
        };

        body.Controls.Add(layout);
        return card;
    }

    private Control BuildCommandHistoryCard()
    {
        var card = CreateSectionCard("E munkamenet parancsai", out Panel body);

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = ThemeManager.Surface,
            Padding = new Padding(0, 6, 0, 0)
        };

        body.Controls.Add(flow);

        if (_commandHistory.Count == 0)
        {
            flow.Controls.Add(CreateEmptyState("Ebben a munkamenetben még nem küldtél parancsot."));
        }
        else
        {
            foreach (CommandHistoryItem item in _commandHistory.Take(50))
            {
                var row = new Panel
                {
                    Width = 930,
                    Height = 56,
                    BackColor = ThemeManager.SurfaceAlternative,
                    Margin = new Padding(0, 0, 0, 6),
                    Padding = new Padding(12, 0, 12, 0)
                };

                var rowLayout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 4,
                    RowCount = 1,
                    BackColor = ThemeManager.SurfaceAlternative
                };

                rowLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
                rowLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
                rowLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
                rowLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));

                rowLayout.Controls.Add(CreateTextLabel(item.CreatedAt.ToString("HH:mm:ss")), 0, 0);
                rowLayout.Controls.Add(CreateTextLabel(item.Hostname, true), 1, 0);
                rowLayout.Controls.Add(CreateTextLabel(item.CommandName), 2, 0);

                var status = CreateTextLabel(item.Status, true);
                status.ForeColor = ThemeManager.Primary;
                rowLayout.Controls.Add(status, 3, 0);

                row.Controls.Add(rowLayout);
                flow.Controls.Add(row);
            }
        }

        return card;
    }

    // =====================================================
    // DATA LOADING
    // =====================================================

    private async void Form1_Shown(object? sender, EventArgs e)
    {
        await LoadDevicesAsync(showErrors: true);
    }

    private async void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        if (_autoRefreshEnabled)
        {
            await LoadDevicesAsync(showErrors: false);
        }
    }

    private async Task LoadDevicesAsync(bool showErrors)
    {
        if (_isLoading)
        {
            return;
        }

        _isLoading = true;

        try
        {
            _devices = await _apiClient.GetDevicesAsync();
            SetServerStatus(true);

            if (_currentPage is AdminPage.Dashboard or AdminPage.Devices or AdminPage.Commands)
            {
                ShowPage(_currentPage);
            }
        }
        catch (Exception exception)
        {
            SetServerStatus(false);

            if (showErrors)
            {
                MessageBox.Show(
                    "Nem sikerült betölteni az eszközöket.\n\n" + exception.Message,
                    "Kommunikációs hiba",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void SetServerStatus(bool available)
    {
        _serverStatus.Text = available ? "● Server elérhető" : "● Server nem elérhető";
        _serverStatus.ForeColor = available ? ThemeManager.Success : ThemeManager.Danger;
    }

    // =====================================================
    // DEVICE DETAILS
    // =====================================================

    private void OpenDeviceDetails(string agentId)
    {
        var details = new DeviceDetailsForm(_apiClient, agentId);
        details.ShowDialog(this);
    }

    // =====================================================
    // SHARED UI HELPERS
    // =====================================================

    private static Panel CreatePageContainer()
    {
        return new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ThemeManager.Background,
            Padding = new Padding(24, 22, 24, 24),
            Margin = Padding.Empty
        };
    }

    private static Panel CreateCardPanel(Padding padding)
    {
        var panel = new Panel
        {
            BackColor = ThemeManager.Surface,
            Padding = padding,
            Margin = Padding.Empty
        };

        panel.Paint += (_, e) =>
        {
            ControlPaint.DrawBorder(
                e.Graphics,
                panel.ClientRectangle,
                ThemeManager.Border,
                ButtonBorderStyle.Solid);
        };

        return panel;
    }

    private static Panel CreateSectionCard(string title, out Panel body)
    {
        var card = CreateCardPanel(new Padding(18));
        card.Dock = DockStyle.Fill;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = ThemeManager.Surface,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };

        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var titleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = ThemeManager.TextPrimary,
            Font = new Font("Segoe UI", 11.5F, FontStyle.Bold)
        };

        body = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ThemeManager.Surface,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        layout.Controls.Add(titleLabel, 0, 0);
        layout.Controls.Add(body, 0, 1);
        card.Controls.Add(layout);

        return card;
    }

    private static Label CreateEmptyState(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = false,
            Width = 900,
            Height = 56,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = ThemeManager.TextSecondary,
            Font = new Font("Segoe UI", 9.5F),
            BackColor = ThemeManager.Surface
        };
    }

    private static Label CreateTextLabel(string text, bool bold = false)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = ThemeManager.TextPrimary,
            Font = new Font("Segoe UI", 9.3F, bold ? FontStyle.Bold : FontStyle.Regular),
            AutoEllipsis = true
        };
    }

    private static Label CreateStatusLabel(bool online)
    {
        return new Label
        {
            Text = online ? "● Online" : "● Offline",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = online ? ThemeManager.Success : ThemeManager.Danger,
            Font = new Font("Segoe UI", 9.3F, FontStyle.Bold),
            AutoEllipsis = true
        };
    }

    private static Control CreateKeyValueBlock(string caption, string value)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = ThemeManager.Surface,
            Margin = Padding.Empty,
            Padding = new Padding(0, 2, 8, 2)
        };

        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var captionLabel = new Label
        {
            Text = caption,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            ForeColor = ThemeManager.TextSecondary,
            Font = new Font("Segoe UI", 8.3F)
        };

        var valueLabel = new Label
        {
            Text = value,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            ForeColor = ThemeManager.TextPrimary,
            Font = new Font("Segoe UI", 9.2F, FontStyle.Bold),
            AutoEllipsis = true
        };

        layout.Controls.Add(captionLabel, 0, 0);
        layout.Controls.Add(valueLabel, 0, 1);

        return layout;
    }

    private static Button CreatePrimaryButton(string text, int width)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 38,
            FlatStyle = FlatStyle.Flat,
            BackColor = ThemeManager.Primary,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.3F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };

        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private static Button CreateSecondaryButton(string text, int width)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 38,
            FlatStyle = FlatStyle.Flat,
            BackColor = ThemeManager.SurfaceAlternative,
            ForeColor = ThemeManager.TextPrimary,
            Font = new Font("Segoe UI", 9.3F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };

        button.FlatAppearance.BorderColor = ThemeManager.Border;
        button.FlatAppearance.BorderSize = 1;
        return button;
    }

    private static void AttachClickRecursive(Control control, Action action)
    {
        control.Click += (_, _) => action();
        control.Cursor = Cursors.Hand;

        foreach (Control child in control.Controls)
        {
            AttachClickRecursive(child, action);
        }
    }

    private static string FormatBytes(ulong bytes)
    {
        const double gigabyte = 1024d * 1024d * 1024d;
        return $"{bytes / gigabyte:N1} GB";
    }

    private void Form1_FormClosed(object? sender, FormClosedEventArgs e)
    {
        _refreshTimer.Stop();
        _refreshTimer.Dispose();
        _httpClient.Dispose();
    }
}

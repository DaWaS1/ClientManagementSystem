using ClientManagement.Admin.Services;
using ClientManagement.Shared.Dtos;

namespace ClientManagement.Admin;

public sealed class DeviceDetailsForm : Form
{
    private readonly AdminApiClient _apiClient;
    private readonly string _agentId;
    private readonly System.Windows.Forms.Timer _statusRefreshTimer;

    private bool _isRefreshingStatus;

    private DeviceSnapshotDto? _currentDevice;
    private DeviceOverviewDto? _currentOverview;

    private Label _lblTitle = null!;
    private Label _lblSubtitle = null!;
    private Label _lblStatus = null!;
    private Label _lblActionMessage = null!;
    private Button _btnRefreshData = null!;
    private Panel _infoHost = null!;
    private FlowLayoutPanel _diskHost = null!;

    public DeviceDetailsForm(AdminApiClient apiClient, string agentId)
    {
        _apiClient = apiClient;
        _agentId = agentId;

        Text = "Eszköz részletei";
        StartPosition = FormStartPosition.CenterParent;
        Width = 1120;
        Height = 780;
        MinimumSize = new Size(920, 650);
        Font = new Font("Segoe UI", 10F);
        DoubleBuffered = true;

        _statusRefreshTimer = new System.Windows.Forms.Timer
        {
            Interval = 10_000
        };

        _statusRefreshTimer.Tick += StatusRefreshTimer_Tick;

        ThemeManager.ThemeChanged += ThemeManager_ThemeChanged;

        BuildInterface();

        Shown += DeviceDetailsForm_Shown;
        FormClosed += DeviceDetailsForm_FormClosed;
    }

    // =====================================================
    // UI BUILD
    // =====================================================

    private void BuildInterface()
    {
        SuspendLayout();
        Controls.Clear();
        BackColor = ThemeManager.Background;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = ThemeManager.Background
        };

        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        Controls.Add(root);
        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildContent(), 0, 1);

        RenderCurrentData();
        ResumeLayout(true);
    }

    private Control BuildHeader()
    {
        var header = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ThemeManager.Surface,
            Padding = new Padding(24, 14, 24, 14)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            BackColor = ThemeManager.Surface,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        Button backButton = CreateGhostButton("← Eszközök");
        backButton.AutoSize = true;
        backButton.Click += (_, _) => Close();

        layout.Controls.Add(backButton, 0, 0);

        var titleFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = ThemeManager.Surface,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        _lblTitle = new Label
        {
            Text = "Kliensgép",
            AutoSize = true,
            Font = new Font("Segoe UI", 22F, FontStyle.Bold),
            ForeColor = ThemeManager.TextPrimary,
            Margin = Padding.Empty
        };

        _lblStatus = new Label
        {
            Text = "● Ismeretlen",
            AutoSize = true,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = ThemeManager.TextSecondary,
            Margin = new Padding(18, 13, 0, 0)
        };

        titleFlow.Controls.Add(_lblTitle);
        titleFlow.Controls.Add(_lblStatus);

        layout.Controls.Add(titleFlow, 0, 1);

        _lblSubtitle = new Label
        {
            Text = "Eszközadatok betöltése...",
            Dock = DockStyle.Fill,
            ForeColor = ThemeManager.TextSecondary,
            Font = new Font("Segoe UI", 9.5F),
            TextAlign = ContentAlignment.TopLeft
        };

        layout.Controls.Add(_lblSubtitle, 0, 2);

        var actionFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = ThemeManager.Surface,
            Padding = new Padding(0, 24, 0, 0)
        };

        _btnRefreshData = CreatePrimaryButton("Adatok frissítése", 180);
        _btnRefreshData.Click += BtnRefreshData_Click;

        _lblActionMessage = new Label
        {
            Text = string.Empty,
            AutoSize = false,
            Width = 230,
            Height = 36,
            ForeColor = ThemeManager.TextSecondary,
            Font = new Font("Segoe UI", 8.5F),
            TextAlign = ContentAlignment.MiddleLeft
        };

        actionFlow.Controls.Add(_btnRefreshData);
        actionFlow.Controls.Add(_lblActionMessage);

        layout.Controls.Add(actionFlow, 1, 1);
        layout.SetRowSpan(actionFlow, 2);

        header.Controls.Add(layout);
        return header;
    }

    private Control BuildContent()
    {
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = ThemeManager.Background,
            Padding = new Padding(24, 24, 24, 24),
            Margin = Padding.Empty
        };

        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 250));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _infoHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ThemeManager.Background,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        content.Controls.Add(_infoHost, 0, 0);
        content.Controls.Add(BuildDiskSection(), 0, 1);

        return content;
    }

    private Control BuildDiskSection()
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

        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var title = new Label
        {
            Text = "Meghajtók",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = ThemeManager.TextPrimary,
            Font = new Font("Segoe UI", 11.5F, FontStyle.Bold)
        };

        _diskHost = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = ThemeManager.Surface,
            Padding = new Padding(0, 4, 4, 0)
        };

        _diskHost.SizeChanged += (_, _) => ResizeDiskRows();

        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(_diskHost, 0, 1);
        card.Controls.Add(layout);

        return card;
    }

    // =====================================================
    // DATA
    // =====================================================

    private async void DeviceDetailsForm_Shown(object? sender, EventArgs e)
    {
        await LoadDeviceAsync();
        _statusRefreshTimer.Start();
    }

    private async Task LoadDeviceAsync()
    {
        try
        {
            UseWaitCursor = true;

            DeviceSnapshotDto? device = await _apiClient.GetDeviceAsync(_agentId);
            DeviceOverviewDto? overview = await _apiClient.GetDeviceOverviewAsync(_agentId);

            if (device is null)
            {
                MessageBox.Show(
                    "A kliensgép adatai nem találhatók.",
                    "Nincs adat",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                Close();
                return;
            }

            _currentDevice = device;
            _currentOverview = overview;
            RenderCurrentData();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                "Nem sikerült betölteni a kliensgép adatait.\n\n" + exception.Message,
                "Hiba",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void RenderCurrentData()
    {
        if (_lblTitle is null || _infoHost is null || _diskHost is null)
        {
            return;
        }

        if (_currentDevice is null)
        {
            _lblTitle.Text = "Kliensgép";
            _lblSubtitle.Text = "Eszközadatok betöltése...";
            _lblStatus.Text = "● Ismeretlen";
            _lblStatus.ForeColor = ThemeManager.TextSecondary;
            return;
        }

        DeviceSnapshotDto device = _currentDevice;

        Text = $"Eszköz részletei - {device.Hostname}";
        _lblTitle.Text = device.Hostname;

        string os = device.OperatingSystem ?? "Ismeretlen operációs rendszer";
        string manufacturer = device.Manufacturer ?? "-";
        string model = device.Model ?? "-";

        _lblSubtitle.Text = $"{os}  •  {manufacturer} {model}";

        UpdateStatusLabels();
        BuildInformationCards();
        BuildDiskRows();
    }

    private void UpdateStatusLabels()
    {
        if (_currentOverview is null)
        {
            _lblStatus.Text = "● Ismeretlen";
            _lblStatus.ForeColor = ThemeManager.TextSecondary;
            return;
        }

        bool online = _currentOverview.IsOnline;
        _lblStatus.Text = online ? "● Online" : "● Offline";
        _lblStatus.ForeColor = online ? ThemeManager.Success : ThemeManager.Danger;
    }

    private void BuildInformationCards()
    {
        if (_currentDevice is null)
        {
            return;
        }

        DeviceSnapshotDto device = _currentDevice;
        bool? online = _currentOverview?.IsOnline;

        _infoHost.SuspendLayout();
        _infoHost.Controls.Clear();

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = ThemeManager.Background,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));

        layout.Controls.Add(
            CreateInfoCard(
                "Általános",
                [
                    ("Gépnév", device.Hostname, ThemeManager.TextPrimary),
                    ("Agent azonosító", ShortenAgentId(device.AgentId), ThemeManager.TextPrimary),
                    ("Operációs rendszer", device.OperatingSystem ?? "-", ThemeManager.TextPrimary),
                    ("OS verzió", device.OperatingSystemVersion ?? "-", ThemeManager.TextPrimary)
                ]),
            0,
            0);

        layout.Controls.Add(
            CreateInfoCard(
                "Hardver",
                [
                    ("Gyártó", device.Manufacturer ?? "-", ThemeManager.TextPrimary),
                    ("Modell", device.Model ?? "-", ThemeManager.TextPrimary),
                    ("Sorozatszám", device.SerialNumber ?? "-", ThemeManager.TextPrimary),
                    ("Memória", FormatBytes(device.TotalMemoryBytes), ThemeManager.TextPrimary)
                ]),
            1,
            0);

        string statusText = online.HasValue ? (online.Value ? "Online" : "Offline") : "Ismeretlen";
        Color statusColor = online.HasValue
            ? (online.Value ? ThemeManager.Success : ThemeManager.Danger)
            : ThemeManager.TextSecondary;

        layout.Controls.Add(
            CreateInfoCard(
                "Kapcsolat",
                [
                    ("Állapot", statusText, statusColor),
                    ("IP-cím", device.IpAddress ?? "-", ThemeManager.TextPrimary),
                    ("Utolsó adatgyűjtés", device.CapturedAtUtc.ToLocalTime().ToString("yyyy. MM. dd. HH:mm:ss"), ThemeManager.TextPrimary)
                ]),
            2,
            0);

        _infoHost.Controls.Add(layout);
        _infoHost.ResumeLayout(true);
    }

    private Control CreateInfoCard(
        string title,
        (string Caption, string Value, Color ValueColor)[] rows)
    {
        var card = CreateCardPanel(new Padding(18));
        card.Dock = DockStyle.Fill;
        card.Margin = new Padding(0, 0, 12, 12);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = rows.Length + 1,
            BackColor = ThemeManager.Surface,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        var titleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = ThemeManager.TextPrimary,
            Font = new Font("Segoe UI", 11.5F, FontStyle.Bold)
        };

        layout.Controls.Add(titleLabel, 0, 0);
        layout.SetColumnSpan(titleLabel, 2);

        for (int i = 0; i < rows.Length; i++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 39));

            var caption = new Label
            {
                Text = rows[i].Caption,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = ThemeManager.TextSecondary,
                Font = new Font("Segoe UI", 8.8F)
            };

            var value = new Label
            {
                Text = rows[i].Value,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = rows[i].ValueColor,
                Font = new Font("Segoe UI", 9.1F, FontStyle.Bold),
                AutoEllipsis = true
            };

            layout.Controls.Add(caption, 0, i + 1);
            layout.Controls.Add(value, 1, i + 1);
        }

        card.Controls.Add(layout);
        return card;
    }

    private void BuildDiskRows()
    {
        _diskHost.SuspendLayout();
        _diskHost.Controls.Clear();

        if (_currentDevice is null || _currentDevice.Disks.Count == 0)
        {
            _diskHost.Controls.Add(new Label
            {
                Text = "Nincs meghajtóadat.",
                Width = 900,
                Height = 50,
                ForeColor = ThemeManager.TextSecondary,
                BackColor = ThemeManager.Surface,
                TextAlign = ContentAlignment.MiddleLeft
            });

            _diskHost.ResumeLayout(true);
            return;
        }

        foreach (DiskInfoDto disk in _currentDevice.Disks.OrderBy(disk => disk.Name))
        {
            _diskHost.Controls.Add(CreateDiskRow(disk));
        }

        _diskHost.ResumeLayout(true);
        ResizeDiskRows();
    }

    private Control CreateDiskRow(DiskInfoDto disk)
    {
        long usedBytes = Math.Max(0, disk.TotalSizeBytes - disk.FreeSpaceBytes);
        double usagePercent = disk.TotalSizeBytes > 0
            ? usedBytes / (double)disk.TotalSizeBytes * 100d
            : 0d;

        var row = new Panel
        {
            Height = 76,
            BackColor = ThemeManager.SurfaceAlternative,
            Margin = new Padding(0, 0, 0, 8),
            Padding = new Padding(14, 10, 14, 10)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 1,
            BackColor = ThemeManager.SurfaceAlternative,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));

        layout.Controls.Add(CreateDiskText(disk.Name, true), 0, 0);
        layout.Controls.Add(CreateDiskText(string.IsNullOrWhiteSpace(disk.VolumeLabel) ? "-" : disk.VolumeLabel), 1, 0);
        layout.Controls.Add(CreateDiskMetric("Teljes", FormatBytes(disk.TotalSizeBytes)), 2, 0);
        layout.Controls.Add(CreateDiskMetric("Foglalt", FormatBytes(usedBytes)), 3, 0);
        layout.Controls.Add(CreateDiskMetric("Szabad", FormatBytes(disk.FreeSpaceBytes)), 4, 0);

        var usage = new UsageBar
        {
            Dock = DockStyle.Fill,
            Percent = usagePercent,
            BackColor = ThemeManager.SurfaceAlternative
        };

        layout.Controls.Add(usage, 5, 0);
        row.Controls.Add(layout);

        return row;
    }

    private static Label CreateDiskText(string text, bool bold = false)
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

    private static Control CreateDiskMetric(string caption, string value)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = ThemeManager.SurfaceAlternative,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };

        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label
        {
            Text = caption,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            ForeColor = ThemeManager.TextSecondary,
            Font = new Font("Segoe UI", 8F)
        }, 0, 0);

        layout.Controls.Add(new Label
        {
            Text = value,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            ForeColor = ThemeManager.TextPrimary,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        }, 0, 1);

        return layout;
    }

    private void ResizeDiskRows()
    {
        if (_diskHost is null)
        {
            return;
        }

        int width = Math.Max(760, _diskHost.ClientSize.Width - 26);

        foreach (Control control in _diskHost.Controls)
        {
            control.Width = width;
        }
    }

    // =====================================================
    // STATUS AUTO REFRESH
    // =====================================================

    private async void StatusRefreshTimer_Tick(object? sender, EventArgs e)
    {
        await RefreshStatusAsync();
    }

    private async Task RefreshStatusAsync()
    {
        if (_isRefreshingStatus)
        {
            return;
        }

        _isRefreshingStatus = true;

        try
        {
            _currentOverview = await _apiClient.GetDeviceOverviewAsync(_agentId);
            UpdateStatusLabels();
            BuildInformationCards();
        }
        catch
        {
            _lblStatus.Text = "● Kapcsolati hiba";
            _lblStatus.ForeColor = ThemeManager.Warning;
        }
        finally
        {
            _isRefreshingStatus = false;
        }
    }

    // =====================================================
    // REMOTE REFRESH
    // =====================================================

    private async void BtnRefreshData_Click(object? sender, EventArgs e)
    {
        try
        {
            _btnRefreshData.Enabled = false;
            _btnRefreshData.Text = "Kérés küldése...";
            _lblActionMessage.Text = string.Empty;

            await _apiClient.RequestSnapshotAsync(_agentId);

            _btnRefreshData.Text = "Frissítés folyamatban...";
            _lblActionMessage.Text = "Parancs elküldve • várakozás az Agentre...";
            _lblActionMessage.ForeColor = ThemeManager.Warning;

            await Task.Delay(TimeSpan.FromSeconds(25));
            await LoadDeviceAsync();

            _lblActionMessage.Text = "Adatok frissítve";
            _lblActionMessage.ForeColor = ThemeManager.Success;
        }
        catch (Exception exception)
        {
            _lblActionMessage.Text = "A frissítés sikertelen";
            _lblActionMessage.ForeColor = ThemeManager.Danger;

            MessageBox.Show(
                "Nem sikerült végrehajtani a távoli adatfrissítést.\n\n" + exception.Message,
                "Hiba",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            _btnRefreshData.Enabled = true;
            _btnRefreshData.Text = "Adatok frissítése";
        }
    }

    // =====================================================
    // THEME
    // =====================================================

    private void ThemeManager_ThemeChanged(object? sender, EventArgs e)
    {
        BuildInterface();
    }

    // =====================================================
    // HELPERS
    // =====================================================

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

    private static Button CreateGhostButton(string text)
    {
        var button = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = ThemeManager.Surface,
            ForeColor = ThemeManager.Primary,
            Font = new Font("Segoe UI", 9.3F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Height = 28,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private static string ShortenAgentId(string agentId)
    {
        if (agentId.Length <= 18)
        {
            return agentId;
        }

        return agentId[..8] + "…" + agentId[^6..];
    }

    private static string FormatBytes(ulong bytes)
    {
        const double gigabyte = 1024d * 1024d * 1024d;
        return $"{bytes / gigabyte:N1} GB";
    }

    private static string FormatBytes(long bytes)
    {
        bytes = Math.Max(0, bytes);
        const double gigabyte = 1024d * 1024d * 1024d;
        return $"{bytes / gigabyte:N1} GB";
    }

    private void DeviceDetailsForm_FormClosed(object? sender, FormClosedEventArgs e)
    {
        ThemeManager.ThemeChanged -= ThemeManager_ThemeChanged;
        _statusRefreshTimer.Stop();
        _statusRefreshTimer.Dispose();
    }

    private sealed class UsageBar : Control
    {
        private double _percent;

        public double Percent
        {
            get => _percent;
            set
            {
                _percent = Math.Clamp(value, 0d, 100d);
                Invalidate();
            }
        }

        public UsageBar()
        {
            DoubleBuffered = true;
            Height = 42;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Rectangle area = new(
                8,
                Math.Max(8, Height / 2 - 8),
                Math.Max(10, Width - 16),
                16);

            using var background = new SolidBrush(ThemeManager.Border);
            e.Graphics.FillRectangle(background, area);

            int fillWidth = (int)(area.Width * Percent / 100d);

            Color fillColor = Percent >= 90d
                ? ThemeManager.Danger
                : Percent >= 75d
                    ? ThemeManager.Warning
                    : ThemeManager.Success;

            if (fillWidth > 0)
            {
                using var fill = new SolidBrush(fillColor);
                e.Graphics.FillRectangle(fill, new Rectangle(area.X, area.Y, fillWidth, area.Height));
            }

            TextRenderer.DrawText(
                e.Graphics,
                $"{Percent:N0} %",
                Font,
                ClientRectangle,
                ThemeManager.TextPrimary,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.Top);
        }
    }
}

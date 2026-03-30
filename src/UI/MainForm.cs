using System.Drawing;
using System.Windows.Forms;
using PbiRestProxy.Logging;
using PbiRestProxy.Session;

namespace PbiRestProxy.UI;

public sealed class MainForm : Form
{
    private readonly AppSessionService sessionService;
    private readonly LogStore logStore;

    private Label tokenSourceValueLabel = null!;
    private Label tokenStateValueLabel = null!;
    private Label audienceValueLabel = null!;
    private Label userValueLabel = null!;
    private Label tenantValueLabel = null!;
    private Label expiresValueLabel = null!;
    private Label workspaceValueLabel = null!;
    private Label semanticModelValueLabel = null!;
    private Label xmlaEndpointValueLabel = null!;
    private Label daxTargetValueLabel = null!;
    private Label daxAvailabilityValueLabel = null!;
    private TextBox tokenInputTextBox = null!;
    private Button applyTokenButton = null!;
    private Button clearTokenButton = null!;
    private ListView logListView = null!;
    private ToolStripStatusLabel sessionStatusLabel = null!;

    public MainForm(AppSessionService sessionService, LogStore logStore)
    {
        this.sessionService = sessionService;
        this.logStore = logStore;

        InitializeComponent();
        WireEvents();
        LoadExistingLogEntries();
        RefreshSessionState();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            sessionService.StateChanged -= HandleSessionStateChanged;
            logStore.EntryAdded -= HandleLogEntryAdded;
            logStore.Cleared -= HandleLogCleared;
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        Text = "pbi-rest-proxy";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1080, 720);
        Size = new Size(1280, 840);

        var tabControl = new TabControl
        {
            Dock = DockStyle.Fill
        };

        tabControl.TabPages.Add(BuildConnectionTab());
        tabControl.TabPages.Add(BuildDaxTab());
        tabControl.TabPages.Add(BuildLogTab());

        var statusStrip = new StatusStrip();
        sessionStatusLabel = new ToolStripStatusLabel
        {
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft
        };

        statusStrip.Items.Add(sessionStatusLabel);
        statusStrip.Items.Add(new ToolStripStatusLabel("Current milestone: UI + session + manual token paste"));

        Controls.Add(tabControl);
        Controls.Add(statusStrip);

        ResumeLayout();
    }

    private TabPage BuildConnectionTab()
    {
        var page = new TabPage("Connection");

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 3
        };

        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(BuildStatusGroupBox(), 0, 0);
        root.Controls.Add(BuildTokenInputGroupBox(), 0, 1);
        root.Controls.Add(BuildTargetGroupBox(), 0, 2);

        page.Controls.Add(root);
        return page;
    }

    private Control BuildStatusGroupBox()
    {
        var groupBox = new GroupBox
        {
            Text = "Session Status",
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };

        var table = CreateTwoColumnTable();

        AddStatusRow(table, "Token source:", out tokenSourceValueLabel);
        AddStatusRow(table, "Token state:", out tokenStateValueLabel);
        AddStatusRow(table, "Audience:", out audienceValueLabel);
        AddStatusRow(table, "Signed in as:", out userValueLabel);
        AddStatusRow(table, "Tenant ID:", out tenantValueLabel);
        AddStatusRow(table, "Expires:", out expiresValueLabel);

        groupBox.Controls.Add(table);
        return groupBox;
    }

    private Control BuildTokenInputGroupBox()
    {
        var groupBox = new GroupBox
        {
            Text = "Manual Access Token",
            Dock = DockStyle.Fill
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 3
        };

        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var instructionLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            Text = "Paste a Power BI / Fabric access token acquired externally, for example via Azure CLI. The token is kept only in memory and is never written to the log."
        };

        tokenInputTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            AcceptsReturn = true,
            AcceptsTab = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            PlaceholderText = "Paste an access token here..."
        };

        applyTokenButton = new Button
        {
            AutoSize = true,
            Text = "Apply Token"
        };
        applyTokenButton.Click += (_, _) => ApplyManualToken();

        clearTokenButton = new Button
        {
            AutoSize = true,
            Text = "Clear Token"
        };
        clearTokenButton.Click += (_, _) => ClearManualToken();

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        buttonPanel.Controls.Add(applyTokenButton);
        buttonPanel.Controls.Add(clearTokenButton);

        layout.Controls.Add(instructionLabel, 0, 0);
        layout.Controls.Add(tokenInputTextBox, 0, 1);
        layout.Controls.Add(buttonPanel, 0, 2);

        groupBox.Controls.Add(layout);
        return groupBox;
    }

    private Control BuildTargetGroupBox()
    {
        var groupBox = new GroupBox
        {
            Text = "Current Target",
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };

        var table = CreateTwoColumnTable();

        AddStatusRow(table, "Workspace:", out workspaceValueLabel);
        AddStatusRow(table, "Semantic model:", out semanticModelValueLabel);
        AddStatusRow(table, "XMLA endpoint:", out xmlaEndpointValueLabel);

        groupBox.Controls.Add(table);
        return groupBox;
    }

    private TabPage BuildDaxTab()
    {
        var page = new TabPage("DAX");

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 4
        };

        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 45f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 55f));

        var infoLabel = new Label
        {
            AutoSize = true,
            Text = "The DAX tab is wired into the app shell now, but query execution is intentionally deferred until model discovery and XMLA connection are in place."
        };

        var queryTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            AcceptsReturn = true,
            AcceptsTab = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Text = "EVALUATE ROW(\"Message\", \"DAX execution is not implemented yet\")"
        };

        var summaryPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        var executeButton = new Button
        {
            AutoSize = true,
            Enabled = false,
            Text = "Execute DAX"
        };

        daxTargetValueLabel = new Label
        {
            AutoSize = true,
            Margin = new Padding(12, 8, 0, 0)
        };

        daxAvailabilityValueLabel = new Label
        {
            AutoSize = true,
            Margin = new Padding(12, 8, 0, 0)
        };

        summaryPanel.Controls.Add(executeButton);
        summaryPanel.Controls.Add(daxTargetValueLabel);
        summaryPanel.Controls.Add(daxAvailabilityValueLabel);

        var resultGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false
        };

        page.Controls.Add(root);
        root.Controls.Add(infoLabel, 0, 0);
        root.Controls.Add(queryTextBox, 0, 1);
        root.Controls.Add(summaryPanel, 0, 2);
        root.Controls.Add(resultGrid, 0, 3);

        return page;
    }

    private TabPage BuildLogTab()
    {
        var page = new TabPage("Log");

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 2
        };

        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        var clearLogButton = new Button
        {
            AutoSize = true,
            Text = "Clear Log"
        };
        clearLogButton.Click += (_, _) => logStore.Clear();

        toolbar.Controls.Add(clearLogButton);

        logListView = new ListView
        {
            Dock = DockStyle.Fill,
            FullRowSelect = true,
            GridLines = true,
            HideSelection = false,
            View = View.Details
        };

        logListView.Columns.Add("Time", 210);
        logListView.Columns.Add("Level", 90);
        logListView.Columns.Add("Source", 120);
        logListView.Columns.Add("Message", 800);

        root.Controls.Add(toolbar, 0, 0);
        root.Controls.Add(logListView, 0, 1);

        page.Controls.Add(root);
        return page;
    }

    private void WireEvents()
    {
        sessionService.StateChanged += HandleSessionStateChanged;
        logStore.EntryAdded += HandleLogEntryAdded;
        logStore.Cleared += HandleLogCleared;
        tokenInputTextBox.TextChanged += (_, _) => UpdateTokenInputButtons();
    }

    private void ApplyManualToken()
    {
        try
        {
            sessionService.SetAccessToken(tokenInputTextBox.Text, AccessTokenSource.Manual);
            tokenInputTextBox.Clear();
        }
        catch (InvalidAccessTokenException ex)
        {
            logStore.WriteError("Auth", ex.Message);
            MessageBox.Show(this, ex.Message, "Invalid access token", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ClearManualToken()
    {
        tokenInputTextBox.Clear();
        sessionService.ClearAccessToken();
    }

    private void LoadExistingLogEntries()
    {
        foreach (var entry in logStore.Snapshot())
        {
            AddLogEntryToListView(entry);
        }
    }

    private void RefreshSessionState()
    {
        var state = sessionService.State;

        tokenSourceValueLabel.Text = state.TokenSource switch
        {
            AccessTokenSource.Manual => "Manual",
            _ => "Not loaded"
        };

        tokenStateValueLabel.Text = state.AccessToken switch
        {
            null => "No token loaded",
            { IsExpired: true } => "Loaded (expired)",
            _ => "Loaded"
        };

        audienceValueLabel.Text = state.AccessToken?.Audience ?? "n/a";
        userValueLabel.Text = state.AccessToken?.DisplayUser ?? "n/a";
        tenantValueLabel.Text = state.AccessToken?.TenantId ?? "n/a";
        expiresValueLabel.Text = state.AccessToken?.ExpiresAtLocal.ToString("yyyy-MM-dd HH:mm:ss zzz") ?? "n/a";

        workspaceValueLabel.Text = state.SelectedWorkspaceName ?? "Not selected yet";
        semanticModelValueLabel.Text = state.SelectedSemanticModelName ?? "Not selected yet";
        xmlaEndpointValueLabel.Text = state.XmlaEndpoint ?? "Not connected yet";

        daxTargetValueLabel.Text = state.SelectedSemanticModelName is null
            ? "Target: none"
            : $"Target: {state.SelectedSemanticModelName}";

        daxAvailabilityValueLabel.Text = state.HasUsableAccessToken
            ? "DAX execution will be enabled after XMLA connectivity is added"
            : "Load a valid token first";

        sessionStatusLabel.Text = state.AccessToken switch
        {
            null => "No access token loaded",
            { IsExpired: true } token => $"Loaded expired token for {token.DisplayUser}",
            var token => $"Loaded token for {token!.DisplayUser}"
        };

        UpdateTokenInputButtons();
    }

    private void UpdateTokenInputButtons()
    {
        applyTokenButton.Enabled = !string.IsNullOrWhiteSpace(tokenInputTextBox.Text);
        clearTokenButton.Enabled = sessionService.State.HasAccessToken || !string.IsNullOrWhiteSpace(tokenInputTextBox.Text);
    }

    private void HandleSessionStateChanged()
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(new Action(RefreshSessionState));
            return;
        }

        RefreshSessionState();
    }

    private void HandleLogEntryAdded(LogEntry entry)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(new Action<LogEntry>(AddLogEntryToListView), entry);
            return;
        }

        AddLogEntryToListView(entry);
    }

    private void HandleLogCleared()
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(new Action(ClearLogListView));
            return;
        }

        ClearLogListView();
    }

    private void AddLogEntryToListView(LogEntry entry)
    {
        var item = new ListViewItem(entry.LocalTimestampText);
        item.SubItems.Add(entry.Level.ToString());
        item.SubItems.Add(entry.Source);
        item.SubItems.Add(entry.Message);

        logListView.Items.Add(item);
        item.EnsureVisible();
    }

    private void ClearLogListView()
    {
        logListView.Items.Clear();
    }

    private static TableLayoutPanel CreateTwoColumnTable()
    {
        return new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Padding = new Padding(12),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 0
        };
    }

    private static void AddStatusRow(TableLayoutPanel table, string caption, out Label valueLabel)
    {
        var rowIndex = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var captionFont = SystemFonts.MessageBoxFont ?? Control.DefaultFont;

        var captionLabel = new Label
        {
            AutoSize = true,
            Font = new Font(captionFont, FontStyle.Bold),
            Margin = new Padding(0, 0, 12, 8),
            Text = caption
        };

        valueLabel = new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8),
            Text = "n/a"
        };

        table.Controls.Add(captionLabel, 0, rowIndex);
        table.Controls.Add(valueLabel, 1, rowIndex);
    }
}

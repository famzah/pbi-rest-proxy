using System.Drawing;
using System.Windows.Forms;
using PbiRestProxy.Auth;
using PbiRestProxy.Discovery;
using PbiRestProxy.Logging;
using PbiRestProxy.Session;

namespace PbiRestProxy.UI;

public sealed class MainForm : Form
{
    private readonly AppSessionService sessionService;
    private readonly LogStore logStore;
    private readonly AzureCliAccessTokenProvider azureCliAccessTokenProvider;
    private readonly PowerBiDiscoveryService discoveryService;

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
    private Button azureCliTokenButton = null!;
    private Button applyTokenButton = null!;
    private Button clearTokenButton = null!;
    private Button loadWorkspacesButton = null!;
    private Button loadSemanticModelsButton = null!;
    private ListView workspaceListView = null!;
    private ListView semanticModelListView = null!;
    private ListView logListView = null!;
    private ToolStripStatusLabel sessionStatusLabel = null!;
    private string? sessionStatusOverrideText;
    private bool isAcquiringAzureCliToken;
    private bool isLoadingWorkspaces;
    private bool isLoadingSemanticModels;

    public MainForm(AppSessionService sessionService, LogStore logStore, PowerBiDiscoveryService discoveryService)
    {
        this.sessionService = sessionService;
        this.logStore = logStore;
        this.discoveryService = discoveryService;
        azureCliAccessTokenProvider = new AzureCliAccessTokenProvider(logStore);

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
        statusStrip.Items.Add(new ToolStripStatusLabel("Current milestone: UI + session + token loading + Power BI discovery"));

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
            RowCount = 4
        };

        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 40f));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 60f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(BuildStatusGroupBox(), 0, 0);
        root.Controls.Add(BuildTokenInputGroupBox(), 0, 1);
        root.Controls.Add(BuildDiscoveryGroupBox(), 0, 2);
        root.Controls.Add(BuildTargetGroupBox(), 0, 3);

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
            Text = "Access Token",
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
            Text = "You can fetch a Power BI / Fabric access token through Azure CLI or paste one manually. The token is kept only in memory and is never written to the log."
        };

        azureCliTokenButton = new Button
        {
            AutoSize = true,
            Text = "Login + Get Token via Azure CLI"
        };
        azureCliTokenButton.Click += async (_, _) => await AcquireTokenFromAzureCliAsync();

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

        buttonPanel.Controls.Add(azureCliTokenButton);
        buttonPanel.Controls.Add(applyTokenButton);
        buttonPanel.Controls.Add(clearTokenButton);

        layout.Controls.Add(instructionLabel, 0, 0);
        layout.Controls.Add(tokenInputTextBox, 0, 1);
        layout.Controls.Add(buttonPanel, 0, 2);

        groupBox.Controls.Add(layout);
        return groupBox;
    }

    private Control BuildDiscoveryGroupBox()
    {
        var groupBox = new GroupBox
        {
            Text = "Discovery",
            Dock = DockStyle.Fill
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 2
        };

        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        loadWorkspacesButton = new Button
        {
            AutoSize = true,
            Text = "Load Workspaces"
        };
        loadWorkspacesButton.Click += async (_, _) => await LoadWorkspacesAsync();

        loadSemanticModelsButton = new Button
        {
            AutoSize = true,
            Text = "Load Models"
        };
        loadSemanticModelsButton.Click += async (_, _) => await LoadSemanticModelsAsync();

        toolbar.Controls.Add(loadWorkspacesButton);
        toolbar.Controls.Add(loadSemanticModelsButton);

        workspaceListView = CreateWorkspaceListView();
        semanticModelListView = CreateSemanticModelListView();

        var listsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };

        listsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42f));
        listsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58f));
        listsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        listsLayout.Controls.Add(BuildListPane("Workspaces", workspaceListView), 0, 0);
        listsLayout.Controls.Add(BuildListPane("Semantic Models", semanticModelListView), 1, 0);

        layout.Controls.Add(toolbar, 0, 0);
        layout.Controls.Add(listsLayout, 0, 1);

        groupBox.Controls.Add(layout);
        return groupBox;
    }

    private Control BuildTargetGroupBox()
    {
        var groupBox = new GroupBox
        {
            Text = "Current Selection",
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

    private static Control BuildListPane(string title, ListView listView)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0)
        };

        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var label = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 8),
            Text = title
        };

        panel.Controls.Add(label, 0, 0);
        panel.Controls.Add(listView, 0, 1);

        return panel;
    }

    private static ListView CreateWorkspaceListView()
    {
        var listView = new ListView
        {
            Dock = DockStyle.Fill,
            FullRowSelect = true,
            GridLines = true,
            HideSelection = false,
            MultiSelect = false,
            View = View.Details
        };

        listView.Columns.Add("Workspace", 280);
        listView.Columns.Add("Capacity", 120);
        return listView;
    }

    private static ListView CreateSemanticModelListView()
    {
        var listView = new ListView
        {
            Dock = DockStyle.Fill,
            FullRowSelect = true,
            GridLines = true,
            HideSelection = false,
            MultiSelect = false,
            View = View.Details
        };

        listView.Columns.Add("Semantic Model", 300);
        listView.Columns.Add("Owner", 260);
        listView.Columns.Add("Refreshable", 110);
        return listView;
    }

    private void WireEvents()
    {
        sessionService.StateChanged += HandleSessionStateChanged;
        logStore.EntryAdded += HandleLogEntryAdded;
        logStore.Cleared += HandleLogCleared;
        tokenInputTextBox.TextChanged += (_, _) => UpdateActionButtons();
        workspaceListView.SelectedIndexChanged += (_, _) => HandleWorkspaceSelectionChanged();
        semanticModelListView.SelectedIndexChanged += (_, _) => HandleSemanticModelSelectionChanged();
    }

    private void ApplyManualToken()
    {
        try
        {
            sessionService.SetAccessToken(tokenInputTextBox.Text, AccessTokenSource.Manual);
            ResetDiscoveryLists();
            tokenInputTextBox.Clear();
        }
        catch (InvalidAccessTokenException ex)
        {
            logStore.WriteError("Auth", ex.Message);
            MessageBox.Show(this, ex.Message, "Invalid access token", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task AcquireTokenFromAzureCliAsync()
    {
        if (isAcquiringAzureCliToken)
        {
            return;
        }

        try
        {
            isAcquiringAzureCliToken = true;
            sessionStatusOverrideText = "Running Azure CLI token acquisition...";
            UseWaitCursor = true;
            RefreshSessionState();

            var accessToken = await azureCliAccessTokenProvider.AcquireAccessTokenAsync();
            sessionService.SetAccessToken(accessToken, AccessTokenSource.AzureCli);
            ResetDiscoveryLists();
            tokenInputTextBox.Clear();
        }
        catch (Exception ex)
        {
            logStore.WriteError("Auth", ex.Message);
            MessageBox.Show(this, ex.Message, "Azure CLI token acquisition failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            isAcquiringAzureCliToken = false;
            sessionStatusOverrideText = null;
            UseWaitCursor = false;
            RefreshSessionState();
        }
    }

    private void ClearManualToken()
    {
        tokenInputTextBox.Clear();
        sessionService.ClearAccessToken();
        ResetDiscoveryLists();
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
            AccessTokenSource.AzureCli => "Azure CLI",
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

        sessionStatusLabel.Text = sessionStatusOverrideText ?? state.AccessToken switch
        {
            null => "No access token loaded",
            { IsExpired: true } token => $"Loaded expired token for {token.DisplayUser}",
            var token => $"Loaded token for {token!.DisplayUser}"
        };

        UpdateActionButtons();
    }

    private void UpdateActionButtons()
    {
        var isBusy = isAcquiringAzureCliToken || isLoadingWorkspaces || isLoadingSemanticModels;

        azureCliTokenButton.Enabled = !isBusy;
        applyTokenButton.Enabled = !isBusy && !string.IsNullOrWhiteSpace(tokenInputTextBox.Text);
        clearTokenButton.Enabled = !isBusy && (sessionService.State.HasAccessToken || !string.IsNullOrWhiteSpace(tokenInputTextBox.Text));
        loadWorkspacesButton.Enabled = !isBusy && sessionService.State.HasUsableAccessToken;
        loadSemanticModelsButton.Enabled =
            !isBusy &&
            sessionService.State.HasUsableAccessToken &&
            sessionService.State.SelectedWorkspace is not null;
    }

    private async Task LoadWorkspacesAsync()
    {
        if (isLoadingWorkspaces)
        {
            return;
        }

        if (!sessionService.State.HasUsableAccessToken || sessionService.CurrentAccessToken is null)
        {
            const string message = "Load a non-expired access token before loading workspaces.";
            logStore.WriteWarning("Discovery", message);
            MessageBox.Show(this, message, "No access token", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            isLoadingWorkspaces = true;
            sessionStatusOverrideText = "Loading workspaces...";
            UseWaitCursor = true;
            RefreshSessionState();

            var workspaces = await discoveryService.LoadWorkspacesAsync(sessionService.CurrentAccessToken);
            PopulateWorkspaces(workspaces);

            if (workspaces.Count == 0)
            {
                sessionService.ClearSelection();
                logStore.WriteWarning("Discovery", "No accessible workspaces were returned for the current user.");
            }
        }
        catch (Exception ex)
        {
            logStore.WriteError("Discovery", ex.Message);
            MessageBox.Show(this, ex.Message, "Workspace discovery failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            isLoadingWorkspaces = false;
            sessionStatusOverrideText = null;
            UseWaitCursor = false;
            RefreshSessionState();
        }
    }

    private async Task LoadSemanticModelsAsync()
    {
        if (isLoadingSemanticModels)
        {
            return;
        }

        if (!sessionService.State.HasUsableAccessToken || sessionService.CurrentAccessToken is null)
        {
            const string message = "Load a non-expired access token before loading semantic models.";
            logStore.WriteWarning("Discovery", message);
            MessageBox.Show(this, message, "No access token", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var workspace = sessionService.State.SelectedWorkspace;

        if (workspace is null)
        {
            const string message = "Select a workspace first.";
            logStore.WriteWarning("Discovery", message);
            MessageBox.Show(this, message, "No workspace selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            isLoadingSemanticModels = true;
            sessionStatusOverrideText = $"Loading semantic models for {workspace.Name}...";
            UseWaitCursor = true;
            RefreshSessionState();

            var semanticModels = await discoveryService.LoadSemanticModelsAsync(sessionService.CurrentAccessToken, workspace);
            PopulateSemanticModels(semanticModels);

            if (semanticModels.Count == 0)
            {
                sessionService.SetSelectedSemanticModel(null);
                logStore.WriteWarning("Discovery", $"No semantic models were returned for workspace '{workspace.Name}'.");
            }
        }
        catch (Exception ex)
        {
            logStore.WriteError("Discovery", ex.Message);
            MessageBox.Show(this, ex.Message, "Semantic model discovery failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            isLoadingSemanticModels = false;
            sessionStatusOverrideText = null;
            UseWaitCursor = false;
            RefreshSessionState();
        }
    }

    private void HandleWorkspaceSelectionChanged()
    {
        var selectedWorkspace = GetSelectedWorkspace();
        var previousWorkspaceId = sessionService.State.SelectedWorkspace?.Id;

        sessionService.SetSelectedWorkspace(selectedWorkspace);

        if (selectedWorkspace is null || !string.Equals(previousWorkspaceId, selectedWorkspace.Id, StringComparison.Ordinal))
        {
            PopulateSemanticModels([]);
        }
    }

    private void HandleSemanticModelSelectionChanged()
    {
        var selectedSemanticModel = GetSelectedSemanticModel();
        sessionService.SetSelectedSemanticModel(selectedSemanticModel);
    }

    private WorkspaceSummary? GetSelectedWorkspace()
    {
        return workspaceListView.SelectedItems.Count == 0
            ? null
            : workspaceListView.SelectedItems[0].Tag as WorkspaceSummary;
    }

    private SemanticModelSummary? GetSelectedSemanticModel()
    {
        return semanticModelListView.SelectedItems.Count == 0
            ? null
            : semanticModelListView.SelectedItems[0].Tag as SemanticModelSummary;
    }

    private void PopulateWorkspaces(IReadOnlyList<WorkspaceSummary> workspaces)
    {
        var selectedWorkspaceId = sessionService.State.SelectedWorkspace?.Id;

        workspaceListView.BeginUpdate();

        try
        {
            workspaceListView.Items.Clear();

            foreach (var workspace in workspaces)
            {
                var item = new ListViewItem(workspace.Name)
                {
                    Tag = workspace
                };

                item.SubItems.Add(workspace.CapacityDisplay);
                workspaceListView.Items.Add(item);
            }
        }
        finally
        {
            workspaceListView.EndUpdate();
        }

        PopulateSemanticModels([]);
        RestoreListSelection(workspaceListView, selectedWorkspaceId);
    }

    private void PopulateSemanticModels(IReadOnlyList<SemanticModelSummary> semanticModels)
    {
        var selectedSemanticModelId = sessionService.State.SelectedSemanticModel?.Id;

        semanticModelListView.BeginUpdate();

        try
        {
            semanticModelListView.Items.Clear();

            foreach (var semanticModel in semanticModels)
            {
                var item = new ListViewItem(semanticModel.Name)
                {
                    Tag = semanticModel
                };

                item.SubItems.Add(semanticModel.OwnerDisplay);
                item.SubItems.Add(semanticModel.RefreshableDisplay);
                semanticModelListView.Items.Add(item);
            }
        }
        finally
        {
            semanticModelListView.EndUpdate();
        }

        RestoreListSelection(semanticModelListView, selectedSemanticModelId);
    }

    private void ResetDiscoveryLists()
    {
        PopulateWorkspaces([]);
        sessionService.ClearSelection();
    }

    private static void RestoreListSelection(ListView listView, string? selectedItemId)
    {
        if (string.IsNullOrWhiteSpace(selectedItemId))
        {
            return;
        }

        foreach (ListViewItem item in listView.Items)
        {
            switch (item.Tag)
            {
                case WorkspaceSummary workspace when string.Equals(workspace.Id, selectedItemId, StringComparison.Ordinal):
                    item.Selected = true;
                    item.Focused = true;
                    item.EnsureVisible();
                    return;
                case SemanticModelSummary semanticModel when string.Equals(semanticModel.Id, selectedItemId, StringComparison.Ordinal):
                    item.Selected = true;
                    item.Focused = true;
                    item.EnsureVisible();
                    return;
            }
        }
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

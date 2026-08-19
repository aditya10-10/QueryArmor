using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using QueryArmor.Application.Configuration;
using QueryArmor.Application.Fixes;
using QueryArmor.Domain.Analysis;

namespace QueryArmor.Presentation.Dialogs
{
    /// <summary>
    /// Modal dialog shown when QueryArmor detects a risky query.
    /// </summary>
    public partial class BlockingWarningDialog : Form
    {
        private readonly AnalysisResult _analysis;
        private readonly string _originalSql;
        private readonly IQueryAutoFixer _fixer;
        private readonly bool _allowOverride;
        private readonly bool _requireDoubleConfirm;
        private readonly ToolTip _toolTip = new ToolTip();

        public UserChoice UserChoice { get; private set; } = UserChoice.Cancel;
        public string? FixedSql { get; private set; }

        public BlockingWarningDialog(AnalysisResult analysis, string sql, IQueryAutoFixer fixer, GuardConfiguration config)
        {
            _analysis = analysis;
            _originalSql = sql;
            _fixer = fixer;
            _allowOverride = config.AllowOverride;
            _requireDoubleConfirm = config.RequireDoubleConfirmForCritical;

            InitializeComponent();
            PopulateDetails();
        }

        private void PopulateDetails()
        {
            bool isCritical = _analysis.RiskLevel == RiskLevel.Critical;

            lblTitle.Text = isCritical
                ? "Critical Risk: Execution Blocked"
                : "Warning: Potentially Unsafe Query";

            lblTitle.ForeColor = isCritical
                ? Color.FromArgb(255, 123, 114)
                : Color.FromArgb(227, 179, 65);

            lblSummary.Text =
                $"QueryArmor detected {_analysis.Violations.Count} issue(s) in your query " +
                $"against '{_analysis.AffectedTable}'.";

            txtViolations.Text = string.Join(
                Environment.NewLine,
                _analysis.Violations.Select(v => $"{v.Severity}: {v.Message}"));

            txtSqlPreview.Text = _originalSql;

            lblImpactTable.Text = string.IsNullOrWhiteSpace(_analysis.AffectedTable)
                ? "Unknown"
                : _analysis.AffectedTable;
            lblImpactOp.Text = _analysis.StatementType.ToString().ToUpperInvariant();
            lblImpactRows.Text = _analysis.IsBlocked ? "ALL ROWS (unbounded)" : "Filtered rows";
            lblImpactRows.ForeColor = _analysis.IsBlocked
                ? Color.FromArgb(255, 123, 114)
                : Color.FromArgb(63, 185, 80);

            var fix = _fixer.TryFix(_originalSql, _analysis);
            btnApplyFix.Enabled = fix != null;
            if (fix != null)
            {
                FixedSql = fix.FixedSql;
                btnApplyFix.Text = "Auto-Fix";
                _toolTip.SetToolTip(btnApplyFix, fix.Description);
            }
            else
            {
                btnApplyFix.Text = "Auto-Fix";
                _toolTip.SetToolTip(btnApplyFix, "No automatic fix is available for this query.");
            }

            btnExecuteAnyway.Text = _allowOverride
                ? "Execute Anyway"
                : "Override Disabled";
            btnExecuteAnyway.Enabled = _allowOverride;
            _toolTip.SetToolTip(btnExecuteAnyway, _allowOverride
                ? "Execute this query and write an audit event."
                : "Team policy does not allow overrides.");
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            UserChoice = UserChoice.Cancel;
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void btnApplyFix_Click(object sender, EventArgs e)
        {
            UserChoice = UserChoice.ApplyFix;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnExecuteAnyway_Click(object sender, EventArgs e)
        {
            if (_analysis.RiskLevel == RiskLevel.Critical && _requireDoubleConfirm)
            {
                var confirm = MessageBox.Show(
                    "Are you absolutely sure?\n\n" +
                    $"Executing this {_analysis.StatementType.ToString().ToUpperInvariant()} " +
                    $"without a WHERE clause on '{_analysis.AffectedTable}' may cause " +
                    "irreversible data loss.\n\n" +
                    "This action will be logged to the audit trail.",
                    "Confirm Override",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);

                if (confirm != DialogResult.Yes)
                    return;
            }

            UserChoice = UserChoice.ForceExecute;
            DialogResult = DialogResult.Yes;
            Close();
        }

        public UserChoice ShowModal()
        {
            ShowDialog();
            return UserChoice;
        }

        private void InitializeComponent()
        {
            Text = "QueryArmor - Safety Check";
            ClientSize = new Size(700, 560);
            MinimumSize = new Size(660, 600);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(22, 27, 34);
            ForeColor = Color.FromArgb(230, 237, 243);
            Font = new Font("Segoe UI", 9.5f);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                BackColor = BackColor,
                ColumnCount = 1,
                RowCount = 8
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 28));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));

            lblTitle = CreateLabel(bold: true, size: 13.5f);
            lblSummary = CreateLabel();
            lblSummary.MaximumSize = new Size(0, 0);

            txtViolations = CreateReadOnlyTextBox(
                Color.FromArgb(13, 17, 23),
                Color.FromArgb(255, 123, 114));
            txtViolations.WordWrap = true;
            txtViolations.ScrollBars = RichTextBoxScrollBars.Vertical;

            var lblPreviewHeader = CreateSectionHeader("Query Preview:");

            txtSqlPreview = CreateReadOnlyTextBox(
                Color.FromArgb(13, 17, 23),
                Color.FromArgb(201, 209, 217));
            txtSqlPreview.Font = new Font("Cascadia Mono", 9f);
            txtSqlPreview.WordWrap = false;
            txtSqlPreview.ScrollBars = RichTextBoxScrollBars.Both;

            var lblImpact = CreateSectionHeader("Impact Assessment:");
            var impactGrid = CreateImpactGrid();
            var actions = CreateActions();

            root.Controls.Add(lblTitle, 0, 0);
            root.Controls.Add(lblSummary, 0, 1);
            root.Controls.Add(txtViolations, 0, 2);
            root.Controls.Add(lblPreviewHeader, 0, 3);
            root.Controls.Add(txtSqlPreview, 0, 4);
            root.Controls.Add(lblImpact, 0, 5);
            root.Controls.Add(impactGrid, 0, 6);
            root.Controls.Add(actions, 0, 7);

            Controls.Add(root);
        }

        private TableLayoutPanel CreateImpactGrid()
        {
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10, 6, 10, 6),
                ColumnCount = 3,
                RowCount = 2,
                BackColor = Color.FromArgb(13, 17, 23),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            grid.Controls.Add(CreateGridLabel("Table", muted: true), 0, 0);
            grid.Controls.Add(CreateGridLabel("Operation", muted: true), 1, 0);
            grid.Controls.Add(CreateGridLabel("Rows", muted: true), 2, 0);

            lblImpactTable = CreateGridLabel("", bold: true);
            lblImpactOp = CreateGridLabel("", bold: true);
            lblImpactRows = CreateGridLabel("", bold: true);

            grid.Controls.Add(lblImpactTable, 0, 1);
            grid.Controls.Add(lblImpactOp, 1, 1);
            grid.Controls.Add(lblImpactRows, 2, 1);

            return grid;
        }

        private TableLayoutPanel CreateActions()
        {
            var actions = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Padding = new Padding(0, 8, 0, 8),
                BackColor = BackColor
            };
            actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));

            btnCancel = CreateButton("Cancel", Color.FromArgb(33, 38, 45), Color.FromArgb(230, 237, 243));
            btnApplyFix = CreateButton("Auto-Fix", Color.FromArgb(31, 111, 235), Color.White);
            btnExecuteAnyway = CreateButton("Execute Anyway", Color.FromArgb(60, 20, 20), Color.FromArgb(255, 123, 114));

            btnCancel.Click += btnCancel_Click;
            btnApplyFix.Click += btnApplyFix_Click;
            btnExecuteAnyway.Click += btnExecuteAnyway_Click;

            actions.Controls.Add(btnCancel, 0, 0);
            actions.Controls.Add(btnApplyFix, 1, 0);
            actions.Controls.Add(btnExecuteAnyway, 2, 0);

            return actions;
        }

        private static Label CreateSectionHeader(string text)
        {
            var label = CreateLabel(bold: true);
            label.Text = text;
            return label;
        }

        private static Label CreateLabel(bool bold = false, bool muted = false, float size = 9.5f)
            => new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                ForeColor = muted ? Color.FromArgb(110, 118, 129) : Color.FromArgb(230, 237, 243),
                Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = false,
                UseMnemonic = false
            };

        private static Label CreateGridLabel(string text, bool bold = false, bool muted = false)
        {
            var label = CreateLabel(bold, muted);
            label.Text = text;
            label.Margin = new Padding(0, 0, 8, 0);
            return label;
        }

        private static RichTextBox CreateReadOnlyTextBox(Color bg, Color fg)
            => new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = bg,
                ForeColor = fg,
                BorderStyle = BorderStyle.FixedSingle,
                ReadOnly = true,
                DetectUrls = false,
                Margin = new Padding(0, 0, 0, 8)
            };

        private static Button CreateButton(string text, Color bg, Color fg)
            => new Button
            {
                Text = text,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 10, 0),
                MinimumSize = new Size(0, 38),
                BackColor = bg,
                ForeColor = fg,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                Cursor = Cursors.Hand,
                AutoEllipsis = true,
                UseMnemonic = false
            };

        private Label lblTitle = null!;
        private Label lblSummary = null!;
        private Label lblImpactTable = null!;
        private Label lblImpactOp = null!;
        private Label lblImpactRows = null!;
        private RichTextBox txtViolations = null!;
        private RichTextBox txtSqlPreview = null!;
        private Button btnCancel = null!;
        private Button btnApplyFix = null!;
        private Button btnExecuteAnyway = null!;
    }

    public enum UserChoice
    {
        Cancel,
        ApplyFix,
        ForceExecute
    }
}

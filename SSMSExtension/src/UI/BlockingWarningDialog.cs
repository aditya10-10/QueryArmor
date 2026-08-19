using System;
using System.Drawing;
using System.Windows.Forms;
using QueryGuard.Core;

namespace QueryGuard.UI
{
    /// <summary>
    /// Modal dialog shown when QueryGuard detects a critical violation.
    /// Presents the issue clearly and gives the user three choices:
    ///   1. Cancel execution (safest)
    ///   2. Apply auto-fix (guided recovery)
    ///   3. Execute anyway (override with audit log entry)
    /// </summary>
    public partial class BlockingWarningDialog : Form
    {
        private readonly AnalysisResult _analysis;
        private readonly string _originalSql;
        private readonly QueryAutoFixer _fixer;
        private readonly bool _allowOverride;
        private readonly bool _requireDoubleConfirm;

        public UserChoice UserChoice { get; private set; } = UserChoice.Cancel;
        public string? FixedSql { get; private set; }

        public BlockingWarningDialog(AnalysisResult analysis, string sql, QueryAutoFixer fixer, Config.GuardConfiguration config)
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
            // Title
            lblTitle.Text = _analysis.RiskLevel == RiskLevel.Critical
                ? "⛔  Critical Risk: Execution Blocked"
                : "⚠  Warning: Potentially Unsafe Query";

            lblTitle.ForeColor = _analysis.RiskLevel == RiskLevel.Critical
                ? Color.FromArgb(215, 58, 73)
                : Color.FromArgb(227, 179, 65);

            // Summary
            lblSummary.Text =
                $"QueryGuard detected {_analysis.Violations.Count} issue(s) in your query " +
                $"against '{_analysis.AffectedTable}'.";

            // Violation list
            lstViolations.Items.Clear();
            foreach (var v in _analysis.Violations)
            {
                string prefix = v.IsWarning ? "⚠  " : "✖  ";
                lstViolations.Items.Add(prefix + v.Message);
            }

            // SQL preview
            txtSqlPreview.Text = _originalSql;

            // Impact information
            lblImpactTable.Text = _analysis.AffectedTable;
            lblImpactOp.Text = _analysis.StatementType.ToString().ToUpperInvariant();
            lblImpactRows.Text = _analysis.IsBlocked ? "ALL ROWS (unbounded)" : "Filtered rows";
            lblImpactRows.ForeColor = _analysis.IsBlocked
                ? Color.FromArgb(215, 58, 73)
                : Color.FromArgb(63, 185, 80);

            // Auto-fix button availability
            var fix = _fixer.TryFix(_originalSql, _analysis);
            btnApplyFix.Enabled = fix != null;
            if (fix != null)
            {
                FixedSql = fix.FixedSql;
                btnApplyFix.Text = $"🔧 Auto-Fix  ({fix.Description})";
            }

            // Override button styling — make it look deliberate/dangerous
            btnExecuteAnyway.BackColor = Color.FromArgb(60, 20, 20);
            btnExecuteAnyway.ForeColor = Color.FromArgb(255, 123, 114);
            btnExecuteAnyway.Text = _allowOverride
                ? "Execute Anyway (Override - Audited)"
                : "Override Disabled By Team Policy";
            btnExecuteAnyway.Enabled = _allowOverride;
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
            // Two-step confirmation for critical overrides
            if (_analysis.RiskLevel == RiskLevel.Critical && _requireDoubleConfirm)
            {
                var confirm = MessageBox.Show(
                    "Are you absolutely sure?\n\n" +
                    $"Executing this {_analysis.StatementType.ToString().ToUpper()} " +
                    $"without a WHERE clause on '{_analysis.AffectedTable}' may cause " +
                    "irreversible data loss.\n\n" +
                    "This action will be logged to the audit trail.",
                    "Confirm Override",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);   // Default to "No"

                if (confirm != DialogResult.Yes) return;
            }

            UserChoice = UserChoice.ForceExecute;
            DialogResult = DialogResult.Yes;
            Close();
        }

        /// <summary>
        /// Returns an initialized modal dialog as a wrapped SSMS window.
        /// Use ShowModal() for proper SSMS thread affinity.
        /// </summary>
        public UserChoice ShowModal()
        {
            ShowDialog();
            return UserChoice;
        }

        #region Designer-generated layout (abbreviated)
        private void InitializeComponent()
        {
            this.Text = "QueryGuard — Safety Check";
            this.Size = new Size(580, 520);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(22, 27, 34);
            this.ForeColor = Color.FromArgb(230, 237, 243);
            this.Font = new Font("Segoe UI", 9.5f);

            lblTitle = CreateLabel("", 16, 16, 548, 28, bold: true, size: 13);
            lblSummary = CreateLabel("", 16, 50, 548, 20);

            var sepLine = new Panel
            {
                Location = new Point(16, 76),
                Size = new Size(548, 1),
                BackColor = Color.FromArgb(48, 54, 61)
            };

            lstViolations = new ListBox
            {
                Location = new Point(16, 85),
                Size = new Size(548, 80),
                BackColor = Color.FromArgb(13, 17, 23),
                ForeColor = Color.FromArgb(255, 123, 114),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Cascadia Mono", 9f)
            };

            var lblPreviewHeader = CreateLabel("Query Preview:", 16, 174, 200, 18, bold: true);

            txtSqlPreview = new RichTextBox
            {
                Location = new Point(16, 194),
                Size = new Size(548, 100),
                BackColor = Color.FromArgb(13, 17, 23),
                ForeColor = Color.FromArgb(201, 209, 217),
                Font = new Font("Cascadia Mono", 9f),
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                ScrollBars = RichTextBoxScrollBars.Vertical
            };

            var lblImpact = CreateLabel("Impact Assessment:", 16, 304, 200, 18, bold: true);

            var impactGrid = new TableLayoutPanel
            {
                Location = new Point(16, 324),
                Size = new Size(548, 60),
                ColumnCount = 4,
                RowCount = 2,
                BackColor = Color.FromArgb(13, 17, 23)
            };
            impactGrid.Controls.Add(CreateLabel("Table", 0, 0, 130, 18, muted: true), 0, 0);
            impactGrid.Controls.Add(CreateLabel("Operation", 0, 0, 130, 18, muted: true), 1, 0);
            impactGrid.Controls.Add(CreateLabel("Rows", 0, 0, 130, 18, muted: true), 2, 0);
            lblImpactTable = CreateLabel("—", 0, 0, 130, 20, bold: true);
            lblImpactOp = CreateLabel("—", 0, 0, 130, 20, bold: true);
            lblImpactRows = CreateLabel("—", 0, 0, 130, 20, bold: true);
            impactGrid.Controls.Add(lblImpactTable, 0, 1);
            impactGrid.Controls.Add(lblImpactOp, 1, 1);
            impactGrid.Controls.Add(lblImpactRows, 2, 1);

            var sep2 = new Panel
            {
                Location = new Point(16, 392),
                Size = new Size(548, 1),
                BackColor = Color.FromArgb(48, 54, 61)
            };

            btnCancel = CreateButton("✕  Cancel Execution", 16, 404, 160, 36,
                Color.FromArgb(33, 38, 45), Color.FromArgb(230, 237, 243));
            btnApplyFix = CreateButton("🔧 Auto-Fix Query", 184, 404, 190, 36,
                Color.FromArgb(31, 111, 235), Color.White);
            btnExecuteAnyway = CreateButton("Execute Anyway", 382, 404, 182, 36,
                Color.FromArgb(60, 20, 20), Color.FromArgb(255, 123, 114));

            btnCancel.Click += btnCancel_Click;
            btnApplyFix.Click += btnApplyFix_Click;
            btnExecuteAnyway.Click += btnExecuteAnyway_Click;

            Controls.AddRange(new Control[] {
                lblTitle, lblSummary, sepLine, lstViolations,
                lblPreviewHeader, txtSqlPreview, lblImpact, impactGrid,
                sep2, btnCancel, btnApplyFix, btnExecuteAnyway
            });
        }

        private static Label CreateLabel(string text, int x, int y, int w, int h,
            bool bold = false, bool muted = false, float size = 9.5f)
            => new Label
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, h),
                ForeColor = muted ? Color.FromArgb(110, 118, 129) : Color.FromArgb(230, 237, 243),
                Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
                AutoEllipsis = true
            };

        private static Button CreateButton(string text, int x, int y, int w, int h,
            Color bg, Color fg)
            => new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, h),
                BackColor = bg,
                ForeColor = fg,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                Cursor = Cursors.Hand
            };

        private Label lblTitle = null!;
        private Label lblSummary = null!;
        private Label lblImpactTable = null!;
        private Label lblImpactOp = null!;
        private Label lblImpactRows = null!;
        private ListBox lstViolations = null!;
        private RichTextBox txtSqlPreview = null!;
        private Button btnCancel = null!;
        private Button btnApplyFix = null!;
        private Button btnExecuteAnyway = null!;
        #endregion
    }

    public enum UserChoice { Cancel, ApplyFix, ForceExecute }
}

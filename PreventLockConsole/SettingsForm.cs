namespace PreventLockConsole
{
    public class SettingsForm : Form
    {
        private readonly Config _cfg;
        private NumericUpDown nudMove, nudIdle, nudCheck;
        private CheckBox cbEnabled, cbStartInTray, cbUseExec;
        private TextBox tbPause, tbEnable, tbExit;

        public SettingsForm(Config cfg)
        {
            _cfg = cfg;
            Text = "PreventLock Settings";
            Width = 420;
            Height = 360;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            var lblEnabled = new Label() { Left = 12, Top = 12, Width = 120, Text = "启用服务" };
            cbEnabled = new CheckBox() { Left = 140, Top = 10, Checked = _cfg.Enabled };

            var lblMove = new Label() { Left = 12, Top = 44, Width = 120, Text = "MoveIntervalSeconds" };
            nudMove = new NumericUpDown()
                { Left = 140, Top = 42, Minimum = 1, Maximum = 3600, Value = _cfg.MoveIntervalSeconds };

            var lblIdle = new Label() { Left = 12, Top = 80, Width = 120, Text = "IdleToStartSeconds" };
            nudIdle = new NumericUpDown()
                { Left = 140, Top = 78, Minimum = 1, Maximum = 86400, Value = _cfg.IdleToStartSeconds };

            var lblCheck = new Label() { Left = 12, Top = 116, Width = 120, Text = "CheckIntervalMilliseconds" };
            nudCheck = new NumericUpDown()
                { Left = 140, Top = 114, Minimum = 100, Maximum = 60000, Value = _cfg.CheckIntervalMilliseconds };

            var lblHot = new Label() { Left = 12, Top = 152, Width = 120, Text = "Hotkeys (Ctrl+Alt+P)" };
            tbPause = new TextBox() { Left = 140, Top = 150, Width = 240, Text = _cfg.Hotkeys.TogglePause };
            tbEnable = new TextBox() { Left = 140, Top = 180, Width = 240, Text = _cfg.Hotkeys.ToggleEnable };
            tbExit = new TextBox() { Left = 140, Top = 210, Width = 240, Text = _cfg.Hotkeys.Exit };

            cbStartInTray = new CheckBox()
                { Left = 12, Top = 246, Width = 180, Text = "Start in tray", Checked = _cfg.StartInTray };
            cbUseExec = new CheckBox()
            {
                Left = 200, Top = 246, Width = 200, Text = "Use SetThreadExecutionState",
                Checked = _cfg.UseExecutionState
            };

            var btnOk = new Button() { Text = "OK", Left = 220, Width = 80, Top = 270, DialogResult = DialogResult.OK };
            var btnCancel = new Button()
                { Text = "Cancel", Left = 310, Width = 80, Top = 270, DialogResult = DialogResult.Cancel };

            btnOk.Click += (s, e) =>
            {
                Apply();
                Close();
            };
            btnCancel.Click += (s, e) => { Close(); };

            Controls.AddRange(new Control[]
            {
                lblEnabled, cbEnabled, lblMove, nudMove, lblIdle, nudIdle, lblCheck, nudCheck, lblHot, tbPause,
                tbEnable, tbExit, cbStartInTray, cbUseExec, btnOk, btnCancel
            });
        }

        private void Apply()
        {
            _cfg.Enabled = cbEnabled.Checked;
            _cfg.MoveIntervalSeconds = (int)nudMove.Value;
            _cfg.IdleToStartSeconds = (int)nudIdle.Value;
            _cfg.CheckIntervalMilliseconds = (int)nudCheck.Value;
            _cfg.Hotkeys.TogglePause = tbPause.Text;
            _cfg.Hotkeys.ToggleEnable = tbEnable.Text;
            _cfg.Hotkeys.Exit = tbExit.Text;
            _cfg.StartInTray = cbStartInTray.Checked;
            _cfg.UseExecutionState = cbUseExec.Checked;
        }
    }
}
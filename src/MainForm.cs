using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace KeyMind
{
    internal static class Skin
    {
        public static readonly Color Background = Color.FromArgb(11, 14, 23);
        public static readonly Color Sidebar = Color.FromArgb(16, 20, 31);
        public static readonly Color Card = Color.FromArgb(23, 29, 43);
        public static readonly Color Input = Color.FromArgb(31, 38, 55);
        public static readonly Color Border = Color.FromArgb(43, 52, 74);
        public static readonly Color White = Color.FromArgb(246, 247, 253);
        public static readonly Color Soft = Color.FromArgb(170, 183, 208);
        public static readonly Color Purple = Color.FromArgb(144, 106, 254);
        public static readonly Color PurpleLight = Color.FromArgb(186, 159, 255);
        public static readonly Color Mint = Color.FromArgb(89, 224, 192);
        public static readonly Color Warning = Color.FromArgb(245, 192, 107);
        public static Font Font(float size, bool bold) { return new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point); }
        public static GraphicsPath Rounded(Rectangle bounds, int radius)
        {
            int r = Math.Max(2, radius * 2);
            GraphicsPath p = new GraphicsPath();
            p.AddArc(bounds.X, bounds.Y, r, r, 180, 90);
            p.AddArc(bounds.Right - r, bounds.Y, r, r, 270, 90);
            p.AddArc(bounds.Right - r, bounds.Bottom - r, r, r, 0, 90);
            p.AddArc(bounds.X, bounds.Bottom - r, r, r, 90, 90);
            p.CloseFigure(); return p;
        }
    }
    internal sealed class ModernCard : Panel
    {
        public Color Fill = Skin.Card;
        public Color Edge = Skin.Border;
        public int Radius = 17;
        public bool DrawEdge = true;
        public ModernCard()
        {
            DoubleBuffered = true;
            BackColor = Skin.Card;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath p = Skin.Rounded(new Rectangle(1, 1, Width - 3, Height - 3), Radius))
            using (SolidBrush b = new SolidBrush(Fill))
            {
                e.Graphics.FillPath(b, p);
                if (DrawEdge) using (Pen stroke = new Pen(Edge, 1)) e.Graphics.DrawPath(stroke, p);
            }
        }
    }
    internal sealed class ModernButton : Control
    {
        public Color Fill = Skin.Input;
        public Color Ink = Skin.White;
        public Color Edge = Skin.Border;
        public bool Selected = false;
        public bool ShowEdge = true;
        public int Radius = 11;
        private bool _hover;
        public ModernButton()
        {
            DoubleBuffered = true;
            Cursor = Cursors.Hand;
            Font = Skin.Font(10, true);
            Size = new Size(150, 42);
            SetStyle(ControlStyles.Selectable | ControlStyles.StandardClick | ControlStyles.UserPaint, true);
            TabStop = true;
        }
        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Return || e.KeyCode == Keys.Space) { OnClick(EventArgs.Empty); e.Handled = true; }
            base.OnKeyDown(e);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color fill = Selected ? Skin.Purple : Fill;
            if (_hover) fill = Selected ? Skin.PurpleLight : Color.FromArgb(Math.Min(255, fill.R + 12), Math.Min(255, fill.G + 12), Math.Min(255, fill.B + 15));
            using (GraphicsPath p = Skin.Rounded(new Rectangle(1, 1, Width - 3, Height - 3), Radius))
            using (SolidBrush b = new SolidBrush(fill))
            {
                e.Graphics.FillPath(b, p);
                if (ShowEdge && !Selected) using (Pen border = new Pen(Edge)) e.Graphics.DrawPath(border, p);
            }
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(9, 3, Width - 18, Height - 6),
                Selected ? Skin.Background : Ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }
    internal sealed class SwitchControl : Control
    {
        private bool _checked;
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool Checked { get { return _checked; } set { _checked = value; Invalidate(); } }
        public event EventHandler CheckedChanged;
        public SwitchControl(bool value)
        {
            _checked = value; Size = new Size(49, 28); Cursor = Cursors.Hand; TabStop = true; DoubleBuffered = true;
        }
        protected override void OnClick(EventArgs e)
        {
            Checked = !Checked;
            if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            base.OnClick(e);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space) { OnClick(EventArgs.Empty); e.Handled = true; }
            base.OnKeyDown(e);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath p = Skin.Rounded(new Rectangle(1, 1, 47, 25), 13))
            using (SolidBrush b = new SolidBrush(Checked ? Skin.Purple : Skin.Border)) e.Graphics.FillPath(b, p);
            using (SolidBrush b = new SolidBrush(Color.White)) e.Graphics.FillEllipse(b, Checked ? 25 : 5, 5, 18, 18);
        }
    }
    public sealed class MainForm : Form
    {
        private readonly KeyMindSettings _settings;
        private readonly WordConverter _converter;
        private KeyboardEngine _engine;
        private NotifyIcon _tray;
        private bool _exit;
        private string _page = "dashboard";
        private Panel _menu;
        private Panel _content;
        private Panel _header;
        private Label _headerTitle;
        private readonly Dictionary<string, ModernButton> _nav = new Dictionary<string, ModernButton>();
        private ModernButton _languageBtn;
        private TextBox _inputSample, _customEn, _customAr, _exclusions;
        private Label _sampleResult, _sampleNote;
        private CheckBox _testArabic, _testForce;
        private TrackBar _threshold;
        private Label _thresholdText, _diagnosticsLabel;
        private System.Windows.Forms.Timer _diagnosticsTimer;
        private bool IsArabic { get { return _settings.UiLanguage != "en"; } }
        private string T(string ar, string en) { return IsArabic ? ar : en; }

        public MainForm()
        {
            _settings = SettingsStore.Load();
            _converter = new WordConverter(_settings);
            Text = "KeyMind 3.0 · Smart Keyboard";
            MinimumSize = new Size(1030, 735);
            ClientSize = new Size(1190, 780);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            BackColor = Skin.Background;
            ForeColor = Skin.White;
            Font = Skin.Font(10, false);
            Icon = LoadIcon();
            BuildShell();
            BuildTray();
            Navigate("dashboard");
            _diagnosticsTimer = new System.Windows.Forms.Timer();
            _diagnosticsTimer.Interval = 700;
            _diagnosticsTimer.Tick += delegate
            {
                if (_diagnosticsLabel != null && !_diagnosticsLabel.IsDisposed && _page == "diagnostics")
                    _diagnosticsLabel.Text = _engine == null ? "Listener is initializing..." : _engine.DiagnosticSnapshot();
            };
            _diagnosticsTimer.Start();
            Shown += delegate
            {
                try
                {
                    _engine = new KeyboardEngine(this, _converter, _settings);
                    _engine.Converted += delegate(string original, string newText)
                    {
                        if (IsDisposed || !IsHandleCreated) return;
                        BeginInvoke((Action)delegate
                        {
                            _settings.Corrections++;
                            SettingsStore.Save(_settings);
                            if (_settings.ShowNotifications && _tray != null)
                                _tray.ShowBalloonTip(900, "KeyMind", T("تم تصحيح الكلمة", "A word was corrected"), ToolTipIcon.None);
                            UpdateState();
                        });
                    };
                    if (!_engine.IsHookActive)
                        MessageBox.Show(T("لم يتمكن التطبيق من تنشيط مراقبة لوحة المفاتيح. راجع إعدادات الأمان وأعد التشغيل.",
                                          "The keyboard listener did not start. Check Windows security settings and restart."), "KeyMind", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    UpdateState();
                }
                catch (Exception ex)
                {
                    Program.LogError(ex);
                    MessageBox.Show(T("تعذر تفعيل لوحة المفاتيح. افتح %LOCALAPPDATA%\\KeyMind\\error.log", "Keyboard hook failed. See %LOCALAPPDATA%\\KeyMind\\error.log"), "KeyMind");
                }
            };
            ResizeEnd += delegate { RenderPage(); };
        }
        private Icon LoadIcon()
        {
            try { return new Icon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "keymind.ico")); }
            catch { return SystemIcons.Application; }
        }
        private Label AddLabel(Control to, string value, int x, int y, int w, int h, int pt, Color ink, bool bold, ContentAlignment align)
        {
            Label l = new Label { Text = value, Location = new Point(x, y), Size = new Size(w, h), ForeColor = ink,
                Font = Skin.Font(pt, bold), BackColor = to is ModernCard ? ((ModernCard)to).Fill : to.BackColor,
                TextAlign = align, AutoEllipsis = true, RightToLeft = IsArabic ? RightToLeft.Yes : RightToLeft.No };
            to.Controls.Add(l);
            return l;
        }
        private ModernButton AddButton(Control to, string name, int x, int y, int w, int h, bool primary, EventHandler click)
        {
            ModernButton b = new ModernButton { Text = name, Location = new Point(x, y), Size = new Size(w, h),
                Fill = primary ? Skin.Purple : Skin.Input, Ink = primary ? Skin.Background : Skin.White,
                Selected = primary, ShowEdge = !primary };
            if (click != null) b.Click += click;
            to.Controls.Add(b);
            return b;
        }
        private ModernCard AddCard(Control to, int x, int y, int w, int h)
        {
            ModernCard c = new ModernCard { Location = new Point(x, y), Size = new Size(Math.Max(70,w), h) };
            to.Controls.Add(c); return c;
        }
        private TextBox AddInput(Control to, int x, int y, int w, int h, string value, bool multi, bool rtl)
        {
            TextBox box = new TextBox { Location = new Point(x, y), Size = new Size(w,h), Text = value,
                Multiline = multi, BorderStyle = BorderStyle.FixedSingle, Font = Skin.Font(11, false),
                BackColor = Skin.Input, ForeColor = Skin.White, RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No,
                ScrollBars = multi ? ScrollBars.Vertical : ScrollBars.None };
            to.Controls.Add(box); return box;
        }
        private void BuildShell()
        {
            _menu = new Panel { Dock = DockStyle.Right, Width = 246, BackColor = Skin.Sidebar };
            _header = new Panel { Dock = DockStyle.Top, Height = 79, BackColor = Skin.Background };
            _content = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Skin.Background };
            Controls.Add(_content); Controls.Add(_header); Controls.Add(_menu);
            PaintBrand();
            AddLabel(_menu, "KEYMIND", 89, 35, 137, 32, 17, Skin.White, true, ContentAlignment.MiddleLeft);
            AddLabel(_menu, "INTELLIGENT INPUT", 90, 66, 145, 16, 8, Skin.Soft, false, ContentAlignment.MiddleLeft);
            AddLabel(_menu, T("مساحة العمل", "WORKSPACE"), 21, 127, 205, 19, 9, Skin.Soft, true, ContentAlignment.MiddleRight);
            AddNavigation("dashboard", T("نظرة عامة", "Overview"), 161);
            AddNavigation("test", T("مختبر الكتابة", "Typing Lab"), 220);
            AddNavigation("settings", T("الإعدادات", "Settings"), 279);
            AddNavigation("about", T("حول التطبيق", "About"), 338);
            AddNavigation("diagnostics", T("تشخيص الكتابة", "Live diagnostics"), 397);
            AddLabel(_menu, T("عربي   /   English", "Arabic   /   English"), 22, 550, 200, 27, 10, Skin.PurpleLight, true, ContentAlignment.MiddleRight);
            AddLabel(_menu, T("الخصوصية أولاً — بلا سحابة", "Privacy first — offline"), 18, 582, 206, 23, 9, Skin.Soft, false, ContentAlignment.MiddleRight);
            AddLabel(_menu, "KEYMIND  3.0   •   WINDOWS", 22, 688, 202, 24, 9, Skin.Soft, false, ContentAlignment.MiddleRight);
            _headerTitle = AddLabel(_header, "", 42, 17, 480, 43, 20, Skin.White, true, ContentAlignment.MiddleLeft);
            _headerTitle.RightToLeft = IsArabic ? RightToLeft.Yes : RightToLeft.No;
            _languageBtn = AddButton(_header, IsArabic ? "English UI" : "واجهة عربية", 550, 20, 130, 39, false,
                delegate { _settings.UiLanguage = IsArabic ? "en" : "ar"; SettingsStore.Save(_settings); Rebuild(); });
            _header.Resize += delegate { _languageBtn.Left = Math.Max(510, _header.Width - 176); _headerTitle.Width = Math.Max(180, _header.Width - 225); };
        }
        private void PaintBrand()
        {
            PictureBox p = new PictureBox { Location = new Point(24, 30), Size = new Size(56, 55), SizeMode = PictureBoxSizeMode.StretchImage, BackColor = Skin.Sidebar };
            string icon = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "brand.png");
            try { p.Image = Image.FromFile(icon); } catch { }
            _menu.Controls.Add(p);
        }
        private void AddNavigation(string key, string title, int top)
        {
            ModernButton b = AddButton(_menu, title, 20, top, 205, 48, false, delegate { Navigate(key); });
            b.Radius = 11; _nav[key] = b;
        }
        private void BuildTray()
        {
            _tray = new NotifyIcon { Icon = Icon ?? SystemIcons.Application, Visible = true, Text = "KeyMind · Smart Keyboard" };
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Open KeyMind", null, delegate { ShowWindow(); });
            menu.Items.Add("Pause / Resume", null, delegate { _settings.Enabled = !_settings.Enabled; SettingsStore.Save(_settings); UpdateState(); });
            menu.Items.Add("Exit", null, delegate { _exit = true; Close(); });
            _tray.ContextMenuStrip = menu;
            _tray.DoubleClick += delegate { ShowWindow(); };
        }
        private void ShowWindow() { Show(); WindowState = FormWindowState.Normal; Activate(); }
        private void Rebuild()
        {
            _menu.Dispose(); _header.Dispose(); _content.Dispose();
            _nav.Clear(); BuildShell(); Navigate(_page);
        }
        private void Navigate(string page)
        {
            _page = page;
            foreach (KeyValuePair<string, ModernButton> pair in _nav)
            {
                pair.Value.Selected = pair.Key == page;
                pair.Value.Ink = pair.Key == page ? Skin.Background : Skin.Soft;
                pair.Value.Invalidate();
            }
            _headerTitle.Text = page == "dashboard" ? T("لوحة التحكم", "Overview") :
                page == "test" ? T("مختبر الكتابة", "Typing Lab") :
                page == "settings" ? T("الإعدادات", "Settings") :
                page == "diagnostics" ? T("تشخيص الكتابة المباشر", "Live keyboard diagnostics") : T("عن KeyMind", "About KeyMind");
            RenderPage();
        }
        private int WidthAvailable { get { return Math.Max(705, _content.ClientSize.Width - 76); } }
        private void RenderPage()
        {
            if (_content == null || _content.IsDisposed) return;
            _diagnosticsLabel = null;
            _content.SuspendLayout(); _content.Controls.Clear();
            _content.AutoScrollMinSize = Size.Empty;
            _content.AutoScrollPosition = new Point(0, 0);
            if (_page == "dashboard") Dashboard();
            else if (_page == "test") TestPage();
            else if (_page == "settings") SettingsPage();
            else if (_page == "diagnostics") DiagnosticsPage();
            else AboutPage();
            _content.ResumeLayout();
        }
        private void Dashboard()
        {
            int w = WidthAvailable;
            ModernCard hero = AddCard(_content, 35, 27, w, 218);
            hero.Fill = Skin.Card; hero.BackColor = Skin.Card;
            AddLabel(hero, T("إكتب بلغتك. والكيبورد يفهمك.", "Type naturally. KeyMind understands."), 30, 27, w - 60, 53, 21, Skin.White, true, ContentAlignment.MiddleRight);
            AddLabel(hero, T("العربية والإنجليزية، بدون تبديل Alt + Shift.", "Arabic + English without Alt + Shift."), 30, 84, w - 60, 32, 12, Skin.Soft, false, ContentAlignment.MiddleRight);
            AddButton(hero, (_engine != null && !_engine.IsHookActive) ? T("! تعذر تشغيل المحرك", "! Hook unavailable") : (_settings.Enabled ? T("●  المحرك مفعل", "●  Engine active") : T("◯  المحرك متوقف", "◯  Engine paused")),
                w - 240, 145, 210, 44, true, delegate { _settings.Enabled = !_settings.Enabled; SettingsStore.Save(_settings); Navigate("dashboard"); });
            AddLabel(hero, "AR  ⇄  EN", 30, 154, 232, 30, 14, Skin.PurpleLight, true, ContentAlignment.MiddleLeft);
            int gap = 17; int tileW = (w - gap * 2) / 3;
            StatTile(35, 267, tileW, 146, T("تصحيحاتك", "Corrections"), _settings.Corrections.ToString("N0"), T("منذ بدء الاستخدام", "Since installation"));
            StatTile(35 + tileW + gap, 267, tileW, 146, T("مستوى الدقة", "Confidence"), _settings.Threshold.ToString() + "%", T("تحكم به من الإعدادات", "Configurable"));
            StatTile(35 + (tileW + gap) * 2, 267, tileW, 146, T("قاموس اللغة", "Dictionary"), "100K+", T("مفردة إنجليزية + عربي", "English entries + Arabic"));
            ModernCard lower = AddCard(_content, 35, 436, w, 242);
            AddLabel(lower, T("كيف يعمل KeyMind؟", "How KeyMind works"), 28, 20, w-54, 35, 15, Skin.White, true, ContentAlignment.MiddleRight);
            AddLabel(lower, T("١   اكتب مخؤشم على الكيبورد العربي", "1   Type مخؤشم using Arabic layout"), 30, 68, w-60, 27, 11, Skin.Soft, false, ContentAlignment.MiddleRight);
            AddLabel(lower, T("٢   عند الضغط على Space يكتشف كلمة local", "2   KeyMind recognizes local when you hit Space"), 30, 105, w-60, 27, 11, Skin.Soft, false, ContentAlignment.MiddleRight);
            AddLabel(lower, T("٣   الكلمات غير المعروفة: Ctrl + Alt + Shift + Space للتحويل الحرفي", "3   Unknown words: Ctrl + Alt + Shift + Space for literal key conversion"), 30, 142, w-60, 27, 11, Skin.Soft, false, ContentAlignment.MiddleRight);
            AddButton(lower, T("جرّب الآن", "Open typing lab"), w - 186, 183, 156, 42, false, delegate { Navigate("test"); });
            _content.AutoScrollMinSize = new Size(0, 720);
        }
        private void StatTile(int x, int y, int w, int h, string title, string value, string note)
        {
            ModernCard c = AddCard(_content, x, y, w, h);
            AddLabel(c, title, 18, 13, w-36, 27, 10, Skin.Soft, false, ContentAlignment.MiddleRight);
            AddLabel(c, value, 18, 49, w-36, 49, 25, Skin.White, true, ContentAlignment.MiddleRight);
            AddLabel(c, note, 18, 110, w-36, 22, 9, Skin.PurpleLight, false, ContentAlignment.MiddleRight);
        }
        private void TestPage()
        {
            int w = WidthAvailable;
            ModernCard c = AddCard(_content, 35, 28, w, 355);
            AddLabel(c, T("جرب محرك التحويل فورًا", "Try the conversion engine"), 27, 23, w-54, 42, 17, Skin.White, true, ContentAlignment.MiddleRight);
            AddLabel(c, T("اكتب كلمة كما ظهرت بالخطأ، ثم اختر تخطيط الكيبورد المستخدم.", "Enter the wrong-layout word, then choose its current keyboard layout."), 27, 68, w-54, 28, 10, Skin.Soft, false, ContentAlignment.MiddleRight);
            AddLabel(c, T("النص المكتوب", "Typed text"), 28, 110, w-56, 28, 11, Skin.Soft, true, ContentAlignment.MiddleRight);
            _inputSample = AddInput(c, 28, 146, w-56, 42, "مخؤشم", false, true);
            _testArabic = new CheckBox { Text = T("تخطيط عربي  ←  إنجليزي", "Arabic layout → English"), Checked = true,
                ForeColor = Skin.White, Font = Skin.Font(10, false), BackColor = Skin.Card,
                Size = new Size(245, 28), Location = new Point(w-280, 206), RightToLeft = RightToLeft.Yes };
            _testArabic.CheckedChanged += delegate { if (_inputSample != null) _inputSample.Text = _testArabic.Checked ? "مخؤشم" : "lpl]"; };
            c.Controls.Add(_testArabic);
            _testForce = new CheckBox { Text = T("تحويل حرفي إجباري", "Force literal mapping"), Checked = false,
                ForeColor = Skin.White, Font = Skin.Font(10, false), BackColor = Skin.Card,
                Size = new Size(224, 28), Location = new Point(w-530, 206), RightToLeft = RightToLeft.Yes };
            c.Controls.Add(_testForce);
            AddButton(c, T("حوّل النص", "Convert text"), w-206, 273, 174, 46, true, delegate { TestConvert(); });
            AddButton(c, T("استبدال المثال", "Switch example"), 28, 273, 176, 46, false,
                delegate { _testArabic.Checked = !_testArabic.Checked; TestConvert(); });
            ModernCard result = AddCard(_content, 35, 402, w, 210);
            AddLabel(result, T("النتيجة المتوقعة", "Converted result"), 27, 20, w-54, 29, 10, Skin.Soft, true, ContentAlignment.MiddleRight);
            _sampleResult = AddLabel(result, "local", 27, 59, w-54, 65, 26, Skin.Mint, true, ContentAlignment.MiddleCenter);
            _sampleNote = AddLabel(result, "", 27, 144, w-54, 34, 10, Skin.Soft, false, ContentAlignment.MiddleCenter);
            AddLabel(_content, T("في التطبيقات الأخرى: Ctrl + Alt + Space للتحويل الذكي، أو أضف Shift للتحويل الإجباري.", "In other apps: Ctrl + Alt + Space for smart conversion; add Shift to force conversion."),
                35, 635, w, 35, 10, Skin.Soft, false, ContentAlignment.MiddleRight);
            _content.AutoScrollMinSize = new Size(0, 700);
            TestConvert();
        }
        private void TestConvert()
        {
            if (_inputSample == null || _sampleResult == null) return;
            var r = _converter.Evaluate(_inputSample.Text.Trim(), _testArabic.Checked, _settings.Threshold, _testForce.Checked);
            _sampleResult.Text = r.Accepted ? r.Converted : r.Converted == r.Original ? _inputSample.Text : r.Converted;
            _sampleResult.ForeColor = r.Accepted ? Skin.Mint : Skin.Warning;
            _sampleResult.RightToLeft = _testArabic.Checked ? RightToLeft.No : RightToLeft.Yes;
            _sampleNote.Text = T("الثقة: ", "Confidence: ") + r.Confidence + "%  ·  " +
                (r.Accepted ? T("مقبول للتصحيح", "Will convert") : T("اقتراح فقط — لن يتم تحويله تلقائيًا", "Suggestion only; auto-correction is off for this word"));
        }
        private void SettingsPage()
        {
            int w = WidthAvailable;
            ModernCard modes = AddCard(_content, 35, 26, w, 209);
            AddLabel(modes, T("التحكم الذكي", "Smart control"), 26, 17, w-52, 39, 16, Skin.White, true, ContentAlignment.MiddleRight);
            AddLabel(modes, T("تشغيل التصحيح تلقائيًا عند المسافة", "Auto-correct when pressing Space"), 25, 71, w-122, 29, 11, Skin.White, true, ContentAlignment.MiddleRight);
            SwitchControl auto = new SwitchControl(_settings.AutoMode) { Location = new Point(31, 72) };
            auto.CheckedChanged += delegate { _settings.AutoMode = auto.Checked; SettingsStore.Save(_settings); };
            modes.Controls.Add(auto);
            AddLabel(modes, T("تشغيل الأداة مع بدء ويندوز", "Start KeyMind with Windows"), 25, 126, w-122, 29, 11, Skin.White, true, ContentAlignment.MiddleRight);
            SwitchControl start = new SwitchControl(_settings.Startup) { Location = new Point(31, 128) };
            start.CheckedChanged += delegate
            {
                _settings.Startup = start.Checked;
                try { SettingsStore.SetStartup(start.Checked); SettingsStore.Save(_settings); }
                catch (Exception ex) { Program.LogError(ex); }
            };
            modes.Controls.Add(start);
            ModernCard confidence = AddCard(_content, 35, 254, w, 163);
            AddLabel(confidence, T("حساسية التصحيح", "Correction sensitivity"), 26, 17, w-52, 34, 15, Skin.White, true, ContentAlignment.MiddleRight);
            AddLabel(confidence, T("كلما زادت النسبة قلّت التصحيحات الخاطئة", "Higher thresholds mean fewer unwanted corrections"), 26, 58, w-52, 26, 10, Skin.Soft, false, ContentAlignment.MiddleRight);
            _threshold = new TrackBar { Location = new Point(20, 95), Size = new Size(Math.Max(120, w-180), 46),
                Minimum = 75, Maximum = 99, TickFrequency = 4, Value = Math.Max(75, Math.Min(99, _settings.Threshold)), BackColor = Skin.Card };
            confidence.Controls.Add(_threshold);
            _thresholdText = AddLabel(confidence, _settings.Threshold + "%", w-142, 95, 100, 35, 17, Skin.PurpleLight, true, ContentAlignment.MiddleRight);
            _threshold.ValueChanged += delegate { _settings.Threshold = _threshold.Value; _thresholdText.Text = _threshold.Value + "%"; SettingsStore.Save(_settings); };
            ModernCard words = AddCard(_content, 35, 436, w, 219);
            AddLabel(words, T("قاموسك الشخصي", "Personal vocabulary"), 26, 13, w-52, 36, 15, Skin.White, true, ContentAlignment.MiddleRight);
            AddLabel(words, T("كلمات إنجليزية إضافية (افصل بينها بفاصلة)", "English custom words (comma-separated)"), 26, 56, w-52, 28, 10, Skin.Soft, false, ContentAlignment.MiddleRight);
            _customEn = AddInput(words, 25, 91, w-50, 35, _settings.ExtraEnglish, false, false);
            AddLabel(words, T("كلمات عربية لا تريد تصحيحها", "Arabic words you want preserved"), 26, 132, w-52, 27, 10, Skin.Soft, false, ContentAlignment.MiddleRight);
            _customAr = AddInput(words, 25, 164, w-50, 34, _settings.ExtraArabic, false, true);
            ModernCard apps = AddCard(_content, 35, 676, w, 169);
            AddLabel(apps, T("تطبيقات مستثناة من المراقبة", "Excluded processes"), 26, 13, w-52, 33, 15, Skin.White, true, ContentAlignment.MiddleRight);
            AddLabel(apps, T("أدخل أسماء العمليات مفصولة بفواصل (مثل: keepass, powershell)", "Comma-separated process names (e.g. keepass, powershell)"), 26, 50, w-52, 25, 10, Skin.Soft, false, ContentAlignment.MiddleRight);
            _exclusions = AddInput(apps, 25, 83, w-50, 36, _settings.ExcludedApps, false, false);
            AddButton(apps, T("حفظ جميع التعديلات", "Save changes"), w-228, 124, 202, 35, true, delegate { SaveUserWords(); });
            AddLabel(_content, T("لا تكتب كلمات المرور داخل مختبر الاختبار. واجهات بعض التطبيقات لا تدعم الإدخال الاصطناعي.", "Do not enter passwords in the lab. Some apps do not support synthetic input."),
                35, 863, w, 44, 10, Skin.Soft, false, ContentAlignment.MiddleRight);
            _content.AutoScrollMinSize = new Size(0, 930);
        }
        private void SaveUserWords()
        {
            if (_customEn == null) return;
            _settings.ExtraEnglish = _customEn.Text;
            _settings.ExtraArabic = _customAr.Text;
            _settings.ExcludedApps = _exclusions.Text;
            _converter.SetCustomWords(_settings.ExtraEnglish, _settings.ExtraArabic);
            SettingsStore.Save(_settings);
            MessageBox.Show(T("تم حفظ الإعدادات.", "Settings saved."), "KeyMind", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        private void DiagnosticsPage()
        {
            int w = WidthAvailable;
            ModernCard card = AddCard(_content, 35, 25, w, 259);
            AddLabel(card, T("مراقبة الكتابة خارج التطبيق", "External keyboard listener"), 25, 20, w-50, 43, 17, Skin.White, true, ContentAlignment.MiddleRight);
            AddLabel(card, T("افتح Chrome واكتب lpl] بلوحة إنجليزية ثم مسافة، وارجع هنا لعرض العدادات.",
                "In Chrome, type lpl] using the English keyboard and hit Space. Return here to inspect counters."),
                25, 71, w-50, 54, 11, Skin.Soft, false, ContentAlignment.MiddleRight);
            _diagnosticsLabel = AddLabel(card, _engine == null ? "Initializing..." : _engine.DiagnosticSnapshot(),
                25, 128, w-50, 105, 10, Skin.Mint, false, ContentAlignment.MiddleLeft);
            _diagnosticsLabel.RightToLeft = RightToLeft.No;
            _diagnosticsLabel.Font = new Font("Consolas", 10, FontStyle.Regular);
            ModernCard how = AddCard(_content, 35, 306, w, 253);
            AddLabel(how, T("كيف تفسر النتائج؟", "How to read the results"), 25, 15, w-50, 39, 15, Skin.White, true, ContentAlignment.MiddleRight);
            AddLabel(how, T("Physical keys لا يزيد: البرنامج لا يستقبل المفاتيح خارج النافذة. تأكد من أن Hook مثبت.",
                "Physical keys not increasing: listener not receiving keystrokes. Close older copies and restart."), 25, 66, w-50, 40, 10, Skin.Soft, false, ContentAlignment.MiddleRight);
            AddLabel(how, T("Spaces يزيد وCandidates لا يزيد: الكلمة لم تتجاوز فحص الثقة أو لغة الكيبورد غير معروفة.",
                "Spaces rise but Candidates don't: confidence check rejected the word or layout is unsupported."), 25, 110, w-50, 42, 10, Skin.Soft, false, ContentAlignment.MiddleRight);
            AddLabel(how, T("Candidates يزيد وReplacements لا يزيد: مشكلة استبدال النص أو قيود Windows.",
                "Candidates rise but Replacements don't: text injection or Windows permissions issue."), 25, 153, w-50, 41, 10, Skin.Soft, false, ContentAlignment.MiddleRight);
            AddButton(how, T("نسخ تقرير التشخيص", "Copy diagnostic report"), w-247, 202, 218, 40, true,
                delegate { if (_engine != null) { try { Clipboard.SetText(_engine.DiagnosticSnapshot()); } catch { } } });
            AddButton(how, T("إعادة تشغيل المراقبة", "Restart listener"), 25, 202, 225, 40, false,
                delegate
                {
                    bool ok = _engine != null && _engine.RestartListener();
                    MessageBox.Show(ok ? T("أُرسل طلب إعادة تشغيل محرك الكتابة.", "Listener restart requested.") :
                        T("تعذر إعادة تشغيل المراقبة. أغلق التطبيق وأعد تشغيله.", "Could not restart the listener. Exit and reopen KeyMind."), "KeyMind");
                });
            AddLabel(_content, T("التشخيص يسجل العدادات واسم التطبيق فقط، ولا يحفظ الأحرف التي تكتبها.",
                "Diagnostics capture counters and active application name only, never the characters you type."),
                35, 587, w, 42, 10, Skin.Soft, false, ContentAlignment.MiddleRight);
            _content.AutoScrollMinSize = new Size(0, 685);
        }

        private void AboutPage()
        {
            int w = WidthAvailable;
            ModernCard c = AddCard(_content, 35, 29, w, 357);
            AddLabel(c, "KEYMIND 3.0", 30, 25, w-60, 55, 28, Skin.White, true, ContentAlignment.MiddleRight);
            AddLabel(c, T("الكتابة بلا حدود بين العربية والإنجليزية", "Seamless writing between Arabic and English"), 30, 90, w-60, 39, 15, Skin.PurpleLight, true, ContentAlignment.MiddleRight);
            AddLabel(c, T("المحرك يحوّل ضغطات المفاتيح الفعلية عبر تخطيط عربي 101 و QWERTY إنجليزي.",
                "The engine maps physical keys between Arabic 101 and English QWERTY."), 30, 149, w-60, 50, 11, Skin.Soft, false, ContentAlignment.MiddleRight);
            AddLabel(c, T("التصحيح التلقائي عند المسافة • تحويل ذكي: Ctrl+Alt+Space", "Auto on Space • Smart: Ctrl+Alt+Space"),
                30, 218, w-60, 29, 11, Skin.White, false, ContentAlignment.MiddleRight);
            AddLabel(c, T("تحويل حرفي لأي كلمة: Ctrl+Alt+Shift+Space • تراجع: Ctrl+Alt+Z", "Force any word: Ctrl+Alt+Shift+Space • Undo: Ctrl+Alt+Z"),
                30, 260, w-60, 30, 11, Skin.White, false, ContentAlignment.MiddleRight);
            AddLabel(c, T("المعالجة محلية. لا يتم حفظ ضغطات المفاتيح أو إرسالها لخادم.", "Local processing; no typed text is logged or sent to a server."),
                30, 308, w-60, 26, 10, Skin.Mint, false, ContentAlignment.MiddleRight);
            ModernCard limitations = AddCard(_content, 35, 407, w, 157);
            AddLabel(limitations, T("حدود الإصدار", "Known limitations"), 25, 15, w-50, 37, 14, Skin.White, true, ContentAlignment.MiddleRight);
            AddLabel(limitations, T("بعض التطبيقات المحمية أو ذات الصلاحيات المرتفعة لا تسمح بتعديل النص. لا تُصحح الكلمات الملتبسة تلقائيًا."
                , "Protected/elevated apps may block text injection; ambiguous words are not changed automatically."),
                25, 57, w-50, 68, 11, Skin.Soft, false, ContentAlignment.MiddleRight);
            _content.AutoScrollMinSize = new Size(0, 610);
        }
        private void UpdateState()
        {
            if (_tray == null) return;
            _tray.Text = (_engine != null && !_engine.IsHookActive) ? "KeyMind · Hook error" : (_settings.Enabled ? "KeyMind · Active" : "KeyMind · Paused");
            if (_page == "dashboard") RenderPage();
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_exit && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true; Hide();
                return;
            }
            if (_diagnosticsTimer != null) { _diagnosticsTimer.Stop(); _diagnosticsTimer.Dispose(); }
            if (_engine != null) _engine.Dispose();
            if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
            base.OnFormClosing(e);
        }
    }
}

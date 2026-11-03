using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

using Timer = System.Windows.Forms.Timer;

namespace WindowStickyNote;

public partial class Form1 : Form
{
    private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType,
        IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax,
        IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc,
        uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(Point point);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "GetClassLong")]
    private static extern int GetClassLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetClassLong")]
    private static extern int SetClassLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
        public bool Equals(RECT o) =>
            Left == o.Left && Top == o.Top && Right == o.Right && Bottom == o.Bottom;
    }

    private const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
    private const uint EVENT_OBJECT_DESTROY = 0x8001;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const int OBJID_WINDOW = 0;

    private const int GCL_STYLE = -26;
    private const int CS_DROPSHADOW = 0x00020000;

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;

    private static readonly Color ColorAccent = Color.FromArgb(76, 175, 80);
    private static readonly Color ColorIdle = Color.FromArgb(190, 184, 168);

    private NoteData _data = null!;
    private NoteColors.Palette _palette = null!;

    private IntPtr _targetHwnd = IntPtr.Zero;
    private IntPtr _hookLocation = IntPtr.Zero;
    private IntPtr _hookDestroy = IntPtr.Zero;
    private WinEventDelegate _winEventProc = null!;

    private int _offsetX;
    private int _offsetY;

    private bool _isSelectingTarget;
    private bool _isDragging;
    private bool _isCommittingDrag;
    private Point _dragStartMouse;
    private Point _dragStartLocation;
    private RECT _lastRect;

    private Timer _saveTimer = null!;

    private Panel _header = null!;
    private Label _bindDot = null!;
    private Button _colorButton = null!;
    private Button _pinButton = null!;
    private Button _closeButton = null!;
    private Panel _contentHost = null!;
    private RichTextBox _noteBox = null!;
    private Label _placeholder = null!;
    private Label _status = null!;

    public Form1(NoteData data)
    {
        _data = data;
        _palette = NoteColors.Get(_data.ColorKey);

        // 加载图标
        try {
            this.Icon = new Icon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico"));
        } catch { }
        
        InitializeUI();
        LoadNote();
    }

    private void InitializeUI()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(340, 260);
        BackColor = _palette.Back;
        Font = new Font("Microsoft YaHei UI", 9.5f);
        DoubleBuffered = true;

        _saveTimer = new Timer { Interval = 600 };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            SaveNow();
        };

        _header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 34,
            BackColor = _palette.Header,
        };

        _bindDot = new Label
        {
            Text = "●",
            AutoSize = false,
            Size = new Size(20, 20),
            Location = new Point(8, 7),
            ForeColor = ColorIdle,
            Font = new Font("Segoe UI", 10f),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent,
        };

        _colorButton = CreateIconButton("🎨", new Point(Size.Width - 110, 4));
        _colorButton.Click += ShowColorMenu;

        _pinButton = CreateIconButton("📌", new Point(Size.Width - 74, 4));
        _pinButton.Click += OnPinButtonClick;

        _closeButton = CreateIconButton("✕", new Point(Size.Width - 38, 4));
        _closeButton.Click += (_, _) => Close();

        _header.Controls.Add(_bindDot);
        _header.Controls.Add(_colorButton);
        _header.Controls.Add(_pinButton);
        _header.Controls.Add(_closeButton);

        _contentHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = _palette.Back,
            Padding = new Padding(12, 8, 12, 4),
        };

        _noteBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackColor = _palette.Back,
            ForeColor = _palette.Text,
            Font = new Font("Microsoft YaHei UI", 10.5f),
            ScrollBars = RichTextBoxScrollBars.Vertical,
            DetectUrls = false,
            AcceptsTab = true,
        };
        _noteBox.TextChanged += (_, _) =>
        {
            UpdatePlaceholder();
            ScheduleSave();
        };
        _noteBox.GotFocus += (_, _) => UpdatePlaceholder();
        _noteBox.LostFocus += (_, _) => UpdatePlaceholder();

        _placeholder = new Label
        {
            Text = "在这里记录…",
            AutoSize = false,
            Dock = DockStyle.Fill,
            ForeColor = _palette.Muted,
            Font = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Italic),
            BackColor = _palette.Back,
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(2, 2, 0, 0),
            Cursor = Cursors.IBeam,
        };
        _placeholder.Click += (_, _) => _noteBox.Focus();

        _contentHost.Controls.Add(_noteBox);
        _contentHost.Controls.Add(_placeholder);

        _status = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 24,
            Text = "自由便签 · 拖动顶栏移动",
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 0, 0),
            Font = new Font("Microsoft YaHei UI", 8.5f),
            ForeColor = _palette.Muted,
            BackColor = _palette.Back,
        };

        Controls.Add(_contentHost);
        Controls.Add(_status);
        Controls.Add(_header);

        AttachDragHandlers(_header);
        AttachDragHandlers(_bindDot);
        AttachDragHandlers(_status);
    }

    private Button CreateIconButton(string text, Point loc)
    {
        var b = new Button
        {
            Text = text,
            Size = new Size(30, 26),
            Location = loc,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Emoji", 10f),
            BackColor = _palette.Header,
            ForeColor = _palette.Text,
            TabStop = false,
            Cursor = Cursors.Hand,
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = _palette.Header;
        b.FlatAppearance.MouseDownBackColor = _palette.Header;
        return b;
    }

    private void ShowColorMenu(object? sender, EventArgs e)
    {
        var menu = new ContextMenuStrip { ShowImageMargin = false };
        foreach (var key in NoteColors.Keys)
        {
            var p = NoteColors.Get(key);
            var item = new ToolStripMenuItem($"  {key}")
            {
                BackColor = p.Display,
                ForeColor = Color.FromArgb(50, 50, 50),
            };
            var captured = key;
            item.Click += (_, _) => ApplyColor(captured);
            menu.Items.Add(item);
        }
        menu.Show(_colorButton, new Point(0, _colorButton.Height));
    }

    private void ApplyColor(string key)
    {
        _data.ColorKey = key;
        _palette = NoteColors.Get(key);

        BackColor = _palette.Back;
        _header.BackColor = _palette.Header;
        _contentHost.BackColor = _palette.Back;
        _noteBox.BackColor = _palette.Back;
        _noteBox.ForeColor = _palette.Text;
        _placeholder.BackColor = _palette.Back;
        _placeholder.ForeColor = _palette.Muted;
        _status.BackColor = _palette.Back;
        _status.ForeColor = _palette.Muted;

        foreach (var b in new[] { _colorButton, _pinButton, _closeButton })
        {
            b.BackColor = _palette.Header;
            b.ForeColor = _palette.Text;
            b.FlatAppearance.MouseOverBackColor = _palette.Header;
            b.FlatAppearance.MouseDownBackColor = _palette.Header;
        }

        SaveNow();
    }

    private void UpdatePlaceholder()
    {
        _placeholder.Visible = _noteBox.Text.Length == 0 && !_noteBox.Focused;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int style = GetClassLong(Handle, GCL_STYLE);
        SetClassLong(Handle, GCL_STYLE, style | CS_DROPSHADOW);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ApplyRoundedRegion(12);
    }

    private void ApplyRoundedRegion(int radius)
    {
        if (Width <= 0 || Height <= 0) return;
        using var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(0, 0, d, d, 180, 90);
        path.AddArc(Width - d, 0, d, d, 270, 90);
        path.AddArc(Width - d, Height - d, d, d, 0, 90);
        path.AddArc(0, Height - d, d, d, 90, 90);
        path.CloseFigure();
        Region = new Region(path);
    }

    private void AttachDragHandlers(Control c)
    {
        c.MouseDown += OnDragMouseDown;
        c.MouseMove += OnDragMouseMove;
        c.MouseUp += OnDragMouseUp;
    }

    private void LoadNote()
    {
        Width = Math.Max(200, _data.Width);
        Height = Math.Max(140, _data.Height);

        _noteBox.Text = _data.Text;

        _offsetX = _data.OffsetX;
        _offsetY = _data.OffsetY;

        if (_data.IsBound && !string.IsNullOrEmpty(_data.BoundProcessName))
        {
            var hwnd = FindWindowByProcessAndTitle(
                _data.BoundProcessName!, _data.BoundWindowTitle ?? "");

            if (hwnd != IntPtr.Zero)
            {
                RestoreBinding(hwnd);
                UpdatePlaceholder();
                return;
            }

            _status.Text = "上次绑定的窗口未找到，已转为自由便签";
            _status.ForeColor = _palette.Muted;
            _data.IsBound = false;
        }

        Location = new Point(_data.FreeX, _data.FreeY);
        _bindDot.ForeColor = ColorIdle;
        _pinButton.Text = "📌";
        UpdatePlaceholder();
    }

    private void RestoreBinding(IntPtr target)
    {
        _targetHwnd = target;
        _lastRect = default;
        _winEventProc = WinEventCallback;

        _hookLocation = SetWinEventHook(
            EVENT_OBJECT_LOCATIONCHANGE, EVENT_OBJECT_LOCATIONCHANGE,
            IntPtr.Zero, _winEventProc, 0, 0, WINEVENT_OUTOFCONTEXT);

        _hookDestroy = SetWinEventHook(
            EVENT_OBJECT_DESTROY, EVENT_OBJECT_DESTROY,
            IntPtr.Zero, _winEventProc, 0, 0, WINEVENT_OUTOFCONTEXT);

        if (!GetWindowRect(target, out var rect)) return;
        _lastRect = rect;

        SetWindowPos(Handle, HWND_TOPMOST,
            rect.Left + _offsetX, rect.Top + _offsetY, 0, 0,
            SWP_NOSIZE | SWP_NOACTIVATE);

        _bindDot.ForeColor = ColorAccent;
        _pinButton.Text = "📍";
        _status.Text = $"已绑定: {GetWindowTitle(target)}";
        _status.ForeColor = ColorAccent;
    }

    private void ScheduleSave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SaveNow()
    {
        _data.Text = _noteBox.Text;
        _data.Width = Width;
        _data.Height = Height;
        _data.UpdatedAt = DateTime.Now;

        if (_targetHwnd != IntPtr.Zero)
        {
            _data.IsBound = true;
            _data.BoundProcessName = GetProcessName(_targetHwnd);
            _data.BoundWindowTitle = GetRawWindowTitle(_targetHwnd);
            _data.OffsetX = _offsetX;
            _data.OffsetY = _offsetY;
            _data.FreeX = Location.X;
            _data.FreeY = Location.Y;
        }
        else
        {
            _data.IsBound = false;
            _data.BoundProcessName = null;
            _data.BoundWindowTitle = null;
            _data.FreeX = Location.X;
            _data.FreeY = Location.Y;
        }

        NoteStorage.Upsert(_data);
    }

    private void OnPinButtonClick(object? sender, EventArgs e)
    {
        if (_targetHwnd != IntPtr.Zero)
        {
            DetachFromTarget();
            return;
        }

        _isSelectingTarget = true;
        _status.Text = "点击任意窗口完成钉选…";
        _status.ForeColor = ColorAccent;
        Capture = true;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (!_isSelectingTarget) return;

        _isSelectingTarget = false;
        Capture = false;

        var screenPoint = PointToScreen(e.Location);
        var hwnd = WindowFromPoint(screenPoint);

        if (hwnd == IntPtr.Zero || hwnd == Handle)
        {
            _status.Text = "自由便签 · 拖动顶栏移动";
            _status.ForeColor = _palette.Muted;
            return;
        }

        AttachToTarget(hwnd);
    }

    private void AttachToTarget(IntPtr target)
    {
        _targetHwnd = target;
        _lastRect = default;
        _winEventProc = WinEventCallback;

        _hookLocation = SetWinEventHook(
            EVENT_OBJECT_LOCATIONCHANGE, EVENT_OBJECT_LOCATIONCHANGE,
            IntPtr.Zero, _winEventProc, 0, 0, WINEVENT_OUTOFCONTEXT);

        _hookDestroy = SetWinEventHook(
            EVENT_OBJECT_DESTROY, EVENT_OBJECT_DESTROY,
            IntPtr.Zero, _winEventProc, 0, 0, WINEVENT_OUTOFCONTEXT);

        if (!GetWindowRect(target, out var rect)) return;

        int initialX = rect.Right - Width - 8;
        int initialY = rect.Top + 8;

        _offsetX = initialX - rect.Left;
        _offsetY = initialY - rect.Top;

        SetWindowPos(Handle, HWND_TOPMOST, initialX, initialY, 0, 0,
            SWP_NOSIZE | SWP_NOACTIVATE);

        _lastRect = rect;

        _bindDot.ForeColor = ColorAccent;
        _pinButton.Text = "📍";
        _status.Text = $"已绑定: {GetWindowTitle(target)}";
        _status.ForeColor = ColorAccent;

        SaveNow();
    }

    private void DetachFromTarget()
    {
        if (_hookLocation != IntPtr.Zero)
        {
            UnhookWinEvent(_hookLocation);
            _hookLocation = IntPtr.Zero;
        }
        if (_hookDestroy != IntPtr.Zero)
        {
            UnhookWinEvent(_hookDestroy);
            _hookDestroy = IntPtr.Zero;
        }

        _targetHwnd = IntPtr.Zero;
        _lastRect = default;
        _isDragging = false;
        _isCommittingDrag = false;

        _bindDot.ForeColor = ColorIdle;
        _pinButton.Text = "📌";
        _status.Text = "自由便签 · 拖动顶栏移动";
        _status.ForeColor = _palette.Muted;

        SaveNow();
    }

    private void WinEventCallback(IntPtr hWinEventHook, uint eventType,
        IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (_targetHwnd == IntPtr.Zero) return;

        if (eventType == EVENT_OBJECT_LOCATIONCHANGE)
        {
            if (idObject != OBJID_WINDOW) return;
            if (hwnd != _targetHwnd) return;
            SyncToTarget();
        }
        else if (eventType == EVENT_OBJECT_DESTROY)
        {
            if (hwnd != _targetHwnd) return;

            BeginInvoke(new Action(() =>
            {
                DetachFromTarget();
                _status.Text = "目标窗口已关闭，已解绑";
            }));
        }
    }

    private void SyncToTarget()
    {
        if (_targetHwnd == IntPtr.Zero) return;
        if (_isDragging || _isCommittingDrag) return;
        if (!GetWindowRect(_targetHwnd, out var rect)) return;
        if (rect.Equals(_lastRect)) return;
        _lastRect = rect;

        SetWindowPos(Handle, HWND_TOPMOST,
            rect.Left + _offsetX, rect.Top + _offsetY, 0, 0,
            SWP_NOSIZE | SWP_NOACTIVATE);
    }

    private void OnDragMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        if (_isSelectingTarget) return;

        _isDragging = true;
        _dragStartMouse = Cursor.Position;
        _dragStartLocation = Location;
    }

    private void OnDragMouseMove(object? sender, MouseEventArgs e)
    {
        if (!_isDragging) return;

        var current = Cursor.Position;
        Location = new Point(
            _dragStartLocation.X + (current.X - _dragStartMouse.X),
            _dragStartLocation.Y + (current.Y - _dragStartMouse.Y));
    }

    private void OnDragMouseUp(object? sender, MouseEventArgs e)
    {
        if (!_isDragging) return;

        if (_targetHwnd != IntPtr.Zero)
            CommitDrag();

        _isDragging = false;
        SaveNow();
    }

    private void CommitDrag()
    {
        if (_targetHwnd == IntPtr.Zero) return;

        _isCommittingDrag = true;
        try
        {
            if (!GetWindowRect(_targetHwnd, out var rect)) return;

            _offsetX = Location.X - rect.Left;
            _offsetY = Location.Y - rect.Top;

            SetWindowPos(Handle, HWND_TOPMOST,
                rect.Left + _offsetX, rect.Top + _offsetY, 0, 0,
                SWP_NOSIZE | SWP_NOACTIVATE);

            _lastRect = rect;
        }
        finally
        {
            _isCommittingDrag = false;
        }
    }

    public void CloseFromList()
    {
        _saveTimer.Stop();
        SaveNow();
        DetachFromTarget();
        Close();
    }

    private static IntPtr FindWindowByProcessAndTitle(string processName, string title)
    {
        IntPtr found = IntPtr.Zero;

        EnumWindows((h, _) =>
        {
            if (!IsWindowVisible(h)) return true;

            GetWindowThreadProcessId(h, out var pid);
            try
            {
                var proc = Process.GetProcessById((int)pid);
                if (!proc.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            catch { return true; }

            var raw = GetRawWindowTitle(h);
            if (raw.Length == 0) return true;

            if (raw.Contains(title, StringComparison.OrdinalIgnoreCase))
            {
                found = h;
                return false;
            }
            return true;
        }, IntPtr.Zero);

        return found;
    }

    private static string GetRawWindowTitle(IntPtr hwnd)
    {
        var sb = new StringBuilder(512);
        GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static string GetProcessName(IntPtr hwnd)
    {
        try
        {
            var pid = GetWindowProcessId(hwnd);
            return Process.GetProcessById((int)pid).ProcessName;
        }
        catch { return ""; }
    }

    private static string GetWindowTitle(IntPtr hwnd)
    {
        try
        {
            var pid = GetWindowProcessId(hwnd);
            var proc = Process.GetProcessById((int)pid);
            var title = proc.MainWindowTitle;
            return title.Length > 20 ? title.Substring(0, 20) + "…" : title;
        }
        catch { return "目标窗口"; }
    }

    private static uint GetWindowProcessId(IntPtr hwnd)
    {
        GetWindowThreadProcessId(hwnd, out var pid);
        return pid;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _saveTimer.Stop();
        SaveNow();
        DetachFromTarget();
        base.OnFormClosing(e);
    }
}
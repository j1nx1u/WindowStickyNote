using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

using Timer = System.Windows.Forms.Timer;

namespace WindowStickyNote;

public class NoteListForm : Form
{
    // 列表背景色 / List background color.
    private static readonly Color BgColor = Color.FromArgb(246, 247, 249);
    // 主文字色 / Primary text color.
    private static readonly Color TextColor = Color.FromArgb(38, 40, 46);
    // 次要文字色 / Secondary text color.
    private static readonly Color SubTextColor = Color.FromArgb(142, 146, 156);

    // 操作区总宽度（左右两个按钮的总和）/ Total width of the action area.
    private const int ActionWidth = 120;
    // 卡片高度 / Card height.
    private const int CardHeight = 92;
    // 卡片圆角半径（胶囊化）/ Card corner radius (capsule style).
    private const int CardRadius = 24;

    private FlowLayoutPanel _list = null!;
    private Panel _emptyPanel = null!;
    private Label _statusLabel = null!;
    private readonly Dictionary<string, Form1> _openWindows = new();
    private SwipeCard? _activeCard;
    private readonly Timer _resizeTimer = new() { Interval = 120 };

    public NoteListForm()
    {
        Text = "随手记";
        Size = new Size(440, 640);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = BgColor;
        Font = new Font("Microsoft YaHei UI", 9.5f);
        DoubleBuffered = true;
        MinimumSize = new Size(340, 450);

        // 提取内嵌的图标作为软件图标 / Extract embedded icon.
        try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        _resizeTimer.Tick += (_, _) =>
        {
            _resizeTimer.Stop();
            if (WindowState != FormWindowState.Minimized)
                ReloadList();
        };

        BuildUi();
        ReloadList();
    }

    private void BuildUi()
    {
        // ================= 顶部头部区 / Header =================
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 84,
            BackColor = Color.White,
        };
        header.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(234, 235, 238), 1);
            e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
        };

        var title = new Label
        {
            Text = "随手记",
            Font = new Font("Microsoft YaHei UI", 18f, FontStyle.Bold),
            ForeColor = TextColor,
            AutoSize = true,
            Location = new Point(24, 16),
        };

        var subtitle = new Label
        {
            Text = "让灵感跟随窗口",
            Font = new Font("Microsoft YaHei UI", 9f),
            ForeColor = SubTextColor,
            AutoSize = true,
            Location = new Point(26, 50),
        };

        var newBtn = new Button
        {
            Text = "＋ 新建便签",
            Size = new Size(100, 38),
            Location = new Point(320, 22),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(72, 176, 116),
            ForeColor = Color.White,
            Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        newBtn.FlatAppearance.BorderSize = 0;
        newBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(62, 162, 104);
        newBtn.FlatAppearance.MouseDownBackColor = Color.FromArgb(54, 148, 94);
        newBtn.Click += (_, _) => CreateNewNote();

        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        header.Controls.Add(newBtn);
        header.Resize += (_, _) =>
            newBtn.Location = new Point(header.Width - newBtn.Width - 24, 22);

        // ================= 底部状态栏 / Footer =================
        var footer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 36,
            BackColor = Color.White,
        };
        footer.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(234, 235, 238), 1);
            e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
        };

        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "共 0 条便签",
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(24, 0, 0, 0),
            Font = new Font("Microsoft YaHei UI", 8.5f),
            ForeColor = SubTextColor,
        };
        footer.Controls.Add(_statusLabel);

        // ================= 列表区 / List =================
        _list = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(20, 16, 20, 16),
            BackColor = BgColor,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
        };
        _list.MouseDown += (_, _) => CloseActiveCard();

        // ================= 空状态提示 / Empty State =================
        _emptyPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BgColor,
            Visible = false,
        };

        var emptyIcon = new Label
        {
            Text = "📝",
            Font = new Font("Segoe UI Emoji", 48f),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(200, 80),
            Location = new Point(120, 160),
            ForeColor = Color.FromArgb(200, 205, 215),
        };

        var emptyTitle = new Label
        {
            Text = "这里空空如也",
            Font = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold),
            ForeColor = TextColor,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(200, 30),
            Location = new Point(120, 250),
        };

        var emptyHint = new Label
        {
            Text = "点击右上角“新建便签”\n记录你的第一条灵感",
            Font = new Font("Microsoft YaHei UI", 9.5f),
            ForeColor = SubTextColor,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(200, 50),
            Location = new Point(120, 285),
        };

        _emptyPanel.Controls.Add(emptyIcon);
        _emptyPanel.Controls.Add(emptyTitle);
        _emptyPanel.Controls.Add(emptyHint);
        
        _emptyPanel.Resize += (_, _) =>
        {
            emptyIcon.Left = (_emptyPanel.Width - emptyIcon.Width) / 2;
            emptyTitle.Left = (_emptyPanel.Width - emptyTitle.Width) / 2;
            emptyHint.Left = (_emptyPanel.Width - emptyHint.Width) / 2;
        };

        Controls.Add(_emptyPanel);
        Controls.Add(_list);
        Controls.Add(footer);
        Controls.Add(header);

        Resize += (_, _) =>
        {
            _resizeTimer.Stop();
            _resizeTimer.Start();
        };
    }

    public void ReloadList()
    {
        _activeCard = null;
        _list.Controls.Clear();
        var notes = NoteStorage.LoadAll();
        notes.Sort((a, b) => b.UpdatedAt.CompareTo(a.UpdatedAt));

        _statusLabel.Text = $"共 {notes.Count} 条便签";

        if (notes.Count == 0)
        {
            _emptyPanel.Visible = true;
            _emptyPanel.BringToFront();
            return;
        }

        _emptyPanel.Visible = false;
        
        foreach (var note in notes)
            _list.Controls.Add(BuildCard(note));
    }

    private Control BuildCard(NoteData note)
    {
        var palette = NoteColors.Get(note.ColorKey);
        int cardWidth = _list.ClientSize.Width - 52;

        var container = new Panel
        {
            Width = cardWidth,
            Height = CardHeight,
            BackColor = BgColor,
            Margin = new Padding(0, 0, 0, 12),
        };

        var slider = new Panel
        {
            Left = 0,
            Top = 0,
            Width = cardWidth + ActionWidth,
            Height = CardHeight,
            BackColor = BgColor,
        };

        var card = new RoundedPanel
        {
            Left = 0,
            Top = 0,
            Width = cardWidth,
            Height = CardHeight,
            BackColor = Color.White,
            Radius = CardRadius,
            IsLeftRoundedOnly = true,
            Cursor = Cursors.Hand,
        };

        var stripe = new Panel
        {
            Left = 0,
            Top = 0,
            Width = 6,
            Height = CardHeight,
            BackColor = Color.Transparent,
        };
        stripe.Paint += (_, e) =>
        {
            var rect = new Rectangle(0, 0, stripe.Width, stripe.Height);
            using var brush = new LinearGradientBrush(
                rect,
                Lighten(palette.Display, 30),
                palette.Display,
                LinearGradientMode.Vertical);
            e.Graphics.FillRectangle(brush, rect);
        };

        var titleLabel = new Label
        {
            Text = string.IsNullOrWhiteSpace(note.Text) ? "空白便签" : FirstLine(note.Text),
            Font = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold),
            ForeColor = TextColor,
            AutoSize = false,
            Location = new Point(24, 20),
            Size = new Size(cardWidth - 160, 26),
            BackColor = Color.Transparent,
        };

        var previewLabel = new Label
        {
            Text = BuildPreview(note),
            Font = new Font("Microsoft YaHei UI", 8.5f),
            ForeColor = SubTextColor,
            AutoSize = false,
            Location = new Point(24, 48),
            Size = new Size(cardWidth - 160, 22),
            BackColor = Color.Transparent,
        };

        var timeLabel = new Label
        {
            Text = note.UpdatedAt.ToString("MM-dd HH:mm"),
            Font = new Font("Microsoft YaHei UI", 8f),
            ForeColor = Color.FromArgb(178, 182, 190),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleRight,
            Location = new Point(cardWidth - 110, 24),
            Size = new Size(86, 20),
            BackColor = Color.Transparent,
        };

        var badge = new RoundedLabel
        {
            Text = note.IsBound ? "已绑定" : "自由",
            Font = new Font("Microsoft YaHei UI", 7.5f),
            ForeColor = note.IsBound ? Color.White : Color.FromArgb(120, 124, 132),
            BackColor = note.IsBound ? palette.Display : Color.FromArgb(238, 239, 242),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(cardWidth - 108, 50),
            Size = new Size(56, 20),
            Radius = 10,
        };

        card.Controls.Add(stripe);
        card.Controls.Add(titleLabel);
        card.Controls.Add(previewLabel);
        card.Controls.Add(timeLabel);
        card.Controls.Add(badge);

        var actions = new Panel
        {
            Left = cardWidth,
            Top = 0,
            Width = ActionWidth,
            Height = CardHeight,
            BackColor = BgColor,
        };

        var copyColor = Color.FromArgb(240, 200, 140);
        var deleteColor = Color.FromArgb(224, 128, 108);

        int halfWidth = ActionWidth / 2;
        
        var copyBtn = BuildActionButton("复制", copyColor, 0, halfWidth, RoundedSide.None);
        var deleteBtn = BuildActionButton("删除", deleteColor, halfWidth, halfWidth, RoundedSide.RightOnly);

        copyBtn.Click += (_, _) =>
        {
            CloseActiveCard();
            CopyNote(note);
        };
        deleteBtn.Click += (_, _) =>
        {
            CloseActiveCard();
            DeleteNote(note);
        };

        actions.Controls.Add(copyBtn);
        actions.Controls.Add(deleteBtn);

        slider.Controls.Add(card);
        slider.Controls.Add(actions);
        container.Controls.Add(slider);

        var swipe = new SwipeCard(container, slider, card, cardWidth, ActionWidth);
        swipe.OpenRequested += () => OpenNote(note.Id);
        swipe.Opened += () => SetActiveCard(swipe);

        foreach (Control c in card.Controls)
            swipe.Attach(c);
        swipe.Attach(card);

        return container;
    }

    private enum RoundedSide { None, LeftOnly, RightOnly, All }

    private static Button BuildActionButton(string text, Color color, int left, int width, RoundedSide side)
    {
        var b = new Button
        {
            Text = text,
            Size = new Size(width, CardHeight),
            Location = new Point(left, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = color,
            ForeColor = Color.FromArgb(90, 62, 40),
            Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            TabStop = false,
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = Darken(color, 10);
        b.FlatAppearance.MouseDownBackColor = Darken(color, 22);
        
        b.Paint += (_, _) =>
        {
            using var path = new GraphicsPath();
            int r = CardRadius;
            
            if (side == RoundedSide.RightOnly)
            {
                path.AddLine(0, 0, b.Width - r, 0);
                path.AddArc(b.Width - r, 0, r, r, 270, 90);
                path.AddArc(b.Width - r, b.Height - r, r, r, 0, 90);
                path.AddLine(b.Width - r, b.Height, 0, b.Height);
            }
            else
            {
                path.AddRectangle(b.ClientRectangle);
            }
            path.CloseFigure();
            b.Region = new Region(path);
        };
        return b;
    }

    private void SetActiveCard(SwipeCard card)
    {
        if (_activeCard != null && _activeCard != card)
            _activeCard.Close();
        _activeCard = card;
    }

    private void CloseActiveCard()
    {
        if (_activeCard == null) return;
        _activeCard.Close();
        _activeCard = null;
    }

    private void OpenNote(string id)
    {
        if (_openWindows.TryGetValue(id, out var existing) && !existing.IsDisposed)
        {
            existing.Activate();
            return;
        }

        var data = NoteStorage.LoadAll().Find(n => n.Id == id);
        if (data == null) return;

        var win = new Form1(data);
        win.FormClosed += (_, _) =>
        {
            _openWindows.Remove(id);
            ReloadList();
        };
        _openWindows[id] = win;
        win.Show();
    }

    private void CreateNewNote()
    {
        var data = new NoteData
        {
            Id = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
        };
        NoteStorage.Upsert(data);
        ReloadList();
        OpenNote(data.Id);
    }

    private void CopyNote(NoteData note)
    {
        var copy = new NoteData
        {
            Id = Guid.NewGuid().ToString("N"),
            Text = note.Text,
            ColorKey = note.ColorKey,
            SizeMode = note.SizeMode, // 保留原尺寸模式
            FreeX = note.FreeX + 24,
            FreeY = note.FreeY + 24,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            IsBound = false,
        };
        NoteStorage.Upsert(copy);
        ReloadList();
    }

    private void DeleteNote(NoteData note)
    {
        if (MessageBox.Show("确定删除这张便签吗？", "删除便签",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        if (_openWindows.TryGetValue(note.Id, out var win))
        {
            win.CloseFromList();
            _openWindows.Remove(note.Id);
        }
        NoteStorage.Delete(note.Id);
        ReloadList();
    }

    private static string BuildPreview(NoteData note)
    {
        if (note.IsBound && !string.IsNullOrWhiteSpace(note.BoundWindowTitle))
            return $"已钉在「{note.BoundWindowTitle}」";

        var text = note.Text.Replace("\r", "").Replace("\n", " ").Trim();
        if (text.Length == 0) return "暂无内容";
        return text.Length > 22 ? text[..22] + "…" : text;
    }

    private static string FirstLine(string text)
    {
        var idx = text.IndexOf('\n');
        var line = idx >= 0 ? text[..idx] : text;
        return line.Length > 20 ? line[..20] + "…" : line;
    }

    private static Color Lighten(Color c, int amount) => Color.FromArgb(
        Math.Min(255, c.R + amount),
        Math.Min(255, c.G + amount),
        Math.Min(255, c.B + amount));

    private static Color Darken(Color c, int amount) => Color.FromArgb(
        Math.Max(0, c.R - amount),
        Math.Max(0, c.G - amount),
        Math.Max(0, c.B - amount));
}

/// <summary>
/// 圆角面板（支持仅左侧圆角，用于拼接右侧操作区）
/// </summary>
internal class RoundedPanel : Panel
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    [DefaultValue(12)]
    public int Radius { get; set; } = 12;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    [DefaultValue(false)]
    public bool IsLeftRoundedOnly { get; set; } = false;

    public RoundedPanel()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint, true);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateRegion();
    }

    private void UpdateRegion()
    {
        if (Width <= 0 || Height <= 0) return;
        
        using var path = new GraphicsPath();
        
        if (IsLeftRoundedOnly)
        {
            int r = Radius;
            path.AddArc(0, 0, r, r, 180, 90);
            path.AddArc(0, Height - r, r, r, 90, 90);
            path.AddLine(0, Height - r, Width, Height);
            path.AddLine(Width, Height, Width, 0);
            path.CloseFigure();
        }
        else
        {
            using var roundedPath = NoteListFormHelper.RoundedRect(ClientRectangle, Radius);
            path.AddPath(roundedPath, false);
        }
        
        Region = new Region(path);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (IsLeftRoundedOnly) return; 

        using var pen = new Pen(Color.FromArgb(232, 233, 237), 1);
        using var path = NoteListFormHelper.RoundedRect(
            new Rectangle(0, 0, Width - 1, Height - 1), Radius);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.DrawPath(pen, path);
    }
}

internal class RoundedLabel : Label
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    [DefaultValue(10)]
    public int Radius { get; set; } = 10;

    public RoundedLabel()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint, true);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (Width <= 0 || Height <= 0) return;
        using var path = NoteListFormHelper.RoundedRect(ClientRectangle, Radius);
        Region = new Region(path);
    }
}

internal static class NoteListFormHelper
{
    public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal class SwipeCard
{
    private readonly Panel _container;
    private readonly Panel _slider;
    private readonly Panel _card;
    private readonly int _cardWidth;
    private readonly int _actionWidth;

    private bool _isDragging;
    private bool _isOpen;
    private int _dragStartX;
    private int _startLeft;

    private readonly Timer _animTimer;
    private int _target;

    public event Action? OpenRequested;
    public event Action? Opened;

    public SwipeCard(Panel container, Panel slider, Panel card, int cardWidth, int actionWidth)
    {
        _container = container;
        _slider = slider;
        _card = card;
        _cardWidth = cardWidth;
        _actionWidth = actionWidth;

        _animTimer = new Timer { Interval = 15 };
        _animTimer.Tick += OnAnimTick;
    }

    public void Attach(Control c)
    {
        c.MouseDown += OnMouseDown;
        c.MouseMove += OnMouseMove;
        c.MouseUp += OnMouseUp;
    }

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;

        _isDragging = true;
        _dragStartX = Cursor.Position.X;
        _startLeft = _slider.Left;
        _animTimer.Stop();
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        if (!_isDragging) return;

        int dx = Cursor.Position.X - _dragStartX;
        int newLeft = _startLeft + dx;

        if (newLeft > 0) newLeft = 0;
        if (newLeft < -_actionWidth) newLeft = -_actionWidth;

        _slider.Left = newLeft;
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;

        int dx = Math.Abs(Cursor.Position.X - _dragStartX);

        if (dx < 8)
        {
            if (_isOpen) Close();
            else OpenRequested?.Invoke();
            return;
        }

        bool shouldOpen = _slider.Left < -_actionWidth / 2;
        if (shouldOpen)
        {
            _isOpen = true;
            AnimateTo(-_actionWidth);
            Opened?.Invoke();
        }
        else
        {
            _isOpen = false;
            AnimateTo(0);
        }
    }

    public void Close()
    {
        if (!_isOpen) return;
        _isOpen = false;
        AnimateTo(0);
    }

    private void AnimateTo(int target)
    {
        _animTimer.Stop();
        _target = target;
        _animTimer.Start();
    }

    private void OnAnimTick(object? sender, EventArgs e)
    {
        int diff = _target - _slider.Left;
        if (Math.Abs(diff) <= 12)
        {
            _slider.Left = _target;
            _animTimer.Stop();
            return;
        }
        _slider.Left += diff / 3;
    }
}
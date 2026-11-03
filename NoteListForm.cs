using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace WindowStickyNote;

public class NoteListForm : Form
{
    private static readonly Color BgColor = Color.FromArgb(248, 248, 250);
    private static readonly Color TextColor = Color.FromArgb(48, 48, 54);
    private static readonly Color MutedColor = Color.FromArgb(140, 140, 150);

    private FlowLayoutPanel _list = null!;
    private Label _emptyHint = null!;
    private readonly Dictionary<string, Form1> _openWindows = new();

    public NoteListForm()
    {
        Text = "便签";
        Size = new Size(420, 560);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = BgColor;
        Font = new Font("Microsoft YaHei UI", 9.5f);
        DoubleBuffered = true;
        MinimumSize = new Size(320, 360);

        BuildUi();
        ReloadList();
    }

    private void BuildUi()
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 64,
            BackColor = Color.White,
        };

        var title = new Label
        {
            Text = "我的便签",
            Font = new Font("Microsoft YaHei UI", 14f, FontStyle.Bold),
            ForeColor = TextColor,
            AutoSize = true,
            Location = new Point(20, 20),
        };

        var newBtn = new Button
        {
            Text = "＋ 新建便签",
            Size = new Size(110, 34),
            Location = new Point(280, 15),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(76, 175, 80),
            ForeColor = Color.White,
            Font = new Font("Microsoft YaHei UI", 9.5f),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        newBtn.FlatAppearance.BorderSize = 0;
        newBtn.Click += (_, _) => CreateNewNote();

        header.Controls.Add(title);
        header.Controls.Add(newBtn);
        header.Resize += (_, _) =>
            newBtn.Location = new Point(header.Width - newBtn.Width - 24, 15);

        _list = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(16),
            BackColor = BgColor,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
        };

        _emptyHint = new Label
        {
            Text = "还没有便签，点击右上角新建一张吧",
            ForeColor = MutedColor,
            Font = new Font("Microsoft YaHei UI", 10f),
            AutoSize = true,
        };

        Controls.Add(_list);
        Controls.Add(header);
    }

    public void ReloadList()
    {
        _list.Controls.Clear();
        var notes = NoteStorage.LoadAll();
        notes.Sort((a, b) => b.UpdatedAt.CompareTo(a.UpdatedAt));

        if (notes.Count == 0)
        {
            _list.Controls.Add(_emptyHint);
            return;
        }

        foreach (var note in notes)
            _list.Controls.Add(BuildCard(note));
    }

    private Control BuildCard(NoteData note)
    {
        var palette = NoteColors.Get(note.ColorKey);

        var card = new Panel
        {
            Width = _list.ClientSize.Width - 40,
            Height = 84,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 0, 10),
            Cursor = Cursors.Hand,
        };

        var stripe = new Panel
        {
            Dock = DockStyle.Left,
            Width = 8,
            BackColor = palette.Display,
        };

        var title = new Label
        {
            Text = string.IsNullOrWhiteSpace(note.Text) ? "空白便签" : FirstLine(note.Text),
            Font = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold),
            ForeColor = TextColor,
            AutoSize = false,
            Location = new Point(24, 16),
            Size = new Size(card.Width - 120, 24),
        };

        var subtitle = new Label
        {
            Text = note.IsBound
                ? $"已绑定 · {note.BoundWindowTitle}"
                : $"自由便签 · {note.UpdatedAt:MM-dd HH:mm}",
            Font = new Font("Microsoft YaHei UI", 8.5f),
            ForeColor = MutedColor,
            AutoSize = false,
            Location = new Point(24, 46),
            Size = new Size(card.Width - 120, 20),
        };

        var deleteBtn = new Button
        {
            Text = "删除",
            Size = new Size(56, 26),
            Location = new Point(card.Width - 70, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(200, 80, 80),
            Font = new Font("Microsoft YaHei UI", 8.5f),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        deleteBtn.FlatAppearance.BorderSize = 1;
        deleteBtn.FlatAppearance.BorderColor = Color.FromArgb(230, 200, 200);
        deleteBtn.Click += (_, _) =>
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
        };

        card.Controls.Add(title);
        card.Controls.Add(subtitle);
        card.Controls.Add(deleteBtn);
        card.Controls.Add(stripe);

        EventHandler open = (_, _) => OpenNote(note.Id);
        card.Click += open;
        title.Click += open;
        subtitle.Click += open;

        card.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(228, 228, 232), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        return card;
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

    private static string FirstLine(string text)
    {
        var idx = text.IndexOf('\n');
        var line = idx >= 0 ? text[..idx] : text;
        return line.Length > 24 ? line[..24] + "…" : line;
    }
}
using System;
using System.Windows.Forms;

namespace WindowStickyNote;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var list = new NoteListForm();

        if (args.Length > 0 && args[0] == "--new")
        {
            var data = new NoteData
            {
                Id = Guid.NewGuid().ToString("N"),
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
            };
            NoteStorage.Upsert(data);

            var note = new Form1(data);
            note.FormClosed += (_, _) => list.ReloadList();
            note.Show();
        }

        Application.Run(list);
    }
}
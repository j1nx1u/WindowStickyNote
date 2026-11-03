using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace WindowStickyNote;

public static class NoteStorage
{
    private static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WindowStickyNote");

    private static readonly string FilePath = Path.Combine(Dir, "notes.json");

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static List<NoteData> LoadAll()
    {
        try
        {
            if (!File.Exists(FilePath)) return new List<NoteData>();
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<List<NoteData>>(json, Options) ?? new List<NoteData>();
        }
        catch
        {
            return new List<NoteData>();
        }
    }

    public static void SaveAll(IEnumerable<NoteData> notes)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var json = JsonSerializer.Serialize(notes.ToList(), Options);
            File.WriteAllText(FilePath, json);
        }
        catch { }
    }

    public static void Upsert(NoteData note)
    {
        var all = LoadAll();
        var idx = all.FindIndex(n => n.Id == note.Id);
        if (idx >= 0) all[idx] = note;
        else all.Add(note);
        SaveAll(all);
    }

    public static void Delete(string id)
    {
        var all = LoadAll();
        all.RemoveAll(n => n.Id == id);
        SaveAll(all);
    }
}
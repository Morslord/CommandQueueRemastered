using System;
using System.IO;
using System.Linq;
using CommandQueueRemastered.Core;

int passed = 0;
void Check(string name, Action action)
{
    try { action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception exception)
    {
        Console.Error.WriteLine("FAIL " + name + ": " + exception);
        Environment.Exit(1);
    }
}
void Assert(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
void Throws(Action action) { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Assert(rejected); }

Check("Tier heads stay separate within the shared Void display", () =>
{
    var queue = new QueueState(); queue.Enqueue("void-white", 2); queue.Enqueue("void-red"); queue.Enqueue("void-white", 3);
    string Tier(string name) => name == "void-red" ? "VoidTier3" : "VoidTier1";
    Assert(queue.FindHead("VoidTier3", Tier) == 1 && queue.FindHead("VoidTier1", Tier) == 0);
    queue.ToggleRepeat("VoidTier1"); queue.Consume(0, "VoidTier1");
    Assert(queue.Entries[0].Count == 1 && queue.Entries[2].Count == 4);
    Assert(queue.Entries.Sum(entry => entry.Count) == 6);
});
Check("Invalid replacement leaves the live queue and repeat flags untouched", () =>
{
    var queue = new QueueState(); queue.Enqueue("a", 4); queue.ToggleRepeat("Tier1");
    Throws(() => queue.Replace(new[] { new QueueEntry("b", 2), new QueueEntry("c", 0) }));
    Assert(queue.Entries.Single().Pickup == "a" && queue.Entries.Single().Count == 4 && queue.IsRepeated("Tier1"));
});
Check("Stack limits reject input rather than silently losing queued items", () =>
{
    var queue = new QueueState(); queue.Enqueue("a", 999); Throws(() => queue.Enqueue("a"));
    Assert(queue.Entries.Single().Count == 999);
});
Check("Repeat preserves one item across a full tail stack", () =>
{
    var queue = new QueueState(); queue.Replace(new[] { new QueueEntry("a", 1), new QueueEntry("b", 1), new QueueEntry("a", 999) });
    queue.ToggleRepeat("Tier1"); queue.Consume(0, "Tier1");
    Assert(queue.Entries.Sum(entry => entry.Count) == 1001 && queue.Entries.Last().Count == 1);
});
Check("Rotating repeat merges the displayed tail across hidden groups immediately", () =>
{
    string Group(string pickup) => pickup == "green" ? "Green" : "White";
    var queue = new QueueState(Group);
    queue.Replace(new[] { new QueueEntry("steak", 8), new QueueEntry("crowbar", 10),
        new QueueEntry("steak", 1), new QueueEntry("green", 5) }, new[] { "Tier1" });
    int changes = 0;
    queue.Changed += () =>
    {
        changes++;
        // Observers must receive only the final state, with the repeated item restored.
        Assert(queue.Entries.Sum(entry => entry.Count) == 24);
    };
    queue.Consume(0, "Tier1");
    Assert(changes == 1);
    var white = queue.Entries.Where(entry => Group(entry.Pickup) == "White").ToArray();
    Assert(white.Select(entry => entry.Pickup).SequenceEqual(new[] { "steak", "crowbar", "steak" }));
    Assert(white.Select(entry => entry.Count).SequenceEqual(new[] { 7, 10, 2 }));
    queue.Consume(0, "Tier1");
    Assert(changes == 2 && queue.Entries.Where(entry => entry.Pickup == "steak").Select(entry => entry.Count).SequenceEqual(new[] { 6, 3 }));
    Assert(queue.Entries.Single(entry => entry.Pickup == "green").Count == 5);
});
Check("Rotating repeat fills a displayed tail stack without losing overflow", () =>
{
    var queue = new QueueState(pickup => pickup == "green" ? "Green" : "White");
    queue.Replace(new[] { new QueueEntry("steak", 2), new QueueEntry("crowbar", 1),
        new QueueEntry("steak", 998), new QueueEntry("green", 3) }, new[] { "Tier1" });
    queue.Consume(0, "Tier1");
    Assert(queue.Entries.Where(entry => entry.Pickup == "steak").Select(entry => entry.Count).SequenceEqual(new[] { 1, 999 }));
    queue.Consume(0, "Tier1");
    Assert(queue.Entries.Where(entry => entry.Pickup == "steak").Select(entry => entry.Count).SequenceEqual(new[] { 999, 1 }));
    Assert(queue.Entries.Sum(entry => entry.Count) == 1004 && queue.Entries.All(entry => entry.Count <= QueueState.MaxCount));
});
Check("Reordering moves the selected stack and removal respects count", () =>
{
    var queue = new QueueState(); queue.Enqueue("a", 2); queue.Enqueue("b", 3); queue.Enqueue("c");
    queue.Move(0, 2); Assert(queue.Entries.Select(entry => entry.Pickup).SequenceEqual(new[] { "b", "c", "a" }));
    queue.Remove(2, false); Assert(queue.Entries[2].Count == 1); queue.Remove(0, true); Assert(queue.Entries.Count == 2);
});
Check("Snapshot and versioned JSON isolate preset contents", () =>
{
    var queue = new QueueState(); queue.Enqueue("a", 9); queue.ToggleRepeat("VoidTier1");
    var saved = QueueDocument.Capture(queue); queue.Remove(0, false);
    var decoded = QueueDocument.Decode(saved.Encode()); decoded.Apply(queue);
    Assert(queue.Entries.Single().Count == 9 && queue.IsRepeated("VoidTier1"));
    bool rejected = false;
    try { QueueDocument.Decode("{\"Version\":3,\"Queue\":[],\"RepeatedTiers\":[]}"); } catch (InvalidDataException) { rejected = true; }
    Assert(rejected);
});
Check("Preset create, reload, rename and delete round trip on disk", () =>
{
    string directory = Path.Combine(Path.GetTempPath(), "commandqueue-check-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try
    {
        string path = Path.Combine(directory, "presets.json");
        var store = new PresetStore(path); var queue = new QueueState(); queue.Enqueue("a", 9);
        store.Add("Test", queue); queue.Clear(); Assert(store.Presets[0].Document.Queue[0].Count == 9);
        store = new PresetStore(path); store.Load(); string id = store.Presets.Single().Id;
        store.Rename(id, "Renamed"); store = new PresetStore(path); store.Load(); Assert(store.Presets.Single().Name == "Renamed");
        store.Delete(id); store = new PresetStore(path); store.Load(); Assert(store.Presets.Count == 0);
    }
    finally
    {
        // Delete only files created by this test; never recursively remove a computed directory.
        foreach (string file in new[] { "presets.json", "presets.json.bak", "presets.json.tmp" })
            if (File.Exists(Path.Combine(directory, file))) File.Delete(Path.Combine(directory, file));
        Directory.Delete(directory);
    }
});
void WithImportFiles(Action<string, string> action)
{
    string directory = Path.Combine(Path.GetTempPath(), "commandqueue-import-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try { action(Path.Combine(directory, "presets.json"), Path.Combine(directory, "commandQueueSaves.json")); }
    finally
    {
        foreach (string file in new[] { "presets.json", "presets.json.bak", "presets.json.tmp", "commandQueueSaves.json" })
        {
            string path = Path.Combine(directory, file);
            if (File.Exists(path)) File.Delete(path);
        }
        Directory.Delete(directory);
    }
}
Check("User legacy presets import once, preserve existing saves and never return after deletion", () =>
    WithImportFiles((path, source) =>
    {
        string original = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "legacy-presets.txt"));
        File.WriteAllText(source, original);
        DateTime modified = File.GetLastWriteTimeUtc(source);
        // Exercise an existing 0.3.1 array file, not just a new empty store.
        File.WriteAllText(path, "[{\"Id\":\"existing\",\"Name\":\"Multi\",\"Document\":{\"Version\":1,\"Queue\":[],\"RepeatedTiers\":[]}}]");
        var store = new PresetStore(path); store.Load();
        Assert(store.ImportLegacy(source) == 4 && store.Presets.Count == 5);
        Assert(File.ReadAllText(source) == original && File.GetLastWriteTimeUtc(source) == modified);
        var imported = store.Presets.Skip(1).ToArray();
        Assert(imported.Select(p => p.Name).SequenceEqual(new[] { "Engi Repeat", "Multi", "Movespeed", "100% Crit" }));
        Assert(imported[0].Document.Queue.Select(e => e.Pickup).SequenceEqual(new[] { "ItemIndex.Mushroom", "ItemIndex.FlatHealth", "ItemIndex.ChainLightning" }));
        Assert(imported[0].Document.Queue.Select(e => e.Count).SequenceEqual(new[] { 2, 1, 1 }));
        Assert(imported[1].Document.Queue.Count == 18 && imported[1].Document.Queue.Sum(e => e.Count) == 37);
        Assert(imported.All(p => p.Document.RepeatedTiers.Count == 0));
        foreach (var preset in imported) store.Delete(preset.Id);
        store = new PresetStore(path); store.Load();
        File.AppendAllText(source, "\nnew|Later|ItemIndex.Hoof*1,|1");
        Assert(store.ImportLegacy(source) == 0 && store.Presets.Single().Id == "existing");
        Assert(File.ReadAllText(source).StartsWith(original, StringComparison.Ordinal));
    }));
Check("Missing or invalid legacy input can retry without partial writes", () =>
    WithImportFiles((path, source) =>
    {
        var store = new PresetStore(path);
        Assert(store.ImportLegacy(source) == 0 && !File.Exists(path));
        var queue = new QueueState(); queue.Enqueue("existing", 2); store.Add("Existing", queue);
        string before = File.ReadAllText(path);
        File.WriteAllText(source, "a|Valid|ItemIndex.Hoof*2,|1\nb|Invalid|ItemIndex.Mushroom*0,|1");
        bool failed = false;
        try { store.ImportLegacy(source); } catch (InvalidDataException) { failed = true; }
        Assert(failed && store.Presets.Count == 1 && File.ReadAllText(path) == before);
        File.WriteAllText(source, "a|Valid|ItemIndex.Hoof*1001,ItemIndex.MissingModItem*3,|1");
        string sourceBefore = File.ReadAllText(source); DateTime modified = File.GetLastWriteTimeUtc(source);
        Assert(store.ImportLegacy(source) == 1);
        Assert(File.ReadAllText(source) == sourceBefore && File.GetLastWriteTimeUtc(source) == modified);
        Assert(store.Presets.Last().Document.Queue.Select(e => e.Count).SequenceEqual(new[] { 999, 2, 3 }));
        store = new PresetStore(path); store.Load(); Assert(store.ImportLegacy(source) == 0);
    }));
Check("Failed import writes retain memory and allow a successful retry", () =>
    WithImportFiles((path, source) =>
    {
        File.WriteAllText(source, "a|Legacy|ItemIndex.Hoof*1,|1");
        var store = new PresetStore(path);
        Directory.CreateDirectory(path + ".tmp");
        bool failed = false;
        try
        {
            try { store.ImportLegacy(source); } catch (UnauthorizedAccessException) { failed = true; }
            Assert(failed && store.Presets.Count == 0 && !File.Exists(path));
        }
        finally { Directory.Delete(path + ".tmp"); }
        Assert(store.ImportLegacy(source) == 1);
        store = new PresetStore(path); store.Load(); Assert(store.ImportLegacy(source) == 0);
    }));
Check("Empty legacy files complete once and corrupt destinations are protected", () =>
    WithImportFiles((path, source) =>
    {
        File.WriteAllText(source, "\n");
        var store = new PresetStore(path); Assert(store.ImportLegacy(source) == 0);
        store = new PresetStore(path); store.Load();
        File.WriteAllText(source, "a|Later|ItemIndex.Hoof*1,|1");
        Assert(store.ImportLegacy(source) == 0 && store.Presets.Count == 0);
        File.WriteAllText(path, "{\"Version\":999,\"Presets\":[]}");
        bool failed = false;
        try { new PresetStore(path).Load(); } catch (InvalidDataException) { failed = true; }
        Assert(failed && File.ReadAllText(path).Contains("999"));
    }));
Check("Left-click increases the selected stack; Shift adds ten", () =>
{
    var queue = new QueueState(); queue.Enqueue("a", 3);
    queue.Increase(0, QueueInteraction.ClickCount(false)); Assert(queue.Entries[0].Count == 4);
    queue.Increase(0, QueueInteraction.ClickCount(true)); Assert(queue.Entries[0].Count == 14);
    queue.Replace(new[] { new QueueEntry("a", 999) }); Throws(() => queue.Increase(0)); Assert(queue.Entries[0].Count == 999);
});
Check("Ctrl-click prepends within the displayed group and keeps other groups", () =>
{
    string Group(string name) => name == "green" ? "Green" : "White";
    var queue = new QueueState(Group); queue.Enqueue("a", 2); queue.Enqueue("green", 3); queue.Enqueue("b");
    queue.Enqueue("b", 10, true);
    Assert(queue.Entries[0].Pickup == "b" && queue.Entries[0].Count == 10);
    Assert(queue.Entries.Where(entry => Group(entry.Pickup) == "White").Select(entry => entry.Pickup).SequenceEqual(new[] { "b", "a", "b" }));
    Assert(queue.Entries.Single(entry => entry.Pickup == "green").Count == 3);
});
Check("Whole-stack dragging merges neighbors on both sides", () =>
{
    var queue = new QueueState(); queue.Replace(new[] { new QueueEntry("a", 2), new QueueEntry("b", 1), new QueueEntry("a", 3) });
    queue.Move(1, 2);
    Assert(queue.Entries.Count == 2 && queue.Entries[0].Pickup == "a" && queue.Entries[0].Count == 5 && queue.Entries[1].Pickup == "b");
});
Check("Shift-drag moves the rounded-down half and preserves the total", () =>
{
    var queue = new QueueState(); queue.Enqueue("a", 5); queue.Enqueue("b", 2);
    queue.Move(0, 1, QueueInteraction.DragCount(5, true, false));
    Assert(queue.Entries.Select(entry => entry.Count).SequenceEqual(new[] { 3, 2, 2 }));
    Assert(queue.Entries.Sum(entry => entry.Count) == 7 && QueueInteraction.DragCount(1, true, false) == 0);
});
Check("Ctrl-drag moves one item; Ctrl takes priority over Shift", () =>
{
    var queue = new QueueState(); queue.Enqueue("a", 5); queue.Enqueue("b", 2);
    queue.Move(0, 1, QueueInteraction.DragCount(5, true, true));
    Assert(queue.Entries.Select(entry => entry.Count).SequenceEqual(new[] { 4, 2, 1 }));
});
Check("Merging across hidden groups preserves their entries and all quantities", () =>
{
    string Group(string name) => name == "green" ? "Green" : "White";
    var queue = new QueueState(Group);
    queue.Replace(new[] { new QueueEntry("a", 2), new QueueEntry("green", 9), new QueueEntry("b", 1), new QueueEntry("a", 3) });
    queue.Move(2, 3);
    Assert(queue.Entries.Single(entry => entry.Pickup == "a").Count == 5);
    Assert(queue.Entries.Single(entry => entry.Pickup == "green").Count == 9 && queue.Entries.Sum(entry => entry.Count) == 15);
});
Check("A merge exceeding a full stack never drops items", () =>
{
    var queue = new QueueState(); queue.Replace(new[] { new QueueEntry("a", 600), new QueueEntry("b", 1), new QueueEntry("a", 600) });
    queue.Move(1, 2);
    Assert(queue.Entries.Where(entry => entry.Pickup == "a").Sum(entry => entry.Count) == 1200);
    Assert(queue.Entries.All(entry => entry.Count <= QueueState.MaxCount));
});
Check("Green-only preset replaces Green and preserves White and its repeat mode", () =>
{
    string Group(string name) => name.StartsWith("g") ? "Green" : "White";
    string RepeatGroup(string tier) => tier == "Tier2" ? "Green" : "White";
    var queue = new QueueState(Group); queue.Enqueue("white", 4); queue.Enqueue("green-old", 3); queue.ToggleRepeat("Tier1");
    var saved = new QueueDocument { Queue = new() { new QueueEntry("green-new", 9) }, RepeatedTiers = new() { "Tier2" } };
    saved.ApplyGroups(queue, Group, RepeatGroup);
    Assert(queue.Entries.Single(entry => entry.Pickup == "white").Count == 4);
    Assert(queue.Entries.Single(entry => entry.Pickup == "green-new").Count == 9);
    Assert(!queue.Entries.Any(entry => entry.Pickup == "green-old") && queue.IsRepeated("Tier1") && queue.IsRepeated("Tier2"));
});
Check("Empty and unresolvable preset groups do not erase live queues", () =>
{
    var queue = new QueueState(); queue.Enqueue("white", 4);
    new QueueDocument().ApplyGroups(queue, _ => "White", _ => "White"); Assert(queue.Entries[0].Count == 4);
    Throws(() => queue.ReplaceGroups(new[] { new QueueEntry("unknown", 1) }, Array.Empty<string>(), _ => null, _ => "White"));
    Assert(queue.Entries.Single().Pickup == "white" && queue.Entries.Single().Count == 4);
});
Check("Insertion markers distinguish dropping before and after a target", () =>
{
    var queue = new QueueState();
    queue.Enqueue("a"); queue.Enqueue("b"); queue.Enqueue("c");
    queue.MoveToBoundary(0, 2); // Before c, after removing a.
    Assert(queue.Entries.Select(entry => entry.Pickup).SequenceEqual(new[] { "b", "a", "c" }));
    queue.MoveToBoundary(1, 3); // After c.
    Assert(queue.Entries.Select(entry => entry.Pickup).SequenceEqual(new[] { "b", "c", "a" }));
    queue.MoveToBoundary(2, 0); // Before the first row.
    Assert(queue.Entries.Select(entry => entry.Pickup).SequenceEqual(new[] { "a", "b", "c" }));
});
Check("Partial insertion at the final marker preserves source remainder and total", () =>
{
    var queue = new QueueState(); queue.Enqueue("a", 5); queue.Enqueue("b", 2); queue.Enqueue("c", 1);
    queue.MoveToBoundary(0, queue.Entries.Count, 2);
    Assert(queue.Entries.Select(entry => entry.Count).SequenceEqual(new[] { 3, 2, 1, 2 }));
    Assert(queue.Entries.Last().Pickup == "a" && queue.Entries.Sum(entry => entry.Count) == 8);
    Throws(() => queue.MoveToBoundary(0, 99)); Assert(queue.Entries.Sum(entry => entry.Count) == 8);
});
Check("Shift-right removes ten or the remaining smaller stack", () =>
{
    var queue = new QueueState(); queue.Enqueue("a", 14);
    queue.RemoveQuantity(0, QueueInteraction.DecreaseCount(14, true, false));
    Assert(queue.Entries.Single().Count == 4);
    queue.RemoveQuantity(0, QueueInteraction.DecreaseCount(4, true, false)); Assert(queue.Entries.Count == 0);
});
Check("Ctrl-right removes the stack and has priority over Shift", () =>
{
    var queue = new QueueState(); queue.Enqueue("a", 30); queue.Enqueue("b", 2);
    queue.RemoveQuantity(0, QueueInteraction.DecreaseCount(30, true, true));
    Assert(queue.Entries.Count == 1 && queue.Entries[0].Pickup == "b" && queue.Entries[0].Count == 2);
});
Check("Plain right-click removes one and removing a separator merges neighbors", () =>
{
    Assert(QueueInteraction.DecreaseCount(20, false, false) == 1);
    var queue = new QueueState(); queue.Enqueue("a", 3); queue.Enqueue("b", 10); queue.Enqueue("a", 4);
    queue.RemoveQuantity(1, 10); Assert(queue.Entries.Single().Count == 7);
    Throws(() => queue.RemoveQuantity(0, 0)); Assert(queue.Entries.Single().Count == 7);
});
Check("LookingGlass text preserves content, colors and emphasis without oversized TMP tags", () =>
{
    string text = "<size=120%>Increases <style=cIsDamage><color=#FFAA00><b>attack</b></color></style> speed</size>";
    Assert(DescriptionFormatting.ForTmp(text) == "Increases <style=cIsDamage><color=#FFAA00><b>attack</b></color></style> speed");
    Assert(DescriptionFormatting.ForTmp("<SIZE=24><i>Text</i></SIZE>") == "<i>Text</i>");
});
Check("Native descriptions retain named color styles and nested emphasis", () =>
{
    string text = "<size=120%><style=cIsDamage>Attack <b>+10%</b></style> <color=#00FF00>health</color></size>";
    Assert(DescriptionFormatting.ForTmp(text) == "<style=cIsDamage>Attack <b>+10%</b></style> <color=#00FF00>health</color>");
    Assert(DescriptionFormatting.ForTmp(null) == "");
});
Check("Descriptions remove no-wrap and width overrides while retaining colors and line breaks", () =>
{
    string text = "<nobr><width=200%><pos=500><style=cIsDamage>Long <color=#FFAA00>description</color></style></pos></width></nobr>\nNext line";
    Assert(DescriptionFormatting.ForTmp(text) == "<style=cIsDamage>Long <color=#FFAA00>description</color></style>\nNext line");
});
Check("Stationary repeat cycles complete quantities without changing the display", () =>
{
    var queue = new QueueState { RotateRepeatedItems = false };
    queue.Enqueue("a", 2); queue.Enqueue("b", 1); queue.ToggleRepeat("Tier1");
    var order = queue.Entries.ToArray();
    foreach (var expected in new[] { 0, 0, 1, 0, 0, 1 })
    {
        int head = queue.FindHead("Tier1", _ => "Tier1"); Assert(head == expected); queue.Consume(head, "Tier1");
        Assert(queue.Entries.SequenceEqual(order) && queue.Entries[0].Count == 2 && queue.Entries[1].Count == 1);
    }
});
Check("Stationary repeat separates Void cycles and restarts after edits or mode changes", () =>
{
    var queue = new QueueState { RotateRepeatedItems = false };
    queue.Enqueue("a", 1); queue.Enqueue("red", 1); queue.Enqueue("b", 1);
    string Tier(string name) => name == "red" ? "VoidTier3" : "VoidTier1";
    queue.ToggleRepeat("VoidTier1"); queue.ToggleRepeat("VoidTier3");
    queue.Consume(queue.FindHead("VoidTier1", Tier), "VoidTier1");
    queue.Consume(queue.FindHead("VoidTier3", Tier), "VoidTier3");
    Assert(queue.FindHead("VoidTier1", Tier) == 2 && queue.FindHead("VoidTier3", Tier) == 1);
    queue.Increase(0); Assert(queue.FindHead("VoidTier1", Tier) == 0);
    queue.RotateRepeatedItems = true; queue.Consume(0, "VoidTier1");
    Assert(queue.Entries[0].Count == 1 && queue.Entries.Last().Pickup == "a");
    queue.ToggleRepeat("VoidTier1"); queue.RotateRepeatedItems = false;
    queue.Consume(0, "VoidTier1"); Assert(queue.Entries[0].Pickup == "red");
});
Check("Grid insertion works across wrapped rows and an incomplete final row", () =>
{
    int Boundary(float x, float y) => QueueInteraction.GridBoundary(8, 3, 64, 7, 8, x, y);
    Assert(Boundary(9, 10) == 0 && Boundary(60, 10) == 1);
    Assert(Boundary(9, 81) == 3 && Boundary(60, 81) == 4);
    Assert(Boundary(151, 81) == 5 && Boundary(200, 81) == 6);
    Assert(Boundary(200, 152) == 8 && Boundary(9, 250) == 8);
});
Check("Command essence tier wins over mixed Void option ordering", () =>
{
    string Tier(string name) => name switch { "white" => "VoidTier1", "green" => "VoidTier2", "red" => "VoidTier3", _ => "VoidBoss" };
    var queue = new QueueState(_ => "Void");
    queue.Enqueue("boss"); queue.Enqueue("red"); queue.Enqueue("green"); queue.Enqueue("white", 2);
    var options = new[] { new CommandOption("boss", Tier("boss"), true), new CommandOption("red", Tier("red"), true),
        new CommandOption("green", Tier("green"), true), new CommandOption("white", Tier("white"), true) };
    foreach (string name in new[] { "white", "green", "red", "boss" })
        Assert(options[CommandSelection.FindOption(queue, options, Tier(name), Tier)].Pickup == name);
    Assert(CommandSelection.FindOption(queue, options, null, Tier) == -1);
    Assert(queue.Entries.Sum(entry => entry.Count) == 5);
});
Check("Empty or unavailable essence tier never consumes a different Void tier", () =>
{
    string Tier(string name) => name == "white" ? "VoidTier1" : "VoidTier3";
    var queue = new QueueState(_ => "Void"); queue.Enqueue("red");
    var options = new[] { new CommandOption("red", "VoidTier3", true), new CommandOption("white", "VoidTier1", true) };
    Assert(CommandSelection.FindOption(queue, options, "VoidTier1", Tier) == -1);
    queue.Enqueue("white");
    options[1] = new CommandOption("white", "VoidTier1", false);
    Assert(CommandSelection.FindOption(queue, options, "VoidTier1", Tier) == -1);
    Assert(queue.Entries.Count == 2);
});
Check("Client tier inference includes unavailable options and keeps the tier head", () =>
{
    var queue = new QueueState(); queue.Enqueue("first"); queue.Enqueue("second");
    var options = new[] { new CommandOption("second", "Tier1", true), new CommandOption("first", "Tier1", true) };
    Assert(CommandSelection.FindOption(queue, options, null, _ => "Tier1") == 1);
    options[1] = new CommandOption("first", "Tier1", false);
    Assert(CommandSelection.FindOption(queue, options, null, _ => "Tier1") == -1);
    options[1] = new CommandOption("first", "VoidBoss", false);
    Assert(CommandSelection.FindOption(queue, options, null, _ => "Tier1") == -1);
});
Check("Green Void repeat cannot handle white, red or boss essences in either repeat mode", () =>
{
    foreach (bool rotate in new[] { true, false })
    {
        var queue = new QueueState(_ => "Void") { RotateRepeatedItems = rotate };
        queue.Enqueue("green"); queue.ToggleRepeat("VoidTier2");
        var options = new[] { new CommandOption("green", "VoidTier2", true),
            new CommandOption("white", "VoidTier1", true), new CommandOption("red", "VoidTier3", true),
            new CommandOption("boss", "VoidBoss", true) };
        for (int cycle = 0; cycle < 3; cycle++)
        {
            foreach (string tier in new[] { "VoidTier1", "VoidTier3", "VoidBoss" })
                Assert(CommandSelection.FindOption(queue, options, tier, _ => "VoidTier2") == -1);
            Assert(CommandSelection.FindOption(queue, options, "VoidTier2", _ => "VoidTier2") == 0);
            queue.Consume(0, "VoidTier2");
            Assert(queue.Entries.Single().Pickup == "green" && queue.Entries.Single().Count == 1);
        }
    }
});
Check("Special pickup policies are independent and permanent pickups consume normally", () =>
{
    var manual = SpecialPickupBehavior.ManualMenu;
    var keep = SpecialPickupBehavior.UseQueueWithoutConsuming;
    var normal = SpecialPickupBehavior.UseQueueNormally;
    Assert(SpecialPickupPolicy.Resolve(false, false, manual, manual) == normal);
    foreach (var behavior in new[] { manual, keep, normal })
    {
        Assert(SpecialPickupPolicy.Resolve(true, false, behavior, manual) == behavior);
        Assert(SpecialPickupPolicy.Resolve(false, true, manual, behavior) == behavior);
    }
    Assert(SpecialPickupPolicy.Resolve(true, true, manual, normal) == manual);
    Assert(SpecialPickupPolicy.Resolve(true, true, keep, manual) == manual);
    Assert(SpecialPickupPolicy.Resolve(true, true, normal, keep) == keep);
    Assert(SpecialPickupPolicy.Resolve(true, true, normal, normal) == normal);
});
Check("Selection without consumption preserves quantities, order and repeat flags", () =>
{
    foreach (bool rotate in new[] { true, false })
    foreach (bool repeat in new[] { true, false })
    {
        var queue = new QueueState() { RotateRepeatedItems = rotate };
        queue.Enqueue("a", 2); queue.Enqueue("b", 3);
        if (repeat) queue.ToggleRepeat("Tier1");
        string saved = QueueDocument.Capture(queue).Encode();
        int changes = 0; queue.Changed += () => changes++;
        for (int pickup = 0; pickup < 5; pickup++)
            SpecialPickupPolicy.CompleteSelection(queue, queue.FindHead("Tier1", _ => "Tier1", false), "Tier1", SpecialPickupBehavior.UseQueueWithoutConsuming);
        Assert(QueueDocument.Capture(queue).Encode() == saved && changes == 0);
        SpecialPickupPolicy.CompleteSelection(queue, 0, "Tier1", SpecialPickupBehavior.ManualMenu);
        Assert(QueueDocument.Capture(queue).Encode() == saved && changes == 0);
    }
});
Check("Non-consuming special choices retain the current stationary Repeat progress", () =>
{
    var queue = new QueueState() { RotateRepeatedItems = false };
    queue.Enqueue("a", 2); queue.Enqueue("b"); queue.ToggleRepeat("Tier1");
    queue.Consume(0, "Tier1");
    var options = new[] { new CommandOption("a", "Tier1", true), new CommandOption("b", "Tier1", true) };
    for (int pickup = 0; pickup < 3; pickup++)
    {
        Assert(CommandSelection.FindOption(queue, options, "Tier1", _ => "Tier1") == 0);
        SpecialPickupPolicy.CompleteSelection(queue, 0, "Tier1", SpecialPickupBehavior.UseQueueWithoutConsuming);
    }
    Assert(queue.FindHead("Tier1", _ => "Tier1") == 0);
    SpecialPickupPolicy.CompleteSelection(queue, 0, "Tier1", SpecialPickupBehavior.UseQueueNormally);
    Assert(queue.FindHead("Tier1", _ => "Tier1") == 1);
    for (int pickup = 0; pickup < 3; pickup++)
    {
        Assert(CommandSelection.FindOption(queue, options, "Tier1", _ => "Tier1") == 1);
        SpecialPickupPolicy.CompleteSelection(queue, 1, "Tier1", SpecialPickupBehavior.UseQueueWithoutConsuming);
    }
    queue.Consume(1, "Tier1");
    Assert(CommandSelection.FindOption(queue, options, "Tier1", _ => "Tier1") == 0);
    queue.Consume(queue.FindHead("Tier1", _ => "Tier1"), "Tier1");
    Assert(queue.FindHead("Tier1", _ => "Tier1") == 0);
});
Check("Normal pickup pools retain strict unavailable heads and regular consumption", () =>
{
    var queue = new QueueState(); queue.Enqueue("a", 2); queue.Enqueue("b");
    var options = new[] { new CommandOption("a", "Tier1", false), new CommandOption("b", "Tier1", true) };
    Assert(CommandSelection.FindOption(queue, options, "Tier1", _ => "Tier1") == -1);
    SpecialPickupPolicy.CompleteSelection(queue, 0, "Tier1", SpecialPickupBehavior.UseQueueNormally);
    Assert(queue.Entries[0].Count == 1 && queue.Entries[1].Pickup == "b");
});
Check("Group pause blocks its selection without resetting stationary Repeat progress", () =>
{
    var queue = new QueueState(_ => "Boss") { RotateRepeatedItems = false };
    queue.Replace(new[] { new QueueEntry("a", 1), new QueueEntry("seed", 2) }, new[] { "Boss" });
    queue.Consume(queue.FindHead("Boss", _ => "Boss"), "Boss");
    var options = new[] { new CommandOption("seed", "Boss", true) };
    string before = QueueDocument.Capture(queue).Encode();
    queue.TogglePause("Boss");
    Assert(CommandSelection.FindOption(queue, options, "Boss", _ => "Boss") == -1);
    Assert(CommandSelection.FindOption(queue, options, "Boss", _ => "Boss", _ => true, ExclusiveItemBehavior.Automatic) == -1);
    queue.TogglePause("Boss");
    Assert(queue.FindHead("Boss", _ => "Boss", false) == 1 && before == QueueDocument.Capture(queue).Encode());
    queue.TogglePause("Boss"); queue.Reset(); Assert(!queue.IsGroupPaused("Boss") && queue.Entries.Count == 0);
});
Check("Pausing groups independently leaves other queued tiers selectable", () =>
{
    string Group(string pickup) => pickup == "white" ? "White" : "Green";
    string Tier(string pickup) => pickup == "white" ? "Tier1" : "Tier2";
    var queue = new QueueState(Group);
    queue.Enqueue("white", 3); queue.Enqueue("green", 2);
    var white = new[] { new CommandOption("white", "Tier1", true) };
    var green = new[] { new CommandOption("green", "Tier2", true) };
    queue.TogglePause("White");
    Assert(queue.IsGroupPaused("White") && !queue.IsGroupPaused("Green"));
    Assert(CommandSelection.FindOption(queue, white, "Tier1", Tier) == -1);
    Assert(CommandSelection.FindOption(queue, green, "Tier2", Tier) == 0);
    queue.TogglePause("Green"); queue.TogglePause("White");
    Assert(!queue.IsGroupPaused("White") && queue.IsGroupPaused("Green"));
    Assert(CommandSelection.FindOption(queue, white, "Tier1", Tier) == 0);
    Assert(CommandSelection.FindOption(queue, green, null, Tier) == -1);
    Assert(queue.Entries.Select(entry => entry.Count).SequenceEqual(new[] { 3, 2 }));
    queue.Reset(); Assert(!queue.IsGroupPaused("White") && !queue.IsGroupPaused("Green"));
});
Check("Void group pause covers all actual Void tiers and exclusive Automatic without an entry", () =>
{
    string Group(string pickup) => pickup.StartsWith("Void") ? "Void" : "Boss";
    var queue = new QueueState(Group);
    queue.TogglePause("Void");
    foreach (string tier in new[] { "VoidTier1", "VoidTier2", "VoidTier3", "VoidBoss" })
    {
        var options = new[] { new CommandOption(tier, tier, true) };
        Assert(CommandSelection.FindOption(queue, options, null, name => name, _ => true,
            ExclusiveItemBehavior.Automatic) == -1);
    }
    var seed = new[] { new CommandOption("seed", "Boss", true) };
    Assert(CommandSelection.FindOption(queue, seed, "Boss", _ => "Boss", _ => true, ExclusiveItemBehavior.Automatic) == 0);
    queue.TogglePause("Boss");
    Assert(CommandSelection.FindOption(queue, seed, "Boss", _ => "Boss", _ => true, ExclusiveItemBehavior.Automatic) == -1);
    queue.TogglePause("Void");
    var zoea = new[] { new CommandOption("VoidBoss", "VoidBoss", true) };
    Assert(CommandSelection.FindOption(queue, zoea, null, name => name, _ => true, ExclusiveItemBehavior.Automatic) == 0);
    Assert(queue.Entries.Count == 0 && queue.IsGroupPaused("Boss"));
});
Check("All six exclusive items select at any queue position with both policies and Repeat modes", () =>
{
    string[] pickups = { "ItemIndex.TitanGoldDuringTP", "ItemIndex.VoidMegaCrabItem", "ItemIndex.MasterCore",
        "ItemIndex.MasterBattery", "ItemIndex.PowerCube", "ItemIndex.PowerPyramid" };
    foreach (string pickup in pickups)
    foreach (var mode in new[] { ExclusiveItemBehavior.Default, ExclusiveItemBehavior.Automatic })
    foreach (bool rotate in new[] { true, false })
    foreach (bool repeat in new[] { true, false })
    foreach (int position in new[] { -1, 0, 1 })
    {
        var queue = new QueueState { RotateRepeatedItems = rotate };
        var entries = new System.Collections.Generic.List<QueueEntry> { new("other", 2) };
        if (position >= 0) entries.Insert(position, new QueueEntry(pickup, 2));
        queue.Replace(entries, repeat ? new[] { "Boss" } : Array.Empty<string>());
        var options = new[] { new CommandOption(pickup, "Boss", true) };
        string before = QueueDocument.Capture(queue).Encode();
        int choice = CommandSelection.FindOption(queue, options, "Boss", _ => "Boss", pickups.Contains, mode);
        Assert(choice == (position >= 0 || mode == ExclusiveItemBehavior.Automatic ? 0 : -1));
        Assert(before == QueueDocument.Capture(queue).Encode());
        if (choice >= 0 && position >= 0)
        {
            int head = queue.FindPickup(pickup, "Boss", true);
            SpecialPickupPolicy.CompleteSelection(queue, head, "Boss", SpecialPickupBehavior.UseQueueNormally);
            Assert(queue.Entries.Single(e => e.Pickup == "other").Count == 2);
            Assert(queue.Entries.Where(e => e.Pickup == pickup).Sum(e => e.Count) == (repeat ? 2 : 1));
        }
        options[0] = new CommandOption(pickup, "Boss", false);
        Assert(CommandSelection.FindOption(queue, options, "Boss", _ => "Boss", pickups.Contains, mode) == -1);
    }
});
Check("Exclusive queued items absent from a normal Boss essence do not block its next item", () =>
{
    foreach (bool rotate in new[] { true, false })
    foreach (bool repeat in new[] { true, false })
    foreach (var policy in new[] { ExclusiveItemBehavior.Default, ExclusiveItemBehavior.Automatic })
    {
        var queue = new QueueState { RotateRepeatedItems = rotate };
        queue.Replace(new[] { new QueueEntry("seed", 2), new QueueEntry("normal", 3) }, repeat ? new[] { "Boss" } : Array.Empty<string>());
        var options = new[] { new CommandOption("normal", "Boss", true) };
        bool Exclusive(string name) => name == "seed";
        Assert(CommandSelection.FindOption(queue, options, "Boss", _ => "Boss", Exclusive, policy) == 0);
        int planned = CommandSelection.FindQueueEntry(queue, options, "Boss", _ => "Boss", Exclusive);
        Assert(planned == 1);
        var entry = queue.Entries[planned];
        int validated = CommandSelection.FindQueueEntry(queue, options, "Boss", _ => "Boss", Exclusive);
        Assert(ReferenceEquals(entry, queue.Entries[validated]));
        string before = QueueDocument.Capture(queue).Encode();
        SpecialPickupPolicy.CompleteSelection(queue, validated, "Boss", SpecialPickupBehavior.UseQueueWithoutConsuming);
        Assert(before == QueueDocument.Capture(queue).Encode());
        int consumed = CommandSelection.FindQueueEntry(queue, options, "Boss", _ => "Boss", Exclusive, true);
        SpecialPickupPolicy.CompleteSelection(queue, consumed, "Boss", SpecialPickupBehavior.UseQueueNormally);
        Assert(queue.Entries.Single(e => e.Pickup == "seed").Count == 2);
        Assert(queue.Entries.Where(e => e.Pickup == "normal").Sum(e => e.Count) == (repeat ? 3 : 2));
        queue.TogglePause("Queue");
        Assert(CommandSelection.FindOption(queue, options, "Boss", _ => "Boss", Exclusive, policy) == -1);
    }
});
Check("Normal stationary Repeat cycles without consuming or resetting absent exclusive items", () =>
{
    var queue = new QueueState { RotateRepeatedItems = false };
    queue.Replace(new[] { new QueueEntry("seed", 1), new QueueEntry("normal-a", 2),
        new QueueEntry("seed", 2), new QueueEntry("normal-b", 1) }, new[] { "Boss" });
    queue.Consume(0, "Boss");
    bool Exclusive(string name) => name == "seed";
    var options = new[] { new CommandOption("normal-b", "Boss", true), new CommandOption("normal-a", "Boss", true) };
    string before = QueueDocument.Capture(queue).Encode();
    foreach (int expected in new[] { 1, 1, 3, 1, 1, 3 })
    {
        int head = CommandSelection.FindQueueEntry(queue, options, "Boss", _ => "Boss", Exclusive);
        int choice = CommandSelection.FindOption(queue, options, "Boss", _ => "Boss", Exclusive);
        Assert(head == expected && options[choice].Pickup == queue.Entries[head].Pickup);
        Assert(queue.FindPickup("seed", "Boss") == 2);
        queue.Consume(CommandSelection.FindQueueEntry(queue, options, "Boss", _ => "Boss", Exclusive, true), "Boss");
    }
    Assert(before == QueueDocument.Capture(queue).Encode() && queue.FindPickup("seed", "Boss") == 2);
});
Check("Only absent exclusive items are bypassed; unavailable and missing normal heads stay manual", () =>
{
    var queue = new QueueState();
    bool Exclusive(string name) => name == "seed";
    queue.Replace(new[] { new QueueEntry("seed", 1), new QueueEntry("blocked", 1), new QueueEntry("normal", 1) });
    var options = new[] { new CommandOption("normal", "Boss", true) };
    Assert(CommandSelection.FindOption(queue, options, "Boss", _ => "Boss", Exclusive) == -1);
    var unavailable = new[] { options[0], new CommandOption("blocked", "Boss", false) };
    Assert(CommandSelection.FindOption(queue, unavailable, "Boss", _ => "Boss", Exclusive) == -1);
    queue.Remove(1, true);
    var offeredSeed = new[] { new CommandOption("seed", "Boss", false), options[0] };
    Assert(CommandSelection.FindOption(queue, offeredSeed, "Boss", _ => "Boss", Exclusive) == -1);
    Assert(CommandSelection.FindOption(queue, options, "VoidBoss", _ => "Boss", Exclusive) == -1);
    queue.Remove(1, true);
    Assert(CommandSelection.FindOption(queue, options, "Boss", _ => "Boss", Exclusive) == -1);
});
Check("Pool-scoped validation detects a changed queue or newly offered exclusive head", () =>
{
    var queue = new QueueState(); queue.Enqueue("seed"); queue.Enqueue("normal");
    bool Exclusive(string name) => name == "seed";
    var options = new[] { new CommandOption("normal", "Boss", true) };
    var planned = queue.Entries[CommandSelection.FindQueueEntry(queue, options, "Boss", _ => "Boss", Exclusive)];
    var changedPool = new[] { new CommandOption("seed", "Boss", true), options[0] };
    int head = CommandSelection.FindQueueEntry(queue, changedPool, "Boss", _ => "Boss", Exclusive);
    Assert(head == 0 && !ReferenceEquals(planned, queue.Entries[head]));
    queue.Replace(new[] { new QueueEntry("seed", 1), new QueueEntry("normal", 1) });
    head = CommandSelection.FindQueueEntry(queue, options, "Boss", _ => "Boss", Exclusive);
    Assert(!ReferenceEquals(planned, queue.Entries[head]));
    queue.Remove(head, true);
    Assert(CommandSelection.FindOption(queue, options, "Boss", _ => "Boss", Exclusive) == -1);
});
Check("Exclusive selection preserves normal pools, tier isolation and changed queue validation", () =>
{
    var queue = new QueueState(); queue.Enqueue("other"); queue.Enqueue("seed");
    bool Exclusive(string pickup) => pickup == "seed";
    var normal = new[] { new CommandOption("other", "Boss", false), new CommandOption("seed", "Boss", true) };
    Assert(CommandSelection.FindOption(queue, normal, "Boss", _ => "Boss", Exclusive, ExclusiveItemBehavior.Automatic) == -1);
    var special = new[] { normal[1] };
    Assert(CommandSelection.FindOption(queue, special, "VoidBoss", _ => "Boss", Exclusive) == -1);
    Assert(CommandSelection.FindOption(queue, special, null, _ => "Boss", Exclusive) == 0);
    queue.Remove(1, true);
    Assert(CommandSelection.FindOption(queue, special, "Boss", _ => "Boss", Exclusive) == -1);
    Assert(CommandSelection.FindOption(queue, special, "Boss", _ => "Boss", Exclusive, ExclusiveItemBehavior.Automatic) == 0);
    queue.TogglePause("Queue");
    Assert(CommandSelection.FindOption(queue, special, "Boss", _ => "Boss", Exclusive, ExclusiveItemBehavior.Automatic) == -1);
});
Check("Exclusive stationary Repeat cycles offered stacks and preserves other tier progress", () =>
{
    var queue = new QueueState { RotateRepeatedItems = false };
    queue.Replace(new[] { new QueueEntry("other", 2), new QueueEntry("seed", 1), new QueueEntry("other", 3),
        new QueueEntry("seed", 2) }, new[] { "Boss" });
    queue.Consume(0, "Boss");
    foreach (int expected in new[] { 1, 3, 3, 1, 3, 3 })
    {
        Assert(queue.FindPickup("seed", "Boss") == expected);
        queue.Consume(queue.FindPickup("seed", "Boss", true), "Boss");
    }
    Assert(queue.FindHead("Boss", _ => "Boss", false) == 0);
    queue.Consume(0, "Boss");
    Assert(queue.FindHead("Boss", _ => "Boss", false) == 2);
    string before = QueueDocument.Capture(queue).Encode();
    SpecialPickupPolicy.CompleteSelection(queue, queue.FindPickup("seed", "Boss"), "Boss", SpecialPickupBehavior.UseQueueWithoutConsuming);
    Assert(before == QueueDocument.Capture(queue).Encode() && queue.FindPickup("seed", "Boss") == 1);
});
Check("Preset order persists across reload with IDs, documents and import marker intact", () =>
    WithImportFiles((path, source) =>
    {
        var store = new PresetStore(path);
        var queue = new QueueState(); queue.Enqueue("a", 4); queue.ToggleRepeat("Tier1");
        store.Add("Same name", queue); store.Add("Same name", queue); store.Add("Third", queue);
        File.WriteAllText(source, "a|Legacy|ItemIndex.Hoof*1,|1");
        store.ImportLegacy(source);
        var originals = store.Presets.ToDictionary(p => p.Id, p => p.Document.Encode());
        var ids = store.Presets.Select(p => p.Id).ToArray();
        store.MoveToBoundary(ids[0], 4);
        Assert(store.Presets.Select(p => p.Id).SequenceEqual(new[] { ids[1], ids[2], ids[3], ids[0] }));
        store.MoveToBoundary(ids[0], 0);
        store.MoveToBoundary(ids[3], 1);
        var expected = new[] { ids[0], ids[3], ids[1], ids[2] };
        Assert(store.Presets.Select(p => p.Id).SequenceEqual(expected));
        store = new PresetStore(path); store.Load();
        Assert(store.Presets.Select(p => p.Id).SequenceEqual(expected));
        Assert(store.Presets.All(p => p.Document.Encode() == originals[p.Id]) && store.ImportLegacy(source) == 0);
        string before = File.ReadAllText(path);
        store.MoveToBoundary(ids[3], 1); store.MoveToBoundary(ids[3], 2);
        Throws(() => store.MoveToBoundary("missing", 0)); Throws(() => store.MoveToBoundary(ids[0], 5));
        Assert(File.ReadAllText(path) == before);
        Directory.CreateDirectory(path + ".tmp");
        bool failed = false;
        try { store.MoveToBoundary(ids[0], 4); } catch (UnauthorizedAccessException) { failed = true; }
        finally { Directory.Delete(path + ".tmp"); }
        Assert(failed && store.Presets.Select(p => p.Id).SequenceEqual(expected) && File.ReadAllText(path) == before);
    }));
Check("All input modes apply independently to Players and Command Queue and update immediately", () =>
{
    Assert(MenuInputPolicy.Resolve(true, GameplayInputBlocking.On, false, false) == MenuInputMode.Block);
    Assert(MenuInputPolicy.Resolve(true, GameplayInputBlocking.On, true, false) == MenuInputMode.Block);
    Assert(MenuInputPolicy.Resolve(true, GameplayInputBlocking.CommandQueueOnly, false, false) == MenuInputMode.Allow);
    Assert(MenuInputPolicy.Resolve(true, GameplayInputBlocking.CommandQueueOnly, true, false) == MenuInputMode.Block);
    Assert(MenuInputPolicy.Resolve(true, GameplayInputBlocking.CommandQueueOnly, false, false) == MenuInputMode.Allow);
    Assert(MenuInputPolicy.Resolve(true, GameplayInputBlocking.Off, true, false) == MenuInputMode.Allow);
    Assert(MenuInputPolicy.Resolve(true, GameplayInputBlocking.Off, false, false) == MenuInputMode.Allow);
    Assert(MenuInputPolicy.Resolve(true, GameplayInputBlocking.On, false, false) == MenuInputMode.Block);
});
Check("Native focus from clicks, hover or text input cannot stop allowed scoreboard gameplay", () =>
{
    foreach (bool nativeFocus in new[] { true, false })
    foreach (bool queueView in new[] { true, false })
    {
        var off = MenuInputPolicy.Resolve(true, GameplayInputBlocking.Off, queueView, false);
        Assert(!MenuInputPolicy.GameplayUiFocused(nativeFocus, off));
        var queueOnly = MenuInputPolicy.Resolve(true, GameplayInputBlocking.CommandQueueOnly, queueView, false);
        Assert(MenuInputPolicy.GameplayUiFocused(nativeFocus, queueOnly) == queueView);
        var on = MenuInputPolicy.Resolve(true, GameplayInputBlocking.On, queueView, false);
        Assert(MenuInputPolicy.GameplayUiFocused(nativeFocus, on));
    }
});
Check("Closed or non-owning scoreboards and the actual pause menu retain native gameplay focus", () =>
{
    foreach (GameplayInputBlocking mode in Enum.GetValues<GameplayInputBlocking>())
    foreach (bool queueView in new[] { true, false })
    foreach (bool nativeFocus in new[] { true, false })
    {
        var closed = MenuInputPolicy.Resolve(false, mode, queueView, false);
        var paused = MenuInputPolicy.Resolve(true, mode, queueView, true);
        Assert(closed == MenuInputMode.KeepNative && paused == MenuInputMode.KeepNative);
        Assert(MenuInputPolicy.GameplayUiFocused(nativeFocus, closed) == nativeFocus);
        Assert(MenuInputPolicy.GameplayUiFocused(nativeFocus, paused) == nativeFocus);
    }
});
Check("Releasing buffered jump and sprint suppression preserves raw input and previous poll states", () =>
{
    var first = new InputBufferFixture(); var second = new InputBufferFixture();
    var release = ButtonBufferAccess.CreateReleaseAction<InputBufferFixture>();
    string raw = first.RawState;
    Assert(first.JumpSuppressed && first.SprintSuppressed);
    release(first);
    Assert(!first.JumpSuppressed && !first.SprintSuppressed && first.RawState == raw);
    Assert(second.JumpSuppressed && second.SprintSuppressed && second.RawState == raw);
    release(first); Assert(first.RawState == raw);
});
Check("Unexpected button-buffer layouts fail explicitly instead of partially changing input", () =>
{
    bool rejected = false;
    try { ButtonBufferAccess.CreateReleaseAction<UnsupportedInputBufferFixture>(); }
    catch (InvalidOperationException) { rejected = true; }
    Assert(rejected);
});
Check("Essence distribution keeps original drops and handles no living recipients", () =>
{
    var distribution = new EssenceDistribution();
    var players = new[] { new DropCandidate(1, 3), new DropCandidate(2, 7) };
    Assert(distribution.Choose("Tier1", EssenceDropMode.Original, players, 1) == null);
    Assert(distribution.Choose("Tier1", EssenceDropMode.RoundRobin, Array.Empty<DropCandidate>(), null) == null);
    Assert(distribution.Choose("Tier1", EssenceDropMode.RoundRobin, players, null) == 1);
});
Check("Round robin is deterministic and independent for every actual tier", () =>
{
    var distribution = new EssenceDistribution();
    var players = new[] { new DropCandidate(7, 0), new DropCandidate(2, 0) };
    foreach (string tier in new[] { "Tier1", "Tier2", "Tier3", "Lunar", "Boss", "VoidTier1", "VoidTier2", "VoidTier3", "VoidBoss", "Cursed", "Hex", "Page" })
    {
        Assert(distribution.Choose(tier, EssenceDropMode.RoundRobin, players, null) == 2);
        Assert(distribution.Choose(tier, EssenceDropMode.RoundRobin, players, null) == 7);
        Assert(distribution.Choose(tier, EssenceDropMode.RoundRobin, players, null) == 2);
    }
});
Check("Distribution survives departures, joins, a sole survivor and a new run", () =>
{
    var distribution = new EssenceDistribution();
    var players = new[] { new DropCandidate(2, 0), new DropCandidate(7, 0) };
    Assert(distribution.Choose("Tier1", EssenceDropMode.RoundRobin, players, null) == 2);
    Assert(distribution.Choose("Tier1", EssenceDropMode.RoundRobin, new[] { new DropCandidate(3, 0), new DropCandidate(7, 0) }, null) == 3);
    Assert(distribution.Choose("Tier1", EssenceDropMode.RoundRobin, new[] { new DropCandidate(7, 0) }, null) == 7);
    Assert(distribution.Choose("Tier1", EssenceDropMode.RoundRobin, players, null) == 2);
    distribution.Reset();
    Assert(distribution.Choose("Tier1", EssenceDropMode.RoundRobin, players, null) == 2);
});
Check("Killer must be an eligible living player and otherwise falls back to rotation", () =>
{
    var distribution = new EssenceDistribution();
    var players = new[] { new DropCandidate(2, 6), new DropCandidate(7, 1) };
    Assert(distribution.Choose("Boss", EssenceDropMode.Killer, players, 7) == 7);
    Assert(distribution.Choose("Boss", EssenceDropMode.Killer, players, 99) == 2);
    Assert(distribution.Choose("Boss", EssenceDropMode.Killer, players, null) == 7);
});
Check("Fewest items follows permanent counts, rotates ties and reevaluates inventories", () =>
{
    var distribution = new EssenceDistribution();
    var players = new[] { new DropCandidate(2, 9), new DropCandidate(7, 3), new DropCandidate(9, 3) };
    Assert(distribution.Choose("Tier1", EssenceDropMode.FewestItems, players, 2) == 7);
    Assert(distribution.Choose("Tier1", EssenceDropMode.FewestItems, players, 2) == 9);
    Assert(distribution.Choose("Tier1", EssenceDropMode.FewestItems, players, 2) == 7);
    Assert(distribution.Choose("Tier1", EssenceDropMode.FewestItems, new[] { new DropCandidate(2, 1), new DropCandidate(7, 3) }, null) == 2);
});
Check("Preset formatting writes one compact preset per CRLF line without changing data or import status", () =>
{
    string directory = Path.Combine(Path.GetTempPath(), "remastered-format-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try
    {
        string path = Path.Combine(directory, "presets.json");
        var store = new PresetStore(path);
        var queue = new QueueState(); queue.Enqueue("item", 5); queue.ToggleRepeat("Tier1");
        store.Add("Quoted \"name\" with \\ slash and\nnewline ä", queue);
        queue.Enqueue("other", 2); store.Add("Second", queue);
        var ids = store.Presets.Select(preset => preset.Id).ToArray();
        string source = Path.Combine(directory, "legacy.txt"); File.WriteAllText(source, "");
        store.ImportLegacy(source);
        string json = File.ReadAllText(path);
        var presetLines = json.Split(new[] { "\r\n" }, StringSplitOptions.None).Where(line => line.StartsWith("{" ) && line.Length > 1).ToArray();
        Assert(presetLines.Length == 2 && presetLines[0].EndsWith(",") && !presetLines[1].EndsWith(","));
        Assert(json.Replace("\r\n", "").IndexOf('\n') == -1);
        var restored = new PresetStore(path); restored.Load();
        Assert(restored.Presets.Select(preset => preset.Id).SequenceEqual(ids));
        Assert(restored.Presets[0].Name == store.Presets[0].Name);
        Assert(restored.Presets[0].Document.Encode() == store.Presets[0].Document.Encode());
        File.WriteAllText(source, "invalid legacy data must not be reimported");
        Assert(restored.ImportLegacy(source) == 0);
        restored.Delete(ids[0]); restored.Delete(ids[1]);
        var empty = new PresetStore(path); empty.Load(); Assert(empty.Presets.Count == 0 && empty.ImportLegacy(source) == 0);
    }
    finally { Directory.Delete(directory, true); }
});

Check("Queued collection authorization is single-use and bound to participant, option and expiry", () =>
{
    var valid = new QueuedCollectionTicket(7, 3, 12);
    Assert(valid.TryConsume(7, 3, 11));
    Assert(!valid.TryConsume(7, 3, 11));
    var changedParticipant = new QueuedCollectionTicket(7, 3, 12);
    Assert(!changedParticipant.TryConsume(8, 3, 11));
    Assert(!changedParticipant.TryConsume(7, 3, 11));
    var changedOption = new QueuedCollectionTicket(7, 3, 12);
    Assert(!changedOption.TryConsume(7, 4, 11));
    Assert(!changedOption.TryConsume(7, 3, 11));
    var stale = new QueuedCollectionTicket(7, 3, 12);
    Assert(!stale.TryConsume(7, 3, 12.1f));
});

Check("Paused recipients are excluded only when the host enables the option", () =>
{
    var players = new[] { new DropCandidate(1, 0, true), new DropCandidate(2, 8) };
    foreach (var mode in new[] { EssenceDropMode.RoundRobin, EssenceDropMode.Killer, EssenceDropMode.FewestItems })
    {
        var distribution = new EssenceDistribution();
        Assert(distribution.Choose("Tier1", mode, players, 1, false) == 1);
        Assert(distribution.Choose("Tier1", mode, players, 1, true) == 2);
    }
});
Check("All paused recipients retain the original drop and resuming restores eligibility", () =>
{
    var distribution = new EssenceDistribution();
    foreach (var mode in new[] { EssenceDropMode.RoundRobin, EssenceDropMode.Killer, EssenceDropMode.FewestItems })
        Assert(distribution.Choose("Tier1", mode, new[] { new DropCandidate(1, 0, true) }, 1, true) == null);
    Assert(distribution.Choose("Tier1", EssenceDropMode.RoundRobin, new[] { new DropCandidate(1, 0) }, null, true) == 1);
});
Check("Pause masks preserve independent groups and the shared Void pause", () =>
{
    var groups = new[] { "White", "Green", "Red", "Lunar", "Boss", "Void", "Hallowed", "Hex", "Pages" };
    var queue = new QueueState(); queue.TogglePause("White"); queue.TogglePause("Void");
    uint mask = QueuePauseMask.Capture(groups, queue.IsGroupPaused);
    Assert(QueuePauseMask.IsPaused(groups, "White", mask));
    Assert(QueuePauseMask.IsPaused(groups, "Void", mask));
    Assert(!QueuePauseMask.IsPaused(groups, "Green", mask));
    Assert(!QueuePauseMask.IsPaused(groups, "unknown", mask));
    queue.Reset(); Assert(QueuePauseMask.Capture(groups, queue.IsGroupPaused) == 0);
});
Check("Remote pauses are bound to sender connection, expire and clear on run reset", () =>
{
    var states = new RemoteQueuePauseStatus(); var owner = new object();
    Assert(!states.IsPaused(1, owner, 0, 0));
    states.Set(1, owner, 1u << 5, 10);
    Assert(states.IsPaused(1, owner, 5, 11));
    Assert(!states.IsPaused(1, owner, 0, 11));
    Assert(!states.IsPaused(1, new object(), 5, 11));
    Assert(!states.IsPaused(2, owner, 5, 11));
    Assert(!states.IsPaused(1, owner, 5, 16));
    Assert(!states.IsPaused(1, owner, -1, 11));
    states.Set(1, owner, 0, 12); Assert(!states.IsPaused(1, owner, 5, 13));
    states.Set(1, owner, 1u << 5, 14); states.Reset(); Assert(!states.IsPaused(1, owner, 5, 14));
});

Check("Automatic essence interaction requires an available queued choice and respects pause", () =>
{
    var queue = new QueueState(_ => "Boss");
    var options = new[] { new CommandOption("seed", "Boss", true) };
    bool CanOpen() => CommandSelection.HasQueuedChoice(queue, options, "Boss", _ => "Boss", name => name == "seed", ExclusiveItemBehavior.Automatic);
    Assert(!CanOpen()); // Exclusive Automatic alone must not open an essence.
    queue.Enqueue("normal"); Assert(!CanOpen());
    queue.Enqueue("seed"); Assert(CanOpen());
    queue.TogglePause("Boss"); Assert(!CanOpen());
    queue.TogglePause("Boss"); Assert(CanOpen());
    options[0] = new CommandOption("seed", "Boss", false); Assert(!CanOpen());
    Assert(queue.Entries.Count == 2 && queue.Entries.All(entry => entry.Count == 1));
});

Check("Eighteen simultaneous drops drain in bounded batches without loss or duplicates", () =>
{
    var work = new BoundedWorkQueue<object>();
    var drops = Enumerable.Range(0, 18).Select(_ => new object()).ToArray();
    foreach (var drop in drops) { work.Add(drop); work.Add(drop); }
    var completed = new System.Collections.Generic.List<object>();
    var batchCounts = new System.Collections.Generic.List<int>();
    while (work.Count > 0)
    {
        int before = completed.Count;
        int drained = work.Drain(4, drop => { Assert(work.Remove(drop) == false); completed.Add(drop); });
        Assert(drained <= 4 && completed.Count - before == drained);
        batchCounts.Add(drained);
    }
    Assert(batchCounts.SequenceEqual(new[] { 4, 4, 4, 4, 2 }));
    Assert(completed.SequenceEqual(drops));
});
Check("Forty drops remain reachable while bounded spawning and automatic selections overlap", () =>
{
    var pending = new BoundedWorkQueue<object>();
    var live = new BoundedWorkQueue<object>();
    var drops = Enumerable.Range(0, 40).Select(_ => new object()).ToArray();
    foreach (var drop in drops) pending.Add(drop);
    var completed = new System.Collections.Generic.HashSet<object>();
    // A selection finishes later than spawning. Remaining candidates must stay registered.
    for (int step = 0; step < 80; step++)
    {
        Assert(pending.Drain(1, drop => live.Add(drop, prioritize: true)) <= 1);
        if (step % 2 == 0 && live.TryVisit(out var selected))
        {
            Assert(completed.Add(selected)); Assert(live.Remove(selected));
        }
    }
    Assert(pending.Count == 0 && live.Count == 0 && completed.SetEquals(drops));
});
Check("A large live set can be visited in constant-size batches with prompt new-spawn priority", () =>
{
    var work = new BoundedWorkQueue<object>();
    var old = Enumerable.Range(0, 10000).Select(_ => new object()).ToArray();
    foreach (var item in old) work.Add(item);
    for (int i = 0; i < 32; i++) { Assert(work.TryVisit(out var item) && item == old[i]); }
    var recent = new object(); work.Add(recent, prioritize: true);
    Assert(work.TryVisit(out var first) && first == recent);
    Assert(work.TryVisit(out var resumed) && resumed == old[32]);
    Assert(work.Count == 10001);
});
Check("Live essence visits remain fair when next/current nodes disappear or are re-enabled", () =>
{
    var work = new BoundedWorkQueue<object>();
    var a = new object(); var b = new object(); var c = new object();
    work.Add(a); work.Add(b); work.Add(c);
    Assert(work.TryVisit(out var first) && first == a);
    Assert(work.Remove(b)); Assert(work.Remove(a));
    Assert(work.TryVisit(out var remaining) && remaining == c);
    work.Add(b); Assert(work.TryVisit(out var next) && next == c);
    Assert(work.TryVisit(out next) && next == b);
    work.Clear(); Assert(work.Count == 0 && !work.TryVisit(out _));
    work.Add(a); Assert(work.TryVisit(out next) && next == a);
});
Check("Callback cancellation leaves no queued work and does not overreport processing", () =>
{
    var work = new BoundedWorkQueue<object>();
    for (int i = 0; i < 18; i++) work.Add(new object());
    Assert(work.Drain(4, _ => work.Clear()) == 1);
    Assert(work.Count == 0 && work.Drain(4, _ => { throw new Exception(); }) == 0);
});
Check("Fifty stored drop payloads survive delayed creation and allow only one delivery per frame", () =>
{
    var work = new DeferredWorkQueue<(int Id, string Pickup, int Flags)>();
    for (int i = 0; i < 50; i++) work.Add((i, "pickup-" + i, i % 4));
    for (int frame = 0; frame < 50; frame++)
    {
        Assert(work.TryBegin(frame, frame, out var drop));
        Assert(drop == (frame, "pickup-" + frame, frame % 4));
        Assert(work.Count == 50 - frame); // Data stays owned until creation completes.
        Assert(!work.TryBegin(frame, frame, out _));
        Assert(work.Complete()); Assert(!work.TryBegin(frame, frame, out _));
        Assert(!work.TryBegin(frame, frame, out _)); // Multiple physics ticks in the same rendered frame.
    }
    Assert(work.Count == 0 && !work.TryBegin(50, 50, out _));
});
Check("A failed deferred creation retains its complete payload and FIFO position for retry", () =>
{
    var work = new DeferredWorkQueue<string>(); work.Add("first:temporary:chest"); work.Add("second:permanent");
    Assert(work.TryBegin(10, 10, out var first)); work.Retry();
    Assert(work.Count == 2 && !work.TryBegin(10, 10, out _));
    Assert(work.TryBegin(11, 11, out var retry) && retry == first); Assert(work.Complete());
    Assert(work.TryBegin(12, 12, out var second) && second == "second:permanent"); Assert(work.Complete());
    Assert(work.Count == 0);
});
Check("Reset cancels deferred creation safely during callbacks and allows a new run", () =>
{
    var work = new DeferredWorkQueue<string>(); work.Add("old");
    Assert(work.TryBegin(25, 25, out _)); work.Clear(); Assert(!work.Complete()); work.Retry();
    Assert(work.Count == 0); work.Add("new");
    Assert(work.TryBegin(0, 0, out var entry) && entry == "new"); Assert(work.Complete());
});
Check("Drop delivery keeps at least a thirtieth of a second between requests at different frame rates", () =>
{
    const double interval = 1.0 / 30;
    foreach (int fps in new[] { 30, 60, 120, 240 })
    {
        var work = new DeferredWorkQueue<int>(interval);
        for (int i = 0; i < 50; i++) work.Add(i);
        var times = new System.Collections.Generic.List<double>();
        for (int frame = 0; frame < fps * 5; frame++)
        {
            double now = (double)frame / fps;
            if (!work.TryBegin(frame, now, out var value)) continue;
            Assert(value == times.Count); times.Add(now); Assert(work.Complete());
            Assert(!work.TryBegin(frame, now, out _));
        }
        Assert(times.Count == 50 && work.Count == 0);
        for (int i = 1; i < times.Count; i++) Assert(times[i] - times[i - 1] >= interval - 1e-12);
        Assert(times.Count(time => time < 1 - 1e-9) <= 30);
    }
});
Check("Frame hitches and retry never bypass the delivery interval; a new run resets it", () =>
{
    var work = new DeferredWorkQueue<string>(1.0 / 30); work.Add("first"); work.Add("second");
    Assert(work.TryBegin(0, 0, out _)); work.Retry();
    Assert(!work.TryBegin(1, 0.01, out _));
    Assert(work.TryBegin(2, 10, out var first) && first == "first"); Assert(work.Complete());
    Assert(!work.TryBegin(3, 10.001, out _)); // No burst after ten seconds of stalled processing.
    Assert(work.TryBegin(4, 10.04, out var second) && second == "second"); Assert(work.Complete());
    work.Clear(); work.Add("new run"); Assert(work.TryBegin(0, 0, out _)); Assert(work.Complete());
    Throws(() => new DeferredWorkQueue<string>(-1));
    Throws(() => new DeferredWorkQueue<string>(double.NaN));
});
Check("Options use consistent distribution and special-item labels without changing stored keys", () =>
{
    Assert(OptionsPresentation.Category("Essence drops") == "Item Distribution");
    Assert(OptionsPresentation.Category("Special items") == "Special Items");
    Assert(OptionsPresentation.Category("General") == "General");
    var keys = new[] { "Tier1", "Tier2", "Tier3", "VoidTier1", "VoidTier2", "VoidTier3", "VoidBoss" };
    var names = new[] { "White", "Green", "Red", "VoidWhite", "VoidGreen", "VoidRed", "VoidBoss" };
    for (int i = 0; i < keys.Length; i++)
    {
        Assert(OptionsPresentation.Name("Essence drops", keys[i]) == names[i]);
        Assert(OptionsPresentation.Name("Other mod", keys[i]) == keys[i]);
    }
});

Check("Limiter aggregates nonadjacent additions and keeps initial insertion order", () =>
{
    var limiter = new ItemLimiter(_ => "White"); limiter.Add("syringe", 1); limiter.Add("glasses", 2); limiter.Add("syringe", 2);
    Assert(limiter.Entries.Select(entry => entry.Pickup).SequenceEqual(new[] { "syringe", "glasses" }));
    Assert(limiter.Entries.Select(entry => entry.Count).SequenceEqual(new[] { 3, 2 }));
    limiter.MoveToBoundary(0, 2);
    Assert(limiter.Entries.Last().Pickup == "syringe" && limiter.Entries.Last().Count == 3);
});
Check("Limiter sorts by catalog ID only on request and never changes other groups", () =>
{
    var limiter = new ItemLimiter(pickup => pickup == "green" ? "Green" : "White");
    limiter.Add("high", 3); limiter.Add("green", 7); limiter.Add("low", 2);
    limiter.Sort("White", pickup => pickup == "low" ? 1 : 99);
    Assert(limiter.Entries.Select(entry => entry.Pickup).SequenceEqual(new[] { "low", "green", "high" }));
    Assert(limiter.Entries.Last().Count == 3);
});
Check("Limiter cap reopens after loss and disabling one group keeps its saved targets", () =>
{
    var limiter = new ItemLimiter(pickup => pickup == "green" ? "Green" : "White"); limiter.Add("a", 3); limiter.Add("green", 2);
    Assert(!limiter.Blocks("a", 2) && limiter.Blocks("a", 3) && limiter.Blocks("a", 4));
    Assert(!limiter.Blocks("a", 1) && !limiter.Blocks("uncapped", 100));
    limiter.Toggle("White"); Assert(!limiter.Blocks("a", 9) && limiter.Blocks("green", 2) && limiter.Limit("a") == 3);
    limiter.Toggle("White"); Assert(limiter.Blocks("a", 3));
});
Check("Limiter rejects overflow and invalid replacement atomically", () =>
{
    var limiter = new ItemLimiter(_ => "White"); limiter.Add("a", 999);
    Throws(() => limiter.Add("a", 1)); Assert(limiter.Limit("a") == 999);
    Throws(() => limiter.Replace(new[] { new QueueEntry("b", 2), new QueueEntry("b", 3) }));
    Assert(limiter.Entries.Single().Pickup == "a");
});
Check("Limiter skips full queue entries without consuming them and restores their priority after loss", () =>
{
    var queue = new QueueState(_ => "White"); queue.Enqueue("syringe", 3); queue.Enqueue("glasses", 2); queue.Enqueue("crowbar");
    queue.Limiter.Add("syringe", 3); queue.Limiter.Add("glasses", 2);
    long syringes = 3; queue.IsPickupAllowed = pickup => !queue.Limiter.Blocks(pickup, pickup == "syringe" ? syringes : pickup == "glasses" ? 2 : 0);
    var options = new[] { new CommandOption("syringe", "Tier1", true), new CommandOption("glasses", "Tier1", true), new CommandOption("crowbar", "Tier1", true) };
    Assert(CommandSelection.FindOption(queue, options, "Tier1", _ => "Tier1") == 2);
    Assert(queue.Entries[0].Count == 3 && queue.Entries[1].Count == 2);
    syringes = 2; Assert(CommandSelection.FindOption(queue, options, "Tier1", _ => "Tier1") == 0);
});
Check("Limiter alone never creates a queue selection and all blocked entries fall back to manual", () =>
{
    var queue = new QueueState(_ => "White"); queue.Limiter.Add("a", 3);
    var options = new[] { new CommandOption("a", "Tier1", true) };
    Assert(CommandSelection.FindOption(queue, options, "Tier1", _ => "Tier1") == -1);
    queue.Enqueue("a"); queue.IsPickupAllowed = _ => false;
    Assert(!CommandSelection.HasQueuedChoice(queue, options, "Tier1", _ => "Tier1", _ => false, ExclusiveItemBehavior.Default));
    queue.IsPickupAllowed = _ => true; queue.TogglePause("White");
    Assert(CommandSelection.FindOption(queue, options, "Tier1", _ => "Tier1") == -1);
});
Check("Stationary repeat can cycle eligible entries around a blocked stack", () =>
{
    var queue = new QueueState(_ => "White"); queue.Enqueue("a", 3); queue.Enqueue("b"); queue.Enqueue("c");
    queue.ToggleRepeat("Tier1"); queue.RotateRepeatedItems = false; queue.IsPickupAllowed = pickup => pickup != "a";
    var options = new[] { new CommandOption("a", "Tier1", true), new CommandOption("b", "Tier1", true), new CommandOption("c", "Tier1", true) };
    for (int index = 0; index < 8; index++)
    {
        int head = CommandSelection.FindQueueEntry(queue, options, "Tier1", _ => "Tier1", resetRepeatCycle: true);
        Assert(head == 1 + index % 2); queue.Consume(head, "Tier1");
    }
    Assert(queue.Entries[0].Count == 3);
});
Check("Queue documents preserve independent limiter targets and disabled groups", () =>
{
    var queue = new QueueState(_ => "White"); queue.Enqueue("a", 2); queue.Limiter.Add("a", 3); queue.Limiter.Toggle("White");
    var document = QueueDocument.Decode(QueueDocument.Capture(queue).Encode()); queue.Reset(); document.Apply(queue);
    Assert(document.Version == 2 && queue.Entries.Single().Count == 2 && queue.Limiter.Limit("a") == 3 && !queue.Limiter.Enabled("White"));
    queue.Limiter.Add("a", 1); Assert(document.Limits.Single().Count == 3);
});
Check("Legacy version one documents load without limits and unsupported versions retain live state", () =>
{
    var document = QueueDocument.Decode("{\"Version\":1,\"Queue\":[{\"Pickup\":\"a\",\"Count\":2}],\"RepeatedTiers\":[]}");
    var queue = new QueueState(); queue.Limiter.Add("old", 2); document.Apply(queue);
    Assert(queue.Entries.Single().Count == 2 && queue.Limiter.Entries.Count == 0);
});
Check("Limiter-only preset groups retain the queue and survive preset disk round trips", () =>
{
    string Group(string pickup) => pickup == "green" ? "Green" : "White";
    string TierGroup(string tier) => tier == "Tier2" ? "Green" : "White";
    var queue = new QueueState(Group); queue.Enqueue("green", 2); queue.Enqueue("white"); queue.Limiter.Add("white", 4);
    var document = new QueueDocument(); document.Limits.Add(new QueueEntry("green", 3)); document.DisabledLimiterGroups.Add("Green");
    document.ApplyGroups(queue, Group, TierGroup);
    Assert(queue.Entries.Select(entry => entry.Pickup).SequenceEqual(new[] { "green", "white" }) && queue.Entries[0].Count == 2 &&
        queue.Limiter.Limit("white") == 4 && queue.Limiter.Limit("green") == 3 && !queue.Limiter.Enabled("Green"));
    string path = Path.Combine(Path.GetTempPath(), "limiter-presets-" + Guid.NewGuid().ToString("N"), "presets.json");
    try { var store = new PresetStore(path); store.Add("Limited", queue); var reload = new PresetStore(path); reload.Load();
        var restored = new QueueState(Group); reload.Presets.Single().Document.Apply(restored); Assert(restored.Limiter.Limit("green") == 3 && !restored.Limiter.Enabled("Green")); }
    finally { Directory.Delete(Path.GetDirectoryName(path), true); }
});
Check("Global distribution and stage exclusions preserve independent source switches", () =>
{
    Assert(!DistributionRules.Allows(false, "stage", "", DropSource.Other, true, true, true));
    Assert(!DistributionRules.Allows(true, "bazaar", " stage, BAZAAR;moon2 ", DropSource.Other, true, true, true));
    Assert(DistributionRules.Allows(true, "bazaar2", "bazaar", DropSource.Other, true, true, true));
    Assert(!DistributionRules.Allows(true, "stage", "", DropSource.Chest, false, true, true));
    Assert(!DistributionRules.Allows(true, "stage", "", DropSource.ExchangePool, true, false, true));
    Assert(!DistributionRules.Allows(true, "stage", "", DropSource.Teleporter, true, true, false));
    Assert(DistributionRules.Allows(true, "stage", "", DropSource.Other, false, false, false));
});
Check("Pending distribution assignments balance a burst before inventories update", () =>
{
    var ledger = new DistributionLedger(); var distribution = new EssenceDistribution();
    var recipients = new System.Collections.Generic.List<long>();
    for (int index = 0; index < 18; index++)
    {
        var players = new[] { 1L, 2L, 3L }.Select(id => new DropCandidate(id, ledger.Count(id, "Tier2", InventoryCountMode.SameTier)));
        long chosen = distribution.Choose("Tier2", EssenceDropMode.FewestItems, players, null).Value;
        ledger.Reserve(chosen, "Tier2"); recipients.Add(chosen);
    }
    Assert(recipients.Take(6).SequenceEqual(new[] { 1L, 2L, 3L, 1L, 2L, 3L }));
    Assert(new[] { 1L, 2L, 3L }.All(id => ledger.Count(id, "Tier2", InventoryCountMode.AllItems) == 6));
});
Check("Distribution reservations can be reassigned and released exactly once across tiers", () =>
{
    var ledger = new DistributionLedger(); var first = ledger.Reserve(1, "Tier1"); ledger.Reserve(1, "Tier2");
    Assert(ledger.Count(1, "Tier1", InventoryCountMode.SameTier) == 1 && ledger.Count(1, "Tier1", InventoryCountMode.AllItems) == 2);
    first.Recipient = 2; Assert(ledger.Count(1, "Tier1", InventoryCountMode.AllItems) == 1 && ledger.Count(2, "Tier1", InventoryCountMode.AllItems) == 1);
    ledger.Release(first); ledger.Release(first); Assert(ledger.Count(2, "Tier1", InventoryCountMode.AllItems) == 0);
    ledger.Reset(); Assert(ledger.Count(1, "Tier1", InventoryCountMode.AllItems) == 0);
});
Check("Hex distributes complete blocks of seven over three players with pending assignments", () =>
{
    var ledger = new DistributionLedger(); var distribution = new EssenceDistribution();
    for (int index = 0; index < 28; index++)
    {
        var players = new[] { 1L, 2L, 3L }.Select(id => new DropCandidate(id, ledger.Count(id, "Hex", InventoryCountMode.SameTier)));
        long chosen = distribution.Choose("Hex", EssenceDropMode.Killer, players, 3, hexGroupsOfSeven: true).Value;
        Assert(chosen == (index / 7) % 3 + 1); ledger.Reserve(chosen, "Hex");
    }
});
Check("Hex finishes the closest partial block and respects pause and Original", () =>
{
    var distribution = new EssenceDistribution();
    var players = new[] { new DropCandidate(1, 5), new DropCandidate(2, 6), new DropCandidate(3, 0, true) };
    Assert(distribution.Choose("Hex", EssenceDropMode.RoundRobin, players, null, true, true) == 2);
    Assert(distribution.Choose("Hex", EssenceDropMode.Original, players, null, true, true) == null);
    Assert(distribution.Choose("Hex", EssenceDropMode.RoundRobin, new[] { new DropCandidate(1, 5), new DropCandidate(2, 7) }, null, true, true) == 1);
    distribution.Reset(); Assert(distribution.Choose("Hex", EssenceDropMode.RoundRobin, new[] { new DropCandidate(1, 0, true) }, null, true, true) == null);
});
Check("Limiter pending selections bridge replicated inventory delays without counting arrivals twice", () =>
{
    var pending = new PendingItemSelections(); pending.Reserve("a", 2, 0); pending.Reserve("a", 2, 0.1);
    Assert(pending.Count("a", 2, 0.2) == 2);
    Assert(pending.Count("a", 3, 1) == 1 && pending.Count("a", 3, 1.1) == 1);
    Assert(pending.Count("a", 4, 2) == 0);
    var cancelled = pending.Reserve("a", 4, 3); pending.Cancel(cancelled); Assert(pending.Count("a", 4, 3.1) == 0);
    pending.Reserve("a", 4, 4); Assert(pending.Count("a", 4, 14) == 0);
});
Check("Distribution special tier labels use Hallowed Hex Pages without changing saved keys", () =>
{
    Assert(OptionsPresentation.Name("Essence drops", "Cursed") == "Hallowed");
    Assert(OptionsPresentation.Name("Essence drops", "Hex") == "Hex");
    Assert(OptionsPresentation.Name("Essence drops", "Page") == "Pages");
});

Check("Combined preset preview sums repeated pickups, appends limiter-only items and retains first-seen order", () =>
{
    var document = new QueueDocument { Queue = new System.Collections.Generic.List<QueueEntry> {
        new QueueEntry("syringe", 1), new QueueEntry("glasses", 2), new QueueEntry("syringe", 2) },
        Limits = new System.Collections.Generic.List<QueueEntry> { new QueueEntry("syringe", 9), new QueueEntry("crowbar", 5) } };
    var preview = PresetPreview.Build(document);
    Assert(preview.Select(item => item.Pickup).SequenceEqual(new[] { "syringe", "glasses", "crowbar" }));
    Assert(preview[0].Quantity == 3 && preview[0].Badge == "3/9" && !preview[0].LimiterOnly);
    Assert(preview[1].Badge == "2/—" && !preview[1].LimiterOnly);
    Assert(preview[2].Quantity == 0 && preview[2].Badge == "0/5" && preview[2].LimiterOnly);
    Assert(document.Queue.Count == 3 && document.Queue[0].Count == 1);
});
Check("Combined preview handles legacy documents and snapshots without mutating either saved list", () =>
{
    var document = QueueDocument.Decode("{\"Version\":1,\"Queue\":[{\"Pickup\":\"a\",\"Count\":999}],\"RepeatedTiers\":[]}");
    var preview = PresetPreview.Build(document); document.Queue[0].Count = 1;
    Assert(preview.Single().Quantity == 999 && preview.Single().Limit == null && !preview.Single().LimiterOnly);
    var empty = new QueueDocument(); Assert(PresetPreview.Build(empty).Count == 0);
    empty.Limits.Add(new QueueEntry("only-limit", 3)); Assert(PresetPreview.Build(empty).Single().LimiterOnly);
});
Check("Queue pause, Limiter enabled and Repeat remain independent when switching their controls", () =>
{
    var queue = new QueueState(_ => "White"); queue.Enqueue("a", 4); queue.Limiter.Add("a", 3);
    queue.TogglePause("White"); Assert(queue.IsGroupPaused("White") && queue.Limiter.Enabled("White"));
    queue.Limiter.Toggle("White"); Assert(queue.IsGroupPaused("White") && !queue.Limiter.Enabled("White"));
    queue.ToggleRepeat("Tier1"); Assert(queue.IsRepeated("Tier1") && !queue.Limiter.Enabled("White"));
    queue.TogglePause("White"); Assert(!queue.IsGroupPaused("White") && !queue.Limiter.Enabled("White"));
    var restored = new QueueState(_ => "White"); QueueDocument.Decode(QueueDocument.Capture(queue).Encode()).Apply(restored);
    Assert(!restored.Limiter.Enabled("White") && restored.IsRepeated("Tier1"));
});
Check("Stage exclusions default to both requested realms and preserve unknown scene names", () =>
{
    var selected = StageExclusions.Parse("BAZAAR, modded_stage; computationalexchange\r\n");
    Assert(StageExclusions.Parse(StageExclusions.Defaults).SetEquals(new[] { "bazaar", "computationalexchange" }));
    StageExclusions.Set(selected, new[] { "blackbeach", "blackbeach2" }, true);
    Assert(StageExclusions.ContainsAll(selected, new[] { "blackbeach", "blackbeach2" }));
    StageExclusions.Set(selected, new[] { "blackbeach", "blackbeach2" }, false);
    Assert(selected.Contains("modded_stage") && selected.Contains("bazaar") && !selected.Contains("blackbeach"));
    Assert(StageExclusions.Parse(StageExclusions.Encode(selected)).SetEquals(selected));
    Assert(!DistributionRules.Allows(true, "computationalexchange", StageExclusions.Defaults, DropSource.Other, true, true, true));
});
Check("Stage variant selection represents partial exclusions and treats clear selection as allow-all", () =>
{
    var selected = StageExclusions.Parse("golemplains");
    Assert(!StageExclusions.ContainsAll(selected, new[] { "golemplains", "golemplains2" }));
    StageExclusions.Set(selected, new[] { "golemplains", "golemplains2" }, true);
    Assert(StageExclusions.ContainsAll(selected, new[] { "golemplains", "golemplains2" }));
    selected.Clear(); Assert(StageExclusions.Encode(selected) == "");
    Assert(DistributionRules.Allows(true, "bazaar", StageExclusions.Encode(selected), DropSource.Other, true, true, true));
});

Check("Queue-only presets preserve all limiter targets and enabled states, including the same group", () =>
{
    var queue = new QueueState(_ => "White"); queue.Enqueue("old", 2); queue.Limiter.Add("old", 8); queue.Limiter.Toggle("White");
    var preset = new QueueDocument(); preset.Queue.Add(new QueueEntry("new", 3));
    preset.ApplyGroups(queue, _ => "White", _ => "White");
    Assert(queue.Entries.Single().Pickup == "new" && queue.Limiter.Limit("old") == 8 && !queue.Limiter.Enabled("White"));
    var legacy = QueueDocument.Decode("{\"Version\":1,\"Queue\":[{\"Pickup\":\"legacy\",\"Count\":1}],\"RepeatedTiers\":[]}");
    legacy.ApplyGroups(queue, _ => "White", _ => "White");
    Assert(queue.Entries.Single().Pickup == "legacy" && queue.Limiter.Limit("old") == 8 && !queue.Limiter.Enabled("White"));
});
Check("Limiter-only presets preserve live queue objects and stationary Repeat progress", () =>
{
    var queue = new QueueState(_ => "White"); queue.Enqueue("a"); queue.Enqueue("b"); queue.ToggleRepeat("Tier1"); queue.RotateRepeatedItems = false;
    var first = queue.Entries[0]; queue.Consume(0, "Tier1");
    var preset = new QueueDocument(); preset.Limits.Add(new QueueEntry("a", 4));
    preset.ApplyGroups(queue, _ => "White", _ => "White");
    Assert(ReferenceEquals(queue.Entries[0], first) && queue.IsRepeated("Tier1") && queue.FindHead("Tier1", _ => "Tier1", false) == 1);
    Assert(queue.Entries.Count == 2 && queue.Limiter.Limit("a") == 4);
});
Check("Mixed presets replace queue and limiter groups independently", () =>
{
    string Group(string name) => name.StartsWith("green", StringComparison.Ordinal) ? "Green" : "White";
    string TierGroup(string name) => name == "Tier2" ? "Green" : "White";
    var queue = new QueueState(Group); queue.Enqueue("white-old", 2); queue.Enqueue("green-old", 3);
    queue.Limiter.Add("white-limit", 6); queue.Limiter.Add("green-old-limit", 9); queue.Limiter.Toggle("White"); queue.ToggleRepeat("Tier2");
    var preset = new QueueDocument(); preset.Queue.Add(new QueueEntry("white-new", 1)); preset.Limits.Add(new QueueEntry("green-new-limit", 4));
    preset.DisabledLimiterGroups.Add("Green"); preset.ApplyGroups(queue, Group, TierGroup);
    Assert(queue.Entries.Any(entry => entry.Pickup == "green-old" && entry.Count == 3) && queue.Entries.Any(entry => entry.Pickup == "white-new"));
    Assert(queue.Limiter.Limit("white-limit") == 6 && queue.Limiter.Limit("green-new-limit") == 4 && !queue.Limiter.Limit("green-old-limit").HasValue);
    Assert(queue.IsRepeated("Tier2") && !queue.Limiter.Enabled("White") && !queue.Limiter.Enabled("Green"));
});
Check("Saving and loading a limiter-only preset never clears an unrelated live queue", () =>
{
    string path = Path.Combine(Path.GetTempPath(), "limiter-isolated-" + Guid.NewGuid().ToString("N"), "presets.json");
    try
    {
        var saved = new QueueState(_ => "White"); saved.Limiter.Add("a", 3);
        var store = new PresetStore(path); store.Add("Limits", saved); Assert(saved.Entries.Count == 0 && saved.Limiter.Limit("a") == 3);
        var reload = new PresetStore(path); reload.Load(); var live = new QueueState(_ => "White"); live.Enqueue("b", 7);
        reload.Presets.Single().Document.ApplyGroups(live, _ => "White", _ => "White");
        Assert(live.Entries.Single().Pickup == "b" && live.Entries.Single().Count == 7 && live.Limiter.Limit("a") == 3);
    }
    finally { Directory.Delete(Path.GetDirectoryName(path), true); }
});
Check("Preview orders each tier's queue items then limiter-only items without changing either saved list", () =>
{
    var document = new QueueDocument(); document.Queue.AddRange(new[] {
        new QueueEntry("green-a", 2), new QueueEntry("white-b", 1), new QueueEntry("white-a", 3), new QueueEntry("green-a", 1) });
    document.Limits.AddRange(new[] { new QueueEntry("green-only", 4), new QueueEntry("white-only", 5), new QueueEntry("white-a", 9) });
    string before = document.Encode();
    var items = PresetPreview.Build(document, name => name.StartsWith("white", StringComparison.Ordinal) ? 0 : 1);
    Assert(items.Select(item => item.Pickup).SequenceEqual(new[] { "white-b", "white-a", "white-only", "green-a", "green-only" }));
    Assert(items[1].Badge == "3/9" && items[3].Badge == "3/—" && items[2].LimiterOnly && document.Encode() == before);
});
Check("Stage list rejects test/art scenes and canonicalizes both Ambry name prefixes", () =>
{
    Assert(!StageExclusions.IsSelectable("artreview", true) && !StageExclusions.IsSelectable("TESTOTHERBROTHER", true));
    Assert(!StageExclusions.IsSelectable("menu", false) && StageExclusions.IsSelectable("artifactworld", true));
    var names = new[] { "Bulwark's Ambry", "HiddenRealm: Bulwark's Ambry", " Hidden Realm: Bulwark’s Ambry " };
    Assert(names.Select(StageExclusions.DisplayName).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1);
    Assert(StageExclusions.DisplayName("Bulwark's Ambry (Tar Research)") == "Bulwark's Ambry (Tar Research)");
});
Check("Limiter count policies independently include temporary and ethereal stacks", () =>
{
    Assert(LimiterCounting.Count(5, 3, 2, LimiterItemCount.PermanentOnly) == 5);
    Assert(LimiterCounting.Count(5, 3, 2, LimiterItemCount.IncludeTemporary) == 8);
    Assert(LimiterCounting.Count(5, 3, 2, LimiterItemCount.IncludeEthereal) == 7);
    Assert(LimiterCounting.Count(5, 3, 2, LimiterItemCount.IncludeBoth) == 10);
    Assert(LimiterCounting.Count(int.MaxValue, int.MaxValue, int.MaxValue, LimiterItemCount.IncludeBoth) == 3L * int.MaxValue);
    Assert(LimiterCounting.PendingAmount(true, false, LimiterItemCount.PermanentOnly) == 0);
    Assert(LimiterCounting.PendingAmount(false, true, LimiterItemCount.IncludeEthereal) == 1);
    Assert(LimiterCounting.PendingAmount(true, true, LimiterItemCount.IncludeBoth) == 2);
    Assert(LimiterCounting.PendingAmount(false, false, LimiterItemCount.IncludeBoth) == 1);
});
Check("Included temporary stacks block queue selection until they expire without consuming blocked entries", () =>
{
    var queue = new QueueState(_ => "White"); queue.Enqueue("a", 2); queue.Enqueue("b"); queue.Limiter.Add("a", 3);
    int temporary = 2;
    queue.IsPickupAllowed = name => !queue.Limiter.Blocks(name, name == "a" ? LimiterCounting.Count(1, temporary, 0, LimiterItemCount.IncludeTemporary) : 0);
    var options = new[] { new CommandOption("a", "Tier1", true), new CommandOption("b", "Tier1", true) };
    Assert(CommandSelection.FindOption(queue, options, "Tier1", _ => "Tier1") == 1 && queue.Entries[0].Count == 2);
    temporary = 0; Assert(CommandSelection.FindOption(queue, options, "Tier1", _ => "Tier1") == 0);
    SpecialPickupPolicy.CompleteSelection(queue, 0, "Tier1", SpecialPickupBehavior.UseQueueNormally);
    Assert(queue.Entries[0].Count == 1);
});
Check("Weighted pending selections acknowledge combined special-stack arrivals without double counting", () =>
{
    var pending = new PendingItemSelections(); pending.Reserve("a", 3, 0, 2);
    Assert(pending.Count("a", 3, 0.1) == 2);
    Assert(pending.Count("a", 4, 1) == 1 && pending.Count("a", 4, 1.1) == 1);
    Assert(pending.Count("a", 5, 2) == 0);
    var ticket = pending.Reserve("a", 5, 3, 2); pending.Cancel(ticket); Assert(pending.Count("a", 5, 3.1) == 0);
});

Check("Special pools skip absent or unavailable regular heads without deleting them", () =>
{
    foreach (bool absent in new[] { false, true })
    {
        var queue = new QueueState(); queue.Enqueue("unsupported", 2); queue.Enqueue("valid", 2);
        var offered = new CommandOption("valid", "Tier1", true, specialPickup: true);
        var options = absent ? new[] { offered } : new[] { new CommandOption("unsupported", "Tier1", false, true), offered };
        string before = QueueDocument.Capture(queue).Encode();
        Assert(CommandSelection.FindOption(queue, options, "Tier1", _ => "Tier1") == options.Length - 1);
        Assert(CommandSelection.HasQueuedChoice(queue, options, "Tier1", _ => "Tier1", _ => false, ExclusiveItemBehavior.Default));
        Assert(CommandSelection.FindQueueEntry(queue, options, "Tier1", _ => "Tier1") == 1);
        Assert(QueueDocument.Capture(queue).Encode() == before);
        SpecialPickupPolicy.CompleteSelection(queue, 1, "Tier1", SpecialPickupBehavior.UseQueueNormally);
        Assert(queue.Entries[0].Pickup == "unsupported" && queue.Entries[0].Count == 2 && queue.Entries[1].Count == 1);
    }
});
Check("Special fallback respects actual tiers, Pause, limits and an empty eligible pool", () =>
{
    var queue = new QueueState(_ => "White"); queue.Enqueue("missing"); queue.Enqueue("capped"); queue.Enqueue("valid"); queue.Enqueue("green");
    string Tier(string pickup) => pickup == "green" ? "Tier2" : "Tier1";
    queue.IsPickupAllowed = pickup => pickup != "capped";
    var options = new[] { new CommandOption("capped", "Tier1", true, true), new CommandOption("valid", "Tier1", true, true), new CommandOption("green", "Tier2", true, true) };
    Assert(CommandSelection.FindOption(queue, options, "Tier1", Tier) == 1);
    queue.TogglePause("White"); Assert(CommandSelection.FindOption(queue, options, "Tier1", Tier) == -1); queue.TogglePause("White");
    Assert(CommandSelection.FindOption(queue, options, null, Tier) == -1);
    options[1] = new CommandOption("valid", "Tier1", false, true);
    Assert(CommandSelection.FindOption(queue, options, "Tier1", Tier) == -1);
    Assert(!CommandSelection.HasQueuedChoice(queue, options, "Tier1", Tier, _ => false, ExclusiveItemBehavior.Default));
});
Check("Special fallback retains strict permanent selection and manual-policy options", () =>
{
    var queue = new QueueState(); queue.Enqueue("missing"); queue.Enqueue("valid");
    Assert(CommandSelection.FindOption(queue, new[] { new CommandOption("valid", "Tier1", true) }, "Tier1", _ => "Tier1") == -1);
    Assert(CommandSelection.FindOption(queue, new[] { new CommandOption("valid", "Tier1", false, true) }, "Tier1", _ => "Tier1") == -1);
    Assert(CommandSelection.FindOption(queue, Array.Empty<CommandOption>(), "Tier1", _ => "Tier1") == -1);
});
Check("Special fallback keeps both Repeat modes and no-consumption progress intact", () =>
{
    foreach (bool rotate in new[] { false, true })
    {
        var queue = new QueueState { RotateRepeatedItems = rotate };
        queue.Replace(new[] { new QueueEntry("missing", 2), new QueueEntry("a", 1), new QueueEntry("b", 1) }, new[] { "Tier1" });
        var options = new[] { new CommandOption("a", "Tier1", true, true), new CommandOption("b", "Tier1", true, true) };
        string before = QueueDocument.Capture(queue).Encode();
        for (int count = 0; count < 3; count++)
        {
            int head = CommandSelection.FindQueueEntry(queue, options, "Tier1", _ => "Tier1");
            Assert(queue.Entries[head].Pickup == "a");
            SpecialPickupPolicy.CompleteSelection(queue, head, "Tier1", SpecialPickupBehavior.UseQueueWithoutConsuming);
        }
        Assert(QueueDocument.Capture(queue).Encode() == before);
        foreach (string expected in new[] { "a", "b", "a", "b" })
        {
            int head = CommandSelection.FindQueueEntry(queue, options, "Tier1", _ => "Tier1", resetRepeatCycle: true);
            Assert(queue.Entries[head].Pickup == expected);
            SpecialPickupPolicy.CompleteSelection(queue, head, "Tier1", SpecialPickupBehavior.UseQueueNormally);
        }
        Assert(queue.Entries[0].Pickup == "missing" && queue.Entries[0].Count == 2);
    }
});
Check("Special fallback revalidates a changed pool and preserves exclusive behavior", () =>
{
    var queue = new QueueState(); queue.Enqueue("missing"); queue.Enqueue("a"); queue.Enqueue("b");
    var options = new[] { new CommandOption("a", "Boss", true, true), new CommandOption("b", "Boss", true, true) };
    var planned = queue.Entries[CommandSelection.FindQueueEntry(queue, options, "Boss", _ => "Boss")];
    options[0] = new CommandOption("a", "Boss", false, true);
    var next = queue.Entries[CommandSelection.FindQueueEntry(queue, options, "Boss", _ => "Boss")];
    Assert(!ReferenceEquals(planned, next) && next.Pickup == "b");
    var exclusive = new[] { new CommandOption("seed", "Boss", true, true) };
    Assert(CommandSelection.FindOption(queue, exclusive, "Boss", _ => "Boss", name => name == "seed") == -1);
    Assert(CommandSelection.FindOption(queue, exclusive, "Boss", _ => "Boss", name => name == "seed", ExclusiveItemBehavior.Automatic) == 0);
    Assert(!CommandSelection.HasQueuedChoice(queue, exclusive, "Boss", _ => "Boss", name => name == "seed", ExclusiveItemBehavior.Automatic));
});
Check("Drifter Salvage defaults to SkillUser with independent source and global exclusions", () =>
{
    Assert(DistributionRules.Allows(true, "stage", "", DropSource.DrifterSalvage, true, true, true));
    Assert(!DistributionRules.Allows(true, "stage", "", DropSource.DrifterSalvage, true, true, true, DrifterSalvageMode.Original));
    foreach (var mode in new[] { DrifterSalvageMode.SkillUser, DrifterSalvageMode.ItemDistribution })
    {
        Assert(DistributionRules.Allows(true, "stage", "", DropSource.DrifterSalvage, false, false, false, mode));
        Assert(!DistributionRules.Allows(false, "stage", "", DropSource.DrifterSalvage, true, true, true, mode));
        Assert(!DistributionRules.Allows(true, "bazaar", "bazaar", DropSource.DrifterSalvage, true, true, true, mode));
        Assert(!DistributionRules.Allows(true, "stage", "", DropSource.Chest, false, true, true, mode));
    }
    Assert(DistributionRules.Allows(true, "stage", "", DropSource.Other, false, false, false, DrifterSalvageMode.Original));
});

Check("Independent Ignore switches preserve the three behavior values and only match their special types", () =>
{
    Assert((int)SpecialPickupBehavior.ManualMenu == 0 && (int)SpecialPickupBehavior.UseQueueWithoutConsuming == 1 &&
        (int)SpecialPickupBehavior.UseQueueNormally == 2 && Enum.GetValues<SpecialPickupBehavior>().Length == 3);
    foreach (bool temporary in new[] { false, true }) foreach (bool ethereal in new[] { false, true })
    {
        uint mask = SpecialPickupDistribution.IgnoreMask(temporary, ethereal);
        Assert(!SpecialPickupDistribution.Ignores(mask, false, false));
        Assert(SpecialPickupDistribution.Ignores(mask, true, false) == temporary);
        Assert(SpecialPickupDistribution.Ignores(mask, false, true) == ethereal);
        Assert(SpecialPickupDistribution.Ignores(mask, true, true) == (temporary || ethereal));
    }
});
Check("Ignore recipient filtering leaves queue and Repeat intact while manual interaction retains Behaviour", () =>
{
    var queue = new QueueState { RotateRepeatedItems = false };
    queue.Replace(new[] { new QueueEntry("a", 1), new QueueEntry("b", 1) }, new[] { "Tier1" });
    var before = QueueDocument.Capture(queue).Encode();
    Assert(EssenceDistribution.ChooseSkillUser(new[] { new DropCandidate(1, 0, ignored: true) }, 1, false) == null);
    Assert(QueueDocument.Capture(queue).Encode() == before && queue.FindHead("Tier1", _ => "Tier1", false) == 0);
    var behaviour = SpecialPickupPolicy.Resolve(true, false, SpecialPickupBehavior.UseQueueNormally, SpecialPickupBehavior.ManualMenu);
    SpecialPickupPolicy.CompleteSelection(queue, 0, "Tier1", behaviour);
    Assert(queue.FindHead("Tier1", _ => "Tier1", false) == 1);
});
Check("Every recipient algorithm excludes Ignore independently of paused-group filtering", () =>
{
    foreach (var mode in new[] { EssenceDropMode.RoundRobin, EssenceDropMode.FewestItems, EssenceDropMode.Killer })
    foreach (bool excludePaused in new[] { false, true })
    {
        var distribution = new EssenceDistribution();
        var players = new[] { new DropCandidate(1, 0, false, true), new DropCandidate(2, 20) };
        for (int index = 0; index < 5; index++) Assert(distribution.Choose("Tier1", mode, players, 1, excludePaused) == 2);
        Assert(distribution.Choose("Tier1", mode, new[] { players[0] }, 1, excludePaused) == null);
    }
    Assert(new EssenceDistribution().Choose("Hex", EssenceDropMode.FewestItems,
        new[] { new DropCandidate(1, 6, false, true), new DropCandidate(2, 0) }, null, false, true) == 2);
});
Check("Drifter skill targeting keeps each caster's drops together without changing distribution order", () =>
{
    var players = new[] { new DropCandidate(10, 0), new DropCandidate(20, 0), new DropCandidate(30, 0) };
    var distribution = new EssenceDistribution();
    Assert(distribution.Choose("Tier1", EssenceDropMode.RoundRobin, players, null) == 10);
    foreach (long caster in new long[] { 10, 20, 20, 10 })
        for (int drop = 0; drop < 4; drop++) Assert(EssenceDistribution.ChooseSkillUser(players, caster, false) == caster);
    Assert(distribution.Choose("Tier1", EssenceDropMode.RoundRobin, players, null) == 20);
    Assert(EssenceDistribution.ChooseSkillUser(players, null, false) == null);
    Assert(EssenceDistribution.ChooseSkillUser(players, 99, false) == null);
    Assert(EssenceDistribution.ChooseSkillUser(Array.Empty<DropCandidate>(), 10, false) == null);
});
Check("Caster targeting respects personal Ignore and optional Pause without assigning another player", () =>
{
    var players = new[] { new DropCandidate(10, 0, false, true), new DropCandidate(20, 0, true), new DropCandidate(30, 0) };
    Assert(EssenceDistribution.ChooseSkillUser(players, 10, false) == null);
    Assert(EssenceDistribution.ChooseSkillUser(players, 10, true) == null);
    Assert(EssenceDistribution.ChooseSkillUser(players, 20, false) == 20);
    Assert(EssenceDistribution.ChooseSkillUser(players, 20, true) == null);
    Assert(EssenceDistribution.ChooseSkillUser(players, 30, true) == 30);
});
Check("Remote Ignore masks remain personal, connection-bound, expiring and separate from Pause", () =>
{
    var ignores = new RemoteQueuePauseStatus(); var pauses = new RemoteQueuePauseStatus();
    var host = new object(); var drifter = new object();
    ignores.Set(1, host, SpecialPickupDistribution.IgnoreMask(true, false), 0);
    ignores.Set(2, drifter, 0, 0);
    Assert(ignores.IsPaused(1, host, 0, 1));
    Assert(!ignores.IsPaused(2, drifter, 0, 1) && !ignores.IsPaused(1, host, 1, 1));
    Assert(!ignores.IsPaused(1, drifter, 0, 1) && !ignores.IsPaused(3, drifter, 0, 1));
    Assert(!pauses.IsPaused(1, host, 0, 1));
    ignores.Set(2, drifter, 3, 1);
    Assert(ignores.IsPaused(2, drifter, 0, 2) && ignores.IsPaused(2, drifter, 1, 2));
    Assert(!ignores.IsPaused(2, drifter, 0, 6.1f));
    ignores.Reset(); Assert(!ignores.IsPaused(1, host, 0, 1));
});

Check("Single Drifter choice migrates both legacy configuration layouts without changing their meaning", () =>
{
    foreach (string target in new[] { "", "SkillUser", "ItemDistribution" })
    {
        Assert(DrifterSalvageSettings.TryMigrate("false", target, out var disabled) && disabled == DrifterSalvageMode.Original);
        Assert(DrifterSalvageSettings.TryMigrate("true", target, out var enabled) && enabled ==
            (target == "SkillUser" ? DrifterSalvageMode.SkillUser : DrifterSalvageMode.ItemDistribution));
    }
    foreach (var mode in Enum.GetValues<DrifterSalvageMode>())
    {
        Assert(DrifterSalvageSettings.TryMigrate(mode.ToString(), "SkillUser", out var parsed) && parsed == mode);
        Assert(DrifterSalvageSettings.TryMigrate(mode.ToString().ToLowerInvariant(), "obsolete", out var again) && again == mode);
    }
    Assert(!DrifterSalvageSettings.TryMigrate("invalid", "SkillUser", out var fallback) && fallback == DrifterSalvageMode.Original);
    Assert(!DrifterSalvageSettings.TryMigrate("99", "", out fallback) && fallback == DrifterSalvageMode.Original);
    Assert(!DrifterSalvageSettings.TryMigrate("true", "invalid", out fallback) && fallback == DrifterSalvageMode.Original);
});

Console.WriteLine($"{passed} checks passed.");

sealed class InputBufferFixture
{
    private Buffers buttonBuffers = new Buffers
    {
        jump = new Button { suppressUntilReleased = true, bufferedValue = true, previousPollReportedValue = false, releasedSinceLastPoll = true },
        sprint = new Button { suppressUntilReleased = true, bufferedValue = false, previousPollReportedValue = true, releasedSinceLastPoll = false }
    };
    public bool JumpSuppressed => buttonBuffers.jump.suppressUntilReleased;
    public bool SprintSuppressed => buttonBuffers.sprint.suppressUntilReleased;
    public string RawState => $"{buttonBuffers.jump.bufferedValue}:{buttonBuffers.jump.previousPollReportedValue}:{buttonBuffers.jump.releasedSinceLastPoll}:" +
        $"{buttonBuffers.sprint.bufferedValue}:{buttonBuffers.sprint.previousPollReportedValue}:{buttonBuffers.sprint.releasedSinceLastPoll}";
    private struct Buffers { public Button jump, sprint; }
    private struct Button { public bool suppressUntilReleased, bufferedValue, previousPollReportedValue, releasedSinceLastPoll; }
}
sealed class UnsupportedInputBufferFixture { }

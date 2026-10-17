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
    try { QueueDocument.Decode("{\"Version\":2,\"Queue\":[],\"RepeatedTiers\":[]}"); } catch (InvalidDataException) { rejected = true; }
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
Check("Normally handled special pickups use regular consumption and unavailable heads stay manual", () =>
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
Console.WriteLine($"{passed} checks passed.");

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
Console.WriteLine($"{passed} checks passed.");

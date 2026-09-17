using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

internal static class InputReleaseChecks
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;

    internal static void Run(Assembly assembly, string root)
    {
        Type type = assembly.GetType("QuickZoom.TrayContext", true)!;
        using var context = (IDisposable)type.GetConstructors()[0].Invoke([null, true, false, Keys.None]);
        void Set(string name, object? value) => type.GetField(name, Instance)!.SetValue(context, value);
        T Get<T>(string name) => (T)type.GetField(name, Instance)!.GetValue(context)!;
        MethodInfo callback = type.GetMethod("KeyboardHookCallbackCore", Instance)!;
        Type dataType = type.GetNestedType("KBDLLHOOKSTRUCT", BindingFlags.NonPublic)!;
        IntPtr dataPointer = Marshal.AllocHGlobal(Marshal.SizeOf(dataType));
        var replayResults = new Queue<uint>();
        var deliveredKeys = new HashSet<int>();
        int replayCalls = 0;
        try
        {
            // This constructor installs no hooks or magnifier. All replay calls
            // use an in-memory sender so these checks never inject native input.
            Set("_keyboardInputSender", (Func<Array, uint>)Replay);
            Set("_settingsPath", Path.Combine(root, "input-release.json"));
            Set("_legacySettingsPath", Path.Combine(root, "input-release.json"));
            Set("_suppressShortcutKeystrokes", false);
            Set("_enableKey", Keys.ControlKey);
            Set("_followCursorKey", Keys.F);
            Set("_invertKey", Keys.I);
            Set("_enabled", false);
            Set("_invertEnabled", false);
            var suppressedKeys = Get<HashSet<int>>("_suppressedShortcutKeyUps");

            foreach (bool strict in new[] { false, true })
            {
                Set("_strictDataMode", strict);
                Set("_suppressShortcutKeystrokes", false);
                Set("_enableKey", Keys.ControlKey);
                Send(Keys.LControlKey, keyUp: false);
                Send(Keys.RMenu, keyUp: false);
                Check(Get<bool>("_enableKeyPressed") && Get<bool>("_altGrPressed"),
                    "Ctrl/AltGr sequence starts with both states tracked; strict=" + strict);
                Send(Keys.LControlKey, keyUp: true);
                Check(!Get<bool>("_enableKeyPressed") && !Get<bool>("_controlKeyPressed"),
                    "releasing Ctrl while AltGr is held clears the enable key; strict=" + strict);
                Send(Keys.RMenu, keyUp: true);
                Check(!Get<bool>("_altGrPressed"), "releasing AltGr clears its state; strict=" + strict);

                // Seed a consumed shortcut rather than invoking its live action.
                suppressedKeys.Add((int)Keys.F);
                Set("_followCursorKeyPressed", true);
                Check(Send(Keys.F, keyUp: false) == (IntPtr)1,
                    "shortcut repeats remain suppressed after the enable key is released; strict=" + strict);
                Check(Send(Keys.F, keyUp: true) == (IntPtr)1 &&
                    !Get<bool>("_followCursorKeyPressed") && suppressedKeys.Count == 0,
                    "shortcut release clears its consumed state; strict=" + strict);

                Send(Keys.LControlKey, keyUp: false);
                Send(Keys.RMenu, keyUp: false);
                suppressedKeys.Add((int)Keys.I);
                Set("_invertKeyPressed", true);
                Check(Send(Keys.I, keyUp: false) == (IntPtr)1,
                    "a consumed shortcut cannot leak repeats while AltGr is held; strict=" + strict);
                Check(Send(Keys.I, keyUp: true) == (IntPtr)1 &&
                    !Get<bool>("_invertKeyPressed") && suppressedKeys.Count == 0,
                    "AltGr does not skip consumed shortcut release bookkeeping; strict=" + strict);
                Send(Keys.RMenu, keyUp: true);
                Send(Keys.LControlKey, keyUp: true);
                Check(!Get<bool>("_enableKeyPressed") && !Get<bool>("_controlKeyPressed") &&
                    !Get<bool>("_altGrPressed"),
                    "reverse modifier release order also clears every state; strict=" + strict);

                foreach (var keys in new[]
                {
                    (Keys.ControlKey, Keys.LControlKey, Keys.RControlKey),
                    (Keys.ShiftKey, Keys.LShiftKey, Keys.RShiftKey),
                    (Keys.Menu, Keys.LMenu, Keys.RMenu),
                    (Keys.LWin, Keys.LWin, Keys.RWin)
                })
                foreach (bool suppress in new[] { false, true })
                foreach (bool originalReleasedFirst in new[] { false, true })
                foreach (bool usedByQuickZoom in new[] { false, true })
                {
                    Set("_enableKey", keys.Item1);
                    Set("_suppressShortcutKeystrokes", suppress);
                    Send(keys.Item2, keyUp: false);
                    if (suppress && usedByQuickZoom) Set("_enableKeyUsedByQuickZoom", true);
                    Send(keys.Item3, keyUp: false);
                    Send(originalReleasedFirst ? keys.Item2 : keys.Item3, keyUp: true);
                    Check(Get<bool>("_enableKeyPressed") &&
                        (keys.Item1 != Keys.ControlKey || Get<bool>("_controlKeyPressed")),
                        "the other modifier remains held: " + keys.Item1 + "; suppress=" + suppress +
                        "; original-first=" + originalReleasedFirst + "; used=" + usedByQuickZoom + "; strict=" + strict);
                    Send(originalReleasedFirst ? keys.Item3 : keys.Item2, keyUp: true);
                    Check(!Get<bool>("_enableKeyPressed") && !Get<bool>("_controlKeyPressed") &&
                        !Get<bool>("_enableKeyDownSuppressed") && deliveredKeys.Count == 0,
                        "both modifier releases balance forwarded and replayed input; strict=" + strict);
                }

                Set("_enableKey", Keys.ControlKey);
                Set("_suppressShortcutKeystrokes", true);
                foreach (uint sent in new uint[] { 0, 1, 2 })
                {
                    replayResults.Enqueue(sent);
                    Check(Send(Keys.LControlKey, keyUp: false) == (IntPtr)1,
                        "the initial enable down is withheld for replay; strict=" + strict);
                    IntPtr currentDown = Send(Keys.B, keyUp: false);
                    Check((currentDown == (IntPtr)1) == (sent == 2) &&
                        Get<bool>("_replayedEnableKeyDown") == (sent > 0),
                        "chord replay tracks exactly the inserted prefix: " + sent + "/2; strict=" + strict);
                    Send(Keys.B, keyUp: true);
                    Send(Keys.LControlKey, keyUp: true);
                    Check(deliveredKeys.Count == 0 && !Get<bool>("_enableKeyPressed"),
                        "partial chord replay leaves no delivered key held: " + sent + "/2; strict=" + strict);

                    replayResults.Enqueue(sent);
                    Send(Keys.LControlKey, keyUp: false);
                    IntPtr enableUp = Send(Keys.LControlKey, keyUp: true);
                    Check((enableUp == (IntPtr)1) == (sent != 1) && deliveredKeys.Count == 0,
                        "a partial tap replay is completed by the physical key-up: " + sent + "/2; strict=" + strict);
                }

                Set("_suppressShortcutKeystrokes", false);
                Send(Keys.LControlKey, keyUp: false);
                Send(Keys.RControlKey, keyUp: false);
                type.GetMethod("ResetTrackedModifierKeys", Instance)!.Invoke(context, null);
                Check(Get<HashSet<int>>("_pressedEnableKeys").Count == 0 &&
                    Get<HashSet<int>>("_pressedControlKeys").Count == 0 &&
                    !Get<bool>("_enableKeyPressed") && !Get<bool>("_controlKeyPressed"),
                    "modifier reset clears physical tracking as well as public state; strict=" + strict);
                Send(Keys.LControlKey, keyUp: true);
                Send(Keys.RControlKey, keyUp: true);
            }
            Check(replayCalls > 0 && replayResults.Count == 0 && deliveredKeys.Count == 0,
                "all simulated replay outcomes were consumed without native input or stuck keys");
        }
        finally
        {
            Marshal.FreeHGlobal(dataPointer);
        }

        IntPtr Send(Keys key, bool keyUp)
        {
            object data = Activator.CreateInstance(dataType)!;
            dataType.GetField("vkCode")!.SetValue(data, (uint)key);
            Marshal.StructureToPtr(data, dataPointer, false);
            IntPtr result = (IntPtr)callback.Invoke(context, [0, (IntPtr)(keyUp ? 0x0101 : 0x0100), dataPointer])!;
            if (result != (IntPtr)1) RecordDelivered((int)key, keyUp);
            return result;
        }

        uint Replay(Array inputs)
        {
            replayCalls++;
            uint sent = replayResults.Count > 0 ? replayResults.Dequeue() : (uint)inputs.Length;
            for (int index = 0; index < sent; index++)
            {
                object input = inputs.GetValue(index)!;
                object union = input.GetType().GetField("data")!.GetValue(input)!;
                object keyboard = union.GetType().GetField("keyboard")!.GetValue(union)!;
                int key = (ushort)keyboard.GetType().GetField("wVk")!.GetValue(keyboard)!;
                uint flags = (uint)keyboard.GetType().GetField("dwFlags")!.GetValue(keyboard)!;
                RecordDelivered(key, (flags & 0x0002) != 0);
            }
            return sent;
        }

        void RecordDelivered(int key, bool keyUp)
        {
            if (keyUp) deliveredKeys.Remove(key);
            else deliveredKeys.Add(key);
        }
    }

    private static void Check(bool value, string description)
    {
        if (!value) throw new Exception("FAIL: " + description);
        Console.WriteLine("PASS: " + description);
    }
}

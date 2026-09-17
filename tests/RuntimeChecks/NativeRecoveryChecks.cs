using System.Drawing;
using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

internal static class NativeRecoveryChecks
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;

    internal static void Run(Assembly assembly, string root)
    {
        Type type = assembly.GetType("QuickZoom.TrayContext", true)!;
        using var context = (IDisposable)type.GetConstructors()[0].Invoke([null, true, false, Keys.None]);
        void Set(string name, object? value) => type.GetField(name, Instance)!.SetValue(context, value);
        T Get<T>(string name) => (T)type.GetField(name, Instance)!.GetValue(context)!;
        object? Call(string name, params object?[] values) => type.GetMethod(name, Instance)!.Invoke(context, values);

        using var timer = new System.Windows.Forms.Timer { Interval = 20 };
        Set("_cursorSpotlightTimer", timer);
        Set("_cursorEnhancementEnabled", false);
        foreach (bool strict in new[] { false, true })
        {
            Set("_strictDataMode", strict);
            Set("_cursorSpotlightHidesSystemCursor", true);
            Set("_cursorSpotlightOverridesSystemCursors", true);
            Set("_cursorEnhancementApplied", true);
            int attempts = 0;
            Set("_restoreSystemCursors", (Func<bool>)(() => ++attempts >= 3));
            timer.Start();
            Call("HandleCursorSpotlightTick");
            Check(attempts == 1 && timer.Enabled && Get<bool>("_cursorSpotlightHidesSystemCursor") &&
                Get<bool>("_cursorSpotlightOverridesSystemCursors") && Get<bool>("_cursorEnhancementApplied"),
                "failed cursor restoration retains pending state and its retry timer; strict=" + strict);
            Call("HandleCursorSpotlightTick");
            Check(attempts == 2 && timer.Enabled, "cursor restore retries without more mouse movement; strict=" + strict);
            Call("HandleCursorSpotlightTick");
            Check(attempts == 3 && !timer.Enabled && !Get<bool>("_cursorSpotlightHidesSystemCursor") &&
                !Get<bool>("_cursorSpotlightOverridesSystemCursors") && !Get<bool>("_cursorEnhancementApplied"),
                "successful cursor restoration clears pending state and stops idle retries; strict=" + strict);

            // A partially installed override can need recovery before the outer hide flag is set.
            Set("_cursorSpotlightOverridesSystemCursors", true);
            Set("_restoreSystemCursors", (Func<bool>)(() => false));
            timer.Stop();
            Call("RestoreSystemCursorVisibility");
            Check(timer.Enabled && Get<bool>("_cursorSpotlightOverridesSystemCursors"),
                "partial cursor overrides remain recoverable after a failed restoration; strict=" + strict);

            Set("_cursorEnhancementEnabled", true);
            attempts = 0;
            Set("_restoreSystemCursors", (Func<bool>)(() => { attempts++; return false; }));
            Call("ApplyCursorEnhancement");
            Check(attempts == 1 && Get<bool>("_cursorSpotlightOverridesSystemCursors") &&
                !Get<bool>("_applyingCursorEnhancement"),
                "cursor enhancement does not use a cursor scheme that failed to restore; strict=" + strict);
            Set("_cursorEnhancementEnabled", false);
            Set("_restoreSystemCursors", (Func<bool>)(() => true));
            Call("HandleCursorSpotlightTick");

            Set("_cursorSpotlightOverridesSystemCursors", true);
            Set("_cursorSpotlightHidesSystemCursor", true);
            attempts = 0;
            Set("_restoreSystemCursors", (Func<bool>)(() => ++attempts >= 3));
            Call("RestoreSystemCursorAtShutdown");
            Check(attempts == 3 && !Get<bool>("_cursorSpotlightOverridesSystemCursors") &&
                !Get<bool>("_cursorSpotlightHidesSystemCursor"),
                "shutdown restores the cursor after two transient native failures; strict=" + strict);
            Set("_cursorSpotlightOverridesSystemCursors", true);
            Set("_cursorSpotlightHidesSystemCursor", true);
            attempts = 0;
            Set("_restoreSystemCursors", (Func<bool>)(() => { attempts++; return false; }));
            Call("RestoreSystemCursorAtShutdown");
            Check(attempts == 3 && Get<bool>("_cursorSpotlightOverridesSystemCursors") &&
                Get<bool>("_cursorSpotlightHidesSystemCursor"),
                "shutdown bounds permanent restore failures and preserves pending state; strict=" + strict);
            Set("_restoreSystemCursors", (Func<bool>)(() => true));
            Call("RestoreSystemCursorAtShutdown");
        }

        Screen current = Screen.AllScreens[0];
        Screen stale = (Screen)typeof(object).GetMethod("MemberwiseClone", Instance)!.Invoke(current, null)!;
        typeof(Screen).GetField("_bounds", Instance)!.SetValue(stale, new Rectangle(-5000, -4000, 320, 200));
        Set("_lockedScreen", stale);
        Call("EnsureLockedScreenStillValid");
        Check(ReferenceEquals(Get<Screen>("_lockedScreen"), current) && Get<Screen>("_lockedScreen").Bounds == current.Bounds,
            "display recovery refreshes the locked monitor snapshot after resolution or position changes");
        typeof(Screen).GetField("_deviceName", Instance)!.SetValue(stale, "missing-display-for-test");
        Set("_lockedScreen", stale);
        Call("EnsureLockedScreenStillValid");
        Check(Get<Screen?>("_lockedScreen") == null, "display recovery releases a removed locked monitor");

        Call("NotifyMagnifierInitializationFailure");
        Check(Get<bool>("_magnifierFailureNotificationPending"),
            "magnifier creation errors queue a notification without blocking an input callback");
        Set("_runtimeStopped", true);
        Application.DoEvents();
        Check(!Get<bool>("_magnifierFailureNotificationPending"),
            "queued magnifier failure notifications are discarded after shutdown without showing a dialog");
        Set("_runtimeStopped", false);
        CheckMagnifierFailureRecovery(type, context);
    }

    private static void CheckMagnifierFailureRecovery(Type type, object context)
    {
        void Set(string name, object? value) => type.GetField(name, Instance)!.SetValue(context, value);
        T Get<T>(string name) => (T)type.GetField(name, Instance)!.GetValue(context)!;
        object? Call(string name, params object?[] values) => type.GetMethod(name, Instance)!.Invoke(context, values);
        Type apiType = type.GetNestedType("MagnifierWindowApi", BindingFlags.NonPublic)!;
        Type pointType = type.GetNestedType("POINT", BindingFlags.NonPublic)!;
        object point = Activator.CreateInstance(pointType)!;
        Screen screen = Screen.FromPoint(Point.Empty);
        var windows = Get<IDictionary>("_monitorWindows");

        foreach (bool strict in new[] { false, true })
        foreach (bool overlay in new[] { false, true })
        foreach (string operation in new[] { "SetColorEffect", "SetTransform", "SetSource", "SetFilterList" })
        {
            Set("_strictDataMode", strict);
            Set("_zoomMode", Enum.Parse(type.GetField("_zoomMode", Instance)!.FieldType, overlay ? "Lens" : "Fullscreen"));
            Set("_lensShape", Enum.Parse(type.GetField("_lensShape", Instance)!.FieldType, "Rectangle"));
            Set("_smoothedLensCenter", null);
            Set("_cachedSelectedScreens", new List<Screen> { screen });
            Set("_monitorLayoutDirty", false);
            Set("_lastShellUiTrackingCheckTick", long.MaxValue);
            Set("_useFullscreenBackend", false);
            Set("_magActive", true);
            Set("_zoomPercent", 200);
            int uninitialized = 0;
            Set("_uninitializeMagnification", (Func<bool>)(() => { uninitialized++; return true; }));

            Rectangle bounds = overlay ? (Rectangle)Call("BuildLensBounds", Point.Empty, screen.Bounds)! : screen.Bounds;
            Type windowType = type.GetNestedType(overlay ? "OverlayMagnifierWindow" : "MonitorMagnifierWindow", BindingFlags.NonPublic)!;
            Type hostType = type.GetNestedType(overlay ? "OverlayMagnifierHostForm" : "MonitorMagnifierHostForm", BindingFlags.NonPublic)!;
            // Bypass native creation and Show; only an ordinary hidden Form exists.
            object window = RuntimeHelpers.GetUninitializedObject(windowType);
            using var host = (Form)Activator.CreateInstance(hostType, [bounds])!;
            windowType.GetField("_host", Instance)!.SetValue(window, host);
            windowType.GetField("_magnifierHandle", Instance)!.SetValue(window, new IntPtr(1));
            object api = Activator.CreateInstance(apiType, nonPublic: true)!;
            foreach (FieldInfo field in apiType.GetFields())
            {
                ParameterExpression[] parameters = field.FieldType.GetMethod("Invoke")!.GetParameters()
                    .Select(parameter => Expression.Parameter(parameter.ParameterType, parameter.Name)).ToArray();
                field.SetValue(api, Expression.Lambda(field.FieldType, Expression.Constant(field.Name != operation), parameters).Compile());
            }
            int destroyed = 0;
            apiType.GetField("Destroy")!.SetValue(api, (Func<IntPtr, bool>)(_ => { destroyed++; return true; }));
            windowType.GetField("_native", Instance)!.SetValue(window, api);

            if (overlay)
            {
                windowType.GetField("_lastBounds", Instance)!.SetValue(window, bounds);
                windowType.GetField("_lastSize", Instance)!.SetValue(window, bounds.Size);
                windowType.GetField("_lastShape", Instance)!.SetValue(window, Get<object>("_lensShape"));
                Set("_overlayWindow", window);
            }
            else
            {
                windows.Add(screen.DeviceName, window);
            }

            if (operation == "SetFilterList")
            {
                bool accepted = overlay
                    ? (bool)windowType.GetMethod("ExcludeFromSource")!.Invoke(window, null)!
                    : (bool)Call("ApplyMagnifierFilterLists")!;
                Check(!accepted, "failed magnifier source exclusion is reported: overlay=" + overlay);
                Call("HandleMagnifierFailure");
            }
            else if (overlay)
            {
                Call("ApplyOverlayTransform", point);
            }
            else
            {
                Call("ApplyTransformAtPoint", point, 2f);
            }

            Check(!Get<bool>("_magActive") && Get<int>("_zoomPercent") == 100 && host.IsDisposed &&
                destroyed == 1 && uninitialized == 1 && windows.Count == 0 && Get<object?>("_overlayWindow") == null,
                "native magnifier failure removes its host and resets zoom: " + operation + "; overlay=" + overlay + "; strict=" + strict);
            Set("_runtimeStopped", true);
            Application.DoEvents(); // Discards deferred error notification; no dialog appears.
            Set("_runtimeStopped", false);
        }
    }

    private static void Check(bool passed, string description)
    {
        Console.WriteLine((passed ? "PASS: " : "FAIL: ") + description);
        if (!passed) throw new InvalidOperationException(description);
    }
}

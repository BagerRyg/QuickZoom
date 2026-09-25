using System.Diagnostics;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32;

internal static class SetupStartupChecks
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    internal static void Run(Assembly assembly, string root)
    {
        Exception? failure = null;
        using var host = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000) };
        host.Shown += (_, _) => host.BeginInvoke((Action)(() =>
        {
            try { RunOnUiThread(assembly, root); }
            catch (Exception ex) { failure = ex; }
            finally { host.Close(); }
        }));
        host.ShowDialog();
        if (failure != null) throw failure;
    }

    private static void RunOnUiThread(Assembly assembly, string root)
    {
        Type setup = assembly.GetType("QuickZoom.FirstRunSetup", true)!;
        Type formType = setup.GetNestedType("FirstRunSetupForm", BindingFlags.NonPublic)!;
        Type stateType = setup.GetNestedType("SetupStartupState", BindingFlags.NonPublic)!;
        object initial = setup.GetMethod("ReadInitialSelection", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [Path.Combine(root, "unused-settings.json")])!;
        var constructor = formType.GetConstructors(Instance).Single();
        Type callbackType = constructor.GetParameters().Last().ParameterType;
        string output = Path.Combine(root, "setup-startup");
        Directory.CreateDirectory(output);

        ValidateApprovalPresentation(formType, initial, output);

        foreach (bool skip in new[] { false, true })
        {
            int launches = 0;
            object? preparedSelection = null;
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Func<object, Task> prepare = selection =>
            {
                preparedSelection = selection;
                launches++;
                return ready.Task;
            };
            ParameterExpression selectionParameter = Expression.Parameter(initial.GetType());
            Delegate callback = Expression.Lambda(callbackType,
                Expression.Invoke(Expression.Constant(prepare), Expression.Convert(selectionParameter, typeof(object))),
                selectionParameter).Compile();
            using var form = (Form)constructor.Invoke([initial, false, false, null, callback]);
            void Set(string name, object value) => formType.GetField(name, Instance)!.SetValue(form, value);
            T Get<T>(string name) => (T)formType.GetField(name, Instance)!.GetValue(form)!;
            object? Call(string name, params object?[] args) => formType.GetMethod(name, Instance)!.Invoke(form, args);
            Set("_captureMode", true);
            Call("ShowStep", 5, false);
            Set("_startupState", Enum.Parse(stateType, skip ? "Declined" : "Ready"));
            Call("UpdateStartupServiceContent", true);
            form.Location = new Point(-20000, -20000);
            form.Show();
            Call(skip ? "SkipStartupService" : "Continue");
            PumpUntil(() => launches == 1);
            Check(Get<int>("_step") == 5 && !Get<bool>("_accepted"), "completion stays hidden while runtime readiness is pending (skip=" + skip + ")");
            Check(!Get<Control>("_continueButton").Enabled && !Get<Control>("_backButton").Enabled,
                "navigation is disabled while startup owns the handoff");
            object tile = Get<object>("_startupServiceTile");
            Check(tile.GetType().GetField("_state", Instance)!.GetValue(tile)!.ToString() == (skip ? "Verifying" : "Ready") &&
                (skip || !(bool)tile.GetType().GetProperty("IsBusy", Instance)!.GetValue(tile)!),
                "Finish preserves verified startup without restarting the orange progress animation (skip=" + skip + ")");
            Check((bool)preparedSelection!.GetType().GetProperty("StartupServiceSkipped")!.GetValue(preparedSelection)! == skip,
                "the runtime receives the user's autostart choice");
            Call("Continue");
            Call("SkipStartupService");
            form.Close();
            Application.DoEvents();
            Check(launches == 1 && !form.IsDisposed, "repeated clicks and closing cannot interrupt or duplicate a pending startup");
            Capture(form, Path.Combine(output, skip ? "skipped-waiting.png" : "waiting.png"));

            if (!skip)
            {
                ready.SetException(new TimeoutException("Simulated delayed elevated startup"));
                PumpUntil(() => Get<bool>("_runtimePreparationFailed"));
                Check(Get<int>("_step") == 5 && Get<Control>("_continueButton").Enabled,
                    "a startup timeout offers Retry without claiming completion");
                Check(!Get<Control>("_backButton").Enabled && !Get<Control>("_skipButton").Visible,
                    "a pending replacement cannot be bypassed by starting another runtime");
                Capture(form, Path.Combine(output, "retry.png"));
                ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                Call("Continue");
                PumpUntil(() => launches == 2);
            }

            ready.SetResult();
            PumpUntil(() => Get<int>("_step") == 6);
            Check(!Get<Control>("_backButton").Visible && Get<Control>("_continueButton").Enabled,
                "the finished screen appears only after readiness, with preferences already applied");
            Capture(form, Path.Combine(output, skip ? "skipped-ready.png" : "ready.png"));
            int expectedLaunches = launches;
            Call("Continue");
            Check(Get<bool>("_accepted") && launches == expectedLaunches,
                "Finish closes setup without launching or preparing the runtime a second time");
        }
    }

    private static void ValidateApprovalPresentation(Type formType, object initial, string output)
    {
        foreach (string view in new[] { "Standard", "Accessible" })
        foreach (float textScale in new[] { 1f, 2.25f })
        {
            var selectionConstructor = initial.GetType().GetConstructors(Instance).Single(candidate => candidate.GetParameters().Length > 1);
            object?[] selectionValues = selectionConstructor.GetParameters().Select(parameter =>
                parameter.Name == "ViewMode"
                    ? Enum.Parse(parameter.ParameterType, view)
                    : initial.GetType().GetProperty(parameter.Name!)!.GetValue(initial)).ToArray();
            object selection = selectionConstructor.Invoke(selectionValues);
            using var form = (Form)formType.GetConstructors(Instance).Single().Invoke([selection, false, false, textScale, null]);
            void Set(string name, object value) => formType.GetField(name, Instance)!.SetValue(form, value);
            T Get<T>(string name) => (T)formType.GetField(name, Instance)!.GetValue(form)!;
            object? Call(string name, params object?[] args) => formType.GetMethod(name, Instance)!.Invoke(form, args);
            void State(string value) => Set("_startupState", Enum.Parse(formType.GetField("_startupState", Instance)!.FieldType, value));

            Set("_captureMode", true);
            Set("_startupStatusChecked", true);
            Call("ShowStep", 5, false);
            State("NotConfigured");
            Call("UpdateStartupServiceContent", true);
            form.Location = new Point(-20000, -20000);
            form.Show();
            Application.DoEvents();
            // Exercise real system-notification handling without starting the
            // real service, elevating, or touching the user's task/settings.
            Set("_captureMode", false);
            var bounds = form.Bounds;
            IntPtr handle = form.Handle;
            var geometry = Descendants(form).ToDictionary(control => control, control => (control.Bounds, control.Font.Size));
            int visibleChanges = 0;
            int movedOrResized = 0;
            int handlesCreated = 0;
            int contentMovedOrResized = 0;
            form.VisibleChanged += (_, _) => visibleChanges++;
            form.LocationChanged += (_, _) => movedOrResized++;
            form.SizeChanged += (_, _) => movedOrResized++;
            form.HandleCreated += (_, _) => handlesCreated++;
            foreach (Control control in geometry.Keys)
            {
                control.LocationChanged += (_, _) => contentMovedOrResized++;
                control.SizeChanged += (_, _) => contentMovedOrResized++;
            }
            void Stable(string phase)
            {
                Application.DoEvents();
                Check(form.Bounds == bounds && form.Handle == handle && form.Visible && form.Opacity == 1 &&
                    visibleChanges == 0 && movedOrResized == 0 && handlesCreated == 0 && contentMovedOrResized == 0 &&
                    geometry.All(pair => !pair.Key.IsDisposed && pair.Key.Bounds == pair.Value.Bounds && pair.Key.Font.Size == pair.Value.Size),
                    $"{view}/{textScale}: {phase} keeps the window, controls, fonts and native surface fixed");
            }
            void ReturnFromApproval()
            {
                for (int i = 0; i < 3; i++)
                    Call("OnAccessibilityPreferenceChanged", form,
                        new UserPreferenceChangedEventArgs(UserPreferenceCategory.General));
                _ = SendMessage(form.Handle, 0x001C, IntPtr.Zero, IntPtr.Zero); // WM_ACTIVATEAPP: secure desktop
                _ = SendMessage(form.Handle, 0x001C, new IntPtr(1), IntPtr.Zero);
                var suggested = new NativeRectangle { Left = bounds.Left + 40, Top = bounds.Top + 40,
                    Right = bounds.Right + 80, Bottom = bounds.Bottom + 80 };
                IntPtr memory = Marshal.AllocHGlobal(Marshal.SizeOf<NativeRectangle>());
                try
                {
                    Marshal.StructureToPtr(suggested, memory, false);
                    int dpi = form.DeviceDpi;
                    _ = SendMessage(form.Handle, 0x02E0, new IntPtr(dpi | (dpi << 16)), memory);
                }
                finally { Marshal.FreeHGlobal(memory); }
                Application.DoEvents();
                Check(Get<object?>("_pendingDpiChange") != null && Get<bool>("_systemPreferencesPending"),
                    "DPI and preference notifications remain deferred during installation");
            }

            int launches = 0, waits = 0, verifies = 0;
            var approval = new TaskCompletionSource<Process?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var helperExit = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var verification = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Set("_launchStartupHelper", (Func<ProcessStartInfo, Task<Process?>>)(info =>
            {
                launches++;
                Check(info.ErrorDialogParentHandle == handle && info.WindowStyle == ProcessWindowStyle.Hidden && info.Verb == "runas",
                    "the shell launch belongs to the unchanged setup window and does not open a helper window");
                return approval.Task;
            }));
            Set("_waitForStartupHelper", (Func<Process, Task<int>>)(_ => { waits++; return helperExit.Task; }));
            Set("_verifyStartupService", (Func<Task<bool>>)(() => { verifies++; return verification.Task; }));
            Call("Continue");
            Check(launches == 1 && !Get<Control>("_continueButton").Enabled, "approval begins once with navigation disabled");
            ReturnFromApproval();
            Stable("pending approval");
            approval.SetException(new Win32Exception(1223));
            PumpUntil(() => Get<object>("_startupState").ToString() == "Declined");
            PumpUntil(() => Get<object?>("_pendingDpiChange") == null && !Get<bool>("_systemPreferencesPending"));
            Stable("declined approval");
            Check(Get<Control>("_continueButton").Enabled && Get<Control>("_skipButton").Visible,
                "declining approval restores Retry and Skip on the same page");

            approval = new TaskCompletionSource<Process?>(TaskCreationOptions.RunContinuationsAsynchronously);
            Call("Continue");
            approval.SetResult(Process.GetCurrentProcess());
            PumpUntil(() => waits == 1);
            ReturnFromApproval();
            Call("Continue");
            Call("SkipStartupService");
            form.Close();
            Stable("accepted approval while helper is still running");
            Check(launches == 2 && Get<int>("_step") == 5 && !Get<bool>("_accepted"),
                "repeated actions cannot dismiss setup or claim readiness during helper installation");
            Capture(form, Path.Combine(output, $"installing-{view}-{textScale}.png"));

            helperExit.SetException(new TimeoutException("Simulated helper wait timeout"));
            PumpUntil(() => Get<object>("_startupState").ToString() == "Failed");
            Stable("helper timeout");
            helperExit = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            Call("Continue");
            Check(launches == 2 && waits == 2, "retry observes the same pending helper instead of launching a duplicate");
            helperExit.SetResult(0);
            PumpUntil(() => verifies == 1);
            Stable("verifying helper success");
            Check(Get<int>("_step") == 5 && !Get<Control>("_continueButton").Enabled,
                "helper exit alone never enables Continue before service verification");
            verification.SetResult(true);
            PumpUntil(() => Get<object>("_startupState").ToString() == "Ready");
            Stable("verified service ready");
            Check(Get<Control>("_continueButton").Enabled && !Get<bool>("_accepted"),
                "service verification enables Continue without automatically closing or changing setup");
            Set("_captureMode", true);

            // A genuine display/DPI change is delayed, not lost. Applying it
            // after verification must not hide, recreate, or replay reveal.
            State("Verifying");
            Call("UpdateStartupServiceContent", false);
            int nextDpi = form.DeviceDpi + 24;
            int dpiEvents = 0, reportedDpi = 0;
            form.DpiChanged += (_, e) => { dpiEvents++; reportedDpi = e.DeviceDpiNew; };
            var nextBounds = new NativeRectangle { Left = bounds.Left, Top = bounds.Top,
                Right = bounds.Right + 100, Bottom = bounds.Bottom + 100 };
            IntPtr pendingBounds = Marshal.AllocHGlobal(Marshal.SizeOf<NativeRectangle>());
            try
            {
                Marshal.StructureToPtr(nextBounds, pendingBounds, false);
                _ = SendMessage(form.Handle, 0x02E0, new IntPtr(nextDpi | (nextDpi << 16)), pendingBounds);
                _ = SendMessage(form.Handle, 0x007E, new IntPtr(32), IntPtr.Zero);
            }
            finally { Marshal.FreeHGlobal(pendingBounds); }
            Stable("genuine display/DPI update pending verification");
            Check(dpiEvents == 0, "DPI rescaling does not run inside the pending approval/install surface");
            State("Ready");
            Call("UpdateStartupServiceContent", false);
            PumpUntil(() => Get<object?>("_pendingDpiChange") == null && !Get<bool>("_displayChangePending"));
            Check(dpiEvents == 1 && reportedDpi == nextDpi && Screen.FromRectangle(form.Bounds).WorkingArea.Contains(form.Bounds) &&
                form.Handle == handle && visibleChanges == 0 && handlesCreated == 0,
                "deferred display/DPI changes apply once after verification without hiding or recreating setup");
            Call("ValidateSetupViewport");
            Check(true, "the updated DPI viewport keeps all setup content and actions visible");
            Capture(form, Path.Combine(output, $"ready-dpi-{view}-{textScale}.png"));

            if (view == "Standard" && textScale == 1f)
            {
                var highContrast = formType.Assembly.GetType("QuickZoom.AccessibilityPreferences", true)!
                    .GetProperty("CaptureHighContrast", BindingFlags.NonPublic | BindingFlags.Static)!;
                object? previous = highContrast.GetValue(null);
                try
                {
                    highContrast.SetValue(null, true);
                    Set("_windowsHighContrast", true);
                    int[] currentColors = Get<int[]>("_windowsSystemColors").ToArray();
                    int[] previousColors = currentColors.ToArray();
                    previousColors[0] ^= 1; // A prior HC scheme, with HC still enabled.
                    Set("_windowsSystemColors", previousColors);
                    var button = Get<Control>("_continueButton");
                    button.BackColor = Color.Magenta;
                    int recolors = 0;
                    button.BackColorChanged += (_, _) => recolors++;
                    State("Verifying");
                    Call("UpdateStartupServiceContent", false);
                    Set("_captureMode", false);
                    Call("OnAccessibilityPreferenceChanged", form,
                        new UserPreferenceChangedEventArgs(UserPreferenceCategory.Color));
                    Application.DoEvents();
                    Check(Get<int[]>("_windowsSystemColors").SequenceEqual(previousColors) && recolors == 0,
                        "a changed high-contrast color scheme waits until service verification finishes");
                    State("Ready");
                    Call("UpdateStartupServiceContent", false);
                    PumpUntil(() => Get<int[]>("_windowsSystemColors").SequenceEqual(currentColors));
                    Check(button.BackColor.ToArgb() == SystemColors.Highlight.ToArgb() && recolors == 1,
                        "a color-only high-contrast scheme change refreshes controls once even when HC stays enabled");
                    for (int i = 0; i < 3; i++)
                        Call("OnAccessibilityPreferenceChanged", form,
                            new UserPreferenceChangedEventArgs(UserPreferenceCategory.Color));
                    Application.DoEvents();
                    Check(recolors == 1, "repeated unchanged high-contrast notifications do not restart visual updates");
                }
                finally
                {
                    Set("_captureMode", true);
                    highContrast.SetValue(null, previous);
                }
            }
        }
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control descendant in Descendants(child)) yield return descendant;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle { internal int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    private static void PumpUntil(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!condition() && watch.Elapsed < TimeSpan.FromSeconds(5))
        {
            Application.DoEvents();
            Thread.Sleep(5);
        }
        Check(condition(), "the setup message loop processes asynchronous startup results");
    }

    private static void Capture(Form form, string path)
    {
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save(path);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Setup startup check failed: " + message);
        Console.WriteLine("PASS: " + message);
    }
}

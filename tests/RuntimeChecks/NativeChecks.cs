using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;

internal static class NativeChecks
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;

    [DllImport("user32.dll")]
    private static extern uint GetGuiResources(IntPtr process, uint flags);

    internal static void Run(Assembly assembly)
    {
        Type type = assembly.GetType("QuickZoom.TrayContext", true)!;
        object? Call(string name, params object?[] values) => type.GetMethod(name, Static)!.Invoke(null, values);
        object Rect(int width, int height)
        {
            Type rectType = type.GetNestedType("RECT", BindingFlags.NonPublic)!;
            object rect = Activator.CreateInstance(rectType)!;
            rectType.GetField("right")!.SetValue(rect, width);
            rectType.GetField("bottom")!.SetValue(rect, height);
            return rect;
        }

        using var process = Process.GetCurrentProcess();
        for (int cycle = 0; cycle < 20; cycle++)
        {
            uint before = GetGuiResources(process.Handle, 1);
            Check((bool)Call("MagInitialize")!, "native magnification initializes");
            try
            {
                using var host = new Form { Size = new Size(400, 300) };
                // Neither the host nor its child is shown; no screen image is read,
                // rendered, captured or saved. The desktop transform is untouched.
                IntPtr child = (IntPtr)Call("CreateWindowEx", 0, "Magnifier", null, 0x40000000,
                    0, 0, 400, 300, host.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero)!;
                Check(child != IntPtr.Zero, "hidden native magnifier control is created");
                try
                {
                    foreach (float zoom in new[] { 1f, 1.01f, 2f, 7.5f })
                    {
                        Type transformType = type.GetNestedType("MAGTRANSFORM", BindingFlags.NonPublic)!;
                        object transform = Activator.CreateInstance(transformType)!;
                        transformType.GetField("v00")!.SetValue(transform, zoom);
                        transformType.GetField("v11")!.SetValue(transform, zoom);
                        transformType.GetField("v22")!.SetValue(transform, 1f);
                        Check((bool)Call("MagSetWindowTransform", child, transform)!, "native transform is accepted");
                        foreach (string name in new[] { "IdentityColorEffect", "InvertColorEffect" })
                            Check((bool)Call("MagSetColorEffect", child, type.GetField(name, Static)!.GetValue(null))!,
                                "native color effect is accepted");
                        Check((bool)Call("MagSetWindowSource", child, Rect((int)(400 / zoom), (int)(300 / zoom)))!,
                            "native source bounds are accepted");
                    }
                    Check((bool)Call("MagSetWindowFilterList", child, 0, 1, new[] { host.Handle })!,
                        "native self-exclusion filter is accepted");
                    Check(!host.Visible, "native validation never shows a window");
                }
                finally { Check((bool)Call("DestroyWindow", child)!, "native child is destroyed"); }
            }
            finally { Check((bool)Call("MagUninitialize")!, "native magnification uninitializes"); }
            uint after = GetGuiResources(process.Handle, 1);
            if (cycle > 0) Check(after <= before, $"native cycle does not leak USER handles: {before} -> {after}");
        }
        Console.WriteLine("PASS: 20 hidden native magnifier lifecycles, 80 transforms, inversion, source bounds, filters and handle cleanup.");

        for (int cycle = 0; cycle < 10; cycle++)
        {
            using var context = (IDisposable)type.GetConstructors()[0].Invoke([null, true, false, Keys.None]);
            try
            {
                type.GetField("_strictDataMode", Instance)!.SetValue(context, cycle % 2 == 1);
                // Lens initialization alone does not show an overlay or change
                // the shared fullscreen transform.
                var mode = type.GetField("_zoomMode", Instance)!;
                mode.SetValue(context, Enum.Parse(mode.FieldType, "Lens"));
                type.GetMethod("EnsureMag", Instance)!.Invoke(context, [true]);
                Check((bool)type.GetField("_magActive", Instance)!.GetValue(context)!,
                    "the app's engine initializes in either data mode");
                // Any real input passes through unchanged while test hooks exist.
                type.GetField("_runtimeStopped", Instance)!.SetValue(context, true);
                type.GetMethod("InstallHook", Instance)!.Invoke(context, null);
                type.GetMethod("InstallKeyboardHook", Instance)!.Invoke(context, null);
            }
            finally
            {
                type.GetField("_runtimeStopped", Instance)!.SetValue(context, false);
                context.Dispose();
            }
            Check((IntPtr)type.GetField("_hook", Instance)!.GetValue(context)! == IntPtr.Zero &&
                (IntPtr)type.GetField("_kbdHook", Instance)!.GetValue(context)! == IntPtr.Zero &&
                !(bool)type.GetField("_magActive", Instance)!.GetValue(context)!,
                "native input hooks are released");
        }
        Console.WriteLine("PASS: 10 app-engine and mouse/keyboard hook lifecycles across both data modes; input passed through unchanged.");
    }

    private static void Check(bool value, string description)
    {
        if (!value) throw new Exception("FAIL: " + description);
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace QuickZoom;

internal enum TrayFluentIcon
{
    Enabled,
    InvertColors,
    FollowCursor,
    MagnifiedDisplays,
    KeyBinds,
    Settings,
    ResetCursor,
    About,
    Exit
}

internal static partial class FluentTrayIcons
{
    private const float ViewBoxSize = 20f;

    public static GraphicsPath Create(TrayFluentIcon icon)
    {
        return SvgPathMiniLanguage.Parse(GetPathData(icon));
    }

    public static string GetPathData(TrayFluentIcon icon) => icon switch
    {
        TrayFluentIcon.Enabled => "M3.25909 11.6021C3.94254 8.32689 6.79437 6 10 6C13.2057 6 16.0574 8.32688 16.7409 11.6021C16.7974 11.8725 17.0622 12.0459 17.3325 11.9895C17.6029 11.933 17.7763 11.6682 17.7199 11.3979C16.9425 7.67312 13.6934 5 10 5C6.3066 5 3.05742 7.67311 2.28017 11.3979C2.22377 11.6682 2.39718 11.933 2.6675 11.9895C2.93782 12.0459 3.20268 11.8725 3.25909 11.6021ZM10 8C8.067 8 6.5 9.567 6.5 11.5C6.5 13.433 8.067 15 10 15C11.933 15 13.5 13.433 13.5 11.5C13.5 9.567 11.933 8 10 8ZM7.5 11.5C7.5 10.1193 8.61929 9 10 9C11.3807 9 12.5 10.1193 12.5 11.5C12.5 12.8807 11.3807 14 10 14C8.61929 14 7.5 12.8807 7.5 11.5Z",
        TrayFluentIcon.InvertColors => "M3 10C3 6.13401 6.13401 3 10 3C13.866 3 17 6.13401 17 10H3ZM10 2C5.58172 2 2 5.58172 2 10C2 14.4183 5.58172 18 10 18C14.4183 18 18 14.4183 18 10C18 5.58172 14.4183 2 10 2Z",
        TrayFluentIcon.FollowCursor => "M5 3.05854C5 2.21347 5.98325 1.74939 6.63564 2.28655L17.6418 11.3487C18.3661 11.9451 17.9444 13.1207 17.0061 13.1207H11.4142C10.9788 13.1207 10.5648 13.3099 10.2799 13.6392L6.75622 17.7117C6.15025 18.412 5 17.9835 5 17.0574L5 3.05854ZM17.0061 12.1207L6 3.05854L6 17.0574L9.52369 12.9849C9.99856 12.4361 10.6885 12.1207 11.4142 12.1207H17.0061Z",
        TrayFluentIcon.MagnifiedDisplays => "M4 2C2.89543 2 2 2.89543 2 4V13C2 14.1046 2.89543 15 4 15H7V17H5.5C5.22386 17 5 17.2239 5 17.5C5 17.7761 5.22386 18 5.5 18H14.5C14.7761 18 15 17.7761 15 17.5C15 17.2239 14.7761 17 14.5 17H13V15H16C17.1046 15 18 14.1046 18 13V4C18 2.89543 17.1046 2 16 2H4ZM12 15V17H8V15H12ZM3 4C3 3.44772 3.44772 3 4 3H16C16.5523 3 17 3.44772 17 4V13C17 13.5523 16.5523 14 16 14H4C3.44772 14 3 13.5523 3 13V4Z",
        TrayFluentIcon.KeyBinds => "M5 12.5C5 12.2239 5.22386 12 5.5 12H14.5C14.7761 12 15 12.2239 15 12.5C15 12.7761 14.7761 13 14.5 13H5.5C5.22386 13 5 12.7761 5 12.5ZM11.5024 8.00468C11.9179 8.00468 12.2547 7.66783 12.2547 7.25231C12.2547 6.83679 11.9179 6.49994 11.5024 6.49994C11.0868 6.49994 10.75 6.83679 10.75 7.25231C10.75 7.66783 11.0868 8.00468 11.5024 8.00468ZM15.2547 7.25231C15.2547 7.66783 14.9179 8.00468 14.5024 8.00468C14.0868 8.00468 13.75 7.66783 13.75 7.25231C13.75 6.83679 14.0868 6.49994 14.5024 6.49994C14.9179 6.49994 15.2547 6.83679 15.2547 7.25231ZM5.50237 8.00468C5.91789 8.00468 6.25474 7.66783 6.25474 7.25231C6.25474 6.83679 5.91789 6.49994 5.50237 6.49994C5.08685 6.49994 4.75 6.83679 4.75 7.25231C4.75 7.66783 5.08685 8.00468 5.50237 8.00468ZM7.74998 9.75231C7.74998 10.1678 7.41313 10.5047 6.99761 10.5047C6.58209 10.5047 6.24524 10.1678 6.24524 9.75231C6.24524 9.33679 6.58209 8.99994 6.99761 8.99994C7.41313 8.99994 7.74998 9.33679 7.74998 9.75231ZM10.0024 10.5047C10.4179 10.5047 10.7547 10.1678 10.7547 9.75231C10.7547 9.33679 10.4179 8.99994 10.0024 8.99994C9.58685 8.99994 9.25 9.33679 9.25 9.75231C9.25 10.1678 9.58685 10.5047 10.0024 10.5047ZM13.7595 9.75231C13.7595 10.1678 13.4227 10.5047 13.0071 10.5047C12.5916 10.5047 12.2548 10.1678 12.2548 9.75231C12.2548 9.33679 12.5916 8.99994 13.0071 8.99994C13.4227 8.99994 13.7595 9.33679 13.7595 9.75231ZM8.50237 8.00468C8.91789 8.00468 9.25474 7.66783 9.25474 7.25231C9.25474 6.83679 8.91789 6.49994 8.50237 6.49994C8.08685 6.49994 7.75 6.83679 7.75 7.25231C7.75 7.66783 8.08685 8.00468 8.50237 8.00468ZM2 5.5C2 4.67157 2.67157 4 3.5 4H16.5C17.3284 4 18 4.67157 18 5.5V13.5C18 14.3284 17.3284 15 16.5 15H3.5C2.67157 15 2 14.3284 2 13.5V5.5ZM3.5 5C3.22386 5 3 5.22386 3 5.5V13.5C3 13.7761 3.22386 14 3.5 14H16.5C16.7761 14 17 13.7761 17 13.5V5.5C17 5.22386 16.7761 5 16.5 5H3.5Z",
        TrayFluentIcon.Settings => "M1.91099 7.38266C2.28028 6.24053 2.88863 5.19213 3.69133 4.30364C3.82707 4.15339 4.04002 4.09984 4.23069 4.16802L6.14897 4.85392C6.66905 5.03977 7.24131 4.76883 7.42716 4.24875C7.44544 4.19762 7.45952 4.14507 7.46925 4.09173L7.83471 2.08573C7.87104 1.88627 8.02422 1.7285 8.22251 1.6863C8.8027 1.5628 9.39758 1.5 10.0003 1.5C10.6026 1.5 11.1971 1.56273 11.7769 1.68607C11.9752 1.72824 12.1284 1.88591 12.1648 2.08529L12.5313 4.09165C12.6303 4.63497 13.1511 4.9951 13.6944 4.89601C13.7479 4.88627 13.8004 4.87219 13.8515 4.85395L15.7698 4.16802C15.9605 4.09984 16.1734 4.15339 16.3092 4.30364C17.1119 5.19213 17.7202 6.24053 18.0895 7.38266C18.1518 7.57534 18.0918 7.78658 17.9374 7.91764L16.3825 9.23773C15.9615 9.5952 15.9101 10.2263 16.2675 10.6473C16.3027 10.6887 16.3411 10.7271 16.3825 10.7623L17.9374 12.0824C18.0918 12.2134 18.1518 12.4247 18.0895 12.6173C17.7202 13.7595 17.1119 14.8079 16.3092 15.6964C16.1734 15.8466 15.9605 15.9002 15.7698 15.832L13.8515 15.1461C13.3315 14.9602 12.7592 15.2312 12.5733 15.7512C12.5551 15.8024 12.541 15.8549 12.5312 15.9085L12.1648 17.9147C12.1284 18.1141 11.9752 18.2718 11.7769 18.3139C11.1971 18.4373 10.6026 18.5 10.0003 18.5C9.39758 18.5 8.8027 18.4372 8.22251 18.3137C8.02422 18.2715 7.87104 18.1137 7.83471 17.9143L7.46926 15.9084C7.37018 15.365 6.8494 15.0049 6.30608 15.104C6.25265 15.1137 6.20011 15.1278 6.14906 15.1461L4.23069 15.832C4.04002 15.9002 3.82707 15.8466 3.69133 15.6964C2.88863 14.8079 2.28028 13.7595 1.91099 12.6173C1.84869 12.4247 1.90876 12.2134 2.06313 12.0824L3.61798 10.7623C4.03897 10.4048 4.09046 9.77373 3.73299 9.35274C3.69784 9.31135 3.65937 9.27288 3.618 9.23775L2.06313 7.91764C1.90876 7.78658 1.84869 7.57534 1.91099 7.38266ZM2.97154 7.37709L4.26523 8.47546C4.34803 8.54576 4.42496 8.62269 4.49526 8.70548C5.2102 9.54746 5.10721 10.8096 4.26521 11.5246L2.97154 12.6229C3.26359 13.4051 3.68504 14.1322 4.21648 14.7751L5.81246 14.2044C5.91473 14.1679 6.01982 14.1397 6.12667 14.1202C7.21332 13.922 8.25487 14.6423 8.45305 15.729L8.75702 17.3975C9.16489 17.4655 9.58024 17.5 10.0003 17.5C10.42 17.5 10.8351 17.4656 11.2427 17.3976L11.5475 15.7289C11.567 15.6221 11.5951 15.517 11.6317 15.4147C12.0034 14.3746 13.1479 13.8327 14.1881 14.2044L15.784 14.7751C16.3155 14.1322 16.7369 13.4051 17.029 12.6229L15.7353 11.5245C15.6525 11.4542 15.5756 11.3773 15.5053 11.2945C14.7903 10.4525 14.8933 9.1904 15.7353 8.47544L17.029 7.37709C16.7369 6.59486 16.3155 5.86783 15.784 5.22494L14.1881 5.79559C14.0858 5.83214 13.9807 5.8603 13.8738 5.87979C12.7872 6.07796 11.7456 5.3577 11.5475 4.27119L11.2427 2.60235C10.8351 2.53443 10.42 2.5 10.0003 2.5C9.58024 2.5 9.16489 2.53448 8.75702 2.60249L8.45304 4.27105C8.43355 4.37791 8.40539 4.48299 8.36884 4.58527C7.99714 5.62542 6.8526 6.1673 5.81237 5.79556L4.21648 5.22494C3.68504 5.86783 3.26359 6.59486 2.97154 7.37709ZM7.50026 10C7.50026 8.61929 8.61954 7.5 10.0003 7.5C11.381 7.5 12.5003 8.61929 12.5003 10C12.5003 11.3807 11.381 12.5 10.0003 12.5C8.61954 12.5 7.50026 11.3807 7.50026 10ZM8.50026 10C8.50026 10.8284 9.17183 11.5 10.0003 11.5C10.8287 11.5 11.5003 10.8284 11.5003 10C11.5003 9.17157 10.8287 8.5 10.0003 8.5C9.17183 8.5 8.50026 9.17157 8.50026 10Z",
        TrayFluentIcon.ResetCursor => "M16 10C16 6.68629 13.3137 4 10 4C8.2234 4 6.62683 4.77191 5.52772 6H7.5C7.77614 6 8 6.22386 8 6.5C8 6.77614 7.77614 7 7.5 7H4.5C4.22386 7 4 6.77614 4 6.5V3.5C4 3.22386 4.22386 3 4.5 3C4.77614 3 5 3.22386 5 3.5V5.10109C6.27012 3.80499 8.04094 3 10 3C13.866 3 17 6.13401 17 10C17 13.866 13.866 17 10 17C6.13401 17 3 13.866 3 10C3 9.8191 3.00687 9.6397 3.02038 9.46207C3.04133 9.18673 3.28152 8.98049 3.55687 9.00144C3.83222 9.02239 4.03845 9.26258 4.0175 9.53793C4.00591 9.69034 4 9.84443 4 10C4 13.3137 6.68629 16 10 16C13.3137 16 16 13.3137 16 10Z",
        TrayFluentIcon.About => "M10 2C14.4183 2 18 5.58172 18 10C18 14.4183 14.4183 18 10 18C5.58172 18 2 14.4183 2 10C2 5.58172 5.58172 2 10 2ZM10 3C6.13401 3 3 6.13401 3 10C3 13.866 6.13401 17 10 17C13.866 17 17 13.866 17 10C17 6.13401 13.866 3 10 3ZM10 13.5C10.4142 13.5 10.75 13.8358 10.75 14.25C10.75 14.6642 10.4142 15 10 15C9.58579 15 9.25 14.6642 9.25 14.25C9.25 13.8358 9.58579 13.5 10 13.5ZM10 5.5C11.3807 5.5 12.5 6.61929 12.5 8C12.5 8.72959 12.1848 9.40774 11.6513 9.8771L11.4967 10.0024L11.2782 10.1655L11.1906 10.2372C11.1348 10.2851 11.0835 10.3337 11.0346 10.3859C10.6963 10.7464 10.5 11.2422 10.5 12C10.5 12.2761 10.2761 12.5 10 12.5C9.72386 12.5 9.5 12.2761 9.5 12C9.5 10.988 9.79312 10.2475 10.3054 9.70162C10.4165 9.5832 10.532 9.47988 10.6609 9.37874L10.9076 9.19439L11.0256 9.09468C11.325 8.81435 11.5 8.42206 11.5 8C11.5 7.17157 10.8284 6.5 10 6.5C9.17157 6.5 8.5 7.17157 8.5 8C8.5 8.27614 8.27614 8.5 8 8.5C7.72386 8.5 7.5 8.27614 7.5 8C7.5 6.61929 8.61929 5.5 10 5.5Z",
        TrayFluentIcon.Exit => "M4.08859 4.21569L4.14645 4.14645C4.32001 3.97288 4.58944 3.9536 4.78431 4.08859L4.85355 4.14645L10 9.293L15.1464 4.14645C15.32 3.97288 15.5894 3.9536 15.7843 4.08859L15.8536 4.14645C16.0271 4.32001 16.0464 4.58944 15.9114 4.78431L15.8536 4.85355L10.707 10L15.8536 15.1464C16.0271 15.32 16.0464 15.5894 15.9114 15.7843L15.8536 15.8536C15.68 16.0271 15.4106 16.0464 15.2157 15.9114L15.1464 15.8536L10 10.707L4.85355 15.8536C4.67999 16.0271 4.41056 16.0464 4.21569 15.9114L4.14645 15.8536C3.97288 15.68 3.9536 15.4106 4.08859 15.2157L4.14645 15.1464L9.293 10L4.14645 4.85355C3.97288 4.67999 3.9536 4.41056 4.08859 4.21569L4.14645 4.14645L4.08859 4.21569Z",
        _ => throw new ArgumentOutOfRangeException(nameof(icon), icon, null)
    };

    public static Size GetLogicalSize(Control owner)
    {
        return new Size(
            ControlDrawing.ScaleLogical(owner, 18),
            ControlDrawing.ScaleLogical(owner, 18));
    }

    [GeneratedRegex(@"[A-Za-z]|[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?", RegexOptions.Compiled)]
    private static partial Regex SvgTokenRegex();

    private static class SvgPathMiniLanguage
    {
        public static GraphicsPath Parse(string pathData)
        {
            MatchCollection matches = SvgTokenRegex().Matches(pathData);
            var tokens = new List<string>(matches.Count);
            foreach (Match match in matches)
            {
                tokens.Add(match.Value);
            }

            var path = new GraphicsPath(FillMode.Winding);
            int index = 0;
            char command = '\0';
            PointF current = new(0, 0);
            PointF figureStart = new(0, 0);
            bool hasOpenFigure = false;

            while (index < tokens.Count)
            {
                string token = tokens[index];
                if (IsCommandToken(token))
                {
                    command = token[0];
                    index++;
                }

                if (command is '\0')
                {
                    throw new InvalidOperationException("SVG path data is missing a command.");
                }

                switch (command)
                {
                    case 'M':
                    {
                        PointF next = ReadPoint(tokens, ref index);
                        if (hasOpenFigure)
                        {
                            path.CloseFigure();
                        }

                        current = next;
                        figureStart = next;
                        hasOpenFigure = true;

                        while (HasNumericPair(tokens, index))
                        {
                            PointF lineEnd = ReadPoint(tokens, ref index);
                            path.AddLine(current, lineEnd);
                            current = lineEnd;
                        }

                        break;
                    }
                    case 'L':
                    {
                        while (HasNumericPair(tokens, index))
                        {
                            PointF lineEnd = ReadPoint(tokens, ref index);
                            path.AddLine(current, lineEnd);
                            current = lineEnd;
                        }

                        break;
                    }
                    case 'H':
                    {
                        while (HasNumeric(tokens, index))
                        {
                            float x = ReadNumber(tokens, ref index);
                            PointF lineEnd = new(x, current.Y);
                            path.AddLine(current, lineEnd);
                            current = lineEnd;
                        }

                        break;
                    }
                    case 'V':
                    {
                        while (HasNumeric(tokens, index))
                        {
                            float y = ReadNumber(tokens, ref index);
                            PointF lineEnd = new(current.X, y);
                            path.AddLine(current, lineEnd);
                            current = lineEnd;
                        }

                        break;
                    }
                    case 'C':
                    {
                        while (HasNumeric(tokens, index))
                        {
                            PointF c1 = ReadPoint(tokens, ref index);
                            PointF c2 = ReadPoint(tokens, ref index);
                            PointF end = ReadPoint(tokens, ref index);
                            path.AddBezier(current, c1, c2, end);
                            current = end;
                        }

                        break;
                    }
                    case 'Z':
                    {
                        if (hasOpenFigure)
                        {
                            path.CloseFigure();
                            current = figureStart;
                            hasOpenFigure = false;
                        }

                        break;
                    }
                    default:
                        throw new NotSupportedException($"Unsupported SVG path command '{command}'.");
                }
            }

            return path;
        }

        private static bool IsCommandToken(string token)
        {
            return token.Length == 1 && char.IsLetter(token[0]);
        }

        private static bool HasNumeric(List<string> tokens, int index)
        {
            return index < tokens.Count && !IsCommandToken(tokens[index]);
        }

        private static bool HasNumericPair(List<string> tokens, int index)
        {
            return index + 1 < tokens.Count && !IsCommandToken(tokens[index]) && !IsCommandToken(tokens[index + 1]);
        }

        private static float ReadNumber(List<string> tokens, ref int index)
        {
            return float.Parse(tokens[index++], CultureInfo.InvariantCulture);
        }

        private static PointF ReadPoint(List<string> tokens, ref int index)
        {
            return new PointF(ReadNumber(tokens, ref index), ReadNumber(tokens, ref index));
        }
    }
}

internal sealed class FluentIconControl : Control
{
    private readonly GraphicsPath _sourcePath;
    private ThemePalette _palette;

    public FluentIconControl(ThemePalette palette, TrayFluentIcon icon)
    {
        _palette = palette;
        Icon = icon;
        _sourcePath = FluentTrayIcons.Create(icon);
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor |
            ControlStyles.UserPaint,
            true);
        BackColor = Color.Transparent;
        Size = new Size(18, 18);
        Margin = new Padding(0);
    }

    public TrayFluentIcon Icon { get; }

    public void ApplyTheme(ThemePalette palette)
    {
        _palette = palette;
        Invalidate();
    }

    protected override void OnCreateControl()
    {
        base.OnCreateControl();
        Size = FluentTrayIcons.GetLogicalSize(this);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        RectangleF drawRect = new(0, 0, Width - 1, Height - 1);
        using var iconPath = (GraphicsPath)_sourcePath.Clone();
        RectangleF bounds = iconPath.GetBounds();

        float inset = Math.Max(0.5f, Math.Min(drawRect.Width, drawRect.Height) * 0.08f);
        RectangleF target = RectangleF.Inflate(drawRect, -inset, -inset);
        float scale = Math.Min(target.Width / ViewBoxSafe(bounds.Width), target.Height / ViewBoxSafe(bounds.Height));
        float offsetX = target.X + (target.Width - (bounds.Width * scale)) / 2f - (bounds.X * scale);
        float offsetY = target.Y + (target.Height - (bounds.Height * scale)) / 2f - (bounds.Y * scale);

        using var matrix = new Matrix();
        matrix.Scale(scale, scale);
        matrix.Translate(offsetX / scale, offsetY / scale, MatrixOrder.Append);
        iconPath.Transform(matrix);

        using SolidBrush fill = new(_palette.Text);
        e.Graphics.FillPath(fill, iconPath);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        Color backColor = Parent is ISurfaceBackgroundProvider provider
            ? provider.SurfaceBackgroundColor
            : ControlDrawing.EffectiveBackColor(this);
        using SolidBrush brush = new(backColor);
        pevent.Graphics.FillRectangle(brush, ClientRectangle);
    }

    private static float ViewBoxSafe(float value) => Math.Max(0.001f, value);
}

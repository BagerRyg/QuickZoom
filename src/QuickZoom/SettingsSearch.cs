using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace QuickZoom;

internal sealed record SettingsSearchEntry(
    Type PageType,
    string PageTitle,
    string Title,
    string Description,
    string Keywords)
{
    public override string ToString()
    {
        return string.IsNullOrWhiteSpace(Description)
            ? $"{Title}, {PageTitle}"
            : $"{Title}, {PageTitle}. {Description}";
    }
}

internal sealed class SettingsSearchControl : UserControl, IMessageFilter
{
    private const int WmLeftButtonDown = 0x0201;
    private const int MaximumVisibleResults = 5;
    private static readonly HashSet<string> IgnoredQueryTerms = new(StringComparer.Ordinal)
    {
        "a", "an", "the", "to", "for", "of", "in", "on", "and", "or", "my", "is", "are",
        "change", "set", "adjust", "configure", "configuration", "option", "options", "setting", "settings",
        "find", "show", "where",
        "en", "et", "den", "det", "de", "til", "af", "i", "pa", "og", "eller", "min", "mit", "mine", "er",
        "ændre", "aendre", "indstil", "juster", "konfigurer", "valg", "indstilling", "indstillinger", "find", "vis", "hvor"
    };
    private readonly ThemePalette _palette;
    private readonly IReadOnlyList<SettingsSearchEntry> _entries;
    private readonly SearchInputTextBox _input;
    private readonly SettingsSearchListBox _results;
    private readonly Label _emptyLabel;
    private readonly string _placeholderText;
    private readonly string _noResultsText;
    private Font _titleFont;
    private Font _detailFont;
    private int _fieldHeight;
    private int _resultHeight;
    private int _hoveredResultIndex = ListBox.NoMatches;
    private bool _outsideDismissQueued;

    public SettingsSearchControl(
        ThemePalette palette,
        IReadOnlyList<SettingsSearchEntry> entries,
        string placeholder,
        string noResultsText,
        string accessibleDescription)
    {
        _palette = palette;
        _entries = entries;
        _placeholderText = placeholder;
        _noResultsText = noResultsText;
        _titleFont = ControlDrawing.UiFont("Segoe UI Semibold", 9.5f, FontStyle.Bold);
        _detailFont = ControlDrawing.UiFont("Segoe UI", 8.4f, FontStyle.Regular);

        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);
        BackColor = palette.MenuBackground;
        Margin = new Padding(0, 0, 0, 8);
        TabStop = false;
        AccessibleName = placeholder;
        AccessibleDescription = accessibleDescription;
        AccessibleRole = AccessibleRole.Grouping;

        _input = new SearchInputTextBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = ControlContrast.FieldBackground(palette),
            ForeColor = palette.Text,
            Font = ControlDrawing.UiFont("Segoe UI", 9.5f, FontStyle.Regular),
            PlaceholderText = placeholder,
            TabStop = true,
            TabIndex = 0,
            AccessibleName = placeholder,
            AccessibleDescription = accessibleDescription,
            AccessibleRole = AccessibleRole.Text
        };
        _input.TextChanged += (_, _) => UpdateResults();
        _input.KeyDown += HandleSearchKeyDown;
        _input.GotFocus += (_, _) =>
        {
            LayoutChildren();
            Invalidate();
        };
        _input.LostFocus += (_, _) =>
        {
            LayoutChildren();
            Invalidate();
        };

        _results = new SettingsSearchListBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = ControlContrast.FieldBackground(palette),
            ForeColor = palette.Text,
            DrawMode = DrawMode.OwnerDrawFixed,
            IntegralHeight = false,
            TabStop = false,
            TabIndex = 1,
            Visible = false,
            AccessibleName = placeholder,
            AccessibleDescription = accessibleDescription,
            AccessibleRole = AccessibleRole.List
        };
        _results.DrawItem += DrawResult;
        _results.KeyDown += HandleSearchKeyDown;
        _results.MouseMove += (_, e) =>
        {
            int index = _results.IndexFromPoint(e.Location);
            if (index < 0 || index >= _results.Items.Count)
            {
                index = ListBox.NoMatches;
            }

            if (_hoveredResultIndex != index)
            {
                _hoveredResultIndex = index;
                _results.Invalidate();
            }
        };
        _results.MouseLeave += (_, _) =>
        {
            if (_hoveredResultIndex != ListBox.NoMatches)
            {
                _hoveredResultIndex = ListBox.NoMatches;
                _results.Invalidate();
            }
        };
        _results.MouseUp += (_, e) =>
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            int index = _results.IndexFromPoint(e.Location);
            if (index >= 0 && index < _results.Items.Count)
            {
                _results.SelectedIndex = index;
                ActivateSelectedResult();
            }
        };
        _results.GotFocus += (_, _) => Invalidate();
        _results.LostFocus += (_, _) => Invalidate();

        _emptyLabel = new Label
        {
            AutoSize = false,
            Text = noResultsText,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = _detailFont,
            ForeColor = palette.SecondaryText,
            BackColor = ControlContrast.FieldBackground(palette),
            Padding = new Padding(12, 0, 12, 0),
            Visible = false,
            AccessibleName = noResultsText,
            AccessibleRole = AccessibleRole.StaticText
        };

        Controls.Add(_results);
        Controls.Add(_emptyLabel);
        Controls.Add(_input);
        UpdateMetrics();
        WindowChrome.TrySetDarkScrollBars(_results, palette.MenuBackground.GetBrightness() < 0.5f);
        Application.AddMessageFilter(this);
    }

    public event EventHandler<SettingsSearchEntry>? ResultActivated;

    public bool ContainsSearchFocus => _input.Focused || _results.Focused;

    public void FocusSearch()
    {
        _input.Focus();
        _input.SelectAll();
    }

    internal void SetQueryForCapture(string query, bool scrollToBottom)
    {
        _input.Focus();
        _input.Text = query;
        if (scrollToBottom && _results.Items.Count > 0)
        {
            _results.TopIndex = Math.Max(0, _results.Items.Count - MaximumVisibleResults);
        }
    }

    public bool TryDismiss()
    {
        return Dismiss(focusSearch: true);
    }

    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg != WmLeftButtonDown ||
            _outsideDismissQueued ||
            (!_results.Visible && !_emptyLabel.Visible) ||
            !IsHandleCreated ||
            IsControlOrDescendant(Control.FromHandle(m.HWnd), this))
        {
            return false;
        }

        _outsideDismissQueued = true;
        BeginInvoke((MethodInvoker)(() =>
        {
            _outsideDismissQueued = false;
            if (!IsDisposed)
            {
                Dismiss(focusSearch: false);
            }
        }));
        return false;
    }

    private static bool IsControlOrDescendant(Control? control, Control ancestor)
    {
        for (Control? current = control; current != null; current = current.Parent)
        {
            if (current == ancestor)
            {
                return true;
            }
        }

        return false;
    }

    private bool Dismiss(bool focusSearch)
    {
        if (_input.TextLength == 0 && !_results.Visible && !_emptyLabel.Visible)
        {
            return false;
        }

        _input.Clear();
        HideResults();
        if (focusSearch)
        {
            _input.Focus();
        }

        return true;
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        return new Size(Math.Max(1, proposedSize.Width), Height);
    }

    protected override void OnCreateControl()
    {
        base.OnCreateControl();
        UpdateMetrics();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        _titleFont.Dispose();
        _detailFont.Dispose();
        _titleFont = ControlDrawing.UiFont("Segoe UI Semibold", 9.5f, FontStyle.Bold);
        _detailFont = ControlDrawing.UiFont("Segoe UI", 8.4f, FontStyle.Regular);
        _emptyLabel.Font = _detailFont;
        UpdateMetrics();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutChildren();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using SolidBrush brush = new(_palette.MenuBackground);
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        Rectangle fieldBounds = new(0, 0, Math.Max(1, Width - 1), Math.Max(1, _fieldHeight - 1));
        using GraphicsPath fieldPath = ControlDrawing.RoundedRect(fieldBounds, ControlDrawing.ScaleLogical(this, 12));
        using SolidBrush fieldBrush = new(ControlContrast.FieldBackground(_palette));
        using Pen fieldBorder = new(ControlContrast.FieldBorder(_palette), 1f);
        e.Graphics.FillPath(fieldBrush, fieldPath);
        e.Graphics.DrawPath(fieldBorder, fieldPath);

        int iconSize = ControlDrawing.ScaleLogical(this, 16);
        int iconLeft = ControlDrawing.ScaleLogical(this, 14);
        int iconTop = (_fieldHeight - iconSize) / 2;
        Rectangle iconBounds = new(iconLeft, iconTop, iconSize, iconSize);
        using Pen iconPen = new(_palette.SecondaryText, Math.Max(1.5f, ControlDrawing.ScaleLogical(this, 1)));
        int lensSize = Math.Max(6, iconSize - ControlDrawing.ScaleLogical(this, 5));
        Rectangle lensBounds = new(iconBounds.Left, iconBounds.Top, lensSize, lensSize);
        e.Graphics.DrawEllipse(iconPen, lensBounds);
        e.Graphics.DrawLine(
            iconPen,
            lensBounds.Right - 1,
            lensBounds.Bottom - 1,
            iconBounds.Right,
            iconBounds.Bottom);

        if (_input.TextLength == 0 && !_input.Focused)
        {
            Rectangle placeholderBounds = new(
                ControlDrawing.ScaleLogical(this, 42),
                0,
                Math.Max(1, Width - ControlDrawing.ScaleLogical(this, 56)),
                _fieldHeight);
            TextRenderer.DrawText(
                e.Graphics,
                _placeholderText,
                _input.Font,
                placeholderBounds,
                _palette.SecondaryText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }

        if ((ContainsSearchFocus && (ShowFocusCues || AccessibilityPreferences.HighContrast)) ||
            ReferenceEquals(ControlDrawing.FocusCaptureTarget, _input))
        {
            ControlDrawing.DrawFocusRing(
                e.Graphics,
                new Rectangle(3, 3, Width - 7, _fieldHeight - 7),
                ControlDrawing.ScaleLogical(this, 10),
                _palette);
        }

        if (_results.Visible || _emptyLabel.Visible)
        {
            Rectangle resultsBounds = new(0, _fieldHeight + ControlDrawing.ScaleLogical(this, 6), Width - 1, Height - _fieldHeight - ControlDrawing.ScaleLogical(this, 6) - 1);
            if (resultsBounds.Width > 2 && resultsBounds.Height > 2)
            {
                using GraphicsPath resultsPath = ControlDrawing.RoundedRect(resultsBounds, ControlDrawing.ScaleLogical(this, 12));
                using Pen resultsBorder = new(ControlContrast.FieldBorder(_palette), 1f);
                e.Graphics.DrawPath(resultsBorder, resultsPath);
            }
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && e.Y >= 0 && e.Y < _fieldHeight)
        {
            FocusSearch();
        }

        base.OnMouseDown(e);
    }

    private void HandleSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Down or Keys.Up)
        {
            if (_results.Items.Count > 0)
            {
                int direction = e.KeyCode == Keys.Down ? 1 : -1;
                int current = _results.SelectedIndex;
                _results.SelectedIndex = current < 0
                    ? (direction > 0 ? 0 : _results.Items.Count - 1)
                    : (current + direction + _results.Items.Count) % _results.Items.Count;
            }

            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        if (e.KeyCode == Keys.Enter)
        {
            ActivateSelectedResult();
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        if (e.KeyCode == Keys.Escape && TryDismiss())
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    private void UpdateResults()
    {
        LayoutChildren();
        Invalidate();
        string query = _input.Text.Trim();
        if (query.Length == 0)
        {
            HideResults();
            return;
        }

        IReadOnlyList<SettingsSearchEntry> matches = FindMatches(_entries, query);

        _results.BeginUpdate();
        _results.Items.Clear();
        foreach (SettingsSearchEntry entry in matches)
        {
            _results.Items.Add(entry);
        }
        _results.EndUpdate();

        _results.Visible = matches.Count > 0;
        _results.TabStop = matches.Count > 0;
        _emptyLabel.Text = _noResultsText;
        _emptyLabel.Visible = matches.Count == 0;
        if (matches.Count > 0)
        {
            _results.SelectedIndex = 0;
        }

        UpdateExpandedHeight();
        AccessibilityNotifyClients(AccessibleEvents.Reorder, -1);
        Invalidate();
    }

    internal static IReadOnlyList<SettingsSearchEntry> FindMatches(
        IReadOnlyList<SettingsSearchEntry> entries,
        string query)
    {
        string[] rawTerms = Normalize(query)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string[] meaningfulTerms = rawTerms
            .Where(term => !IgnoredQueryTerms.Contains(term))
            .ToArray();
        IReadOnlyList<string> terms = meaningfulTerms.Length > 0 ? meaningfulTerms : rawTerms;
        if (terms.Count == 0)
        {
            return [];
        }

        return entries
            .Select(entry => (Entry: entry, Score: ScoreEntry(entry, terms)))
            .Where(match => match.Score < int.MaxValue)
            .OrderBy(match => match.Score)
            .ThenBy(match => match.Entry.PageTitle, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(match => match.Entry.Title, StringComparer.CurrentCultureIgnoreCase)
            .Select(match => match.Entry)
            .ToList();
    }

    private static int ScoreEntry(SettingsSearchEntry entry, IReadOnlyList<string> terms)
    {
        string title = Normalize(entry.Title);
        string page = Normalize(entry.PageTitle);
        string description = Normalize(entry.Description);
        string keywords = Normalize(entry.Keywords);
        string all = string.Join(' ', title, page, description, keywords);
        if (terms.Any(term => !TermMatches(all, term)))
        {
            return int.MaxValue;
        }

        int score = 0;
        foreach (string term in terms)
        {
            score += title.StartsWith(term, StringComparison.Ordinal) ? 0
                : title.Contains(term, StringComparison.Ordinal) ? 10
                : TermMatches(title, term) ? 18
                : page.Contains(term, StringComparison.Ordinal) ? 24
                : TermMatches(page, term) ? 30
                : description.Contains(term, StringComparison.Ordinal) ? 36
                : TermMatches(description, term) ? 42
                : keywords.Contains(term, StringComparison.Ordinal) ? 50
                : 58;
        }

        return score + Math.Min(20, title.Length / 4);
    }

    private static bool TermMatches(string text, string term)
    {
        if (text.Contains(term, StringComparison.Ordinal))
        {
            return true;
        }

        if (term.Length < 4)
        {
            return false;
        }

        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (word.Length >= 4 &&
                (word.StartsWith(term, StringComparison.Ordinal) || term.StartsWith(word, StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (char character in decomposed)
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
            else if (builder.Length > 0 && builder[^1] != ' ')
            {
                builder.Append(' ');
            }
        }

        return builder.ToString().Trim().Normalize(NormalizationForm.FormC);
    }

    private void ActivateSelectedResult()
    {
        if (_results.Items.Count == 0)
        {
            return;
        }

        if (_results.SelectedIndex < 0)
        {
            _results.SelectedIndex = 0;
        }

        if (_results.SelectedItem is not SettingsSearchEntry entry)
        {
            return;
        }

        _input.Clear();
        HideResults();
        ResultActivated?.Invoke(this, entry);
    }

    private void DrawResult(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _results.Items.Count || _results.Items[e.Index] is not SettingsSearchEntry entry)
        {
            return;
        }

        bool selected = (e.State & DrawItemState.Selected) != 0;
        bool highlighted = _hoveredResultIndex == ListBox.NoMatches
            ? selected
            : e.Index == _hoveredResultIndex;
        Color background = highlighted ? ControlContrast.FieldHover(_palette) : ControlContrast.FieldBackground(_palette);
        Color titleColor = AccessibilityPreferences.HighContrast && highlighted ? SystemColors.HighlightText : _palette.Text;
        Color detailColor = AccessibilityPreferences.HighContrast && highlighted ? SystemColors.HighlightText : _palette.SecondaryText;
        using SolidBrush backgroundBrush = new(background);
        e.Graphics.FillRectangle(backgroundBrush, e.Bounds);

        int horizontalPadding = ControlDrawing.ScaleLogical(this, 12);
        int verticalPadding = ControlDrawing.ScaleLogical(this, 6);
        Rectangle titleBounds = new(
            e.Bounds.Left + horizontalPadding,
            e.Bounds.Top + verticalPadding,
            Math.Max(1, e.Bounds.Width - (horizontalPadding * 2)),
            _titleFont.Height + 2);
        Rectangle detailBounds = new(
            titleBounds.Left,
            titleBounds.Bottom + ControlDrawing.ScaleLogical(this, 2),
            titleBounds.Width,
            _detailFont.Height + 2);
        TextRenderer.DrawText(
            e.Graphics,
            entry.Title,
            _titleFont,
            titleBounds,
            titleColor,
            TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        string detail = string.IsNullOrWhiteSpace(entry.Description)
            ? entry.PageTitle
            : $"{entry.PageTitle}  •  {entry.Description}";
        TextRenderer.DrawText(
            e.Graphics,
            detail,
            _detailFont,
            detailBounds,
            detailColor,
            TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

        if (selected && (e.State & DrawItemState.Focus) != 0)
        {
            using Pen focusPen = new(ControlDrawing.FocusColor(_palette), AccessibilityPreferences.HighContrast ? 2f : 1.5f);
            Rectangle focusBounds = Rectangle.Inflate(e.Bounds, -2, -2);
            e.Graphics.DrawRectangle(focusPen, focusBounds);
        }
    }

    private void HideResults()
    {
        _hoveredResultIndex = ListBox.NoMatches;
        _results.Visible = false;
        _results.TabStop = false;
        _emptyLabel.Visible = false;
        _results.Items.Clear();
        UpdateExpandedHeight();
        Invalidate();
    }

    private void UpdateMetrics()
    {
        _fieldHeight = ControlDrawing.ScaleLogical(this, 38);
        _resultHeight = Math.Max(ControlDrawing.ScaleLogical(this, 48), _titleFont.Height + _detailFont.Height + ControlDrawing.ScaleLogical(this, 12));
        _results.ItemHeight = _resultHeight;
        UpdateExpandedHeight();
        LayoutChildren();
    }

    private void UpdateExpandedHeight()
    {
        int gap = ControlDrawing.ScaleLogical(this, 6);
        int resultsHeight = 0;
        if (_results.Visible)
        {
            int visibleResultCount = Math.Min(MaximumVisibleResults, Math.Max(1, _results.Items.Count));
            resultsHeight = (visibleResultCount * Math.Max(1, _results.ItemHeight)) + 2;
        }
        else if (_emptyLabel.Visible)
        {
            resultsHeight = ControlDrawing.ScaleLogical(this, 44);
        }

        int nextHeight = _fieldHeight + (resultsHeight > 0 ? gap + resultsHeight : 0);
        if (Height != nextHeight)
        {
            Height = nextHeight;
        }

        LayoutChildren();
    }

    private void LayoutChildren()
    {
        if (_fieldHeight <= 0 || Width <= 0)
        {
            return;
        }

        int leftInset = ControlDrawing.ScaleLogical(this, 42);
        int rightInset = ControlDrawing.ScaleLogical(this, 14);
        int inputHeight = Math.Max(_input.PreferredHeight, _input.Font.Height + 4);
        bool showPaintedPlaceholder = _input.TextLength == 0 && !_input.Focused;
        _input.Bounds = showPaintedPlaceholder
            ? new Rectangle(Math.Max(0, Width - rightInset - 1), Math.Max(1, (_fieldHeight - inputHeight) / 2), 1, inputHeight)
            : new Rectangle(
                leftInset,
                Math.Max(1, (_fieldHeight - inputHeight) / 2),
                Math.Max(1, Width - leftInset - rightInset),
                inputHeight);

        int resultsTop = _fieldHeight + ControlDrawing.ScaleLogical(this, 6);
        int resultsHeight = Math.Max(0, Height - resultsTop);
        _results.Bounds = new Rectangle(1, resultsTop + 1, Math.Max(1, Width - 2), Math.Max(1, resultsHeight - 2));
        _emptyLabel.Bounds = _results.Bounds;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Application.RemoveMessageFilter(this);
            _titleFont.Dispose();
            _detailFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private sealed class SearchInputTextBox : ClipboardFreeTextBox
    {
        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            return key is Keys.Enter or Keys.Escape or Keys.Up or Keys.Down || base.IsInputKey(keyData);
        }
    }
}

internal sealed class SettingsSearchListBox : ListBox
{
    private const int WmMouseMove = 0x0200;
    private const int WmMouseWheel = 0x020A;
    private const uint TrackMouseLeave = 0x00000002;
    private int _wheelDeltaRemainder;
    private bool _trackingMouseLeave;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct TrackMouseEventData
    {
        public uint Size;
        public uint Flags;
        public IntPtr WindowHandle;
        public uint HoverTime;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool TrackMouseEvent(ref TrackMouseEventData eventData);

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmMouseMove && Control.MouseButtons == MouseButtons.None)
        {
            EnsureMouseLeaveTracking();
            Point location = PointToClient(Cursor.Position);
            OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, location.X, location.Y, 0));
            return;
        }

        if (m.Msg == WmMouseWheel)
        {
            int delta = unchecked((short)((m.WParam.ToInt64() >> 16) & 0xffff));
            ScrollFromWheel(delta);
            return;
        }

        base.WndProc(ref m);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _trackingMouseLeave = false;
        base.OnMouseLeave(e);
    }

    private void EnsureMouseLeaveTracking()
    {
        if (_trackingMouseLeave || !IsHandleCreated)
        {
            return;
        }

        var eventData = new TrackMouseEventData
        {
            Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<TrackMouseEventData>(),
            Flags = TrackMouseLeave,
            WindowHandle = Handle
        };
        _trackingMouseLeave = TrackMouseEvent(ref eventData);
    }

    private void ScrollFromWheel(int delta)
    {
        if (delta == 0 || Items.Count == 0)
        {
            return;
        }

        _wheelDeltaRemainder += delta;
        int detents = _wheelDeltaRemainder / SystemInformation.MouseWheelScrollDelta;
        if (detents == 0)
        {
            return;
        }

        _wheelDeltaRemainder %= SystemInformation.MouseWheelScrollDelta;
        int visibleItems = Math.Max(1, ClientSize.Height / Math.Max(1, ItemHeight));
        int configuredLines = SystemInformation.MouseWheelScrollLines;
        int itemsPerDetent = configuredLines < 0
            ? visibleItems
            : Math.Max(1, configuredLines);
        int maximumTopIndex = Math.Max(0, Items.Count - visibleItems);
        int nextTopIndex = Math.Clamp(TopIndex - (detents * itemsPerDetent), 0, maximumTopIndex);
        if (TopIndex != nextTopIndex)
        {
            TopIndex = nextTopIndex;
        }
    }
}

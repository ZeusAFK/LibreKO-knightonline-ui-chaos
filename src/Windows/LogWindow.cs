using Godot;
using KnightOnlineUiChaos.Layout;
using LibreKO;
using LibreKO.Plugins;

namespace KnightOnlineUiChaos.Windows;

public partial class LogWindow : Control
{
    private const string LayoutName = "co_information_box_us";
    private const string TextId = "text_message";
    private const string BackgroundId = "img_background";
    private const string OpaqueBackgroundId = "img_background2";
    private const string OptionButton = "btn_option";
    private const string ScrollGroup = "scroll";
    private const string LayoutId = "theme_log";
    private const string SettingShadeIndex = "log.shadeIndex";
    private const int DefaultShadeIndex = 1;
    private static readonly float[] ShadeLevels = { 0.75f, 0.5f, 0.25f, 0f };
    private const float ScreenMargin = 8f;
    private const float GapAboveBar = 4f;
    private const float ScrollStep = 32f;
    private const int DefaultFontSize = 11;

    private readonly LayoutView _view;
    private readonly PluginGame _game;
    private readonly PluginSettings _settings;
    private readonly LogText _log;
    private readonly ColorRect _shade;
    private readonly Control? _dragButton;
    private readonly Vector2 _baseSize;
    private readonly LayoutNode _layoutRoot;
    private VerticalGrow? _grow;
    private HorizontalGrow? _widen;
    private bool _relaying;
    private HudLayout? _hudLayout;

    private static readonly HashSet<string> StretchIds = new() { "base_msgwnd", "img_background", "img_background2", "scroll", "scroll_bar", "img_scrollback", "img_scroll3", "text_message", "base_area" };

    private static readonly HashSet<string> ShiftIds = new() { "img_scroll1", "btn_msgwnd_lock", "base_option" };

    private static readonly HashSet<string> WidenIds = new() { "base_msgwnd", "img_background", "img_background2", "text_message", "base_area" };

    private static readonly Vector2 MaximumScreenShare = new(0.3f, 0.7f);
    private int _shadeIndex;

    public LogWindow()
    {
        var kit = Plugin.Kit;
        _game = kit.Game;
        var layout = kit.Layout(LayoutName);
        _layoutRoot = layout;
        _view = new LayoutView(kit, layout);
        MouseFilter = MouseFilterEnum.Ignore;
        Size = layout.SizeVec;
        AddChild(_view);
        _settings = kit.Context.Settings;
        _view.Hide("img_sizechange", "img_sizeline", "btn_msgwnd_lock");
        _view.FadeOut(OpaqueBackgroundId, BackgroundId);
        var background = _view.Get(BackgroundId);
        _shade = new ColorRect
        {
            Position = background?.Position ?? Vector2.Zero,
            Size = background?.Size ?? layout.SizeVec,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        (background?.GetParent() ?? (Node)_view).AddChild(_shade);
        if (background != null) _shade.GetParent().MoveChild(_shade, background.GetIndex() + 1);
        _shadeIndex = Math.Clamp(_settings.GetInt(SettingShadeIndex, DefaultShadeIndex), 0, ShadeLevels.Length - 1);
        ApplyShade();
        _dragButton = _view.Get(OptionButton);

        var area = _view.Get(TextId);
        var node = layout.Find(TextId);
        _log = new LogText(node != null ? UiKit.FontSize(node) : DefaultFontSize, node?.Color ?? Colors.White, kit.Regular)
        {
            Position = area?.Position ?? Vector2.Zero,
            Size = area?.Size ?? new Vector2(248, 130),
            MouseFilter = MouseFilterEnum.Stop,
        };
        if (area != null) area.Visible = false;
        (area?.GetParent() ?? (Node)_view).AddChild(_log);

        if (layout.Find(ScrollGroup) is { } scroll)
        {
            _view.OnPressed(scroll, "btn_scroll_Up", () => _log.Scroll(ScrollStep));
            foreach (var child in scroll.Children)
                if (child.IsButton && child.Id.Length == 0 && _view.ControlOf(child) is BaseButton down)
                    down.Pressed += () => _log.Scroll(-ScrollStep);
        }

        _baseSize = Size;
        Resized += Relayout;
        Callable.From(CaptureGrow).CallDeferred();
    }

    private void CaptureGrow()
    {
        var grow = new VerticalGrow();
        grow.Add(_view, VerticalGrow.Mode.Stretch);
        var scrollDown = _layoutRoot.Find("scroll") is { } scroll
            ? scroll.Children.Where(n => n.IsButton && n.Id.Length == 0).Select(n => _view.ControlOf(n)).FirstOrDefault()
            : null;
        foreach (var (node, control) in _view.Where(_ => true))
            grow.Add(control, control == scrollDown ? VerticalGrow.Mode.Shift
                : StretchIds.Contains(node.Id) ? VerticalGrow.Mode.Stretch
                : ShiftIds.Contains(node.Id) ? VerticalGrow.Mode.Shift
                : VerticalGrow.Mode.Stay);
        grow.Add(_shade, VerticalGrow.Mode.Stretch);
        grow.Add(_log, VerticalGrow.Mode.Stretch);
        foreach (var parent in grow.Stretched.Where(c => c != _shade && c != _log))
            grow.ShiftBottomBand(parent);
        _grow = grow;

        var widen = new HorizontalGrow();
        widen.Add(_view, HorizontalGrow.Mode.Stretch);
        foreach (var (node, control) in _view.Where(_ => true))
            widen.Add(control, WidenIds.Contains(node.Id) ? HorizontalGrow.Mode.Stretch : HorizontalGrow.Mode.Stay);
        widen.Add(_shade, HorizontalGrow.Mode.Stretch);
        widen.Add(_log, HorizontalGrow.Mode.Stretch);
        foreach (var parent in widen.Stretched.Where(c => c != _shade && c != _log))
            widen.ShiftRightHalf(parent);
        _widen = widen;
        Relayout();
    }

    private void Relayout()
    {
        if (_grow == null || _relaying) return;
        _relaying = true;
        _grow.Apply(Mathf.Max(0f, Size.Y - _baseSize.Y));
        _widen?.Apply(Mathf.Max(0f, Size.X - _baseSize.X));
        _relaying = false;
    }

    public override void _EnterTree()
    {
        _game.Log.LineAdded += OnLine;
        _game.BecameAvailable += Reload;
        _hudLayout ??= HudLayout.Attach(this, LayoutId, _dragButton, DefaultPosition, resizable: true,
            defaultSize: _baseSize, minimumSize: _baseSize, resizeCorner: HudLayout.Corner.TopLeft,
            backgroundOpacityChanged: _ => CycleShade());
        _hudLayout.MaximumViewportShare = MaximumScreenShare;
        Reload();
    }

    public override void _ExitTree()
    {
        _game.Log.LineAdded -= OnLine;
        _game.BecameAvailable -= Reload;
    }

    private Vector2 DefaultPosition()
    {
        var room = GetViewport().GetVisibleRect().Size;
        return new Vector2(room.X - Size.X - ScreenMargin, room.Y - Taskbar.BarHeight - Size.Y - GapAboveBar);
    }

    private void CycleShade()
    {
        _shadeIndex = (_shadeIndex + 1) % ShadeLevels.Length;
        _settings.SetInt(SettingShadeIndex, _shadeIndex);
        ApplyShade();
    }

    private void ApplyShade() => _shade.Color = new Color(0, 0, 0, ShadeLevels[_shadeIndex]);

    private void Reload() => _log.Set(_game.Log.History.Select(Format));

    private void OnLine(GameLogLine line) => _log.Append(Format(line));

    private static string Format(GameLogLine line) =>
        $"[color=#{line.Color.ToHtml(false)}]{line.Text.Replace("[", "[lb]")}[/color]";
}

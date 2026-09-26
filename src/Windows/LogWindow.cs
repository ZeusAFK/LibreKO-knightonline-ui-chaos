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
    private HudLayout? _hudLayout;
    private int _shadeIndex;

    public LogWindow()
    {
        var kit = Plugin.Kit;
        _game = kit.Game;
        var layout = kit.Layout(LayoutName);
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
    }

    public override void _EnterTree()
    {
        _game.Log.LineAdded += OnLine;
        _game.BecameAvailable += Reload;
        _hudLayout ??= HudLayout.Attach(this, LayoutId, _dragButton, DefaultPosition, backgroundOpacityChanged: _ => CycleShade());
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

using Godot;
using LibreKO.Domain;
using KnightOnlineUiChaos.Layout;
using LibreKO.Plugins;

namespace KnightOnlineUiChaos.Windows;

public partial class WhisperWindow : Control
{
    private const string OpenLayout = "co_whisper_open_us";
    private const string CollapsedLayout = "co_whisper_close_us";
    private const string ChatArea = "exit_chat";
    private const string ScrollGroup = "scroll";
    private const float ScrollStep = 24f;
    private const float BlinkSeconds = 0.5f;
    private const int TitleBarHeight = 21;
    private const int DefaultFontSize = 12;
    private static readonly Color MineColour = new(0.5f, 1f, 1f);
    private static readonly Color TheirsColour = new(1f, 1f, 0f);
    private static readonly Color NoticeColour = new(1f, 1f, 1f);

    private readonly WindowHost _host;
    private readonly PluginGame _game;
    private readonly string _name;
    private readonly Control _drag = new() { MouseFilter = MouseFilterEnum.Stop };
    private LayoutView _view = null!;
    private LogText? _log;
    private LineEdit? _input;
    private bool _collapsed;
    private Tween? _blink;

    public WhisperWindow(WindowHost host)
    {
        _host = host;
        _game = Plugin.Kit.Game;
        _name = host.Title;
        _collapsed = _game.Whispers.StartsCollapsed(_name);
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(_drag);
        host.SetDragHandle(_drag);
        Build();
    }

    public override void _EnterTree()
    {
        _game.Whispers.LineAdded += OnLine;
        _game.Whispers.Changed += OnChanged;
    }

    public override void _ExitTree()
    {
        _game.Whispers.LineAdded -= OnLine;
        _game.Whispers.Changed -= OnChanged;
        _game.Chat.SetTyping(false);
    }

    private void Build()
    {
        var kit = Plugin.Kit;
        _blink?.Kill();
        _blink = null;
        if (_view != null) _view.QueueFree();
        var layout = kit.Layout(_collapsed ? CollapsedLayout : OpenLayout);
        _view = new LayoutView(kit, layout);
        AddChild(_view);
        Size = _view.Size;
        CustomMinimumSize = _view.Size;
        Modulate = new Color(1f, 1f, 1f, _game.Whispers.Alpha);
        _drag.Size = new Vector2(_view.Size.X, Mathf.Min(TitleBarHeight, _view.Size.Y));
        Callable.From(_host.Window.ResetSize).CallDeferred();
        if (_view.Get("btn_bar") is { } bar) bar.MouseFilter = MouseFilterEnum.Ignore;
        _view.SetText("exit_id", _name);
        if (_collapsed)
        {
            _log = null;
            _input = null;
            _view.OnPressed("btn_open", () => SetCollapsed(false));
            return;
        }

        _view.Hide("btn_chat");
        _view.OnPressed("btn_close", _host.Close);
        _view.OnPressed("btn_hide", () => SetCollapsed(true));
        var area = _view.Get(ChatArea);
        var areaNode = layout.Find(ChatArea);
        _log = new LogText(areaNode != null ? UiKit.FontSize(areaNode) : DefaultFontSize, areaNode?.Color ?? Colors.White, kit.Regular)
        {
            Position = area?.Position ?? Vector2.Zero,
            Size = area?.Size ?? new Vector2(270, 150),
            MouseFilter = MouseFilterEnum.Stop,
        };
        if (area != null) area.Visible = false;
        (area?.GetParent() ?? (Node)_view).AddChild(_log);
        _log.EnableLinks(link =>
        {
            if (WhisperLinks.IsWebLink(link)) OS.ShellOpen(link);
        });
        _log.Set(_game.Whispers.History(_name).Select(Format));

        if (layout.Find(ScrollGroup) is { } scroll)
        {
            var buttons = scroll.Children.Where(n => n.IsButton).ToList();
            if (buttons.Count > 0 && _view.ControlOf(buttons[0]) is BaseButton up) up.Pressed += () => _log.Scroll(ScrollStep);
            if (buttons.Count > 1 && _view.ControlOf(buttons[^1]) is BaseButton down) down.Pressed += () => _log.Scroll(-ScrollStep);
        }

        _input = _view.Get<LineEdit>("edit_chat");
        if (_input != null)
        {
            _input.AddThemeFontOverride("font", kit.Regular);
            _input.MaxLength = 128;
            _input.TextSubmitted += Submit;
            _input.FocusEntered += () => _game.Chat.SetTyping(true);
            _input.FocusExited += () => _game.Chat.SetTyping(false);
        }
    }

    private string Format(GameWhisperLine line)
    {
        var colour = line.Notice ? NoticeColour : line.Mine ? MineColour : TheirsColour;
        if (line.Notice) return $"[color=#{colour.ToHtml(false)}]{WhisperLinks.Linkify(line.Text)}[/color]";
        string who = line.Mine ? _game.Character.Name : _name;
        return $"[color=#{colour.ToHtml(false)}]{Escape(who)} : {Escape(line.Text)}[/color]";
    }

    private static string Escape(string text) => text.Replace("[", "[lb]");

    private void Submit(string text)
    {
        if (text.Trim().Length == 0) return;
        _game.Whispers.Send(_name, text);
        _input?.Clear();
    }

    private void OnLine(string name, GameWhisperLine line)
    {
        if (!string.Equals(name, _name, System.StringComparison.OrdinalIgnoreCase)) return;
        if (_log != null) _log.Append(Format(line));
        else if (!line.Mine) Blink();
    }

    private void OnChanged() => Modulate = new Color(1f, 1f, 1f, _game.Whispers.Alpha);

    private void SetCollapsed(bool collapsed)
    {
        if (_collapsed == collapsed) return;
        _collapsed = collapsed;
        Build();
        if (!collapsed) _input?.CallDeferred(Control.MethodName.GrabFocus);
    }

    private void Blink()
    {
        _blink?.Kill();
        _view.Modulate = Colors.White;
        _blink = _view.CreateTween().SetLoops();
        _blink.TweenProperty(_view, "modulate:a", 0.35f, BlinkSeconds);
        _blink.TweenProperty(_view, "modulate:a", 1f, BlinkSeconds);
    }

    public override void _GuiInput(InputEvent ev)
    {
        if (ev is not InputEventMouseButton { Pressed: true } || _blink == null) return;
        _blink.Kill();
        _blink = null;
        _view.Modulate = Colors.White;
    }
}

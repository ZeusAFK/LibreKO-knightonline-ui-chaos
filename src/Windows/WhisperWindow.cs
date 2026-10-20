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

    private readonly WindowHost host;
    private readonly PluginGame game;
    private readonly string name;
    private readonly Control drag = new() { MouseFilter = MouseFilterEnum.Stop };
    private LayoutView view = null!;
    private LogText? log;
    private LineEdit? input;
    private bool collapsed;
    private Tween? blink;

    public WhisperWindow(WindowHost host)
    {
        this.host = host;
        game = Plugin.Kit.Game;
        name = host.Title;
        collapsed = game.Whispers.StartsCollapsed(name);
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(drag);
        host.SetDragHandle(drag);
        Build();
    }

    public override void _EnterTree()
    {
        game.Whispers.LineAdded += OnLine;
        game.Whispers.Changed += OnChanged;
        game.Whispers.CollapseRequested += OnCollapseRequested;
        game.Whispers.SetCollapsed(name, collapsed);
    }

    public override void _ExitTree()
    {
        game.Whispers.LineAdded -= OnLine;
        game.Whispers.Changed -= OnChanged;
        game.Whispers.CollapseRequested -= OnCollapseRequested;
        game.Chat.SetTyping(false);
    }

    private void OnCollapseRequested(string name)
    {
        if (!string.Equals(name, this.name, System.StringComparison.OrdinalIgnoreCase)) return;
        input?.ReleaseFocus();
        SetCollapsed(true);
    }

    private void Build()
    {
        var kit = Plugin.Kit;
        blink?.Kill();
        blink = null;
        if (view != null) view.QueueFree();
        var layout = kit.Layout(collapsed ? CollapsedLayout : OpenLayout);
        view = new LayoutView(kit, layout);
        AddChild(view);
        Size = view.Size;
        CustomMinimumSize = view.Size;
        Modulate = new Color(1f, 1f, 1f, game.Whispers.Alpha);
        drag.Size = new Vector2(view.Size.X, Mathf.Min(TitleBarHeight, view.Size.Y));
        Callable.From(host.Window.ResetSize).CallDeferred();
        if (view.Get("btn_bar") is { } bar) bar.MouseFilter = MouseFilterEnum.Ignore;
        view.SetText("exit_id", name);
        if (collapsed)
        {
            log = null;
            input = null;
            view.OnPressed("btn_open", () => SetCollapsed(false));
            return;
        }

        view.Hide("btn_chat");
        view.OnPressed("btn_close", host.Close);
        view.OnPressed("btn_hide", () => SetCollapsed(true));
        var area = view.Get(ChatArea);
        var areaNode = layout.Find(ChatArea);
        log = new LogText(areaNode != null ? UiKit.FontSize(areaNode) : DefaultFontSize, areaNode?.Color ?? Colors.White, kit.Regular)
        {
            Position = area?.Position ?? Vector2.Zero,
            Size = area?.Size ?? new Vector2(270, 150),
            MouseFilter = MouseFilterEnum.Stop,
        };
        if (area != null) area.Visible = false;
        (area?.GetParent() ?? (Node)view).AddChild(log);
        log.EnableLinks(link =>
        {
            if (WhisperLinks.IsWebLink(link)) OS.ShellOpen(link);
        });
        log.Set(game.Whispers.History(name).Select(Format));

        if (layout.Find(ScrollGroup) is { } scroll)
        {
            var buttons = scroll.Children.Where(n => n.IsButton).ToList();
            if (buttons.Count > 0 && view.ControlOf(buttons[0]) is BaseButton up) up.Pressed += () => log.Scroll(ScrollStep);
            if (buttons.Count > 1 && view.ControlOf(buttons[^1]) is BaseButton down) down.Pressed += () => log.Scroll(-ScrollStep);
        }

        input = view.Get<LineEdit>("edit_chat");
        if (input != null)
        {
            input.AddThemeFontOverride("font", kit.Regular);
            input.MaxLength = 128;
            input.TextSubmitted += Submit;
            input.FocusEntered += () => game.Chat.SetTyping(true);
            input.FocusExited += () => game.Chat.SetTyping(false);
        }
    }

    private string Format(GameWhisperLine line)
    {
        var colour = line.Notice ? NoticeColour : line.Mine ? MineColour : TheirsColour;
        if (line.Notice) return $"[color=#{colour.ToHtml(false)}]{WhisperLinks.Linkify(line.Text)}[/color]";
        string who = line.Mine ? game.Character.Name : name;
        return $"[color=#{colour.ToHtml(false)}]{Escape(who)} : {Escape(line.Text)}[/color]";
    }

    private static string Escape(string text) => text.Replace("[", "[lb]");

    private void Submit(string text)
    {
        if (text.Trim().Length == 0) return;
        game.Whispers.Send(name, text);
        input?.Clear();
    }

    private void OnLine(string name, GameWhisperLine line)
    {
        if (!string.Equals(name, this.name, System.StringComparison.OrdinalIgnoreCase)) return;
        if (log != null) log.Append(Format(line));
        else if (!line.Mine) Blink();
    }

    private void OnChanged() => Modulate = new Color(1f, 1f, 1f, game.Whispers.Alpha);

    private void SetCollapsed(bool collapsed)
    {
        if (this.collapsed == collapsed) return;
        this.collapsed = collapsed;
        game.Whispers.SetCollapsed(name, collapsed);
        Build();
        if (!collapsed) input?.CallDeferred(Control.MethodName.GrabFocus);
    }

    private void Blink()
    {
        blink?.Kill();
        view.Modulate = Colors.White;
        blink = view.CreateTween().SetLoops();
        blink.TweenProperty(view, "modulate:a", 0.35f, BlinkSeconds);
        blink.TweenProperty(view, "modulate:a", 1f, BlinkSeconds);
    }

    public override void _GuiInput(InputEvent ev)
    {
        if (ev is not InputEventMouseButton { Pressed: true } || blink == null) return;
        blink.Kill();
        blink = null;
        view.Modulate = Colors.White;
    }
}

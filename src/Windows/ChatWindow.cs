using Godot;
using KnightOnlineUiChaos.Layout;
using LibreKO;
using LibreKO.Plugins;

namespace KnightOnlineUiChaos.Windows;

public partial class ChatWindow : Control
{
    private const string LayoutName = "co_chatting_box_us";
    private const string TextArea = "text0";
    private const string TextGroup = "base_chat";
    private const string InputId = "edit0";
    private const string InputBackgroundId = "img_edit_bg";
    private const string ScrollGroup = "scroll";
    private const string BackgroundId = "img_background";
    private const string OpaqueBackgroundId = "img_background2";
    private const string UserCheckPrefix = "btn_check_user_";
    private const string OptionButton = "btn_option";
    private const string WhisperButton = "btn_private";
    private const string WhisperPrefix = "@";
    private const string LayoutId = "theme_chat";
    private const string SettingShadeIndex = "chat.shadeIndex";
    private const int DefaultShadeIndex = 1;
    private static readonly float[] ShadeLevels = { 0.75f, 0.5f, 0.25f, 0f };
    private const float ScreenMargin = 4f;
    private const float ScrollStep = 32f;
    private const int DefaultFontSize = 12;
    private const string PrefixChars = "@!#$%&~|\\/+";
    private static readonly string[] HiddenIds =
    {
        "img_sizechange", "img_sizeline", "img_edit2", "btn_knights", "btn_force", "btn_filter", "btn_chat_lock", "base_filter",
    };
    private static readonly string[] InputRowIds = { "img_edit1", "img_edit_bg" };

    private static readonly (string Button, string Prefix)[] ChannelTabs =
    {
        ("btn_normal", ""),
        ("btn_shout", "!"),
        ("btn_party_force", "#"),
        ("btn_clan", "$"),
        ("btn_union", "&"),
    };

    private readonly LayoutView view;
    private readonly PluginGame game;
    private readonly PluginSettings settings;
    private readonly LogText log;
    private readonly LineEdit? input;
    private readonly ColorRect shade;
    private readonly Control? dragButton;
    private readonly Vector2 baseSize;
    private Control? scrollDown;
    private VerticalGrow? grow;
    private bool relaying;
    private HudLayout? hudLayout;

    private static readonly HashSet<string> StretchIds = new()
    {
        "base_chat", "base_area", "base_button_area", "img_background", "img_background2", "scroll", "scroll_bar",
        "img_scrollback", "img_scroll3", "text0",
    };

    private static readonly HashSet<string> ShiftIds = new()
    {
        "img_edit_bg", "img_edit1", "img_edit2", "edit0", "btn_clan", "btn_knights", "btn_party_force", "btn_private",
        "btn_normal", "btn_shout", "btn_union", "btn_force", "btn_chat_lock", "base_option", "img_scroll1",
    };
    private int shadeIndex;
    private string prefix = "";

    public ChatWindow()
    {
        var kit = Plugin.Kit;
        game = kit.Game;
        var layout = kit.Layout(LayoutName);
        view = new LayoutView(kit, layout);
        MouseFilter = MouseFilterEnum.Ignore;
        Size = layout.SizeVec;
        AddChild(view);
        view.Hide(HiddenIds);
        view.FadeOut(OpaqueBackgroundId);
        foreach (var (_, control) in view.Where(n => n.Id.StartsWith(UserCheckPrefix)))
            control.Visible = false;

        settings = kit.Context.Settings;
        var textBackground = layout.Find(TextGroup) is { } textGroup ? view.Get(textGroup, BackgroundId) : null;
        if (textBackground != null) textBackground.Visible = false;
        shade = new ColorRect
        {
            Position = textBackground?.Position ?? Vector2.Zero,
            Size = textBackground?.Size ?? Vector2.Zero,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        (textBackground?.GetParent() ?? (Node)view).AddChild(shade);
        if (textBackground != null) shade.GetParent().MoveChild(shade, textBackground.GetIndex() + 1);
        shadeIndex = Math.Clamp(settings.GetInt(SettingShadeIndex, DefaultShadeIndex), 0, ShadeLevels.Length - 1);
        ApplyShade();
        dragButton = view.Get(OptionButton);

        var textArea = view.Get(TextArea);
        var textNode = layout.Find(TextArea);
        log = new LogText(textNode != null ? UiKit.FontSize(textNode) : DefaultFontSize, textNode?.Color ?? Colors.White, kit.Regular)
        {
            Position = textArea?.Position ?? Vector2.Zero,
            Size = textArea?.Size ?? new Vector2(379, 108),
            MouseFilter = MouseFilterEnum.Pass,
        };
        if (textArea != null) textArea.Visible = false;
        (textArea?.GetParent() ?? (Node)view).AddChild(log);

        if (layout.Find(ScrollGroup) is { } scroll)
        {
            view.OnPressed(scroll, "btn_scroll_Up", () => log.Scroll(ScrollStep));
            foreach (var node in scroll.Children)
                if (node.IsButton && node.Id.Length == 0 && view.ControlOf(node) is BaseButton down)
                {
                    down.Pressed += () => log.Scroll(-ScrollStep);
                    scrollDown = down;
                }
        }

        input = view.Get<LineEdit>(InputId);
        if (input != null)
        {
            input.AddThemeFontOverride("font", kit.Regular);
            input.TextSubmitted += Submit;
            input.FocusEntered += () => game.Chat.SetTyping(true);
            input.FocusExited += () => { game.Chat.SetTyping(false); ShowInput(false); };
            if (layout.Find(InputBackgroundId) is { } row && layout.Find(InputId) is { } edit)
            {
                float rowCentre = input.Position.Y + (row.Y - edit.Y) + row.H * 0.5f;
                Callable.From(() => input.Position = new Vector2(input.Position.X, rowCentre - input.Size.Y * 0.5f)).CallDeferred();
            }
        }
        ShowInput(false);

        var group = new ButtonGroup();
        foreach (var (button, prefix) in ChannelTabs)
        {
            if (view.Get<TextureButton>(button) is not { } tab) continue;
            tab.ToggleMode = true;
            tab.ButtonGroup = group;
            tab.ButtonPressed = prefix.Length == 0;
            string which = prefix;
            tab.Pressed += () => this.prefix = which;
        }
        view.OnPressed(WhisperButton, StartWhisper);

        baseSize = Size;
        Resized += Relayout;
        Callable.From(CaptureGrow).CallDeferred();
    }

    private void CaptureGrow()
    {
        var grow = new VerticalGrow();
        grow.Add(view, VerticalGrow.Mode.Stretch);
        foreach (var (node, control) in view.Where(_ => true))
            grow.Add(control, control == scrollDown ? VerticalGrow.Mode.Shift
                : StretchIds.Contains(node.Id) ? VerticalGrow.Mode.Stretch
                : ShiftIds.Contains(node.Id) ? VerticalGrow.Mode.Shift
                : VerticalGrow.Mode.Stay);
        grow.Add(shade, VerticalGrow.Mode.Stretch);
        grow.Add(log, VerticalGrow.Mode.Stretch);
        foreach (var parent in grow.Stretched.Where(c => c != shade && c != log))
            grow.ShiftBottomBand(parent);
        this.grow = grow;
        Relayout();
    }

    private void Relayout()
    {
        if (grow == null || relaying) return;
        relaying = true;
        if (!Mathf.IsEqualApprox(Size.X, baseSize.X)) Size = new Vector2(baseSize.X, Size.Y);
        grow.Apply(Mathf.Max(0f, Size.Y - baseSize.Y));
        relaying = false;
    }

    public override void _EnterTree()
    {
        game.Chat.LineAdded += Append;
        game.Chat.InputRequested += FocusInput;
        game.BecameAvailable += Reload;
        hudLayout ??= HudLayout.Attach(this, LayoutId, dragButton, DefaultPosition, resizable: true,
            defaultSize: baseSize, minimumSize: baseSize, resizeCorner: HudLayout.Corner.TopRight,
            backgroundOpacityChanged: _ => CycleShade());
        Reload();
    }

    public override void _ExitTree()
    {
        game.Chat.LineAdded -= Append;
        game.Chat.InputRequested -= FocusInput;
        game.BecameAvailable -= Reload;
    }

    public override void _Input(InputEvent ev)
    {
        if (input == null || !input.HasFocus() || ev is not InputEventKey { Pressed: true, Echo: false } k) return;
        if (k.Keycode != Key.Escape) return;
        input.Clear();
        input.ReleaseFocus();
        GetViewport().SetInputAsHandled();
    }

    private void CycleShade()
    {
        shadeIndex = (shadeIndex + 1) % ShadeLevels.Length;
        settings.SetInt(SettingShadeIndex, shadeIndex);
        ApplyShade();
    }

    private void ApplyShade() => shade.Color = new Color(0, 0, 0, ShadeLevels[shadeIndex]);

    private void StartWhisper()
    {
        if (game.Target.Has && game.Target.IsPlayer)
        {
            game.Whispers.OpenWith(game.Target.Name);
            return;
        }
        if (input == null) return;
        FocusInput();
        input.Text = WhisperPrefix;
        input.CaretColumn = input.Text.Length;
    }

    private void ShowInput(bool on)
    {
        if (input != null) input.Visible = on;
        foreach (var id in InputRowIds)
            foreach (var (_, control) in view.Where(n => n.Id == id))
                control.Visible = on;
    }

    private Vector2 DefaultPosition()
    {
        var room = GetViewport().GetVisibleRect().Size;
        return new Vector2(ScreenMargin, room.Y - Taskbar.BarHeight - Size.Y - ScreenMargin);
    }

    private void Reload() => log.Set(game.Chat.History);

    private void Append(string bbcode) => log.Append(bbcode);

    private void FocusInput()
    {
        if (input == null) return;
        ShowInput(true);
        input.GrabFocus();
        input.CaretColumn = input.Text.Length;
    }

    private void Submit(string text)
    {
        if (input == null) return;
        input.Clear();
        input.ReleaseFocus();
        text = text.Trim();
        if (text.Length == 0) return;
        bool hasPrefix = PrefixChars.Contains(text[0]);
        game.Chat.Send(hasPrefix || prefix.Length == 0 ? text : prefix + text);
    }
}

using Godot;
using KnightOnlineUiChaos.Layout;
using LibreKO.Plugins;

namespace KnightOnlineUiChaos.Windows;

public partial class Taskbar : Control
{
    public const float BarHeight = 41f;
    private const string LayoutName = "co_taskbar_main_us";
    private const string MenuGroup = "base_menu";
    private const string CollapsedGroup = "base_task00";
    private const string ExpandedGroup = "base_task01";
    private const string RightGroup = "base_right";
    private const string Background = "img_bg";
    private const string ExpBar = "progress_exp";
    private const string ExpText = "str_exp";
    private const string NoticeText = "str_usernotice";
    private const string SitButton = "btn_00";
    private const string StandButton = "btn_01";
    private const string TradeButton = "btn_03";
    private const string TownButton = "btn_07";
    private const string ChangeButton = "btn_change";
    private const string MenuButton = "btn_menu";
    private const string KarusQuestButton = "btn_seed_karu";
    private const string ElmoQuestButton = "btn_seed_elmo";
    private const int KarusNation = 1;

    private static readonly (string Button, string Window)[] WindowButtons =
    {
        ("btn_02", "seek_party"),
        ("btn_04", "skills"),
        ("btn_05", "character_info"),
        ("btn_06", "inventory"),
    };

    private static readonly (string Button, string Window)[] MenuButtons =
    {
        (KarusQuestButton, "quests"),
        (ElmoQuestButton, "quests"),
        ("btn_powerup", "shoppingmall"),
    };

    private readonly LayoutView view;
    private readonly PluginGame game;
    private readonly LayoutNode layout;
    private readonly LayoutNode expanded;
    private readonly LayoutNode menu;

    public Taskbar()
    {
        var kit = Plugin.Kit;
        game = kit.Game;
        layout = kit.Layout(LayoutName);
        view = new LayoutView(kit, layout);
        expanded = layout.Find(ExpandedGroup) ?? layout;
        menu = layout.Find(MenuGroup) ?? layout;
        MouseFilter = MouseFilterEnum.Ignore;
        AddChild(view);
        view.Hide(CollapsedGroup, "btn_question_karu", "btn_question_elmo");
        view.Hide(expanded, StandButton, ChangeButton);
        foreach (var (button, window) in WindowButtons)
            view.OnPressed(expanded, button, () => game.Windows.Toggle(window));
        foreach (var (button, window) in MenuButtons)
            view.OnPressed(menu, button, () => game.Windows.Toggle(window));
        view.OnPressed(expanded, TownButton, () => game.Commands.GoTown());
        view.OnPressed(expanded, TradeButton, () => game.Commands.TradeWithTarget());
        view.OnPressed(expanded, SitButton, () => game.Commands.ToggleSit());
        view.OnPressed(menu, MenuButton, () => LibreKO.SettingsPanel.Open(GetTree().Root));
        view.SetTextAll(NoticeText, "");
    }

    public override void _EnterTree()
    {
        game.Character.Changed += RefreshExp;
        game.BecameAvailable += RefreshExp;
        GetViewport().SizeChanged += Place;
        Place();
        RefreshExp();
    }

    public override void _ExitTree()
    {
        game.Character.Changed -= RefreshExp;
        game.BecameAvailable -= RefreshExp;
        GetViewport().SizeChanged -= Place;
    }

    private void Place()
    {
        var room = GetViewport().GetVisibleRect().Size;
        view.Position = new Vector2(0, room.Y - layout.H);
        if (expanded.Find(RightGroup) is not { } right || view.ControlOf(right) is not { } rightControl) return;
        float rightLeft = room.X - right.W;
        rightControl.Position = new Vector2(rightLeft - layout.X, right.Y - layout.Y);
        if (expanded.Find(Background) is { } bg && view.ControlOf(bg) is { } bgControl)
            bgControl.Size = new Vector2(rightLeft - bg.X, bg.H);
        float barRight = 0f;
        if (expanded.Find(ExpBar) is { } exp && view.ControlOf(exp) is { } expControl)
        {
            barRight = rightLeft + (exp.X + exp.W - right.X);
            expControl.Size = new Vector2(barRight - exp.X, exp.H);
            if (expanded.Find(ExpText) is { } text && view.ControlOf(text) is { } textControl)
                textControl.Position = new Vector2(exp.X + (barRight - exp.X - text.W) * 0.5f - layout.X, textControl.Position.Y);
        }
        if (expanded.Find(NoticeText) is { } notice && view.ControlOf(notice) is { } noticeControl && barRight > 0f)
            noticeControl.Size = new Vector2(barRight - notice.X, notice.H);
    }

    private void RefreshExp()
    {
        var c = game.Character;
        float fraction = Mathf.Clamp((float)(c.ExpPercent / 100.0), 0f, 1f);
        if (view.Get<TextureProgressBar>(expanded, ExpBar) is { } bar) bar.Value = fraction;
        if (view.Get<Label>(expanded, ExpText) is { } text) text.Text = $"EXP : {c.ExpPercent:0.0}%";
        bool karus = c.Nation == KarusNation;
        if (view.Get(menu, KarusQuestButton) is { } karusQuests) karusQuests.Visible = karus;
        if (view.Get(menu, ElmoQuestButton) is { } elmoQuests) elmoQuests.Visible = !karus;
    }
}

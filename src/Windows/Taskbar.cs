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

    private readonly LayoutView _view;
    private readonly PluginGame _game;
    private readonly LayoutNode _layout;
    private readonly LayoutNode _expanded;
    private readonly LayoutNode _menu;

    public Taskbar()
    {
        var kit = Plugin.Kit;
        _game = kit.Game;
        _layout = kit.Layout(LayoutName);
        _view = new LayoutView(kit, _layout);
        _expanded = _layout.Find(ExpandedGroup) ?? _layout;
        _menu = _layout.Find(MenuGroup) ?? _layout;
        MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_view);
        _view.Hide(CollapsedGroup, "btn_question_karu", "btn_question_elmo");
        _view.Hide(_expanded, StandButton, ChangeButton);
        foreach (var (button, window) in WindowButtons)
            _view.OnPressed(_expanded, button, () => _game.Windows.Toggle(window));
        foreach (var (button, window) in MenuButtons)
            _view.OnPressed(_menu, button, () => _game.Windows.Toggle(window));
        _view.OnPressed(_expanded, TownButton, () => _game.Commands.GoTown());
        _view.OnPressed(_expanded, TradeButton, () => _game.Commands.TradeWithTarget());
        _view.OnPressed(_expanded, SitButton, () => _game.Commands.ToggleSit());
        _view.OnPressed(_menu, MenuButton, () => LibreKO.SettingsPanel.Open(GetTree().Root));
        _view.SetTextAll(NoticeText, "");
    }

    public override void _EnterTree()
    {
        _game.Character.Changed += RefreshExp;
        _game.BecameAvailable += RefreshExp;
        GetViewport().SizeChanged += Place;
        Place();
        RefreshExp();
    }

    public override void _ExitTree()
    {
        _game.Character.Changed -= RefreshExp;
        _game.BecameAvailable -= RefreshExp;
        GetViewport().SizeChanged -= Place;
    }

    private void Place()
    {
        var room = GetViewport().GetVisibleRect().Size;
        _view.Position = new Vector2(0, room.Y - _layout.H);
        if (_expanded.Find(RightGroup) is not { } right || _view.ControlOf(right) is not { } rightControl) return;
        float rightLeft = room.X - right.W;
        rightControl.Position = new Vector2(rightLeft - _layout.X, right.Y - _layout.Y);
        if (_expanded.Find(Background) is { } bg && _view.ControlOf(bg) is { } bgControl)
            bgControl.Size = new Vector2(rightLeft - bg.X, bg.H);
        float barRight = 0f;
        if (_expanded.Find(ExpBar) is { } exp && _view.ControlOf(exp) is { } expControl)
        {
            barRight = rightLeft + (exp.X + exp.W - right.X);
            expControl.Size = new Vector2(barRight - exp.X, exp.H);
            if (_expanded.Find(ExpText) is { } text && _view.ControlOf(text) is { } textControl)
                textControl.Position = new Vector2(exp.X + (barRight - exp.X - text.W) * 0.5f - _layout.X, textControl.Position.Y);
        }
        if (_expanded.Find(NoticeText) is { } notice && _view.ControlOf(notice) is { } noticeControl && barRight > 0f)
            noticeControl.Size = new Vector2(barRight - notice.X, notice.H);
    }

    private void RefreshExp()
    {
        var c = _game.Character;
        float fraction = Mathf.Clamp((float)(c.ExpPercent / 100.0), 0f, 1f);
        if (_view.Get<TextureProgressBar>(_expanded, ExpBar) is { } bar) bar.Value = fraction;
        if (_view.Get<Label>(_expanded, ExpText) is { } text) text.Text = $"EXP : {c.ExpPercent:0.0}%";
        bool karus = c.Nation == KarusNation;
        if (_view.Get(_menu, KarusQuestButton) is { } karusQuests) karusQuests.Visible = karus;
        if (_view.Get(_menu, ElmoQuestButton) is { } elmoQuests) elmoQuests.Visible = !karus;
    }
}

using Godot;
using KnightOnlineUiChaos.Layout;
using LibreKO.Plugins;

namespace KnightOnlineUiChaos.Windows;

public partial class CharacterWindow : Control
{
    private const string FrameLayout = "co_various_frame_us";
    private const string PageLayout = "co_page_state_us";
    private const int TitleBarHeight = 24;
    private static readonly string[] HiddenTabs =
    {
        "btn_state", "btn_knights", "btn_knights_active", "btn_clan", "btn_clan_active", "btn_friends", "btn_friends_active",
        "btn_force", "btn_force_active",
    };

    private static readonly (string Button, int StatRow)[] StatButtons =
    {
        ("Btn_Strength", 0),
        ("Btn_Stamina", 1),
        ("Btn_Dexterity", 2),
        ("Btn_Intelligence", 3),
        ("Btn_MagicAttack", 4),
    };

    private readonly LayoutView _page;
    private readonly LayoutView _frame;
    private readonly PluginGame _game;
    private readonly WindowHost _host;

    public CharacterWindow(WindowHost host)
    {
        _host = host;
        var kit = Plugin.Kit;
        _game = kit.Game;
        var pageLayout = kit.Layout(PageLayout);
        var frameLayout = kit.Layout(FrameLayout);
        _frame = new LayoutView(kit, frameLayout);
        _page = new LayoutView(kit, pageLayout);
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(_frame);
        AddChild(_page);
        _page.Position = new Vector2(pageLayout.X - frameLayout.X, pageLayout.Y - frameLayout.Y);
        CustomMinimumSize = new Vector2(Mathf.Max(_frame.Size.X, _page.Position.X + _page.Size.X),
            Mathf.Max(_frame.Size.Y, _page.Position.Y + _page.Size.Y));
        Size = CustomMinimumSize;

        host.SetDragHandle(_frame.MakeDragHandle(new Rect2(0, 0, _frame.Size.X, TitleBarHeight)));
        _frame.OnPressed("btn_close", host.Close);
        _frame.Hide(HiddenTabs);
        foreach (var (button, row) in StatButtons)
        {
            int which = row;
            _page.OnPressed(button, () => _game.Character.AllocateStat(which));
        }
        Refresh();
    }

    public override void _EnterTree()
    {
        _game.Character.Changed += Refresh;
        _game.BecameAvailable += Refresh;
        _host.Shown += Refresh;
        Refresh();
    }

    public override void _ExitTree()
    {
        _game.Character.Changed -= Refresh;
        _game.BecameAvailable -= Refresh;
        _host.Shown -= Refresh;
    }

    private void Refresh()
    {
        var c = _game.Character;
        _page.SetText("text_Id", c.Name);
        _page.SetText("Text_Class", c.ClassName);
        _page.SetText("Text_Level", c.Level.ToString());
        _page.SetText("Text_Nation", c.NationName);
        _page.SetText("Text_Exp", $"{c.Exp:n0} / {c.MaxExp:n0}");
        _page.SetText("Text_RealmPoint", c.Np.ToString("n0"));
        _page.SetText("Text_AP", c.Ap.ToString());
        _page.SetText("Text_GP", c.Ac.ToString());
        _page.SetText("Text_Manner", "");
        _page.SetText("Text_Strength", c.Str.ToString());
        _page.SetText("Text_Stamina", c.Sta.ToString());
        _page.SetText("Text_Dexterity", c.Dex.ToString());
        _page.SetText("Text_MagicAttack", c.Mag.ToString());
        _page.SetText("Text_Intelligence", c.Intel.ToString());
        _page.SetText("Text_BonusPoint", c.Points.ToString());
        _page.SetText("Text_RegistFire", c.Resist(0).ToString());
        _page.SetText("Text_RegistIce", c.Resist(1).ToString());
        _page.SetText("Text_RegistLightR", c.Resist(2).ToString());
        _page.SetText("Text_RegistMagic", c.Resist(3).ToString());
        _page.SetText("Text_RegistCurse", c.Resist(4).ToString());
        _page.SetText("Text_RegistPoison", c.Resist(5).ToString());
        foreach (var (button, _) in StatButtons)
            if (_page.Get(button) is { } b) b.Visible = c.Points > 0;
    }
}

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

    private static readonly (string Text, int StatRow)[] StatTexts =
    {
        ("Text_Strength", 0),
        ("Text_Stamina", 1),
        ("Text_Dexterity", 2),
        ("Text_Intelligence", 3),
        ("Text_MagicAttack", 4),
    };

    private readonly LayoutView page;
    private readonly LayoutView frame;
    private readonly PluginGame game;
    private readonly WindowHost host;

    public CharacterWindow(WindowHost host)
    {
        this.host = host;
        var kit = Plugin.Kit;
        game = kit.Game;
        var pageLayout = kit.Layout(PageLayout);
        var frameLayout = kit.Layout(FrameLayout);
        frame = new LayoutView(kit, frameLayout);
        page = new LayoutView(kit, pageLayout);
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(frame);
        AddChild(page);
        page.Position = new Vector2(pageLayout.X - frameLayout.X, pageLayout.Y - frameLayout.Y);
        CustomMinimumSize = new Vector2(Mathf.Max(frame.Size.X, page.Position.X + page.Size.X),
            Mathf.Max(frame.Size.Y, page.Position.Y + page.Size.Y));
        Size = CustomMinimumSize;

        host.SetDragHandle(frame.MakeDragHandle(new Rect2(0, 0, frame.Size.X, TitleBarHeight)));
        frame.OnPressed("btn_close", host.Close);
        frame.Hide(HiddenTabs);
        foreach (var (button, row) in StatButtons)
        {
            int which = row;
            page.OnPressed(button, () => game.Character.AllocateStat(which));
        }
        Refresh();
    }

    public override void _EnterTree()
    {
        game.Character.Changed += Refresh;
        game.BecameAvailable += Refresh;
        host.Shown += Refresh;
        Refresh();
    }

    public override void _ExitTree()
    {
        game.Character.Changed -= Refresh;
        game.BecameAvailable -= Refresh;
        host.Shown -= Refresh;
    }

    private void Refresh()
    {
        var c = game.Character;
        page.SetText("text_Id", c.Name);
        page.SetText("Text_Class", c.ClassName);
        page.SetText("Text_Level", c.Level.ToString());
        page.SetText("Text_Nation", c.NationName);
        page.SetText("Text_Exp", $"{c.Exp:n0} / {c.MaxExp:n0}");
        page.SetText("Text_RealmPoint", c.Np.ToString("n0"));
        page.SetText("Text_AP", c.Ap.ToString());
        page.SetText("Text_GP", c.Ac.ToString());
        page.SetText("Text_Manner", c.Manner.ToString("n0"));
        page.SetText("Text_Strength", c.Str.ToString());
        page.SetText("Text_Stamina", c.Sta.ToString());
        page.SetText("Text_Dexterity", c.Dex.ToString());
        page.SetText("Text_MagicAttack", c.Mag.ToString());
        page.SetText("Text_Intelligence", c.Intel.ToString());
        page.SetText("Text_BonusPoint", c.Points.ToString());
        page.SetText("Text_RegistFire", c.Resist(0).ToString());
        page.SetText("Text_RegistIce", c.Resist(1).ToString());
        page.SetText("Text_RegistLightR", c.Resist(2).ToString());
        page.SetText("Text_RegistMagic", c.Resist(3).ToString());
        page.SetText("Text_RegistCurse", c.Resist(4).ToString());
        page.SetText("Text_RegistPoison", c.Resist(5).ToString());
        foreach (var (button, _) in StatButtons)
            if (page.Get(button) is { } b) b.Visible = c.Points > 0;
        foreach (var (text, row) in StatTexts)
            if (page.Get(text) is { } label)
            {
                label.MouseFilter = MouseFilterEnum.Pass;
                label.TooltipText = StatBonusTooltip(c.RebirthStat(row), c.StatBonus(row) - c.RebirthStat(row));
            }
    }

    private static string StatBonusTooltip(int rebirth, int other)
    {
        var lines = new List<string>();
        if (rebirth != 0) lines.Add($"Rebirth {rebirth:+#;-#}");
        if (other != 0) lines.Add($"Items and buffs {other:+#;-#}");
        return string.Join("\n", lines);
    }
}

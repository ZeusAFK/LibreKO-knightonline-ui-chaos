using Godot;
using KnightOnlineUiChaos.Layout;
using LibreKO.Plugins;

namespace KnightOnlineUiChaos.Windows;

public partial class StatusHud : Control
{
    public const string LayoutName = "co_hpbar_us";
    public static readonly Vector2 ScreenPosition = new(8, 8);

    private readonly LayoutView view;
    private readonly PluginGame game;

    public StatusHud()
    {
        var kit = Plugin.Kit;
        game = kit.Game;
        view = new LayoutView(kit, kit.Layout(LayoutName));
        Position = ScreenPosition;
        Size = view.Size;
        MouseFilter = MouseFilterEnum.Ignore;
        AddChild(view);
        view.Hide("Progress_HP_slow", "Progress_HP_drop", "Progress_HP_undead", "Progress_HP_lasting",
            "img_mail_on", "img_mail_normal", "img_Reporter");
        Refresh();
    }

    public override void _EnterTree()
    {
        game.Character.Changed += Refresh;
        game.BecameAvailable += Refresh;
        Refresh();
    }

    public override void _ExitTree()
    {
        game.Character.Changed -= Refresh;
        game.BecameAvailable -= Refresh;
    }

    private void Refresh()
    {
        var c = game.Character;
        view.SetProgress("Progress_HP", Fraction(c.Hp, c.MaxHp));
        view.SetProgress("Progress_msp", Fraction(c.Mp, c.MaxMp));
        view.SetText("Text_HP", $"{c.Hp}/{c.MaxHp}");
        view.SetText("Text_MSP", $"{c.Mp}/{c.MaxMp}");
        view.SetText("text_level", c.Level > 0 ? c.Level.ToString() : "");
        view.SetText("text_id", c.Name);
    }

    private static float Fraction(int value, int max) => max > 0 ? Mathf.Clamp(value / (float)max, 0f, 1f) : 0f;
}

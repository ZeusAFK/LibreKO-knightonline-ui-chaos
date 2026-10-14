using System.Collections.Generic;
using System.Linq;
using Godot;
using KnightOnlineUiChaos.Layout;
using LibreKO.Plugins;

namespace KnightOnlineUiChaos.Windows;

public partial class TargetFrame : Control
{
    private const string LayoutName = "co_targetbar_us";
    private const string HpBar = "pro_target";
    private const string NameText = "text_target";
    private const float TopMargin = 8f;
    private const int HpTextSize = 11;
    private const int HpTextOutline = 3;
    private const float HpTextOverhang = 4f;

    private static readonly Dictionary<TargetHpLook, string> BarOfLook = new()
    {
        [TargetHpLook.Lasting] = "Progress_HP_lasting",
        [TargetHpLook.Drop] = "Progress_HP_drop",
        [TargetHpLook.Slow] = "Progress_HP_slow",
    };

    private readonly LayoutView view;
    private readonly PluginGame game;
    private readonly Label hpText;

    public TargetFrame()
    {
        var kit = Plugin.Kit;
        game = kit.Game;
        view = new LayoutView(kit, kit.Layout(LayoutName));
        Size = view.Size;
        MouseFilter = MouseFilterEnum.Ignore;
        AddChild(view);
        view.Hide("Button_Board");
        hpText = BuildHpText();
        ShowBar(HpBar);
        Visible = false;
    }

    private Label BuildHpText()
    {
        var bar = view.Where(n => n.Id == HpBar).First().Node;
        var label = new Label
        {
            Position = new Vector2(bar.X, bar.Y - HpTextOverhang * 0.5f),
            Size = new Vector2(bar.W, bar.H + HpTextOverhang),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", HpTextSize);
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.85f));
        label.AddThemeConstantOverride("outline_size", HpTextOutline);
        view.AddChild(label);
        return label;
    }

    public override void _EnterTree()
    {
        game.Target.Changed += Refresh;
        game.BecameAvailable += Refresh;
        game.BecameUnavailable += Refresh;
        GetViewport().SizeChanged += Place;
        Place();
        Refresh();
    }

    public override void _ExitTree()
    {
        game.Target.Changed -= Refresh;
        game.BecameAvailable -= Refresh;
        game.BecameUnavailable -= Refresh;
        GetViewport().SizeChanged -= Place;
    }

    private void Place()
    {
        float width = GetViewport().GetVisibleRect().Size.X;
        Position = new Vector2(Mathf.Round((width - Size.X) * 0.5f), TopMargin);
    }

    private void ShowBar(string shown)
    {
        if (view.Get(HpBar) is { } normal) normal.Visible = shown == HpBar;
        foreach (var id in BarOfLook.Values)
            if (view.Get(id) is { } bar) bar.Visible = id == shown;
    }

    private void Refresh()
    {
        var t = game.Target;
        Visible = game.Available && t.Has;
        if (!Visible) return;
        view.SetText(NameText, t.Level > 0 ? $"{t.Name}  Lv.{t.Level}" : t.Name);
        string bar = BarOfLook.TryGetValue(t.HpLook, out var id) ? id : HpBar;
        ShowBar(bar);
        view.SetProgress(bar, t.MaxHp > 0 ? t.Hp / (float)t.MaxHp : 1f);
        hpText.Text = t.MaxHp > 0 ? $"{t.Hp} / {t.MaxHp}" : "";
    }
}

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

    private readonly LayoutView _view;
    private readonly PluginGame _game;
    private readonly Label _hpText;

    public TargetFrame()
    {
        var kit = Plugin.Kit;
        _game = kit.Game;
        _view = new LayoutView(kit, kit.Layout(LayoutName));
        Size = _view.Size;
        MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_view);
        _view.Hide("Button_Board");
        _hpText = BuildHpText();
        ShowBar(HpBar);
        Visible = false;
    }

    private Label BuildHpText()
    {
        var bar = _view.Where(n => n.Id == HpBar).First().Node;
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
        _view.AddChild(label);
        return label;
    }

    public override void _EnterTree()
    {
        _game.Target.Changed += Refresh;
        _game.BecameAvailable += Refresh;
        _game.BecameUnavailable += Refresh;
        GetViewport().SizeChanged += Place;
        Place();
        Refresh();
    }

    public override void _ExitTree()
    {
        _game.Target.Changed -= Refresh;
        _game.BecameAvailable -= Refresh;
        _game.BecameUnavailable -= Refresh;
        GetViewport().SizeChanged -= Place;
    }

    private void Place()
    {
        float width = GetViewport().GetVisibleRect().Size.X;
        Position = new Vector2(Mathf.Round((width - Size.X) * 0.5f), TopMargin);
    }

    private void ShowBar(string shown)
    {
        if (_view.Get(HpBar) is { } normal) normal.Visible = shown == HpBar;
        foreach (var id in BarOfLook.Values)
            if (_view.Get(id) is { } bar) bar.Visible = id == shown;
    }

    private void Refresh()
    {
        var t = _game.Target;
        Visible = _game.Available && t.Has;
        if (!Visible) return;
        _view.SetText(NameText, t.Level > 0 ? $"{t.Name}  Lv.{t.Level}" : t.Name);
        string bar = BarOfLook.TryGetValue(t.HpLook, out var id) ? id : HpBar;
        ShowBar(bar);
        _view.SetProgress(bar, t.MaxHp > 0 ? t.Hp / (float)t.MaxHp : 1f);
        _hpText.Text = t.MaxHp > 0 ? $"{t.Hp} / {t.MaxHp}" : "";
    }
}

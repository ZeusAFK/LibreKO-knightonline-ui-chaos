using Godot;
using KnightOnlineUiChaos.Layout;
using LibreKO;
using LibreKO.Plugins;

namespace KnightOnlineUiChaos.Windows;

public partial class MiniMapFrame : Control
{
    public const float FrameWidth = 255f;
    public const float FrameHeight = 240f;
    private const string LayoutName = "co_minimap_us";
    private const string MapArea = "Img_MiniMap";
    private const string BandId = "Img_StateBar";
    private const string ZoneText = "str_zoneid";
    private const string PositionText = "Text_Position";
    private const string MinimiseButton = "btn_minimize";
    private const string MaximiseButton = "btn_maximize";
    private const string LayoutId = "theme_minimap";
    private const float ScreenMargin = 8f;
    private static readonly string[] HiddenIds =
    {
        "Group_WarMap", "Group_Battle", "Group_Toggle", "Group_Mark", "Group_Note", "Group_NpcList", "scroll_Alpah_line",
        "btn_battle_p", "btn_battle_n", "Btn_WarMap", "btn_NpcListopen", "area_note",
    };
    private static readonly string[] BandIds = { BandId, ZoneText, PositionText, MinimiseButton, MaximiseButton };

    private readonly LayoutView view;
    private readonly LayoutNode layout;
    private readonly PluginGame game;
    private readonly MapCanvas canvas;
    private HudLayout? hudLayout;
    private bool collapsed;

    public MiniMapFrame()
    {
        var kit = Plugin.Kit;
        game = kit.Game;
        layout = kit.Layout(LayoutName);
        view = new LayoutView(kit, layout);
        MouseFilter = MouseFilterEnum.Ignore;
        Size = new Vector2(FrameWidth, FrameHeight);
        AddChild(view);
        view.Hide(HiddenIds);

        var area = view.Get(MapArea);
        canvas = new MapCanvas(game) { Position = area?.Position ?? Vector2.Zero, Size = area?.Size ?? new Vector2(203, 199) };
        (area?.GetParent() ?? (Node)view).AddChild(canvas);
        if (area != null) canvas.GetParent().MoveChild(canvas, area.GetIndex() + 1);

        view.OnPressed("Btn_ZoomIn", () => canvas.Zoom(-1));
        view.OnPressed("Btn_ZoomOut", () => canvas.Zoom(1));
        view.OnPressed("btn_globalmap", () => game.Windows.Toggle("globalmap"));
        view.OnPressed("btn_battle_a", canvas.ToggleBlips);
        view.OnPressed(MinimiseButton, () => SetCollapsed(true));
        view.OnPressed(MaximiseButton, () => SetCollapsed(false));
        if (view.Get(BandId) is { } band) band.MouseFilter = MouseFilterEnum.Stop;
        SetCollapsed(false);
        Refresh();
    }

    public override void _EnterTree()
    {
        game.Map.Changed += Refresh;
        game.BecameAvailable += Refresh;
        hudLayout ??= HudLayout.Attach(this, LayoutId, view.Get(BandId), DefaultPosition);
        Refresh();
    }

    public override void _ExitTree()
    {
        game.Map.Changed -= Refresh;
        game.BecameAvailable -= Refresh;
    }

    private Vector2 DefaultPosition()
    {
        var room = GetViewport().GetVisibleRect().Size;
        return new Vector2(room.X - FrameWidth - ScreenMargin, ScreenMargin);
    }

    private void SetCollapsed(bool collapsed)
    {
        this.collapsed = collapsed;
        foreach (var child in layout.Children)
        {
            if (view.ControlOf(child) is not { } control) continue;
            control.Visible = BandIds.Contains(child.Id) || !collapsed;
        }
        canvas.Visible = !collapsed;
        if (!collapsed) view.Hide(HiddenIds);
        if (view.Get(MinimiseButton) is { } minimise) minimise.Visible = !collapsed;
        if (view.Get(MaximiseButton) is { } maximise) maximise.Visible = collapsed;
    }

    private void Refresh()
    {
        var m = game.Map;
        view.SetText(ZoneText, m.ZoneName);
        view.SetText(PositionText, $"{m.X:0}, {m.Z:0}");
        canvas.QueueRedraw();
    }
}

public partial class MapCanvas : Control
{
    private static readonly float[] ZoomRadii = { 90f, 140f, 220f, 340f };
    private const int DefaultZoom = 1;
    private static readonly Color Background = new(0.03f, 0.035f, 0.045f);
    private static readonly Color PlayerColor = new(1f, 0.95f, 0.6f);
    private const float PlayerArrow = 6f;

    private readonly PluginGame game;
    private int zoom = DefaultZoom;
    private bool showBlips = true;

    public MapCanvas(PluginGame game)
    {
        this.game = game;
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public void ToggleBlips()
    {
        showBlips = !showBlips;
        QueueRedraw();
    }

    public void Zoom(int dir)
    {
        zoom = Mathf.Clamp(zoom + dir, 0, ZoomRadii.Length - 1);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var m = game.Map;
        DrawRect(new Rect2(Vector2.Zero, Size), Background);
        float radius = ZoomRadii[zoom];
        var tex = m.MapTexture;
        if (tex != null && m.WorldExtent > 0f)
        {
            float texPerWorld = tex.GetWidth() / m.WorldExtent;
            var centre = new Vector2(m.X * texPerWorld, (m.WorldExtent - m.Z) * texPerWorld);
            var srcSize = new Vector2(2f * radius * texPerWorld, 2f * radius * texPerWorld * (Size.Y / Size.X));
            var src = new Rect2(centre - srcSize * 0.5f, srcSize);
            DrawTextureRectRegion(tex, new Rect2(Vector2.Zero, Size), src);
        }
        float pxPerWorld = Size.X * 0.5f / radius;
        var mid = Size * 0.5f;
        foreach (var b in showBlips ? m.Blips : Array.Empty<MiniMap.Blip>())
        {
            var p = mid + new Vector2((b.X - m.X) * pxPerWorld, -(b.Z - m.Z) * pxPerWorld);
            if (p.X < 0 || p.Y < 0 || p.X > Size.X || p.Y > Size.Y) continue;
            if (b.Hollow) DrawArc(p, b.Radius, 0f, Mathf.Tau, 16, b.Color, 1.5f, true);
            else DrawCircle(p, b.Radius, b.Color);
        }
        float heading = Mathf.DegToRad(m.HeadingDegrees);
        var dir = new Vector2(Mathf.Sin(heading), -Mathf.Cos(heading));
        var side = new Vector2(-dir.Y, dir.X);
        DrawColoredPolygon(new[]
        {
            mid + dir * PlayerArrow,
            mid - dir * PlayerArrow * 0.7f + side * PlayerArrow * 0.6f,
            mid - dir * PlayerArrow * 0.7f - side * PlayerArrow * 0.6f,
        }, PlayerColor);
    }
}

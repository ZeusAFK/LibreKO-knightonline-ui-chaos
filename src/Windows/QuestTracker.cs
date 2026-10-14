using Godot;
using KnightOnlineUiChaos.Layout;
using LibreKO;
using LibreKO.Plugins;

namespace KnightOnlineUiChaos.Windows;

public partial class QuestTracker : Control
{
    private const string LayoutName = "co_quest_mini_tip_us";
    private const string TitleBarId = "MiniBar_Title";
    private const string ListBarId = "MiniBar_List";
    private const string PageBarId = "MiniBar_Page";
    private const string RowBackgroundId = "img_middle";
    private const string RowLineId = "img_bottom";
    private const string TitleCountId = "str_title";
    private const string QuestTitleId = "szText";
    private const string PageNumberId = "str_pagenum";
    private const string ObjectivesToggle = "btn_list_title";
    private const string QuestsWindow = "quests";
    private const string LayoutId = "theme_quest_tip";
    private const float ScreenMargin = 8f;
    private const int ObjectiveFontSize = 11;
    private const float ObjectiveRowHeight = 16f;
    private const float ObjectiveLeft = 26f;
    private const float ObjectiveWidth = 187f;
    private static readonly Color ObjectiveColor = Colors.White;
    private static readonly Color DoneColor = new(0.6f, 1f, 0.6f);
    private static readonly Color ShadowColor = new(0, 0, 0, 0.75f);

    private readonly LayoutView view;
    private readonly LayoutNode layout;
    private readonly PluginGame game;
    private readonly Control? rowBackground;
    private readonly Control? rowLine;
    private readonly Control? pageBar;
    private readonly Control? listBar;
    private readonly List<Label> objectives = new();
    private readonly float listTop;
    private readonly float listBottom;
    private readonly float pageBarHeight;
    private readonly float titleHeight;
    private HudLayout? hudLayout;
    private int page;
    private bool closed;
    private bool minimised;
    private bool objectivesOpen = true;

    public QuestTracker()
    {
        var kit = Plugin.Kit;
        game = kit.Game;
        layout = kit.Layout(LayoutName);
        view = new LayoutView(kit, layout);
        MouseFilter = MouseFilterEnum.Ignore;
        Size = layout.SizeVec;
        AddChild(view);
        rowBackground = view.Get(RowBackgroundId);
        rowLine = view.Get(RowLineId);
        pageBar = view.Get(PageBarId);
        listBar = view.Get(ListBarId);
        var list = layout.Find(ListBarId);
        var pageNode = layout.Find(PageBarId);
        var title = layout.Find(TitleBarId);
        listTop = list != null ? list.Y - layout.Y : 0f;
        listBottom = list != null ? list.Y + list.H - layout.Y : 0f;
        pageBarHeight = pageNode?.H ?? 0f;
        titleHeight = title != null ? title.Y + title.H - layout.Y : 0f;

        view.OnPressed("btn_close", () => { closed = true; Visible = false; });
        view.OnPressed("btn_mini", () => SetMinimised(true));
        view.OnPressed("btn_max", () => SetMinimised(false));
        view.OnPressed(ObjectivesToggle, () => SetObjectivesOpen(!objectivesOpen));
        view.OnPressed("btn_prevpage", () => Turn(-1));
        view.OnPressed("btn_nextpage", () => Turn(1));
        view.OnPressed("btn_seedopen", () => game.Windows.Toggle(QuestsWindow));
        SetMinimised(false);
    }

    public override void _EnterTree()
    {
        game.Quests.Changed += Refresh;
        game.BecameAvailable += Refresh;
        hudLayout ??= HudLayout.Attach(this, LayoutId, view.Get(TitleBarId), DefaultPosition);
        Refresh();
    }

    public override void _ExitTree()
    {
        game.Quests.Changed -= Refresh;
        game.BecameAvailable -= Refresh;
    }

    private Vector2 DefaultPosition()
    {
        var room = GetViewport().GetVisibleRect().Size;
        return new Vector2(room.X - layout.W - ScreenMargin, MiniMapFrame.FrameHeight + 2 * ScreenMargin);
    }

    private void Turn(int delta)
    {
        int count = game.Quests.Tracked.Count;
        if (count == 0) return;
        page = ((page + delta) % count + count) % count;
        Refresh();
    }

    private void SetMinimised(bool minimised)
    {
        this.minimised = minimised;
        view.Hide(minimised ? "btn_mini" : "btn_max");
        if (view.Get(minimised ? "btn_max" : "btn_mini") is { } shown) shown.Visible = true;
        Refresh();
    }

    private void SetObjectivesOpen(bool open)
    {
        objectivesOpen = open;
        Refresh();
    }

    private void Refresh()
    {
        var quests = game.Quests.Tracked;
        Visible = !closed && quests.Count > 0;
        foreach (var label in objectives) label.QueueFree();
        objectives.Clear();
        if (quests.Count == 0) return;
        page = Mathf.Clamp(page, 0, quests.Count - 1);
        var quest = quests[page];
        view.SetText(TitleCountId, quests.Count.ToString());
        view.SetClippedText(QuestTitleId, quest.Title);
        view.SetText(PageNumberId, (page + 1).ToString());

        float bottom = listBottom;
        if (objectivesOpen && !minimised)
        {
            foreach (var line in quest.Objectives)
            {
                var label = new Label
                {
                    Text = line,
                    Position = new Vector2(ObjectiveLeft, bottom),
                    Size = new Vector2(ObjectiveWidth, ObjectiveRowHeight),
                    ClipText = true,
                    MouseFilter = MouseFilterEnum.Ignore,
                };
                label.AddThemeFontSizeOverride("font_size", ObjectiveFontSize);
                label.AddThemeFontOverride("font", Plugin.Kit.Regular);
                label.AddThemeColorOverride("font_color", quest.ReadyToTurnIn ? DoneColor : ObjectiveColor);
                label.AddThemeColorOverride("font_shadow_color", ShadowColor);
                label.AddThemeConstantOverride("shadow_offset_x", 1);
                label.AddThemeConstantOverride("shadow_offset_y", 1);
                view.AddChild(label);
                objectives.Add(label);
                bottom += ObjectiveRowHeight;
            }
        }
        Layout(bottom);
    }

    private void Layout(float contentBottom)
    {
        bool body = !minimised;
        if (pageBar != null) pageBar.Visible = body;
        if (listBar != null) listBar.Visible = body;
        if (!body)
        {
            Size = new Vector2(layout.W, titleHeight);
            return;
        }
        float rowBottom = contentBottom - listTop - 1f;
        if (pageBar != null) pageBar.Position = new Vector2(pageBar.Position.X, contentBottom);
        if (rowBackground != null) rowBackground.Size = new Vector2(rowBackground.Size.X, rowBottom);
        if (rowLine != null) rowLine.Position = new Vector2(rowLine.Position.X, rowBottom);
        Size = new Vector2(layout.W, contentBottom + pageBarHeight);
    }
}

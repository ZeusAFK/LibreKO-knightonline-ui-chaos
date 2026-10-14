using Godot;
using KnightOnlineUiChaos.Layout;
using LibreKO.Domain;
using LibreKO.Plugins;
using ItemSlot = KnightOnlineUiChaos.Layout.ItemSlot;

namespace KnightOnlineUiChaos.Windows;

public partial class InventoryWindow : Control
{
    private const string LayoutName = "co_inventory_us";
    private const int EquipAreaType = 1;
    private const int GridAreaType = 2;
    private const int CospreAreaType = 17;
    private const int BagAreaType = 18;
    private const int TitleBarHeight = 30;
    private const string CospreGroup = "base_cos";
    private const string TrashArea = "area_samma";
    private static readonly Vector2 CarrySize = new(36, 36);
    private static readonly string[] HiddenIds = { "base_bag", "elmo_ecli666", "btn_helmetview", "btn_gold", "img_hide" };

    private readonly LayoutView view;
    private readonly PluginGame game;
    private readonly WindowHost host;
    private readonly List<ItemSlot> slots = new();
    private readonly List<ItemSlot> bagSlots = new();
    private readonly Dictionary<int, ItemSlot> bySlot = new();
    private readonly TextureRect carry;
    private readonly int leftPanelWidth;
    private bool cospreOpen;
    private int bag;
    private int carried = -1;
    private TextureRect? portrait;
    private bool portraitOn;

    public InventoryWindow(WindowHost host)
    {
        this.host = host;
        var kit = Plugin.Kit;
        game = kit.Game;
        var layout = kit.Layout(LayoutName);
        view = new LayoutView(kit, layout);
        leftPanelWidth = layout.Images.Where(i => i.Id.Length == 0).Select(i => i.X).DefaultIfEmpty(0).Min();
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(view);
        view.Hide(HiddenIds);

        host.SetDragHandle(view.MakeDragHandle(new Rect2(leftPanelWidth, 0, view.Size.X - leftPanelWidth, TitleBarHeight)));
        view.OnPressed("btn_close", host.Close);
        view.OnPressed("btn_open_cos", () => SetCospreOpen(!cospreOpen));
        view.OnPressed("btn_close_cos", () => SetCospreOpen(false));
        var bagTabs = new ButtonGroup();
        for (int bag = 0; bag < InventoryConstants.BagSlotMax; bag++)
        {
            int which = bag;
            string id = $"btn_bag{bag + 1}";
            if (view.Get<TextureButton>(id) is not { } tab) continue;
            tab.ToggleMode = true;
            tab.ButtonGroup = bagTabs;
            tab.ButtonPressed = bag == 0;
            tab.Pressed += () => SelectBag(which);
        }

        foreach (var (node, control) in view.Areas(EquipAreaType))
            if (int.TryParse(node.Id, out int equip) && equip < InventoryConstants.SlotMax)
                slots.Add(BindSlot(control, equip));
        foreach (var (node, control) in view.Areas(GridAreaType))
            slots.Add(BindSlot(control, InventoryConstants.InventoryStart + int.Parse(node.Id)));
        foreach (var (node, control) in view.Areas(CospreAreaType))
            slots.Add(BindSlot(control, InventoryConstants.CospreStart + int.Parse(node.Id)));
        foreach (var (node, control) in view.Areas(BagAreaType))
            bagSlots.Add(BindSlot(control, InventoryConstants.MagicBagStart + int.Parse(node.Id)));

        if (view.Get(TrashArea) is { } trash)
        {
            var target = new DropTarget
            {
                Position = trash.Position,
                Size = trash.Size,
                OnDrop = from => game.Inventory.Drop(from),
                OnClick = () => { if (carried < 0) return; int from = carried; CancelCarry(); game.Inventory.Drop(from); },
            };
            trash.GetParent().AddChild(target);
        }

        if (view.Get("area_char") is { } charArea)
        {
            portrait = new TextureRect
            {
                Position = charArea.Position,
                Size = charArea.Size,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                MouseFilter = MouseFilterEnum.Ignore,
                ClipContents = true,
            };
            charArea.GetParent().AddChild(portrait);
        }

        carry = new TextureRect
        {
            Visible = false,
            Size = CarrySize,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 10,
        };
        AddChild(carry);
        SetProcess(false);
        SetCospreOpen(false);
        Refresh();
    }

    private ItemSlot BindSlot(Control area, int slot)
    {
        var cell = new ItemSlot(slot)
        {
            Position = area.Position,
            Size = area.Size,
            Source = s => game.Inventory.At(s),
            OnDrop = (from, to) => { CancelCarry(); game.Inventory.Move(from, to); },
            OnClick = SlotClicked,
            OnActivate = SlotActivated,
            OnDoubleClick = s => { CancelCarry(); game.Inventory.Use(s); },
            OnHover = (s, over) => { if (over) game.Inventory.ShowTooltip(s); else game.Inventory.HideTooltip(); },
        };
        area.GetParent().AddChild(cell);
        bySlot[slot] = cell;
        return cell;
    }

    private void SlotClicked(int slot)
    {
        if (carried < 0)
        {
            if (game.Inventory.At(slot).IsEmpty) return;
            carried = slot;
            carry.Texture = game.Inventory.At(slot).Icon;
            carry.Visible = true;
            carry.Position = GetLocalMousePosition() - CarrySize * 0.5f;
            if (bySlot.TryGetValue(slot, out var cell)) cell.SetCarried(true);
            game.Inventory.HideTooltip();
            SetProcess(true);
            return;
        }
        int from = carried;
        CancelCarry();
        if (from != slot) game.Inventory.Move(from, slot);
    }

    private void SlotActivated(int slot)
    {
        if (carried >= 0) { CancelCarry(); return; }
        game.Inventory.Use(slot);
    }

    private void CancelCarry()
    {
        if (carried >= 0 && bySlot.TryGetValue(carried, out var cell)) cell.SetCarried(false);
        carried = -1;
        carry.Visible = false;
        SetProcess(false);
    }

    public override void _Process(double delta)
    {
        if (carried < 0) return;
        carry.Position = GetLocalMousePosition() - CarrySize * 0.5f;
    }

    public override void _Input(InputEvent ev)
    {
        if (carried < 0 || ev is not InputEventKey { Pressed: true, Keycode: Key.Escape }) return;
        CancelCarry();
        GetViewport().SetInputAsHandled();
    }

    private void SetCospreOpen(bool open)
    {
        bool wasOpen = cospreOpen;
        cospreOpen = open;
        if (view.Get(CospreGroup) is { } cos) cos.Visible = open;
        float width = open ? view.Size.X : view.Size.X - leftPanelWidth;
        view.Position = new Vector2(open ? 0 : -leftPanelWidth, 0);
        CustomMinimumSize = new Vector2(width, view.Size.Y);
        Size = CustomMinimumSize;
        if (wasOpen != open && IsInsideTree())
            host.Window.Position += new Vector2(open ? -leftPanelWidth : leftPanelWidth, 0);
    }

    private void SelectBag(int bag)
    {
        this.bag = Mathf.Clamp(bag, 0, InventoryConstants.BagSlotMax - 1);
        RefreshBags();
    }

    private void RefreshBags()
    {
        for (int i = 0; i < bagSlots.Count; i++)
        {
            var cell = bagSlots[i];
            int slot = InventoryConstants.MagicBagStart + bag * InventoryConstants.MagicBagMax + i;
            cell.Source = _ => game.Inventory.At(slot);
            cell.OnDrop = (from, _) => { CancelCarry(); game.Inventory.Move(from, slot); };
            cell.OnClick = _ => SlotClicked(slot);
            cell.OnActivate = _ => SlotActivated(slot);
            cell.OnDoubleClick = _ => { CancelCarry(); game.Inventory.Use(slot); };
            cell.OnHover = (_, over) => { if (over) game.Inventory.ShowTooltip(slot); else game.Inventory.HideTooltip(); };
            cell.Refresh();
        }
    }

    public override void _EnterTree()
    {
        game.Inventory.Changed += Refresh;
        game.Character.Changed += RefreshTotals;
        game.BecameAvailable += Refresh;
        host.Shown += Refresh;
        host.Shown += ShowPortrait;
        host.Hidden += OnHidden;
        Refresh();
    }

    public override void _ExitTree()
    {
        game.Inventory.Changed -= Refresh;
        game.Character.Changed -= RefreshTotals;
        game.BecameAvailable -= Refresh;
        host.Shown -= Refresh;
        host.Shown -= ShowPortrait;
        host.Hidden -= OnHidden;
        HidePortrait();
    }

    private void ShowPortrait()
    {
        if (portrait == null || portraitOn) return;
        portraitOn = true;
        game.Character.SetPortraitShown(true);
        portrait.Texture = game.Character.Portrait;
    }

    private void HidePortrait()
    {
        if (!portraitOn) return;
        portraitOn = false;
        game.Character.SetPortraitShown(false);
    }

    private void OnHidden()
    {
        HidePortrait();
        CancelCarry();
        game.Inventory.HideTooltip();
    }

    private void Refresh()
    {
        foreach (var cell in slots) cell.Refresh();
        GhostOtherHand(InventoryConstants.RightHand, InventoryConstants.LeftHand);
        GhostOtherHand(InventoryConstants.LeftHand, InventoryConstants.RightHand);
        RefreshBags();
        RefreshTotals();
        if (carried >= 0 && game.Inventory.At(carried).IsEmpty) CancelCarry();
    }

    private void GhostOtherHand(int hand, int other)
    {
        var item = game.Inventory.At(hand);
        if (!item.TwoHanded || !bySlot.TryGetValue(other, out var cell)) return;
        cell.SetGhost(item.Icon);
    }

    private void RefreshTotals()
    {
        var c = game.Character;
        view.SetFittedText("text_gold", c.Gold.ToString("n0"));
        view.SetFittedText("text_weight", c.MaxWeight > 0 ? $"Weight : {c.Weight / 10f:0.0}/{c.MaxWeight / 10f:0.0}" : "");
    }
}

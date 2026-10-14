using Godot;

namespace KnightOnlineUiChaos.Layout;

public sealed class HorizontalGrow
{
    public enum Mode { Stay, Shift, Stretch }

    private readonly List<(Control Control, float X, float Width, Mode Mode)> parts = new();
    private readonly HashSet<Control> placed = new();

    public void Add(Control control, Mode mode)
    {
        if (mode == Mode.Stay) return;
        parts.Add((control, control.Position.X, control.Size.X, mode));
        placed.Add(control);
    }

    public void ShiftRightHalf(Control parent)
    {
        float middle = parent.Size.X * 0.5f;
        foreach (var child in parent.GetChildren().OfType<Control>())
            if (!placed.Contains(child) && child.Position.X + child.Size.X * 0.5f >= middle)
                Add(child, Mode.Shift);
    }

    public IEnumerable<Control> Stretched => parts.Where(p => p.Mode == Mode.Stretch).Select(p => p.Control).ToList();

    public void Apply(float extra)
    {
        foreach (var (control, x, width, mode) in parts)
        {
            if (!GodotObject.IsInstanceValid(control)) continue;
            if (mode == Mode.Shift)
                control.Position = new Vector2(x + extra, control.Position.Y);
            else
            {
                control.Position = new Vector2(x, control.Position.Y);
                control.Size = new Vector2(width + extra, control.Size.Y);
            }
        }
    }
}

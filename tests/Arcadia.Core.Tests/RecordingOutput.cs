using Arcadia.Input;

namespace Arcadia.Core.Tests;

/// <summary>Writes down what the mouse mode sends instead of sending it.</summary>
internal sealed class RecordingOutput : IMouseKeyboardOutput
{
    public List<string> Events { get; } = [];

    public int PointerX { get; set; }

    public int PointerY { get; set; }

    public int WheelVertical { get; private set; }

    public void MovePointer(int dx, int dy)
    {
        PointerX += dx;
        PointerY += dy;
    }

    public void SetButton(MouseButton button, bool down) => Events.Add($"{button}Button {(down ? "down" : "up")}");

    public void Scroll(int vertical, int horizontal) => WheelVertical += vertical;

    public void SetKey(OutputKey key, bool down) => Events.Add($"{key} {(down ? "down" : "up")}");

    public void Type(char character) => Events.Add($"type {character}");
}

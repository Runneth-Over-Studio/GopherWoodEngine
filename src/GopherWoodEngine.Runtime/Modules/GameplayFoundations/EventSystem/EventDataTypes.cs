using Silk.NET.Input;
using System;

namespace GopherWoodEngine.Runtime.Modules;

#region Window Event Args
public class WindowLoadEventArgs : EventArgs
{
    // This class can be extended in the future if needed.
}

public class WindowUpdateEventArgs(double deltaTime = 0D) : EventArgs
{
    public double DeltaTime { get; set; } = deltaTime;
}

public class WindowRenderEventArgs(double deltaTime = 0D) : EventArgs
{
    public double DeltaTime { get; set; } = deltaTime;
}

public class WindowResizeEventArgs(int width, int height) : EventArgs
{
    public int Width { get; set; } = width;
    public int Height { get; set; } = height;
}

public class WindowFramebufferResizeEventArgs(int width, int height) : EventArgs
{
    public int Width { get; set; } = width;
    public int Height { get; set; } = height;
}

public class WindowFocusChangedEventArgs(bool focused) : EventArgs
{
    public bool Focused { get; set; } = focused;
}

public class WindowCloseEventArgs : EventArgs
{
    // This class can be extended in the future if needed.
}
#endregion

#region Input Event Args
public class InputDeviceConnectionChangedEventArgs(IInputDevice inputDevice, bool connected) : EventArgs
{
    public IInputDevice InputDevice { get; set; } = inputDevice;
    public bool Connected { get; set; } = connected;
}

// Gamepads
public class ThumbstickMovedEventArgs(Thumbstick thumbstick) : EventArgs
{
    public Thumbstick Thumbstick { get; set; } = thumbstick;
}

public class TriggerMovedEventArgs(Trigger trigger) : EventArgs
{
    public Trigger Trigger { get; set; } = trigger;
}

// Joysticks
public class AxisMovedEventArgs(Axis axis) : EventArgs
{
    public Axis Axis { get; set; } = axis;
}

public class HatMovedEventArgs(Hat hat) : EventArgs
{
    public Hat Hat { get; set; } = hat;
}

// Gamepads and Joysticks
public class ButtonDownEventArgs(Button button) : EventArgs
{
    public Button Button { get; set; } = button;
}

public class ButtonUpEventArgs(Button button) : EventArgs
{
    public Button Button { get; set; } = button;
}

// Keyboard
public class KeyDownEventArgs(Key key, int scancode) : EventArgs
{
    public Key Key { get; set; } = key;
    public int Scancode { get; set; } = scancode;
}

public class KeyUpEventArgs(Key key, int scancode) : EventArgs
{
    public Key Key { get; set; } = key;
    public int Scancode { get; set; } = scancode;
}

public class KeyReceivedEventArgs(char character) : EventArgs
{
    public char Character { get; set; } = character;
}

// Mouse
public class MouseDownEventArgs(MouseButton button) : EventArgs
{
    public MouseButton Button { get; set; } = button;
}

public class MouseUpEventArgs(MouseButton button) : EventArgs
{
    public MouseButton Button { get; set; } = button;
}

public class MouseClickEventArgs(MouseButton button, float x, float y) : EventArgs
{
    public MouseButton Button { get; set; } = button;
    public float X { get; set; } = x;
    public float Y { get; set; } = y;
}

public class MouseDoubleClickEventArgs(MouseButton button, float x, float y) : EventArgs
{
    public MouseButton Button { get; set; } = button;
    public float X { get; set; } = x;
    public float Y { get; set; } = y;
}

public class MouseMoveEventArgs(float x, float y) : EventArgs
{
    public float X { get; set; } = x;
    public float Y { get; set; } = y;
}

public class MouseScrollEventArgs(ScrollWheel scrollWheel) : EventArgs
{
    public ScrollWheel ScrollWheel { get; set; } = scrollWheel;
}
#endregion

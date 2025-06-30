using Silk.NET.Input;
using System;

namespace GopherWoodEngine.Runtime.Modules;

/// <summary>
/// Event that is raised when the connection state of an input device changes.
/// </summary>
public class InputDeviceConnectionChangedEventArgs(IInputDevice inputDevice, bool connected) : EventArgs
{
    /// <summary>
    /// The input device used for receiving user input.
    /// </summary>
    public IInputDevice InputDevice { get; } = inputDevice;

    /// <summary>
    /// Indicates whether the input device is currently connected.
    /// </summary>
    public bool Connected { get; } = connected;
}

// *** Gamepads

/// <summary>
/// Event that is raised when a thumbstick is moved.
/// </summary>
public class ThumbstickMovedEventArgs(Thumbstick thumbstick) : EventArgs
{
    /// <summary>
    /// The current state of the thumbstick, including its position and any associated input data.
    /// </summary>
    public Thumbstick Thumbstick { get; } = thumbstick;
}

/// <summary>
/// Event that is raised when a trigger is moved.
/// </summary>
public class TriggerMovedEventArgs(Trigger trigger) : EventArgs
{
    /// <summary>
    /// The trigger associated with the current operation.
    /// </summary>
    public Trigger Trigger { get; } = trigger;
}

// *** Joysticks

/// <summary>
/// Event that is raised when a joystick axis changes.
/// </summary>
public class AxisMovedEventArgs(Axis axis) : EventArgs
{
    /// <summary>
    /// The axis configuration for the joystick.
    /// </summary>
    public Axis Axis { get; } = axis;
}

/// <summary>
/// Event that occurs when a hat is moved.
/// </summary>
public class HatMovedEventArgs(Hat hat) : EventArgs
{
    /// <summary>
    /// The hat associated with the joystick.
    /// </summary>
    public Hat Hat { get; } = hat;
}

// *** Gamepads and Joysticks

/// <summary>
/// Event that is raised when a button is pressed on a gamepad or joystick.
/// </summary>
public class ButtonDownEventArgs(Button button) : EventArgs
{
    /// <summary>
    /// The pressed button.
    /// </summary>
    public Button Button { get; } = button;
}

/// <summary>
/// The event raised when a button is released on a gamepad or joystick.
/// </summary>
public class ButtonUpEventArgs(Button button) : EventArgs
{
    /// <summary>
    /// The released button.
    /// </summary>
    public Button Button { get; } = button;
}

// *** Keyboard

/// <summary>
/// Event that is raised when a key is pressed on the keyboard.
/// </summary>
public class KeyDownEventArgs(Key key, int scancode) : EventArgs
{
    /// <summary>
    /// The pressed key.
    /// </summary>
    public Key Key { get; } = key;

    /// <summary>
    /// Number assigned to the pressed key.
    /// </summary>
    public int Scancode { get; } = scancode;
}

/// <summary>
/// Event that is raised when a key is released on the keyboard.
/// </summary>
public class KeyUpEventArgs(Key key, int scancode) : EventArgs
{
    /// <summary>
    /// The released key.
    /// </summary>
    public Key Key { get; } = key;

    /// <summary>
    /// Number assigned to the released key.
    /// </summary>
    public int Scancode { get; } = scancode;
}

/// <summary>
/// Event that is triggered when a key is received.
/// </summary>
public class KeyReceivedEventArgs(char character) : EventArgs
{
    /// <summary>
    /// Received keyboard key character.
    /// </summary>
    public char Character { get; } = character;
}

// *** Mouse

/// <summary>
/// The mouse down event.
/// </summary>
public class MouseDownEventArgs(MouseButton button) : EventArgs
{
    /// <summary>
    /// The mouse button associated with the current input event.
    /// </summary>
    public MouseButton Button { get; } = button;
}

/// <summary>
/// The mouse button release event.
/// </summary>
public class MouseUpEventArgs(MouseButton button) : EventArgs
{
    /// <summary>
    /// The mouse button associated with the current input event.
    /// </summary>
    public MouseButton Button { get; } = button;
}

/// <summary>
/// Provides data for mouse click event, including the button clicked and the cursor's position.
/// </summary>
public class MouseClickEventArgs(MouseButton button, float x, float y) : EventArgs
{
    /// <summary>
    /// The mouse button associated with the current input event.
    /// </summary>
    public MouseButton Button { get; } = button;

    /// <summary>
    /// The X-coordinate value of the click location.
    /// </summary>
    public float X { get; } = x;

    /// <summary>
    /// The Y-coordinate value of the click location.
    /// </summary>
    public float Y { get; } = y;
}

/// <summary>
/// Provides data for a mouse double-click event, including the button clicked and the cursor's position.
/// </summary>
public class MouseDoubleClickEventArgs(MouseButton button, float x, float y) : EventArgs
{
    /// <summary>
    /// The mouse button associated with the current input event.
    /// </summary>
    public MouseButton Button { get; } = button;

    /// <summary>
    /// The X-coordinate value of the double-click location.
    /// </summary>
    public float X { get; } = x;

    /// <summary>
    /// The Y-coordinate value of the double-click location.
    /// </summary>
    public float Y { get; } = y;
}

/// <summary>
/// Provides data for the mouse move event, including the current X and Y coordinates of the mouse.
/// </summary>
public class MouseMoveEventArgs(float x, float y) : EventArgs
{
    /// <summary>
    /// The X-coordinate value.
    /// </summary>
    public float X { get; } = x;

    /// <summary>
    /// The Y-coordinate value.
    /// </summary>
    public float Y { get; } = y;
}

/// <summary>
/// The mouse scroll wheel event.
/// </summary>
public class MouseScrollEventArgs(ScrollWheel scrollWheel) : EventArgs
{
    /// <summary>
    /// The current state of the scroll wheel input device.
    /// </summary>
    public ScrollWheel ScrollWheel { get; } = scrollWheel;
}

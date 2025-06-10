using System.Collections.Generic;
using System.Linq;
using Silk.NET.Input;

namespace GopherWoodEngine.Runtime.Modules;

internal class PhysicalDeviceIO : IPhysicalDeviceIO
{
    public IInputContext InputContext { get; }

    public PhysicalDeviceIO(IGraphicsDevice graphicsDevice, IEventSystem eventSystem)
    {
        InputContext = graphicsDevice.GetWindowInputContext();

        InputContext.ConnectionChanged += (device, connected) => eventSystem.Publish(this, new InputDeviceConnectionChangedEventArgs(device, connected));
        HookGamepadEvents(InputContext.Gamepads, eventSystem);
        HookJoystickEvents(InputContext.Joysticks, eventSystem);
        HookKeyboardEvents(InputContext.Keyboards, eventSystem);
        HookMouseEvents(InputContext.Mice, eventSystem);
    }

    private static void HookGamepadEvents(IEnumerable<IGamepad> gamepads, IEventSystem eventSystem)
    {
        foreach (IGamepad gamepadDevice in gamepads)
        {
            gamepadDevice.ThumbstickMoved += (gamepad, thumbstick) => eventSystem.Publish(gamepad, new ThumbstickMovedEventArgs(thumbstick));
            gamepadDevice.TriggerMoved += (gamepad, trigger) => eventSystem.Publish(gamepad, new TriggerMovedEventArgs(trigger));
            gamepadDevice.ButtonDown += (gamepad, button) => eventSystem.Publish(gamepad, new ButtonDownEventArgs(button));
            gamepadDevice.ButtonUp += (gamepad, button) => eventSystem.Publish(gamepad, new ButtonUpEventArgs(button));
        }
    }
    
    private static void HookJoystickEvents(IEnumerable<IJoystick> joysticks, IEventSystem eventSystem)
    {
        foreach (IJoystick joysticksDevice in joysticks)
        {
            joysticksDevice.AxisMoved += (joystick, axis) => eventSystem.Publish(joystick, new AxisMovedEventArgs(axis));
            joysticksDevice.HatMoved += (joystick, hat) => eventSystem.Publish(joystick, new HatMovedEventArgs(hat));
            joysticksDevice.ButtonDown += (joystick, button) => eventSystem.Publish(joystick, new ButtonDownEventArgs(button));
            joysticksDevice.ButtonUp += (joystick, button) => eventSystem.Publish(joystick, new ButtonUpEventArgs(button));
        }
    }
    
    private static void HookKeyboardEvents(IEnumerable<IKeyboard> keyboards, IEventSystem eventSystem)
    {
        if (keyboards.Any())
        {
            IKeyboard keyboardDevice = keyboards.First();
            keyboardDevice.KeyDown += (keyboard, key, scancode) => eventSystem.Publish(keyboard, new KeyDownEventArgs(key, scancode));
            keyboardDevice.KeyUp += (keyboard, key, scancode) => eventSystem.Publish(keyboard, new KeyUpEventArgs(key, scancode));
            keyboardDevice.KeyChar += (keyboard, character) => eventSystem.Publish(keyboard, new KeyReceivedEventArgs(character));
        }
    }
    
    private static void HookMouseEvents(IEnumerable<IMouse> mice, IEventSystem eventSystem)
    {
        if (mice.Any())
        {
            IMouse mouseDevice = mice.First();
            mouseDevice.MouseDown += (mouse, button) => eventSystem.Publish(mouse, new MouseDownEventArgs(button));
            mouseDevice.MouseUp += (mouse, button) => eventSystem.Publish(mouse, new MouseUpEventArgs(button));
            mouseDevice.Click += (mouse, button, position) => eventSystem.Publish(mouse, new MouseClickEventArgs(button, position.X, position.Y));
            mouseDevice.DoubleClick += (mouse, button, position) => eventSystem.Publish(mouse, new MouseDoubleClickEventArgs(button, position.X, position.Y));
            mouseDevice.MouseMove += (mouse, position) => eventSystem.Publish(mouse, new MouseMoveEventArgs(position.X, position.Y));
            mouseDevice.Scroll += (mouse, scrollWheel) => eventSystem.Publish(mouse, new MouseScrollEventArgs(scrollWheel));
        }
    }
}

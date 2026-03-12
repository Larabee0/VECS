using SDL3;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using VECS.LowLevel;

namespace VECS
{
    /// <summary>
    /// class for mangaging user input outside of resizing the window and closing it (with esc)
    /// This handles input like mouse movement for the camera
    /// and wasd for moving around the sceen.
    /// 
    /// This uses teh SDL3 events system*
    /// *apart from mouse motion because this didn't behave well.
    /// Mouse motion update is triggered by <see cref="SDL3Window.UpdateWindowEvents"/>,
    /// but handled locally in <see cref="MouseMotion(SDL_Event)"/>
    /// </summary>
    public class InputManager
    {
        public Vector2 MousePos => _mousePos;
        public Vector2 MousePosOld => _mousePosOld;
        public Vector2 MouseDelta => _mouseDelta;

        private Vector3 _moveInput = Vector3.Zero;
        private Vector2 _mousePos = Vector2.Zero;
        private Vector2 _mousePosOld = Vector2.Zero;
        private Vector2 _mouseDelta = Vector2.Zero;


        private bool _mouseMotion = false;
        public bool MouseMotion => _mouseMotion;

        private readonly Dictionary<SDL_Keycode, (bool, bool)> _keyStates = new(Enum.GetNames<SDL_Keycode>().Length);
        private readonly Queue<SDL_Keycode> _keysChangedState = new();


        private readonly Dictionary<SDL_Button, (bool, bool)> _mouseButtonStates = new(Enum.GetNames<SDL_Button>().Length);
        private readonly Queue<SDL_Button> _mouseButtonsChangedState = new();

        public static InputManager Instance { get; private set; }

        public unsafe InputManager(bool mainInputManager)
        {
            var keys = Enum.GetValues<SDL_Keycode>();
            var mouseButtons = Enum.GetValues<SDL_Button>();
            for (int i = 0; i < keys.Length; i++)
            {
                _keyStates.Add(keys[i], (false, false));
            }
            for (int i = 0; i < mouseButtons.Length; i++)
            {
                _mouseButtonStates.Add(mouseButtons[i], (false, false));
            }

            if (mainInputManager)
            {
                Instance = this;
            }
        }

        public static unsafe void RegisterWatcher(delegate* unmanaged[Cdecl]<nint, SDL_Event*, SDLBool> filter)
        {
            SDL3.SDL3.SDL_AddEventWatch(filter, IntPtr.Zero);
        }

        /// <summary>
        /// defines a pointable function for handling a right click event.
        /// </summary>
        /// <param name="n"></param>
        /// <param name="eventPtr"></param>
        /// <returns></returns>
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static unsafe SDLBool MouseButtonEvents(nint n, SDL_Event* eventPtr)
        {
            InputManager inputHandler = SDL3WindowManager.GetWindowInputManager(eventPtr->window.windowID);

            if (inputHandler == null) return false;

            var button = eventPtr->button.Button;
            var type = eventPtr->type;
            if (type == SDL_EventType.MouseButtonDown && !inputHandler._mouseButtonStates[button].Item1)
            {
                inputHandler._mouseButtonStates[button] = (true, true);
                inputHandler._mouseButtonsChangedState.Enqueue(button);
            }
            else if (type == SDL_EventType.MouseButtonUp && inputHandler._mouseButtonStates[button].Item1)
            {
                inputHandler._mouseButtonStates[button] = (false, true);
                inputHandler._mouseButtonsChangedState.Enqueue(button);
            }

            return false;
        }

        /// <summary>
        /// defines a pointale function for handling wasd input events
        /// </summary>
        /// <param name="n"></param>
        /// <param name="eventPtr"></param>
        /// <returns></returns>
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static unsafe SDLBool KeyboardButtonEvents(nint n, SDL_Event* eventPtr)
        {
            InputManager inputHandler = SDL3WindowManager.GetWindowInputManager(eventPtr->window.windowID);

            if (inputHandler == null) return false;

            var keyCode = eventPtr->key.key;
            var type = eventPtr->type;

            if (type == SDL_EventType.KeyDown && !inputHandler._keyStates[keyCode].Item1)
            {
                inputHandler._keyStates[keyCode] = (true, true);
                inputHandler._keysChangedState.Enqueue(keyCode);
            }
            else if (type == SDL_EventType.KeyUp && inputHandler._keyStates[keyCode].Item1)
            {
                inputHandler._keyStates[keyCode] = (false, true);
                inputHandler._keysChangedState.Enqueue(keyCode);
            }

            return false;
        }

        public bool GetKey(SDL_Keycode keycode)
        {
            return _keyStates[keycode].Item1;
        }

        public bool GetKeyDown(SDL_Keycode keycode)
        {
            var val = _keyStates[keycode];
            return val.Item1 && val.Item2;
        }

        public bool GetKeyUp(SDL_Keycode keycode)
        {
            var val = _keyStates[keycode];
            return !val.Item1 && val.Item2;
        }

        public bool GetMouseButton(int button)
        {
            var val = button switch
            {
                0 => _mouseButtonStates[SDL_Button.Left],
                1 => _mouseButtonStates[SDL_Button.Right],
                2 => _mouseButtonStates[SDL_Button.Middle],
                3 => _mouseButtonStates[SDL_Button.X1],
                4 => _mouseButtonStates[SDL_Button.X2],
                _ => throw new IndexOutOfRangeException(),
            };
            return val.Item1;
        }

        public bool GetMouseButtonDown(int button)
        {
            var val = button switch
            {
                0 => _mouseButtonStates[SDL_Button.Left],
                1 => _mouseButtonStates[SDL_Button.Right],
                2 => _mouseButtonStates[SDL_Button.Middle],
                3 => _mouseButtonStates[SDL_Button.X1],
                4 => _mouseButtonStates[SDL_Button.X2],
                _ => throw new IndexOutOfRangeException(),
            };
            return val.Item1 && val.Item2;
        }

        public bool GetMouseButtonUp(int button)
        {
            var val = button switch
            {
                0 => _mouseButtonStates[SDL_Button.Left],
                1 => _mouseButtonStates[SDL_Button.Right],
                2 => _mouseButtonStates[SDL_Button.Middle],
                3 => _mouseButtonStates[SDL_Button.X1],
                4 => _mouseButtonStates[SDL_Button.X2],
                _ => throw new IndexOutOfRangeException(),
            };
            return !val.Item1 && val.Item2;
        }

        /// <summary>
        /// processes a mouse input event
        /// </summary>
        /// <param name="sdlEvent"></param>
        public void OnMouseMotion(SDL_Event sdlEvent)
        {
            _mouseMotion = true;
            _mousePos.X = sdlEvent.motion.x;
            _mousePos.Y = sdlEvent.motion.y;
            _mouseDelta.X = sdlEvent.motion.xrel;
            _mouseDelta.Y = sdlEvent.motion.yrel;
        }

        /// <summary>
        /// sets mouse  delta to zero ready for next frame.
        /// </summary>
        public void LateUpdate()
        {
            while (_keysChangedState.Count > 0)
            {
                var key = _keysChangedState.Dequeue();
                (bool, bool) val = _keyStates[key];
                val.Item2 = false;
                _keyStates[key] = val;
            }
            while (_mouseButtonsChangedState.Count > 0)
            {
                var mouseButton = _mouseButtonsChangedState.Dequeue();
                (bool, bool) val = _mouseButtonStates[mouseButton];
                val.Item2 = false;
                _mouseButtonStates[mouseButton] = val;
            }
            _mouseDelta = Vector2.Zero;
            _mouseMotion = false;
            _mousePosOld = _mousePos;
        }

        public void Destroy()
        {
            Instance = null;
        }
    }
}

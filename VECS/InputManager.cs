using SDL3;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
        public Vector3 moveInput = Vector3.Zero;
        public Vector2 mousePos = Vector2.Zero;
        public Vector2 mousePosOld = Vector2.Zero;
        public Vector2 mouseDelta = Vector2.Zero;
        public bool mouseMotion = false;
        public bool firstMouse = true;

        public bool rightMouseDown = false;
        public bool shiftDown = false;
        public bool ctrlDown = false;
        public bool altDown = false;

        public static InputManager Instance { get; private set; }

        public unsafe InputManager()
        {
            Instance = this;
            RegisterWatcher(&KeyboardMove);
            RegisterWatcher(&RightClick);
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
        private static unsafe SDLBool RightClick(nint n, SDL_Event* eventPtr)
        {
            if (eventPtr->button.Button == SDL_Button.Right)
            {
                if (eventPtr->type == SDL_EventType.MouseButtonDown)
                {
                    Instance.rightMouseDown = true;
                }
                else if (eventPtr->type == SDL_EventType.MouseButtonUp)
                {
                    Instance.rightMouseDown = false;
                    Instance.firstMouse = true;
                }
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
        private static unsafe SDLBool KeyboardMove(nint n, SDL_Event* eventPtr)
        {
            switch (eventPtr->type)
            {
                case SDL_EventType.KeyDown:
                    {
                        var cur = Instance.moveInput;
                        switch (eventPtr->key.key)
                        {
                            case SDL_Keycode.A:
                                cur.X = 1;
                                break;
                            case SDL_Keycode.D:
                                cur.X = -1;
                                break;
                            case SDL_Keycode.W:
                                cur.Z = 1;
                                break;
                            case SDL_Keycode.S:
                                cur.Z = -1;
                                break;
                            case SDL_Keycode.Q:
                                cur.Y = -1;
                                break;
                            case SDL_Keycode.E:
                                cur.Y = 1;
                                break;
                            case SDL_Keycode.LeftShift:
                                Instance.shiftDown = true;
                                break;
                            case SDL_Keycode.LeftControl:
                                Instance.ctrlDown = true;
                                break;
                            case SDL_Keycode.LeftAlt:
                                Instance.altDown = true;
                                break;
                        }
                        Instance.moveInput = cur;
                        break;
                    }

                case SDL_EventType.KeyUp:
                    {
                        var cur = Instance.moveInput;
                        switch (eventPtr->key.key)
                        {
                            case SDL_Keycode.A when cur.X > 0:
                                cur.X = 0;
                                break;
                            case SDL_Keycode.D when cur.X < 0:
                                cur.X = 0;
                                break;
                            case SDL_Keycode.W when cur.Z > 0:
                                cur.Z = 0;
                                break;
                            case SDL_Keycode.S when cur.Z < 0:
                                cur.Z = 0;
                                break;
                            case SDL_Keycode.Q when cur.Y < 0:
                                cur.Y = 0;
                                break;
                            case SDL_Keycode.E when cur.Y > 0:
                                cur.Y = 0;
                                break;
                            case SDL_Keycode.LeftShift:
                                Instance.shiftDown = false;
                                break;
                            case SDL_Keycode.LeftControl:
                                Instance.ctrlDown = false;
                                break;
                            case SDL_Keycode.LeftAlt:
                                Instance.altDown = false;
                                break;
                        }
                        Instance.moveInput = cur;
                        break;
                    }
            }
            return false;
        }

        public static void Update()
        {
        }

        /// <summary>
        /// sets mouse  delta to zero ready for next frame.
        /// </summary>
        public void LateUpdate()
        {
            mouseDelta = Vector2.Zero;
            mouseMotion = false;
        }

        /// <summary>
        /// processes a mouse input event
        /// </summary>
        /// <param name="sdlEvent"></param>
        public void MouseMotion(SDL_Event sdlEvent)
        {
            mouseMotion = true;

            if (!firstMouse)
            {

                var pos = mousePos;
                var delta = mouseDelta;
                pos.X = sdlEvent.motion.x;
                pos.Y = sdlEvent.motion.y;
                delta.X = sdlEvent.motion.xrel;
                delta.Y = sdlEvent.motion.yrel;
                mousePos = pos;
                mouseDelta = delta;
            }
            else
            {
                firstMouse = false;

                var delta = mouseDelta;
                delta.X = 0;
                delta.Y = 0;
                mouseDelta = delta;
            }
        }

    }
}

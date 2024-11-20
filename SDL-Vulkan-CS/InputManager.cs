using SDL3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SDL_Vulkan_CS
{
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

        public static InputManager Instance { get; private set; }

        public unsafe InputManager()
        {
            Instance = this;
            SDL3.SDL3.SDL_AddEventWatch(&KeyboardMove, IntPtr.Zero);
            SDL3.SDL3.SDL_AddEventWatch(&RightClick, IntPtr.Zero);
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        private static unsafe SDLBool RightClick(nint n, SDL_Event* eventPtr)
        {
            if (eventPtr->button.Button == SDL_Button.Right)
            {
                if(eventPtr->type == SDL_EventType.MouseButtonDown)
                {
                    Instance.rightMouseDown = true;
                }
                else if(eventPtr->type == SDL_EventType.MouseButtonUp)
                {
                    Instance.rightMouseDown = false;
                    Instance.firstMouse = true;
                }
            }
            return false;
        }

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
                        }
                        Instance.moveInput = cur;
                        break;
                    }
            }
            return false;
        }

        public void Update()
        {
            //Console.WriteLine(mouseInput.ToString());
        }

        public void LateUpdate()
        {
            mouseDelta = Vector2.Zero;
            //mousePosOld = mousePos;
            mouseMotion = false;
        }
    }
}

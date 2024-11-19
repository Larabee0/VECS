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
        public Vector2 moveInput = Vector2.Zero;
        public Vector2 mousePos = Vector2.Zero;
        public Vector2 mousePosOld = Vector2.Zero;
        public Vector2 Delta => mousePos - mousePosOld;
        public static InputManager Instance { get; private set; }

        public unsafe InputManager()
        {
            Instance = this;
            SDL3.SDL3.SDL_AddEventWatch(&KeyboardMove, IntPtr.Zero);
            SDL3.SDL3.SDL_AddEventWatch(&MouseDelta, IntPtr.Zero);
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        private static unsafe SDLBool MouseDelta(nint n, SDL_Event* eventPtr)
        {
            if (eventPtr->type == SDL_EventType.MouseMotion)
            {
                var cur = Instance.mousePos;
                cur.X = eventPtr->motion.x;
                cur.Y = eventPtr->motion.y;
                Instance.mousePos = cur;
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
                                cur.Y = 1;
                                break;
                            case SDL_Keycode.S:
                                cur.Y = -1;
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
                            case SDL_Keycode.W when cur.Y > 0:
                                cur.Y = 0;
                                break;
                            case SDL_Keycode.S when cur.Y < 0:
                                cur.Y = 0;
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
            mousePosOld = mousePos;
        }
    }
}

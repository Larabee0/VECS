using Noesis;
using SDL3;

namespace VECS
{
    public static class SDL_KeycodeExtensions
    {
        public static Key ToNoesis(this SDL_Keycode keycode)
        {
            return keycode switch
            {
                SDL_Keycode.Return => Key.Return,
                SDL_Keycode.Escape => Key.Escape,
                SDL_Keycode.Backspace => Key.Back,
                SDL_Keycode.Tab => Key.Tab,
                SDL_Keycode.Space => Key.Space,
                // case SDL_Keycode.Exclaim:
                //     return Key
                // case SDL_Keycode.Dblapostrophe:
                //     return Key
                // case SDL_Keycode.Hash:
                //     return Key
                // case SDL_Keycode.Dollar:
                //     break;
                // case SDL_Keycode.Percent:
                //     break;
                // case SDL_Keycode.Ampersand:
                //     break;
                // case SDL_Keycode.Apostrophe:
                //     break;
                // case SDL_Keycode.LeftParen:
                //     break;
                // case SDL_Keycode.RightParen:
                //     break;
                // case SDL_Keycode.Asterisk:
                //     break;
                SDL_Keycode.Plus => Key.OemPlus,
                SDL_Keycode.Comma => Key.OemComma,
                SDL_Keycode.Minus => Key.OemMinus,
                SDL_Keycode.Period => Key.OemPeriod,
                // case SDL_Keycode.Slash:
                //     return Key
                SDL_Keycode._0 => Key.D0,
                SDL_Keycode._1 => Key.D1,
                SDL_Keycode._2 => Key.D2,
                SDL_Keycode._3 => Key.D3,
                SDL_Keycode._4 => Key.D4,
                SDL_Keycode._5 => Key.D5,
                SDL_Keycode._6 => Key.D6,
                SDL_Keycode._7 => Key.D7,
                SDL_Keycode._8 => Key.D8,
                SDL_Keycode._9 => Key.D9,
                // case SDL_Keycode.Colon:
                //     return Key
                SDL_Keycode.Semicolon => Key.OemSemicolon,
                // case SDL_Keycode.Less:
                //     return Key
                // case SDL_Keycode.Equals:
                //    return Key
                // case SDL_Keycode.Greater:
                //     return Key
                SDL_Keycode.Question => Key.OemQuestion,
                // case SDL_Keycode.At: // @
                //     Key.At
                SDL_Keycode.LeftBracket => Key.OemOpenBrackets,
                SDL_Keycode.Backslash => Key.OemBackslash,
                SDL_Keycode.RightBracket => Key.OemCloseBrackets,
                // case SDL_Keycode.Caret: // ^
                //     return Key
                // case SDL_Keycode.Underscore: // _
                //     return Key
                // case SDL_Keycode.Grave: // `
                //     return Key
                SDL_Keycode.A => Key.A,
                SDL_Keycode.B => Key.B,
                SDL_Keycode.C => Key.C,
                SDL_Keycode.D => Key.D,
                SDL_Keycode.E => Key.E,
                SDL_Keycode.F => Key.F,
                SDL_Keycode.G => Key.G,
                SDL_Keycode.H => Key.H,
                SDL_Keycode.I => Key.I,
                SDL_Keycode.J => Key.J,
                SDL_Keycode.K => Key.K,
                SDL_Keycode.L => Key.L,
                SDL_Keycode.M => Key.M,
                SDL_Keycode.N => Key.N,
                SDL_Keycode.O => Key.O,
                SDL_Keycode.P => Key.P,
                SDL_Keycode.Q => Key.Q,
                SDL_Keycode.R => Key.R,
                SDL_Keycode.S => Key.S,
                SDL_Keycode.T => Key.T,
                SDL_Keycode.U => Key.U,
                SDL_Keycode.V => Key.V,
                SDL_Keycode.W => Key.W,
                SDL_Keycode.X => Key.X,
                SDL_Keycode.Y => Key.Y,
                SDL_Keycode.Z => Key.Z,
                // case SDL_Keycode.Leftbrace: // {
                //     return Key
                SDL_Keycode.Pipe => Key.OemPipe,
                // case SDL_Keycode.Rightbrace: // }
                //     break;
                SDL_Keycode.Tilde => Key.OemTilde,
                SDL_Keycode.Delete => Key.Delete,
                // case SDL_Keycode.PlusMinus:
                //     return Key
                SDL_Keycode.Capslock => Key.CapsLock,
                SDL_Keycode.F1 => Key.F1,
                SDL_Keycode.F2 => Key.F2,
                SDL_Keycode.F3 => Key.F3,
                SDL_Keycode.F4 => Key.F4,
                SDL_Keycode.F5 => Key.F5,
                SDL_Keycode.F6 => Key.F6,
                SDL_Keycode.F7 => Key.F7,
                SDL_Keycode.F8 => Key.F8,
                SDL_Keycode.F9 => Key.F9,
                SDL_Keycode.F10 => Key.F10,
                SDL_Keycode.F11 => Key.F11,
                SDL_Keycode.F12 => Key.F12,
                SDL_Keycode.PrintScreen => Key.PrintScreen,
                SDL_Keycode.ScrollLock => Key.Scroll,
                SDL_Keycode.Pause => Key.Pause,
                SDL_Keycode.Insert => Key.Insert,
                SDL_Keycode.Home => Key.Home,
                SDL_Keycode.PageUp => Key.PageUp,
                SDL_Keycode.End => Key.End,
                SDL_Keycode.PageDown => Key.PageDown,
                SDL_Keycode.Right => Key.Right,
                SDL_Keycode.Left => Key.Left,
                SDL_Keycode.Down => Key.Down,
                SDL_Keycode.Up => Key.Up,
                SDL_Keycode.NumLockClear => Key.OemClear,
                SDL_Keycode.KpDivide => Key.Divide,
                SDL_Keycode.KpMultiply => Key.Multiply,
                SDL_Keycode.KpMinus => Key.OemMinus,
                SDL_Keycode.KpPlus => Key.OemPlus,
                SDL_Keycode.KpEnter => Key.Enter,
                SDL_Keycode.Kp1 => Key.NumPad1,
                SDL_Keycode.Kp2 => Key.NumPad2,
                SDL_Keycode.Kp3 => Key.NumPad3,
                SDL_Keycode.Kp4 => Key.NumPad4,
                SDL_Keycode.Kp5 => Key.NumPad5,
                SDL_Keycode.Kp6 => Key.NumPad6,
                SDL_Keycode.Kp7 => Key.NumPad7,
                SDL_Keycode.Kp8 => Key.NumPad8,
                SDL_Keycode.Kp9 => Key.NumPad9,
                SDL_Keycode.Kp0 => Key.NumPad0,
                SDL_Keycode.KpPeriod => Key.OemPeriod,
                SDL_Keycode.Application => Key.Apps,
                // case SDL_Keycode.Power:
                //     return Key
                SDL_Keycode.KpEquals => Key.Enter,
                SDL_Keycode.F13 => Key.F13,
                SDL_Keycode.F14 => Key.F14,
                SDL_Keycode.F15 => Key.F15,
                SDL_Keycode.F16 => Key.F16,
                SDL_Keycode.F17 => Key.F17,
                SDL_Keycode.F18 => Key.F18,
                SDL_Keycode.F19 => Key.F19,
                SDL_Keycode.F20 => Key.F20,
                SDL_Keycode.F21 => Key.F21,
                SDL_Keycode.F22 => Key.F22,
                SDL_Keycode.F23 => Key.F23,
                SDL_Keycode.F24 => Key.F24,
                SDL_Keycode.Execute => Key.Execute,
                SDL_Keycode.Help => Key.Help,
                // case SDL_Keycode.Menu:
                //     return Key
                SDL_Keycode.Select => Key.Select,
                // case SDL_Keycode.Stop:
                //     return Key;
                // case SDL_Keycode.Again:
                //     break;
                // case SDL_Keycode.Undo:
                //     break;
                // case SDL_Keycode.Cut:
                //     break;
                SDL_Keycode.Copy => Key.OemCopy,
                // case SDL_Keycode.Paste:
                //     break;
                // case SDL_Keycode.Find:
                //     return Key
                SDL_Keycode.Mute => Key.VolumeMute,
                SDL_Keycode.VolumeUp => Key.VolumeUp,
                SDL_Keycode.VolumeDown => Key.VolumeUp,
                SDL_Keycode.KpComma => Key.OemComma,
                // case SDL_Keycode.KpEqualsas400:
                //     break;
                // case SDL_Keycode.Alterase:
                //     return Key
                // case SDL_Keycode.Sysreq:
                //     return Key
                SDL_Keycode.Cancel => Key.Cancel,
                SDL_Keycode.Clear => Key.Clear,
                SDL_Keycode.Prior => Key.Prior,
                // case SDL_Keycode.Return2:
                //     return Key
                SDL_Keycode.Separator => Key.Separator,
                // case SDL_Keycode.Out:
                //     return Key
                // case SDL_Keycode.Oper:
                //     return Key
                // case SDL_Keycode.Clearagain:
                //     return Key
                SDL_Keycode.Crsel => Key.CrSel,
                SDL_Keycode.Exsel => Key.ExSel,
                // case SDL_Keycode.Kp00:
                //     return Key
                // case SDL_Keycode.Kp000:
                //     return Key
                // case SDL_Keycode.Thousandsseparator:
                //     return Key
                // case SDL_Keycode.Decimalseparator:
                //     break;
                // case SDL_Keycode.Currencyunit:
                //     return Key
                // case SDL_Keycode.Currencysubunit:
                //     break;
                // case SDL_Keycode.KpLeftParen:
                //     break;
                // case SDL_Keycode.KpRightParen:
                //     break;
                // case SDL_Keycode.KpLeftbrace:
                //     break;
                // case SDL_Keycode.KpRightbrace:
                //     break;
                // case SDL_Keycode.KpTab:
                //     break;
                // case SDL_Keycode.KpBackspace:
                //     break;
                // case SDL_Keycode.KpA:
                //     break;
                // case SDL_Keycode.KpB:
                //     break;
                // case SDL_Keycode.KpC:
                //     break;
                // case SDL_Keycode.KpD:
                //     break;
                // case SDL_Keycode.KpE:
                //     break;
                // case SDL_Keycode.KpF:
                //     break;
                // case SDL_Keycode.KpXor:
                //     return Key
                // case SDL_Keycode.KpPower:
                //     break;
                // case SDL_Keycode.KpPercent:
                //     return Key
                // case SDL_Keycode.KpLess:
                //     break;
                // case SDL_Keycode.KpGreater:
                //     break;
                // case SDL_Keycode.KpAmpersand:
                //     return Key
                // case SDL_Keycode.KpDblampersand:
                //     break;
                // case SDL_Keycode.KpVerticalbar:
                //     return Key
                // case SDL_Keycode.KpDblverticalbar:
                //     break;
                // case SDL_Keycode.KpColon:
                //     return Key
                // case SDL_Keycode.KpHash:
                //     break;
                // case SDL_Keycode.KpSpace:
                //     break;
                // case SDL_Keycode.KpAt:
                //     break;
                // case SDL_Keycode.KpExclam:
                //     break;
                // case SDL_Keycode.KpMemstore:
                //     break;
                // case SDL_Keycode.KpMemrecall:
                //     break;
                // case SDL_Keycode.KpMemclear:
                //     break;
                // case SDL_Keycode.KpMemadd:
                //     break;
                // case SDL_Keycode.KpMemsubtract:
                //     break;
                // case SDL_Keycode.KpMemmultiply:
                //     break;
                // case SDL_Keycode.KpMemdivide:
                //     break;
                // case SDL_Keycode.KpPlusMinus:
                //     break;
                // case SDL_Keycode.KpClear:
                //     break;
                // case SDL_Keycode.KpClearentry:
                //     break;
                // case SDL_Keycode.KpBinary:
                //     break;
                // case SDL_Keycode.KpOctal:
                //     break;
                // case SDL_Keycode.KpDecimal:
                //     break;
                // case SDL_Keycode.KpHexadecimal:
                //     break;
                SDL_Keycode.LeftControl => Key.LeftCtrl,
                SDL_Keycode.LeftShift => Key.LeftShift,
                SDL_Keycode.LeftAlt => Key.LeftAlt,
                SDL_Keycode.LeftGui => Key.LWin,
                SDL_Keycode.RightControl => Key.RightCtrl,
                SDL_Keycode.RightShift => Key.RightShift,
                SDL_Keycode.RightAlt => Key.RightAlt,
                SDL_Keycode.RightGui => Key.RWin,
                // case SDL_Keycode.Mode:
                //     return Key
                SDL_Keycode.Sleep => Key.Sleep,
                // case SDL_Keycode.Wake:
                //     return Key
                // case SDL_Keycode.ChannelIncrement:
                //     return Key
                // case SDL_Keycode.ChannelDecrement:
                //     break;
                SDL_Keycode.MediaPlay => Key.Play,
                SDL_Keycode.MediaPause => Key.Pause,
                // case SDL_Keycode.MediaRecord:
                //     return Key
                // case SDL_Keycode.MediaFastForward:
                //     return Key
                // case SDL_Keycode.MediaRewind:
                //     break;
                SDL_Keycode.MediaNextTrack => Key.MediaNextTrack,
                SDL_Keycode.MediaPreviousTrack => Key.MediaPreviousTrack,
                SDL_Keycode.MediaStop => Key.MediaStop,
                // case SDL_Keycode.MediaEject:
                //     return Key
                SDL_Keycode.MediaPlayPause => Key.MediaPlayPause,
                SDL_Keycode.MediaSelect => Key.SelectMedia,
                // case SDL_Keycode.AcNew:
                //     return Key
                // case SDL_Keycode.AcOpen:
                //     
                // case SDL_Keycode.AcClose:
                //     break;
                // case SDL_Keycode.AcExit:
                //     break;
                // case SDL_Keycode.AcSave:
                //     break;
                SDL_Keycode.AcPrint => Key.Print,
                // case SDL_Keycode.AcProperties:
                //     break;
                SDL_Keycode.AcSearch => Key.BrowserSearch,
                SDL_Keycode.AcHome => Key.BrowserHome,
                SDL_Keycode.AcBack => Key.BrowserBack,
                SDL_Keycode.AcForward => Key.BrowserForward,
                SDL_Keycode.AcStop => Key.BrowserStop,
                SDL_Keycode.AcRefresh => Key.BrowserRefresh,
                SDL_Keycode.AcBookmarks => Key.BrowserFavorites,
                // case SDL_Keycode.Softleft:
                //     break;
                // case SDL_Keycode.Softright:
                //     break;
                // case SDL_Keycode.Call:
                //     return Key
                // case SDL_Keycode.Endcall:
                //     break;
                // case SDL_Keycode.LeftTab:
                //     return Key
                // case SDL_Keycode.Level5Shift:
                //     break;
                // case SDL_Keycode.MultiKeyCompose:
                //     break;
                // case SDL_Keycode.Lmeta:
                //     break;
                // case SDL_Keycode.Rmeta:
                //     break;
                // case SDL_Keycode.Lhyper:
                //     break;
                // case SDL_Keycode.Rhyper:
                //     break;
                _ => Key.None,
            };
        }

    }
}

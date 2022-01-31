using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Bitfish
{
    public class KeyHandler
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = false)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        /// <summary>
        /// Sends a Keydown message(0x100) to the specified window with a Virtual Key
        /// </summary>
        /// <param name="winTitle">Window Title</param>
        /// <param name="Key">Key to Send</param>
        public static void KeyDown(string winTitle, int Key)
        {
            IntPtr hWnd = FindWindow(null, winTitle);
            SendMessage(hWnd, 0x100, Key, 0);
        }

        /// <summary>
        /// Sends a Keydup message(0x101) to the specified window with a Virtual Key
        /// </summary>
        /// <param name="winTitle">Window Title</param>
        /// <param name="Key">Key to Send</param>
        public static void KeyUp(string winTitle, int Key)
        {
            IntPtr hWnd = FindWindow(null, winTitle);
            SendMessage(hWnd, 0x101, Key, 0);
        }

        /// <summary>
        /// Simulates a key press which is a combination of KeyDown and KeyUp.
        /// It also have some slight delay
        /// </summary>
        public static void PressKey(int Key)
        {
            Key = 0x30 + Key;
            KeyDown("World of Warcraft", Key);
            Thread.Sleep(150);
            KeyUp("World of Warcraft", Key);
        }
    }
}

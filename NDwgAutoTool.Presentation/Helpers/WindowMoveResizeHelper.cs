using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace NDwgAutoTool.Helpers
{
    public static class WindowMoveResizeHelper
    {
        private const int WmNcLeftButtonDown = 0xA1;
        private const int HtCaption = 0x2;
        private const int HtBottomRight = 17;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        public static void Move(Window window)
        {
            ReleaseCapture();

            var handle = new WindowInteropHelper(window).Handle;
            SendMessage(handle, WmNcLeftButtonDown, HtCaption, 0);
        }

        public static void ResizeFromBottomRight(Window window)
        {
            ReleaseCapture();

            var handle = new WindowInteropHelper(window).Handle;
            SendMessage(handle, WmNcLeftButtonDown, HtBottomRight, 0);
        }
    }
}

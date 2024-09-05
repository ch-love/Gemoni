using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

class Program
{
    const int MOD_CTRL = 0x0002;
    const int MOD_SHIFT = 0x0004;
    const int WM_HOTKEY = 0x0312;
    const int SNAP_MARGIN = 50;  // Margin within which snapping occurs

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

    [DllImport("user32.dll")]
    static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern bool SystemParametersInfo(uint action, uint param, ref RECT rect, uint update);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    static void Main()
    {
        // Register global hotkeys for arrow key movements with Ctrl+Shift
        RegisterHotKey(IntPtr.Zero, 1, MOD_CTRL | MOD_SHIFT, (uint)Keys.Up);
        RegisterHotKey(IntPtr.Zero, 2, MOD_CTRL | MOD_SHIFT, (uint)Keys.Down);
        RegisterHotKey(IntPtr.Zero, 3, MOD_CTRL | MOD_SHIFT, (uint)Keys.Left);
        RegisterHotKey(IntPtr.Zero, 4, MOD_CTRL | MOD_SHIFT, (uint)Keys.Right);

        Application.Run(new MyApplicationContext());

        // Unregister hotkeys when the application exits
        UnregisterHotKey(IntPtr.Zero, 1);
        UnregisterHotKey(IntPtr.Zero, 2);
        UnregisterHotKey(IntPtr.Zero, 3);
        UnregisterHotKey(IntPtr.Zero, 4);
    }

    private class MyApplicationContext : ApplicationContext
    {
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                IntPtr hWnd = GetForegroundWindow();
                RECT rect;
                GetWindowRect(hWnd, out rect);

                // Handle window movement based on the pressed key
                switch (m.WParam.ToInt32())
                {
                    case 1: // Up arrow
                        MoveWindowOrSnap(hWnd, rect, 0, -100);
                        break;
                    case 2: // Down arrow
                        MoveWindowOrSnap(hWnd, rect, 0, 100);
                        break;
                    case 3: // Left arrow
                        MoveWindowOrSnap(hWnd, rect, -100, 0);
                        break;
                    case 4: // Right arrow
                        MoveWindowOrSnap(hWnd, rect, 100, 0);
                        break;
                }
            }

            base.WndProc(ref m);
        }
    }

    static void MoveWindowOrSnap(IntPtr hWnd, RECT rect, int deltaX, int deltaY)
    {
        int newX = rect.Left + deltaX;
        int newY = rect.Top + deltaY;

        // Get screen dimensions and handle multi-monitor setups
        Screen currentScreen = Screen.FromHandle(hWnd);
        RECT screenBounds = new RECT
        {
            Left = currentScreen.Bounds.Left,
            Top = currentScreen.Bounds.Top,
            Right = currentScreen.Bounds.Right,
            Bottom = currentScreen.Bounds.Bottom
        };

        // Ensure the new position stays within the screen boundaries
        if (newX < screenBounds.Left) newX = screenBounds.Left;
        if (newY < screenBounds.Top) newY = screenBounds.Top;
        if (newX + (rect.Right - rect.Left) > screenBounds.Right)
            newX = screenBounds.Right - (rect.Right - rect.Left);
        if (newY + (rect.Bottom - rect.Top) > screenBounds.Bottom)
            newY = screenBounds.Bottom - (rect.Bottom - rect.Top);

        // Check for other windows to snap around the new position
        bool shouldSnap = false;
        EnumWindows((EnumWindowsProc)((otherHWnd, lParam) =>
        {
            if (IsWindowVisible(otherHWnd) && otherHWnd != hWnd)
            {
                RECT otherRect;
                GetWindowRect(otherHWnd, out otherRect);

                // Check if the other window is within snapping distance
                if (Math.Abs(otherRect.Left - newX) < SNAP_MARGIN && Math.Abs(otherRect.Top - newY) < SNAP_MARGIN)
                {
                    shouldSnap = true;
                    return false; // Stop enumerating windows
                }
            }
            return true;
        }), IntPtr.Zero);

        if (!shouldSnap)
        {
            // Move the window smoothly to the new location
            SmoothMoveWindow(hWnd, newX, newY, rect.Right - rect.Left, rect.Bottom - rect.Top);
        }
        else
        {
            // Snap logic: Find the nearest available spot (could be a more complex snapping algorithm)
            FindClosestSnapPosition(ref newX, ref newY, screenBounds);
            SmoothMoveWindow(hWnd, newX, newY, rect.Right - rect.Left, rect.Bottom - rect.Top);
        }
    }

    static void SmoothMoveWindow(IntPtr hWnd, int newX, int newY, int width, int height)
    {
        const int steps = 10;
        RECT currentRect;
        GetWindowRect(hWnd, out currentRect);
        int stepX = (newX - currentRect.Left) / steps;
        int stepY = (newY - currentRect.Top) / steps;

        for (int i = 1; i <= steps; i++)
        {
            int intermediateX = currentRect.Left + stepX * i;
            int intermediateY = currentRect.Top + stepY * i;
            MoveWindow(hWnd, intermediateX, intermediateY, width, height, true);
            System.Threading.Thread.Sleep(10); // Add delay to smooth the movement
        }
    }

    static void FindClosestSnapPosition(ref int newX, ref int newY, RECT screenBounds)
    {
        // Implement more advanced snapping logic here, e.g., snapping to grid or nearest free area
        // Example: Snap to top-left corner of screen if there's no room
        if (newX < screenBounds.Left + SNAP_MARGIN) newX = screenBounds.Left;
        if (newY < screenBounds.Top + SNAP_MARGIN) newY = screenBounds.Top;
    }
}

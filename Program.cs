using EventHook;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Minesweeper_Bot_.Net_Framework
{
    internal class Program
    {
        private static System.Drawing.Point click1;
        private static System.Drawing.Point click2;
        static void Main(string[] args)
        {
            bool listening = false;
            bool click = false;

            Console.WriteLine("Enter Game Size (heigth x width) ex: 9x9");
            string[] gamesizestring = Console.ReadLine().Split('x');
            int[] gamesize = new int[] { int.Parse(gamesizestring[0]), int.Parse(gamesizestring[1]) };
            Console.WriteLine("Press F10 to Run the Bot");

            using (var eventHookFactory = new EventHookFactory())
            {
                var keyboardWatcher = eventHookFactory.GetKeyboardWatcher();
                keyboardWatcher.Start();
                var mouseWatcher = eventHookFactory.GetMouseWatcher();
                mouseWatcher.Start();
                keyboardWatcher.OnKeyInput += (s, e) =>
                {
                    if (e.KeyData.Keyname == Keys.F8.ToString() && e.KeyData.EventType == KeyEvent.up)
                    {
                        listening = !listening;
                    }
                    if (e.KeyData.Keyname == Keys.F10.ToString() && e.KeyData.EventType == KeyEvent.up)
                    {
                        listening = false;
                        mouseWatcher.Stop();
                        Console.WriteLine("Starting bot");
                        Bot(gamesize);
                        mouseWatcher.Start();
                    }
                };

                mouseWatcher.OnMouseInput += (s, e) =>
                {
                    if (e.Message == EventHook.Hooks.MouseMessages.WM_LBUTTONUP) {
                        //Console.WriteLine("Clicked at position: ({0},{1})", Cursor.Position.X, Cursor.Position.Y);
                        if (listening)
                        {
                            if (!click)
                            {
                                click1 = Cursor.Position;
                            }
                            else
                            {
                                click2 = Cursor.Position;
                            }
                            click = !click;
                        }
                    }
                };

                //waiting here to keep this thread running           
                Console.Read();

                //stop watching
                keyboardWatcher.Stop();
                mouseWatcher.Stop();
            }
        }

        public static void Bot(int[] gamesize)
        {
            Bitmap bitmap = CaptureScreen();

            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
ImageLockMode.ReadOnly, bitmap.PixelFormat);  // make sure you check the pixel format as you will be looking directly at memory

            var smiley = FindSmiley(data);
            if (smiley == null)
            {
                return;
            }
            var topleft = FindTopLeftCorner(data, smiley);
            if (topleft == null)
            {
                return;
            }
            var topright = FindTopRightCorner(data, topleft, gamesize[1]);
            if (topright == null)
            {
                return;
            }
            var bottomright = FindBottomRightCorner(data, topright, gamesize[0]);
            if (bottomright == null)
            {
                return;
            }
            var bottomleft = FindBottomLeftCorner(data, bottomright, gamesize[1]);
            if (bottomleft == null)
            {
                return;
            }

            var center = new int[] { (topleft[0] + bottomright[0]) / 2, (topleft[1] + bottomright[1]) / 2 };
            if (gamesize[1] % 2 == 0)
            {
                center[0] += (topright[0] - topleft[0]) / gamesize[0] / 2;
            }
            if (gamesize[0] % 2 == 0)
            {
                center[1] += (topright[1] - bottomright[1]) / gamesize[1] / 2;
            }
            Cursor.Position = new Point((int)(center[0] * 0.8), (int)(center[1] * 0.8));
            VirtualMouse.LeftClick();

            bitmap.UnlockBits(data);

            //var startingpos = new System.Drawing.Point((click1.X+click2.X)/2, (click1.Y+click2.Y)/2);
            //var boardSize = new int[] { Math.Abs(click1.X - click2.X), Math.Abs(click1.Y - click2.Y) };
            //var boxSize = boardSize[0] / gamesize[0];
            //// check if the gamesize is even
            //// if it is, move half a box to the right or down to click the center of the box
            //if (gamesize[0] % 2 == 0)
            //{
            //    startingpos.X += boxSize / 2;
            //}
            //if (gamesize[1] % 2 == 0)
            //{
            //    startingpos.Y += boxSize / 2;
            //}
            //Console.WriteLine("Clicking at Point: {0},{1}", startingpos.X, startingpos.Y);
            //Cursor.Position = startingpos;
            //VirtualMouse.LeftClick();
        }

        public static int[] FindSmiley(BitmapData data)
        {
            int counter = 0;
            unsafe
            {
                // example assumes 24bpp image.  You need to verify your pixel depth
                // loop by row for better data locality
                for (int y = 0; y < data.Height; ++y)
                {
                    byte* pRow = (byte*)data.Scan0 + y * data.Stride;
                    for (int x = 0; x < data.Width; ++x)
                    {
                        // windows stores images in BGR pixel order
                        byte r = pRow[2];
                        byte g = pRow[1];
                        byte b = pRow[0];

                        if (r == 255 && g == 255 && b == 0)
                        {
                            counter++;
                            //Console.WriteLine("Found Yellow at address ({0},{1})", x, y);
                        }
                        else
                        {
                            counter = 0;
                        }
                        if (counter > 15)
                        {
                            Console.WriteLine("Found Smiley at ({0},{1})", x, y);
                            var location = new int[] { x, y };
                            return location;
                        }

                        // next pixel in the row
                        pRow += 3;
                    }
                }
                Console.WriteLine("No Smiley was found");
                return null;
            }
        }

        public static int[] FindTopLeftCorner(BitmapData data, int[] location)
        {
            unsafe
            {
                bool found1 = false;
                bool found2 = false;
                // progress downward untill a white pixel is found
                for (int y = location[1]; y < data.Height; ++y)
                {
                    byte* pRow1 = (byte*)data.Scan0 + y * data.Stride;
                    int start = location[0] * 3;
                    byte r = pRow1[start + 2];
                    byte g = pRow1[start + 1];
                    byte b = pRow1[start + 0];
                    // check for white
                    if (!found2)
                    {
                        if (!found1)
                        {
                            // found first white
                            if (r > 220 && g > 220 && b > 220)
                            {
                                // go until no more white
                                found1 = true;
                            }
                        }
                        else
                        {
                            // found not white
                            if (r < 220 && g < 220 && b < 220)
                            {
                                // go until second white
                                found2 = true;
                            }
                        }
                    }
                    else
                    {
                        // found second white
                        if (r > 220 && g > 220 && b > 220)
                        {
                            // start going left
                            Console.WriteLine("Found top of white at ({0},{1})", location[0], y);
                            location[1] = y;
                            break;
                        }
                    }
                }
                byte* pRow = (byte*)data.Scan0 + location[1] * data.Stride;
                pRow += location[0] * 3;
                for (int x = location[0]; x > 0; --x)
                {
                    byte r = pRow[2];
                    byte g = pRow[1];
                    byte b = pRow[0];

                    // check if pixel is that grey color
                    if (r < 185 & g < 185 & b < 185)
                    {
                        Console.WriteLine("Found Top Left Corner at ({0},{1})", x + 1, location[1]);
                        Console.WriteLine("Moving to Top Left Corner");
                        Cursor.Position = new Point((int)((x + 1) * 0.8), (int)(location[1] * 0.8));
                        return new int[] { x + 1, location[1] };
                    }

                    pRow -= 3;
                }
                return null;
            }
        }

        public static int[] FindTopRightCorner(BitmapData data, int[] topleft, int width)
        {
            unsafe
            {
                int start = topleft[0];
                for (int i = 0; i < width; ++i)
                {
                    byte* pRow = (byte*)data.Scan0 + topleft[1] * data.Stride;
                    pRow += topleft[0] * 3;
                    bool found = false;
                    for (int x = start; x < data.Width; ++x)
                    {
                        byte r = pRow[2];
                        byte g = pRow[1];
                        byte b = pRow[0];
                        // navigate until it finds a color thats not white
                        if (r < 212 & g < 212 & b < 212)
                        {
                            found = true;
                        }
                        // then navigate to a color that is white
                        if (found & r > 212 & g > 212 & b > 212)
                        {
                            // found location of next box
                            start = x;
                            //Console.WriteLine("Found box at ({0},{1})", x, topleft[1]);
                            break;
                        }
                        pRow += 3;
                    }
                }
                Console.WriteLine("Found Top Right Corner at ({0},{1})", start - 1, topleft[1]);
                Console.WriteLine("Moving to Top Left Corner");
                Cursor.Position = new Point((int)((start-1)*0.8), (int)(topleft[1]*0.8));
                return new int[] {start-1, topleft[1]};
            }
        }

        public static int[] FindBottomRightCorner(BitmapData data, int[] topright, int height)
        {
            unsafe
            {
                int start = topright[1];
                for (int i = 0; i < height; ++i)
                {
                    bool found = false;
                    for (int y = start; y < data.Height; ++y)
                    {
                        byte* pRow = (byte*)data.Scan0 + y * data.Stride + topright[0] * 3;
                        byte r = pRow[2];
                        byte g = pRow[1];
                        byte b = pRow[0];
                        // navigate until it finds a color thats light grey
                        if (r < 150 & g < 150 & b < 150)
                        {
                            found = true;
                        }
                        // then navigate to a color that is dark grey
                        if (found & r > 150 & g > 150 & b > 150)
                        {
                            // found location of next box
                            start = y;
                            //Console.WriteLine("Found box at ({0},{1})", topright[0], y);
                            break;
                        }
                    }
                }
                Console.WriteLine("Found Bottom Right Corner at ({0},{1})", topright[0], start - 1);
                Console.WriteLine("Moving to Bottom Right Corner");
                Cursor.Position = new Point((int)(topright[0] * 0.8), (int)((start-1) * 0.8));
                return new int[] { topright[0], start-1 };
            }
        }

        public static int[] FindBottomLeftCorner(BitmapData data, int[] bottomright, int width)
        {
            unsafe
            {
                int start = bottomright[0];
                for (int i = 0; i < width; ++i)
                {
                    byte* pRow = (byte*)data.Scan0 + (bottomright[1]-1) * data.Stride + start * 3;
                    bool found = false;
                    for (int x = start; x < data.Width; --x)
                    {
                        byte r = pRow[2];
                        byte g = pRow[1];
                        byte b = pRow[0];
                        //Console.WriteLine("{0} {1} {2}", r, g, b); 
                        // navigate until it finds a color thats light grey
                        if (r > 150 & g > 150 & b > 150)
                        {
                            found = true;
                        }
                        // then navigate to a color that is dark grey
                        if (found & r < 150 & g < 150 & b < 150)
                        {
                            // found location of next box
                            start = x;
                            //Console.WriteLine("Found box at ({0},{1})", topright[0], y);
                            break;
                        }
                        pRow -= 3;
                    }
                }
                Console.WriteLine("Found Bottom Left Corner at ({0},{1})", start + 1, bottomright[1]);
                Console.WriteLine("Moving to Bottom Left Corner");
                Cursor.Position = new Point((int)((start+1) * 0.8), (int)(bottomright[1] * 0.8));
                return new int[] { start+1, bottomright[1] };
            }
        }

        public static Bitmap CaptureScreen()
        {
            var image = new Bitmap(1920, 1080, PixelFormat.Format24bppRgb);
            var gfx = Graphics.FromImage(image);
            gfx.CopyFromScreen(0, 0, 0, 0, image.Size, CopyPixelOperation.SourceCopy);
            image.Save("capture.jpg", ImageFormat.Jpeg);
            return image;
        }

        public static byte[] ImageToByte(Image img)
        {
            ImageConverter converter = new ImageConverter();
            return (byte[])converter.ConvertTo(img, typeof(byte[]));
        }
    }

    public static class VirtualMouse
    {
        [DllImport("user32.dll")]
        static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);
        private const int MOUSEEVENTF_MOVE = 0x0001;
        private const int MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const int MOUSEEVENTF_LEFTUP = 0x0004;
        private const int MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const int MOUSEEVENTF_RIGHTUP = 0x0010;
        private const int MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const int MOUSEEVENTF_MIDDLEUP = 0x0040;
        private const int MOUSEEVENTF_ABSOLUTE = 0x8000;
        public static void Move(int xDelta, int yDelta)
        {
            mouse_event(MOUSEEVENTF_MOVE, xDelta, yDelta, 0, 0);
        }
        public static void MoveTo(int x, int y)
        {
            mouse_event(MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_MOVE, x, y, 0, 0);
        }
        public static void LeftClick()
        {
            mouse_event(MOUSEEVENTF_LEFTDOWN, System.Windows.Forms.Control.MousePosition.X, System.Windows.Forms.Control.MousePosition.Y, 0, 0);
            mouse_event(MOUSEEVENTF_LEFTUP, System.Windows.Forms.Control.MousePosition.X, System.Windows.Forms.Control.MousePosition.Y, 0, 0);
        }

        public static void LeftDown()
        {
            mouse_event(MOUSEEVENTF_LEFTDOWN, System.Windows.Forms.Control.MousePosition.X, System.Windows.Forms.Control.MousePosition.Y, 0, 0);
        }

        public static void LeftUp()
        {
            mouse_event(MOUSEEVENTF_LEFTUP, System.Windows.Forms.Control.MousePosition.X, System.Windows.Forms.Control.MousePosition.Y, 0, 0);
        }

        public static void RightClick()
        {
            mouse_event(MOUSEEVENTF_RIGHTDOWN, System.Windows.Forms.Control.MousePosition.X, System.Windows.Forms.Control.MousePosition.Y, 0, 0);
            mouse_event(MOUSEEVENTF_RIGHTUP, System.Windows.Forms.Control.MousePosition.X, System.Windows.Forms.Control.MousePosition.Y, 0, 0);
        }

        public static void RightDown()
        {
            mouse_event(MOUSEEVENTF_RIGHTDOWN, System.Windows.Forms.Control.MousePosition.X, System.Windows.Forms.Control.MousePosition.Y, 0, 0);
        }

        public static void RightUp()
        {
            mouse_event(MOUSEEVENTF_RIGHTUP, System.Windows.Forms.Control.MousePosition.X, System.Windows.Forms.Control.MousePosition.Y, 0, 0);
        }
    }
}
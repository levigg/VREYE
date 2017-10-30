using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using EyeXFramework;
using Tobii.EyeX.Framework;
using System.Windows.Threading;
using EyeXFramework.Wpf;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Microsoft.Win32;
using System.Windows.Forms;
using System.Threading;

using WindowsHookSample;

namespace EyePlayerGame
{
    /// <summary>
    /// MainWindow.xaml 的互動邏輯
    /// </summary>
    public partial class MainWindow : Window
    {
        double screenHeight = SystemParameters.PrimaryScreenHeight;
        double screenWidth = SystemParameters.PrimaryScreenWidth;

        [DllImport("User32.dll")]
        private static extern bool SetCursorPos(int X, int Y);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern void mouse_event(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo);
        public const int MOUSEEVENTF_LEFTDOWN = 0x0002;
        public const int MOUSEEVENTF_LEFTUP = 0x0004;

        //[DllImport("user32.dll", EntryPoint = "keybd_event")]
        //public static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);
        //private const int KEYEVENTF_EXTENDEDKEY = 1;
        //private const int KEYEVENTF_KEYUP = 2;


        //Boolean gazeNotTracked = false;
        //Boolean gazeTracked = false;


        DispatcherTimer dt;
        public readonly WpfEyeXHost eHost = new WpfEyeXHost();

        public MainWindow()
        {
            InitializeComponent();

            eHost.Start();

            dt = new DispatcherTimer();
            dt.Tick += MouseControl;
            dt.Interval = new TimeSpan(1000 * 250);
            dt.Start();

            //全域鍵盤偵測
            KeyboardHook.Enabled = true;
            KeyboardHook.GlobalKeyUp += new EventHandler<KeyboardHook.KeyEventArgs>(GetKeyUp);

            StartEyeTrack();
        }

        public void StartEyeTrack()
        {
            GazePointDataStream gpda = eHost.CreateGazePointDataStream(GazePointDataMode.LightlyFiltered);
            gpda.Next += new EventHandler<GazePointEventArgs>(GetPos);

            //eHost.GazeTrackingChanged += new EventHandler<EngineStateValue<GazeTracking>>(GetGaze);
        }

        //public void GetGaze(object sender, EngineStateValue<GazeTracking> e)
        //{
        //    if (eHost.GazeTracking.ToString() == "GazeNotTracked")
        //    {
        //        gazeNotTracked = true;
        //        gazeTracked = false;
        //    }
        //    if (eHost.GazeTracking.ToString() == "GazeTracked")
        //    {
        //        gazeNotTracked = false;
        //        gazeTracked = true;
        //    }
        //}

        List<Point> posList = new List<Point>();

        private void GetPos(object sender, GazePointEventArgs e)
        {
            if (e.X > 0 && e.Y > 0)
            {
                Point eyePoint = new Point(e.X, e.Y);
                posList.Add(eyePoint);
            }
        }

        double smoothPosX = 0;
        double smoothPosY = 0;
        double newSmoothPosX;
        double newSmoothPosY;
        double posListAllX;
        double posListAllY;

        private void Smooth()
        {
            //Console.WriteLine(posList.Count);

            for (int i = 0; i < posList.Count; i++)
            {
                //Console.WriteLine(posList[i].X + ", " + posList[i].Y);
                posListAllX += posList[i].X;
                posListAllY += posList[i].Y;
            }
            //Console.WriteLine(posX + ", "+ posY);

            newSmoothPosX = posListAllX / posList.Count;
            newSmoothPosY = posListAllY / posList.Count;

            if (((newSmoothPosX - smoothPosX) > 1 || (smoothPosX - newSmoothPosX) > 1) && ((newSmoothPosY - smoothPosY) > 1 || (smoothPosY - newSmoothPosY) > 1))
            {
                smoothPosX = smoothPosX + (newSmoothPosX - smoothPosX) / 40;
                smoothPosY = smoothPosY + (newSmoothPosY - smoothPosY) / 40;
            }
            //Console.WriteLine(newSmoothPosX + ", " + newSmoothPosY);
            posListAllX = 0;
            posListAllY = 0;
            posList.Clear();

        }

        

        Boolean eyeControl = false;
        Boolean dragControl = false;

        private void MouseControl(object sender, EventArgs e)
        {

                Smooth();
                if (eyeControl == true)
                    SetCursorPos((int)smoothPosX, (int)smoothPosY);
            if (dragControl == true)
            {
                SetCursorPos((int)(screenWidth - smoothPosX), (int)(screenHeight - smoothPosY));
                //ReDrag();
            }

            //if (gazeNotTracked == true)
            //{
            //    gazeNotTracked = false;
            //}
            //if (gazeTracked == true)
            //{
            //    gazeTracked = false;
            //}

            Console.WriteLine(screenWidth);


        }



        //Boolean ReDragTimeStore;
        //DateTime ReDragTime;

        //public void ReDrag()
        //{
        //    if (ReDragTimeStore == false)
        //    {
        //        ReDragTime = DateTime.Now;
        //        ReDragTimeStore = true;
        //    }

        //    if ((DateTime.Now - ReDragTime).TotalSeconds > 0.5)
        //    {
        //        //SetCursorPos((int)screenWidth / 2, (int)screenHeight / 2);
        //        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
        //        Thread.Sleep(10);
        //        SetCursorPos((int)screenWidth / 2, (int)screenHeight / 2);
        //        mouse_event(MOUSEEVENTF_LEFTDOWN, (int)screenWidth / 2, (int)screenHeight / 2, 0, 0);
        //        //Thread.Sleep(10);

        //        ReDragTimeStore = false;
        //    }
        //}

        public void GetKeyUp(object sender, KeyboardHook.KeyEventArgs e)
        {
            switch (e.Key.ToString())
            {
                case "Escape":
                    System.Windows.Application.Current.Shutdown();
                    break;
                case "V":
                    if (eyeControl == false)
                    {
                        dragControl = false;
                        eyeControl = true;
                    }
                    else if (eyeControl == true)
                    {
                        eyeControl = false;
                    }
                    break;
                case "R":
                    if (dragControl == false)
                    {
                        SetCursorPos((int)screenWidth/2, (int)screenHeight/2);
                        mouse_event(MOUSEEVENTF_LEFTDOWN, (int)screenWidth / 2, (int)screenHeight / 2, 0, 0);
                        eyeControl = false;
                        dragControl = true;
                        vrDemo.Opacity = 1;
                    }
                    else if (dragControl == true)
                    {
                        SetCursorPos((int)screenWidth / 2, (int)screenHeight / 2);
                        Thread.Sleep(2000);
                        mouse_event(MOUSEEVENTF_LEFTUP, (int)screenWidth / 2, (int)screenHeight / 2, 0, 0);
                        dragControl = false;
                        vrDemo.Opacity = 0.5;
                    }
                    break;
            }
        }

        

        


    }
}

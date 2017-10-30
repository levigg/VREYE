using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Windows.Forms;

namespace WindowsHookSample
{
    /// <summary>
    /// 提供Unmanaged方法接收全域滑鼠事件。
    /// </summary>
    public static class MouseHook
    {
        /// <summary>
        /// 取得或設定是否獨佔所有滑鼠事件。
        /// </summary>
        public static bool Monopolize { get; set; }

        /// <summary>
        /// 當滑鼠按鍵壓下時引發此事件。
        /// </summary>
        public static event EventHandler<MouseEventArgs> GlobalMouseDown;
        /// <summary>
        /// 當滑鼠按鍵放開時引發此事件。
        /// </summary>
        public static event EventHandler<MouseEventArgs> GlobalMouseUp;
        /// <summary>
        /// 當滑鼠按鍵點擊時引發此事件。
        /// </summary>
        public static event EventHandler<MouseEventArgs> GlobalMouseClick;
        /// <summary>
        /// 當滑鼠按鍵連點兩次時引發此事件。
        /// </summary>
        public static event EventHandler<MouseEventArgs> GlobalMouseDoubleClick;
        /// <summary>
        /// 當滑鼠滾輪滾動時引發此事件。
        /// </summary>
        public static event EventHandler<MouseEventArgs> GlobalMouseWheel;
        /// <summary>
        /// 當滑鼠移動時引發此事件。
        /// </summary>
        public static event EventHandler<MouseEventArgs> GlobalMouseMove;

        /// <summary>
        /// 取得或設定是否開始接收全域滑鼠事件。
        /// </summary>
        public static bool Enabled
        {
            get { return m_Enabled; }
            set
            {
                if (m_Enabled != value)
                {
                    m_Enabled = value;
                    if (value)
                        Install();
                    else
                        Uninstall();
                }
            }
        }
        private static bool m_Enabled = false;

        private static int m_HookHandle = 0;
        private static NativeStructs.HookProc m_HookProc;
        /// <summary>
        /// 向Windows註冊Hook。
        /// </summary>
        private static void Install()
        {
            if (m_HookHandle == 0)
            {
                Process curProcess = Process.GetCurrentProcess();
                ProcessModule curModule = curProcess.MainModule;

                m_HookProc = new NativeStructs.HookProc(HookProc);
                m_HookHandle = NativeMethods.SetWindowsHookEx(NativeContansts.WH_MOUSE_LL, m_HookProc, NativeMethods.GetModuleHandle(curModule.ModuleName), 0);

                curModule.Dispose();
                curProcess.Dispose();

                m_DoubleClickTimer = new Timer
                {
                    Interval = NativeMethods.GetDoubleClickTime(),
                    Enabled = false
                };
                m_DoubleClickTimer.Tick += DoubleClickTimeElapsed;
                GlobalMouseDown += OnMouseDown;

                if (m_HookHandle == 0)
                    throw new Exception("Install Hook Faild.");
            }
        }
        private static void Uninstall()
        {
            if (m_HookHandle != 0)
            {
                bool ret = NativeMethods.UnhookWindowsHookEx(m_HookHandle);

                if (ret)
                    m_HookHandle = 0;
                else
                    throw new Exception("Uninstall Hook Faild.");
            }
        }

        //記憶游標上一次的位置，避免MouseMove事件一直引發。
        private static int m_OldX = 0;
        private static int m_OldY = 0;

        //記憶上次MouseDonw的引發位置，如果與MouseUp的位置不同則不引發Click事件。
        private static int m_LastBTDownX = 0;
        private static int m_LastBTDownY = 0;
        /// <summary>
        /// 註冊Windows Hook時用到的委派方法，當全域事件發生時會執行這個方法，並提供全域事件資料。
        /// </summary>
        private static int HookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            MouseEventArgs e = null;

            if (nCode >= 0)
            {
                int wParam_Int32 = wParam.ToInt32();
                NativeStructs.MOUSELLHookStruct mouseHookStruct = (NativeStructs.MOUSELLHookStruct)Marshal.PtrToStructure(lParam, typeof(NativeStructs.MOUSELLHookStruct));

                short mouseDelta = 0;

                if (GlobalMouseWheel != null && wParam_Int32 == NativeContansts.WM_MOUSEWHEEL)
                    mouseDelta = (short)((mouseHookStruct.MouseData >> 16) & 0xffff);

                e = new MouseEventArgs(wParam_Int32, mouseHookStruct.Point.X, mouseHookStruct.Point.Y, mouseDelta);

                if (GlobalMouseWheel != null && wParam_Int32 == NativeContansts.WM_MOUSEWHEEL)
                    GlobalMouseWheel.Invoke(null, e);
                else if (GlobalMouseUp != null && (wParam_Int32 == NativeContansts.WM_LBUTTONUP || wParam_Int32 == NativeContansts.WM_RBUTTONUP || wParam_Int32 == NativeContansts.WM_MBUTTONUP))
                {
                    GlobalMouseUp.Invoke(null, e);
                    if (GlobalMouseClick != null && (mouseHookStruct.Point.X == m_LastBTDownX && mouseHookStruct.Point.Y == m_LastBTDownY))
                        GlobalMouseClick.Invoke(null, e);
                }
                else if (GlobalMouseDown != null && (wParam_Int32 == NativeContansts.WM_LBUTTONDOWN || wParam_Int32 == NativeContansts.WM_RBUTTONDOWN || wParam_Int32 == NativeContansts.WM_MBUTTONDOWN))
                {
                    m_LastBTDownX = mouseHookStruct.Point.X;
                    m_LastBTDownY = mouseHookStruct.Point.Y;
                    GlobalMouseDown.Invoke(null, e);
                }
                else if (GlobalMouseMove != null && (m_OldX != mouseHookStruct.Point.X || m_OldY != mouseHookStruct.Point.Y))
                {
                    m_OldX = mouseHookStruct.Point.X;
                    m_OldY = mouseHookStruct.Point.Y;
                    if (GlobalMouseMove != null)
                        GlobalMouseMove.Invoke(null, e);
                }
            }

            if (Monopolize || (e != null && e.Handled))
                return -1;

            return NativeMethods.CallNextHookEx(m_HookHandle, nCode, wParam, lParam);
        }
        private static void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button.Equals(m_LastClickedButton))
            {
                if (GlobalMouseDoubleClick != null)
                    GlobalMouseDoubleClick.Invoke(null, e);
            }
            else
            {
                m_DoubleClickTimer.Enabled = true;
                m_LastClickedButton = e.Button;
            }
        }
        private static Buttons m_LastClickedButton;
        private static System.Windows.Forms.Timer m_DoubleClickTimer;
        private static void DoubleClickTimeElapsed(object sender, EventArgs e)
        {
            m_DoubleClickTimer.Enabled = false;
            m_LastClickedButton = Buttons.None;
        }

        /// <summary>
        /// 提供 GlobalMouseUp、GlobalMouseDown 和 GlobalMouseMove 事件的資料。
        /// </summary>
        public class MouseEventArgs : EventArgs
        {
            /// <summary>
            /// 取得按下哪個滑鼠鍵的資訊。
            /// </summary>
            public Buttons Button { get; private set; }
            /// <summary>
            /// 取得滑鼠滾輪滾動時帶有正負號的刻度數乘以 WHEEL_DELTA 常數。 一個刻度是一個滑鼠滾輪的刻痕。
            /// </summary>
            public int Delta { get; private set; }
            /// <summary>
            /// 取得滑鼠在產生滑鼠事件期間的 X 座標。
            /// </summary>
            public int X { get; private set; }
            /// <summary>
            /// 取得滑鼠在產生滑鼠事件期間的 Y 座標。
            /// </summary>
            public int Y { get; private set; }
            internal MouseEventArgs(int wParam, int x, int y, int delta)
            {
                Button = Buttons.None;
                switch (wParam)
                {
                    case (int)NativeContansts.WM_LBUTTONDOWN:
                    case (int)NativeContansts.WM_LBUTTONUP:
                        Button = Buttons.Left;
                        break;
                    case (int)NativeContansts.WM_RBUTTONDOWN:
                    case (int)NativeContansts.WM_RBUTTONUP:
                        Button = Buttons.Right;
                        break;
                    case (int)NativeContansts.WM_MBUTTONDOWN:
                    case (int)NativeContansts.WM_MBUTTONUP:
                        Button = Buttons.Middle;
                        break;
                }
                this.X = x;
                this.Y = y;
                this.Delta = delta;
            }
            private bool m_Handled;
            /// <summary>
            /// 取得或設定值，指出是否處理事件。
            /// </summary>
            public bool Handled
            {
                get { return m_Handled; }
                set { m_Handled = value; }
            }
        }
    }

    /// <summary>
    /// 指定定義按哪個滑鼠按鈕的常數。
    /// </summary>
    public enum Buttons
    {
        /// <summary>
        /// 不按任何滑鼠鍵。
        /// </summary>
        None,
        /// <summary>
        /// 按滑鼠左鍵。
        /// </summary>
        Left,
        /// <summary>
        /// 按滑鼠右鍵。
        /// </summary>
        Right,
        /// <summary>
        /// 按滑鼠中間鍵。
        /// </summary>
        Middle,
    }
}

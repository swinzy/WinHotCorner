using Gma.System.MouseKeyHook;
using System;
using WindowsInput.Native;

namespace WinHotCornerService
{
    internal class HotCornerService
    {
        public System.Drawing.Point TriggerPoint { get; set; } = new System.Drawing.Point(-5, -5);
        private IKeyboardMouseEvents m_GlobalHook;
        public HotCornerService()
        {
            m_GlobalHook = Hook.GlobalEvents();     
        }
        public void Start()
        {
            m_GlobalHook.MouseMove += HotCornerService_MouseMove;
            Console.WriteLine("Started");
        }

        public void Stop()
        {
            m_GlobalHook.MouseMove -= HotCornerService_MouseMove;

            //It is recommened to dispose it
            m_GlobalHook.Dispose();
        }

        private bool LessThan(System.Drawing.Point p1, System.Drawing.Point p2)
        {
            return (p1.X < p2.X && p1.Y < p2.Y);
        }

        private void HotCornerService_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (LessThan(e.Location, TriggerPoint) && !triggered)
            {
                TriggerWinTab();
                triggered = true;
            }
            else if (LessThan(new System.Drawing.Point(0, 0), e.Location))
            {
                triggered = false;
            }
        }

        private bool triggered = false;
        private void TriggerWinTab()
        {
            if (!FullscreenCheck.ShouldTrigger())
            {
                Console.WriteLine("Not triggered: App running in fullscreen");
                return;
            }
            // TODO: Dirty method, waiting for a better solution (probably an Windows API call?)
            WindowsInput.InputSimulator simulator = new WindowsInput.InputSimulator();
            simulator.Keyboard.ModifiedKeyStroke(VirtualKeyCode.LWIN, VirtualKeyCode.TAB);
        }
    }
}

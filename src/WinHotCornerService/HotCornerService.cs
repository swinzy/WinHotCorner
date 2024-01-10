using Gma.System.MouseKeyHook;
using System;
using System.Threading.Tasks;
using WindowsInput.Native;

namespace WinHotCorner
{
    internal class HotCornerService
    {
        private const int CFG_RETRY = 3;
        public bool ShouldReloadConfig { get; set; } = false;
        private Configuration Configuration { get; set; } = new Configuration();
        private bool _mouseDown = false;

        private IKeyboardMouseEvents m_GlobalHook;
        public HotCornerService()
        {
            m_GlobalHook = Hook.GlobalEvents();
        }
        public void Start()
        {
            m_GlobalHook.MouseMove += HotCornerService_MouseMove;
            m_GlobalHook.MouseDown += (object sender, System.Windows.Forms.MouseEventArgs e) => _mouseDown = true;
            m_GlobalHook.MouseUp += (object sender, System.Windows.Forms.MouseEventArgs e) => _mouseDown = false;
            Console.WriteLine("Started");
            Console.WriteLine($"Force is {Configuration.Force}.");
        }

        public void Stop()
        {
            m_GlobalHook.MouseMove -= HotCornerService_MouseMove;

            //It is recommened to dispose it
            m_GlobalHook.Dispose();
        }

        public async void ReloadConfigAsync()
        {
            Console.WriteLine("Reloading configuration...");
            ShouldReloadConfig = false;
            var newCfg = ConfigManager.Load();
            for (int i = 0; i < CFG_RETRY; i++)
            {
                if (newCfg is null)
                {
                    Console.WriteLine($"Failed to load configuration, retrying in 1 second ({i+1}/{CFG_RETRY}).");
                    await Task.Delay(1000).ContinueWith(_ => newCfg = ConfigManager.Load());
                }
                else
                {
                    break;
                }
            }
            if (newCfg is null)
            {
                Console.WriteLine("Cannot load configuration, aborted.");
                ShouldReloadConfig = true;
                return;
            }
            Console.WriteLine("Configuration loaded.");
            Configuration = newCfg;
            Console.WriteLine(Configuration);
        }

        private bool HitHotCorner(System.Drawing.Point cursorPoint, int force) => cursorPoint.X < -force && cursorPoint.Y < -force;

        private bool OutsideHotCorner(System.Drawing.Point cursorPoint) => cursorPoint.X > 0 && cursorPoint.Y > 0;
        
        private void HotCornerService_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (HitHotCorner(e.Location, Configuration.Force) && !triggered)
            {
                TriggerWinTab();
                triggered = true;
            }
            else if (triggered && OutsideHotCorner(e.Location))
            {
                triggered = false;
                if (ShouldReloadConfig)
                    ReloadConfigAsync();
            }
        }

        private bool triggered = false;
        private void TriggerWinTab()
        {
            // Check fullscreen
            if (Configuration.DisableWhenFullscreen && !FullscreenCheck.ShouldTrigger())
            {
                Console.WriteLine("Not triggered: App running in fullscreen");
                return;
            }

            if (Configuration.DisableWhenMouseDown && _mouseDown)
            {
                Console.WriteLine("Not triggered: Mouse down");
                return;
            }

            // TODO: Dirty method, waiting for a better solution (probably an Windows API call?)
            WindowsInput.InputSimulator simulator = new WindowsInput.InputSimulator();
            simulator.Keyboard.ModifiedKeyStroke(VirtualKeyCode.LWIN, VirtualKeyCode.TAB);
        }
    }
}

using Gma.System.MouseKeyHook;
using System;
using System.Threading.Tasks;
using WindowsInput.Native;

namespace WinHotCorner
{
    internal class HotCornerService
    {
        /// <summary>
        /// Defines how many times it should retry upon failure to load configuration
        /// </summary>
        private const int CFG_RETRY = 3;
        public bool ShouldReloadConfig { get; set; } = false;
        private Configuration Configuration { get; set; } = new Configuration();
        private bool _triggered = false;
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
        }

        public void Stop()
        {
            m_GlobalHook.MouseMove -= HotCornerService_MouseMove;

            //It is recommened to dispose it
            m_GlobalHook.Dispose();
        }

        /// <summary>
        /// Reloads configuration from file (contains retry mechanism)
        /// </summary>
        public async void ReloadConfigAsync()
        {
            Console.WriteLine("Reloading configuration...");

            // Immediately set flag to false to prevent multiple reloading process at the same time
            ShouldReloadConfig = false;

            // Start trying to load config and retry upon failure
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

            // Failure after a series of retrials
            if (newCfg is null)
            {
                Console.WriteLine("Cannot load configuration, aborted.");
                // Reset flag for retrying next time
                ShouldReloadConfig = true;
                return;
            }

            // Success
            Console.WriteLine("Configuration loaded.");
            Configuration = newCfg;
            Console.WriteLine(Configuration);
        }

        /// <summary>
        /// Detects if the current cursor point hits the hot corner with suitable force
        /// </summary>
        /// <param name="cursorPoint">cursor location</param>
        /// <param name="force">minimum force required</param>
        /// <returns></returns>
        private bool HitHotCorner(System.Drawing.Point cursorPoint, int force) => cursorPoint.X < -force && cursorPoint.Y < -force;

        /// <summary>
        /// Detects if the current cursor point is outside the hot corner detection area
        /// </summary>
        /// <param name="cursorPoint"></param>
        /// <returns></returns>
        private bool OutsideHotCorner(System.Drawing.Point cursorPoint) => cursorPoint.X > 0 && cursorPoint.Y > 0;
        
        private void HotCornerService_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            // Only detect hot corner if not triggered recently, to prevent multiple trigger of hot corner
            if (!_triggered && HitHotCorner(e.Location, Configuration.Force))
            {
                TriggerWinTab();
                _triggered = true;
            }
            // If mouse cursor exits the detection area, mark as not recently triggered (ready to be triggered again)
            else if (_triggered && OutsideHotCorner(e.Location))
            {
                _triggered = false;

                // Reload configuration if it has been updated
                if (ShouldReloadConfig)
                    ReloadConfigAsync();
            }
        }

        private void TriggerWinTab()
        {
            // Check fullscreen
            if (Configuration.DisableWhenFullscreen && !FullscreenCheck.ShouldTrigger())
            {
                Console.WriteLine("Not triggered: App running in fullscreen");
                return;
            }

            // Check guesture
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

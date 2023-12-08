using Gma.System.MouseKeyHook;
using System;
using System.Collections.Generic;
using System.Drawing;
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
using WindowsInput.Native;

namespace WinHotCorner
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public System.Drawing.Point TriggerPoint { get; set; } = new (-5, -5);
        public MainWindow()
        {
            InitializeComponent();
            RunHook();
        }

        private bool LessThan(System.Drawing.Point p1, System.Drawing.Point p2)
        {
            return (p1.X < p2.X && p1.Y < p2.Y);
        }

        private void RunHook()
        {
            Hook.GlobalEvents().MouseMove += async (sender, e) =>
            {
                //mainBlock.Text = ($"Mouse location: {e.Location}.");
                if (LessThan(e.Location, TriggerPoint) && !triggered)
                {
                    TriggerWinTab();
                    triggered = true;
                }
                else if (LessThan(new System.Drawing.Point(0,0), e.Location))
                {
                    triggered = false;
                }
            };
        }

        private bool triggered = false;
        private void TriggerWinTab()
        {
            mainBlock.Text += "Triggered\n";
            // TODO: This is currently buggy. Waiting for a better solution (probably an Windows API call?)
            WindowsInput.InputSimulator simulator = new WindowsInput.InputSimulator();
            simulator.Keyboard.ModifiedKeyStroke(VirtualKeyCode.LWIN, VirtualKeyCode.TAB);
        }
    }
}

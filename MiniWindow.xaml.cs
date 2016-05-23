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
using System.Windows.Shapes;

namespace S2SMtDemoClient
{
    /// <summary>
    /// Interaction logic for MiniWindow.xaml
    /// </summary>
    public partial class MiniWindow : Window
    {
        private int _NoOfLines;

        public MiniWindow()
        {
            InitializeComponent();
            this.SizeChanged += MiniWindow_SizeChanged;
            this.Closing += MiniWindow_Closing;

            // Restore the window size and position
            this.Height = (Properties.Settings.Default.MiniWindow_Height > 5) ? Properties.Settings.Default.MiniWindow_Height : 100;
            this.Width = (Properties.Settings.Default.MiniWindow_Width > 5) ? Properties.Settings.Default.MiniWindow_Width : 400;
            this.Left = Properties.Settings.Default.MiniWindow_Left;
            this.Top = Properties.Settings.Default.MiniWindow_Top;
        }

        private void MiniWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Save the window size and position
            Properties.Settings.Default.MiniWindow_Height = (int) this.Height;
            Properties.Settings.Default.MiniWindow_Width = (int) this.Width;
            Properties.Settings.Default.MiniWindow_Left = (int) this.Left;
            Properties.Settings.Default.MiniWindow_Top = (int) this.Top;
        }

        private void MiniWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            SetFontSize(_NoOfLines);
        }

        /// <summary>
        /// Sets the font size so that NoOfLines+1 lines fit into the Mini Window
        /// </summary>
        /// <param name="NoOfLines">Number of lines (index 0)</param>
        public void SetFontSize(int NoOfLines)
        {
            _NoOfLines = NoOfLines;
            DisplayText.FontSize = (DisplayText.ActualHeight > 10) ? (DisplayText.ActualHeight / (NoOfLines + 1) * 0.66) : 8;
        }
    }
}

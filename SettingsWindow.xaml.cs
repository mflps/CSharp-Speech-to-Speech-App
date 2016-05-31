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
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            ClientID.Text = Properties.Settings.Default.ClientID;
            ClientSecret.Text = Properties.Settings.Default.ClientSecret;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.ClientID = ClientID.Text;
            Properties.Settings.Default.ClientSecret = ClientSecret.Text;
            Properties.Settings.Default.Save();
            this.Close();
        }
    }
}

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

using System.IO.Ports;


namespace WPF_test
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Modbus_Master master = new Modbus_Master();

        public MainWindow()
        {
            InitializeComponent();
           

            master.Serial_port_init("COM3",StopBits.One, Parity.None);

            
            /*for(; ; )
            {
                master.Write_serial_data(data, data.Length);
                System.Threading.Thread.Sleep(5);
            }*/
        }

        private void btn_test_Click(object sender, RoutedEventArgs e)
        {
            byte[] data = new byte[] { 49 };
            master.Write_serial_data(data, data.Length);
        }
    }
}

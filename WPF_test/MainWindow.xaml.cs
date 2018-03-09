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
using System.Diagnostics;
using System.Windows.Threading;

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
           
            Trace.WriteLine("Program Started");
         
           string[] ports = SerialPort.GetPortNames();
            
            Logger logger = new Logger();

            master.RxDataReceived += logger.OnRxDataReceived;

            master.Serial_port_init("COM1",StopBits.One, Parity.None);

            /*for(; ; )
            {
                master.Write_serial_data(data, data.Length);
                System.Threading.Thread.Sleep(5);
            }*/
        }

      
  

        public class Logger
        {

     
            public static void log_update(TextBox tb, string txt)
            {
              tb.Text +=txt;
            }
   
            public void OnRxDataReceived(object sender, Modbus_Master.RxDataReceivedEventArgs args)
            {
                
                //log_update(tbLog , args.Rx_Data); // Invoke?
                Trace.WriteLine("Logger subscibed event."+ args.Rx_Data);
            }
        }

        private void btn_test_Click(object sender, RoutedEventArgs e)
        {
            byte[] data = new byte[] { 49 };
            master.Write_serial_data(data, data.Length);
        }

    }
}

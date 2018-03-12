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
using System.Globalization;
using System.Windows.Threading;

using System.IO.Ports;
using System.Runtime.Remoting.Messaging;


namespace WPF_test
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public Modbus modbus = new Modbus();

        public MainWindow()
        {
            InitializeComponent();
           
            Trace.WriteLine("Program Started");
         
           var ports = SerialPort.GetPortNames();
            
         
            modbus.RxDataReceived += OnRxDataReceived;

            modbus.Serial_port_init("COM1",StopBits.One, Parity.None);

            TreeViewItem Main = new TreeViewItem()
            {
                Header = "Status"
            };

        
            Main.Items.Add(MakeTreeViewItem("RX", modbus.Counters.BytesReceived));
            Main.Items.Add(MakeTreeViewItem("TX", modbus.Counters.BytesTransmitted));

            TvStatus.Items.Add((Main));


            /*for(; ; )
            {
                master.Write_serial_data(data, data.Length);
                System.Threading.Thread.Sleep(5);
            }*/
        }

        private TreeViewItem MakeTreeViewItem(string header, int value)
        {
            var tw = new TreeViewItem();
            var head = "";

            head = header +": " + value.ToString();
            tw.Header = head;

            return tw;
        }
        
        /*public static void log_update(TextBox tb, string txt)
        {
            tb.Text +=txt;
        }*/

        public void OnRxDataReceived(object sender, Modbus.RxDataReceivedEventArgs args)
        {
            //log_update(tbLog , args.Rx_Data); // Invoke?

            Trace.WriteLine("Event received."+ args.RxData);

            TbLog.Dispatcher.Invoke(() =>
            {
                var timeStamp = DateTime.Now.ToString(CultureInfo.InvariantCulture);
                TbLog.Text += timeStamp +" "+ args.RxData + Environment.NewLine;
                TbLog.ScrollToEnd();
            });
        }
        
        private void btn_test_Click(object sender, RoutedEventArgs e)
        {
            //byte[] data = new byte[] { 49 };
            byte[] pingFrame = Modbus.ModbusPing(9, 0x0102ABCD);

            Modbus.Request req = new Modbus.Request(pingFrame);

            modbus.Write_serial_data(req.RequestData, pingFrame.Length);
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            TbLog.Text = "Log cleared" + Environment.NewLine;
        }

    }//class

    
}//namespace

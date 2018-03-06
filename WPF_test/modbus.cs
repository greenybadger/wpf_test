using System;
using System.IO.Ports;

internal class Modbus_Master
{
    public SerialPort _serialPort = new SerialPort();


    //1- Defie delegate
    public delegate void FirstByteReceivedEventHandler(object sender, EventArgs args);
    //2- Define event
    public event FirstByteReceivedEventHandler FirstByteReceived;
    //3 - Event  rise. .NET recommendation methotd should be protected virtual void, starts with On....
    protected virtual void OnFirstByteReceives()
    {
        if(FirstByteReceived != null)
        {

        }
    }

    public void Serial_port_init(string name, StopBits bits, Parity parity)
    {
        _serialPort.PortName = name;
        _serialPort.StopBits = bits;
        _serialPort.Parity = parity;
        _serialPort.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
        _serialPort.ErrorReceived += new SerialErrorReceivedEventHandler(ErrorReceivedHandler);

    }

    public class Counters_Errors
    {
        public int errors;
        private int crc;
        private int parity;
        private int time_out;
        private int Frame;
        private int Overrun;
        private int RXOver;
        private int RXParity;
        private int TXFull;
    };
     
    //public Counters_Errors counters_errors = new Counters_Errors();


    private static void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
    {
        
    }

    private static void ErrorReceivedHandler(object sender, SerialErrorReceivedEventArgs e)
    {
       

    }

    public void Write_serial_data(byte[] data,int size )
    {
        if(_serialPort.IsOpen)
        {

        }
        else
        {
            _serialPort.Open();
        }
        _serialPort.Write(data, 0, size);
    }


}



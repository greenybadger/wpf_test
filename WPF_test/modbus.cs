using System;
using System.IO.Ports;
using System.Collections.Generic;

public class Modbus_Master
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


    /*
     *
     *
     *
    */

    
    public enum Function_Code
    {
        Read_Registers =0x03,   
        Write_Single_Register = 0x06,
        Write_Multi_Registers = 0x0F,
        Diagnostic = 0x08
    }

    bool Is_time_out(Request req)
    {
        bool timeout = false;
        int tick_now = get_system_tick();

        /*if( (tick_now - req.sys_tick) > req.sys_time_out)
        {
            timeout = true;
        }*/

        return timeout;
    }

    static int get_system_tick()
    {
        return Environment.TickCount & Int32.MaxValue;  
    }

    Queue<Request> qRequests = new Queue<Request>();

    class Request
    {
        byte slave_id;
        Function_Code fc;
        byte[] request_data; //Set by constructor
        byte[] respond_data; //Set by constructor
 
       
        int sys_tick; //Set by constructor
        int time_out; //Set by constructor
   
        public Request(byte[] data,  int _timeout) // Contructor
        {
            //TODO: timeout can be calcualted by data size and baund rate.
            this.request_data = data;
            this.time_out = _timeout; 
            this.sys_tick = get_system_tick();
        }
    }

    static byte[] Modbus_Read_Registers(int slave_id, int Register_Start, int size)
    {
        //  ID + 0x03 + SREG_H + SREG_L + SIZE_H + SIZE_L + CRC_H + CRC_L = 8 Bytes
        byte[] request_data = new byte[8];
        
        request_data[0] = (byte) slave_id;
        request_data[1] = (byte) Function_Code.Read_Registers;
        request_data[2] = get_byte_High(Register_Start);
        request_data[3] = get_byte_Low(Register_Start);
        request_data[4] = get_byte_High(size);
        request_data[5] = get_byte_Low(size);

        byte[] crc = calc_crc(request_data);

        request_data[6] = crc[0]; 
        request_data[7] = crc[1]; 

        return request_data;
    }

    static byte[] Modbus_Ping(int slave_id, int data)
    {
        //  ID + 0x08 + SUB_H + SUB_L + DATA_H + DATA_L + CRC_H + CRC_L = 8 Bytes
        byte[] request_data = new byte[8];
        
        request_data[0] = (byte) slave_id;
        request_data[1] = (byte) Function_Code.Diagnostic;
        request_data[2] = 0;
        request_data[3] = 0;
        request_data[4] = get_byte_High(data);
        request_data[5] = get_byte_Low(data);

        byte[] crc = calc_crc(request_data);

        request_data[6] = crc[0]; 
        request_data[7] = crc[1]; 

        return request_data;
    }

    static byte[] calc_crc(byte[] data)
    {
        byte[] crc = new byte[2];

        byte crc_L = 0;
        byte crc_H = 0;

        //TODO add crc calculation, do not calculate last two bytes

        crc[0] = crc_H;
        crc[1] = crc_L;

        return crc;
    }

    static byte get_byte_Low(int data)
    {
        return (byte) (data);
    }

    static byte get_byte_High(int data)
    {
        return (byte) (data>>8);
    }

    public class RxDataReceivedEventArgs : EventArgs
    {
        public string Rx_Data { get; set; }
    }
    
    public event EventHandler<RxDataReceivedEventArgs> RxDataReceived;

    protected virtual void OnRxDataReceived(RxDataReceivedEventArgs args) //Rise event
    {
        if(RxDataReceived != null) //check if thera ara subscribers
        {
            RxDataReceived(this, args); //Rise event
        }

    }

    static Queue<string> qRX_data = new Queue<string>();

    private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
    {
        SerialPort sp = new SerialPort();
        sp = (SerialPort) sender;
        string data = sp.ReadExisting();
        qRX_data.Enqueue(data);

        OnRxDataReceived(new RxDataReceivedEventArgs(){Rx_Data = data});
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

    byte[] Read_serial_data(int size)
    {
        byte[] data = new byte[size];

        if(_serialPort.IsOpen)
        {

        }
        else
        {
            _serialPort.Open();
        }
        //_serialPort.ReadTimeout = 100; // https://msdn.microsoft.com/en-us/library/system.io.ports.serialport.readtimeout(v=vs.110).aspx
        _serialPort.Read(data, 0, size); // TODO timeout
        return data;
    }
}



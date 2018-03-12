using System;
using System.IO.Ports;
using System.Collections.Generic;
using System.Linq;

public class Modbus
{
    public SerialPort SPort;
    public Counts Counters;
    
    public Modbus()
    {
        Counters = new Counts();
        SPort = new SerialPort();
    }

    public void Serial_port_init(string name, StopBits bits, Parity parity)
    {
        SPort.PortName = name;
        SPort.StopBits = bits;
        SPort.Parity = parity;
        SPort.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
        SPort.ErrorReceived += new SerialErrorReceivedEventHandler(ErrorReceivedHandler);
    }

    public class Counts
    {
        public int BytesReceived;
        public int BytesTransmitted;
        public int FramesOk;
        
        public class Errors

        {
            public int Cnt;
            public int Crc;
            public int Parity;
            public int TimeOut;
            public int Frame;
            public int Overrun;
            public int RxOver;
            public int RxParity;
            public int TxFull;
        };
    }

    public enum FunctionCode
    {
        ReadRegisters = 0x03,
        WriteSingleRegister = 0x06,
        WriteMultiRegisters = 0x0F,
        Diagnostic = 0x08
    }

    static int get_system_tick()
    {
        return Environment.TickCount & Int32.MaxValue;  
    }

    public static int TimeoutCalculate(int baunds, int size, int extraDelay) // Returns miliseconds
    {
        float timeout = (float) extraDelay + 
                        ( (float)(11 * size)/ (float) baunds) * 1000; // 8 data bits, 2 stop bit, 1 parity = 11bits
        return (int) timeout;
    }

    static bool IsTimeOut(int tick, int timeout)
    {
        bool isTimeOut = false;
        int tickNow = get_system_tick();

        if ((tickNow - tick) > timeout)
        {
            isTimeOut = true;
        }

        return isTimeOut;
    }

    private static Queue<Request> QRequests = new Queue<Request>();

    public class Request:Modbus
    {

        public readonly int SlaveId;
        public readonly FunctionCode Fc;
        public int _sysTick;
        public int _timeOut;

        public byte[] RequestData;
        public Respond respond;


        public Request(byte[] data) // Contructor
        {
            SlaveId = (int) data[0];
            Fc = (FunctionCode) data[1];
            RequestData = data;

            int len = GetNumberOfBytesToRead(Fc);
            respond = new Respond(len);

            _sysTick = get_system_tick();
            _timeOut = TimeoutCalculate(SPort.BaudRate, data.Length, 100);
            QRequests.Enqueue(this); //Add to queue
        }
    }

    public static int GetNumberOfBytesToRead(FunctionCode fc)
    {
        int size = 0;
        if (fc == FunctionCode.Diagnostic)
        {
            size = 8; // 8 = ID+FC+ 4 data bytes + crc_H, crc_L
        }
        return size;
    }

    public class Respond
    {
        public byte[] RespondData;
        public int Delay;
        public Flags Status;

        public Respond(int len)
        {
            RespondData = new byte[len];
            Status = new Flags
            {
                isHeaderDetected = false,
                IsFrameReady = false,
                IsTimeOut = false
            };
        }

        public class Flags
        {
            public bool isHeaderDetected; // 2 bytes = ID + FC
            public bool IsFrameReady;
            public bool IsTimeOut;
        }
    }


    private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
    {
        var sp = (SerialPort)sender;
        // string data = sp.ReadExisting();

        byte[] data_byte = new byte[sp.BytesToRead];
        sp.Read(data_byte, 0, data_byte.Length);    //Read all available bytes

        Counters.BytesReceived += data_byte.Length; //Update Rx Counter

        string str = ByteArrayToString(data_byte, OutputFormat.Ascii);

        if (QRequests.Count > 0) //Check if there is any requests in queue
        {
            Request req = QRequests.Peek(); // Get firts item in queue , but don't remove from queue 
            if (req.Fc == FunctionCode.Diagnostic)
            {
                bool isEq = true;
                for(byte i = 0; i < data_byte.Length; i++)
                {
                    if (req.RequestData[i] != data_byte[i])
                    {
                        isEq = false;
                        break;
                    }
                }

                if (isEq == true)
                {
                    int delay = get_system_tick() - req._sysTick;
                    str += " Packed Dequeue, Delay: " + delay.ToString();
                    QRequests.Dequeue(); //Remove first item from queue
                }
            }
        }
        else
        {
            str += "Not requosted data!";
        }



        OnRxDataReceived(new RxDataReceivedEventArgs { RxData = str });
    }


    public enum ParseState
    {
        DetectSlaveId = 0,
        DetectFc
    }

    public bool ParseModbusData(ref SerialPort sp, ref Request req, ParseState state)
    {
        /*TODO
         * 1. If there are no requosted data, drop all revceived data. 
         * 2. if there are requosted data, read data while SlaveID matched.
         * 3. if SlaveID matched read next byte, and check if Function code match,
         * 4. if FC dont match, start from 3. If SlaveID not found, drop all received data.
         * 5. Check if last byte is SlaveID, if yes next event start from 4.
         * 6. If SlaveID and FC detected, continue parsing data, by FC.
         *
         *
         */

        bool isFrameReady = false;



        return isFrameReady;
    }

    public static byte[] ModbusPing(int slaveId, int data)
    {
        //  ID + 0x08 + SUB_H + SUB_L + DATA_H + DATA_L + CRC_H + CRC_L = 8 Bytes
        byte[] requestData = new byte[8];

        requestData[0] = (byte)slaveId;
        requestData[1] = (byte)FunctionCode.Diagnostic;
        requestData[2] = (byte)(data >> 24);
        requestData[3] = (byte)(data >> 16);
        requestData[4] = (byte)(data >> 8);
        requestData[5] = (byte)(data);

        byte[] crc = Calc_CRC(requestData);

        requestData[6] = crc[0];
        requestData[7] = crc[1];

        return requestData;
    }

    public class RxDataReceivedEventArgs : EventArgs
    {
        public string RxData { get; set; }
    }
    
    public event EventHandler<RxDataReceivedEventArgs> RxDataReceived;

    protected virtual void OnRxDataReceived(RxDataReceivedEventArgs args) //Rise event
    {
        /*if(RxDataReceived != null) //check if thera ara subscribers
        {
            RxDataReceived(this, args); //Rise event
        }*/
        RxDataReceived?.Invoke(this, args); //Rise event
    }


    public enum OutputFormat
    {
        X2 = 0,
        X2Nospace,
        Ascii
    }

    public void Write_serial_data(byte[] data, int size)
    {

        if (SPort.IsOpen)
        {

        }
        else
        {
            SPort.Open();
        }

        byte[] ping_frame = Modbus.ModbusPing(9, 0xABCD);

        SPort.Write(data, 0, size);

        Counters.BytesTransmitted += size;
    }

    private byte[] ReadSerialData(int size)
    {
        var data = new byte[size];

        if(SPort.IsOpen)
        {

        }
        else
        {
            SPort.Open();
        }
        
        SPort.Read(data, 0, size);
        return data;
    }

    private static void ErrorReceivedHandler(object sender, SerialErrorReceivedEventArgs e)
    {

    }

    public static byte[] ModbusReadRegisters(int slaveId, int registerStart, int size)
    {
        //  ID + 0x03 + SREG_H + SREG_L + SIZE_H + SIZE_L + CRC_H + CRC_L = 8 Bytes
        byte[] requestData = new byte[8];

        requestData[0] = (byte)slaveId;
        requestData[1] = (byte)FunctionCode.ReadRegisters;
        requestData[2] = get_byte_High(registerStart);
        requestData[3] = Get_byte_Low(registerStart);
        requestData[4] = get_byte_High(size);
        requestData[5] = Get_byte_Low(size);

        byte[] crc = Calc_CRC(requestData);

        requestData[6] = crc[0];
        requestData[7] = crc[1];

        return requestData;
    }

    public static string ByteArrayToString(byte[] data, OutputFormat format)
    {
        string str = "";

        switch (format)
        {
            case OutputFormat.X2:

                foreach (byte item in data)
                {
                    str += string.Format("{0:X2}", item) + " ";
                }
                break;

            case OutputFormat.X2Nospace:

                foreach (byte item in data)
                {
                    str += string.Format("{0:X2}", item);
                }
                break;

            case OutputFormat.Ascii:
                str = System.Text.Encoding.ASCII.GetString(data);

                break;
        }

        return str;
    }

    public static byte[] Calc_CRC(byte[] data) //Returns 2 byte array, CRC_H, CRC_L
    {
        byte[] crc = new byte[2];

        byte crc_L = 0x11;
        byte crc_H = 0x22;

        //TODO add crc calculation, dont calculate last two bytes. 

        crc[0] = crc_H;
        crc[1] = crc_L;

        return crc;
    }

    static byte Get_byte_Low(int data)
    {
        return (byte)(data);
    }

    static byte get_byte_High(int data)
    {
        return (byte)(data >> 8);
    }
}



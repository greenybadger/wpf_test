using System;
using System.IO.Ports;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Permissions;
using System.Timers;

public class Modbus
{
    public SerialPort SPort;


    public Modbus()
    {
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

    public static class Counters
    {
        public static int BytesReceived;
        public static int BytesTransmitted;
        public static int FramesOk;
    
        
        public static class Errors
        {
            public static int Cnt;
            public static int Crc;
            public static int Parity;
            public static int TimeOut;
            public static int Frame;
            public static int FrameSize;
            public static int Overrun;
            public static int RxOver;
            public static int RxParity;
            public static int TxFull;
            public static int BytesMissing;
            public static int IdMissing;
            public static int FcMissing;
            public static int CountInvalid;
        }
    }


    public bool IsFunctionCodeValid(byte fc)
    {
        bool isValid = true;

        FunctionCode code = (FunctionCode) fc;

        switch (code)
        {
            case FunctionCode.ReadRegisters:
                break;
            case FunctionCode.WriteSingleRegister:
                break;
            case FunctionCode.WriteMultiRegisters:
                break;
            case FunctionCode.Diagnostic:
                break;
            case FunctionCode.ErrorReadRegisters:
                break;
            case FunctionCode.ErrorWriteSingleRegister:
                break;
            case FunctionCode.ErrorWriteMultiRegisters:
                break;
            default:
                isValid = false;
                break;
        }

        return isValid;
    }

    public enum FunctionCode //Also Error codes
    {
        ReadRegisters = 0x03, //Request: ID + 0x03 + Respond:
        WriteSingleRegister = 0x06,
        WriteMultiRegisters = 0x0F,
        Diagnostic = 0x08,

        //Error codes, Example: ID + ErrorCode + ExceptionCode + CRC_H + CRC_L = 5Bytes
        ErrorReadRegisters = 0x83,
        ErrorWriteSingleRegister = 0x86,
        ErrorWriteMultiRegisters = 0x8F
    }


    public enum ExceptionCodes
    {
        Function = 1,
        DataAddress = 2,
        DataValue = 3
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

    private static Queue<byte> QBufferIn = new Queue<byte>();
    private static Queue<Request> QRequests = new Queue<Request>();


    public class Request:Modbus
    {
        public readonly int SlaveId;
        public readonly FunctionCode Fc;

        public int _sysTick;
        public int _timeOut;
        public System.Timers.Timer TimOutTimer;
        public int NumberOfBytesToRead;
        public int PayloadBytesToRead;
        public byte RespondFc;
        public byte RespondSlaveID;

        public byte[] ReceivedData;
    
        public Request(byte[] data) // Contructor
        {
            SlaveId = (int) data[0];
            Fc = (FunctionCode) data[1];
       
            NumberOfBytesToRead = GetNumberOfBytesToRead(ref data);
            PayloadBytesToRead = NumberOfBytesToRead - 2; // Without Id and function code

            _sysTick = get_system_tick();
            _timeOut = TimeoutCalculate(SPort.BaudRate, data.Length, 100);

            //Timeout with timer, 
            TimOutTimer = new System.Timers.Timer();
            TimOutTimer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
            TimOutTimer.Interval = _timeOut;
            TimOutTimer.Enabled = false; // Enable here timeout

            QRequests.Enqueue(this); //Add to queue
        }

        private static void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            Console.WriteLine("Request TimeOut!"); 
        }
    }


    public static int GetNumberOfBytesToRead(ref byte[] req) //req = request frame
    {
        int size = 0;

        switch ((FunctionCode)req[1])
        {
            case FunctionCode.ReadRegisters:
                size = 5 + req[5]*2;
                break;
            case FunctionCode.WriteSingleRegister:
                break;
            case FunctionCode.WriteMultiRegisters:
                break;
            case FunctionCode.Diagnostic:
                size = 8;
                break;
            case FunctionCode.ErrorReadRegisters:
                break;
            case FunctionCode.ErrorWriteSingleRegister:
                break;
            case FunctionCode.ErrorWriteMultiRegisters:
                break;
        }

        return size;
    }



    bool IsHeaderAvailable(byte slaveID, byte[] dataIn, byte size)
    {
        bool isHeaderDetected = false;
        byte index = 0;

        for (index = 0; index < size; index++)
        {
            if (dataIn[index] == slaveID)
            {
                index++;
                if (IsFunctionCodeValid(dataIn[index]) == true)
                {
                    break;
                }
            }
        }
        return isHeaderDetected;
    }




    enum ParseState
    {
        SlaveId = 0,
        FunctionCode,
        Payload,
    }

    ParseState parseState= ParseState.SlaveId;

    private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
    {
        var sp = (SerialPort)sender;
        // string data = sp.ReadExisting();

        byte[] dataIn = new byte[sp.BytesToRead];
        string str = "";

        if (QRequests.Count > 0) //Check if there is any Requests
        {
            Request req = QRequests.Peek(); // Get firts item in queue , but don't remove from queue 
            
            if (parseState == ParseState.SlaveId)
            {
                //SlaveID detect
                while (sp.BytesToRead > 0)
                {
                    if ((byte)sp.ReadByte() == req.SlaveId)
                    {
                        req.RespondSlaveID = (byte) req.SlaveId;
                        parseState = ParseState.FunctionCode;
                        break;
                    }
                }
            }

            //Check Fc
            while ((sp.BytesToRead > 0) && (parseState == ParseState.FunctionCode) )
            {
                byte tempFc = (byte) sp.ReadByte();

                if (IsFunctionCodeValid(tempFc) == true)
                {
                    req.RespondFc = tempFc;

                    switch ( (FunctionCode) tempFc )
                    {
                        case FunctionCode.ReadRegisters:
                            req.PayloadBytesToRead = req.NumberOfBytesToRead - 2;
                            break;
                        case FunctionCode.WriteSingleRegister:
                            req.PayloadBytesToRead = 6;
                            break;
                        case FunctionCode.WriteMultiRegisters:
                            req.PayloadBytesToRead = 6;
                            break;
                        case FunctionCode.Diagnostic:
                            req.PayloadBytesToRead = 6;
                            break;
                        case FunctionCode.ErrorReadRegisters:
                            req.PayloadBytesToRead = 3;
                            break;
                        case FunctionCode.ErrorWriteSingleRegister:
                            req.PayloadBytesToRead = 3;
                            break;
                        case FunctionCode.ErrorWriteMultiRegisters:
                            req.PayloadBytesToRead = 3;
                            break;
                    }
                    parseState = ParseState.Payload;
                }
                else
                {
                    parseState = ParseState.SlaveId;
                }
            }
            


            //Check if all Payload data available
            if (parseState == ParseState.Payload)
            {
                if (req.PayloadBytesToRead <= sp.BytesToRead)
                {
                    byte[] payload = new byte[req.PayloadBytesToRead + 2];
                    payload[0] = req.RespondSlaveID;
                    payload[1] = req.RespondFc;

                    sp.Read(payload, 2, req.PayloadBytesToRead); //Read only bytes needed
                    Modbus.Counters.BytesReceived += payload.Length + 2; //Update Rx Counter
                    str = ByteArrayToString(payload, OutputFormat.X2);

                    //Parse frame
                    if (IsFrameValid((byte)0x09, ref payload) == true)
                    {
                        str += " Frame is Valid.";

                        int delay = get_system_tick() - req._sysTick;
                        str += " Packed Dequeue, Delay: " + delay.ToString();
                        QRequests.Dequeue(); //Remove first item from queue
                    }
                    else
                    {
                        str += "Invalid Frame";
                    }

                    OnRxDataReceived(new RxDataReceivedEventArgs { RxData = str }); //Rise event
                }
                else
                {
                    //Don't parse data if not all data reveived
                    str += "Not all Data received";
                    OnRxDataReceived(new RxDataReceivedEventArgs { RxData = str }); //Rise event
                }

            }


        }
        else
        {
            sp.Read(dataIn, 0, dataIn.Length);    //Read all available bytes
            Modbus.Counters.BytesReceived += dataIn.Length; //Update Rx Counter
            str = ByteArrayToString(dataIn, OutputFormat.X2);
            str += "Not requested data";
            OnRxDataReceived(new RxDataReceivedEventArgs { RxData = str }); //Rise event
        }
    }


    enum ParseHeaderState
    {
        IsIdValid = 0,
        IsFcValid
    }

    bool ErrorUpdate(ref int err)
    {
        err++;
        return false;
    }

    bool IsReadRegistersFrameValid(int slaveId, ref byte[] dataIn, int dataSize)
    {
        //   0   1     2       3        4                               
        //  ID 0x03, COUNT, DATA0_H, DATA0_L ... DATAN_H, DATAN_L, CRC_H, CRC_L
        //  if COUNT value = 2, TOTAL = 5 + 2 = 7 Bytes

        
        bool isValid = false;


        //0. Slave id check
        if (dataIn[0] != (byte) slaveId)
        {
            return isValid = ErrorUpdate(ref Counters.Errors.IdMissing);
        }

        //1. function code check
        if ((FunctionCode)dataIn[1] != FunctionCode.ReadRegisters)
        {
            return isValid = ErrorUpdate(ref Counters.Errors.FcMissing);
        }


        //2. Count check
        if (dataIn[2] < 2)
        {
            return isValid = ErrorUpdate(ref Counters.Errors.CountInvalid);
        }

        //2.1 Count check, Count should be even number.
        if ( (dataIn[2] % 2 ) != 0)
        {
            return isValid = ErrorUpdate(ref Counters.Errors.CountInvalid);
        }


        //3 Data size check
        const int minSize = 5; //  ID + 0x03 + COUNT + CRCH + CRCL = 5 Bytes.
        int lenght = minSize + dataIn[2]; // Bytes needed

        if (lenght > dataSize)
        {
            return isValid = ErrorUpdate(ref Counters.Errors.FrameSize);
        }

        //4 Check CRC
        byte crc_H = 0x12;
        byte crc_L = 0x34;

        if ((dataIn[(byte)lenght-2] != crc_H) || (dataIn[(byte)lenght - 1] != crc_L))
        {
            return isValid = ErrorUpdate(ref Counters.Errors.Crc);
        }

        return isValid = true;
    }

    bool IsWriteSingleRegFrameValid()
    {
        bool isValid = false;

        return isValid;
    }

    bool IsWriteMultiRegFrameValid()
    {
        bool isValid = false;
        return isValid;
    }

    bool IsDiagnosticFrameValid(ref byte[] dataIn , int dataInSize)
    {
        //     0    1   2       3       4    5       6     7     //Total = 8  
        //    ID 0x08, DUMMY, DUMMY, DUMMY, DUMMY, CRC_H, CRC_L
       
        bool isValid = false;
        const int lenght = 8;

        //0. Slave id checked before calling this function.

        //1. function code check
        if ((FunctionCode) dataIn[1] != FunctionCode.Diagnostic)
        {
            return isValid = false;
        }

        //2. Buffer size check
        if (dataIn.Length < lenght)
        {
            return isValid = false;
        }

        //3. DUMMY BYTES dont need check.

        //4. Check CRC
        byte crc_H = 0x12;
        byte crc_L = 0x34;

        if ( (dataIn[6] != crc_H)  || (dataIn[7] != crc_L))
        {
            Counters.Errors.Crc++;
            return isValid = false;
        }

        isValid = true;

        return isValid;
    }

    bool IsErrorCodeFrameValid()
    {
        bool isValid = false;

        return isValid = true;
    }


    public bool IsFrameValid(byte slaveId, ref byte[] dataIn)
    {
        bool isFrameValid = false;

        //1. Check SlaveID
        if (dataIn[0] != slaveId)
        {
            Counters.Errors.IdMissing++;
            return isFrameValid = false;
        }

        //2. Check Function/Error Code
        switch((FunctionCode)dataIn[1])
        {
            //Function codes
            case FunctionCode.ReadRegisters:
                isFrameValid = IsReadRegistersFrameValid(slaveId, ref dataIn, dataIn.Length);
                break;
            case FunctionCode.WriteSingleRegister:
                break;
            case FunctionCode.WriteMultiRegisters:
                break;
            case FunctionCode.Diagnostic:
                isFrameValid = IsDiagnosticFrameValid(ref dataIn, dataIn.Length);
                break;

            //Error codes
            case FunctionCode.ErrorReadRegisters:
                break;
            case FunctionCode.ErrorWriteSingleRegister:
                break;
            case FunctionCode.ErrorWriteMultiRegisters:
                break;
            default:
                //Function/Error code invalid.
                Counters.Errors.FcMissing++;
                break;
        }
     
        return isFrameValid;
    }

    public bool ParseModbusData(ref SerialPort sp, ref Request req)
    {
        /* TODO . IMPORTAT: EACH BYTE MUST BE PARSED.
         * #1. If there are no requosted data, drop all revceived data.
         * #2. if there are requosted data, read data while SlaveID matched.
         * #3. if SlaveID matched read next byte, if not loop while match or buffer is empty.
         * #4. if FC dont match, start from #3.
         * #5. Check if last byte is SlaveID, if yes next event start from #4.
         * #6. If SlaveID and FC detected, continue parsing data, by FC.
         * #7. After FC parsing, if there are any data left, start parsing from #2.
         *
         */

        bool isFrameReady = false;

        return isFrameReady;
    }

    public static byte[] RequestPing(int slaveId, int data)
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

    public static byte[] RequestReadRegisters(int slaveId, int startAddress, int numberOfRegisters)
    {
        //  Request: ID + 0x03 + AddressH + AddressL + NumberH + NumberL + CRC_H + CRC_L = 8 Bytes
        byte[] buf = new byte[8];

        buf[0] = (byte)slaveId;
        buf[1] = (byte)FunctionCode.ReadRegisters;

        buf[2] = (byte)(startAddress >> 8);
        buf[3] = (byte)(startAddress);

        buf[4] = (byte)(numberOfRegisters >> 8);
        buf[5] = (byte)(numberOfRegisters);

        byte[] crc = Calc_CRC(buf);

        buf[6] = crc[0];
        buf[7] = crc[1];

        return buf;
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

        byte[] ping_frame = Modbus.RequestPing(9, 0xABCD);

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

        byte crc_L = 0x34;
        byte crc_H = 0x12;

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



using System;
using System.IO.Ports;

class Modbus_Master
{
    public SerialPort _serialPort = new SerialPort();

    public void Serial_port_init(string name, StopBits bits, Parity parity)
    {
        _serialPort.PortName = name;
        _serialPort.StopBits = bits;
        _serialPort.Parity = parity;
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

    public void Delay(int del)
    {
        while(del > 0)
        {
            del--;
        }
    }
}



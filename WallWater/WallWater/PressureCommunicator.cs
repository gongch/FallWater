using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Threading;

namespace WallWater
{
    public class PressureCommunicator
    {
        public void Start()
        {
            if( null != _thread && _thread.IsAlive)
            {
                return;
            }

            if(null == _thread)
            {
                _thread = new Thread(LoopFunction);
            }
            _thread.Start();

        }
        public void Stop()
        {
            _stopEvent.Set();
            _thread.Join(1000);
        }

        public PressurreArrivedEvent PressurreArrived;
        public delegate void PressurreArrivedEvent(float x, float y, int pressure);
        public DeviceConnectionStatusChangedEvent DeviceConnectionStatusChanged;
        public delegate void DeviceConnectionStatusChangedEvent( EnumDeviceConnectionStatus status);
        private void OpenDevice()
        {
            var ports = GetPortNames();
            if (!ports.Any())
            {
                DeviceConnectionStatusChanged?.Invoke(EnumDeviceConnectionStatus.Disconnected);
                return;
            }
            _serialPort = new SerialPort(ports.First());
            _serialPort.BaudRate = 115200;
            _serialPort.Open();
            var command = MakeCommand(EnumCommand.Start);
            _serialPort.Write(command, 0, command.Length);
        }

        public string[] GetPortNames()
        {
            var ports = SerialPort.GetPortNames();

            List<string> strs = new List<string>();
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("select * from Win32_PnPEntity"))
                {
                    var hardInfos = searcher.Get();
                    foreach (var port in ports)
                    {
                        foreach (var hardInfo in hardInfos)
                        {
                            try
                            {
                                var tmp = hardInfo.Properties["Name"].Value.ToString();

                                if (tmp.Contains(port) && tmp.Contains("Silicon Labs CP210x USB to UART Bridge"))
                                {
                                    strs.Add(port);
                                }
                            }
                            catch (Exception)
                            {
                                continue;
                            }
                        }
                    }
                    searcher.Dispose();
                }
                return strs.ToArray();
            }
            catch
            {
                return null;
            }
            finally
            { strs = null; }
        }

        private void CloseDevice()
        {
            var command = MakeCommand(EnumCommand.Stop);
            _serialPort.Write(command, 0, command.Length);
        }

        private byte[] MakeCommand(EnumCommand command)
        {
            var commandBytes = new byte[] { 0x5A, 0xA5, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00 };

            switch(command)
            {
                case EnumCommand.Start:
                    commandBytes[2] = 1;
                    break;
                case EnumCommand.Stop:
                default:
                    commandBytes[2] = 2;
                    break;
            }

            commandBytes[7] = CRC8Calculate(commandBytes, 0,7);
            return commandBytes;

        }
        
        private void LoopFunction()
        {
            OpenDevice();
            _stopEvent.Reset();
            var waitInterval = 0;
            const int PackageLength = 68;
            byte[] buffer = new byte[PackageLength * 2];
            byte[] bufferClone = new byte[PackageLength * 2];
            int bufferLength = 0;
            while(!_stopEvent.WaitOne(waitInterval))
            {
                if(_serialPort.BytesToRead == 0)
                {
                    waitInterval = 1;
                    continue;
                }
                int readLength = _serialPort.BytesToRead;
                if(readLength+ bufferLength >= PackageLength*2)
                {
                    readLength = PackageLength * 2 - bufferLength;
                }
                _serialPort.Read(buffer, bufferLength, readLength);
                bufferLength += readLength;

                while (bufferLength>=PackageLength)
                {
                    int i;
                    bool founded = false;
                    for( i= 0;i< bufferLength - PackageLength+2; i++)
                    {
                        if(buffer[i] == 0x5a && buffer[i+1] == 0xa5)
                        {
                            var crc = CRC8Calculate(buffer, i, PackageLength - 1);
                            if(crc == buffer[i+PackageLength-1])
                            {
                                AnalyzePackage(buffer, i);
                                founded = true;
                                break;
                            }
                        }

                    }
                    if(founded)
                    {
                        bufferLength -= i+PackageLength;
                        Array.Copy(buffer, i, bufferClone, 0, bufferLength);
                        Array.Copy(bufferClone, buffer, bufferLength);
                    }else
                    {
                        bufferLength -= PackageLength-bufferLength+1;
                        Array.Copy(buffer, i, bufferClone, 0, bufferLength);
                        Array.Copy(bufferClone, buffer, bufferLength);

                    }

                }

            }

            CloseDevice();

        }
        private Thread _thread;
        private ManualResetEvent _stopEvent = new ManualResetEvent(false);
        private SerialPort _serialPort;

        private static byte CRC8Calculate(byte[] data, int offset, int length)
        {

            byte retCRCValue = 0x00;
            int i = 0;
            byte pDataBuf = 0;


            for (int index = offset; index < length-offset; index++)
            {
                pDataBuf = data[index];
                for (i = 0; i < 8; i++)
                {
                    if (((retCRCValue ^ (pDataBuf)) & 0x01) != 0)
                    {
                        retCRCValue ^= 0x18;
                        retCRCValue >>= 1;
                        retCRCValue |= 0x80;
                    }
                    else
                    {
                        retCRCValue >>= 1;
                    }
                    pDataBuf >>= 1;
                }

            }
            return retCRCValue;
        }

        private void AnalyzePackage(byte[] data, int offset)
        {
            uint pLT =  (uint)((data[offset+2]<<24 )+ (data[offset+3]<<16 )+(data[offset+4]<<8 )+ data[offset +5]);
            uint pLB = (uint)((data[offset + 6] << 24) + (data[offset + 7] << 16 )+ (data[offset + 8] << 8 )+ data[offset + 9]);
            uint pRT = (uint)((data[offset + 10] << 24) + (data[offset + 11] << 16) + (data[offset + 12] << 8) + data[offset + 13]);
            uint pRB = (uint)((data[offset + 14] << 24) + (data[offset + 15] << 16 )+ (data[offset + 16] << 8 )+ data[offset + 17]);
            var total = pLT + pLB + pRT + pRB;
            PressurreArrived?.Invoke((pLB + pLT) * 1f / total, (pLT + pRT) * 1f / total, (int)total / 4);
        }


    }

    public enum EnumDeviceConnectionStatus
    {
        Initial,
        Connected,
        Disconnected
    }
    public enum EnumCommand
    {
        Start,
        Stop
    }
}

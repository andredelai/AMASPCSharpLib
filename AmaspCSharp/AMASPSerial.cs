using System;
using System.IO.Ports;
using System.Text;

namespace AmaspCSharp
{
    public abstract class AMASPSerial
    {

        public const int MSGMAXSIZE = 4096;
        private const int PKTMAXSIZE = 4096 + 15;

        private ErrorCheckTypes errorCheckType = ErrorCheckTypes.None;

        SerialPort serialCom;

        public ErrorCheckTypes ErrorCheckType { get => errorCheckType; set => errorCheckType = value; }

        public enum PacketTypes
        {
            MRP = 0,
            SRP = 1,
            SIP = 2,
            CEP = 3,
            Timeout = 4
        }

        public enum ErrorCheckTypes
        {
            None = 0,
            XOR8 = 1,
            checksum16 = 2,
            LRC16 = 3,
            fletcher16 = 4,
            CRC16 = 5
        }

        public class PacketData
        {
            private PacketTypes type;
            private int deviceId;
            private byte[] message;
            private int codeLength;
            private ErrorCheckTypes errorCheckType;

            public PacketTypes Type { get => type; set => type = value; }
            public int DeviceId { get => deviceId; set => deviceId = value; }
            public byte[] Message { get => message; set => message = value; }
            public int CodeLength { get => codeLength; set => codeLength = value; }
            public ErrorCheckTypes ErrorCheckType { get => errorCheckType; set => errorCheckType = value; }
        }



        public bool Begin(SerialPort serialCom)
        {
            this.serialCom = serialCom;
            if (serialCom != null)
            {
                try
                {
                    serialCom.Open();
                }
                catch (Exception)
                {
                    return false;
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        public void end()
        {
            if (serialCom != null)
            {
                serialCom.Close();
            }
        }

        public void SendError(int deviceId, int errorCode)
        {
            byte[] hex;
            byte[] pkt = new byte[14];

            //Packet Type
            pkt[0] = (byte)'!';
            pkt[1] = (byte)'~';
            //ECA
            hex = Encoding.ASCII.GetBytes(String.Format("{0:X1}", errorCheckType));
            pkt[2] = hex[0];
            //Devide ID
            hex = Encoding.ASCII.GetBytes(String.Format("{0:X3}", deviceId));
            pkt[3] = hex[0];
            pkt[4] = hex[1];
            pkt[5] = hex[2];
            //Error Code
            hex = Encoding.ASCII.GetBytes(String.Format("{0:X2}", errorCode));
            pkt[6] = hex[0];
            pkt[7] = hex[1];
            //Error Checking data
            hex = Encoding.ASCII.GetBytes(String.Format("{0:X4}", errorCode));
            pkt[8] = hex[0];
            pkt[9] = hex[1];
            pkt[10] = hex[2];
            pkt[11] = hex[3];
            //Packet End
            pkt[12] = (byte)'\r';
            pkt[13] = (byte)'\n';

            serialCom.Write(pkt, 0, 14);
        }

        public PacketData readPacket()
        {
            PacketData pktData = new PacketData();
            byte[] buffer = new byte[PKTMAXSIZE];
            byte[] auxBuf = new byte[PKTMAXSIZE - 9];
            int aux;
            ErrorCheckTypes eCheck;
            double milisecPerByte = 1 / ((double)serialCom.BaudRate / 8000);

            pktData.Type = PacketTypes.Timeout;
            pktData.DeviceId = 0x000;
            pktData.CodeLength = 0x000;
            pktData.Message = null;

            try
            {
                while (serialCom.Read(buffer, 0, 1) > 0)
                {
                    if (buffer[0] == '!')
                    {
                        //Reading packet type, error check type and device ID bytes
                        aux = 0;
                        while (serialCom.BytesToRead < 5 && aux <= serialCom.ReadTimeout)
                        {
                            aux++;
                            System.Threading.Thread.Sleep(1);
                        }
                        if (serialCom.Read(auxBuf, 0, 5) == 5)
                        {
                            buffer[1] = auxBuf[0];//pkt type byte
                            buffer[2] = auxBuf[1];//error check type byte
                            buffer[3] = auxBuf[2];//device ID byte2
                            buffer[4] = auxBuf[3];//device ID byte1
                            buffer[5] = auxBuf[4];//devide ID byte0
                        }

                        //Pre-check of ECA value
                        if (buffer[2] < '0' || buffer[2] > '5')
                        {
                            //ECA no identified (ignore the packet) 
                            pktData.Type = PacketTypes.Timeout;
                            return pktData;
                        }

                        //Extracting error check type                                   
                        eCheck = (ErrorCheckTypes)(buffer[2] - '0');

                        //Extracting device ID
                        try
                        {
                            pktData.DeviceId = int.Parse(Encoding.UTF8.GetString(buffer, 3, 6), System.Globalization.NumberStyles.HexNumber);
                        }
                        catch
                        {
                            return pktData;
                        }

                        switch (buffer[1])
                        {
                            //MRP packet
                            case (byte)'?':
                                //Reading error check type, device ID and msg length
                                aux = 0;
                                while (serialCom.BytesToRead < 3 && aux <= serialCom.ReadTimeout)
                                {
                                    aux++;
                                    System.Threading.Thread.Sleep(1);
                                }
                                if (serialCom.Read(auxBuf, 0, 3) == 3)
                                {
                                    Array.Copy(auxBuf, 0, buffer, 6, 3);

                                    //Extracting message length
                                    pktData.CodeLength = int.Parse(Encoding.UTF8.GetString(buffer, 6, 9), System.Globalization.NumberStyles.HexNumber);

                                    //Reading message, error check data and end packet chars
                                    aux = 0;
                                    while (serialCom.BytesToRead <= (pktData.CodeLength + 6) && aux <= serialCom.ReadTimeout)
                                    {
                                        aux++;
                                        System.Threading.Thread.Sleep(1);
                                    }
                                    if (serialCom.Read(auxBuf, 0, pktData.CodeLength + 6) == pktData.CodeLength + 6)
                                    {
                                        Array.Copy(auxBuf, 0, buffer, 9, pktData.CodeLength + 6);
                                        aux = int.Parse(Encoding.UTF8.GetString(buffer, pktData.CodeLength + 9, pktData.CodeLength + 13), System.Globalization.NumberStyles.HexNumber);
                                        //checking for errors
                                        if (aux == errorCheck(buffer, pktData.CodeLength + 9, eCheck))
                                        {
                                            //End chars checking
                                            if (buffer[pktData.CodeLength + 13] == '\r' || buffer[pktData.CodeLength + 14] == '\n')
                                            {
                                                pktData.Message = new byte[pktData.CodeLength];
                                                Array.Copy(buffer, 9, pktData.Message, 0, pktData.CodeLength);
                                                pktData.Type = (PacketTypes.MRP); //MRP recognized
                                                pktData.ErrorCheckType = eCheck;
                                                return pktData;
                                            }
                                        }
                                    }
                                }
                                break;

                            //SRP packet
                            case (byte)'#':
                                //Reading error check type, device ID and msg length
                                aux = 0;
                                while (serialCom.BytesToRead < 3 && aux <= serialCom.ReadTimeout)
                                {
                                    aux++;
                                    System.Threading.Thread.Sleep(1);
                                }
                                if (serialCom.Read(auxBuf, 0, 3) == 3)
                                {
                                    Array.Copy(auxBuf, 0, buffer, 6, 3);

                                    //Extracting message length
                                    pktData.CodeLength = int.Parse(Encoding.UTF8.GetString(buffer, 6, 9), System.Globalization.NumberStyles.HexNumber);

                                    //Reading message, error check data and end packet chars
                                    aux = 0;
                                    while (serialCom.BytesToRead <= (pktData.CodeLength + 6) && aux <= serialCom.ReadTimeout)
                                    {
                                        aux++;
                                        System.Threading.Thread.Sleep(1);
                                    }
                                    if (serialCom.Read(auxBuf, 0, pktData.CodeLength + 6) == pktData.CodeLength + 6)
                                    {
                                        Array.Copy(auxBuf, 0, buffer, 9, pktData.CodeLength + 6);
                                        aux = int.Parse(Encoding.UTF8.GetString(buffer, pktData.CodeLength + 9, pktData.CodeLength + 13), System.Globalization.NumberStyles.HexNumber);
                                        //checking for errors
                                        if (aux == errorCheck(buffer, pktData.CodeLength + 9, eCheck))
                                        {
                                            //End chars checking
                                            if (buffer[pktData.CodeLength + 13] == '\r' || buffer[pktData.CodeLength + 14] == '\n')
                                            {
                                                pktData.Message = new byte[pktData.CodeLength];
                                                Array.Copy(buffer, 9, pktData.Message, 0, pktData.CodeLength);
                                                pktData.Type = (PacketTypes.SRP); //SRP recognized
                                                pktData.ErrorCheckType = eCheck;
                                                return pktData;
                                            }
                                        }
                                    }
                                }
                                break;

                            //CEP packet
                            case (byte)'~':
                                //Reading error code
                                aux = 0;
                                while (serialCom.BytesToRead < 8 && aux <= serialCom.ReadTimeout)
                                {
                                    aux++;
                                    System.Threading.Thread.Sleep(1);
                                }
                                if (serialCom.Read(auxBuf, 0, 8) == 8)
                                {
                                    Array.Copy(auxBuf, 0, buffer, 6, 8);
                                    aux = int.Parse(Encoding.UTF8.GetString(buffer, 8, 12), System.Globalization.NumberStyles.HexNumber);
                                    if (aux == errorCheck(buffer, 8, eCheck))
                                    {
                                        // ErrorCode extraction
                                        pktData.CodeLength = int.Parse(Encoding.UTF8.GetString(buffer, 6, 8), System.Globalization.NumberStyles.HexNumber);
                                        pktData.ErrorCheckType = eCheck;
                                        pktData.Type = PacketTypes.CEP; //CEP recognized
                                        return pktData;
                                    }
                                }
                                break;

                            //SIP packet
                            case (byte)'!':
                                //Reading error code
                                aux = 0;
                                while (serialCom.BytesToRead < 8 && aux <= serialCom.ReadTimeout)
                                {
                                    aux++;
                                    System.Threading.Thread.Sleep(1);
                                }
                                if (serialCom.Read(auxBuf, 0, 8) == 8)
                                {
                                    Array.Copy(auxBuf, 0, buffer, 6, 8);
                                    aux = int.Parse(Encoding.UTF8.GetString(buffer, 8, 12), System.Globalization.NumberStyles.HexNumber);
                                    if (aux == errorCheck(buffer, 8, eCheck))
                                    {
                                        // ErrorCode extraction
                                        pktData.CodeLength = int.Parse(Encoding.UTF8.GetString(buffer, 6, 8), System.Globalization.NumberStyles.HexNumber);
                                        pktData.ErrorCheckType = eCheck;
                                        pktData.Type = PacketTypes.SIP; //CEP recognized
                                        return pktData;
                                    }
                                }
                                break;

                            default:
                                break;
                        }
                    }
                }

            }
            catch
            {

            }

            return pktData;

        }

        protected ushort CRC16Check(byte[] data, int dataLenght)
        {
            ushort crc = 0xFFFF;
            for (int pos = 0; pos < dataLenght; pos++)
            {
                crc ^= (ushort)data[pos];
                for (int i = 8; i != 0; i--)
                {
                    if ((crc & 0x0001) != 0)
                    {
                        crc >>= 1;
                        crc &= 0x7FFF;
                        crc ^= 0xA001;
                    }
                    else
                    {
                        crc >>= 1;
                        crc &= 0x7FFF;
                    }
                }
            }

            // Note, this number has low and high bytes swapped, so use it accordingly (or swap bytes)
            return crc;
        }

        protected int LRC16Check(byte[] data, int dataLength)
        {
            int lrc = 0;
            for (int i = 0; i < dataLength; i++)
            {
                lrc = (lrc + data[i]) & 0xFFFF;
            }
            lrc = (((lrc ^ 0xFFFF) + 1) & 0xFFFF);
            return lrc;
        }

        protected int XORCheck(byte[] data, int dataLength)
        {
            byte xorCheck = 0;
            for (int i = 0; i < dataLength; i++)
            {
                xorCheck ^= data[i];
            }
            return (int)xorCheck;
        }

        //Classical checksum
        protected int checksum16Check(byte[] message, int dataLength)
        {
            int sum = 0;
            for (int i = 0; i < dataLength; i++)
            {
                sum += message[i];
            }
            return sum;
        }


        protected int fletcher16Checksum(byte[] data, int dataLength)
        {

            int sum1 = 0, sum2 = 0, index;

            for (index = 0; index < dataLength; ++index)
            {
                sum1 = (sum1 + data[index]) % 255;
                sum2 = (sum2 + sum1) % 255;
            }

            return (sum2 << 8) | sum1;

        }

        protected int errorCheck(byte[] data, int dataLength, ErrorCheckTypes eCheckType)
        {
            int ret;
            switch (eCheckType)
            {
                case ErrorCheckTypes.XOR8:
                    ret = XORCheck(data, dataLength);
                    break;

                case ErrorCheckTypes.checksum16:
                    ret = checksum16Check(data, dataLength);
                    break;

                case ErrorCheckTypes.LRC16:
                    ret = LRC16Check(data, dataLength);
                    break;

                case ErrorCheckTypes.fletcher16:
                    ret = fletcher16Checksum(data, dataLength);
                    break;

                case ErrorCheckTypes.CRC16:
                    ret = CRC16Check(data, dataLength);
                    break;

                default:
                    ret = 0x00;
                    break;
            }
            return ret;
        }

    }
}

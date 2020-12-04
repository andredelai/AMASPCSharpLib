/*    
  Created by Andre L. Delai.

  This is a free software; you can redistribute it and/or
  modify it under the terms of the GNU Lesser General Public
  License as published by the Free Software Foundation; either
  version 2.1 of the License, or (at your option) any later version.

  This library is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
  Lesser General Public License for more details.

  You should have received a copy of the GNU Lesser General Public
  License along with this library; if not, write to the Free Software
  Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
*/

using System;
using System.IO.Ports;
using System.Text;

namespace AmaspCSharp
{
    /// <summary>
    /// AMASP Abstract class
    /// </summary>
    public abstract class AMASPSerial
    {

        public const int MSGMAXSIZE = 4096;
        private const int PKTMAXSIZE = 4096 + 15;

        private ErrorCheckTypes errorCheckType = ErrorCheckTypes.None;

        SerialPort serialCom;
                
        public ErrorCheckTypes ErrorCheckType { get => errorCheckType; set => errorCheckType = value; }
        public SerialPort SerialCom { get => serialCom; set => serialCom = value; }

        /// <summary>
        /// Enumeration to the packet types available in AMASP and the Timeout.
        /// MRP(0), SRP(1), SIP(2), CEP(3), Timeout(4).
        /// </summary>
        public enum PacketTypes
        {
            MRP = 0,
            SRP = 1,
            SIP = 2,
            CEP = 3,
            Timeout = 4
        }

        /// <summary>
        /// Enumeration to the error checking algorithms available in AMASP.
        /// None(0), XOR8(1), checksum16(2), LRC16(3), fletcher16(4), CRC16(5).
        /// </summary>
        public enum ErrorCheckTypes
        {
            None = 0,
            XOR8 = 1,
            checksum16 = 2,
            LRC16 = 3,
            fletcher16 = 4,
            CRC16 = 5
        }

        /// <summary>
        /// Represents the data packet, containing the packet type, device ID, Message, code length and erro check type.
        /// </summary>
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


        /// <summary>
        /// Establishes a serial connection.
        /// </summary>
        /// <param name="serialCom">The serial communication object.</param>
        /// <returns>True if the serial connection was stablished or false if not.</returns>
        public bool Begin(SerialPort serialCom)
        {
            this.SerialCom = serialCom;
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

        /// <summary>
        /// Closes the serial connection.
        /// </summary>
        public void end()
        {
            if (SerialCom != null)
            {
                SerialCom.Close();
            }
        }

        /// <summary>
        /// Send a CEP packet (Communication Error Packet).
        /// </summary>
        /// <param name="deviceId">Id of the target device in communication.</param>
        /// <param name="errorCode">The communication error code (0 to 255).</param>
        public void SendError(int deviceId, int errorCode)
        {
            byte[] hex;
            byte[] pkt = new byte[14];

            //Packet Type
            pkt[0] = (byte)'!';
            pkt[1] = (byte)'~';
            //ECA
            hex = Encoding.Default.GetBytes(String.Format("{0:X1}", errorCheckType));
            pkt[2] = hex[0];
            //Devide ID
            hex = Encoding.Default.GetBytes(String.Format("{0:X3}", deviceId));
            pkt[3] = hex[0];
            pkt[4] = hex[1];
            pkt[5] = hex[2];
            //Error Code
            hex = Encoding.Default.GetBytes(String.Format("{0:X2}", errorCode));
            pkt[6] = hex[0];
            pkt[7] = hex[1];
            //Error Checking data
            hex = Encoding.Default.GetBytes(String.Format("{0:X4}", errorCode));
            pkt[8] = hex[0];
            pkt[9] = hex[1];
            pkt[10] = hex[2];
            pkt[11] = hex[3];
            //Packet End
            pkt[12] = (byte)'\r';
            pkt[13] = (byte)'\n';

            SerialCom.Write(pkt, 0, 14);
        }

        /// <summary>
        /// Check if a valid packet is available and read it.
        /// </summary>
        /// <returns>A PacketData Object which contains the information and data from a packet.</returns>
        public PacketData readPacket()
        {
            PacketData pktData = new PacketData();
            byte[] buffer = new byte[PKTMAXSIZE];
            byte[] auxBuf = new byte[PKTMAXSIZE - 9];
            int aux;
            ErrorCheckTypes eCheck;
            double milisecPerByte = 1 / ((double)SerialCom.BaudRate / 8000);

            pktData.Type = PacketTypes.Timeout;
            pktData.DeviceId = 0x000;
            pktData.CodeLength = 0x000;
            pktData.Message = null;

            try
            {
                while (SerialCom.Read(buffer, 0, 1) > 0)
                {
                    if (buffer[0] == '!')
                    {
                        //Reading packet type, error check type and device ID bytes
                        aux = 0;
                        while (SerialCom.BytesToRead < 5 && aux <= SerialCom.ReadTimeout)
                        {
                            aux++;
                            System.Threading.Thread.Sleep(1);
                        }
                        if (SerialCom.Read(auxBuf, 0, 5) == 5)
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
                            pktData.DeviceId = int.Parse(Encoding.Default.GetString(buffer, 3, 6), System.Globalization.NumberStyles.HexNumber);
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
                                while (SerialCom.BytesToRead < 3 && aux <= SerialCom.ReadTimeout)
                                {
                                    aux++;
                                    System.Threading.Thread.Sleep(1);
                                }
                                if (SerialCom.Read(auxBuf, 0, 3) == 3)
                                {
                                    Array.Copy(auxBuf, 0, buffer, 6, 3);

                                    //Extracting message length
                                    pktData.CodeLength = int.Parse(Encoding.Default.GetString(buffer, 6, 9), System.Globalization.NumberStyles.HexNumber);

                                    //Reading message, error check data and end packet chars
                                    aux = 0;
                                    while (SerialCom.BytesToRead <= (pktData.CodeLength + 6) && aux <= SerialCom.ReadTimeout)
                                    {
                                        aux++;
                                        System.Threading.Thread.Sleep(1);
                                    }
                                    if (SerialCom.Read(auxBuf, 0, pktData.CodeLength + 6) == pktData.CodeLength + 6)
                                    {
                                        Array.Copy(auxBuf, 0, buffer, 9, pktData.CodeLength + 6);
                                        aux = int.Parse(Encoding.Default.GetString(buffer, pktData.CodeLength + 9, pktData.CodeLength + 13), System.Globalization.NumberStyles.HexNumber);
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
                                while (SerialCom.BytesToRead < 3 && aux <= SerialCom.ReadTimeout)
                                {
                                    aux++;
                                    System.Threading.Thread.Sleep(1);
                                }
                                if (SerialCom.Read(auxBuf, 0, 3) == 3)
                                {
                                    Array.Copy(auxBuf, 0, buffer, 6, 3);

                                    //Extracting message length
                                    pktData.CodeLength = int.Parse(Encoding.Default.GetString(buffer, 6, 9), System.Globalization.NumberStyles.HexNumber);

                                    //Reading message, error check data and end packet chars
                                    aux = 0;
                                    while (SerialCom.BytesToRead <= (pktData.CodeLength + 6) && aux <= SerialCom.ReadTimeout)
                                    {
                                        aux++;
                                        System.Threading.Thread.Sleep(1);
                                    }
                                    if (SerialCom.Read(auxBuf, 0, pktData.CodeLength + 6) == pktData.CodeLength + 6)
                                    {
                                        Array.Copy(auxBuf, 0, buffer, 9, pktData.CodeLength + 6);
                                        aux = int.Parse(Encoding.Default.GetString(buffer, pktData.CodeLength + 9, pktData.CodeLength + 13), System.Globalization.NumberStyles.HexNumber);
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
                                while (SerialCom.BytesToRead < 8 && aux <= SerialCom.ReadTimeout)
                                {
                                    aux++;
                                    System.Threading.Thread.Sleep(1);
                                }
                                if (SerialCom.Read(auxBuf, 0, 8) == 8)
                                {
                                    Array.Copy(auxBuf, 0, buffer, 6, 8);
                                    aux = int.Parse(Encoding.Default.GetString(buffer, 8, 12), System.Globalization.NumberStyles.HexNumber);
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
                                while (SerialCom.BytesToRead < 8 && aux <= SerialCom.ReadTimeout)
                                {
                                    aux++;
                                    System.Threading.Thread.Sleep(1);
                                }
                                if (SerialCom.Read(auxBuf, 0, 8) == 8)
                                {
                                    Array.Copy(auxBuf, 0, buffer, 6, 8);
                                    aux = int.Parse(Encoding.Default.GetString(buffer, 8, 12), System.Globalization.NumberStyles.HexNumber);
                                    if (aux == errorCheck(buffer, 8, eCheck))
                                    {
                                        // ErrorCode extraction
                                        pktData.CodeLength = int.Parse(Encoding.Default.GetString(buffer, 6, 8), System.Globalization.NumberStyles.HexNumber);
                                        pktData.ErrorCheckType = eCheck;
                                        pktData.Type = PacketTypes.SIP; //SIP recognized
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

        protected ushort CRC16Modbus(byte[] data, int dataLength)
        {
            ushort crc = 0xFFFF;
            for (int pos = 0; pos < dataLength; pos++)
            {
                crc ^= data[pos];

                for (int i = 8; i != 0; i--)
                {
                    if ((crc & 0x0001) != 0)
                    {
                        crc >>= 1;
                        crc ^= 0xA001; // Polynomial Modbus
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }
            }
            return crc;
        }


        protected ushort LRC16Check(byte[] data, int dataLength)
        {
            ushort lrc = 0;
            for (int i = 0; i < dataLength; i++)
            {
                lrc = (ushort)((lrc + data[i]) & 0xFFFF);
            }
            lrc = (ushort)(((lrc ^ 0xFFFF) + 1) & 0xFFFF);
            return lrc;
        }

        protected ushort XORCheck(byte[] data, int dataLength)
        {
            byte xorCheck = 0;
            for (int i = 0; i < dataLength; i++)
            {
                xorCheck ^= data[i];
            }
            return xorCheck;
        }

        //Classical checksum
        protected ushort checksum16Check(byte[] data, int dataLength)
        {
            ushort sum = 0;
            for (int i = 0; i < dataLength; i++)
            {
                sum += data[i];
            }
            return (sum);
        }


        protected ushort Fletcher16Check(byte[] data, int dataLength)
        {
            uint sum1 = 0, sum2 = 0, index;

            for (index = 0; index < dataLength; ++index)
            {
                sum1 = (sum1 + data[index]) % 255;
                sum2 = (sum2 + sum1) % 255;
            }

            return (ushort)((sum2 << 8) | sum1);
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
                    ret = Fletcher16Check(data, dataLength);
                    break;

                case ErrorCheckTypes.CRC16:
                    ret = CRC16Modbus(data, dataLength);
                    break;

                default:
                    ret = 0x00;
                    break;
            }
            return ret;
        }

    }
}

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
using System.Collections.Generic;
using System.Text;

namespace AmaspCSharp
{
    /// <summary>
    /// AMASP Master class
    /// </summary>
    public class AMASPSerialMaster : AMASPSerial
    {

        /// <summary>
        /// Send a MRP packet to a slave computer.
        /// </summary>
        /// <param name="deviceId">Id of the requested device in slave.</param>
        /// <param name="message">The message in bytes to be send.</param>
        /// <param name="msgLength">The message length.</param>
        /// <returns>The error check data.</returns>
        public int SendRequest (int deviceId, byte[] message, int msgLength)
        {
            byte[] hex;
            int ecd;

            if (message.Length < msgLength)
            {
                msgLength = message.Length; //saturating
            }

            //mounting the packet
            byte[] pkt = new byte[msgLength + 15];
            //Packet Type
            pkt[0] = (byte) '!';
            pkt[1] = (byte)'?';
            //ECA
            
            hex = Encoding.Default.GetBytes(((int)ErrorCheckType).ToString("X1"));
            //hex = Encoding.Default.GetBytes(String.Format("{0:X1}", ErrorCheckType));
            pkt[2] = hex[0];
            //device Id
            hex = Encoding.Default.GetBytes(deviceId.ToString("X3"));
            pkt[3] = hex[0];
            pkt[4] = hex[1];
            pkt[5] = hex[2];
            //Message length
            hex = Encoding.Default.GetBytes(msgLength.ToString("X3"));
            pkt[6] = hex[0];
            pkt[7] = hex[1];
            pkt[8] = hex[2];
            //Message (payload)
            for (int i = 0; i < msgLength; i++)
            {
                pkt[9 + i] = message[i];
            }
            //Error checking
            ecd = ErrorCheck(pkt, msgLength + 9, ErrorCheckType);
            hex = Encoding.Default.GetBytes(ecd.ToString("X4"));
            pkt[9 + msgLength] = hex[0];
            pkt[9 + msgLength + 1] = hex[1];
            pkt[9 + msgLength + 2] = hex[2];
            pkt[9 + msgLength + 3] = hex[3];
            //Packet End
            pkt[9 + msgLength + 4] = (byte)'\r';
            pkt[9 + msgLength + 5] = (byte)'\n';

            //Sending request
            SerialCom.Write(pkt, 0, 15 + msgLength);
            return ecd;
        }

        /// <summary>
        /// Send a MRP packet to a slave computer.
        /// </summary>
        /// <param name="deviceID">Id of the requested device in slave.</param>
        /// <param name="message">The string message to be send.</param>
        /// <param name="msgLength">The message length.</param>
        /// /// <returns>The error check data.</returns>
        public int SendRequest(int deviceID, String message, int msgLength)
        {
            return SendRequest(deviceID, Encoding.Default.GetBytes(message), msgLength);
        }
    }
}

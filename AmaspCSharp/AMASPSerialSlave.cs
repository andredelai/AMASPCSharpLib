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
    class AMASPSerialSlave : AMASPSerial
    {
        public void sendResponse(int deviceId, byte[] message, int msgLength)
        {
            byte[] hex;

            if (message.Length < msgLength)
            {
                msgLength = message.Length; //saturating
            }

            //mounting the packet
            byte[] pkt = new byte[msgLength + 15];

            //Packet Type
            pkt[0] = (byte)'!';
            pkt[1] = (byte)'#';
            //ECA
            hex = Encoding.Default.GetBytes(String.Format("{0:X1}", ErrorCheckType));
            pkt[2] = hex[0];
            //Device ID
            hex = Encoding.Default.GetBytes(String.Format("{0:X3}", deviceId));
            pkt[3] = hex[0];
            pkt[4] = hex[1];
            pkt[5] = hex[2];
            //Message Length
            hex = Encoding.Default.GetBytes(String.Format("{0:X3}", msgLength));
            pkt[6] = (byte)hex[0];
            pkt[7] = (byte)hex[1];
            pkt[8] = (byte)hex[2];
            //Message (payload)
            for (int i = 0; i < msgLength; i++)
            {
                pkt[9 + i] = message[i];
            }
            //CRC       
            hex = Encoding.Default.GetBytes(String.Format("{0:X4}", errorCheck(pkt, msgLength + 9, ErrorCheckType)));
            pkt[9 + msgLength] = (byte)hex[0];
            pkt[9 + msgLength + 1] = (byte)hex[1];
            pkt[9 + msgLength + 2] = (byte)hex[2];
            pkt[9 + msgLength + 3] = (byte)hex[3];
            //Packet End
            pkt[9 + msgLength + 4] = (byte)'\r';
            pkt[9 + msgLength + 5] = (byte)'\n';

            //Sending request
            SerialCom.Write(pkt, 0, 15 + msgLength);
        }
    }
}

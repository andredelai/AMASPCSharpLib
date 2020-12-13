= AMASP Library for C# =

//AMASP is a very simple protocol based on four packet types to communication between a Master and a Slave computer.

This library implements the AMASP (ASCII Master/Slave Protocol) for Arduino boards, a simple way to exchange messages between two computers using serial communication.

AMASP is an open standard communication protocol that uses four different packet types:

MASTER to SLAVE:

 MRP - Master Request Packet
 CEP - Communication Error Packet

SLAVE to MASTER:

 SRP - Slave Response Packet 
 SIP - Slave Interruption Packet
 CEP - Communication Error Packet

The protocol is transparent to the user that only needs to use the AMASP Arduino Library functions to implement his own applications. Please, see the available examples codes.

The library is always in test and improvements, if you have any problem using it, please send a mail to the author (Spanish, Portuguese or English) adelai@gmail.com.

Contributors will be welcome!

Do you want to develop an AMASP library to other platforms? Be my guest!

Author:
Andre L. Delai
adelai@gmail.com

Developed in the Renato Archer Information Technology Center (http://www.cti.gov.br) - Brazil.

Documentation about AMASP available here:  https://doi.org/10.14209/jcis.2019.1

Enjoy :)

== License ==

Copyright (c) Arduino LLC. All right reserved.

This library is free software; you can redistribute it and/or
modify it under the terms of the GNU Lesser General Public
License as published by the Free Software Foundation; either
version 2.1 of the License, or (at your option) any later version.

This library is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
Lesser General Public License for more details.

You should have received a copy of the GNU Lesser General Public
License along with this library; if not, write to the Free Software
Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA 02110-1301 USA

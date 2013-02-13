/*
This file is part of SharpPcap.

SharpPcap is free software: you can redistribute it and/or modify
it under the terms of the GNU Lesser General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

SharpPcap is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Lesser General Public License for more details.

You should have received a copy of the GNU Lesser General Public License
along with SharpPcap.  If not, see <http://www.gnu.org/licenses/>.
*/
/* 
 * Copyright 2005 Tamir Gal <tamir@tamirgal.com>
 * Copyright 2009 Chris Morgan <chmorgan@gmail.com>
 */

using System;
using System.Text;

namespace SharpPcap.Containers
{
    // managed version of pcap_addr
    public class PcapAddress
    {
        private Sockaddr addr;
        public Sockaddr Addr
        {
            get { return addr; }
            internal set { addr = value; }
        }

        private Sockaddr netmask;
        public Sockaddr Netmask
        {
            get { return netmask; }
            internal set { netmask = value; }
        }

        private Sockaddr broadaddr;
        public Sockaddr Broadaddr
        {
            get { return broadaddr; }
            internal set { broadaddr = value; }
        }

        private Sockaddr dstaddr;
        public Sockaddr Dstaddr
        {
            get { return dstaddr; }
            internal set { dstaddr = value; }
        }

        internal PcapAddress()
        { }

        internal PcapAddress(PcapUnmanagedStructures.pcap_addr pcap_addr)
        {
            if (pcap_addr.Addr != IntPtr.Zero)
                Addr = new Sockaddr(pcap_addr.Addr);
            if (pcap_addr.Netmask != IntPtr.Zero)
                Netmask = new Sockaddr(pcap_addr.Netmask);
            if (pcap_addr.Broadaddr != IntPtr.Zero)
                Broadaddr = new Sockaddr(pcap_addr.Broadaddr);
            if (pcap_addr.Dstaddr != IntPtr.Zero)
                Dstaddr = new Sockaddr(pcap_addr.Dstaddr);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            if (Addr != null)
                sb.AppendFormat(" Address: {0}\n", Addr.ToString());

            if (Netmask != null)
                sb.AppendFormat(" Subnet Mask: {0}\n", Netmask.ToString());

            if (Broadaddr != null)
                sb.AppendFormat(" Broadcast Address: {0}\n", Broadaddr.ToString());

            if (Dstaddr != null)
                sb.AppendFormat(" Destination Address: {0}\n", Dstaddr.ToString());

            return sb.ToString();
        }
    }
}

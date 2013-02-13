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
 * Copyright 2008-2009 Chris Morgan <chmorgan@gmail.com>
 * Copyright 2008-2009 Phillip Lemon <lucidcomms@gmail.com>
 */

using SharpPcap.Containers;
using SharpPcap.Packets;
using System;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text;

namespace SharpPcap
{
    /// <summary>
    /// Capture live packets from a network device
    /// </summary>
    public partial class PcapDevice
    {
        /// <summary>
        /// The working mode of a Pcap device
        /// </summary>
        public enum PcapMode
        {
            /// <summary>
            /// Set a Pcap device to Capture mode (MODE_CAPT)
            /// </summary>
            Capture,

            /// <summary>
            /// Set a Pcap device to Statistics mode (MODE_STAT)
            /// </summary>
            Statistics
        };

        private PcapInterface m_pcapIf;
        public PcapInterface Interface
        {
            get { return m_pcapIf; }
        }

        private IntPtr m_pcapAdapterHandle = IntPtr.Zero;
        private IntPtr m_pcapDumpHandle = IntPtr.Zero;
        private PcapMode m_pcapMode = PcapMode.Capture;
        private int m_pcapPacketCount = Pcap.INFINITE;//Infinite
        private int m_mask = 0; //for filter expression

        /// <summary>
        /// Constructs a new PcapDevice based on a 'pcapIf' struct
        /// </summary>
        /// <param name="pcapIf">A 'pcapIf' struct representing
        /// the pcap device</param>
        internal PcapDevice(PcapInterface pcapIf)
        {
            m_pcapIf = pcapIf;
        }

        /// <summary>
        /// Default contructor for subclasses
        /// </summary>
        protected PcapDevice()
        {
        }

        /// <summary>
        /// PcapDevice finalizer.  Ensure PcapDevices are stopped and closed before exit.
        /// </summary>
        ~PcapDevice()
        {
            this.Close();
        }

        /// <summary>
        /// Fires whenever a new packet is received on this Pcap Device.<br/>
        /// This event is invoked only when working in "PcapMode.Capture" mode.
        /// </summary>
        public event Pcap.PacketArrivalEvent OnPacketArrival;

        /// <summary>
        /// Fires whenever a new pcap statistics is available for this Pcap Device.<br/>
        /// This event is invoked only when working in "PcapMode.Statistics" mode.
        /// </summary>
        public event Pcap.PcapStatisticsModeEvent OnPcapStatistics;

        /// <summary>
        /// Fired when the capture process of this pcap device is stopped
        /// </summary>
        public event Pcap.PcapCaptureStoppedEvent OnCaptureStopped;

        /// <summary>
        /// Gets the pcap name of this network device
        /// </summary>
        public virtual string Name
        {
            get { return m_pcapIf.Name; }
        }

        public virtual ReadOnlyCollection<PcapAddress> Addresses
        {
            get { return new ReadOnlyCollection<PcapAddress>(m_pcapIf.Addresses); }
        }

        /// <summary>
        /// Gets the pcap description of this device
        /// </summary>
        public virtual string Description
        {
            get { return m_pcapIf.Description; }
        }

        public virtual uint Flags
        {
            get { return m_pcapIf.Flags; }
        }

        public virtual bool Loopback
        {
            get { return (Flags & Pcap.PCAP_IF_LOOPBACK) == 1; }
        }

        /// <summary>
        /// The underlying pcap device handle
        /// </summary>
        internal virtual IntPtr PcapHandle
        {
            get { return m_pcapAdapterHandle; }
            set { m_pcapAdapterHandle = value; }
        }

        /// <summary>
        /// Return the pcap link layer value of an adapter. 
        /// </summary>
        public virtual LinkLayers PcapDataLink
        {
            get
            {
                if (!Opened)
                    throw new PcapDeviceNotReadyException("Cannot get datalink, the pcap device is not opened");
                return (LinkLayers)SafeNativeMethods.pcap_datalink(PcapHandle);
            }
        }

        /// <summary>
        /// Return a value indicating if this adapter is opened
        /// </summary>
        public virtual bool Opened
        {
            get { return (PcapHandle != IntPtr.Zero); }
        }

        public virtual PcapMode Mode
        {
            get { return m_pcapMode; }
            set
            {
                if (!Opened)
                {
                    throw new PcapDeviceNotReadyException
                        ("Can't set PcapMode, the device is not opened");
                }

                m_pcapMode = value;
                int mode = (m_pcapMode == PcapMode.Capture ?
                             Pcap.MODE_CAPT :
                             Pcap.MODE_STAT);
                int result = SafeNativeMethods.pcap_setmode(this.PcapHandle, mode);
                if (result < 0)
                    throw new PcapException("Error setting PcapDevice mode. : " + LastError);
            }
        }

        /// <summary>
        /// Open the device with default values of: promiscuous_mode=false, read_timeout=1000
        /// To start capturing call the 'PcapStartCapture' function
        /// </summary>
        public virtual void Open()
        {
            this.Open(false);
        }

        /// <summary>
        /// Open the device. To start capturing call the 'PcapStartCapture' function
        /// </summary>
        /// <param name="promiscuous_mode">A value indicating wether to open the
        ///  device in promiscuous mode (true = capture *all* packets on the network,
        ///  including packets not for me)</param>
        public virtual void Open(bool promiscuous_mode)
        {
            this.Open(promiscuous_mode, 1000);
        }

        /// <summary>
        /// Open the device. To start capturing call the 'PcapStartCapture' function
        /// </summary>
        /// <param name="promiscuous_mode">A value indicating wether to open the
        ///  device in promiscuous mode (true = capture *all* packets on the network,
        ///  including packets not for me)</param>
        /// <param name="read_timeout">The timeout in miliseconds to wait for a  packet arrival.</param>
        public virtual void Open(bool promiscuous_mode, int read_timeout)
        {
            short mode = 0;
            if (promiscuous_mode) mode = 1;

            if (!Opened)
            {
                StringBuilder errbuf = new StringBuilder(Pcap.PCAP_ERRBUF_SIZE); //will hold errors

                PcapHandle = SafeNativeMethods.pcap_open_live
                    (Name,           // name of the device
                        Pcap.MAX_PACKET_SIZE,   // portion of the packet to capture. 
                    // MAX_PACKET_SIZE (65536) grants that the whole packet will be captured on all the MACs.
                        mode,               // promiscuous mode
                        (short)read_timeout,// read timeout                                             
                        errbuf);           // error buffer

                if (PcapHandle == IntPtr.Zero)
                {
                    string err = "Unable to open the adapter (" + Name + "). " + errbuf.ToString();
                    throw new PcapException(err);
                }
            }
        }

        /// <summary>
        /// Set/Get Non-Blocking Mode. returns allways false for savefiles.
        /// </summary>
        private const int disableBlocking = 0;
        private const int enableBlocking = 1;
        public bool NonBlockingMode
        {
            get
            {
                StringBuilder errbuf = new StringBuilder(Pcap.PCAP_ERRBUF_SIZE); //will hold errors
                int ret = SafeNativeMethods.pcap_getnonblock(PcapHandle, errbuf);

                // Errorbuf is only filled when ret = -1
                if (ret == -1)
                {
                    string err = "Unable to set get blocking" + errbuf.ToString();
                    throw new PcapException(err);
                }

                if (ret == enableBlocking)
                    return true;
                return false;
            }
            set
            {
                StringBuilder errbuf = new StringBuilder(Pcap.PCAP_ERRBUF_SIZE); //will hold errors

                int block = disableBlocking;
                if (value)
                    block = enableBlocking;

                int ret = SafeNativeMethods.pcap_setnonblock(PcapHandle, block, errbuf);

                // Errorbuf is only filled when ret = -1
                if (ret == -1)
                {
                    string err = "Unable to set non blocking" + errbuf.ToString();
                    throw new PcapException(err);
                }
            }
        }

        /// <summary>
        /// Closes this adapter
        /// </summary>
        public virtual void Close()
        {
            if (PcapHandle == IntPtr.Zero)
                return;

            if (Started)
            {
                StopCapture();
            }
            SafeNativeMethods.pcap_close(PcapHandle);
            PcapHandle = IntPtr.Zero;

            //Remove event handlers
            if (OnPacketArrival != null)
            {
                foreach (Pcap.PacketArrivalEvent pa in OnPacketArrival.GetInvocationList())
                {
                    OnPacketArrival -= pa;
                }
            }
            if (OnPcapStatistics != null)
            {
                foreach (Pcap.PcapStatisticsModeEvent pse in OnPcapStatistics.GetInvocationList())
                {
                    OnPcapStatistics -= pse;
                }
            }
        }

        /// <summary>
        /// Gets the next packet captured on this device
        /// </summary>
        /// <returns>The next packet captured on this device</returns>
        public virtual Packet GetNextPacket()
        {
            Packet p;
            int res = GetNextPacket(out p);
            if (res == -1)
                throw new PcapException("Error receiving packet.");
            return p;
        }

        /// <summary>
        /// Gets the next packet captured on this device
        /// </summary>
        /// <param name="p">A packet reference</param>
        /// <returns>A reference to a packet object</returns>
        public virtual int GetNextPacket(out Packet p)
        {
            //Pointer to a packet info struct
            IntPtr header = IntPtr.Zero;
            //Pointer to a packet struct
            IntPtr data = IntPtr.Zero;
            int res = 0;

            // using an invalid PcapHandle can result in an unmanaged segfault
            // so check for that here
            if (!Opened)
            {
                throw new PcapDeviceNotReadyException("Device must be opened via Open() prior to use");
            }

            //Get a packet from winpcap
            res = SafeNativeMethods.pcap_next_ex(PcapHandle, ref header, ref data);
            p = null;

            if (res > 0)
            {
                //Marshal the packet
                if ((header != IntPtr.Zero) && (data != IntPtr.Zero))
                {
                    p = MarshalPacket(header, data);
                }
            }
            return res;
        }

        /// <summary>
        /// Gets the next packet captured on this device
        /// </summary>
        /// <returns>The next packet captured on this device</returns>
        public virtual RawPacket GetNextRawPacket()
        {
            RawPacket p;
            int res = GetNextPacket(out p);
            if (res == -1)
                throw new PcapException("Error receiving packet.");
            return p;
        }

        /// <summary>
        /// Retrieve the next packet as a RawPacket. Method is temporary until
        /// packet parsing code is removed from SharpPcap when Packet.net is mature enough
        /// </summary>
        /// <param name="p">
        /// A <see cref="RawPacket"/>
        /// </param>
        /// <returns>
        /// A <see cref="System.Int32"/>
        /// </returns>
        public virtual int GetNextPacket(out RawPacket p)
        {
            //Pointer to a packet info struct
            IntPtr header = IntPtr.Zero;
            //Pointer to a packet struct
            IntPtr data = IntPtr.Zero;
            int res = 0;

            // using an invalid PcapHandle can result in an unmanaged segfault
            // so check for that here
            if (!Opened)
            {
                throw new PcapDeviceNotReadyException("Device must be opened via Open() prior to use");
            }

            //Get a packet from winpcap
            res = SafeNativeMethods.pcap_next_ex(PcapHandle, ref header, ref data);
            p = null;

            if (res > 0)
            {
                //Marshal the packet
                if ((header != IntPtr.Zero) && (data != IntPtr.Zero))
                {
                    p = MarshalRawPacket(header, data);
                }
            }
            return res;
        }

        /// <summary>
        /// Pcap_loop callback method.
        /// </summary>
        protected virtual void PacketHandler(IntPtr param, IntPtr /* pcap_pkthdr* */ header, IntPtr data)
        {
            Packet p = MarshalPacket(header, data);
            SendPacketArrivalEvent(p);
        }

        protected virtual Packet MarshalPacket(IntPtr header, IntPtr data)
        {
            Packet p;

            // marshal the header
            PcapHeader pcapHeader = new PcapHeader(header);

            byte[] pkt_data = new byte[pcapHeader.CaptureLength];
            Marshal.Copy(data, pkt_data, 0, (int)pcapHeader.CaptureLength);

            p = Packets.PacketFactory.dataToPacket(PcapDataLink, pkt_data,
                                                   new Packets.Util.Timeval(pcapHeader.Seconds,
                                                                            pcapHeader.MicroSeconds));
            p.pcapHeader = pcapHeader;

            return p;
        }

        protected virtual RawPacket MarshalRawPacket(IntPtr /* pcap_pkthdr* */ header, IntPtr data)
        {
            RawPacket p;

            // marshal the header
            PcapHeader pcapHeader = new PcapHeader(header);

            byte[] pkt_data = new byte[pcapHeader.CaptureLength];
            Marshal.Copy(data, pkt_data, 0, (int)pcapHeader.CaptureLength);

            p = new RawPacket(PcapDataLink,
                              new Packets.Util.Timeval(pcapHeader.Seconds,
                                                       pcapHeader.MicroSeconds),
                              pkt_data);

            return p;
        }

        private void SendPacketArrivalEvent(Packet p)
        {
            //If mode is MODE_CAP:
            if (Mode == PcapMode.Capture)
            {
                if (OnPacketArrival != null)
                {
                    //Invoke the packet arrival event                                           
                    OnPacketArrival(this, new PcapCaptureEventArgs(p, this));
                }
            }
            //else mode is MODE_STAT
            else if (Mode == PcapMode.Statistics)
            {
                if (OnPcapStatistics != null)
                {
                    //Invoke the pcap statistics event
                    OnPcapStatistics(this, new PcapStatisticsModeEventArgs(p, this));
                }
            }
        }

        private void SendCaptureStoppedEvent(bool error)
        {
            if (OnCaptureStopped != null)
            {
                //Notify upper applications
                OnCaptureStopped(this, error);
            }
        }

        // If CompileFilter() returns true bpfProgram must be freed by passing it to FreeBpfProgram()
        // or unmanaged memory will be leaked
        private static bool CompileFilter(IntPtr pcapHandle,
                                          string filterExpression,
                                          uint mask,
                                          out IntPtr bpfProgram,
                                          out string errorString)
        {
            int result;
            string err = String.Empty;

            bpfProgram = IntPtr.Zero;
            errorString = null;

            //Alocate an unmanaged buffer
            bpfProgram = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(PcapUnmanagedStructures.bpf_program)));

            //compile the expressions
            result = SafeNativeMethods.pcap_compile(pcapHandle,
                                                    bpfProgram,
                                                    filterExpression,
                                                    1,
                                                    mask);

            if (result < 0)
            {
                err = GetLastError(pcapHandle);

                // free up the program memory
                Marshal.FreeHGlobal(bpfProgram);
                bpfProgram = IntPtr.Zero; // make sure not to pass out a valid pointer

                // set the error string
                errorString = err;

                return false;
            }

            return true;
        }

        /// <summary>
        /// Free memory allocated in CompileFilter()
        /// </summary>
        /// <param name="bpfProgram">
        /// A <see cref="IntPtr"/>
        /// </param>
        private static void FreeBpfProgram(IntPtr bpfProgram)
        {
            // free any pcap internally allocated memory from pcap_compile()
            SafeNativeMethods.pcap_freecode(bpfProgram);

            // free allocated buffers
            Marshal.FreeHGlobal(bpfProgram);
        }

        /// <summary>
        /// Returns true if the filter expression was able to be compiled into a
        /// program without errors
        /// </summary>
        public static bool CheckFilter(string filterExpression,
                                       out string errorString)
        {
            IntPtr bpfProgram;
            IntPtr fakePcap = SafeNativeMethods.pcap_open_dead((int)LinkLayers.Ethernet10Mb, Pcap.MAX_PACKET_SIZE);

            uint mask = 0;
            if (!CompileFilter(fakePcap, filterExpression, mask, out bpfProgram, out errorString))
            {
                SafeNativeMethods.pcap_close(fakePcap);
                return false;
            }

            FreeBpfProgram(bpfProgram);

            SafeNativeMethods.pcap_close(fakePcap);
            return true;
        }

        /// <summary>
        /// Compile a kernel level filtering expression, and associate the filter 
        /// with this device. For more info on filter expression syntax, see:
        /// http://www.winpcap.org/docs/docs_40_2/html/group__language.html
        /// </summary>
        /// <param name="filterExpression">The filter expression to compile</param>
        public virtual void SetFilter(string filterExpression)
        {
            int res;
            IntPtr bpfProgram;
            string errorString;

            // pcap_setfilter() requires a valid pcap_t which isn't present if
            // the device hasn't been opened
            if (!Opened)
            {
                throw new PcapDeviceNotReadyException("device is not open");
            }

            // attempt to compile the program
            if (!CompileFilter(PcapHandle, filterExpression, (uint)m_mask, out bpfProgram, out errorString))
            {
                string err = string.Format("Can't compile filter ({0}) : {1} ", filterExpression, errorString);
                throw new PcapException(err);
            }

            //associate the filter with this device
            res = SafeNativeMethods.pcap_setfilter(PcapHandle, bpfProgram);

            // Free the program whether or not we were successful in setting the filter
            // we don't want to leak unmanaged memory if we throw an exception.
            FreeBpfProgram(bpfProgram);

            //watch for errors
            if (res < 0)
            {
                errorString = string.Format("Can't set filter ({0}) : {1}", filterExpression, LastError);
                throw new PcapException(errorString);
            }
        }

        /// <summary>
        /// Opens a file for packet writings
        /// </summary>
        /// <param name="fileName"></param>
        public void DumpOpen(string fileName)
        {
            if (DumpOpened)
            {
                throw new PcapException("A dump file is already opened");
            }
            m_pcapDumpHandle = SafeNativeMethods.pcap_dump_open(PcapHandle, fileName);
            if (!DumpOpened)
                throw new PcapException("Error openning dump file.");
        }

        /// <summary>
        /// Closes the opened dump file
        /// </summary>
        public void DumpClose()
        {
            if (DumpOpened)
            {
                SafeNativeMethods.pcap_dump_close(m_pcapDumpHandle);
                m_pcapDumpHandle = IntPtr.Zero;
            }
        }

        /// <summary>
        /// Flushes all write buffers of the opened dump file
        /// </summary>
        public void DumpFlush()
        {
            if (DumpOpened)
            {
                int result = SafeNativeMethods.pcap_dump_flush(m_pcapDumpHandle);
                if (result < 0)
                    throw new PcapException("Error writing buffer to dumpfile. " + LastError);
            }
        }

        /// <summary>
        /// Writes a packet to the pcap dump file associated with this device.
        /// </summary>
        /// <param name="p">The packet to write</param>
        public void Dump(byte[] p, PcapHeader h)
        {
            if (!Opened)
                throw new PcapDeviceNotReadyException("Cannot dump packet, device is not opened");
            if (!DumpOpened)
                throw new PcapDeviceNotReadyException("Cannot dump packet, dump file is not opened");

            //Marshal packet
            IntPtr pktPtr;
            pktPtr = Marshal.AllocHGlobal(p.Length);
            Marshal.Copy(p, 0, pktPtr, p.Length);

            //Marshal header
            IntPtr hdrPtr = h.MarshalToIntPtr();

            SafeNativeMethods.pcap_dump(m_pcapDumpHandle, hdrPtr, pktPtr);

            Marshal.FreeHGlobal(pktPtr);
            Marshal.FreeHGlobal(hdrPtr);
        }

        /// <summary>
        /// Writes a packet to the pcap dump file associated with this device.
        /// </summary>
        /// <param name="p">The packet to write</param>
        public void Dump(byte[] p)
        {
            Dump(p, new PcapHeader(0, 0, (uint)p.Length, (uint)p.Length));
        }

        /// <summary>
        /// Writes a packet to the pcap dump file associated with this device.
        /// </summary>
        /// <param name="p">The packet to write</param>
        public void Dump(Packet p)
        {
            Dump(p.Bytes, p.PcapHeader);
        }

        /// <summary>
        /// Gets a value indicating wether pcap dump file is already associated with this device
        /// </summary>
        public bool DumpOpened
        {
            get { return m_pcapDumpHandle != IntPtr.Zero; }
        }

        /// <summary>
        /// Sends a raw packet throgh this device
        /// </summary>
        /// <param name="p">The packet to send</param>
        public void SendPacket(Packet p)
        {
            SendPacket(p.Bytes);
        }


        /// <summary>
        /// Sends a raw packet throgh this device
        /// </summary>
        /// <param name="p">The packet to send</param>
        /// <param name="size">The number of bytes to send</param>
        public void SendPacket(Packet p, int size)
        {
            SendPacket(p.Bytes, size);
        }

        /// <summary>
        /// Sends a raw packet throgh this device
        /// </summary>
        /// <param name="p">The packet bytes to send</param>
        public void SendPacket(byte[] p)
        {
            SendPacket(p, p.Length);
        }

        /// <summary>
        /// Sends a raw packet throgh this device
        /// </summary>
        /// <param name="p">The packet bytes to send</param>
        /// <param name="size">The number of bytes to send</param>
        public void SendPacket(byte[] p, int size)
        {
            if (Opened)
            {
                if (size > p.Length)
                {
                    throw new ArgumentException("Invalid packet size value: " + size +
                    "\nArgument size is larger than the total size of the packet.");
                }

                if (p.Length > Pcap.MAX_PACKET_SIZE)
                {
                    throw new ArgumentException("Packet length can't be larger than " + Pcap.MAX_PACKET_SIZE);
                }

                IntPtr p_packet = IntPtr.Zero;
                p_packet = Marshal.AllocHGlobal(size);
                Marshal.Copy(p, 0, p_packet, size);

                int res = SafeNativeMethods.pcap_sendpacket(PcapHandle, p_packet, size);
                Marshal.FreeHGlobal(p_packet);
                if (res < 0)
                {
                    throw new PcapException("Can't send packet: " + LastError);
                }
            }
            else
            {
                throw new PcapDeviceNotReadyException("Can't send packet, the device is closed");
            }
        }

        /// <summary>
        /// Sends all packets in a 'PcapSendQueue' out this pcap device
        /// </summary>
        /// <param name="q">The 'PcapSendQueue' hodling the packets</param>
        public int SendQueue(PcapSendQueue q, bool sync)
        {
            return q.Transmit(this, sync);
        }

        /// <summary>
        /// Retrieves pcap statistics
        /// </summary>
        /// <returns>
        /// A <see cref="PcapStatistics"/>
        /// </returns>
        public virtual PcapStatistics Statistics()
        {
            // can only call PcapStatistics on an open device
            if (!Opened)
                throw new PcapDeviceNotReadyException("device not open");

            return new PcapStatistics(this.m_pcapAdapterHandle);
        }

        /// <value>
        /// Set the kernel value buffer size in bytes
        /// WinPcap extension
        /// </value>
        public int KernelBufferSize
        {
            set
            {
                // setting the kernel buffer size is a WinPcap extension
                if ((Environment.OSVersion.Platform != PlatformID.Win32NT) &&
                   (Environment.OSVersion.Platform != PlatformID.Win32Windows))
                {
                    throw new System.InvalidOperationException("only supported in winpcap");
                }

                // make sure the device is open
                if (!Opened)
                {
                    throw new PcapDeviceNotReadyException
                        ("Can't set PcapMode, the device is not opened");
                }

                int retval = SafeNativeMethods.pcap_setbuff(this.m_pcapAdapterHandle,
                                                            value);
                if (retval != 0)
                {
                    throw new System.InvalidOperationException("pcap_setbuff() failed");
                }
            }
        }

        /// <summary>
        /// Retrieve the last error string for a given pcap_t* device
        /// </summary>
        /// <param name="deviceHandle">
        /// A <see cref="IntPtr"/>
        /// </param>
        /// <returns>
        /// A <see cref="System.String"/>
        /// </returns>
        internal static string GetLastError(IntPtr deviceHandle)
        {
            IntPtr err_ptr = SafeNativeMethods.pcap_geterr(deviceHandle);
            return Marshal.PtrToStringAnsi(err_ptr);
        }

        /// <summary>
        /// The last pcap error associated with this pcap device
        /// </summary>
        public string LastError
        {
            get { return GetLastError(PcapHandle); }
        }

        public override string ToString()
        {
            return "Interface: " + m_pcapIf.ToString() + "\n";
        }
    }
}

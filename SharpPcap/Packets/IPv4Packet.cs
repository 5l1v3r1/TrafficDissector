// The file has been modified by Victor Alexiev (some properties added)
using System;
using SharpPcap.Packets.Util;

namespace SharpPcap.Packets
{
    public class IPv4Packet : EthernetPacket
    {
        public const int HeaderMinimumLength = 20;

        /// <summary> Type of service code constants for IP. Type of service describes 
        /// how a packet should be handled.
        /// <p>
        /// TOS is an 8-bit record in an IP header which contains a 3-bit 
        /// precendence field, 4 TOS bit fields and a 0 bit.
        /// </p>
        /// <p>
        /// The following constants are bit masks which can be logically and'ed
        /// with the 8-bit IP TOS field to determine what type of service is set.
        /// </p>
        /// <p>
        /// Taken from TCP/IP Illustrated V1 by Richard Stevens, p34.
        /// </p>
        /// </summary>
        public struct TypesOfService_Fields
        {
            public readonly static int MINIMIZE_DELAY = 0x10;
            public readonly static int MAXIMIZE_THROUGHPUT = 0x08;
            public readonly static int MAXIMIZE_RELIABILITY = 0x04;
            public readonly static int MINIMIZE_MONETARY_COST = 0x02;
            public readonly static int UNUSED = 0x01;
        }

        public struct Flags_Fields
        {
            public readonly static int RESERVED = 0x04;
            public readonly static int DONT_FRAGMENT = 0x02;
            public readonly static int MORE_FRAGMENTS = 0x041;
        }

        public static int ipVersion = 4;

        /// <summary>
        ///  should be overriden by upper classes
        /// </summary>
        public override void OnOffsetChanged()
        {
            base.OnOffsetChanged();
            _ipOffset = _ethPayloadOffset + IPHeaderLength;
        }

        /// <summary> Get the IP version code.</summary>
        public virtual int Version
        {
            get
            {
                return IPVersion;
            }
            set
            {
                IPVersion = value;
            }
        }

        /// <summary> Get the IP version code.</summary>
        virtual public int IPVersion
        {
            get
            {
                return (ArrayHelper.extractInteger(Bytes,
                                                   _ethPayloadOffset + IPv4Fields_Fields.IP_VER_POS,
                                                   IPv4Fields_Fields.IP_VER_LEN) >> 4) & 0xF;
            }

            set
            {
                Bytes[_ethPayloadOffset + IPv4Fields_Fields.IP_VER_POS] &= (byte)(0x0F);
                Bytes[_ethPayloadOffset + IPv4Fields_Fields.IP_VER_POS] |= (byte)(((value << 4) & 0xF0));
            }

        }

        /// <summary> Fetch the IP header length in bytes. </summary>
        /// <summary> Sets the IP header length field.  At most, this can be a 
        /// four-bit value.  The high order bits beyond the fourth bit
        /// will be ignored.
        /// 
        /// </summary>
        virtual public int IPHeaderLength
        {
            get
            {
                return (ArrayHelper.extractInteger(Bytes,
                                                   _ethPayloadOffset + IPv4Fields_Fields.IP_VER_POS,
                                                   IPv4Fields_Fields.IP_VER_LEN) & 0xF) * 4;
            }

            set
            {
                value /= 4;
                // Clear low order bits and then set
                Bytes[_ethPayloadOffset + IPv4Fields_Fields.IP_VER_POS] &= (byte)(0xF0);
                Bytes[_ethPayloadOffset + IPv4Fields_Fields.IP_VER_POS] |= (byte)(value & 0x0F);
                // set offset into _bytes of previous layers
                _ipOffset = _ethPayloadOffset + IPHeaderLength;
            }
        }

        /// <summary> Fetch the packet IP header length.</summary>
        override public int HeaderLength
        {
            get
            {
                return IPHeaderLength;
            }
        }

        /// <summary> Fetch the unique ID of this IP datagram. The ID normally 
        /// increments by one each time a datagram is sent by a host.
        /// </summary>
        /// <summary> Sets the IP identification header value.
        /// 
        /// </summary>
        virtual public int Id
        {
            get
            {
                return ArrayHelper.extractInteger(Bytes,
                                                  _ethPayloadOffset + IPv4Fields_Fields.IP_ID_POS,
                                                  IPv4Fields_Fields.IP_ID_LEN);
            }

            set
            {
                ArrayHelper.insertLong(Bytes, value,
                                       _ethPayloadOffset + IPv4Fields_Fields.IP_ID_POS,
                                       IPv4Fields_Fields.IP_ID_LEN);
            }

        }

        /// <summary> Fetch fragment offset.</summary>
        /// <summary> Sets the fragment offset header value.  The offset specifies a
        /// number of octets (i.e., bytes).
        /// 
        /// </summary>
        virtual public int FragmentOffset
        {
            get
            {
                return ArrayHelper.extractInteger(Bytes, _ethPayloadOffset + IPv4Fields_Fields.IP_FRAG_POS, IPv4Fields_Fields.IP_FRAG_LEN) & 0x1FFF;
            }

            set
            {
                Bytes[_ethPayloadOffset + IPv4Fields_Fields.IP_FRAG_POS] &= (byte)(0xE0);
                Bytes[_ethPayloadOffset + IPv4Fields_Fields.IP_FRAG_POS] |= (byte)(((value >> 8) & 0x1F));
                Bytes[_ethPayloadOffset + IPv4Fields_Fields.IP_FRAG_POS + 1] = (byte)(value & 0xFF);
            }

        }

        /// <summary> Fetch the IP address of the host where the packet originated from.</summary>
        virtual public System.Net.IPAddress SourceAddress
        {
            get
            {
                return IPPacket.GetIPAddress(System.Net.Sockets.AddressFamily.InterNetwork,
                                             _ethPayloadOffset + IPv4Fields_Fields.IP_SRC_POS, Bytes);
            }

            set
            {
                byte[] address = value.GetAddressBytes();
                System.Array.Copy(address, 0, Bytes, _ethPayloadOffset + IPv4Fields_Fields.IP_SRC_POS, address.Length);
            }
        }

        /// <summary> Fetch the IP address of the host where the packet is destined.</summary>
        virtual public System.Net.IPAddress DestinationAddress
        {
            get
            {
                return IPPacket.GetIPAddress(System.Net.Sockets.AddressFamily.InterNetwork,
                                             _ethPayloadOffset + IPv4Fields_Fields.IP_DST_POS,
                                             Bytes);
            }

            set
            {
                byte[] address = value.GetAddressBytes();
                System.Array.Copy(address, 0, Bytes, _ethPayloadOffset + IPv4Fields_Fields.IP_DST_POS, address.Length);
            }
        }

        /// <summary> Fetch the IP header a byte array.</summary>
        virtual public byte[] IPHeader
        {
            get
            {
                return PacketEncoding.extractHeader(_ethPayloadOffset, IPHeaderLength, Bytes);
            }
            set // NB! added by Victor Alexiev
            {
                Array.Copy(value, 0, Bytes, _ethPayloadOffset, IPHeaderLength);
            }
        }

        /// <summary> Fetch the IP header as a byte array.</summary>
        override public byte[] Header
        {
            get
            {
                return IPHeader;
            }

        }

        /// <summary> Fetch the IP data as a byte array.</summary>
        virtual public byte[] IPData
        {
            get
            {
                return PacketEncoding.extractData(_ethPayloadOffset, IPHeaderLength, Bytes, IPPayloadLength);
            }

            set
            {
                // retrieve the current payload length
                int currentIPPayloadLength = IPPayloadLength;

                // compute the difference between the current and the
                // requested payload lengths
                int changeInLength = value.Length - currentIPPayloadLength;

                // create a new buffer for the entire packet
                byte[] newByteArray = new Byte[Bytes.Length + changeInLength];

                // copy the old contents over to the new buffer
                Array.Copy(Bytes, newByteArray, Bytes.Length - currentIPPayloadLength);

                // copy the new payload into place
                Array.Copy(value, 0,
                           newByteArray, Bytes.Length - currentIPPayloadLength,
                           value.Length);

                // update the Bytes to the new byte array
                Bytes = newByteArray;

                // update the IP length
                IPPayloadLength = value.Length;
            }
        }

        /// <summary> Fetch the header checksum.</summary>
        virtual public int IPChecksum
        {
            get
            {
                return ArrayHelper.extractInteger(Bytes, _ethPayloadOffset + IPv4Fields_Fields.IP_CSUM_POS, IPv4Fields_Fields.IP_CSUM_LEN);
            }

            set
            {
                SetChecksum(value, _ethPayloadOffset + IPv4Fields_Fields.IP_CSUM_POS);
            }

        }

        /// <summary> Check if the IP packet is valid, checksum-wise.</summary>
        virtual public bool ValidChecksum
        {
            get
            {
                return ValidIPChecksum;
            }

        }

        /// <summary> Check if the IP packet is valid, checksum-wise.</summary>
        virtual public bool ValidIPChecksum
        {
            get
            {
                // first validate other information about the packet. if this stuff
                // is not true, the packet (and therefore the checksum) is invalid
                // - ip_hl >= 5 (ip_hl is the length in 4-byte words)
                if (IPHeaderLength < IPv4Fields_Fields.IP_HEADER_LEN)
                {
                    return false;
                }
                else
                {
                    return (ChecksumUtils.OnesSum(Bytes, _ethPayloadOffset, IPHeaderLength) == 0xFFFF);
                }
            }

        }

        /// <summary> Fetch ascii escape sequence of the color associated with this packet type.</summary>
        override public System.String Color
        {
            get
            {
                return AnsiEscapeSequences_Fields.WHITE;
            }

        }
        // offset from beginning of byte array where IP header ends (i.e.,
        //  size of ethernet frame header and IP header
        protected internal int _ipOffset;

        /// <summary> Create a new IP packet. </summary>
        public IPv4Packet(int lLen, byte[] bytes)
            : base(lLen, bytes)
        {
            _ipOffset = _ethPayloadOffset + IPHeaderLength;
        }

        /// <summary> Create a new IP packet.</summary>
        public IPv4Packet(int lLen, byte[] bytes, Timeval tv)
            : this(lLen, bytes)
        {
            this._timeval = tv;
        }

        /// <summary> Fetch the type of service.</summary>
        public virtual int TypeOfService
        {
            get
            {
                return ArrayHelper.extractInteger(Bytes,
                                                  _ethPayloadOffset + IPv4Fields_Fields.IP_TOS_POS,
                                                  IPv4Fields_Fields.IP_TOS_LEN);
            }
            set
            {
                Bytes[_ethPayloadOffset + IPv4Fields_Fields.IP_TOS_POS] = (byte)(value & 0xFF);
            }
        }

        public virtual int Precedence
        {
            get
            {
                return (TypeOfService & 0xE0) >> 5;
            }
        }

        public virtual bool MinimizeDelay
        {
            get
            {
                return (TypeOfService & TypesOfService_Fields.MINIMIZE_DELAY) != 0;
            }
        }

        public virtual bool MaximizeThroughput
        {
            get
            {
                return (TypeOfService & TypesOfService_Fields.MAXIMIZE_THROUGHPUT) != 0;
            }
        }

        public virtual bool MaximizeReliability
        {
            get
            {
                return (TypeOfService & TypesOfService_Fields.MAXIMIZE_RELIABILITY) != 0;
            }
        }

        public virtual bool MinimizeMonetaryCost
        {
            get
            {
                return (TypeOfService & TypesOfService_Fields.MINIMIZE_MONETARY_COST) != 0;
            }
        }

        /// <summary> Fetch the IP length in bytes.</summary>
        public virtual int Length
        {
            get
            {
                return IPTotalLength;
            }
            set
            {
                IPTotalLength = value;
            }
        }

        /// <summary> Fetch the IP length in bytes.</summary>
        public virtual int IPTotalLength
        {
            get
            {
                return ArrayHelper.extractInteger(Bytes,
                                                  _ethPayloadOffset + IPv4Fields_Fields.IP_LEN_POS,
                                                  IPv4Fields_Fields.IP_LEN_LEN);
            }
            set
            {
                ArrayHelper.insertLong(Bytes, value,
                                       _ethPayloadOffset + IPv4Fields_Fields.IP_LEN_POS,
                                       IPv4Fields_Fields.IP_LEN_LEN);
            }
        }

        public virtual int IPPayloadLength
        {
            get
            {
                return IPTotalLength - IPv4Fields_Fields.IP_HEADER_LEN;
            }

            set
            {
                IPTotalLength = value + IPv4Fields_Fields.IP_HEADER_LEN;
            }
        }

        /// <summary> Fetch fragmentation flags.</summary>
        public virtual int FragmentFlags
        {
            get
            {
                // fragment flags are the high 3 bits
                return (ArrayHelper.extractInteger(Bytes, _ethPayloadOffset + IPv4Fields_Fields.IP_FRAG_POS, IPv4Fields_Fields.IP_FRAG_LEN) >> 13) & 0x7;
            }
            set
            {
                Bytes[_ethPayloadOffset + IPv4Fields_Fields.IP_FRAG_POS] &= (byte)(0x1F);
                Bytes[_ethPayloadOffset + IPv4Fields_Fields.IP_FRAG_POS] |= (byte)(((value << 5) & 0xE0));

            }
        }

        public virtual bool ReservedFlag
        {
            get
            {
                return (FragmentFlags & Flags_Fields.RESERVED) != 0;
            }
        }

        public virtual bool DontFragment
        {
            get
            {
                return (FragmentFlags & Flags_Fields.DONT_FRAGMENT) != 0;
            }
        }

        public virtual bool MoreFragments
        {
            get
            {
                return (FragmentFlags & Flags_Fields.MORE_FRAGMENTS) != 0;
            }
        }

        /// <summary> Fetch the time to live. TTL sets the upper limit on the number of 
        /// routers through which this IP datagram is allowed to pass.
        /// Originally intended to be the number of seconds the packet lives it is now decremented
        /// by one each time a router passes the packet on
        /// 
        /// 8-bit value
        /// </summary>
        public virtual int TimeToLive
        {
            get
            {
                return ArrayHelper.extractInteger(Bytes, _ethPayloadOffset + IPv4Fields_Fields.IP_TTL_POS,
                                                  IPv4Fields_Fields.IP_TTL_LEN);
            }
            set
            {
                Bytes[_ethPayloadOffset + IPv4Fields_Fields.IP_TTL_POS] = (byte)value;
            }
        }

        /// <summary> Fetch the code indicating the type of protocol embedded in the IP</summary>
        public virtual IPProtocol.IPProtocolType IPProtocol
        {
            get
            {
                return (IPProtocol.IPProtocolType)ArrayHelper.extractInteger(Bytes,
                                                                             _ethPayloadOffset + IPv4Fields_Fields.IP_CODE_POS,
                                                                             IPv4Fields_Fields.IP_CODE_LEN);
            }
            set
            {
                Bytes[_ethPayloadOffset + IPv4Fields_Fields.IP_CODE_POS] = (byte)value;
            }
        }

        /// <summary> Fetch the IP data as a byte array.</summary>
        public override byte[] Data
        {
            get
            {
                return IPData;
            }
        }

        /// <summary> Fetch the IP header checksum.</summary>
        public virtual int Checksum
        {
            get
            {
                return IPChecksum;
            }
            set
            {
                IPChecksum = value;
            }
        }

        /// <summary> Sets the IP header checksum.</summary>
        protected internal virtual void SetChecksum(int cs, int checkSumOffset)
        {
            ArrayHelper.insertLong(Bytes, cs, checkSumOffset, 2);
        }

        protected internal virtual void SetTransportLayerChecksum(int cs, int csPos)
        {
            SetChecksum(cs, _ipOffset + csPos);
        }

        /// <summary> Computes the IP checksum, optionally updating the IP checksum header.
        /// 
        /// </summary>
        /// <param name="update">Specifies whether or not to update the IP checksum
        /// header after computing the checksum.  A value of true indicates
        /// the header should be updated, a value of false indicates it
        /// should not be updated.
        /// </param>
        /// <returns> The computed IP checksum.
        /// </returns>
        public int ComputeIPChecksum(bool update)
        {
            //copy the ip header
            byte[] ip = ArrayHelper.copy(Bytes, _ethPayloadOffset, IPHeaderLength);
            //reset the checksum field (checksum is calculated when this field is zeroed)
            ArrayHelper.insertLong(ip, 0, IPv4Fields_Fields.IP_CSUM_POS, 2);
            //compute the one's complement sum of the ip header
            int cs = ChecksumUtils.OnesComplementSum(ip, 0, ip.Length);
            if (update)
            {
                IPChecksum = cs;
            }

            return cs;
        }

        /// <summary> Same as <code>computeIPChecksum(true);</code>
        /// 
        /// </summary>
        /// <returns> The computed IP checksum value.
        /// </returns>
        public int ComputeIPChecksum()
        {
            return ComputeIPChecksum(true);
        }

        // Prepend to the given byte[] origHeader the portion of the IPv6 header used for
        // generating an tcp checksum
        //
        // http://en.wikipedia.org/wiki/Transmission_Control_Protocol#TCP_checksum_using_IPv4
        // http://tools.ietf.org/html/rfc793
        protected internal virtual byte[] AttachPseudoIPHeader(byte[] origHeader)
        {
            bool odd = origHeader.Length % 2 != 0;
            int numberOfBytesFromIPHeaderUsedToGenerateChecksum = 12;
            int headerSize = numberOfBytesFromIPHeaderUsedToGenerateChecksum + origHeader.Length;
            if (odd)
                headerSize++;

            byte[] headerForChecksum = new byte[headerSize];
            // 0-7: ip src+dest addr
            Array.Copy(Bytes, _ethPayloadOffset + IPv4Fields_Fields.IP_SRC_POS, headerForChecksum, 0, 8);
            // 8: always zero
            headerForChecksum[8] = 0;
            // 9: ip protocol
            headerForChecksum[9] = (byte)IPProtocol;
            // 10-11: header+data length
            ArrayHelper.insertLong(headerForChecksum, origHeader.Length, 10, 2);

            // prefix the pseudoHeader to the header+data
            Array.Copy(origHeader,
                       0, headerForChecksum,
                       numberOfBytesFromIPHeaderUsedToGenerateChecksum, origHeader.Length);

            //if not even length, pad with a zero
            if (odd)
                headerForChecksum[headerForChecksum.Length - 1] = 0;

            return headerForChecksum;
        }

        public override bool IsValid(out string errorString)
        {
            errorString = string.Empty;

            // validate the base class(es)
            bool baseValid = base.IsValid(out errorString);

            // perform some quick validation
            if (IPTotalLength < IPHeaderLength)
            {
                errorString += string.Format("IPTotalLength {0} < IPHeaderLength {1}",
                                            IPTotalLength, IPHeaderLength);
                return false;
            }

            return baseValid;
        }

        public virtual bool IsValidTransportLayerChecksum(bool pseudoIPHeader)
        {
            byte[] upperLayer = IPData;
            if (pseudoIPHeader)
                upperLayer = AttachPseudoIPHeader(upperLayer);
            int onesSum = ChecksumUtils.OnesSum(upperLayer);
            return (onesSum == 0xFFFF);
        }

        /// <summary> Convert this IP packet to a readable string.</summary>
        public override System.String ToString()
        {
            return ToColoredString(false);
        }

        /// <summary> Generate string with contents describing this IP packet.</summary>
        /// <param name="colored">whether or not the string should contain ansi
        /// color escape sequences.
        /// </param>
        public override System.String ToColoredString(bool colored)
        {
            System.Text.StringBuilder buffer = new System.Text.StringBuilder();
            buffer.Append('[');
            if (colored)
                buffer.Append(Color);
            buffer.Append("IPv4Packet");
            if (colored)
                buffer.Append(AnsiEscapeSequences_Fields.RESET);
            buffer.Append(": ");
            buffer.Append(SourceAddress + " -> " + DestinationAddress);
            buffer.Append(" proto=" + IPProtocol);
            buffer.Append(" l=" + IPHeaderLength + "," + Length);
            buffer.Append(']');

            // append the base class output
            buffer.Append(base.ToColoredString(colored));

            return buffer.ToString();
        }

        /// <summary> Convert this IP packet to a more verbose string.</summary>
        public override System.String ToColoredVerboseString(bool colored)
        {
            System.Text.StringBuilder buffer = new System.Text.StringBuilder();
            buffer.Append('[');
            if (colored)
                buffer.Append(Color);
            buffer.Append("IPv4Packet");
            if (colored)
                buffer.Append(AnsiEscapeSequences_Fields.RESET);
            buffer.Append(": ");
            buffer.Append("version=" + Version + ", ");
            buffer.Append("hlen=" + HeaderLength + ", ");
            buffer.Append("tos=" + TypeOfService + ", ");
            buffer.Append("length=" + Length + ", ");
            buffer.Append("id=" + Id + ", ");
            buffer.Append("flags=0x" + System.Convert.ToString(FragmentFlags, 16) + ", ");
            buffer.Append("offset=" + FragmentOffset + ", ");
            buffer.Append("ttl=" + TimeToLive + ", ");
            buffer.Append("proto=" + IPProtocol + ", ");
            buffer.Append("sum=0x" + System.Convert.ToString(Checksum, 16));
            if (this.ValidChecksum)
                buffer.Append(" (correct), ");
            else
                buffer.Append(" (incorrect, should be " + ComputeIPChecksum(false) + "), ");
            buffer.Append("src=" + SourceAddress + ", ");
            buffer.Append("dest=" + DestinationAddress);
            buffer.Append(']');

            // append the base class output
            buffer.Append(base.ToColoredVerboseString(colored));

            return buffer.ToString();
        }

        /// <summary> This inner class provides access to private methods for unit testing.</summary>
        public class TestProbe
        {
            public TestProbe(IPv4Packet enclosingInstance)
            {
                InitBlock(enclosingInstance);
            }

            private void InitBlock(IPv4Packet enclosingInstance)
            {
                this.enclosingInstance = enclosingInstance;
            }

            private IPv4Packet enclosingInstance;
            virtual public int ComputedReceiverIPChecksum
            {
                get
                {
                    return ChecksumUtils.OnesSum(Enclosing_Instance.Bytes,
                                                 Enclosing_Instance._ethPayloadOffset,
                                                 Enclosing_Instance.IPHeaderLength);
                }
            }

            virtual public int ComputedSenderIPChecksum()
            {
                return Enclosing_Instance.ComputeIPChecksum(false);
            }

            public IPv4Packet Enclosing_Instance
            {
                get
                {
                    return enclosingInstance;
                }
            }
        }
    }
}

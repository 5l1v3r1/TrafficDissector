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
 */

using System;
using System.Text;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using SharpPcap.Containers;

namespace SharpPcap
{
    /// <summary>
    /// List of available Pcap Interfaces.
    /// </summary>
    public class PcapDeviceList : ReadOnlyCollection<PcapDevice>
    {
        private static PcapDeviceList instance;
        public static PcapDeviceList Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new PcapDeviceList();
                }

                return instance;
            }
        }

        /// <summary>
        /// Caution: Use the singlton instance unless you know why you need to call this.
        /// One use is for multiple filters on the same physical device. The work around
        /// is to simply open the same physical device multiple times, one for each
        /// filter by calling this routine and picking the same device out of each
        /// PcapDeviceList.
        /// </summary>
        /// <returns>
        /// A <see cref="PcapDeviceList"/>
        /// </returns>
        public static PcapDeviceList New()
        {
            return new PcapDeviceList();
        }

        /// <summary>
        /// Represents a strongly typed, read-only list of PcapDevices.
        /// </summary>
        private PcapDeviceList()
            : base(new List<PcapDevice>())
        {
            Refresh();
        }

        /// <summary>
        /// Retrieve a list of the current PcapDevices
        /// </summary>
        private static List<PcapDevice> GetDevices()
        {
            List<PcapDevice> deviceList = new List<PcapDevice>();

            IntPtr devicePtr = IntPtr.Zero;
            StringBuilder errorBuffer = new StringBuilder(256);

            int result = SafeNativeMethods.pcap_findalldevs(ref devicePtr, errorBuffer);
            if (result < 0)
                throw new PcapException(errorBuffer.ToString());

            IntPtr nextDevPtr = devicePtr;

            while (nextDevPtr != IntPtr.Zero)
            {
                // Marshal pointer into a struct
                PcapUnmanagedStructures.pcap_if pcap_if_unmanaged =
                    (PcapUnmanagedStructures.pcap_if)Marshal.PtrToStructure(nextDevPtr,
                                                    typeof(PcapUnmanagedStructures.pcap_if));
                PcapInterface pcap_if = new PcapInterface(pcap_if_unmanaged);
                deviceList.Add(new PcapDevice(pcap_if));
                nextDevPtr = pcap_if_unmanaged.Next;
            }
            SafeNativeMethods.pcap_freealldevs(devicePtr);  // Free unmanaged memory allocation.

            // go through the network interfaces to populate the mac address
            // for each of the devices
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            foreach (PcapDevice device in deviceList)
            {
                foreach (NetworkInterface adapter in nics)
                {
                    // if the name and id match then we have found the NetworkInterface
                    // that matches the PcapDevice
                    if (device.Name.EndsWith(adapter.Id))
                    {
                        device.Interface.MacAddress = adapter.GetPhysicalAddress();
                        device.Interface.FriendlyName = adapter.Name;
                    }
                }
            }

            return deviceList;
        }

        /// <summary>
        /// Refresh the device list
        /// </summary>
        public void Refresh()
        {
            lock (this)
            {
                // retrieve the current device list
                List<PcapDevice> newDeviceList = GetDevices();

                // update existing devices with values in the new list
                foreach (PcapDevice newItem in newDeviceList)
                {
                    foreach (PcapDevice existingItem in base.Items)
                    {
                        if (newItem.Name == existingItem.Name)
                        {
                            // copy the flags and addresses over
                            existingItem.Interface.Flags = newItem.Interface.Flags;
                            existingItem.Interface.Addresses = newItem.Interface.Addresses;

                            break; // break out of the foreach(existingItem)
                        }
                    }
                }

                // find items the current list is missing
                foreach (PcapDevice newItem in newDeviceList)
                {
                    bool found = false;
                    foreach (PcapDevice existingItem in base.Items)
                    {
                        if (existingItem.Name == newItem.Name)
                        {
                            found = true;
                            break;
                        }
                    }

                    // add items that we were missing
                    if (!found)
                    {
                        base.Items.Add(newItem);
                    }
                }

                // find items that we have that the current list is missing
                List<PcapDevice> itemsToRemove = new List<PcapDevice>();
                foreach (PcapDevice existingItem in base.Items)
                {
                    bool found = false;

                    foreach (PcapDevice newItem in newDeviceList)
                    {
                        if (existingItem.Name == newItem.Name)
                        {
                            found = true;
                            break;
                        }
                    }

                    // add the PcapDevice we didn't see in the new list
                    if (!found)
                    {
                        itemsToRemove.Add(existingItem);
                    }
                }

                // remove the items outside of the foreach() to avoid
                // enumeration errors
                foreach (PcapDevice itemToRemove in itemsToRemove)
                {
                    base.Items.Remove(itemToRemove);
                }
            }
        }

        #region PcapDevice Indexers
        /// <param name="Name">The name or description of the pcap interface to get.</param>
        public PcapDevice this[string Name]
        {
            get
            {
                // lock to prevent issues with multi-threaded access
                // with other methods
                lock (this)
                {
                    List<PcapDevice> devices = (List<PcapDevice>)base.Items;
                    PcapDevice dev = devices.Find(delegate(PcapDevice i) { return i.Name == Name; });
                    PcapDevice result = dev ?? devices.Find(delegate(PcapDevice i) { return i.Description == Name; });

                    if (result == null)
                        throw new IndexOutOfRangeException();
                    return result;
                }
            }
        }
        #endregion
    }
}

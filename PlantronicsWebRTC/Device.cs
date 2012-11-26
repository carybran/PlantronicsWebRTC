using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;

namespace Plantronics.UC.WebRTCDemo
{
    public class Device : PlantronicsObject
    {
        private String id;
        public String Id
        {
            get { return id; }
            set { id = value; }
        }
        private String vendorId;

        public String VendorId
        {
            get { return vendorId; }
            set { vendorId = value; }
        }
        private String productId;

        public String ProductId
        {
            get { return productId; }
            set { productId = value; }
        }
        private int versionNumber;
        public int VersionNumber
        {
            get { return versionNumber; }
            set { versionNumber = value; }
        }
        private int sampleRate;
        public int SampleRate
        {
            get { return sampleRate; }
            set { sampleRate = value; }
        }

        private int numberOfChannels;
        public int NumberOfChannels
        {
            get { return numberOfChannels; }
            set { numberOfChannels = value; }
        }
        private String internalName;

        public String InternalName
        {
            get { return internalName; }
            set { internalName = value; }
        }
        private String productName;

        public String ProductName
        {
            get { return productName; }
            set { productName = value; }
        }
        private String manufacturerName;

        public String ManufacturerName
        {
            get { return manufacturerName; }
            set { manufacturerName = value; }
        }

        public Hashtable GetObjectAsHashtable()
        {
            Hashtable messageAsHash = new Hashtable();
            messageAsHash.Add("id", id);
            messageAsHash.Add("vendorId", vendorId);
            messageAsHash.Add("productId", productId);
            messageAsHash.Add("versionNumber", versionNumber);
            messageAsHash.Add("internalName", internalName);
            messageAsHash.Add("productName", ProductName);
            messageAsHash.Add("manufacturerName", manufacturerName);
            messageAsHash.Add("numberOfChannels", numberOfChannels);
            messageAsHash.Add("sampleRate", sampleRate);

            return messageAsHash;

        }


    }
}

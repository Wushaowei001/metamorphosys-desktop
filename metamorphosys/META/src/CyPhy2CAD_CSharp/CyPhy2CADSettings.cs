﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace CyPhy2CAD_CSharp
{   
    public class OtherDataFormat
    {
        public bool STLAscii;
        public bool STLBinary;
        public bool Inventor;

        public OtherDataFormat()
        {
            STLAscii = false;
            STLBinary = false;
            Inventor = false;
        }
    }
    

    public class SpecialInstructions
    {
        public bool LeafAssembliesMetric;

        public SpecialInstructions()
        {
            LeafAssembliesMetric = false;
        }
    }

    [Serializable]
    [ComVisible(true)]
    [ProgId("ISIS.META.CyPhy2CADSettings")]
    [Guid("BA1F52BD-F9B3-4614-8C66-982834D266F2")]
    public class CyPhy2CADSettings : CyPhyGUIs.IInterpreterConfiguration
    {
        public const string ConfigFilename = "CyPhy2CAD_config.xml";
        public string OutputDirectory { get; set; }
        public string AuxiliaryDirectory { get; set; }
        public List<string> StepFormats { get; set; }
        public OtherDataFormat OtherDataFormat { get; set; }
        public SpecialInstructions SpecialInstructions { get; set; }
        public bool PrepIFab { get; set; }
        public bool MetaLink { get; set; }

        public CyPhy2CADSettings()
        {
            OtherDataFormat = new OtherDataFormat();
            SpecialInstructions = new SpecialInstructions();
            StepFormats = new List<string>();
            PrepIFab = false;
            MetaLink = false;
        }
    }
}

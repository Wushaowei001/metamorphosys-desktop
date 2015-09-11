﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CyPhy2Schematic
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Runtime.InteropServices;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    [Serializable]
    [ComVisible(true)]
    [ProgId("ISIS.META.CyPhy2Schematic_Settings")]
    [Guid("91D39BEB-0026-431D-8315-E600F0D84FAA")]

    public class CyPhy2Schematic_Settings : CyPhyGUIs.IInterpreterConfiguration
    {
        public const string ConfigFilename = "CyPhy2Schematic_config.xml";
        
        public CyPhy2Schematic_Settings()
        {
            this.Verbose = false;
            this.doChipFit = null;
            this.doPlaceRoute = null;
            this.doPlaceOnly = null;
            this.doSpice = null;
            this.doSpiceForSI = null;
            this.skipGUI = null;
            this.showChipFitVisualizer = null;
        }

        public bool Verbose { get; set; }

        [CyPhyGUIs.WorkflowConfigItem]
        public string doChipFit { get; set; }

        [CyPhyGUIs.WorkflowConfigItem]
        public string doPlaceRoute { get; set; }

        [CyPhyGUIs.WorkflowConfigItem]
        public string doPlaceOnly { get; set; }

        [CyPhyGUIs.WorkflowConfigItem]
        public string doSpice  { get; set; }

        [CyPhyGUIs.WorkflowConfigItem]
        public string doSpiceForSI { get; set; }

        [CyPhyGUIs.WorkflowConfigItem]
        public string showChipFitVisualizer { get; set; }

        [CyPhyGUIs.WorkflowConfigItem]
        [System.Xml.Serialization.XmlIgnore]
        public string skipGUI { get; set; }
    }
}


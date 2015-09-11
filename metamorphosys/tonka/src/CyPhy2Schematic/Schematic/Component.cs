﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using Tonka = ISIS.GME.Dsml.CyPhyML.Interfaces;
using TonkaClasses = ISIS.GME.Dsml.CyPhyML.Classes;

namespace CyPhy2Schematic.Schematic
{
    public class Component : ModelBase<Tonka.ComponentType>
    {
        public Component(Tonka.ComponentType impl)
            : base(impl)
        {
            Parameters = new SortedSet<Parameter>();
            Ports = new List<Port>();
            string iname = Regex.Replace(impl.Name, "[ ]", "_");
            string name = iname;
            int partCount = 1;
            if (CodeGenerator.partNames.ContainsKey(name))
            {
                partCount = CodeGenerator.partNames[name];
                name = String.Format("{0}${1}", name, partCount++);
            }
            CodeGenerator.partNames[iname] = partCount;
            this.Name = name;
        }
        public SortedSet<Parameter> Parameters { get; set; }
        public List<Port> Ports { get; set; }
        public ComponentAssembly Parent { get; set; }
        public Eagle.eagle SchematicLib { get; set; }
        public string SpiceLib { get; set; }
        public void accept(Visitor visitor)
        {
            visitor.visit(this);
            foreach (var port_obj in Ports)
            {
                port_obj.accept(visitor);
            }
            visitor.upVisit(this);
        }
    }
}

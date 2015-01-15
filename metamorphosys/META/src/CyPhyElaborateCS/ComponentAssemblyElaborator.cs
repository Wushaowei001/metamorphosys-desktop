﻿/*
Copyright (C) 2013-2015 MetaMorph Software, Inc

Permission is hereby granted, free of charge, to any person obtaining a
copy of this data, including any software or models in source or binary
form, as well as any drawings, specifications, and documentation
(collectively "the Data"), to deal in the Data without restriction,
including without limitation the rights to use, copy, modify, merge,
publish, distribute, sublicense, and/or sell copies of the Data, and to
permit persons to whom the Data is furnished to do so, subject to the
following conditions:

The above copyright notice and this permission notice shall be included
in all copies or substantial portions of the Data.

THE DATA IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
THE AUTHORS, SPONSORS, DEVELOPERS, CONTRIBUTORS, OR COPYRIGHT HOLDERS BE
LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
WITH THE DATA OR THE USE OR OTHER DEALINGS IN THE DATA.  

=======================
This version of the META tools is a fork of an original version produced
by Vanderbilt University's Institute for Software Integrated Systems (ISIS).
Their license statement:

Copyright (C) 2011-2014 Vanderbilt University

Developed with the sponsorship of the Defense Advanced Research Projects
Agency (DARPA) and delivered to the U.S. Government with Unlimited Rights
as defined in DFARS 252.227-7013.

Permission is hereby granted, free of charge, to any person obtaining a
copy of this data, including any software or models in source or binary
form, as well as any drawings, specifications, and documentation
(collectively "the Data"), to deal in the Data without restriction,
including without limitation the rights to use, copy, modify, merge,
publish, distribute, sublicense, and/or sell copies of the Data, and to
permit persons to whom the Data is furnished to do so, subject to the
following conditions:

The above copyright notice and this permission notice shall be included
in all copies or substantial portions of the Data.

THE DATA IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
THE AUTHORS, SPONSORS, DEVELOPERS, CONTRIBUTORS, OR COPYRIGHT HOLDERS BE
LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
WITH THE DATA OR THE USE OR OTHER DEALINGS IN THE DATA.  
*/

namespace CyPhyElaborateCS
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using GME.MGA;

    /// <summary>
    /// Elaborator for a Component Assembly context.
    /// </summary>
    public class ComponentAssemblyElaborator : Elaborator
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ComponentAssemblyElaborator"/> class with a subject
        /// object on which the elaboration will be performed.
        /// </summary>
        /// <param name="subject">Component Assembly object.</param>
        /// <exception cref="ArgumentNullException">If any arguments are null</exception>
        public ComponentAssemblyElaborator(MgaModel subject)
        {
            if (subject == null)
            {
                throw new ArgumentNullException("subject");
            }

            // initialize subject
            this.Subject = subject;

            // initialize collections
            this.Traceability = new Dictionary<string, string>();
            this.InnerElaborators = new List<ComponentAssemblyElaborator>();
            this.ComponentAssemblyReferences = new Queue<MgaFCO>();
        }

        /// <summary>
        /// Gets or sets elaborators for contained component assembly references that need further elaboration.
        /// </summary>
        public List<ComponentAssemblyElaborator> InnerElaborators { get; set; }

        /// <summary>
        /// Gets or sets the already processed Component Assembly references from the parent to this object. Does not
        /// contain any branches. Used to detect circular dependencies between tree branches.
        /// </summary>
        public Queue<MgaFCO> ComponentAssemblyReferences { get; set; }

        /// <summary>
        /// Gets a reverse look up map for given object ids.
        /// </summary>
        /// <param name="originalIds">Ids to get the reverse map. Ids must be in a form of GME IDs <code>id-006x-xxxxxxxx</code>. Lower case hexadecimal digits.</param>
        /// <returns>Reverse loop up map between the original object and the elaborated objects</returns>
        public Dictionary<string, List<MgaFCO>> GetReverseLookupMap(IEnumerable<string> originalIds)
        {
            Dictionary<string, List<MgaFCO>> map = new Dictionary<string, List<MgaFCO>>();

            // initialize the lists
            foreach (var id in originalIds.Distinct())
            {
                map[id] = new List<MgaFCO>();
            }

            // build reverse map only for requested objects. Performance vs. memory considerations
            // Memory: we do not want to store the full (big) traceability dictionary twice
            // Performace: while traceability map is big and we need the keys by value; iterate through only once
            //             and build reverse map for the objects that we need.
            foreach (var kvp in this.Traceability)
            {
                if (map.ContainsKey(kvp.Value))
                {
                    map[kvp.Value].Add(this.Subject.Project.GetFCOByID(kvp.Key));
                }
            }

            return map;
        }

        /// <summary>
        /// Elaborates the given context recursively.
        /// </summary>
        public override void Elaborate()
        {
            MgaFilter filter = this.Subject.Project.CreateFilter();

            var allObjects = this.Subject.GetDescendantFCOs(filter);
            foreach (MgaFCO obj in allObjects)
            {
                if (obj.IsPrimaryDerived)
                {
                    obj.DetachFromArcheType();
                }
            }

            foreach (MgaFCO obj in allObjects)
            {
                if (this.Traceability.ContainsKey(obj.ID) == false)
                {
                    // add to traceability
                    this.Traceability.Add(obj.ID, obj.ID);
                }

                if (obj is MgaReference)
                {
                    var reference = obj as MgaReference;
                    if (reference.Referred == null)
                    {
                        if (reference.MetaBase.MetaRef == this.Factory.ComponentRefMeta)
                        {
                            this.Logger.WriteWarning(string.Format("Null {0} [{1}] was ignored and skipped.", reference.Name, reference.MetaBase.Name));
                        }

                        continue;
                    }

                    var referred = reference.Referred;

                    if (referred.MetaBase.MetaRef == this.Factory.ComponentMeta)
                    {
                        MgaObject parent = null;
                        GME.MGA.Meta.objtype_enum type;
                        reference.GetParent(out parent, out type);

                        var copied = this.SwitchReferenceToModel(parent as MgaModel, reference, true);

                        // delete reference
                        reference.DestroyObject();
                    }
                    else if (referred.MetaBase.MetaRef == this.Factory.ComponentAssemblyMeta)
                    {
                        MgaObject parent = null;
                        GME.MGA.Meta.objtype_enum type;
                        reference.GetParent(out parent, out type);

                        MgaObject parent2 = parent;
                        GME.MGA.Meta.objtype_enum type2;

                        // worst case this will terminate at the root folder level
                        while (parent2 != null && parent2 is MgaModel)
                        {
                            // FIXME: is this safe? should we compare IDs?
                            if (parent2 == reference.Referred)
                            {
                                string message = string.Format("Circular dependency: {0} --> {1}", parent2.Name, reference.Referred.Name);
                                throw new ElaboratorCircularReferenceException(message);
                            }

                            parent2.GetParent(out parent2, out type2);
                        }

                        if (this.ComponentAssemblyReferences.Any(x => x.ID == reference.Referred.ID))
                        {
                            string message = string.Format("Circular dependency: {0} --> {1}", string.Join(" -> ", this.ComponentAssemblyReferences.Select(x => x.Name)), reference.Referred.Name);
                            throw new ElaboratorCircularReferenceException(message);
                        }

                        var copied = this.SwitchReferenceToModel(parent as MgaModel, reference, false);

                        // prevent circular dependency
                        var innerElaborator = Elaborator.GetElaborator(copied, this.Logger) as ComponentAssemblyElaborator;

                        // use only one map
                        innerElaborator.Traceability = this.Traceability;

                        // hold only one queue
                        foreach (var item in this.ComponentAssemblyReferences)
                        {
                            innerElaborator.ComponentAssemblyReferences.Enqueue(item);
                        }

                        innerElaborator.ComponentAssemblyReferences.Enqueue(reference.Referred);
                        this.InnerElaborators.Add(innerElaborator);
                        innerElaborator.Elaborate();

                        // delete reference
                        reference.DestroyObject();
                    }
                }
            }

            this.IsElaborated = true;
        }
    }
}
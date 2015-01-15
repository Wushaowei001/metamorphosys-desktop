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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using GME.MGA;
using GME.CSharp;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using System.Windows.Forms;
using System.IO;
using CyPhyMetaLinkBridgeClient;
using edu.vanderbilt.isis.meta;
using Xunit;
using System.Collections.Concurrent;
using CyPhyML = ISIS.GME.Dsml.CyPhyML.Interfaces;
using CyPhyMLClasses = ISIS.GME.Dsml.CyPhyML.Classes;
using System.Xml.XPath;
using System.Xml;

namespace CyPhyPropagateTest
{
    public class CyPhyPropagateTest : MetaLinkTestBase
    {


        static void Main(string[] args)
        {
            int ret = Xunit.ConsoleClient.Program.Main(new string[] {
                System.Reflection.Assembly.GetAssembly(typeof(CyPhyPropagateTest)).CodeBase.Substring("file:///".Length),
                //"/noshadow",
            });
            Console.In.ReadLine();
            //System.Console.Out.WriteLine("HEllo World");
        }


        private static string PrepareComponentUpdateXml(string xmlpath, IDictionary<string, string> paths)
        {
            string xml = File.ReadAllText(xmlpath);

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);

            XmlNamespaceManager manager = new XmlNamespaceManager(doc.NameTable);
            manager.AddNamespace("avm", "avm");
            manager.AddNamespace("cad", "cad");

            XPathNavigator navigator = doc.CreateNavigator();
            var resourceDependencies = navigator.Select("/avm:Component/avm:ResourceDependency", manager).Cast<XPathNavigator>()
                .Concat(navigator.Select("/avm:Component/ResourceDependency", manager).Cast<XPathNavigator>());
            
            foreach (XPathNavigator node in resourceDependencies)
            {
                string path = node.GetAttribute("Path", "avm");
                if (String.IsNullOrWhiteSpace(path))
                {
                    path = node.GetAttribute("Path", "");
                }
                string newpath;
                if (paths.TryGetValue(node.GetAttribute("Name", ""), out newpath))
                {
                    node.MoveToAttribute("Path", "");
                    node.SetValue(newpath);
                }
            }
            StringBuilder sb = new StringBuilder();
            XmlTextWriter w = new XmlTextWriter(new StringWriter(sb));
            doc.WriteContentTo(w);
            w.Flush();
            return sb.ToString();
        }

        [Fact]
        // Update existing AVM component - a new resource has been added
        public void TestUpdateComponentReplaceResource()
        {
            SetupTest();

            SendInterest(CyPhyMetaLink.CyPhyMetaLinkAddon.CadAssemblyTopic, testAssemblyId);
            SendInterest(CyPhyMetaLink.CyPhyMetaLinkAddon.ComponentManifestTopic);
            WaitToReceiveMessage();
            WaitToReceiveMessage();

            RunCyPhyMLSync(
                (project, propagate, interpreter) =>
                {
                    string testCompGuid = null;
                    {
                        interpreter.MgaGateway.PerformInTransaction(() =>
                            testCompGuid = (string)((MgaModel)project.RootFolder.ObjectByPath[testComponentPath]).GetAttributeByNameDisp("AVMID"));
                        SendInterest(CyPhyMetaLink.CyPhyMetaLinkAddon.ComponentUpdateTopic, testCompGuid);
                        WaitToReceiveMessage();
                    }

                    // TODO propagate.StartEditingComponent
                    {
                        Edit msg = new Edit()
                        {
                            editMode = Edit.EditMode.POST,
                        };
                        msg.mode.Add(Edit.EditMode.POST);
                        msg.origin.Add(origin);
                        msg.topic.Add(CyPhyMetaLink.CyPhyMetaLinkAddon.ComponentUpdateTopic);
                        msg.topic.Add(testCompGuid);

                        msg.actions.Add(new edu.vanderbilt.isis.meta.Action());
                        msg.actions[0].actionMode = edu.vanderbilt.isis.meta.Action.ActionMode.UPDATE;
                        msg.actions[0].alien = new Alien();
                        msg.actions[0].alien.encodingMode = Alien.EncodingMode.XML;
                        msg.actions[0].alien.encoded = Encoding.UTF8.GetBytes(
                            PrepareComponentUpdateXml(Path.Combine(TestModelDir, @"UpdateComponentData3.xml"),
                            new Dictionary<string, string>() { 
                            { "test.asm", Path.Combine(TestModelDir, "") },
                            }));
                        propagate.EditMessageReceived(msg);
                        Application.DoEvents();

                        interpreter.MgaGateway.PerformInTransaction(() =>
                        {
                            var component = ISIS.GME.Dsml.CyPhyML.Classes.Component.Cast((MgaModel)project.RootFolder.ObjectByPath[testComponentPath]);
                            var cadModel = component.Children.CADModelCollection.First();
                            Assert.Equal(4, cadModel.Children.CADParameterCollection.Count());
                            Assert.Equal(9, cadModel.Children.CADDatumCollection.Count());
                            VerifyResources(component, new string[] { "cadxxx\\test.asm", "manufacturing\\damper_2.xml" });
                        });

                    }
                });
        }


        [Fact]
        // Update existing AVM component - a new resource has been added
        public void TestUpdateComponentNewResource()
        {
            SetupTest();

            SendInterest(CyPhyMetaLink.CyPhyMetaLinkAddon.CadAssemblyTopic, testAssemblyId);
            SendInterest(CyPhyMetaLink.CyPhyMetaLinkAddon.ComponentManifestTopic);
            WaitToReceiveMessage();
            WaitToReceiveMessage();

            RunCyPhyMLSync(
                (project, propagate, interpreter) =>
                {
                    string testCompGuid = null;
                    {
                        interpreter.MgaGateway.PerformInTransaction(() =>
                            testCompGuid = (string)((MgaModel)project.RootFolder.ObjectByPath[testComponentPath]).GetAttributeByNameDisp("AVMID"));
                        SendInterest(CyPhyMetaLink.CyPhyMetaLinkAddon.ComponentUpdateTopic, testCompGuid);
                        WaitToReceiveMessage();
                    }

                    // TODO propagate.StartEditingComponent
                    {
                        Edit msg = new Edit()
                        {
                            editMode = Edit.EditMode.POST,
                        };
                        msg.mode.Add(Edit.EditMode.POST);
                        msg.origin.Add(origin);
                        msg.topic.Add(CyPhyMetaLink.CyPhyMetaLinkAddon.ComponentUpdateTopic);
                        msg.topic.Add(testCompGuid);

                        msg.actions.Add(new edu.vanderbilt.isis.meta.Action());
                        msg.actions[0].actionMode = edu.vanderbilt.isis.meta.Action.ActionMode.UPDATE;
                        msg.actions[0].alien = new Alien();
                        msg.actions[0].alien.encodingMode = Alien.EncodingMode.XML;
                        msg.actions[0].alien.encoded = Encoding.UTF8.GetBytes(
                            PrepareComponentUpdateXml(Path.Combine(TestModelDir, @"UpdateComponentData2.xml"),
                            new Dictionary<string, string>() { 
                            { "DAMPER.PRT", Path.Combine(TestModelDir, "components\\Damper_2\\AVM.Component-50ec54a86e6cc33768468c31v2\\CADXXX\\") },
                            { "test.asm", Path.Combine(TestModelDir, "") }
                            }));
                        propagate.EditMessageReceived(msg);
                        Application.DoEvents();

                        interpreter.MgaGateway.PerformInTransaction(() =>
                        {
                            var component = ISIS.GME.Dsml.CyPhyML.Classes.Component.Cast((MgaModel)project.RootFolder.ObjectByPath[testComponentPath]);
                            var cadModel = component.Children.CADModelCollection.First();
                            Assert.Equal(4, cadModel.Children.CADParameterCollection.Count());
                            Assert.Equal(9, cadModel.Children.CADDatumCollection.Count());
                            VerifyResources(component, new string[] { "cadxxx\\test.asm", "cadxxx\\damper.prt", "manufacturing\\damper_2.xml" });
                        });

                    }
                });
        }

        [Fact]
        // Update existing AVM component - no resource change
        public void TestUpdateComponent()
        {
            SetupTest();

            SendInterest(CyPhyMetaLink.CyPhyMetaLinkAddon.CadAssemblyTopic, testAssemblyId);
            SendInterest(CyPhyMetaLink.CyPhyMetaLinkAddon.ComponentManifestTopic);
            WaitToReceiveMessage();
            WaitToReceiveMessage();

            RunCyPhyMLSync(
                (project, propagate, interpreter) =>
                {
                    string testCompGuid = null;
                    {
                        interpreter.MgaGateway.PerformInTransaction(() =>
                            testCompGuid = (string)((MgaModel)project.RootFolder.ObjectByPath[testComponentPath]).GetAttributeByNameDisp("AVMID"));
                        SendInterest(CyPhyMetaLink.CyPhyMetaLinkAddon.ComponentUpdateTopic, testCompGuid);
                        WaitToReceiveMessage();
                    }

                    // TODO propagate.StartEditingComponent
                    {
                        Edit msg = new Edit()
                        {
                            editMode = Edit.EditMode.POST,
                        };
                        msg.mode.Add(Edit.EditMode.POST);
                        msg.origin.Add(origin);
                        msg.topic.Add(CyPhyMetaLink.CyPhyMetaLinkAddon.ComponentUpdateTopic);
                        msg.topic.Add(testCompGuid);

                        msg.actions.Add(new edu.vanderbilt.isis.meta.Action());
                        msg.actions[0].actionMode = edu.vanderbilt.isis.meta.Action.ActionMode.UPDATE;
                        msg.actions[0].alien = new Alien();
                        msg.actions[0].alien.encodingMode = Alien.EncodingMode.XML;
                        msg.actions[0].alien.encoded = Encoding.UTF8.GetBytes(PrepareComponentUpdateXml(Path.Combine(TestModelDir, @"UpdateComponentData.xml"), new Dictionary<string, string>() { { "DAMPER.PRT", Path.Combine(TestModelDir, "components\\Damper_2\\AVM.Component-50ec54a86e6cc33768468c31v2\\CADXXX\\") } }));
                        propagate.EditMessageReceived(msg);
                        Application.DoEvents();

                        interpreter.MgaGateway.PerformInTransaction(() =>
                        {
                            var component = ISIS.GME.Dsml.CyPhyML.Classes.Component.Cast((MgaModel)project.RootFolder.ObjectByPath[testComponentPath]);
                            var cadModel = component.Children.CADModelCollection.First();
                            Assert.Equal(4, cadModel.Children.CADParameterCollection.Count());
                            Assert.Equal(9, cadModel.Children.CADDatumCollection.Count());
                            VerifyResources(component, new string[] { "cadxxx\\damper.prt", "manufacturing\\damper_2.xml" });
                        });

                    }
                });
        }

        private void VerifyResources(ISIS.GME.Dsml.CyPhyML.Interfaces.Component component, string[] resources)
        {
            Assert.Equal(resources.Length, component.Children.ResourceCollection.Count());
            foreach (var res in component.Children.ResourceCollection)
            {
                Assert.True(resources.Contains(res.Attributes.Path.ToLower()));
            }
        }

        private void WaitToReceiveMessage()
        {
            Edit received;
            if (this.receivedMessagesQueue.TryTake(out received, 5 * 1000) == false)
            {
                throw new TimeoutException();
            }
        }

        [Fact]
        // Create AVM component from scractch
        void TestCreateComponent()
        {
            SetupTest();

            RunCyPhyMLSync(
                (project, propagate, interpreter) =>
                {
                    {
                        Edit msg = new Edit()
                        {
                            editMode = Edit.EditMode.POST,
                        };
                        msg.mode.Add(Edit.EditMode.POST);
                        msg.origin.Add(origin);
                        msg.topic.Add(CyPhyMetaLink.CyPhyMetaLinkAddon.ComponentCreateTopic);

                        msg.actions.Add(new edu.vanderbilt.isis.meta.Action());
                        msg.actions[0].actionMode = edu.vanderbilt.isis.meta.Action.ActionMode.UPDATE;
                        msg.actions[0].alien = new Alien();
                        msg.actions[0].alien.encodingMode = Alien.EncodingMode.XML;
                        msg.actions[0].alien.encoded = Encoding.UTF8.GetBytes(File.ReadAllText(Path.Combine(TestModelDir,"CreateComponentData.xml")));
                        propagate.EditMessageReceived(msg);
                        Application.DoEvents();
                    }
                    project.BeginTransactionInNewTerr();
                    try
                    {
                        var imported = project.RootFolder.GetObjectByPathDisp("/@Components");
                        var result = imported.ChildObjects.Cast<IMgaObject>().Count(o => o.Name == "Damper_Test");
                        Xunit.Assert.Equal(1, result);
                    }
                    finally
                    {
                        project.AbortTransaction();
                    }
                }
            );
        }

        [Fact]
        // Remove a component and see that only one resync message sent
        void TestAssemblySync()
        {
            SetupTest();

            SendInterest(CyPhyMetaLink.CyPhyMetaLinkAddon.CadAssemblyTopic, testAssemblyId);
            SendInterest(CyPhyMetaLink.CyPhyMetaLinkAddon.ResyncTopic, testAssemblyId);
            WaitToReceiveMessage();
            WaitToReceiveMessage();

            RunCyPhyMLSync(
                (project, propagate, interpreter) =>
                {
                    MgaFCO assembly;
                    MgaFCO componentRefToDelete;
                    project.BeginTransactionInNewTerr();
                    try
                    {
                        assembly = GetTestAssembly(project);
                        componentRefToDelete = assembly.ChildObjects.Cast<MgaFCO>().Where(fco => fco.Name.Contains("Mass__Mass_Steel")).First();
                    }
                    finally
                    {
                        project.AbortTransaction();
                    }
                    interpreter.StartAssemblySync(project, assembly, 128);
                    Application.DoEvents();
                    project.BeginTransactionInNewTerr();
                    try
                    {
                        //connectorCompositionToDelete.DestroyObject();
                        componentRefToDelete.DestroyObject();
                    }
                    finally
                    {
                        project.CommitTransaction();
                    }
                    Application.DoEvents();
                    Thread.Sleep(1000); // XXX don't race with propagate's send message thread
                    WaitForAllMetaLinkMessages();
                    
                    Xunit.Assert.Equal(1,
                        this.receivedMessages.Where(
                            msg => msg.actions.Any(a => a.actionMode == edu.vanderbilt.isis.meta.Action.ActionMode.CLEAR)).Count());
                }
            );
        }


        [Fact]
        // Modify a component in design sync mode, and see that exactly one resync message sent
        void TestAssemblySync_ModifyComponent()
        {
            SetupTest();

            SendInterest(CyPhyMetaLink.CyPhyMetaLinkAddon.CadAssemblyTopic, testAssemblyId);
            SendInterest(CyPhyMetaLink.CyPhyMetaLinkAddon.ResyncTopic, testAssemblyId);
            WaitToReceiveMessage();
            WaitToReceiveMessage();

            RunCyPhyMLSync(
                (project, propagate, interpreter) =>
                {
                    MgaFCO assembly;
                    MgaFCO connectorToDelete;
                    
                    project.BeginTransactionInNewTerr();
                    try
                    {
                        assembly = GetTestAssembly(project);
                        MgaReference componentRef = (MgaReference) assembly.ChildObjects.Cast<MgaFCO>().Where(fco => fco.Name.Contains("Mass__Mass_Steel")).First();
                        connectorToDelete = ((MgaModel)componentRef.Referred).ChildFCOs.Cast<MgaFCO>().Where(fco => fco.Name == "PIN").First();
                    }
                    finally
                    {
                        project.AbortTransaction();
                    }
                    interpreter.StartAssemblySync(project, assembly, 128);
                    Application.DoEvents();
                    project.BeginTransactionInNewTerr();
                    try
                    {
                        connectorToDelete.DestroyObject();
                    }
                    finally
                    {
                        project.CommitTransaction();
                    }
                    Application.DoEvents();
                    Thread.Sleep(1000); // XXX don't race with propagate's send message thread
                    WaitForAllMetaLinkMessages();
                    
                    Xunit.Assert.Equal(1,
                        this.receivedMessages.Where(
                            msg => msg.actions.Any(a => a.actionMode == edu.vanderbilt.isis.meta.Action.ActionMode.CLEAR)).Count());
                }
            );
        }

        [Fact]
        // Insert an existing AVM component into a design
        void TestInsertComponentIntoDesign()
        {
            SetupTest();

            RunCyPhyMLSync(
                (project, propagate, interpreter) =>
                {
                    {
                        Edit msg = new Edit()
                        {
                            editMode = Edit.EditMode.POST,
                        };
                        msg.mode.Add(Edit.EditMode.POST);
                        msg.origin.Add(origin);
                        msg.topic.Add(CyPhyMetaLink.CyPhyMetaLinkAddon.CadAssemblyTopic);
                        msg.topic.Add(testAssemblyId);
                        edu.vanderbilt.isis.meta.Action action = new edu.vanderbilt.isis.meta.Action()
                        {
                            actionMode = edu.vanderbilt.isis.meta.Action.ActionMode.INSERT,
                            payload = new Payload()
                        };
                        action.payload.components.Add(new CADComponentType()
                        {
                            Name = "TestInsertComp",
                            AvmComponentID = testAVMId
                        });
                        msg.actions.Add(action);
                        
                        propagate.EditMessageReceived(msg);
                        Application.DoEvents();
                    }
                    project.BeginTransactionInNewTerr();
                    try
                    {
                        var assembly = GetTestAssembly(project);
                        var insertcomp = assembly.ChildObjects.Cast<MgaFCO>().Where(fco => fco.Name.Contains("TestInsertComp")).First();
                        Xunit.Assert.NotEqual(null, insertcomp);
                        CyPhyML.ComponentRef insertcompref = CyPhyMLClasses.ComponentRef.Cast(insertcomp);
                        Xunit.Assert.NotEqual(null, insertcompref.AllReferred);
                        Xunit.Assert.Equal(true, insertcompref.AllReferred is CyPhyML.Component);
                        Xunit.Assert.Equal(testAVMId, (insertcompref.AllReferred as CyPhyML.Component).Attributes.AVMID);
                    }
                    finally
                    {
                        project.AbortTransaction();
                    }
                }
            );
        }

        [Fact]
        // Select component - only test that it won't fail
        void TestSelectComponent()
        {
            SetupTest();

            RunCyPhyMLSync(
                (project, propagate, interpreter) =>
                {
                    {
                        Edit msg = new Edit()
                        {
                            editMode = Edit.EditMode.POST,
                        };
                        msg.mode.Add(Edit.EditMode.POST);
                        msg.origin.Add(origin);
                        msg.topic.Add(CyPhyMetaLink.CyPhyMetaLinkAddon.CadAssemblyTopic);
                        msg.topic.Add(testAssemblyId);
                        edu.vanderbilt.isis.meta.Action action = new edu.vanderbilt.isis.meta.Action()
                        {
                            actionMode = edu.vanderbilt.isis.meta.Action.ActionMode.SELECT,
                            payload = new Payload()
                        };
                        action.payload.components.Add(new CADComponentType()
                        {
                            ComponentID = massInstanceGuid
                        });
                        msg.actions.Add(action);

                        propagate.EditMessageReceived(msg);
                        Application.DoEvents();
                    }
                    project.BeginTransactionInNewTerr();
                    try
                    {
                        // Nothing to check here
                    }
                    finally
                    {
                        project.AbortTransaction();
                    }
                }
            );
        }

        [Fact]
        // Remove component from design
        void TestRemoveComponent()
        {
            SetupTest();

            RunCyPhyMLSync(
                (project, propagate, interpreter) =>
                {
                    {
                        Edit msg = new Edit()
                        {
                            editMode = Edit.EditMode.POST,
                        };
                        msg.mode.Add(Edit.EditMode.POST);
                        msg.origin.Add(origin);
                        msg.topic.Add(CyPhyMetaLink.CyPhyMetaLinkAddon.CadAssemblyTopic);
                        msg.topic.Add(testAssemblyId);
                        edu.vanderbilt.isis.meta.Action action = new edu.vanderbilt.isis.meta.Action()
                        {
                            actionMode = edu.vanderbilt.isis.meta.Action.ActionMode.DISCARD,
                            payload = new Payload()
                        };
                        action.payload.components.Add(new CADComponentType()
                        {
                            ComponentID = massInstanceGuid
                        });
                        msg.actions.Add(action);

                        propagate.EditMessageReceived(msg);
                        Application.DoEvents();
                    }
                    project.BeginTransactionInNewTerr();
                    try
                    {
                        var assembly = GetTestAssembly(project);
                        var masscomp = assembly.ChildObjects.Cast<MgaFCO>().Where(fco => fco.Name.Equals(massName));
                        Xunit.Assert.Equal(0, masscomp.Count());
                    }
                    finally
                    {
                        project.AbortTransaction();
                    }
                }
            );
        }

        //[Fact]
        // Remove multiple components from design
        void TestRemoveComponents()
        {
            SetupTest();

            RunCyPhyMLSync(
                (project, propagate, interpreter) =>
                {
                    {
                        Edit msg = new Edit()
                        {
                            editMode = Edit.EditMode.POST,
                        };
                        msg.mode.Add(Edit.EditMode.POST);
                        msg.origin.Add(origin);
                        msg.topic.Add(CyPhyMetaLink.CyPhyMetaLinkAddon.CadAssemblyTopic);
                        msg.topic.Add(testAssemblyId);
                        edu.vanderbilt.isis.meta.Action action = new edu.vanderbilt.isis.meta.Action()
                        {
                            actionMode = edu.vanderbilt.isis.meta.Action.ActionMode.INSERT,
                            payload = new Payload()
                        };
                        action.payload.components.Add(new CADComponentType()
                        {
                            ComponentID = massInstanceGuid
                        });
                        action.payload.components.Add(new CADComponentType()
                        {
                            ComponentID = springInstanceGuid
                        });
                        msg.actions.Add(action);

                        propagate.EditMessageReceived(msg);
                        Application.DoEvents();
                    }
                    project.BeginTransactionInNewTerr();
                    try
                    {
                        var assembly = GetTestAssembly(project);
                        
                        Xunit.Assert.Equal(1, assembly.ChildObjects.Cast<MgaFCO>().Where(fco => fco.MetaRole.Kind.Name=="ComponentRef").Count());
                    }
                    finally
                    {
                        project.AbortTransaction();
                    }
                }
            );
        }

        [Fact]
        // Receive a list of components
        void TestComponentList()
        {
            SetupTest();

            {
                Edit interest = new Edit();
                interest.mode.Add(Edit.EditMode.INTEREST);
                interest.origin.Add(origin);
                interest.topic.Add(CyPhyMetaLink.CyPhyMetaLinkAddon.ComponentManifestTopic);
                testingClient.SendToMetaLinkBridge(interest);
            } 
            
            RunCyPhyMLSync(
                (project, propagate, interpreter) =>
                {
                    {
                        Edit msg = new Edit()
                        {
                            editMode = Edit.EditMode.INTEREST,
                        };
                        msg.mode.Add(Edit.EditMode.INTEREST);
                        msg.origin.Add(origin);
                        msg.topic.Add(CyPhyMetaLink.CyPhyMetaLinkAddon.ComponentManifestTopic);

                        this.receivedMessages.Clear();
                        propagate.EditMessageReceived(msg);
                        Application.DoEvents();
                        Thread.Sleep(1000); // XXX don't race with propagate's send message thread
                        WaitForAllMetaLinkMessages();
                    }
                    project.BeginTransactionInNewTerr();
                    try
                    {
                        Xunit.Assert.Equal(1, this.receivedMessages.Where(msg => msg.actions.Count == 1 && msg.actions[0].manifest.Count() == 11).Count());
                    }
                    finally
                    {
                        project.AbortTransaction();
                    }
                }
            );
        }

        [Fact]
        // Insert something into a Component
        void TestInsertIntoComponent()
        {
            SetupTest();

            RunCyPhyMLSync(
                (project, propagate, interpreter) =>
                {
                    {
                        Edit msg = new Edit();
                        msg.mode.Add(Edit.EditMode.POST);
                        msg.origin.Add(origin);
                        msg.topic.Add(CyPhyMetaLink.CyPhyMetaLinkAddon.ComponentUpdateTopic);
                        msg.topic.Add(testAVMId);
                        msg.actions.Add(new edu.vanderbilt.isis.meta.Action()
                        {
                            actionMode = edu.vanderbilt.isis.meta.Action.ActionMode.INSERT,
                            payload = new Payload()
                        });
                        msg.actions[0].payload.connectors.Add(new ConnectorType()
                        {
                            DisplayName = "TestConnector",
                        });
                        msg.actions[0].payload.connectors[0].Datums.Add(new ConnectorDatumType()
                        {
                            DisplayName = "COMMON_PLANE_1_BOTTOM",
                            ID = "COMMON_PLANE_1_BOTTOM"
                        });
                        msg.actions[0].payload.connectors[0].Datums.Add(new ConnectorDatumType()
                        {
                            DisplayName = "COMMON_PLANE_1_TOP",
                            ID = "COMMON_PLANE_1_TOP"
                        });
                        msg.actions[0].payload.connectors[0].Datums.Add(new ConnectorDatumType()
                        {
                            DisplayName = "COMMON_AXIS",
                            ID = "COMMON_AXIS"
                        });
                        

                        propagate.EditMessageReceived(msg);
                        Application.DoEvents();
                        Thread.Sleep(1000); // XXX don't race with propagate's send message thread
                    }
                    project.BeginTransactionInNewTerr();
                    try
                    {
                        var imported = project.RootFolder.GetObjectByPathDisp("/@Imported_Components");
                        var damper = imported.ChildObjects.Cast<IMgaObject>().Where(o => o.Name == "Damper_2").First();
                        var testconn = damper.ChildObjects.Cast<MgaFCO>().Where(fco => fco.Name.Equals("TestConnector")).First();
                        CyPhyML.Connector conn = CyPhyMLClasses.Connector.Cast(testconn);
                        Xunit.Assert.Equal(conn.Children.CADDatumCollection.Count(), 3);
                    }
                    finally
                    {
                        project.AbortTransaction();
                    }
                }
            );
        }

        //[Fact]
        // Connect components within a design
        void TestConnectComponents()
        {
            SetupTest();

            RunCyPhyMLSync(
                (project, propagate, interpreter) =>
                {
                    {
                        Edit msg = new Edit();
                        msg.mode.Add(Edit.EditMode.POST);
                        msg.origin.Add(origin);
                        msg.topic.Add(CyPhyMetaLink.CyPhyMetaLinkAddon.ConnectTopic);
                        msg.actions.Add(new edu.vanderbilt.isis.meta.Action()
                        {
                            payload = new Payload()
                        });
                        msg.actions[0].payload.components.Add(new CADComponentType()
                        {
                            ComponentID = testAssemblyId
                        });
                        msg.actions[0].payload.components.Add(new CADComponentType()
                        {
                            ComponentID = massInstanceGuid
                        });
                        msg.actions[0].payload.components.Add(new CADComponentType()
                        {
                            ComponentID = springInstanceGuid
                        });


                        propagate.EditMessageReceived(msg);
                        Application.DoEvents();
                        Thread.Sleep(1000); // XXX don't race with propagate's send message thread
                    }
                    project.BeginTransactionInNewTerr();
                    try
                    {
                        var imported = project.RootFolder.GetObjectByPathDisp("/@Imported_Components");
                        var damper = imported.ChildObjects.Cast<IMgaObject>().Where(o => o.Name == "Damper_2").First();
                        var testconn = damper.ChildObjects.Cast<MgaFCO>().Where(fco => fco.Name.Equals("TestConnector")).First();
                        CyPhyML.Connector conn = CyPhyMLClasses.Connector.Cast(testconn);
                        Xunit.Assert.Equal(conn.Children.CADDatumCollection.Count(), 3);
                    }
                    finally
                    {
                        project.AbortTransaction();
                    }
                }
            );
        }
        /*
        static void GenerateAssemblies(CyPhyML.ComponentAssemblies rootfolder, CyPhyML.ComponentAssembly assembly, int level, CyPhyML.Component leaf)
        {
            if (level <= 3)
            {
                System.Console.Out.WriteLine(level);
                for (int i = 0; i < 5; i++)
                {
                    CyPhyML.ComponentRef cref = CyPhyMLClasses.ComponentRef.Create(assembly);
                    CyPhyML.ComponentAssembly childassembly = CyPhyMLClasses.ComponentAssembly.Create(rootfolder);
                    if (level < 3)
                    {
                        cref.Referred.ComponentAssembly = childassembly;
                        GenerateAssemblies(rootfolder, childassembly, level + 1, leaf);
                    }
                    else
                    {
                        cref.Referred.Component = leaf;
                    }
                }
            }
        }
         */

        //[Fact]
        // Elaborator performance test
        /*
        void TestElaborator()
        {
            SetupTest();

            RunCyPhyMLSync(
                (project, propagate, interpreter) =>
                {
                    project.BeginTransactionInNewTerr(transactiontype_enum.TRANSACTION_NON_NESTED);
                    CyPhyML.ComponentAssembly assembly = CyPhyMLClasses.ComponentAssembly.Cast(GetTestAssembly(project));
                    var imported = project.RootFolder.GetObjectByPathDisp("/@Imported_Components");
                    var damper = imported.ChildObjects.Cast<IMgaObject>().Where(o => o.Name == "Damper_2").First();
                    GenerateAssemblies(assembly.ParentContainer as CyPhyML.ComponentAssemblies, assembly, 0, CyPhyMLClasses.Component.Cast(damper));


                    Type t = Type.GetTypeFromProgID("MGA.Interpreter.CyPhyElaborateCS"); 
                    IMgaComponentEx elaborator = Activator.CreateInstance(t) as IMgaComponentEx;
                    DateTime t1 = DateTime.Now;
                    elaborator.Initialize(project);
                    elaborator.ComponentParameter["automated_expand"] = "true";
                    elaborator.ComponentParameter["console_messages"] = "off";
                    elaborator.InvokeEx(project, GetTestAssembly(project), (MgaFCOs)Activator.CreateInstance(Type.GetTypeFromProgID("Mga.MgaFCOs")), 0);
                    TimeSpan t2 = DateTime.Now - t1;
                    System.Console.Out.WriteLine("Elaborator time: " + t2.TotalMilliseconds);

                    project.AbortTransaction();
                }
            );
        }
        */
        [Fact]
        // Running CAD flattener on the assembly
        void TestCadFlattener()
        {
            SetupTest();

            RunCyPhyMLSync(
                (project, propagate, interpreter) =>
                {
                    project.BeginTransactionInNewTerr();
                    CyPhyML.ComponentAssembly assembly = CyPhyMLClasses.ComponentAssembly.Cast(GetTestAssembly(project));

                    Type t = Type.GetTypeFromProgID("MGA.Interpreter.CyPhyElaborateCS");
                    IMgaComponentEx elaborator = Activator.CreateInstance(t) as IMgaComponentEx;
                    DateTime t1 = DateTime.Now;
                    elaborator.Initialize(project);
                    elaborator.ComponentParameter["automated_expand"] = "true";
                    elaborator.ComponentParameter["console_messages"] = "off";
                    elaborator.InvokeEx(project, GetTestAssembly(project), (MgaFCOs)Activator.CreateInstance(Type.GetTypeFromProgID("Mga.MgaFCOs")), 0);
                    
                    CyPhy2CAD_CSharp.Logger.Instance.Reset();
                    CyPhy2CAD_CSharp.CADFlatDataCreator datacreator = new CyPhy2CAD_CSharp.CADFlatDataCreator("", Path.GetDirectoryName(project.ProjectConnStr.Substring("MGA=".Length)), metalink: true);

                    datacreator.CreateFlatData(assembly);
                    CyPhy2CAD_CSharp.DataRep.CADContainer cadcontainer = datacreator.CreateCADDataContainer(assembly.Guid.ToString(), CyPhy2CAD_CSharp.UtilityHelpers.CleanString2(assembly.Name));

                    // IF this is an empty design, add a root component
                    if (cadcontainer.assemblies.Count == 0)
                    {
                        cadcontainer.AddRootComponent(assembly);
                    }

                    Xunit.Assert.Equal(CyPhy2CAD_CSharp.Logger.Instance.ErrorCnt, 0);

                    project.AbortTransaction();
                }
            );
        }

        [Fact]
        void TestAssemblySyncDiscardComponent()
        {
            SetupTest();

            SendInterest(CyPhyMetaLink.CyPhyMetaLinkAddon.CadAssemblyTopic, testAssemblyHierarchy_2Id);
            SendInterest(CyPhyMetaLink.CyPhyMetaLinkAddon.ResyncTopic, testAssemblyHierarchy_2Id);
            WaitToReceiveMessage();
            WaitToReceiveMessage();

            RunCyPhyMLSync(
                (project, propagate, interpreter) =>
                {
                    MgaFCO testAssembly;
                    MgaFCO testAssemblyHierarchy_2;
                    MgaFCO componentInstance;
                    string componentInstanceGuid;
                    string testHierarchy_1RefId;
                    string testAssemblyRefId;
                    project.BeginTransactionInNewTerr();
                    try
                    {
                        var filter = project.CreateFilter();
                        filter.Kind = "ComponentAssembly";
                        testAssembly = project.AllFCOs(filter).Cast<MgaFCO>()
                            .Where(fco => fco.GetGuidDisp() == new Guid(testAssemblyId).ToString("B")).First();
                        testAssemblyHierarchy_2 = project.AllFCOs(filter).Cast<MgaFCO>()
                            .Where(fco => fco.GetGuidDisp() == new Guid(testAssemblyHierarchy_2Id).ToString("B")).First();
                        //hullAndHookHookGuid = (string)((MgaModel)project.RootFolder.ObjectByPath[hullAndHookHookPath]).GetAttributeByNameDisp("AVMID");
                        componentInstance = testAssembly.ChildObjects.Cast<MgaFCO>().Where(f => f.Name.Contains("Damper")).First();
                        componentInstanceGuid = componentInstance.GetStrAttrByNameDisp("InstanceGUID");
                        MgaReference testHierarchy_1Ref = testAssemblyHierarchy_2.ChildObjects.Cast<MgaFCO>().OfType<MgaReference>().First();
                        testHierarchy_1RefId = new Guid(testHierarchy_1Ref.GetGuidDisp()).ToString("B");
                        MgaReference testAssemblyRef = ((MgaModel)testHierarchy_1Ref.Referred).ChildObjects.Cast<MgaFCO>().OfType<MgaReference>().First();
                        testAssemblyRefId = new Guid(testAssemblyRef.GetGuidDisp()).ToString("B");
                    }
                    finally
                    {
                        project.AbortTransaction();
                    }
                    interpreter.StartAssemblySync(project, testAssemblyHierarchy_2, 128);
                    Application.DoEvents();
                    var msg = new Edit();
                    msg.mode.Add(Edit.EditMode.POST);
                    msg.origin.Add(origin);
                    msg.topic.AddRange(new string[] { CyPhyMetaLink.CyPhyMetaLinkAddon.CadAssemblyTopic, 
                        testAssemblyHierarchy_2Id });
                    var action = new edu.vanderbilt.isis.meta.Action();
                    action.actionMode = edu.vanderbilt.isis.meta.Action.ActionMode.DISCARD;
                    action.payload = new Payload();
                    action.payload.components.Add(new CADComponentType()
                    {
                        //ComponentID = hookInstanceGuid,
                        ComponentID = testHierarchy_1RefId
                         + testAssemblyRefId
                         + componentInstanceGuid
                    });
                    msg.actions.Add(action);

                    SendToMetaLinkBridgeAndWaitForAddonToReceive(msg);
                    WaitForAllMetaLinkMessages();
                    Application.DoEvents();

                    project.BeginTransactionInNewTerr();
                    try
                    {
                        Xunit.Assert.Equal((int)objectstatus_enum.OBJECT_DELETED, componentInstance.Status);
                    }
                    finally
                    {
                        project.AbortTransaction();
                    }
                }
            );
        }

        //[Fact]
        void TestAssemblySyncConnect()
        {
            // TODO: this needs to be rewritten
            SetupTest();

            SendInterest(CyPhyMetaLink.CyPhyMetaLinkAddon.CadAssemblyTopic, testAssemblyId);
            SendInterest(CyPhyMetaLink.CyPhyMetaLinkAddon.ResyncTopic, testAssemblyId);
            WaitToReceiveMessage();
            WaitToReceiveMessage();

            RunCyPhyMLSync(
                (project, propagate, interpreter) =>
                {
                    MgaFCO CAD_debug;
                    MgaFCO CAD_debug_copy;
                    MgaFCO hierarchy_2;
                    MgaFCO hookInstance;
                    MgaFCO hookCopyInstance;
                    MgaReference CAD_debug_ref;
                    MgaReference hierarchy_1_ref;
                    MgaReference hierarchy_1_ref_copy;
                    project.BeginTransactionInNewTerr();
                    try
                    {
                        var filter = project.CreateFilter();
                        filter.Kind = "ComponentAssembly";
                        CAD_debug = project.AllFCOs(filter).Cast<MgaFCO>()
                            .Where(fco => fco.GetGuidDisp() == new Guid(testAssemblyId).ToString("B")).First();
                        // FIXME this copy is no good, since CyPhyMetaLink can just create the connection in CAD_debug
                        CAD_debug_copy = CAD_debug.ParentFolder.CopyFCODisp(CAD_debug);
                        CAD_debug_copy.Name = CAD_debug.Name + "_copy";
                        hierarchy_2 = project.AllFCOs(filter).Cast<MgaFCO>()
                            .Where(fco => fco.GetGuidDisp() == new Guid(testAssemblyHierarchy_2Id).ToString("B")).First();
                        //hullAndHookHookGuid = (string)((MgaModel)project.RootFolder.ObjectByPath[hullAndHookHookPath]).GetAttributeByNameDisp("AVMID");
                        hookInstance = CAD_debug.ChildObjects.Cast<MgaFCO>().Where(f => f.Name.Contains("drawbar")).First();
                        hierarchy_1_ref = (MgaReference)hierarchy_2.ChildObjectByRelID[1];
                        CAD_debug_ref = (MgaReference)((MgaModel)hierarchy_1_ref.Referred).ChildObjectByRelID[1];
                        hookCopyInstance = CAD_debug_copy.ChildObjects.Cast<MgaFCO>().Where(f => f.Name.Contains("drawbar")).First();
                        hierarchy_1_ref_copy = (MgaReference)((IMgaModel)hierarchy_2).CopyFCODisp((MgaFCO)hierarchy_1_ref, hierarchy_1_ref.MetaRole);
                        hierarchy_1_ref_copy.Referred = CAD_debug_copy;
                    }
                    finally
                    {
                        project.CommitTransaction();
                    }
                    Edit connectMsg;
                    project.BeginTransactionInNewTerr();
                    try
                    {
                        string hookInstanceGuid;
                        string hookCopyInstanceGuid;
                        hookInstanceGuid = hookInstance.GetStrAttrByNameDisp("InstanceGUID");
                        hookCopyInstanceGuid = hookCopyInstance.GetStrAttrByNameDisp("InstanceGUID");

                        // CyPhyAddon should reassign this (during the last CommitTransaction)
                        Xunit.Assert.NotEqual(hierarchy_1_ref.GetStrAttrByNameDisp("InstanceGUID"),
                            hierarchy_1_ref_copy.GetStrAttrByNameDisp("InstanceGUID"));
                        Xunit.Assert.NotEqual(hookInstanceGuid, hookCopyInstanceGuid);

                        connectMsg = new Edit();
                        connectMsg.mode.Add(Edit.EditMode.POST);
                        connectMsg.origin.Add(origin);
                        connectMsg.topic.AddRange(new string[] { CyPhyMetaLink.CyPhyMetaLinkAddon.ConnectTopic, 
                        //    hullAndHookHierarchy_2_AssemblyId
                        });
                        var action = new edu.vanderbilt.isis.meta.Action();
                        action.actionMode = edu.vanderbilt.isis.meta.Action.ActionMode.DISCARD;
                        action.payload = new Payload();
                        action.payload.components.Add(new CADComponentType()
                        {
                            ComponentID = Guid.Parse(hierarchy_2.GetGuidDisp()).ToString()
                        });
                        action.payload.components.Add(new CADComponentType()
                        {
                            ComponentID = "2e1091d6-989e-4130-bfed-c046ef0f7011" + "_" +
                                hierarchy_1_ref.GetStrAttrByNameDisp("InstanceGUID")
                                 + CAD_debug_ref.GetStrAttrByNameDisp("InstanceGUID")
                                 + hookInstanceGuid
                        });
                        action.payload.components.Add(new CADComponentType()
                        {
                            ComponentID = "2e1091d6-989e-4130-bfed-c046ef0f7011" + "_" +
                                hierarchy_1_ref_copy.GetStrAttrByNameDisp("InstanceGUID")
                                 + hookCopyInstanceGuid
                        });
                        connectMsg.actions.Add(action);
                    }
                    finally
                    {
                        project.CommitTransaction();
                    }
                    interpreter.StartAssemblySync(project, hierarchy_2, 128);
                    Application.DoEvents();

                    SendToMetaLinkBridgeAndWaitForAddonToReceive(connectMsg);
                    WaitForAllMetaLinkMessages();
                    Application.DoEvents();
                    Thread.Sleep(1000); // XXX don't race with propagate's send message thread
                    WaitForAllMetaLinkMessages();

                    project.BeginTransactionInNewTerr();
                    try
                    {
                        Xunit.Assert.Equal((int)objectstatus_enum.OBJECT_DELETED, hookInstance.Status);
                    }
                    finally
                    {
                        project.AbortTransaction();
                    }
                }
            );
        }

        [Fact]
        public void TestCopyComponent()
        {
            SetupTest();

            SendInterest(CyPhyMetaLink.CyPhyMetaLinkAddon.CadAssemblyTopic, testAssemblyId);
            SendInterest(CyPhyMetaLink.CyPhyMetaLinkAddon.ComponentManifestTopic);
            WaitToReceiveMessage();
            WaitToReceiveMessage();

            RunCyPhyMLSync(
                (project, propagate, interpreter) =>
                {
                    CyPhyML.Component damper2 = null;
                    MgaFCO firstChild = null;
                    interpreter.MgaGateway.PerformInTransaction(delegate
                    {
                        damper2 = CyPhyMLClasses.Component.Cast(((MgaFCO)project.RootFolder.ObjectByPath[testComponentPath]));
                        firstChild = (MgaFCO)damper2.Children.CADModelCollection.First().Impl;
                    });

                    Func<IEnumerable<string>> componentNames = () => damper2.ParentContainer.AllChildren.Select(f => f.Name);

                    interpreter.MgaGateway.PerformInTransaction(delegate
                    {
                        Assert.False(componentNames().Contains(damper2.Name + "_new"));
                        Assert.False(componentNames().Contains(damper2.Name + "_new_2"));
                    }, transactiontype_enum.TRANSACTION_GENERAL, false);
                    propagate.StartEditingComponent(damper2, firstChild, true);

                    interpreter.MgaGateway.PerformInTransaction(delegate
                    {
                        Assert.True(componentNames().Contains(damper2.Name + "_new"));
                        Assert.False(componentNames().Contains(damper2.Name + "_new_2"));
                    }, transactiontype_enum.TRANSACTION_GENERAL, false);

                    propagate.StartEditingComponent(damper2, firstChild, true);

                    interpreter.MgaGateway.PerformInTransaction(delegate
                    {
                        var x = damper2.ParentContainer.AllChildren.Select(f => f.Name).ToArray();
                        Assert.True(componentNames().Contains(damper2.Name + "_new"));
                        Assert.True(componentNames().Contains(damper2.Name + "_new_2")); // META-2526
                    }, transactiontype_enum.TRANSACTION_GENERAL, false);

                });
        }

    }
}

// cd C:\Users\kevin\meta11\META_MetaLink_HullandHook
// change addcomp.txt topic guids
// java -jar \Users\kevin\Documents\META_13.15\src\MetaLink\meta-bridge\java-client\target\metalink-java-client-1.0.0.jar  -p addcomp.txt
// java -jar \Users\kevin\Documents\META_13.15\src\MetaLink\meta-bridge\java-server\target\metalink-java-server-1.0.0.jar   -r EditComponent.mlp

/*
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
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using GME.CSharp;
using GME;
using GME.MGA;
using GME.MGA.Core;
using System.Windows.Forms;
using CyPhy = ISIS.GME.Dsml.CyPhyML.Interfaces;
using CyPhyClasses = ISIS.GME.Dsml.CyPhyML.Classes;
using CyPhy2DesignInterchange;
using System.Linq;
using CyPhyGUIs;
using System.Reflection;
using System.Xml;
using META;
using Ionic.Zip;

namespace CyPhyDesignExporter
{
    /// <summary>
    /// This class implements the necessary COM interfaces for a GME interpreter component.
    /// </summary>
    [Guid(ComponentConfig.guid),
    ProgId(ComponentConfig.progID),
    ClassInterface(ClassInterfaceType.AutoDual)]
    [ComVisible(true)]
    public class CyPhyDesignExporterInterpreter : IMgaComponentEx, IGMEVersionInfo, ICyPhyInterpreter
    {
        /// <summary>
        /// Contains information about the GUI event that initiated the invocation.
        /// </summary>
        public enum ComponentStartMode
        {
            GME_MAIN_START = 0, 		// Not used by GME
            GME_BROWSER_START = 1,      // Right click in the GME Tree Browser window
            GME_CONTEXT_START = 2,		// Using the context menu by right clicking a model element in the GME modeling window
            GME_EMBEDDED_START = 3,		// Not used by GME
            GME_MENU_START = 16,		// Clicking on the toolbar icon, or using the main menu
            GME_BGCONTEXT_START = 18,	// Using the context menu by right clicking the background of the GME modeling window
            GME_ICON_START = 32,		// Not used by GME
            GME_SILENT_MODE = 128 		// Not used by GME, available to testers not using GME
        }

        /// <summary>
        /// This function is called for each interpreter invocation before Main.
        /// Don't perform MGA operations here unless you open a tansaction.
        /// </summary>
        /// <param name="project">The handle of the project opened in GME, for which the interpreter was called.</param>
        public void Initialize(MgaProject project)
        {
            // TODO: Add your initialization code here...
        }

        /// <summary>
        /// Parameter of this run.
        /// </summary>
        private InterpreterMainParameters mainParameters { get; set; }

        public string InterpreterConfigurationProgId
        {
            get
            {
                return (typeof(CyPhyGUIs.NullInterpreterConfiguration).GetCustomAttributes(typeof(ProgIdAttribute), false)[0] as ProgIdAttribute).Value;
            }
        }

        public IInterpreterPreConfiguration PreConfig(IPreConfigParameters parameters)
        {
            return null;
        }

        public IInterpreterConfiguration DoGUIConfiguration(IInterpreterPreConfiguration preConfig, IInterpreterConfiguration previousConfig)
        {
            return new CyPhyGUIs.NullInterpreterConfiguration();
        }

        public IInterpreterResult Main(IInterpreterMainParameters parameters)
        {
            this.mainParameters = (InterpreterMainParameters)parameters;

            try
            {
                MgaGateway = new MgaGateway(mainParameters.Project);
                parameters.Project.CreateTerritoryWithoutSink(out MgaGateway.territory);

                MgaGateway.PerformInTransaction(delegate
                {
                    MainInTransaction((InterpreterMainParameters)parameters);
                });
                return new InterpreterResult() { Success = true, RunCommand = "" };
            }
            finally
            {
                if (MgaGateway.territory != null)
                {
                    MgaGateway.territory.Destroy();
                }
                MgaGateway = null;                
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        public void MainInTransaction(InterpreterMainParameters parameters)
        {
            this.mainParameters = (InterpreterMainParameters)parameters;

            Boolean disposeLogger = false;
            if (Logger == null)
            {
                Logger = new GMELogger(mainParameters.Project, "CyPhyDesignExporter");
                disposeLogger = true;
            }

            var currentObject = mainParameters.CurrentFCO;
            var currentOutputDirectory = mainParameters.OutputDirectory;
            string artifactName = string.Empty;
            string metaBaseName = currentObject.MetaBase.Name;

            try
            {
                if (metaBaseName == typeof(CyPhyClasses.DesignContainer).Name)
                {
                    artifactName = ExportToFile(CyPhyClasses.DesignContainer.Cast(currentObject), currentOutputDirectory);
                }
                else if (metaBaseName == typeof(CyPhyClasses.ComponentAssembly).Name)
                {
                    artifactName = ExportToFile(CyPhyClasses.ComponentAssembly.Cast(currentObject), currentOutputDirectory);
                }
                else if (IsTestBenchType(metaBaseName))
                {
                    artifactName = ExportToFile(CyPhyClasses.TestBenchType.Cast(currentObject), currentOutputDirectory);
                }

                if (!string.IsNullOrWhiteSpace(artifactName))
                {
                    var manifest = AVM.DDP.MetaTBManifest.OpenForUpdate(currentOutputDirectory);
                    manifest.AddArtifact(Path.GetFileName(artifactName), "Design Model");
                    manifest.Serialize(currentOutputDirectory);
                }
            }
            finally
            {
                if (disposeLogger)
                {
                    DisposeLogger();
                }
            }

        }

        private static Lazy<string[]> m_TestBenchTypeNames = new Lazy<string[]>(() => 
        {
            var tbt = typeof(CyPhy.TestBenchType);
            return Assembly.
                        GetAssembly(tbt).
                        GetTypes().
                        Where(t => tbt.IsAssignableFrom(t)).
                        Select(x => x.Name).ToArray();
        });

        private static string[] TestBenchTypeNames 
        {
            get
            {
                return m_TestBenchTypeNames.Value;
            }
        }

        private bool IsDesignType(string typeName)
        {
            if (typeName == "ComponentAssembly" ||
                typeName == "DesignContainer")
            { 
                return true;
            }

            return false;
        }

        private bool IsTestBenchType(string typeName)
        {
            return TestBenchTypeNames.Contains(typeName);
        }

        private string ExportToFile(CyPhy.TestBenchType testBench, string outputDirectory)
        {
            var topLevelSystem = testBench.Children.TopLevelSystemUnderTestCollection.FirstOrDefault();
            if (topLevelSystem != null)
            {
                var design = topLevelSystem.Referred.DesignEntity;
                if (design != null)
                {
                    return ExportToFile(design, outputDirectory);
                }
            }
            else
            {
                var tlsut = ((MgaObject)testBench).ChildObjects.
                                    Cast<MgaObject>().
                                    OfType<MgaFCO>().
                                    Where(x => x.MetaBase.Name == "TopLevelSystemUnderTest")
                                    .Cast<CyPhyClasses.DesignEntity>().FirstOrDefault();
                if (tlsut != null)
                    return ExportToFile(tlsut, outputDirectory);
            }
            throw new NotSupportedException("No TopLevelSystemUnderTest found");
        }

        private String Safeify(String s_in)
        {
            String rtn = s_in;
            rtn = rtn.Replace("\\", "_");
            rtn = rtn.Replace("/", "_");
            return rtn;
        }

        private string ExportToFile(CyPhy.DesignEntity de, String s_outFolder)
        {
            // Elaborate first
            CallElaborator(de.Impl.Project, de.Impl as MgaFCO, null, 128, true);
            
            var dm = CyPhy2DesignInterchange.CyPhy2DesignInterchange.Convert(de);
            String s_outFilePath = String.Format("{0}\\{1}.adm", s_outFolder, Safeify(de.Name));
            //dm.SaveToFile(s_outFilePath);
            XSD2CSharp.AvmXmlSerializer.SaveToFile(Path.GetFullPath(Path.Combine(s_outFolder, Safeify(de.Name) + ".adm")), dm);

            CheckForDuplicateIDs(dm);

            return s_outFilePath;   
        }

        public string ExportToPackage(CyPhy.ComponentAssembly ca, String s_outFolder)
        {
            // Create a temp folder
            var pathTemp = Path.Combine(System.IO.Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(pathTemp);

            // Export an ADM file to that temp folder
            var pathADM = ExportToFile(ca, pathTemp);

            // Generate zip file
            String pathADP = Path.Combine(s_outFolder,
                                          Path.GetFileNameWithoutExtension(pathADM) + ".adp");
            File.Delete(pathADP);
            using (ZipFile zip = new ZipFile(pathADP)
                                 {
                                    CompressionLevel = Ionic.Zlib.CompressionLevel.BestCompression
                                 })
            {
                var pathCA = ca.GetDirectoryPath(ComponentLibraryManager.PathConvention.ABSOLUTE);
                if (false == (pathCA.EndsWith("//") || 
                              pathCA.EndsWith("\\\\")))
                {
                    pathCA += "//";
                }

                foreach (var file in Directory.EnumerateFiles(pathCA, "*.*", SearchOption.AllDirectories))
                {
                    var relpath = Path.GetDirectoryName(ComponentLibraryManager.MakeRelativePath(pathCA, file));
                    zip.AddFile(file, relpath);
                }

                // Add the ADM file
                zip.AddFile(pathADM, "");

                zip.Save();
            }

            // Delete temporary directory
            Directory.Delete(pathTemp, true);            

            return pathADP;
        }

        public bool CheckForDuplicateIDs(avm.Design d)
        {
            //String str = d.Serialize();  
            String str = XSD2CSharp.AvmXmlSerializer.Serialize(d);

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(str);
            XmlNode root = doc.DocumentElement;

            var ls_EncounteredIDs = new List<String>();
            foreach (XmlAttribute node in root.SelectNodes("//@ID"))
            {
                ls_EncounteredIDs.Add(node.Value);
            }
            
            // Get all duplicate IDs that aren't empty/whitespace
            var duplicates = ls_EncounteredIDs.Where(s => !String.IsNullOrWhiteSpace(s))
                                              .GroupBy(s => s)
                                              .Where(g => g.Count() > 1)
                                              .Select(g => g.Key)
                                              .ToList();
            if (duplicates.Any())
            {
                String msg = "Duplicate IDs found in exported design: ";
                foreach (var dupe in duplicates)
                    msg += String.Format("{0}\"{1}\", ", Environment.NewLine, dupe);

                if (Logger != null)
                    Logger.WriteError(msg);
                return true;
            }
            
            return false;
        }

        private bool CallElaborator(
            MgaProject project,
            MgaFCO currentobj,
            MgaFCOs selectedobjs,
            int param,
            bool expand = true)
        {
            bool result = false;
            try
            {
                if (Logger != null)
                    Logger.WriteInfo("Elaborating model...");
                var elaborator = new CyPhyElaborateCS.CyPhyElaborateCSInterpreter();
                elaborator.Initialize(project);
                int verbosity = 128;
                elaborator.UnrollConnectors = false;
                result = elaborator.RunInTransaction(project, currentobj, selectedobjs, verbosity);

                if (Logger != null)
                    Logger.WriteInfo("Elaboration is done.");
            }
            catch (Exception ex)
            {
                if (Logger != null)
                    Logger.WriteError("Exception occurred in Elaborator : {0}", ex.ToString());
                result = false;
            }

            return result;
        }

        /// <summary>
        /// The main entry point of the interpreter. A transaction is already open,
        /// GMEConsole is available. A general try-catch block catches all the exceptions
        /// coming from this function, you don't need to add it. For more information, see InvokeEx.
        /// </summary>
        /// <param name="project">The handle of the project opened in GME, for which the interpreter was called.</param>
        /// <param name="currentobj">The model open in the active tab in GME. Its value is null if no model is open (no GME modeling windows open). </param>
        /// <param name="selectedobjs">
        /// A collection for the selected model elements. It is never null.
        /// If the interpreter is invoked by the context menu of the GME Tree Browser, then the selected items in the tree browser. Folders
        /// are never passed (they are not FCOs).
        /// If the interpreter is invoked by clicking on the toolbar icon or the context menu of the modeling window, then the selected items 
        /// in the active GME modeling window. If nothing is selected, the collection is empty (contains zero elements).
        /// </param>
        /// <param name="startMode">Contains information about the GUI event that initiated the invocation.</param>
        [ComVisible(false)]
        public void Main(MgaProject project, MgaFCO currentobj, MgaFCOs selectedobjs, ComponentStartMode startMode)
        {
            Boolean disposeLogger = false;
            if (Logger == null)
            {
                Logger = new CyPhyGUIs.GMELogger(project, "CyPhyDesignExporter");
                disposeLogger = true;
            }

            // TODO: Add your interpreter code
            Logger.WriteInfo("Running Design Exporter...");

            #region Prompt for Output Path
            // Get an output path from the user.
            if (this.OutputDir == null)
            {
                using (META.FolderBrowserDialog fbd = new META.FolderBrowserDialog()
                {
                    Description = "Choose a path for the generated files.",
                    //ShowNewFolderButton = true,
                    SelectedPath = Environment.CurrentDirectory,
                })
                {

                    DialogResult dr = fbd.ShowDialog();
                    if (dr == DialogResult.OK)
                    {
                        OutputDir = fbd.SelectedPath;
                    }
                    else
                    {
                        Logger.WriteWarning("Design Exporter cancelled");
                        return;
                    }
                }
            }
            #endregion

            Logger.WriteInfo("Beginning Export...");
            List<CyPhy.DesignEntity> lde_allCAandDC = new List<CyPhy.DesignEntity>();
            List<CyPhy.TestBenchType> ltbt_allTB = new List<CyPhy.TestBenchType>();
            
            if (currentobj != null && 
                currentobj.Meta.Name == "ComponentAssembly")
            {
                lde_allCAandDC.Add(CyPhyClasses.ComponentAssembly.Cast(currentobj));
            }
            else if (currentobj != null &&
                     currentobj.Meta.Name == "DesignContainer")
            {
                lde_allCAandDC.Add(CyPhyClasses.DesignContainer.Cast(currentobj));
            }
            else if (currentobj != null &&
                IsTestBenchType(currentobj.MetaBase.Name))
            {
                ltbt_allTB.Add(CyPhyClasses.TestBenchType.Cast(currentobj));
            }
            else if (selectedobjs != null && selectedobjs.Count > 0)
            {
                foreach (MgaFCO mf in selectedobjs)
                {
                    if (mf.Meta.Name == "ComponentAssembly")
                    {
                        lde_allCAandDC.Add(CyPhyClasses.ComponentAssembly.Cast(mf));
                    }
                    else if (mf.Meta.Name == "DesignContainer")
                    {
                        lde_allCAandDC.Add(CyPhyClasses.DesignContainer.Cast(mf));
                    }
                    else if (IsTestBenchType(mf.MetaBase.Name))
                    {
                        ltbt_allTB.Add(CyPhyClasses.TestBenchType.Cast(mf));
                    }
                }
            }
            else
            {
                CyPhy.RootFolder rootFolder = ISIS.GME.Common.Utils.CreateObject<CyPhyClasses.RootFolder>(project.RootFolder as MgaObject);

                MgaFilter filter = project.CreateFilter();
                filter.Kind = "ComponentAssembly";
                foreach (var item in project.AllFCOs(filter).Cast<MgaFCO>())
                {
                    if (item.ParentFolder != null)
                    {
                        lde_allCAandDC.Add(CyPhyClasses.ComponentAssembly.Cast(item));
                    }
                }

                filter = project.CreateFilter();
                filter.Kind = "DesignContainer";
                foreach (var item in project.AllFCOs(filter).Cast<MgaFCO>())
                {
                    if (item.ParentFolder != null)
                    {
                        lde_allCAandDC.Add(CyPhyClasses.DesignContainer.Cast(item));
                    }
                }

                filter = project.CreateFilter();
                filter.Kind = "TestBenchType";
                foreach (var item in project.AllFCOs(filter).Cast<MgaFCO>())
                {
                    if (item.ParentFolder != null)
                    {
                        ltbt_allTB.Add(CyPhyClasses.TestBenchType.Cast(item));
                    }
                }
            }

            foreach (CyPhy.DesignEntity de in lde_allCAandDC)
            {
                System.Windows.Forms.Application.DoEvents();
                try
                {
                    if (de is CyPhy.ComponentAssembly)
                    {
                        ExportToPackage(de as CyPhy.ComponentAssembly, OutputDir);
                    }
                    else
                    {
                        ExportToFile(de, OutputDir);
                    }
                } 
                catch (Exception ex) 
                {
                    Logger.WriteError("{0}: Exception encountered ({1})",de.Name,ex.Message);
                }
                Logger.WriteInfo("{0}: {1}", de.Name, OutputDir);
            }

            foreach (CyPhy.TestBenchType tbt in ltbt_allTB)
            {
                System.Windows.Forms.Application.DoEvents();
                try
                {
                    ExportToFile(tbt, OutputDir);
                }
                catch (Exception ex)
                {
                    Logger.WriteError("{0}: Exception encountered ({1})", tbt.Name, ex.Message);
                }
                Logger.WriteInfo("{0}: {1}", tbt.Name, OutputDir);
            }

            Logger.WriteInfo(String.Format("{0} model(s) exported", lde_allCAandDC.Count + ltbt_allTB.Count));
            Logger.WriteInfo("Design Exporter finished");

            if (disposeLogger)
            {
                DisposeLogger();
            }
        }

        #region IMgaComponentEx Members

        MgaGateway MgaGateway { get; set; }
        GMELogger Logger { get; set; }

        public void DisposeLogger()
        {
            if (Logger != null)
            {
                Logger.Dispose();
                Logger = null;
            }
        }

        public void InvokeEx(MgaProject project, MgaFCO currentobj, MgaFCOs selectedobjs, int param)
        {
            if (!enabled)
            {
                return;
            }

            try
            {
                MgaGateway = new MgaGateway(project);
                project.CreateTerritoryWithoutSink(out MgaGateway.territory);

                MgaGateway.BeginTransaction();
                Main(project, currentobj, selectedobjs, Convert(param));
                MgaGateway.AbortTransaction();
            }
            finally
            {
                if (MgaGateway.territory != null)
                {
                    MgaGateway.territory.Destroy();
                }
                MgaGateway = null;
                project = null;
                currentobj = null;
                selectedobjs = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        private ComponentStartMode Convert(int param)
        {
            switch (param)
            {
                case (int)ComponentStartMode.GME_BGCONTEXT_START:
                    return ComponentStartMode.GME_BGCONTEXT_START;
                case (int)ComponentStartMode.GME_BROWSER_START:
                    return ComponentStartMode.GME_BROWSER_START;

                case (int)ComponentStartMode.GME_CONTEXT_START:
                    return ComponentStartMode.GME_CONTEXT_START;

                case (int)ComponentStartMode.GME_EMBEDDED_START:
                    return ComponentStartMode.GME_EMBEDDED_START;

                case (int)ComponentStartMode.GME_ICON_START:
                    return ComponentStartMode.GME_ICON_START;

                case (int)ComponentStartMode.GME_MAIN_START:
                    return ComponentStartMode.GME_MAIN_START;

                case (int)ComponentStartMode.GME_MENU_START:
                    return ComponentStartMode.GME_MENU_START;
                case (int)ComponentStartMode.GME_SILENT_MODE:
                    return ComponentStartMode.GME_SILENT_MODE;
            }

            return ComponentStartMode.GME_SILENT_MODE;
        }

        #region Component Information
        public string ComponentName
        {
            get { return GetType().Name; }
        }

        public string ComponentProgID
        {
            get
            {
                return ComponentConfig.progID;
            }
        }

        public componenttype_enum ComponentType
        {
            get { return ComponentConfig.componentType; }
        }
        public string Paradigm
        {
            get { return ComponentConfig.paradigmName; }
        }
        #endregion

        #region Enabling
        bool enabled = true;
        public void Enable(bool newval)
        {
            enabled = newval;
        }
        #endregion

        #region Interactive Mode
        protected bool interactiveMode = true;
        public bool InteractiveMode
        {
            get
            {
                return interactiveMode;
            }
            set
            {
                interactiveMode = value;
            }
        }
        #endregion

        #region Custom Parameters
        SortedDictionary<string, object> componentParameters = null;

        public object get_ComponentParameter(string Name)
        {
            if (Name == "type")
                return "csharp";

            if (Name == "path")
                return GetType().Assembly.Location;

            if (Name == "fullname")
                return GetType().FullName;

            object value;
            if (componentParameters != null && componentParameters.TryGetValue(Name, out value))
            {
                return value;
            }

            return null;
        }

        public void set_ComponentParameter(string Name, object pVal)
        {
            if (componentParameters == null)
            {
                componentParameters = new SortedDictionary<string, object>();
            }

            componentParameters[Name] = pVal;
        }
        #endregion

        #region Unused Methods
        // Old interface, it is never called for MgaComponentEx interfaces
        public void Invoke(MgaProject Project, MgaFCOs selectedobjs, int param)
        {
            throw new NotImplementedException();
        }

        // Not used by GME
        public void ObjectsInvokeEx(MgaProject Project, MgaObject currentobj, MgaObjects selectedobjs, int param)
        {
            throw new NotImplementedException();
        }

        #endregion

        #endregion

        #region IMgaVersionInfo Members

        public GMEInterfaceVersion_enum version
        {
            get { return GMEInterfaceVersion_enum.GMEInterfaceVersion_Current; }
        }

        #endregion

        #region Registration Helpers

        [ComRegisterFunctionAttribute]
        public static void GMERegister(Type t)
        {
            Registrar.RegisterComponentsInGMERegistry();

        }

        [ComUnregisterFunctionAttribute]
        public static void GMEUnRegister(Type t)
        {
            Registrar.UnregisterComponentsInGMERegistry();
        }

        #endregion

        public string OutputDir;
    }
}

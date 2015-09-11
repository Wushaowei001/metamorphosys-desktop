using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using GME.CSharp;
using GME;
using GME.MGA;
using GME.MGA.Core;
using CyPhyGUIs;
using CyPhyClasses = ISIS.GME.Dsml.CyPhyML.Classes;
using CyPhy = ISIS.GME.Dsml.CyPhyML.Interfaces;
using META;

namespace CyPhy2PCBMfg
{
    /// <summary>
    /// This class implements the necessary COM interfaces for a GME interpreter component.
    /// </summary>
    [Guid(ComponentConfig.guid),
    ProgId(ComponentConfig.progID),
    ClassInterface(ClassInterfaceType.AutoDual)]
    [ComVisible(true)]
    public class CyPhy2PCBMfgInterpreter : IMgaComponentEx, IGMEVersionInfo, ICyPhyInterpreter
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
        /// Don't perform MGA operations here unless you open a transaction.
        /// </summary>
        /// <param name="project">The handle of the project opened in GME, for which the interpreter was called.</param>
        public void Initialize(MgaProject project)
        {
            MgaGateway = new MgaGateway(project);
            project.CreateTerritoryWithoutSink(out MgaGateway.territory);
        }

        #region CyPhyGUIs
        /// <summary>
        /// ProgId of the configuration class of this interpreter.
        /// </summary>
        public string InterpreterConfigurationProgId
        {
            get
            {
                return (typeof(CyPhyGUIs.NullInterpreterConfiguration).GetCustomAttributes(typeof(ProgIdAttribute), false)[0] as ProgIdAttribute).Value;
            }
        }

        /// <summary>
        /// Preconfig gets called first. No transaction is open, but one may be opened.
        /// In this function model may be processed and some object ids get serialized
        /// and returned as preconfiguration (project-wise configuration).
        /// </summary>
        /// <param name="preConfigParameters"></param>
        /// <returns>Null if no configuration is required by the DoGUIConfig.</returns>
        public IInterpreterPreConfiguration PreConfig(IPreConfigParameters preConfigParameters)
        {
            return null;
        }

        /// <summary>
        /// Shows a form for the user to select/change settings through a GUI. All interactive 
        /// GUI operations MUST happen within this function scope.
        /// </summary>
        /// <param name="preConfig">Result of PreConfig</param>
        /// <param name="previousConfig">Previous configuration to initialize the GUI.</param>
        /// <returns>Null if operation is canceled by the user. Otherwise returns with a new
        /// configuration object.</returns>
        public IInterpreterConfiguration DoGUIConfiguration(
            IInterpreterPreConfiguration preConfig,
            IInterpreterConfiguration previousConfig)
        {
            return new NullInterpreterConfiguration();
        }

        /// <summary>
        /// No GUI and interactive elements are allowed within this function.
        /// </summary>
        /// <param name="parameters">Main parameters for this run and GUI configuration.</param>
        /// <returns>Result of the run, which contains a success flag.</returns>
        public IInterpreterResult Main(IInterpreterMainParameters parameters)
        {
            this.mainParameters = parameters;

            this.result = new InterpreterResult()
            {
                Success = true
            };


            bool disposeLogger = false;
            try
            {
                if (this.Logger == null)
                {
                    this.Logger = new CyPhyGUIs.GMELogger(parameters.Project, this.ComponentName);
                    disposeLogger = true;
                }
                
                string pathDesign = "";
                string camFilePath = "";
                MgaGateway.PerformInTransaction(delegate
                {
                    var testbench = CyPhyClasses.TestBench.Cast(parameters.CurrentFCO)
                                    as CyPhy.TestBench;
                    var firstThing = testbench.Children.TopLevelSystemUnderTestCollection.First();
                    var design = firstThing.Referred.ComponentAssembly;
                    pathDesign = design.GetDirectoryPath(ComponentLibraryManager.PathConvention.ABSOLUTE);
                    // MOT-444: find a test bench parameter with a value of the CAM file's path.
                    foreach (var param in testbench.Children.ParameterCollection)
                    {
                        if (param.Name.ToUpper().Contains("CAM "))
                        {
                            if (param.Attributes.Value.ToUpper().Contains(".CAM"))
                            {
                                camFilePath = param.Attributes.Value;
                            }
                        }
                    }
                },
                transactiontype_enum.TRANSACTION_NON_NESTED,
                abort: true);

                // The input files to the CAM and Eagle ULP scripts should already be in the pathDesign folder.
                // We need to move them into the project results/<random> folder, which is under parameters.OutputDirectory.
                // See also MOT-444.
                string[] filesToMove = new string[] { "BomTable.csv", "schema.brd", "schema.sch", "reference_designator_mapping_table.html" };
                foreach (string fileName in filesToMove)
                {
                    string srcPath = Path.Combine(pathDesign, fileName);
                    string dstPath = Path.Combine(parameters.OutputDirectory, fileName);

                    if (File.Exists(dstPath))
                    {
                        File.Delete(dstPath);
                    }

                    if (File.Exists(srcPath))
                    {
                        Logger.WriteInfo("About to copy \"{0}\" to \"{1}\".", srcPath, dstPath);
                        File.Copy(srcPath, dstPath);
                    }
                    else
                    {
                        Logger.WriteWarning("The file {0} does not exist.", srcPath);
                    }
                }
                // Set the current directory to the project directory, so that
                // the CAM file's path can be relative to it.
                try
                {
                    // Get the current directory. 
                    string path = this.mainParameters.ProjectDirectory;
                    System.IO.Directory.SetCurrentDirectory(path);
                    Logger.WriteInfo("The current directory is {0}", path);
                }
                catch (Exception e)
                {
                    Console.WriteLine("GetCurrentDirectory() failed: {0}", e.ToString());
                }

                // Copy the CAM file to a well-known name in the output directory.
                {
                    string srcPath = camFilePath;
                    string dstPath = Path.Combine(parameters.OutputDirectory, "pcb_mfg.cam");

                    if (File.Exists(dstPath))
                    {
                        File.Delete(dstPath);
                    }

                    if (File.Exists(srcPath))
                    {
                        Logger.WriteInfo("About to copy \"{0}\" to \"{1}\".", srcPath, dstPath);
                        File.Copy(srcPath, dstPath);
                    }
                    else
                    {
                        Logger.WriteWarning("The file {0} does not exist.", srcPath);
                    }
                }


                if (this.result.Success)
                {
                    this.result.RunCommand = "cmd /c dir";     // Dummy first command of job manager for master interpreter.
                    Logger.WriteInfo("Generated files are here: <a href=\"file:///{0}\" target=\"_blank\">{0}</a>", this.mainParameters.OutputDirectory);
                    Logger.WriteInfo("CyPhy2PCBMfg has finished. [SUCCESS: {0}, Labels: {1}]", this.result.Success, this.result.Labels);
                }
                else
                {
                    Logger.WriteError("CyPhy2PCBMfg failed! See error messages above.");
                }
            }
            catch (Exception ex)
            {
                this.Logger.WriteError("Exception was thrown : {0}", ex.ToString());
                this.result.Success = false;
            }
            finally
            {
                if (disposeLogger && this.Logger != null)
                {
                    this.DisposeLogger();
                }
            }

            return this.result;
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
			// Get RootFolder
			IMgaFolder rootFolder = project.RootFolder;
            
            // To use the domain-specific API:
            //  Create another project with the same name as the paradigm name
            //  Copy the paradigm .mga file to the directory containing the new project
            //  In the new project, install the GME DSMLGenerator NuGet package (search for DSMLGenerator)
            //  Add a Reference in this project to the other project
            //  Add "using [ParadigmName] = ISIS.GME.Dsml.[ParadigmName].Classes.Interfaces;" to the top of this file
            // if (currentobj != null && currentobj.Meta.Name == "KindName")
            // [ParadigmName].[KindName] dsCurrentObj = ISIS.GME.Dsml.[ParadigmName].Classes.[KindName].Cast(currentobj);			
        }

        #endregion

        #region IMgaComponentEx Members

        MgaGateway MgaGateway { get; set; }
        GMELogger Logger { get; set; }

        public void InvokeEx(MgaProject project, MgaFCO currentobj, MgaFCOs selectedobjs, int param)
        {
            if (!enabled)
            {
                return;
            }

            try
            {
                if (Logger == null)
                {
                    Logger = new GMELogger(project, ComponentName);
                }
                MgaGateway = new MgaGateway(project);
                project.CreateTerritoryWithoutSink(out MgaGateway.territory);

                this.mainParameters = new InterpreterMainParameters()
                {
                    Project = project,
                    CurrentFCO = currentobj as MgaFCO,
                    SelectedFCOs = selectedobjs,
                    StartModeParam = param
                };

                Main(this.mainParameters);
            }
            catch (Exception ex)
            {
                Logger.WriteError("Exception: {0}<br> {1}", ex.Message, ex.StackTrace);
            }
            finally
            {
                if (MgaGateway != null &&
                    MgaGateway.territory != null)
                {
                    MgaGateway.territory.Destroy();
                }
                MgaGateway = null;
                project = null;
                currentobj = null;
                selectedobjs = null;
                DisposeLogger();
                GC.Collect();
                GC.WaitForPendingFinalizers();
			}
		}

        public void DisposeLogger()
        {
            if (Logger != null)
            {
                Logger.Dispose();
                Logger = null;
            }
        }

        //private Queue<Tuple<string, TimeSpan>> runtime = new Queue<Tuple<string, TimeSpan>>();
        private DateTime startTime = DateTime.Now;
        /*private void PrintRuntimeStatistics()
        {
            Logger.WriteDebug("======================================================");
            Logger.WriteDebug("Start time: {0}", this.startTime);
            foreach (var time in this.runtime)
            {
                Logger.WriteDebug("{0} = {1}", time.Item1, time.Item2);
            }
            Logger.WriteDebug("======================================================");
        }*/

        /// <summary>
        /// Parameter of this run.
        /// </summary>
        private IInterpreterMainParameters mainParameters { get; set; }

        /// <summary>
        /// Result of the latest run of this interpreter.
        /// </summary>
        private InterpreterResult result = new InterpreterResult();
        private void UpdateSuccess(string message, bool success)
        {
            this.result.Success = this.result.Success && success;

            //this.runtime.Enqueue(new Tuple<string, TimeSpan>(message, DateTime.Now - this.startTime));
            if (success)
            {
                Logger.WriteInfo("{0} : OK", message);
            }
            else
            {
                Logger.WriteError("{0} : FAILED", message);
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
    }
}

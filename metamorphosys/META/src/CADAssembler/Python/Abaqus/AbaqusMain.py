# Copyright (C) 2013-2015 MetaMorph Software, Inc

# Permission is hereby granted, free of charge, to any person obtaining a
# copy of this data, including any software or models in source or binary
# form, as well as any drawings, specifications, and documentation
# (collectively "the Data"), to deal in the Data without restriction,
# including without limitation the rights to use, copy, modify, merge,
# publish, distribute, sublicense, and/or sell copies of the Data, and to
# permit persons to whom the Data is furnished to do so, subject to the
# following conditions:

# The above copyright notice and this permission notice shall be included
# in all copies or substantial portions of the Data.

# THE DATA IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
# IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
# FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
# THE AUTHORS, SPONSORS, DEVELOPERS, CONTRIBUTORS, OR COPYRIGHT HOLDERS BE
# LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
# OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
# WITH THE DATA OR THE USE OR OTHER DEALINGS IN THE DATA.  

# =======================
# This version of the META tools is a fork of an original version produced
# by Vanderbilt University's Institute for Software Integrated Systems (ISIS).
# Their license statement:

# Copyright (C) 2011-2014 Vanderbilt University

# Developed with the sponsorship of the Defense Advanced Research Projects
# Agency (DARPA) and delivered to the U.S. Government with Unlimited Rights
# as defined in DFARS 252.227-7013.

# Permission is hereby granted, free of charge, to any person obtaining a
# copy of this data, including any software or models in source or binary
# form, as well as any drawings, specifications, and documentation
# (collectively "the Data"), to deal in the Data without restriction,
# including without limitation the rights to use, copy, modify, merge,
# publish, distribute, sublicense, and/or sell copies of the Data, and to
# permit persons to whom the Data is furnished to do so, subject to the
# following conditions:

# The above copyright notice and this permission notice shall be included
# in all copies or substantial portions of the Data.

# THE DATA IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
# IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
# FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
# THE AUTHORS, SPONSORS, DEVELOPERS, CONTRIBUTORS, OR COPYRIGHT HOLDERS BE
# LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
# OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
# WITH THE DATA OR THE USE OR OTHER DEALINGS IN THE DATA.  

"""

AbaqusMain.py, version 1.4.0

For use with Abaqus 6.13-1 (Python 2.6.2).

Created by Ozgur Yapar   <oyapar@isis.vanderbilt.edu>
           Robert Boyles <rboyles@isis.vanderbilt.edu>

    - The main script which takes care of pre-processing,
      solving and post-processing.
      
    - Can be run with the following parameters:
        1) Mesh Only - Build the assembly and generate mesh with CAE file
        2) Mesh & Boundary Conditions - Create mesh and PNG files of
                boundary conditions applied.
        3) Static/Standard analysis
        4) Explicit Dynamic analysis
        5) Implicit Dynamic analysis
        6) Modal analysis
    
    - Static jobs can be run with adaptive remeshing functionality.
        - Will be expanded to include dynamic analysis at time TBD.
    - FEA can be kicked off to run as standalone application
        - Build model and submit jobs through CAE.
    OR
    - FEA can be kicked off by SOT with ADAMS LOD files as an input.
        - Create INP files and run analysis on those.
        
    - ADAMS-to-Abaqus can run with options (1),(3),(4).
        - No adaptive re-meshing (8/29/14)
        - (1) only generates mesh quality CSV files as no CAE is used.
    - Standalone Abaqus can use (1),(2),(3),(5), and (6).
"""

from abaqus import *
from abaqusConstants import *
from caeModules import *
from numpy import *
from jobMessage import *
from optparse import *
from operator import xor
import multiprocessing
import math, re, sys, json, os
import interaction, visualization, regionToolset, job, mesh
import numpy.linalg as LA
import xml.etree.ElementTree as ET
import string as STR
import uuid, ctypes
import csv
import _winreg
import odbAccess
import shutil
from collections import defaultdict
import glob
import traceback
from subprocess import Popen
import pickle
import time
import logging

# Get directories first relative to META_PATH. This is needed for imports to work
with _winreg.OpenKey(_winreg.HKEY_LOCAL_MACHINE, r'Software\META', 0, _winreg.KEY_READ | _winreg.KEY_WOW64_32KEY) as key:
    META_PATH = _winreg.QueryValueEx(key, 'META_PATH')[0]
abaqusDir = os.path.join(META_PATH,'bin','CAD', 'Abaqus')
sys.path.append(abaqusDir)

from AbaqusParse import *
from AbaqusGeometry import *
from AbaqusProcessData import *
from AbaqusDataCheck import checkConstraintErrors
from AbaqusDict import *
from AbaqusSTEP import *
from AbaqusCAE import *
from AbaqusRun import runAbaqusDispatch
from AbaqusCAE_ADAMS import AbaqusCAE_ADAMS

logger = logging.getLogger()

try:
    materialLibInterfacePath = os.path.join(META_PATH,"bin","Python27","Lib","site-packages")
    sys.path.insert(0, materialLibInterfacePath)
    import sitecustomize
    from MaterialLibraryInterface import LibraryManager

    path = ctypes.c_wchar_p(chr(0x00) * 256)
    FOLDERID_Documents = ctypes.c_char_p(uuid.UUID("ED4824AF-DCE4-45A8-81E2-FC7965083634").bytes_le)
    ctypes.windll.shell32.SHGetKnownFolderPath(FOLDERID_Documents, 0, None, ctypes.byref(path))
    materialLibPath = os.path.join(path.value,"META Documents","MaterialLibrary","material_library.json")

    library_manager = LibraryManager(materialLibPath)

except Exception as e:
    logger.error('ERROR: Error during finding the material library\n')
    logger.error(STR.join(traceback.format_exception(*sys.exc_info())))
    raise
    
from AbaqusParse import *
from AbaqusGeometry import *
from AbaqusProcessData import *
from AbaqusDataCheck import checkConstraintErrors
from AbaqusDict import *
from AbaqusSTEP import *
from AbaqusCAE import *
from AbaqusRun import runAbaqusDispatch
from AbaqusCAE_ADAMS import AbaqusCAE_ADAMS

def setup_logger():
    logger = logging.getLogger()
    logger.setLevel(logging.DEBUG)
    fh = logging.FileHandler(os.path.join(os.getcwd(), "log", "AbaqusCmd.log"),'w')
    fh.setLevel(logging.DEBUG)
    formatter = logging.Formatter('%(asctime)s: %(levelname)s - %(message)s')
    fh.setFormatter(formatter)
    logger.addHandler(fh)
    return logger
    

def check_for_adams(runAdams):
    root = os.getcwd()
    reportFile = 'testbench_manifest.json'
    kinComValFile = "kinComputedValues.xml"
    reportPath = os.path.join(root, reportFile)
    kinComValPath = os.path.join(root, kinComValFile)
    with open(reportPath, 'r') as json_data:
        data = json.load(json_data)
    try:
        adamsData = data["Dependencies"]
        adamsPath = adamsData[0]["Directory"]
        runAdams = True
    except IndexError:
        logger.info('No ADAMS analysis load file found, executing standard analysis! \n')
    if runAdams:
        os.chdir(adamsPath)
        for hgx in glob.glob("*.lod"):
            shutil.copy(hgx,root)
        for hgx in glob.glob("*.xml"):
            if hgx == "ComputedValues.xml":
                shutil.copy(hgx,kinComValPath)
            if hgx == "CADAssembly_metrics.xml":
                shutil.copy(hgx, os.path.join(root, 'CADAssembly_metrics.xml'))
        logger.info('ADAMS analysis load files found! \n')
    os.chdir(root)
    logger.info("**********************************************************************************" + '\n')
    
    return runAdams
    
    
def main():
    logger.info("**********************************************************************************" + '\n')
    logger.info("**********************************************************************************" + '\n')
    logger.info("************************STARTING FEA MODEL-BASED v1.3.0***************************" + '\n')
    logger.info("**********************************************************************************" + '\n')
    logger.info("**********************************************************************************" + '\n')
    logger.info("Initializing Abaqus/CAE" + '\n')
    logger.info("**********************************************************************************" + '\n')
    logger.info("**********************************************************************************" + '\n')
    
    # take command line arguments
    usage = "usage: abaqus cae script=CyPhy2AbaqusCmd.py -- [options]"
    parser = OptionParser(usage=usage)
    parser.add_option("-o","--meshOnly", default=False, action="store_true",
        help="""disable assembly of faces from datum points and creation of loads and BCs
            (necessary for META 13.13)""")
    parser.add_option("-b","--meshAndBC", default=False, action="store_true",
        help="Create mesh and generate loads/boundary conditions. Do not run analysis.")
    parser.add_option("-s","--standard", default=False, action="store_true",
        help="run a standard FEA analysis")
    parser.add_option("-i","--dynamicImplicit", default=False, action="store_true",
        help="run a dynamic implicit FEA analysis")
    parser.add_option("-e","--dynamicExplicit", default=False, action="store_true",
        help="run a dynamic explicit FEA analysis")
    parser.add_option("-m","--modal", default=False, action="store_true",
        help="run a modal analysis")
    parser.add_option("-p","--parallelCores", default=1, type=int,
        help="number of CPU cores to use in solver")
    parser.add_option("-r","--ramAllocated", default=90, type=int,
        help="integer amount of memory allocated to solver, defined in units memoryUnits")
    parser.add_option("-u","--memoryUnits", default="PERCENTAGE",
        choices=["PERCENTAGE","MEGA_BYTES","GIGA_BYTES"],
        help="units of memory allocated to solver")
    parser.add_option("-t","--separationTolerance", default=1E-3, type=float,
        help="tolerance for separation between faces when creating tie constraints")
    parser.add_option("-q","--errorIndicator", default="ENDENERI",
        choices=["MISESERI","ENDENERI","CPRESSERI","CSHEARERI","PEEQUERI",
                 "PEERI","CEERI","HFLERI","EFLERI","EPGERI"],
        help="""error indicator variable for adaptive meshing;
                not all variables are supported yet
                (see Section 4.1.4 of the Abaqus Analysis Manual)""")
    parser.add_option("-v","--rigidParts", default=True, action="store_true",
        help="use Rigid parts as specified in CADAssembly.xml")

    if sys.argv[2] == '-noGUI':
        (args,testBenchArg) = parser.parse_args(sys.argv[8:])
    else:
        (args,testBenchArg) = parser.parse_args(sys.argv[6:])

    # Assign variable to the blank model produced at startup
    root = os.getcwd()
    myModel = mdb.models['Model-1']
    myAsm = myModel.rootAssembly

    # Determine if ADAMS analysis preceeded this FEA run
    runAdams = check_for_adams(runAdams=False)
    
    # Parse necessary information from the XML files  
    (xmlName, feaXML, cadAssemblyXML, metricSetXML, metricsSetXML,
     metricsComponentSetXML, maxNumberIter, analysisConstraintSetXML, thermalSetXML) \
        = parseCADAssemblyXML(args)
    
    # TODO: this first check should only be temporary.
    if maxNumberIter > 1 and any([args.dynamicImplicit, args.dynamicExplicit, runAdams]):
        msg = "Adaptive remeshing requested with invalid analysis type. Currently " + \
              "supported type is static/standard standalone FEA."
        logger.error(msg)
        raise Exception(msg)
        
    if runAdams and args.dynamicImplicit or not runAdams and args.dynamicExplicit:
        msg = "Invalid combination of dynamic analysis type and ADAMS analysis data. " + \
              "Available options are Standalone FEA with Implicit Dynamics or " + \
              "FEA with ADAMS load data with Explicit Dynamics."
        logger.error(msg)
        raise Exception(msg)
        
    elif runAdams and args.meshAndBC or runAdams and args.modal:
        msg = "ADAMS-to-Abaqus runs can not have the Abaqus analysis set to " + \
              "MeshAndBoundaryConditions or Modal."
        logger.error(msg)
        raise Exception(msg)
        
    if thermalSetXML and any([args.dynamicImplicit, args.dynamicExplicit, args.modal, runAdams]):
        msg = "Coupled Thermal analysis currently only supported for standalone FEA static runs."
        logger.error(msg)
        raise Exception(msg)
    
    # Parse STEP file for raw assembly data
    (stepPath, testBenchName, step) = parseStep(cadAssemblyXML)  
    
    # Parse CADMetrics file for CreateAssembly program data
    (xmlMetricsName, metricComponentsXML, metricComponentXML, 
         metricAssembliesXML, metricAssemblyXML, 
         metricCADComponentsXML, jointsXML) = parseCADAssemblyMetricsXML(runAdams)
    
    # Determine unit length of CAD files    
    unitLength = getUnitLength(metricComponentsXML)
    
    if runAdams:
        # Parse kinematic computed values (ADAMS data)
        (anchorID, anchorPointID) = parseKinComputedValuesXML()

    # Calculate scale factor (with respect to meters)
    (unitScale, unitShort) = calcGeoScaleFac(unitLength)
    conv = generateConvDict(unitScale)
    
    # Generate necessary general dictionaries
    (instRef, instIndex, instAssemblyRef, instAssemblyIndex,
      rigidPartMasses, rigidPartVolumes, rigidPartDensities,
      rigidParts, rigidPartPresent, Jan24_deactivate_rigidity) \
           = generateInstRef(metricComponentsXML, metricCADComponentsXML, 
                             xmlMetricsName, jointsXML, xmlName, runAdams, args)

    libraryManager = parseMaterialLibrary()    
    mtrlRef = generateMtrlRef(libraryManager, metricComponentsXML, 
                              metricCADComponentsXML, thermalSetXML, rigidParts, unitScale, conv)
                              
    (metricRef, metricRef2, metricRef3, allAssembly) \
        = generateMetricRef(metricSetXML, metricsComponentSetXML, instIndex, instAssemblyIndex)
    if not runAdams:
        (loadBCLib, accel) = generateLoadBCLib(analysisConstraintSetXML, feaXML, thermalSetXML, conv)
    else:
        (loadBCLib, accel) = ([],[])
    superRef = generateSuperRef(instRef, instAssemblyRef, mtrlRef, metricRef, metricRef2, metricRef3,
                                instIndex, loadBCLib, unitLength, allAssembly) 
    
    # Process the data obtained from the step file
    (pointsBySR, inst2SR, localTMs, localTVs, topLevelAsm, subAsms,
         asmParts) = processStep(stepPath, testBenchName) 
    
    # Generate necessary geometric dictionaries
    (localCoords, CGs) = generateLocalCoords(inst2SR, pointsBySR,
                                             metricCADComponentsXML, metricComponentsXML)
    logger.info("Creating a new dictionary with points translated into the global coordinate system" + '\n')
    datumPointDict = coordTransform(localTMs,localTVs,topLevelAsm,
                                    subAsms,asmParts,localCoords)
    logger.info("**********************************************************************************" + '\n')

    # Start building Abaqus model
    createParts(myModel, step, CGs)  
    deleteInvalidParts(myModel)

    # Define step - standard default
    if thermalSetXML:
        (myStep, amp, analysisStepName) = defineCoupledThermalDisplacementStep(myModel)
    else:
        if args.dynamicImplicit or args.dynamicExplicit:
            (myStep, amp, analysisStepName) = defineDynamicStep(myModel, runAdams)
        elif args.modal:
            (myStep, amp, analysisStepName) = defineModalStep(myModel)
        else:
            (myStep, amp, analysisStepName) = defineStaticStep(myModel)
    
    defineMaterials(myModel, mtrlRef)
        
    if not runAdams:
        assignSections(instRef, myModel, myAsm, subAsms, asmParts)
        mergeAssemblyComponents(myAsm, instAssemblyRef, asmParts)
        defineContactInteractions(myModel)
        defineRigidBodyConstraints(instRef, Jan24_deactivate_rigidity,
                                   instIndex, myAsm, myModel)
        EliminateOverlaps(instRef, rigidParts, myAsm, myModel)
        
        meshInstances(40.0, unitShort, instRef, instAssemblyRef, myAsm,
                      feaXML, metricComponentsXML, metricAssemblyXML, args) 
        
        if not args.meshOnly:
            apply_loads_and_bcs(myModel, myAsm, myStep, instRef, loadBCLib, instIndex, instAssemblyIndex,
                                datumPointDict, accel, amp, args, Jan24_deactivate_rigidity, thermalSetXML)
    
           

    if runAdams:
        EliminateOverlaps(instRef, rigidParts, myAsm, myModel)
        
        (includeAnchoredPart, anchoredPart) = decideAnchoredPart(jointsXML,
                        anchorID, anchorPointID, instRef, myModel)

        AbaqusCAE_ADAMS(instRef, includeAnchoredPart, anchoredPart, myModel,
                        myAsm, 40.0, unitShort, jointsXML, 
                        feaXML, metricComponentsXML, metricAssemblyXML,
                        args.parallelCores, args.ramAllocated, args, instAssemblyRef, asmParts)
    else:
        includeAnchoredPart, anchoredPart = None, None
                   
    logger.info("**********************************************************************************" + '\n')               
                   
    if not runAdams:
        if not args.meshOnly:
            connectParts(myModel, myAsm, instRef, instAssemblyRef, cadAssemblyXML,
                         asmParts, analysisStepName, args)
            try:
                CreateLOADSBCPNG('PreProcessing', myAsm, analysisStepName, os.getcwd())
            except:
                logger.info('ERROR: Error during creating BC PNG files - Program will keep executing \n')
                pass
            createCAE(os.getcwd(), testBenchName, args)
            
            checkConstraintErrors(myModel, args)
            
            exportNastranDeck()
      
    if not args.meshOnly and not args.meshAndBC:    
        jobName = runAbaqusDispatch(myModel, myAsm, superRef, analysisStepName, rigidPartPresent,
                                     rigidParts, args, includeAnchoredPart, anchoredPart, maxNumberIter)

    if not runAdams:
        if args.dynamicImplicit or args.dynamicExplicit or args.standard:
            afterJob(jobName, superRef, analysisStepName, runAdams, thermalSetXML)
        elif args.modal:
            afterJobModal(jobName,analysisStepName)
    
    # To comply with file location for Deck-Based solution.
    # abqdir = os.path.join(root,'Analysis','Abaqus')
    # if not os.path.exists(abqdir):
        # os.makedirs(abqdir)
    # shutil.move(os.path.join(root,'ComputedValues.xml'), \
                # os.path.join(abqdir))
    
if __name__ == "__main__":
    logger = setup_logger()
    main()
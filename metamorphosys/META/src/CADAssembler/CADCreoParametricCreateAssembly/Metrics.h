#ifndef METRICS_H
#define METRICS_H

namespace isis
{
	const string METRICS_FILE_ERROR_string = "METRICS FILE ERROR";

	/*! \file Metrics.h 
    \brief This file declares a function (OutputCADMetricsToXML) that outputs an XML file with metrics about assemblies and detail parts.

	Detailed Description:

	This function takes as input information about assemblies, and creates an XML file containing metrics 
	about the assemblies and their sub-assemblies/detail parts.  in_CADAssemblies contains meta data about 
	the assemblies. The meta data contains a key into in_CADComponentData_map, where the key indicates the 
	top-level component in the assembly.  in_CADComponentData_map contains a list of children, if any, and 
	thus starting with the top-level component the entire assembly hierarchy can be traversed.

	The metrics information includes data such as surface area, volume, density, mass, mass moment of 
	inertia tensor, and principal moments of inertia ... 

	The schema for the XML file is located at 
	<SVN>\trunk\src\CADCreo\CADCreoParametricCreateAssembly\Schema\CADMetrics.xsd

	Pre-Conditions: 

-	in_CADAssemblies must be populated with the meta data for at least one assembly.  

-	in_CADComponentData_map must be populated with the complete information about the assemblies, 
	sub-assemblies, and detail parts comprising the in_CADAssemblies assemblies.  This complete information 
	would have been populated by the BuildAssembly function in this VisualStudio project.

	Post-Conditions:

-	If in_MeticsOutputXML_PathAndFileName does not contain a valid path and file name or if there was not 
	write access to the file, then std::exception would be thrown. Note: the path and file name can contain spaces.

-	If a Creo model is malformed in such a way that the metric information cannot be collected via Creo toolkit 
	functions, then isis::application_exception would be thrown.

-	If no exceptions, then the file identified by in_MeticsOutputXML_PathAndFileName would be populated with 
	metric information about the assemblies, sub-assemblies, and detail parts. 

	*/

	void OutputCADMetricsToXML( 
							const isis::CADAssemblies						&in_CADAssemblies,
							std::map<std::string, isis::CADComponentData>	&in_CADComponentData_map,  
							const std::string								&in_MeticsOutputXML_PathAndFileName, 
							bool											in_OutputJoints,
							bool											&out_ErrorOccurred )
							throw (isis::application_exception, std::exception); 


	void OutputCADMetricsToXML_Driver( 
							bool											in_regenerationSucceeded_ForAllAssemblies,
							bool											in_OutputJoints,
							const isis::CADAssemblies						&in_CADAssemblies,
							std::map<std::string, isis::CADComponentData>	&in_CADComponentData_map,  
							const std::string								&in_MeticsOutputXML_PathAndFileName,
							const std::string								&in_LogFile_PathAndFileName )
																	throw (isis::application_exception, std::exception); 


	void RetrieveUnits_withDescriptiveErrorMsg( 
					const std::string				&in_ComponentInstanceID,
					const isis::MultiFormatString	&in_ModelName,				
					ProMdl							in_Model,
					std::string						&out_DistanceUnit_ShortName,
					std::string						&out_DistanceUnit_LongName,
					
					std::string						&out_MassUnit_ShortName,
					std::string						&out_MassUnit_LongName,

					std::string						&out_ForceUnit_ShortName,
					std::string						&out_ForceUnit_LongName,

					std::string						&out_TimeUnit_ShortName,
					std::string						&out_TimeUnit_LongName,

					std::string						&out_TemperatureUnit_ShortName, 
					std::string						&out_TemperatureUnit_LongName )
														throw(isis::application_exception);


} // end namespace isis

#endif   // END METRICS_H
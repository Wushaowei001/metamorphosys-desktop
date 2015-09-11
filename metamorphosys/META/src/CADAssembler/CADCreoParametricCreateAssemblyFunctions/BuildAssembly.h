#ifndef BUILD_ASSEMBLY_H
#define BUILD_ASSEMBLY_H
//#include <AssemblyInterface.hxx>
#include <isis_application_exception.h>
#include <CommonStructures.h>
#include <AssembleUtils.h>
#include <map>
#include "CadFactoryAbstract.h"

//using namespace std;

namespace isis
{


void BuildAssembly( 
		cad::CadFactoryAbstract								&in_factory,
		const std::string									&in_AssemblyComponentID, 
		const std::string									&in_WORKING_DIR,
		bool												in_SaveAssembly,
		std::map<std::string, isis::CADComponentData>		&in_CADComponentData_map,
		bool												&out_RegenerationSucceeded,
		std::vector<isis::CADCreateAssemblyError>			&out_ErrorList,
		bool												in_AllowUnconstrainedModels = false)
					throw (isis::application_exception);

//void CopyModels(const std::map<std::string, std::string>	  &in_ToPartName_FromPartName )
//																	throw (isis::application_exception);
void CopyModels(const std::vector<CopyModelDefinition>	&in_FromModel_ToModel )
																	throw (isis::application_exception);

void ReadInitialPositions(std::map<string, double*> &out_positions, const std::string &in_filename);


void Add_Subassemblies_and_Parts( 
		cad::CadFactoryAbstract				&	in_factory,
		ProMdl								in_p_asm,
		const std::string					&	in_ParentName,
		const std::list<std::string>		&	in_Components,
		std::map<string, isis::CADComponentData>	&in_CADComponentData_map,
		int									&in_out_addedToAssemblyOrdinal)
					throw (isis::application_exception);

void	RegenerateModel( ProSolid in_p_asm,
						 const std::string in_ParentName,
						 const std::string in_ParentComponentID,
						 bool  &out_RegenerationSucceeded,
						 bool  in_PresentDetailedErrorMessage = false)
												throw (isis::application_exception);

} // end namespace isis


#endif // ASSEMBLY_TREE_PARSER_H
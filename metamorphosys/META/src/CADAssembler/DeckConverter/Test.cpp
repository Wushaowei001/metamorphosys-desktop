// Test.cpp : Defines the entry point for the console application.
//

#include <stdio.h>
#include <iostream>
#include <string>
#include <vector>
#include <io.h>     // For access().
#include <sys/types.h>  // For stat().
#include <sys/stat.h>   // For stat().

#include "../CADCommonFunctions/Nastran.h"
#include "DeckConverter.h"

bool DirectoryExists( const char* absolutePath ){

    if( _access( absolutePath, 0 ) == 0 ){

        struct stat status;
        stat( absolutePath, &status );

        return (status.st_mode & S_IFDIR) != 0;
    }
    return false;
}

int main(int argc, char* argv[])
{
	if (argc < 4)
	{
		std::cout << "-i <Nastran Input Deck File>   -o <Output Directory>" << std::endl;
		return 0;
	}

	try
	{
		std::string nasFile;
		std::string abaqusFile;
		for (int i = 1; i < argc; i++)
		{
			std::string anArg = argv[i];

			if (anArg == "-i")
			{
				nasFile = argv[i+1];
			}
			if (anArg == "-o")
			{
				abaqusFile = argv[i+1];
			}

		}
		
		std::string::size_type pos = nasFile.find(".nas");
		if (pos == std::string::npos)
			std::cout << "Incorrect File Name: " << nasFile << std::endl;
		else
		{
			if (!DirectoryExists(abaqusFile.c_str()))
				std::cout << "Error: Must specify an output directory!" << std::endl;

			isis_CADCommon::NastranDeck nasDeck;
			nasDeck.ReadNastranDeck(nasFile);
			//isis::CalculixConverter converter;
			//converter.ConvertNastranDeck(commonDS, abaqusFile);

			isis::ElmerConverter converter;
			converter.ConvertNastranDeck(nasDeck, abaqusFile);
		}
	}
	catch(isis::application_exception& e)
	{
		std::cout << "ISIS Application Exception: " << e.what() << std::endl;
	}

	return 0;
}


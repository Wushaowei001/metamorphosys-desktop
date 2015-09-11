#include <MultiFormatString.h>
#include <sstream>
#include <ProUtil.h>


namespace isis
{
	////////////////////////////////////////////////////////////////////////////////////////////////////////
	std::string MaxStringLengthError( const std::string &in_String, int in_MaxNumberofChars) 
	{
		std::stringstream errorString;
		errorString << "MultiFormatString::MultiFormatString, String: " << in_String << ", exceeded max number of characters. Max allowed characters: " 
					<< in_MaxNumberofChars << ", actual Number of characters: " << in_String.size(); 
		return errorString.str();
	}
	////////////////////////////////////////////////////////////////////////////////////////////////////////
	std::string isis_WideCharToString( const wchar_t *in_WideCharArray )
	{
		int string_length = static_cast<int>(wcslen(in_WideCharArray));
		char  *tempNarrowCharArray = new char[string_length + 1];
		ProWstringToString(tempNarrowCharArray, (wchar_t *)in_WideCharArray);

		return tempNarrowCharArray;
	}
	////////////////////////////////////////////////////////////////////////////////////////////////////////
	bool ExceededMaxStringLength( const std::string &in_String, int in_MaxNumberChars )
	{
		if ( in_MaxNumberChars == CREO_STRING_DO_NOT_CHECK_NUM_CHARS )
		{
			return false;
		}
		else
		{
			if ( in_String.size() > in_MaxNumberChars )
				return true;
			else
				return false;
		}
	}
	////////////////////////////////////////////////////////////////////////////////////////////////////////
	bool ExceededMaxStringLength( const char *in_NarrowCharArray, int in_MaxNumberChars )
	{
		if ( in_MaxNumberChars == CREO_STRING_DO_NOT_CHECK_NUM_CHARS )
		{
			return false;
		}
		else
		{
			int string_length = static_cast<int>(strlen(in_NarrowCharArray));
			if ( string_length > in_MaxNumberChars )
				return true;
			else
				return false;
		}
	}
	////////////////////////////////////////////////////////////////////////////////////////////////////////
	bool ExceededMaxStringLength( const wchar_t *in_WideCharArray, int in_MaxNumberChars )
	{
		if ( in_MaxNumberChars == CREO_STRING_DO_NOT_CHECK_NUM_CHARS )
		{
			return false;
		}
		else
		{
			int string_length = static_cast<int>(wcslen(in_WideCharArray));
			if ( string_length > in_MaxNumberChars )
				return true;
			else
				return false;
		}
	}
	////////////////////////////////////////////////////////////////////////////////////////////////////////
	///////////////////////////////////// Class MultiFormatString ////////////////////////////////////////////
	////////////////////////////////////////////////////////////////////////////////////////////////////////
	void MultiFormatString::setNarrowString()
	{
		if ( narrowString_Defined ) 
			return;
		else if ( narrowCharArray )
		{
			narrowString = narrowCharArray;
			narrowString_Defined = true;
		}
		else if ( wideCharArray )
		{
			int buffer_length = static_cast<int>(wcslen(wideCharArray) + 1);
			narrowCharArray = new char[buffer_length];
			ProWstringToString( narrowCharArray, wideCharArray);
			narrowString = narrowCharArray;
			narrowString_Defined = true;
		}
		else
		{
			// If reached here, there is a bug in MultiFormatStringType.  At this point, at least one string type should be defined.
			std::stringstream errorString;
			errorString << "MultiFormatStringType::setNarrowString, did not find a narrowString, narrowCharArray, or wideCharArray defined.  " 
						<< "This condition would be caused by a bug in MultiFormatStringType class.";
			  throw isis::application_exception(errorString.str().c_str());
		}
	}
	////////////////////////////////////////////////////////////////////////////////////////////////////////
	void MultiFormatString::setNarrowCharArray()
	{
		if ( narrowCharArray ) 
		{
			return;
		}
		else if ( narrowString_Defined )
		{		
			int buffer_length = static_cast<int>(narrowString.size() + 1);
			narrowCharArray = new char[buffer_length];
			strcpy_s( narrowCharArray, buffer_length, narrowString.c_str());
		}
		else if ( wideCharArray )
		{
			int buffer_length = static_cast<int>(wcslen(wideCharArray) + 1);
			narrowCharArray = new char[buffer_length];
			ProWstringToString(narrowCharArray,wideCharArray);
		}
		else
		{
			// If reached here, there is a bug in MultiFormatStringType.  At this point, at least one string type should be defined.
			std::stringstream errorString;
			errorString << "Error: MultiFormatStringType::setNarrowCharArray, did not find a narrowString, narrowCharArray, or wideCharArray defined.  " 
						<< "This condition would be caused by a bug in MultiFormatStringType class.";
			  throw isis::application_exception(errorString.str().c_str());
		}
	}
	////////////////////////////////////////////////////////////////////////////////////////////////////////
	void MultiFormatString::setWideCharArray()
	{
		if ( wideCharArray ) 
		{
			return;
		}
		else if ( narrowString_Defined )
		{		
			int buffer_length = static_cast<int>(narrowString.size() + 1);
			//std::cout << std::endl << "SIZE buffer_length: " << buffer_length;
			wideCharArray = new wchar_t[buffer_length];
			ProStringToWstring( wideCharArray, (char *) narrowString.c_str());
		}
		else if ( narrowCharArray )
		{
			int buffer_length = static_cast<int>(strlen(narrowCharArray) + 1);
			wideCharArray = new wchar_t[buffer_length];
			ProStringToWstring( wideCharArray, narrowCharArray);
		}
		else
		{
			// If reached here, there is a bug in MultiFormatStringType.  At this point, at least one string type should be defined.
			std::stringstream errorString;
			errorString << "Error: MultiFormatStringType::setWideCharArray, did not find a wideString, wideCharArray, or wideCharArray defined.  " 
						<< "This condition would be caused by a bug in MultiFormatStringType class.";
			  throw isis::application_exception(errorString.str().c_str());
		}
	}
	////////////////////////////////////////////////////////////////////////////////////////////////////////
	void MultiFormatString::setString(const std::string &in_String )
	{
		narrowString = in_String;
		narrowString_Defined = true;
	}
	////////////////////////////////////////////////////////////////////////////////////////////////////////
	void MultiFormatString::setString(const char *in_NarrowCharArray )
	{
		int buffer_length = static_cast<int>(strlen(in_NarrowCharArray) + 1);
		narrowCharArray = new char[buffer_length];
		strcpy_s( narrowCharArray, buffer_length, in_NarrowCharArray); 
	}
	////////////////////////////////////////////////////////////////////////////////////////////////////////
	void MultiFormatString::setString(const wchar_t *in_WideCharArray )
	{
		int buffer_length= static_cast<int>(wcslen(in_WideCharArray) + 1);
		wideCharArray = new wchar_t[buffer_length ];
		wcscpy_s( wideCharArray, buffer_length, in_WideCharArray); 
	}
	////////////////////////////////////////////////////////////////////////////////////////////////////////
	void MultiFormatString::setAllStrings()
	{
		setNarrowCharArray();
		setWideCharArray();
		setNarrowString();

	}
	////////////////////////////////////////////////////////////////////////////////////////////////////////
	MultiFormatString::MultiFormatString( int in_MaxNumberChars ) 
										:	narrowString(""), 
											narrowString_Defined(false),
											narrowCharArray(NULL), 
											wideCharArray(NULL),
											maxNumberChars(in_MaxNumberChars)
	{
		setString(std::string(""));
		setAllStrings();
	}
	////////////////////////////////////////////////////////////////////////////////////////////////////////
	MultiFormatString::MultiFormatString( const std::string &in_String, int in_MaxNumberChars ) 
									:	maxNumberChars(in_MaxNumberChars)
	{
		if  (ExceededMaxStringLength(in_String, in_MaxNumberChars ))
			   throw isis::application_exception(MaxStringLengthError(in_String, in_MaxNumberChars ).c_str());

		nullStringValues();

		//std::cout << std::endl << "MultiFormatString::MultiFormatString( const std::string &in_String, int in_MaxNumberChars ) ";
		//std::cout << std::endl <<  "in_String: " << in_String;
		setString(in_String);
		setAllStrings();
	}

	////////////////////////////////////////////////////////////////////////////////////////////////////////
	MultiFormatString::MultiFormatString( const char *in_NarrowCharArray, int in_MaxNumberChars )
									:	maxNumberChars(in_MaxNumberChars)
	{
	
		if ( ExceededMaxStringLength( in_NarrowCharArray, in_MaxNumberChars ))
					throw isis::application_exception(MaxStringLengthError(in_NarrowCharArray, in_MaxNumberChars ).c_str());

		nullStringValues();

		//std::cout << std::endl << "MultiFormatString::MultiFormatString( const char *in_NarrowCharArray, int in_MaxNumberChars )";
		setString(in_NarrowCharArray);
		setAllStrings();
	};

	////////////////////////////////////////////////////////////////////////////////////////////////////////
	MultiFormatString::MultiFormatString( const wchar_t *in_WideCharArray, int in_MaxNumberChars )
												:	maxNumberChars(in_MaxNumberChars)
	{
		
		if ( ExceededMaxStringLength( in_WideCharArray, in_MaxNumberChars))
			throw isis::application_exception(MaxStringLengthError(isis_WideCharToString(in_WideCharArray), maxNumberChars ).c_str());

		nullStringValues();

		//std::cout << std::endl << "MultiFormatString::MultiFormatString( const wchar_t *in_WideCharArray, int in_MaxNumberChars )";
		setString(in_WideCharArray);
		setAllStrings();

	};
	///////////////////////////////////////////////////////////////////////////////////////////////////////

	MultiFormatString::operator const char* () const 
	{
		//std::cout << std::endl << "Called operator const char* ()";
		return narrowCharArray;
	};
	////////////////////////////////////////////////////////////////////////////////////////////////////////
	MultiFormatString::operator const wchar_t* () const
	{
		//std::cout << std::endl << "Called operator const wchar_t* ()";
		//std::cout << std::endl << wideCharArray;
		//std::cout << std::endl << narrowCharArray;
		//std::cout << std::endl << narrowString;

		//char narrowCharArray_temp[512];
		//ProWstringToString(narrowCharArray_temp,wideCharArray);
		//std::cout << std::endl << narrowCharArray_temp;

		return wideCharArray;
	};
	////////////////////////////////////////////////////////////////////////////////////////////////////////
	MultiFormatString::operator const std::string& () const
	{
		//std::cout << std::endl << "operator const std::string ()";
		return narrowString;
	}
	////////////////////////////////////////////////////////////////////////////////////////////////////////

	void MultiFormatString::nullStringValues()
	{
		narrowCharArray = NULL;
		wideCharArray = NULL;
		narrowString = "";
		narrowString_Defined = false;
	}
	void MultiFormatString::deleteAndNullStringValues()
	{
		delete [] narrowCharArray;
		delete [] wideCharArray;
		nullStringValues();
	}
	////////////////////////////////////////////////////////////////////////////////////////////////////////
	MultiFormatString& MultiFormatString::operator=( const char* in_NarrowCharArray )
	{

		if ( ExceededMaxStringLength( in_NarrowCharArray, maxNumberChars ))
					throw isis::application_exception(MaxStringLengthError(in_NarrowCharArray, maxNumberChars ).c_str());
	
		deleteAndNullStringValues();

		//std::cout << std::endl << "MultiFormatString::operator=( const char* in_NarrowCharArray )";
		setString(in_NarrowCharArray);
		setAllStrings();

		return *this;
	};
	////////////////////////////////////////////////////////////////////////////////////////////////////////
	MultiFormatString& MultiFormatString::operator=( const wchar_t* in_WideCharArray )
	{
		if ( ExceededMaxStringLength( in_WideCharArray, maxNumberChars))
			throw isis::application_exception(MaxStringLengthError(isis_WideCharToString(in_WideCharArray), maxNumberChars ).c_str());

		deleteAndNullStringValues();

		setString(in_WideCharArray);
		setAllStrings();

		//std::cout << std::endl << "MultiFormatString::operator=( const wchar_t* in_WideCharArray )";
		return *this;
	}
	////////////////////////////////////////////////////////////////////////////////////////////////////////
	MultiFormatString& MultiFormatString::operator=( const std::string &in_String )
	{
		if  (ExceededMaxStringLength(in_String, maxNumberChars ))
			   throw isis::application_exception(MaxStringLengthError(in_String, maxNumberChars ).c_str());

		deleteAndNullStringValues();

		setString(in_String);	
		setAllStrings();

		//std::cout << std::endl << "MultiFormatString::operator=( const std::string &in_String )";
		return *this;
	}
	////////////////////////////////////////////////////////////////////////////////////////////////////////
	MultiFormatString& MultiFormatString::operator=( const	MultiFormatString &in_MultiFormatString )
	{
		deleteAndNullStringValues();

		setString(in_MultiFormatString.narrowCharArray);
		setString(in_MultiFormatString.wideCharArray);
		setString(in_MultiFormatString.narrowString);

		return *this;
	}
	////////////////////////////////////////////////////////////////////////////////////////////////////////
	MultiFormatString::MultiFormatString( const MultiFormatString &in_MultiFormatString)
	{
		maxNumberChars = in_MultiFormatString.maxNumberChars;
		setString(in_MultiFormatString.narrowCharArray);
		setString(in_MultiFormatString.wideCharArray);
		setString(in_MultiFormatString.narrowString);
	}
	////////////////////////////////////////////////////////////////////////////////////////////////////////
	unsigned int MultiFormatString::size() const
	{
		return static_cast<int>(narrowString.size());
	}

	////////////////////////////////////////////////////////////////////////////////////////////////////////
	MultiFormatString::~MultiFormatString()
	{
		delete [] narrowCharArray;
		delete [] wideCharArray;
	}
	////////////////////////////////////////////////////////////////////////////////////////////////////////

} //END namespace isis
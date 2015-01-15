// -*-C++-*-
// bddmain.cpp
// CBdd class implementation


#include "core/utils.h"
#include "core/bddmain.h"





bdd_manager CBdd::manager=0;
bdd* CBdd::vars=0;
int CBdd::length=0;


void CBdd::Init(int len, int props, ...)
{
	va_list ap;
	va_start(ap, props);
	va_end(ap);

	length = len;

	vars = new bdd[len+1];
	manager = bdd_init();

	bdd_node_limit(manager, 10000000);

	if(mtbdd_one_data(manager, -1l, -1l)!=0)    // mtbdd one
	{
		throw new CDesertException("mtbdd_one_data fails: other terminal nodes already exist");
	}
	
	vars[0]=bdd_new_var_first(manager);
	for(int i=1; i<=length; i++) 
		vars[i]=bdd_new_var_after(manager, vars[i-1]);
}


void CBdd::Finit(void)
{
	for (int i=0; i<length; i++)
		bdd_free(manager, vars[i]);
	bdd_quit(manager);
	delete[] vars;

	manager=0;
	length=0;
	vars=0;
}

CBdd CBdd::Encode(int encVal, int startVar, int encLen)
{
	MANAGER_CHECK("CBdd::Encode()");
	bdd ret=bdd_one(manager); 
	int i, n;
	for(i=startVar+encLen-1, n=0; n < encLen; i--, n++)
	{
		int bit = encVal & 0x1;
		encVal = encVal >> 1;
		ret = bit ? bdd_and(manager, ret, vars[i] ) : bdd_and(manager, ret, bdd_not(manager, vars[i]) );
	}
	return CBdd(ret);
}

CBdd CBdd::Encode(int *enc, int begin_var, int num_vars)
{
	MANAGER_CHECK("CBdd::Encode()");
	bdd ret=bdd_one(manager); 
	int i, n;
	for(i=begin_var, n=0; n < num_vars; i++, n++)
	{
		if(enc[n] == 1) ret = bdd_and(manager, ret, vars[i] );
		if(enc[n] == 0) ret = bdd_and(manager, ret, bdd_not(manager, vars[i]) );
	}
	return CBdd(ret);
}

CBdd CBdd::Encode(CVIndex enc[], int len)
{
  MANAGER_CHECK("CBdd::Encode()");
  bdd ret=bdd_one(manager); 
  for(int i=0; i < len; i++)
  {
    if (enc[i].index >= length)
    {
      Error("CBddManager::m_encode", "index (%d) >= length (%d)", enc[i].index, length);
      break;
    }
    if(enc[i].val == 1) ret = bdd_and(manager, ret, vars[enc[i].index] );
    if(enc[i].val == 0) ret = bdd_and(manager, ret, bdd_not(manager, vars[enc[i].index]));
  }
  return CBdd(ret);
}

int CBdd::Satisfy(CBdd& b, CPtrList& encVectors)
{
	/*
	The function Satisfy fills up and returns a list of bitvectors. 
	The size of the list is the number of configurations.
	This size of bitvector is the number of bits in the encoding. For ex. 
	for a 2-bit encoding Satisy may return something like 00, 10, 11. Thus, 
	in effect it enumerates and returns all encodings that satisfy the BDD. This 
	internalyy uses the bdd library function bdd_satisfy.
	*/
  bdd f = b.core;

  if (f == bdd_one(manager))
  {
    int *encVec = new int[length];
    memset(encVec, 0xff, sizeof(int)*length);
    ExpandDontCare(encVec, 0, encVectors);
    delete[] encVec;
  }
  else
  {
    int rows=0;
    int **mat = new int*[BDD_MAX_PATHS];
    bdd_sat_f_mat(manager, f, 0, length-1, mat, &rows);
    if (rows > 0)
    {
      for (int i=0; i<rows; i++)
      {
        ExpandDontCare(mat[i], 0, encVectors);
        // mat[i] is allocated in bdd_sat_f_mat
        free(mat[i]);
      }
    }

    delete[] mat;
  }

  return encVectors.GetCount();
}

void CBdd::ExpandDontCare(int *enc, int cur, CPtrList& encVectors)
{
	/*
	enumerate the  dont care's
	*/
  if (cur == length)
  {
    int *encVec = new int[length];
    memcpy(encVec, enc, sizeof(int)*length);
    encVectors.AddTail(encVec);
  } else if (enc[cur] == -1) {
    enc[cur] = 0;
    ExpandDontCare(enc, cur + 1, encVectors);
    enc[cur] = 1;
    ExpandDontCare(enc, cur + 1, encVectors);
    enc[cur] = -1;
  } else {
    ExpandDontCare(enc, cur + 1, encVectors);
  }
}


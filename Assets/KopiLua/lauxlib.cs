/*
** $Id: lauxlib.c,v 1.159.1.3 2008/01/21 13:20:51 roberto Exp $
** Auxiliary functions for building Lua libraries
** See Copyright Notice in lua.h
*/

#define lauxlib_c
#define LUA_LIB

using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace KopiLua
{
	using LuaNumberType = System.Double;
	using LuaIntegerType = System.Int32;

	public partial class Lua
	{
		#if LUA_COMPAT_GETN
		public static int LuaLGetN(LuaState L, int t);
		public static void LuaLSetN(LuaState L, int t, int n);
		#else
		public static int LuaLGetN(LuaState L, int i) {return (int)LuaObjectLen(L, i);}
		public static void LuaLSetN(LuaState L, int i, int j) {} /* no op! */
		#endif

		#if LUA_COMPAT_OPENLIB
		//#define luaI_openlib	luaL_openlib
		#endif


		/* extra error code for `luaL_load' */
		public const int LUA_ERRFILE     = (LUA_ERRERR+1);


		public class LuaLReg {
		  public LuaLReg(CharPtr name, LuaNativeFunction func) {
			  this.name = name;
			  this.func = func;
		  }

		  public CharPtr name;
		  public LuaNativeFunction func;
		};


		/*
		** ===============================================================
		** some useful macros
		** ===============================================================
		*/

		public static void LuaLArgCheck(LuaState L, bool cond, int numarg, string extramsg) {
			if (!cond)
				LuaLArgError(L, numarg, extramsg);
		}
		public static CharPtr LuaLCheckString(LuaState L, int n) { return LuaLCheckLString(L, n); }
		public static CharPtr LuaLOptString(LuaState L, int n, CharPtr d) { uint len; return LuaLOptLString(L, n, d, out len); }
		public static int LuaLCheckInt(LuaState L, int n)	{return (int)LuaLCheckInteger(L, n);}
		public static int LuaLOptInt(LuaState L, int n, LuaIntegerType d)	{return (int)LuaLOptInteger(L, n, d);}
		public static long LuaLCheckLong(LuaState L, int n)	{return LuaLCheckInteger(L, n);}
		public static long LuaLOptLong(LuaState L, int n, LuaIntegerType d)	{return LuaLOptInteger(L, n, d);}

		public static CharPtr LuaLTypeName(LuaState L, int i)	{return LuaTypeName(L, LuaType(L,i));}

		//#define luaL_dofile(L, fn) \
		//    (luaL_loadfile(L, fn) || lua_pcall(L, 0, LUA_MULTRET, 0))

		//#define luaL_dostring(L, s) \
		//    (luaL_loadstring(L, s) || lua_pcall(L, 0, LUA_MULTRET, 0))

		public static void LuaLGetMetatable(LuaState L, CharPtr n) { LuaGetField(L, LUA_REGISTRYINDEX, n); }

		public delegate LuaNumberType LuaLOptDelegate (LuaState L, int narg);		
		public static LuaNumberType LuaLOpt(LuaState L, LuaLOptDelegate f, int n, LuaNumberType d) {
			return LuaIsNoneOrNil(L, (n != 0) ? d : f(L, n)) ? 1 : 0;}

		public delegate LuaIntegerType LuaLOptDelegateInteger(LuaState L, int narg);
		public static LuaIntegerType LuaLOptInteger(LuaState L, LuaLOptDelegateInteger f, int n, LuaNumberType d) {
			return (LuaIntegerType)(LuaIsNoneOrNil(L, n) ? d : f(L, (n)));
		}

		/*
		** {======================================================
		** Generic Buffer manipulation
		** =======================================================
		*/



		public class LuaLBuffer {
		  public int p;			/* current position in buffer */
		  public int lvl;  /* number of strings in the stack (level) */
		  public LuaState L;
		  public CharPtr buffer = new char[LUAL_BUFFERSIZE];
		};

		public static void LuaLAddChar(LuaLBuffer B, char c) {
			if (B.p >= LUAL_BUFFERSIZE)
				LuaLPrepBuffer(B);
			B.buffer[B.p++] = c;
		}

		///* compatibility only */
		public static void LuaLPutChar(LuaLBuffer B, char c)	{LuaLAddChar(B,c);}

		public static void LuaLAddSize(LuaLBuffer B, int n)	{B.p += n;}

		/* }====================================================== */


		/* compatibility with ref system */

		/* pre-defined references */
		public const int LUA_NOREF       = (-2);
		public const int LUA_REFNIL      = (-1);

		//#define lua_ref(L,lock) ((lock) ? luaL_ref(L, LUA_REGISTRYINDEX) : \
		//      (lua_pushstring(L, "unlocked references are obsolete"), lua_error(L), 0))

		//#define lua_unref(L,ref)        luaL_unref(L, LUA_REGISTRYINDEX, (ref))

		//#define lua_getref(L,ref)       lua_rawgeti(L, LUA_REGISTRYINDEX, (ref))


		//#define luaL_reg	luaL_Reg


		/* This file uses only the official API of Lua.
		** Any function declared here could be written as an application function.
		*/

		//#define lauxlib_c
		//#define LUA_LIB

		public const int FREELIST_REF	= 0;	/* free list of references */


		/* convert a stack index to positive */
		public static int AbsIndex(LuaState L, int i)
		{
			return ((i) > 0 || (i) <= LUA_REGISTRYINDEX ? (i) : LuaGetTop(L) + (i) + 1);
		}


		/*
		** {======================================================
		** Error-report functions
		** =======================================================
		*/


		public static int LuaLArgError (LuaState L, int narg, CharPtr extramsg) {
		  LuaDebug ar = new LuaDebug();
		  if (LuaGetStack(L, 0, ref ar)==0)  /* no stack frame? */
			  return LuaLError(L, "bad argument #%d (%s)", narg, extramsg);
		  LuaGetInfo(L, "n", ref ar);
		  if (strcmp(ar.namewhat, "method") == 0) {
			narg--;  /* do not count `self' */
			if (narg == 0)  /* error is in the self argument itself? */
			  return LuaLError(L, "calling " + LUA_QS + " on bad self ({1})",
								   ar.name, extramsg);
		  }
		  if (ar.name == null)
			ar.name = "?";
		  return LuaLError(L, "bad argument #%d to " + LUA_QS + " (%s)",
								narg, ar.name, extramsg);
		}


		public static int LuaLTypeError (LuaState L, int narg, CharPtr tname) {
		  CharPtr msg = LuaPushFString(L, "%s expected, got %s",
											tname, LuaLTypeName(L, narg));
		  return LuaLArgError(L, narg, msg);
		}


		private static void TagError (LuaState L, int narg, int tag) {
		  LuaLTypeError(L, narg, LuaTypeName(L, tag));
		}


		public static void LuaLWhere (LuaState L, int level) {
		  LuaDebug ar = new LuaDebug();
		  if (LuaGetStack(L, level, ref ar) != 0) {  /* check function at level */
			LuaGetInfo(L, "Sl", ref ar);  /* get info about it */
			if (ar.currentline > 0) {  /* is there info? */
			  LuaPushFString(L, "%s:%d: ", ar.short_src, ar.currentline);
			  return;
			}
		  }
		  LuaPushLiteral(L, "");  /* else, no information available... */
		}

		public static int LuaLError(LuaState L, CharPtr fmt, params object[] p)
		{
		  LuaLWhere(L, 1);
		  LuaPushVFString(L, fmt, p);
		  LuaConcat(L, 2);
		  return LuaError(L);
		}


		/* }====================================================== */


		public static int LuaLCheckOption (LuaState L, int narg, CharPtr def,
										 CharPtr [] lst) {
		  CharPtr name = (def != null) ? LuaLOptString(L, narg, def) :
									 LuaLCheckString(L, narg);
		  int i;
		  for (i=0; i<lst.Length; i++)
			if (strcmp(lst[i], name)==0)
			  return i;
		  return LuaLArgError(L, narg,
							   LuaPushFString(L, "invalid option " + LUA_QS, name));
		}


		public static int LuaLNewMetatable (LuaState L, CharPtr tname) {
		  LuaGetField(L, LUA_REGISTRYINDEX, tname);  /* get registry.name */
		  if (!LuaIsNil(L, -1))  /* name already in use? */
			return 0;  /* leave previous value on top, but return 0 */
		  LuaPop(L, 1);
		  LuaNewTable(L);  /* create metatable */
		  LuaPushValue(L, -1);
		  LuaSetField(L, LUA_REGISTRYINDEX, tname);  /* registry.name = metatable */
		  return 1;
		}


		public static object LuaLCheckUData (LuaState L, int ud, CharPtr tname) {
		  object p = LuaToUserData(L, ud);
		  if (p != null) {  /* value is a userdata? */
			if (LuaGetMetatable(L, ud) != 0) {  /* does it have a metatable? */
			  LuaGetField(L, LUA_REGISTRYINDEX, tname);  /* get correct metatable */
			  if (LuaRawEqual(L, -1, -2) != 0) {  /* does it have the correct mt? */
				LuaPop(L, 2);  /* remove both metatables */
				return p;
			  }
			}
		  }
		  LuaLTypeError(L, ud, tname);  /* else error */
		  return null;  /* to avoid warnings */
		}


		public static void LuaLCheckStack (LuaState L, int space, CharPtr mes) {
		  if (LuaCheckStack(L, space) == 0)
			LuaLError(L, "stack overflow (%s)", mes);
		}


		public static void LuaLCheckType (LuaState L, int narg, int t) {
		  if (LuaType(L, narg) != t)
			TagError(L, narg, t);
		}


		public static void LuaLCheckAny (LuaState L, int narg) {
		  if (LuaType(L, narg) == LUA_TNONE)
			LuaLArgError(L, narg, "value expected");
		}


		public static CharPtr LuaLCheckLString(LuaState L, int narg) {uint len; return LuaLCheckLString(L, narg, out len);}

		[CLSCompliantAttribute(false)]
		public static CharPtr LuaLCheckLString (LuaState L, int narg, out uint len) {
		  CharPtr s = LuaToLString(L, narg, out len);
		  if (s==null) TagError(L, narg, LUA_TSTRING);
		  return s;
		}


		public static CharPtr LuaLOptLString (LuaState L, int narg, CharPtr def) {
			uint len; return LuaLOptLString (L, narg, def, out len); }

		[CLSCompliantAttribute(false)]
		public static CharPtr LuaLOptLString (LuaState L, int narg, CharPtr def, out uint len) {
		  if (LuaIsNoneOrNil(L, narg)) {
			len = (uint)((def != null) ? strlen(def) : 0);
			return def;
		  }
		  else return LuaLCheckLString(L, narg, out len);
		}


		public static LuaNumberType LuaLCheckNumber (LuaState L, int narg) {
			LuaNumberType d = LuaToNumber(L, narg);
		  if ((d == 0) && (LuaIsNumber(L, narg)==0))  /* avoid extra test when d is not 0 */
			TagError(L, narg, LUA_TNUMBER);
		  return d;
		}


		public static LuaNumberType LuaLOptNumber (LuaState L, int narg, LuaNumberType def) {
		  return LuaLOpt(L, LuaLCheckNumber, narg, def);
		}


		public static LuaIntegerType LuaLCheckInteger (LuaState L, int narg) {
			LuaIntegerType d = LuaToInteger(L, narg);
		  if (d == 0 && LuaIsNumber(L, narg)==0)  /* avoid extra test when d is not 0 */
			TagError(L, narg, LUA_TNUMBER);
		  return d;
		}


		public static LuaIntegerType LuaLOptInteger (LuaState L, int narg, LuaIntegerType def) {
		  return LuaLOptInteger(L, LuaLCheckInteger, narg, def);
		}


		public static int LuaLGetMetafield (LuaState L, int obj, CharPtr event_) {
		  if (LuaGetMetatable(L, obj)==0)  /* no metatable? */
			return 0;
		  LuaPushString(L, event_);
		  LuaRawGet(L, -2);
		  if (LuaIsNil(L, -1)) {
			LuaPop(L, 2);  /* remove metatable and metafield */
			return 0;
		  }
		  else {
			LuaRemove(L, -2);  /* remove only metatable */
			return 1;
		  }
		}


		public static int LuaLCallMeta (LuaState L, int obj, CharPtr event_) {
		  obj = AbsIndex(L, obj);
		  if (LuaLGetMetafield(L, obj, event_)==0)  /* no metafield? */
			return 0;
		  LuaPushValue(L, obj);
		  LuaCall(L, 1, 1);
		  return 1;
		}


		public static void LuaLRegister(LuaState L, CharPtr libname,
										LuaLReg[] l) {
		  LuaIOpenLib(L, libname, l, 0);
		}

		// we could just take the .Length member here, but let's try
		// to keep it as close to the C implementation as possible.
		private static int LibSize (LuaLReg[] l) {
		  int size = 0;
		  for (; l[size].name!=null; size++);
		  return size;
		}

		public static void LuaIOpenLib (LuaState L, CharPtr libname,
									  LuaLReg[] l, int nup) {		  
		  if (libname!=null) {
			int size = LibSize(l);
			/* check whether lib already exists */
			LuaLFindTable(L, LUA_REGISTRYINDEX, "_LOADED", 1);
			LuaGetField(L, -1, libname);  /* get _LOADED[libname] */
			if (!LuaIsTable(L, -1)) {  /* not found? */
			  LuaPop(L, 1);  /* remove previous result */
			  /* try global variable (and create one if it does not exist) */
			  if (LuaLFindTable(L, LUA_GLOBALSINDEX, libname, size) != null)
				LuaLError(L, "name conflict for module " + LUA_QS, libname);
			  LuaPushValue(L, -1);
			  LuaSetField(L, -3, libname);  /* _LOADED[libname] = new table */
			}
			LuaRemove(L, -2);  /* remove _LOADED table */
			LuaInsert(L, -(nup+1));  /* move library table to below upvalues */
		  }
		  int reg_num = 0;
		  for (; l[reg_num].name!=null; reg_num++) {
			int i;
			for (i=0; i<nup; i++)  /* copy upvalues to the top */
			  LuaPushValue(L, -nup);
			LuaPushCClosure(L, l[reg_num].func, nup);
			LuaSetField(L, -(nup+2), l[reg_num].name);
		  }
		  LuaPop(L, nup);  /* remove upvalues */
		}



		/*
		** {======================================================
		** getn-setn: size for arrays
		** =======================================================
		*/

		#if LUA_COMPAT_GETN

		static int checkint (LuaState L, int topop) {
		  int n = (lua_type(L, -1) == LUA_TNUMBER) ? lua_tointeger(L, -1) : -1;
		  lua_pop(L, topop);
		  return n;
		}


		static void getsizes (LuaState L) {
		  lua_getfield(L, LUA_REGISTRYINDEX, "LUA_SIZES");
		  if (lua_isnil(L, -1)) {  /* no `size' table? */
			lua_pop(L, 1);  /* remove nil */
			lua_newtable(L);  /* create it */
			lua_pushvalue(L, -1);  /* `size' will be its own metatable */
			lua_setmetatable(L, -2);
			lua_pushliteral(L, "kv");
			lua_setfield(L, -2, "__mode");  /* metatable(N).__mode = "kv" */
			lua_pushvalue(L, -1);
			lua_setfield(L, LUA_REGISTRYINDEX, "LUA_SIZES");  /* store in register */
		  }
		}


		public static void luaL_setn (LuaState L, int t, int n) {
		  t = abs_index(L, t);
		  lua_pushliteral(L, "n");
		  lua_rawget(L, t);
		  if (checkint(L, 1) >= 0) {  /* is there a numeric field `n'? */
			lua_pushliteral(L, "n");  /* use it */
			lua_pushinteger(L, n);
			lua_rawset(L, t);
		  }
		  else {  /* use `sizes' */
			getsizes(L);
			lua_pushvalue(L, t);
			lua_pushinteger(L, n);
			lua_rawset(L, -3);  /* sizes[t] = n */
			lua_pop(L, 1);  /* remove `sizes' */
		  }
		}


		public static int luaL_getn (LuaState L, int t) {
		  int n;
		  t = abs_index(L, t);
		  lua_pushliteral(L, "n");  /* try t.n */
		  lua_rawget(L, t);
		  if ((n = checkint(L, 1)) >= 0) return n;
		  getsizes(L);  /* else try sizes[t] */
		  lua_pushvalue(L, t);
		  lua_rawget(L, -2);
		  if ((n = checkint(L, 2)) >= 0) return n;
		  return (int)lua_objlen(L, t);
		}

		#endif

		/* }====================================================== */



		public static CharPtr LuaLGSub (LuaState L, CharPtr s, CharPtr p,
																	   CharPtr r) {
		  CharPtr wild;
		  uint l = (uint)strlen(p);
		  LuaLBuffer b = new LuaLBuffer();
		  LuaLBuffInit(L, b);
		  while ((wild = strstr(s, p)) != null) {
			LuaLAddLString(b, s, (uint)(wild - s));  /* push prefix */
			LuaLAddString(b, r);  /* push replacement in place of pattern */
			s = wild + l;  /* continue after `p' */
		  }
		  LuaLAddString(b, s);  /* push last suffix */
		  LuaLPushResult(b);
		  return LuaToString(L, -1);
		}


		public static CharPtr LuaLFindTable (LuaState L, int idx,
											   CharPtr fname, int szhint) {
		  CharPtr e;
		  LuaPushValue(L, idx);
		  do {
			e = strchr(fname, '.');
			if (e == null) e = fname + strlen(fname);
			LuaPushLString(L, fname, (uint)(e - fname));
			LuaRawGet(L, -2);
			if (LuaIsNil(L, -1)) {  /* no such field? */
			  LuaPop(L, 1);  /* remove this nil */
			  LuaCreateTable(L, 0, (e == '.' ? 1 : szhint)); /* new table for field */
			  LuaPushLString(L, fname, (uint)(e - fname));
			  LuaPushValue(L, -2);
			  LuaSetTable(L, -4);  /* set new table into field */
			}
			else if (!LuaIsTable(L, -1)) {  /* field has a non-table value? */
			  LuaPop(L, 2);  /* remove table and value */
			  return fname;  /* return problematic part of the name */
			}
			LuaRemove(L, -2);  /* remove previous table */
			fname = e + 1;
		  } while (e == '.');
		  return null;
		}



		/*
		** {======================================================
		** Generic Buffer manipulation
		** =======================================================
		*/


		private static int BufferLen(LuaLBuffer B)	{return B.p;}
		private static int BufferFree(LuaLBuffer B)	{return LUAL_BUFFERSIZE - BufferLen(B);}

		public const int LIMIT = LUA_MINSTACK / 2;


		private static int EmptyBuffer (LuaLBuffer B) {
		  uint l = (uint)BufferLen(B);
		  if (l == 0) return 0;  /* put nothing on stack */
		  else {
			LuaPushLString(B.L, B.buffer, l);
			B.p = 0;
			B.lvl++;
			return 1;
		  }
		}


		private static void AdjustStack (LuaLBuffer B) {
		  if (B.lvl > 1) {
			LuaState L = B.L;
			int toget = 1;  /* number of levels to concat */
			uint toplen = LuaStrLen(L, -1);
			do {
			  uint l = LuaStrLen(L, -(toget+1));
			  if (B.lvl - toget + 1 >= LIMIT || toplen > l) {
				toplen += l;
				toget++;
			  }
			  else break;
			} while (toget < B.lvl);
			LuaConcat(L, toget);
			B.lvl = B.lvl - toget + 1;
		  }
		}


		public static CharPtr LuaLPrepBuffer (LuaLBuffer B) {
		  if (EmptyBuffer(B) != 0)
			AdjustStack(B);
			return new CharPtr(B.buffer, B.p);
		}

		[CLSCompliantAttribute(false)]
		public static void LuaLAddLString (LuaLBuffer B, CharPtr s, uint l) {
			while (l-- != 0)
			{
				char c = s[0];
				s = s.next();
				LuaLAddChar(B, c);
			}
		}


		public static void LuaLAddString (LuaLBuffer B, CharPtr s) {
		  LuaLAddLString(B, s, (uint)strlen(s));
		}


		public static void LuaLPushResult (LuaLBuffer B) {
		  EmptyBuffer(B);
		  LuaConcat(B.L, B.lvl);
		  B.lvl = 1;
		}


		public static void LuaLAddValue (LuaLBuffer B) {
		  LuaState L = B.L;
		  uint vl;
		  CharPtr s = LuaToLString(L, -1, out vl);
		  if (vl <= BufferFree(B)) {  /* fit into buffer? */
			CharPtr dst = new CharPtr(B.buffer.chars, B.buffer.index + B.p);
			CharPtr src = new CharPtr(s.chars, s.index);
			for (uint i = 0; i < vl; i++)
				dst[i] = src[i];
			B.p += (int)vl;
			LuaPop(L, 1);  /* remove from stack */
		  }
		  else {
			if (EmptyBuffer(B) != 0)
			  LuaInsert(L, -2);  /* put buffer before new value */
			B.lvl++;  /* add new value into B stack */
			AdjustStack(B);
		  }
		}


		public static void LuaLBuffInit (LuaState L, LuaLBuffer B) {
		  B.L = L;
		  B.p = /*B.buffer*/ 0;
		  B.lvl = 0;
		}

		/* }====================================================== */


		public static int LuaLRef (LuaState L, int t) {
		  int ref_;
		  t = AbsIndex(L, t);
		  if (LuaIsNil(L, -1)) {
			LuaPop(L, 1);  /* remove from stack */
			return LUA_REFNIL;  /* `nil' has a unique fixed reference */
		  }
		  LuaRawGetI(L, t, FREELIST_REF);  /* get first free element */
		  ref_ = (int)LuaToInteger(L, -1);  /* ref = t[FREELIST_REF] */
		  LuaPop(L, 1);  /* remove it from stack */
		  if (ref_ != 0) {  /* any free element? */
			LuaRawGetI(L, t, ref_);  /* remove it from list */
			LuaRawSetI(L, t, FREELIST_REF);  /* (t[FREELIST_REF] = t[ref]) */
		  }
		  else {  /* no free elements */
			ref_ = (int)LuaObjectLen(L, t);
			ref_++;  /* create new reference */
		  }
		  LuaRawSetI(L, t, ref_);
		  return ref_;
		}


		public static void LuaLUnref (LuaState L, int t, int ref_) {
		  if (ref_ >= 0) {
			t = AbsIndex(L, t);
			LuaRawGetI(L, t, FREELIST_REF);
			LuaRawSetI(L, t, ref_);  /* t[ref] = t[FREELIST_REF] */
			LuaPushInteger(L, ref_);
			LuaRawSetI(L, t, FREELIST_REF);  /* t[FREELIST_REF] = ref */
		  }
		}



		/*
		** {======================================================
		** Load functions
		** =======================================================
		*/

		public class LoadF {
		  public int extraline;
		  public Stream f;
		  public CharPtr buff = new char[LUAL_BUFFERSIZE];
		};

		[CLSCompliantAttribute(false)]
		public static CharPtr GetF (LuaState L, object ud, out uint size) {
		  size = 0;
		  LoadF lf = (LoadF)ud;
		  //(void)L;
		  if (lf.extraline != 0) {
			lf.extraline = 0;
			size = 1;
			return "\n";
		  }
		  if (feof(lf.f) != 0) return null;
		  size = (uint)fread(lf.buff, 1, lf.buff.chars.Length, lf.f);
		  return (size > 0) ? new CharPtr(lf.buff) : null;
		}


		private static int ErrFile (LuaState L, CharPtr what, int fnameindex) {
		  CharPtr serr = strerror(errno());
		  CharPtr filename = LuaToString(L, fnameindex) + 1;
		  LuaPushFString(L, "cannot %s %s: %s", what, filename, serr);
		  LuaRemove(L, fnameindex);
		  return LUA_ERRFILE;
		}


		public static int LuaLLoadFile (LuaState L, CharPtr filename) {
		  LoadF lf = new LoadF();
		  int status, readstatus;
		  int c;
		  int fnameindex = LuaGetTop(L) + 1;  /* index of filename on the stack */
		  lf.extraline = 0;
		  if (filename == null) {
			LuaPushLiteral(L, "=stdin");
			lf.f = stdin;
		  }
		  else {
			LuaPushFString(L, "@%s", filename);
			lf.f = fopen(filename, "r");
			if (lf.f == null) return ErrFile(L, "open", fnameindex);
		  }
		  c = getc(lf.f);
		  if (c == '#') {  /* Unix exec. file? */
			lf.extraline = 1;
			while ((c = getc(lf.f)) != EOF && c != '\n') ;  /* skip first line */
			if (c == '\n') c = getc(lf.f);
		  }
		  if (c == LUA_SIGNATURE[0] && (filename!=null)) {  /* binary file? */
			lf.f = freopen(filename, "rb", lf.f);  /* reopen in binary mode */
			if (lf.f == null) return ErrFile(L, "reopen", fnameindex);
			/* skip eventual `#!...' */
		   while ((c = getc(lf.f)) != EOF && c != LUA_SIGNATURE[0]) ;
			lf.extraline = 0;
		  }
		  ungetc(c, lf.f);
		  status = LuaLoad(L, GetF, lf, LuaToString(L, -1));
		  readstatus = ferror(lf.f);
		  if (filename != null) fclose(lf.f);  /* close file (even in case of errors) */
		  if (readstatus != 0) {
			LuaSetTop(L, fnameindex);  /* ignore results from `lua_load' */
			return ErrFile(L, "read", fnameindex);
		  }
		  LuaRemove(L, fnameindex);
		  return status;
		}


		public class LoadS {
		  public CharPtr s;
          [CLSCompliantAttribute(false)]
		  public uint size;
		};


		static CharPtr GetS (LuaState L, object ud, out uint size) {
		  LoadS ls = (LoadS)ud;
		  //(void)L;
		  //if (ls.size == 0) return null;
		  size = ls.size;
		  ls.size = 0;
		  return ls.s;
		}

		[CLSCompliantAttribute(false)]
		public static int LuaLLoadBuffer(LuaState L, CharPtr buff, uint size,
										CharPtr name) {
		  LoadS ls = new LoadS();
		  ls.s = new CharPtr(buff);
		  ls.size = size;
		  return LuaLoad(L, GetS, ls, name);
		}


		public static int LuaLLoadString(LuaState L, CharPtr s) {
		  return LuaLLoadBuffer(L, s, (uint)strlen(s), s);
		}



		/* }====================================================== */


		private static object LuaAlloc (Type t) {
			return System.Activator.CreateInstance(t);
		}


		private static int Panic (LuaState L) {
		  //(void)L;  /* to avoid warnings */
		  fprintf(stderr, "PANIC: unprotected error in call to Lua API (%s)\n",
						   LuaToString(L, -1));
		  return 0;
		}


		public static LuaState LuaLNewState()
		{
			LuaState L = LuaNewState(LuaAlloc, null);
		  if (L != null) LuaAtPanic(L, Panic);
		  return L;
		}

	}
}

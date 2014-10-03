/*
** $Id: lua.h,v 1.218.1.5 2008/08/06 13:30:12 roberto Exp $
** Lua - An Extensible Extension Language
** Lua.org, PUC-Rio, Brazil (http://www.lua.org)
** See Copyright Notice at the end of this file
*/

using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace KopiLua
{
	using LuaNumberType = Double;
	using LuaIntegerType = System.Int32;

	/* Functions to be called by the debuger in specific events */
	public delegate void LuaHook(LuaState L, LuaDebug ar);

	public delegate int LuaNativeFunction(LuaState L);

#if !UNITY_3D
	[CLSCompliantAttribute(true)]
#endif
	public partial class Lua
	{
		private static bool RunningOnUnix
		{
			get {
				var platform = (int)Environment.OSVersion.Platform;

				return (platform == 4) || (platform == 6) || (platform == 128);
			}
		}

		static Lua()
		{
			if (RunningOnUnix) {
				LUA_ROOT = UNIX_LUA_ROOT;
				LUA_LDIR = UNIX_LUA_LDIR;
				LUA_CDIR = UNIX_LUA_CDIR;
				LUA_PATH_DEFAULT = UNIX_LUA_PATH_DEFAULT;
				LUA_CPATH_DEFAULT = UNIX_LUA_CPATH_DEFAULT;
			} else {
				LUA_ROOT = null;
				LUA_LDIR = WIN32_LUA_LDIR;
				LUA_CDIR = WIN32_LUA_CDIR;
				LUA_PATH_DEFAULT = WIN32_LUA_PATH_DEFAULT;
				LUA_CPATH_DEFAULT = WIN32_LUA_CPATH_DEFAULT;
			}
		}

		public const string LUA_VERSION = "Lua 5.1";
		public const string LUA_RELEASE = "Lua 5.1.5";
		public const int LUA_VERSION_NUM	= 501;
		public const string LUA_COPYRIGHT = "Copyright (C) 1994-2012 Lua.org, PUC-Rio";
		public const string LUA_AUTHORS = "R. Ierusalimschy, L. H. de Figueiredo & W. Celes";


		/* mark for precompiled code (`<esc>Lua') */
		public const string LUA_SIGNATURE = "\x01bLua";

		/* option for multiple returns in `lua_pcall' and `lua_call' */
		public const int LUA_MULTRET	= (-1);


		/*
		** pseudo-indices
		*/
		public const int LUA_REGISTRYINDEX	= (-10000);
		public const int LUA_ENVIRONINDEX	= (-10001);
		public const int LUA_GLOBALSINDEX	= (-10002);
		public static int LuaUpValueIndex(int i)	{return LUA_GLOBALSINDEX-i;}


		/* thread status; 0 is OK */
		public const int LUA_YIELD	= 1;
		public const int LUA_ERRRUN = 2;
		public const int LUA_ERRSYNTAX	= 3;
		public const int LUA_ERRMEM	= 4;
		public const int LUA_ERRERR	= 5;





		/*
		** functions that read/write blocks when loading/dumping Lua chunks
		*/
#if !UNITY_3D
		[CLSCompliantAttribute(false)]
#endif
        public delegate CharPtr lua_Reader(LuaState L, object ud, out uint sz);
#if !UNITY_3D
		[CLSCompliantAttribute(false)]
#endif
		public delegate int lua_Writer(LuaState L, CharPtr p, uint sz, object ud);


		/*
		** prototype for memory-allocation functions
		*/
        //public delegate object lua_Alloc(object ud, object ptr, uint osize, uint nsize);
		public delegate object lua_Alloc(Type t);


		/*
		** basic types
		*/
		public const int LUA_TNONE = -1;

        public const int LUA_TNIL = 0;
        public const int LUA_TBOOLEAN = 1;
        public const int LUA_TLIGHTUSERDATA = 2;
        public const int LUA_TNUMBER = 3;
        public const int LUA_TSTRING = 4;
        public const int LUA_TTABLE = 5;
        public const int LUA_TFUNCTION = 6;
        public const int LUA_TUSERDATA = 7;
        public const int LUA_TTHREAD = 8;



		/* minimum Lua stack available to a C function */
		public const int LUA_MINSTACK = 20;


		/* type of numbers in Lua */
		//typedef LUA_NUMBER LuaNumberType;


		/* type for integer functions */
		//typedef LUA_INTEGER LuaIntegerType;

		/*
		** garbage-collection function and options
		*/

		public const int LUA_GCSTOP			= 0;
		public const int LUA_GCRESTART		= 1;
		public const int LUA_GCCOLLECT		= 2;
		public const int LUA_GCCOUNT		= 3;
		public const int LUA_GCCOUNTB		= 4;
		public const int LUA_GCSTEP			= 5;
		public const int LUA_GCSETPAUSE		= 6;
		public const int LUA_GCSETSTEPMUL	= 7;

		/* 
		** ===============================================================
		** some useful macros
		** ===============================================================
		*/

        public static void LuaPop(LuaState L, int n)
        {
            LuaSetTop(L, -(n) - 1);
        }

        public static void LuaNewTable(LuaState L)
        {
            LuaCreateTable(L, 0, 0);
        }

        public static void LuaRegister(LuaState L, CharPtr n, LuaNativeFunction f)
        {
            LuaPushCFunction(L, f);
            LuaSetGlobal(L, n);
        }

        public static void LuaPushCFunction(LuaState L, LuaNativeFunction f)
        {
            LuaPushCClosure(L, f, 0);
        }

#if !UNITY_3D
		[CLSCompliantAttribute(false)]
#endif
        public static uint LuaStrLen(LuaState L, int i)
        {
            return LuaObjectLen(L, i);
        }

        public static bool LuaIsFunction(LuaState L, int n)
        {
            return LuaType(L, n) == LUA_TFUNCTION;
        }

        public static bool LuaIsTable(LuaState L, int n)
        {
			return LuaType(L, n) == LUA_TTABLE;
        }

        public static bool LuaIsLightUserData(LuaState L, int n)
        {
            return LuaType(L, n) == LUA_TLIGHTUSERDATA;
        }

        public static bool LuaIsNil(LuaState L, int n)
        {
            return LuaType(L, n) == LUA_TNIL;
        }

        public static bool LuaIsBoolean(LuaState L, int n)
        {
            return LuaType(L, n) == LUA_TBOOLEAN;
        }

        public static bool LuaIsThread(LuaState L, int n)
        {
            return LuaType(L, n) == LUA_TTHREAD;
        }

        public static bool LuaIsNone(LuaState L, int n)
        {
            return LuaType(L, n) == LUA_TNONE;
        }

        public static bool LuaIsNoneOrNil(LuaState L, LuaNumberType n)
        {
            return LuaType(L, (int)n) <= 0;
        }

        public static void LuaPushLiteral(LuaState L, CharPtr s)
        {
            //TODO: Implement use using lua_pushlstring instead of lua_pushstring
			//lua_pushlstring(L, "" s, (sizeof(s)/GetUnmanagedSize(typeof(char)))-1)
            LuaPushString(L, s);
        }

        public static void LuaSetGlobal(LuaState L, CharPtr s)
        {
            LuaSetField(L, LUA_GLOBALSINDEX, s);
        }

        public static void LuaGetGlobal(LuaState L, CharPtr s)
        {
            LuaGetField(L, LUA_GLOBALSINDEX, s);
        }

        public static CharPtr LuaToString(LuaState L, int i)
        {
            uint blah;
            return LuaToLString(L, i, out blah);
        }

		////#define lua_open()	luaL_newstate()
		public static LuaState LuaOpen()
        {
            return LuaLNewState();
        }

        ////#define lua_getregistry(L)	lua_pushvalue(L, LUA_REGISTRYINDEX)
        public static void LuaGetRegistry(LuaState L)
        {
            LuaPushValue(L, LUA_REGISTRYINDEX);
        }

        ////#define lua_getgccount(L)	lua_gc(L, LUA_GCCOUNT, 0)
        public static int LuaGetGCCount(LuaState L)
        {
            return LuaGC(L, LUA_GCCOUNT, 0);
        }

		//#define lua_Chunkreader		lua_Reader
		//#define lua_Chunkwriter		lua_Writer


		/*
		** {======================================================================
		** Debug API
		** =======================================================================
		*/


		/*
		** Event codes
		*/
		public const int LUA_HOOKCALL = 0;
        public const int LUA_HOOKRET = 1;
        public const int LUA_HOOKLINE = 2;
        public const int LUA_HOOKCOUNT = 3;
        public const int LUA_HOOKTAILRET = 4;


		/*
		** Event masks
		*/
		public const int LUA_MASKCALL = (1 << LUA_HOOKCALL);
        public const int LUA_MASKRET = (1 << LUA_HOOKRET);
        public const int LUA_MASKLINE = (1 << LUA_HOOKLINE);
        public const int LUA_MASKCOUNT = (1 << LUA_HOOKCOUNT);




		/* }====================================================================== */


		/******************************************************************************
		* Copyright (C) 1994-2012 Lua.org, PUC-Rio.  All rights reserved.
		*
		* Permission is hereby granted, free of charge, to any person obtaining
		* a copy of this software and associated documentation files (the
		* "Software"), to deal in the Software without restriction, including
		* without limitation the rights to use, copy, modify, merge, publish,
		* distribute, sublicense, and/or sell copies of the Software, and to
		* permit persons to whom the Software is furnished to do so, subject to
		* the following conditions:
		*
		* The above copyright notice and this permission notice shall be
		* included in all copies or substantial portions of the Software.
		*
		* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
		* EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
		* MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
		* IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
		* CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
		* TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
		* SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
		******************************************************************************/

	}
}

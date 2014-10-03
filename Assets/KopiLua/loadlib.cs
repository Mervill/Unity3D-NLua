/*
** $Id: loadlib.c,v 1.52.1.3 2008/08/06 13:29:28 roberto Exp $
** Dynamic library loader for Lua
** See Copyright Notice in lua.h
**
** This module contains an implementation of loadlib for Unix systems
** that have dlfcn, an implementation for Darwin (Mac OS X), an
** implementation for Windows, and a stub for other systems.
*/

using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace KopiLua
{
	public partial class Lua
	{

		/* prefix for open functions in C libraries */
		public const string LUAPOF = "luaopen_";

		/* separator for open functions in C libraries */
		public const string LUAOFSEP = "_";


		public const string LIBPREFIX = "LOADLIB: ";

		public const string POF = LUAPOF;
		public const string LIBFAIL = "open";


		/* error codes for ll_loadfunc */
		public const int ERRLIB			= 1;
		public const int ERRFUNC		= 2;

		//public static void setprogdir(LuaState L) { }

		public static void SetProgDir(LuaState L)
		{
			#if WINDOWS_PHONE
			// On Windows Phone, the current directory is the root of the 
			// Isolated Storage directory, which is "/".

			CharPtr buff = "/";

			#elif SILVERLIGHT
			// Not all versions of Silverlight support this method.
			// So, if it is unsupported, rollback to the Isolated
			// Storage root (a.k.a. the leap of faith).

			CharPtr buff;
			try
			{
				buff = Directory.GetCurrentDirectory(); 
			}
			catch (MethodAccessException)
			{
				buff = "/";
			}
			#else
				CharPtr buff = Directory.GetCurrentDirectory(); 
			#endif

			LuaLGSub(L, LuaToString(L, -1), LUA_EXECDIR, buff);
			LuaRemove(L, -2);  /* remove original string */
		}


		#if LUA_DL_DLOPEN
		/*
		** {========================================================================
		** This is an implementation of loadlib based on the dlfcn interface.
		** The dlfcn interface is available in Linux, SunOS, Solaris, IRIX, FreeBSD,
		** NetBSD, AIX 4.2, HPUX 11, and  probably most other Unix flavors, at least
		** as an emulation layer on top of native functions.
		** =========================================================================
		*/

		//#include <dlfcn.h>

		static void ll_unloadlib (void *lib) {
		  dlclose(lib);
		}


		static void *ll_load (LuaState L, readonly CharPtr path) {
		  void *lib = dlopen(path, RTLD_NOW);
		  if (lib == null) lua_pushstring(L, dlerror());
		  return lib;
		}


		static lua_CFunction ll_sym (LuaState L, void *lib, readonly CharPtr sym) {
		  lua_CFunction f = (lua_CFunction)dlsym(lib, sym);
		  if (f == null) lua_pushstring(L, dlerror());
		  return f;
		}

		/* }====================================================== */



		//#elif defined(LUA_DL_DLL)
		/*
		** {======================================================================
		** This is an implementation of loadlib for Windows using native functions.
		** =======================================================================
		*/

		//#include <windows.h>


		//#undef setprogdir

		static void setprogdir (LuaState L) {
		  char buff[MAX_PATH + 1];
		  char *lb;
		  DWORD nsize = sizeof(buff)/GetUnmanagedSize(typeof(char));
		  DWORD n = GetModuleFileNameA(null, buff, nsize);
		  if (n == 0 || n == nsize || (lb = strrchr(buff, '\\')) == null)
			luaL_error(L, "unable to get ModuleFileName");
		  else {
			*lb = '\0';
			luaL_gsub(L, lua_tostring(L, -1), LUA_EXECDIR, buff);
			lua_remove(L, -2);  /* remove original string */
		  }
		}


		static void pusherror (LuaState L) {
		  int error = GetLastError();
		  char buffer[128];
		  if (FormatMessageA(FORMAT_MESSAGE_IGNORE_INSERTS | FORMAT_MESSAGE_FROM_SYSTEM,
			  null, error, 0, buffer, sizeof(buffer), null))
			lua_pushstring(L, buffer);
		  else
			lua_pushfstring(L, "system error %d\n", error);
		}

		static void ll_unloadlib (void *lib) {
		  FreeLibrary((HINSTANCE)lib);
		}


		static void *ll_load (LuaState L, readonly CharPtr path) {
		  HINSTANCE lib = LoadLibraryA(path);
		  if (lib == null) pusherror(L);
		  return lib;
		}


		static lua_CFunction ll_sym (LuaState L, void *lib, readonly CharPtr sym) {
		  lua_CFunction f = (lua_CFunction)GetProcAddress((HINSTANCE)lib, sym);
		  if (f == null) pusherror(L);
		  return f;
		}

		/* }====================================================== */



#elif LUA_DL_DYLD
		/*
		** {======================================================================
		** Native Mac OS X / Darwin Implementation
		** =======================================================================
		*/

		//#include <mach-o/dyld.h>


		/* Mac appends a `_' before C function names */
		//#undef POF
		//#define POF	"_" LUA_POF


		static void pusherror (LuaState L) {
		  CharPtr err_str;
		  CharPtr err_file;
		  NSLinkEditErrors err;
		  int err_num;
		  NSLinkEditError(err, err_num, err_file, err_str);
		  lua_pushstring(L, err_str);
		}


		static CharPtr errorfromcode (NSObjectFileImageReturnCode ret) {
		  switch (ret) {
			case NSObjectFileImageInappropriateFile:
			  return "file is not a bundle";
			case NSObjectFileImageArch:
			  return "library is for wrong CPU type";
			case NSObjectFileImageFormat:
			  return "bad format";
			case NSObjectFileImageAccess:
			  return "cannot access file";
			case NSObjectFileImageFailure:
			default:
			  return "unable to load library";
		  }
		}


		static void ll_unloadlib (void *lib) {
		  NSUnLinkModule((NSModule)lib, NSUNLINKMODULE_OPTION_RESET_LAZY_REFERENCES);
		}


		static void *ll_load (LuaState L, readonly CharPtr path) {
		  NSObjectFileImage img;
		  NSObjectFileImageReturnCode ret;
		  /* this would be a rare case, but prevents crashing if it happens */
		  if(!_dyld_present()) {
			lua_pushliteral(L, "dyld not present");
			return null;
		  }
		  ret = NSCreateObjectFileImageFromFile(path, img);
		  if (ret == NSObjectFileImageSuccess) {
			NSModule mod = NSLinkModule(img, path, NSLINKMODULE_OPTION_PRIVATE |
							   NSLINKMODULE_OPTION_RETURN_ON_ERROR);
			NSDestroyObjectFileImage(img);
			if (mod == null) pusherror(L);
			return mod;
		  }
		  lua_pushstring(L, errorfromcode(ret));
		  return null;
		}


		static lua_CFunction ll_sym (LuaState L, void *lib, readonly CharPtr sym) {
		  NSSymbol nss = NSLookupSymbolInModule((NSModule)lib, sym);
		  if (nss == null) {
			lua_pushfstring(L, "symbol " + LUA_QS + " not found", sym);
			return null;
		  }
		  return (lua_CFunction)NSAddressOfSymbol(nss);
		}

		/* }====================================================== */



#else
		/*
		** {======================================================
		** Fallback for other systems
		** =======================================================
		*/

		//#undef LIB_FAIL
		//#define LIB_FAIL	"absent"


		public const string DLMSG = "dynamic libraries not enabled; check your Lua installation";


		public static void LLUnloadLib (object lib) {
		  //(void)lib;  /* to avoid warnings */
		}


		public static object LLLoad (LuaState L, CharPtr path) {
		  //(void)path;  /* to avoid warnings */
		  LuaPushLiteral(L, DLMSG);
		  return null;
		}


		public static LuaNativeFunction LLSym (LuaState L, object lib, CharPtr sym) {
		  //(void)lib; (void)sym;  /* to avoid warnings */
		  LuaPushLiteral(L, DLMSG);
		  return null;
		}

		/* }====================================================== */
		#endif



		private static object LLRegister (LuaState L, CharPtr path) {
			// todo: the whole usage of plib here is wrong, fix it - mjf
		  //void **plib;
		  object plib = null;
		  LuaPushFString(L, "%s%s", LIBPREFIX, path);
		  LuaGetTable(L, LUA_REGISTRYINDEX);  /* check library in registry? */
		  if (!LuaIsNil(L, -1))  /* is there an entry? */
			plib = LuaToUserData(L, -1);
		  else {  /* no entry yet; create one */
			LuaPop(L, 1);
			//plib = lua_newuserdata(L, (uint)Marshal.SizeOf(plib));
			//plib[0] = null;
			LuaLGetMetatable(L, "_LOADLIB");
			LuaSetMetatable(L, -2);
			LuaPushFString(L, "%s%s", LIBPREFIX, path);
			LuaPushValue(L, -2);
			LuaSetTable(L, LUA_REGISTRYINDEX);
		  }
		  return plib;
		}


		/*
		** __gc tag method: calls library's `ll_unloadlib' function with the lib
		** handle
		*/
		private static int Gctm (LuaState L) {
		  object lib = LuaLCheckUData(L, 1, "_LOADLIB");
		  if (lib != null) LLUnloadLib(lib);
		  lib = null;  /* mark library as closed */
		  return 0;
		}


		private static int LLLoadFunc (LuaState L, CharPtr path, CharPtr sym) {
		  object reg = LLRegister(L, path);
		  if (reg == null) reg = LLLoad(L, path);
		  if (reg == null)
			return ERRLIB;  /* unable to load library */
		  else {
			LuaNativeFunction f = LLSym(L, reg, sym);
			if (f == null)
			  return ERRFUNC;  /* unable to find function */
			LuaPushCFunction(L, f);
			return 0;  /* return function */
		  }
		}


		private static int LLLoadLib (LuaState L) {
		  CharPtr path = LuaLCheckString(L, 1);
		  CharPtr init = LuaLCheckString(L, 2);
		  int stat = LLLoadFunc(L, path, init);
		  if (stat == 0)  /* no errors? */
			return 1;  /* return the loaded function */
		  else {  /* error; error message is on stack top */
			LuaPushNil(L);
			LuaInsert(L, -2);
			LuaPushString(L, (stat == ERRLIB) ?  LIBFAIL : "init");
			return 3;  /* return nil, error message, and where */
		  }
		}



		/*
		** {======================================================
		** 'require' function
		** =======================================================
		*/


		private static int Readable (CharPtr filename) {
		  Stream f = fopen(filename, "r");  /* try to open file */
		  if (f == null) return 0;  /* open failed */
		  fclose(f);
		  return 1;
		}


		private static CharPtr PushNextTemplate (LuaState L, CharPtr path) {
		  CharPtr l;
		  while (path[0] == LUA_PATHSEP[0]) path = path.next();  /* skip separators */
		  if (path[0] == '\0') return null;  /* no more templates */
		  l = strchr(path, LUA_PATHSEP[0]);  /* find next separator */
		  if (l == null) l = path + strlen(path);
		  LuaPushLString(L, path, (uint)(l - path));  /* template */
		  return l;
		}


		private static CharPtr FindFile (LuaState L, CharPtr name,
												   CharPtr pname) {
		  CharPtr path;
		  name = LuaLGSub(L, name, ".", LUA_DIRSEP);
		  LuaGetField(L, LUA_ENVIRONINDEX, pname);
		  path = LuaToString(L, -1);
		  if (path == null)
			LuaLError(L, LUA_QL("package.%s") + " must be a string", pname);
		  LuaPushLiteral(L, "");  /* error accumulator */
		  while ((path = PushNextTemplate(L, path)) != null) {
			CharPtr filename;
			filename = LuaLGSub(L, LuaToString(L, -1), LUA_PATH_MARK, name);
			LuaRemove(L, -2);  /* remove path template */
			if (Readable(filename) != 0)  /* does file exist and is readable? */
			  return filename;  /* return that file name */
			LuaPushFString(L, "\n\tno file " + LUA_QS, filename);
			LuaRemove(L, -2);  /* remove file name */
			LuaConcat(L, 2);  /* add entry to possible error message */
		  }
		  return null;  /* not found */
		}


		private static void LoadError (LuaState L, CharPtr filename) {
		  LuaLError(L, "error loading module " + LUA_QS + " from file " + LUA_QS + ":\n\t%s",
						LuaToString(L, 1), filename, LuaToString(L, -1));
		}


		private static int LoaderLua (LuaState L) {
		  CharPtr filename;
		  CharPtr name = LuaLCheckString(L, 1);
		  filename = FindFile(L, name, "path");
		  if (filename == null) return 1;  /* library not found in this path */
		  if (LuaLLoadFile(L, filename) != 0)
			LoadError(L, filename);
		  return 1;  /* library loaded successfully */
		}


		private static CharPtr MakeFuncName (LuaState L, CharPtr modname) {
		  CharPtr funcname;
		  CharPtr mark = strchr(modname, LUA_IGMARK[0]);
		  if (mark!=null) modname = mark + 1;
		  funcname = LuaLGSub(L, modname, ".", LUAOFSEP);
		  funcname = LuaPushFString(L, POF + "%s", funcname);
		  LuaRemove(L, -2);  /* remove 'gsub' result */
		  return funcname;
		}


		private static int LoaderC (LuaState L) {
		  CharPtr funcname;
		  CharPtr name = LuaLCheckString(L, 1);
		  CharPtr filename = FindFile(L, name, "cpath");
		  if (filename == null) return 1;  /* library not found in this path */
		  funcname = MakeFuncName(L, name);
		  if (LLLoadFunc(L, filename, funcname) != 0)
			LoadError(L, filename);
		  return 1;  /* library loaded successfully */
		}


		private static int LoaderCRoot (LuaState L) {
		  CharPtr funcname;
		  CharPtr filename;
		  CharPtr name = LuaLCheckString(L, 1);
		  CharPtr p = strchr(name, '.');
		  int stat;
		  if (p == null) return 0;  /* is root */
		  LuaPushLString(L, name, (uint)(p - name));
		  filename = FindFile(L, LuaToString(L, -1), "cpath");
		  if (filename == null) return 1;  /* root not found */
		  funcname = MakeFuncName(L, name);
		  if ((stat = LLLoadFunc(L, filename, funcname)) != 0) {
			if (stat != ERRFUNC) LoadError(L, filename);  /* real error */
			LuaPushFString(L, "\n\tno module " + LUA_QS + " in file " + LUA_QS,
							   name, filename);
			return 1;  /* function not found */
		  }
		  return 1;
		}


		private static int LoaderPreLoad (LuaState L) {
		  CharPtr name = LuaLCheckString(L, 1);
		  LuaGetField(L, LUA_ENVIRONINDEX, "preload");
		  if (!LuaIsTable(L, -1))
			LuaLError(L, LUA_QL("package.preload") + " must be a table");
		  LuaGetField(L, -1, name);
		  if (LuaIsNil(L, -1))  /* not found? */
			LuaPushFString(L, "\n\tno field package.preload['%s']", name);
		  return 1;
		}


		public static object sentinel = new object();


		public static int LLRequire (LuaState L) {
		  CharPtr name = LuaLCheckString(L, 1);
		  int i;
		  LuaSetTop(L, 1);  /* _LOADED table will be at index 2 */
		  LuaGetField(L, LUA_REGISTRYINDEX, "_LOADED");
		  LuaGetField(L, 2, name);
		  if (LuaToBoolean(L, -1) != 0) {  /* is it there? */
			if (LuaToUserData(L, -1) == sentinel)  /* check loops */
			  LuaLError(L, "loop or previous error loading module " + LUA_QS, name);
			return 1;  /* package is already loaded */
		  }
		  /* else must load it; iterate over available loaders */
		  LuaGetField(L, LUA_ENVIRONINDEX, "loaders");
		  if (!LuaIsTable(L, -1))
			LuaLError(L, LUA_QL("package.loaders") + " must be a table");
		  LuaPushLiteral(L, "");  /* error message accumulator */
		  for (i=1; ; i++) {
			LuaRawGetI(L, -2, i);  /* get a loader */
			if (LuaIsNil(L, -1))
			  LuaLError(L, "module " + LUA_QS + " not found:%s",
							name, LuaToString(L, -2));
			LuaPushString(L, name);
			LuaCall(L, 1, 1);  /* call it */
			if (LuaIsFunction(L, -1))  /* did it find module? */
			  break;  /* module loaded successfully */
			else if (LuaIsString(L, -1) != 0)  /* loader returned error message? */
			  LuaConcat(L, 2);  /* accumulate it */
			else
			  LuaPop(L, 1);
		  }
		  LuaPushLightUserData(L, sentinel);
		  LuaSetField(L, 2, name);  /* _LOADED[name] = sentinel */
		  LuaPushString(L, name);  /* pass name as argument to module */
		  LuaCall(L, 1, 1);  /* run loaded module */
		  if (!LuaIsNil(L, -1))  /* non-nil return? */
			LuaSetField(L, 2, name);  /* _LOADED[name] = returned value */
		  LuaGetField(L, 2, name);
		  if (LuaToUserData(L, -1) == sentinel) {   /* module did not set a value? */
			LuaPushBoolean(L, 1);  /* use true as result */
			LuaPushValue(L, -1);  /* extra copy to be returned */
			LuaSetField(L, 2, name);  /* _LOADED[name] = true */
		  }
		  return 1;
		}

		/* }====================================================== */



		/*
		** {======================================================
		** 'module' function
		** =======================================================
		*/
		  

		private static void SetFEnv (LuaState L) {
		  LuaDebug ar = new LuaDebug();
		  if (LuaGetStack(L, 1, ref ar) == 0 ||
			  LuaGetInfo(L, "f", ref ar) == 0 ||  /* get calling function */
			  LuaIsCFunction(L, -1))
			LuaLError(L, LUA_QL("module") + " not called from a Lua function");
		  LuaPushValue(L, -2);
		  LuaSetFEnv(L, -2);
		  LuaPop(L, 1);
		}


		private static void DoOptions (LuaState L, int n) {
		  int i;
		  for (i = 2; i <= n; i++) {
			LuaPushValue(L, i);  /* get option (a function) */
			LuaPushValue(L, -2);  /* module */
			LuaCall(L, 1, 0);
		  }
		}


		private static void ModInit (LuaState L, CharPtr modname) {
		  CharPtr dot;
		  LuaPushValue(L, -1);
		  LuaSetField(L, -2, "_M");  /* module._M = module */
		  LuaPushString(L, modname);
		  LuaSetField(L, -2, "_NAME");
		  dot = strrchr(modname, '.');  /* look for last dot in module name */
		  if (dot == null) dot = modname;
		  else dot = dot.next();
		  /* set _PACKAGE as package name (full module name minus last part) */
		  LuaPushLString(L, modname, (uint)(dot - modname));
		  LuaSetField(L, -2, "_PACKAGE");
		}


		private static int LLModule (LuaState L) {
		  CharPtr modname = LuaLCheckString(L, 1);
		  int loaded = LuaGetTop(L) + 1;  /* index of _LOADED table */
		  LuaGetField(L, LUA_REGISTRYINDEX, "_LOADED");
		  LuaGetField(L, loaded, modname);  /* get _LOADED[modname] */
		  if (!LuaIsTable(L, -1)) {  /* not found? */
			LuaPop(L, 1);  /* remove previous result */
			/* try global variable (and create one if it does not exist) */
			if (LuaLFindTable(L, LUA_GLOBALSINDEX, modname, 1) != null)
			  return LuaLError(L, "name conflict for module " + LUA_QS, modname);
			LuaPushValue(L, -1);
			LuaSetField(L, loaded, modname);  /* _LOADED[modname] = new table */
		  }
		  /* check whether table already has a _NAME field */
		  LuaGetField(L, -1, "_NAME");
		  if (!LuaIsNil(L, -1))  /* is table an initialized module? */
			LuaPop(L, 1);
		  else {  /* no; initialize it */
			LuaPop(L, 1);
			ModInit(L, modname);
		  }
		  LuaPushValue(L, -1);
		  SetFEnv(L);
		  DoOptions(L, loaded - 1);
		  return 0;
		}


		private static int LLSeeAll (LuaState L) {
		  LuaLCheckType(L, 1, LUA_TTABLE);
		  if (LuaGetMetatable(L, 1)==0) {
			LuaCreateTable(L, 0, 1); /* create new metatable */
			LuaPushValue(L, -1);
			LuaSetMetatable(L, 1);
		  }
		  LuaPushValue(L, LUA_GLOBALSINDEX);
		  LuaSetField(L, -2, "__index");  /* mt.__index = _G */
		  return 0;
		}


		/* }====================================================== */



		/* auxiliary mark (for internal use) */
		public readonly static string AUXMARK		= String.Format("{0}", (char)1);

		private static void SetPath (LuaState L, CharPtr fieldname, CharPtr envname,
										   CharPtr def) {
		  CharPtr path = getenv(envname);
		  if (path == null)  /* no environment variable? */
			LuaPushString(L, def);  /* use default */
		  else {
			/* replace ";;" by ";AUXMARK;" and then AUXMARK by default path */
			path = LuaLGSub(L, path, LUA_PATHSEP + LUA_PATHSEP,
									  LUA_PATHSEP + AUXMARK + LUA_PATHSEP);
			LuaLGSub(L, path, AUXMARK, def);
			LuaRemove(L, -2);
		  }
		  SetProgDir(L);
		  LuaSetField(L, -2, fieldname);
		}


		private readonly static LuaLReg[] PKFuncs = {
		  new LuaLReg("loadlib", LLLoadLib),
		  new LuaLReg("seeall", LLSeeAll),
		  new LuaLReg(null, null)
		};


		private readonly static LuaLReg[] LLFuncs = {
		  new LuaLReg("module", LLModule),
		  new LuaLReg("require", LLRequire),
		  new LuaLReg(null, null)
		};


		public readonly static LuaNativeFunction[] loaders =
		  {LoaderPreLoad, LoaderLua, LoaderC, LoaderCRoot, null};


		public static int LuaOpenPackage (LuaState L) {
		  int i;
		  /* create new type _LOADLIB */
		  LuaLNewMetatable(L, "_LOADLIB");
		  LuaPushCFunction(L, Gctm);
		  LuaSetField(L, -2, "__gc");
		  /* create `package' table */
		  LuaLRegister(L, LUA_LOADLIBNAME, PKFuncs);
		#if LUA_COMPAT_LOADLIB
		  lua_getfield(L, -1, "loadlib");
		  lua_setfield(L, LUA_GLOBALSINDEX, "loadlib");
		#endif
		  LuaPushValue(L, -1);
		  LuaReplace(L, LUA_ENVIRONINDEX);
		  /* create `loaders' table */
		  LuaCreateTable(L, loaders.Length - 1, 0);
		  /* fill it with pre-defined loaders */
		  for (i=0; loaders[i] != null; i++) {
			LuaPushCFunction(L, loaders[i]);
			LuaRawSetI(L, -2, i+1);
		  }
		  LuaSetField(L, -2, "loaders");  /* put it in field `loaders' */
		  SetPath(L, "path", LUA_PATH, LUA_PATH_DEFAULT);  /* set field `path' */
		  SetPath(L, "cpath", LUA_CPATH, LUA_CPATH_DEFAULT); /* set field `cpath' */
		  /* store config information */
		  LuaPushLiteral(L, LUA_DIRSEP + "\n" + LUA_PATHSEP + "\n" + LUA_PATH_MARK + "\n" +
							 LUA_EXECDIR + "\n" + LUA_IGMARK);
		  LuaSetField(L, -2, "config");
		  /* set field `loaded' */
		  LuaLFindTable(L, LUA_REGISTRYINDEX, "_LOADED", 2);
		  LuaSetField(L, -2, "loaded");
		  /* set field `preload' */
		  LuaNewTable(L);
		  LuaSetField(L, -2, "preload");
		  LuaPushValue(L, LUA_GLOBALSINDEX);
		  LuaLRegister(L, null, LLFuncs);  /* open lib into global table */
		  LuaPop(L, 1);
		  return 1;  /* return 'package' table */
		}

	}
}

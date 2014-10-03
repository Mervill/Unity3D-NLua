/*
** $Id: linit.c,v 1.14.1.1 2007/12/27 13:02:25 roberto Exp $
** Initialization of libraries for lua.c
** See Copyright Notice in lua.h
*/

using System;
using System.Collections.Generic;
using System.Text;

namespace KopiLua
{
	public partial class Lua
	{
		private readonly static LuaLReg[] lualibs = {
		  new LuaLReg("", LuaOpenBase),
		  new LuaLReg(LUA_LOADLIBNAME, LuaOpenPackage),
		  new LuaLReg(LUA_TABLIBNAME, luaopen_table),
		  new LuaLReg(LUA_IOLIBNAME, LuaOpenIo),
		  new LuaLReg(LUA_OSLIBNAME, LuaOpenOS),
		  new LuaLReg(LUA_STRLIBNAME, luaopen_string),
		  new LuaLReg(LUA_MATHLIBNAME, LuaOpenMath),
		  new LuaLReg(LUA_DBLIBNAME, LuaOpenDebug),
		  new LuaLReg(null, null)
		};


		public static void LuaLOpenLibs (LuaState L) {
		  for (int i=0; i<lualibs.Length-1; i++)
		  {
			LuaLReg lib = lualibs[i];
			LuaPushCFunction(L, lib.func);
			LuaPushString(L, lib.name);
			LuaCall(L, 1, 0);
		  }
		}

	}
}

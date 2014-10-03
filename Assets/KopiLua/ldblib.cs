/*
** $Id: ldblib.c,v 1.104.1.3 2008/01/21 13:11:21 roberto Exp $
** Interface from Lua to its debug API
** See Copyright Notice in lua.h
*/

using System;
using System.Collections.Generic;
using System.Text;

namespace KopiLua
{
	public partial class Lua
	{
		private static int DBGetRegistry (LuaState L) {
		  LuaPushValue(L, LUA_REGISTRYINDEX);
		  return 1;
		}


		private static int DBGetMetatable (LuaState L) {
		  LuaLCheckAny(L, 1);
		  if (LuaGetMetatable(L, 1) == 0) {
			LuaPushNil(L);  /* no metatable */
		  }
		  return 1;
		}


		private static int DBSetMetatable (LuaState L) {
		  int t = LuaType(L, 2);
		  LuaLArgCheck(L, t == LUA_TNIL || t == LUA_TTABLE, 2,
							"nil or table expected");
		  LuaSetTop(L, 2);
		  LuaPushBoolean(L, LuaSetMetatable(L, 1));
		  return 1;
		}


		private static int DBGetFEnv (LuaState L) {
		  LuaLCheckAny(L, 1);
		  LuaGetFEnv(L, 1);
		  return 1;
		}


		private static int DBSetFEnv (LuaState L) {
		  LuaLCheckType(L, 2, LUA_TTABLE);
		  LuaSetTop(L, 2);
		  if (LuaSetFEnv(L, 1) == 0)
			LuaLError(L, LUA_QL("setfenv") +
						  " cannot change environment of given object");
		  return 1;
		}


		private static void SetTabsS (LuaState L, CharPtr i, CharPtr v) {
		  LuaPushString(L, v);
		  LuaSetField(L, -2, i);
		}


		private static void SetTabSI (LuaState L, CharPtr i, int v) {
		  LuaPushInteger(L, v);
		  LuaSetField(L, -2, i);
		}


		private static LuaState GetThread (LuaState L, out int arg) {
		  if (LuaIsThread(L, 1)) {
			arg = 1;
			return LuaToThread(L, 1);
		  }
		  else {
			arg = 0;
			return L;
		  }
		}


		private static void TreatStackOption (LuaState L, LuaState L1, CharPtr fname) {
		  if (L == L1) {
			LuaPushValue(L, -2);
			LuaRemove(L, -3);
		  }
		  else
			LuaXMove(L1, L, 1);
		  LuaSetField(L, -2, fname);
		}


		private static int DBGetInfo (LuaState L) {
		  LuaDebug ar = new LuaDebug();
		  int arg;
		  LuaState L1 = GetThread(L, out arg);
		  CharPtr options = LuaLOptString(L, arg+2, "flnSu");
		  if (LuaIsNumber(L, arg+1) != 0) {
			if (LuaGetStack(L1, (int)LuaToInteger(L, arg+1), ref ar)==0) {
			  LuaPushNil(L);  /* level out of range */
			  return 1;
			}
		  }
		  else if (LuaIsFunction(L, arg+1)) {
			LuaPushFString(L, ">%s", options);
			options = LuaToString(L, -1);
			LuaPushValue(L, arg+1);
			LuaXMove(L, L1, 1);
		  }
		  else
			return LuaLArgError(L, arg+1, "function or level expected");
		  if (LuaGetInfo(L1, options,ref ar)==0)
			return LuaLArgError(L, arg+2, "invalid option");
		  LuaCreateTable(L, 0, 2);
		  if (strchr(options, 'S') != null) {
			SetTabsS(L, "source", ar.source);
			SetTabsS(L, "short_src", ar.short_src);
			SetTabSI(L, "linedefined", ar.linedefined);
			SetTabSI(L, "lastlinedefined", ar.lastlinedefined);
			SetTabsS(L, "what", ar.what);
		  }
		  if (strchr(options, 'l') != null)
			SetTabSI(L, "currentline", ar.currentline);
		  if (strchr(options, 'u')  != null)
			SetTabSI(L, "nups", ar.nups);
		  if (strchr(options, 'n')  != null) {
			SetTabsS(L, "name", ar.name);
			SetTabsS(L, "namewhat", ar.namewhat);
		  }
		  if (strchr(options, 'L') != null)
			TreatStackOption(L, L1, "activelines");
		  if (strchr(options, 'f')  != null)
			TreatStackOption(L, L1, "func");
		  return 1;  /* return table */
		}
		    

		private static int DBGetLocal (LuaState L) {
		  int arg;
		  LuaState L1 = GetThread(L, out arg);
		  LuaDebug ar = new LuaDebug();
		  CharPtr name;
		  if (LuaGetStack(L1, LuaLCheckInt(L, arg+1), ref ar)==0)  /* out of range? */
			return LuaLArgError(L, arg+1, "level out of range");
		  name = LuaGetLocal(L1, ar, LuaLCheckInt(L, arg+2));
		  if (name != null) {
			LuaXMove(L1, L, 1);
			LuaPushString(L, name);
			LuaPushValue(L, -2);
			return 2;
		  }
		  else {
			LuaPushNil(L);
			return 1;
		  }
		}


		private static int DBSetLocal (LuaState L) {
		  int arg;
		  LuaState L1 = GetThread(L, out arg);
		  LuaDebug ar = new LuaDebug();
		  if (LuaGetStack(L1, LuaLCheckInt(L, arg+1), ref ar)==0)  /* out of range? */
			return LuaLArgError(L, arg+1, "level out of range");
		  LuaLCheckAny(L, arg+3);
		  LuaSetTop(L, arg+3);
		  LuaXMove(L, L1, 1);
		  LuaPushString(L, LuaSetLocal(L1, ar, LuaLCheckInt(L, arg+2)));
		  return 1;
		}


		private static int AuxUpValue (LuaState L, int get) {
		  CharPtr name;
		  int n = LuaLCheckInt(L, 2);
		  LuaLCheckType(L, 1, LUA_TFUNCTION);
		  if (LuaIsCFunction(L, 1)) return 0;  /* cannot touch C upvalues from Lua */
		  name = (get!=0) ? LuaGetUpValue(L, 1, n) : LuaSetUpValue(L, 1, n);
		  if (name == null) return 0;
		  LuaPushString(L, name);
		  LuaInsert(L, -(get+1));
		  return get + 1;
		}


		private static int DBGetUpValue (LuaState L) {
		  return AuxUpValue(L, 1);
		}


		private static int DBSetUpValue (LuaState L) {
		  LuaLCheckAny(L, 3);
		  return AuxUpValue(L, 0);
		}



		private const string KEY_HOOK = "h";


		private static readonly string[] hooknames =
			{"call", "return", "line", "count", "tail return"};

		private static void HookF (LuaState L, LuaDebug ar) {
		  LuaPushLightUserData(L, KEY_HOOK);
		  LuaRawGet(L, LUA_REGISTRYINDEX);
		  LuaPushLightUserData(L, L);
		  LuaRawGet(L, -2);
		  if (LuaIsFunction(L, -1)) {
			LuaPushString(L, hooknames[(int)ar.event_]);
			if (ar.currentline >= 0)
			  LuaPushInteger(L, ar.currentline);
			else LuaPushNil(L);
			LuaAssert(LuaGetInfo(L, "lS",ref ar));
			LuaCall(L, 2, 0);
		  }
		}


		private static int MakeMask (CharPtr smask, int count) {
		  int mask = 0;
		  if (strchr(smask, 'c') != null) mask |= LUA_MASKCALL;
		  if (strchr(smask, 'r') != null) mask |= LUA_MASKRET;
		  if (strchr(smask, 'l') != null) mask |= LUA_MASKLINE;
		  if (count > 0) mask |= LUA_MASKCOUNT;
		  return mask;
		}


		private static CharPtr UnmakeMask (int mask, CharPtr smask) {
			int i = 0;
			if ((mask & LUA_MASKCALL) != 0) smask[i++] = 'c';
			if ((mask & LUA_MASKRET) != 0) smask[i++] = 'r';
			if ((mask & LUA_MASKLINE) != 0) smask[i++] = 'l';
			smask[i] = '\0';
			return smask;
		}


		private static void GetHookTable (LuaState L) {
		  LuaPushLightUserData(L, KEY_HOOK);
		  LuaRawGet(L, LUA_REGISTRYINDEX);
		  if (!LuaIsTable(L, -1)) {
			LuaPop(L, 1);
			LuaCreateTable(L, 0, 1);
			LuaPushLightUserData(L, KEY_HOOK);
			LuaPushValue(L, -2);
			LuaRawSet(L, LUA_REGISTRYINDEX);
		  }
		}


		private static int DBSetHook (LuaState L) {
		  int arg, mask, count;
		  LuaHook func;
		  LuaState L1 = GetThread(L, out arg);
		  if (LuaIsNoneOrNil(L, arg+1)) {
			LuaSetTop(L, arg+1);
			func = null; mask = 0; count = 0;  /* turn off hooks */
		  }
		  else {
			CharPtr smask = LuaLCheckString(L, arg+2);
			LuaLCheckType(L, arg+1, LUA_TFUNCTION);
			count = LuaLOptInt(L, arg+3, 0);
			func = HookF; mask = MakeMask(smask, count);
		  }
		  GetHookTable(L);
		  LuaPushLightUserData(L, L1);
		  LuaPushValue(L, arg+1);
		  LuaRawSet(L, -3);  /* set new hook */
		  LuaPop(L, 1);  /* remove hook table */
		  LuaSetHook(L1, func, mask, count);  /* set hooks */
		  return 0;
		}


		private static int DBGetHook (LuaState L) {
		  int arg;
		  LuaState L1 = GetThread(L, out arg);
		  CharPtr buff = new char[5];
		  int mask = LuaGetHookMask(L1);
		  LuaHook hook = LuaGetHook(L1);
		  if (hook != null && hook != HookF)  /* external hook? */
			LuaPushLiteral(L, "external hook");
		  else {
			GetHookTable(L);
			LuaPushLightUserData(L, L1);
			LuaRawGet(L, -2);   /* get hook */
			LuaRemove(L, -2);  /* remove hook table */
		  }
		  LuaPushString(L, UnmakeMask(mask, buff));
		  LuaPushInteger(L, LuaGetHookCount(L1));
		  return 3;
		}


		private static int DBDebug (LuaState L) {
		  for (;;) {
			CharPtr buffer = new char[250];
			fputs("lua_debug> ", stderr);
			if (fgets(buffer, stdin) == null ||
				strcmp(buffer, "cont\n") == 0)
			  return 0;
			if (LuaLLoadBuffer(L, buffer, (uint)strlen(buffer), "=(debug command)")!=0 ||
				LuaPCall(L, 0, 0, 0)!=0) {
			  fputs(LuaToString(L, -1), stderr);
			  fputs("\n", stderr);
			}
			LuaSetTop(L, 0);  /* remove eventual returns */
		  }
		}


		public const int LEVELS1	= 12;	/* size of the first part of the stack */
		public const int LEVELS2	= 10;	/* size of the second part of the stack */

		private static int DBErrorFB (LuaState L) {
		  int level;
		  bool firstpart = true;  /* still before eventual `...' */
		  int arg;
		  LuaState L1 = GetThread(L, out arg);
		  LuaDebug ar = new LuaDebug();
		  if (LuaIsNumber(L, arg+2) != 0) {
			level = (int)LuaToInteger(L, arg+2);
			LuaPop(L, 1);
		  }
		  else
			level = (L == L1) ? 1 : 0;  /* level 0 may be this own function */
		  if (LuaGetTop(L) == arg)
			LuaPushLiteral(L, "");
		  else if (LuaIsString(L, arg+1)==0) return 1;  /* message is not a string */
		  else LuaPushLiteral(L, "\n");
		  LuaPushLiteral(L, "stack traceback:");
		  while (LuaGetStack(L1, level++, ref ar) != 0) {
			if (level > LEVELS1 && firstpart) {
			  /* no more than `LEVELS2' more levels? */
			  if (LuaGetStack(L1, level+LEVELS2, ref ar)==0)
				level--;  /* keep going */
			  else {
				LuaPushLiteral(L, "\n\t...");  /* too many levels */
				while (LuaGetStack(L1, level+LEVELS2, ref ar) != 0)  /* find last levels */
				  level++;
			  }
			  firstpart = false;
			  continue;
			}
			LuaPushLiteral(L, "\n\t");
			LuaGetInfo(L1, "Snl", ref ar);
			LuaPushFString(L, "%s:", ar.short_src);
			if (ar.currentline > 0)
			  LuaPushFString(L, "%d:", ar.currentline);
			if (ar.namewhat != '\0')  /* is there a name? */
				LuaPushFString(L, " in function " + LUA_QS, ar.name);
			else {
			  if (ar.what == 'm')  /* main? */
				LuaPushFString(L, " in main chunk");
			  else if (ar.what == 'C' || ar.what == 't')
				LuaPushLiteral(L, " ?");  /* C function or tail call */
			  else
				LuaPushFString(L, " in function <%s:%d>",
								   ar.short_src, ar.linedefined);
			}
			LuaConcat(L, LuaGetTop(L) - arg);
		  }
		  LuaConcat(L, LuaGetTop(L) - arg);
		  return 1;
		}


		private readonly static LuaLReg[] dblib = {
		  new LuaLReg("debug", DBDebug),
		  new LuaLReg("getfenv", DBGetFEnv),
		  new LuaLReg("gethook", DBGetHook),
		  new LuaLReg("getinfo", DBGetInfo),
		  new LuaLReg("getlocal", DBGetLocal),
		  new LuaLReg("getregistry", DBGetRegistry),
		  new LuaLReg("getmetatable", DBGetMetatable),
		  new LuaLReg("getupvalue", DBGetUpValue),
		  new LuaLReg("setfenv", DBSetFEnv),
		  new LuaLReg("sethook", DBSetHook),
		  new LuaLReg("setlocal", DBSetLocal),
		  new LuaLReg("setmetatable", DBSetMetatable),
		  new LuaLReg("setupvalue", DBSetUpValue),
		  new LuaLReg("traceback", DBErrorFB),
		  new LuaLReg(null, null)
		};


		public static int LuaOpenDebug (LuaState L) {
		  LuaLRegister(L, LUA_DBLIBNAME, dblib);
		  return 1;
		}

	}
}

/*
** $Id: lbaselib.c,v 1.191.1.6 2008/02/14 16:46:22 roberto Exp $
** Basic library
** See Copyright Notice in lua.h
*/

using System;
using System.Collections.Generic;
using System.Text;

namespace KopiLua
{
	using LuaNumberType = System.Double;

	public partial class Lua
	{
		/*
		** If your system does not support `stdout', you can just remove this function.
		** If you need, you can define your own `print' function, following this
		** model but changing `fputs' to put the strings at a proper place
		** (a console window or a log file, for instance).
		*/
		private static int LuaBPrint (LuaState L) {
		  int n = LuaGetTop(L);  /* number of arguments */
		  int i;
		  LuaGetGlobal(L, "tostring");
		  for (i=1; i<=n; i++) {
			CharPtr s;
			LuaPushValue(L, -1);  /* function to be called */
			LuaPushValue(L, i);   /* value to print */
			LuaCall(L, 1, 1);
			s = LuaToString(L, -1);  /* get result */
			if (s == null)
			  return LuaLError(L, LUA_QL("tostring") + " must return a string to " +
								   LUA_QL("print"));
			if (i > 1) fputs("\t", stdout);
			fputs(s, stdout);
			LuaPop(L, 1);  /* pop result */
		  }
		  Console.Write("\n", stdout);
		  return 0;
		}


		private static int LuaBToNumber (LuaState L) {
		  int base_ = LuaLOptInt(L, 2, 10);
		  if (base_ == 10) {  /* standard conversion */
			LuaLCheckAny(L, 1);
			if (LuaIsNumber(L, 1) != 0) {
			  LuaPushNumber(L, LuaToNumber(L, 1));
			  return 1;
			}
		  }
		  else {
			CharPtr s1 = LuaLCheckString(L, 1);
			CharPtr s2;
			ulong n;
			LuaLArgCheck(L, 2 <= base_ && base_ <= 36, 2, "base out of range");
			n = strtoul(s1, out s2, base_);
			if (s1 != s2) {  /* at least one valid digit? */
			  while (isspace((byte)(s2[0]))) s2 = s2.next();  /* skip trailing spaces */
			  if (s2[0] == '\0') {  /* no invalid trailing characters? */
				LuaPushNumber(L, (LuaNumberType)n);
				return 1;
			  }
			}
		  }
		  LuaPushNil(L);  /* else not a number */
		  return 1;
		}


		private static int LuaBError (LuaState L) {
		  int level = LuaLOptInt(L, 2, 1);
		  LuaSetTop(L, 1);
		  if ((LuaIsString(L, 1)!=0) && (level > 0)) {  /* add extra information? */
			LuaLWhere(L, level);
			LuaPushValue(L, 1);
			LuaConcat(L, 2);
		  }
		  return LuaError(L);
		}


		private static int LuaBGetMetatable (LuaState L) {
		  LuaLCheckAny(L, 1);
		  if (LuaGetMetatable(L, 1)==0) {
			LuaPushNil(L);
			return 1;  /* no metatable */
		  }
		  LuaLGetMetafield(L, 1, "__metatable");
		  return 1;  /* returns either __metatable field (if present) or metatable */
		}


		private static int LuaBSetMetatable (LuaState L) {
		  int t = LuaType(L, 2);
		  LuaLCheckType(L, 1, LUA_TTABLE);
		  LuaLArgCheck(L, t == LUA_TNIL || t == LUA_TTABLE, 2,
							"nil or table expected");
		  if (LuaLGetMetafield(L, 1, "__metatable") != 0)
			LuaLError(L, "cannot change a protected metatable");
		  LuaSetTop(L, 2);
		  LuaSetMetatable(L, 1);
		  return 1;
		}


		private static void GetFunc (LuaState L, int opt) {
		  if (LuaIsFunction(L, 1)) LuaPushValue(L, 1);
		  else {
			LuaDebug ar = new LuaDebug();
			int level = (opt != 0) ? LuaLOptInt(L, 1, 1) : LuaLCheckInt(L, 1);
			LuaLArgCheck(L, level >= 0, 1, "level must be non-negative");
			if (LuaGetStack(L, level, ref ar) == 0)
			  LuaLArgError(L, 1, "invalid level");
			LuaGetInfo(L, "f", ref ar);
			if (LuaIsNil(L, -1))
			  LuaLError(L, "no function environment for tail call at level %d",
							level);
		  }
		}


		private static int LuaBGetFEnv (LuaState L) {
		  GetFunc(L, 1);
		  if (LuaIsCFunction(L, -1))  /* is a C function? */
			LuaPushValue(L, LUA_GLOBALSINDEX);  /* return the thread's global env. */
		  else
			LuaGetFEnv(L, -1);
		  return 1;
		}


		private static int LuaBSetFEnv (LuaState L) {
		  LuaLCheckType(L, 2, LUA_TTABLE);
		  GetFunc(L, 0);
		  LuaPushValue(L, 2);
		  if ((LuaIsNumber(L, 1)!=0) && (LuaToNumber(L, 1) == 0)) {
			/* change environment of current thread */
			LuaPushThread(L);
			LuaInsert(L, -2);
			LuaSetFEnv(L, -2);
			return 0;
		  }
		  else if (LuaIsCFunction(L, -2) || LuaSetFEnv(L, -2) == 0)
			LuaLError(L,
				  LUA_QL("setfenv") + " cannot change environment of given object");
		  return 1;
		}


		private static int LuaBRawEqual (LuaState L) {
		  LuaLCheckAny(L, 1);
		  LuaLCheckAny(L, 2);
		  LuaPushBoolean(L, LuaRawEqual(L, 1, 2));
		  return 1;
		}


		private static int LuaBRawGet (LuaState L) {
		  LuaLCheckType(L, 1, LUA_TTABLE);
		  LuaLCheckAny(L, 2);
		  LuaSetTop(L, 2);
		  LuaRawGet(L, 1);
		  return 1;
		}

		private static int LuaBRawSet (LuaState L) {
		  LuaLCheckType(L, 1, LUA_TTABLE);
		  LuaLCheckAny(L, 2);
		  LuaLCheckAny(L, 3);
		  LuaSetTop(L, 3);
		  LuaRawSet(L, 1);
		  return 1;
		}


		private static int LuaBGGInfo (LuaState L) {
		  LuaPushInteger(L, LuaGetGCCount(L));
		  return 1;
		}

		public static readonly CharPtr[] opts = {"stop", "restart", "collect",
			"count", "step", "setpause", "setstepmul", null};
		public readonly static int[] optsnum = {LUA_GCSTOP, LUA_GCRESTART, LUA_GCCOLLECT,
			LUA_GCCOUNT, LUA_GCSTEP, LUA_GCSETPAUSE, LUA_GCSETSTEPMUL};

		private static int LuaBCollectGarbage (LuaState L) {		  
		  int o = LuaLCheckOption(L, 1, "collect", opts);
		  int ex = LuaLOptInt(L, 2, 0);
		  int res = LuaGC(L, optsnum[o], ex);
		  switch (optsnum[o]) {
			case LUA_GCCOUNT: {
			  int b = LuaGC(L, LUA_GCCOUNTB, 0);
			  LuaPushNumber(L, res + ((LuaNumberType)b/1024));
			  return 1;
			}
			case LUA_GCSTEP: {
			  LuaPushBoolean(L, res);
			  return 1;
			}
			default: {
			  LuaPushNumber(L, res);
			  return 1;
			}
		  }
		}


		private static int LuaBType (LuaState L) {
		  LuaLCheckAny(L, 1);
		  LuaPushString(L, LuaLTypeName(L, 1));
		  return 1;
		}


		private static int LuaBNext (LuaState L) {
		  LuaLCheckType(L, 1, LUA_TTABLE);
		  LuaSetTop(L, 2);  /* create a 2nd argument if there isn't one */
		  if (LuaNext(L, 1) != 0)
			return 2;
		  else {
			LuaPushNil(L);
			return 1;
		  }
		}


		private static int LuaBPairs (LuaState L) {
		  LuaLCheckType(L, 1, LUA_TTABLE);
		  LuaPushValue(L, LuaUpValueIndex(1));  /* return generator, */
		  LuaPushValue(L, 1);  /* state, */
		  LuaPushNil(L);  /* and initial value */
		  return 3;
		}


		private static int CheckPairsAux (LuaState L) {
		  int i = LuaLCheckInt(L, 2);
		  LuaLCheckType(L, 1, LUA_TTABLE);
		  i++;  /* next value */
		  LuaPushInteger(L, i);
		  LuaRawGetI(L, 1, i);
		  return (LuaIsNil(L, -1)) ? 0 : 2;
		}


		private static int LuaBIPairs (LuaState L) {
		  LuaLCheckType(L, 1, LUA_TTABLE);
		  LuaPushValue(L, LuaUpValueIndex(1));  /* return generator, */
		  LuaPushValue(L, 1);  /* state, */
		  LuaPushInteger(L, 0);  /* and initial value */
		  return 3;
		}


		private static int LoadAux (LuaState L, int status) {
		  if (status == 0)  /* OK? */
			return 1;
		  else {
			LuaPushNil(L);
			LuaInsert(L, -2);  /* put before error message */
			return 2;  /* return nil plus error message */
		  }
		}


		private static int LuaBLoadString (LuaState L) {
		  uint l;
		  CharPtr s = LuaLCheckLString(L, 1, out l);
		  CharPtr chunkname = LuaLOptString(L, 2, s);
		  return LoadAux(L, LuaLLoadBuffer(L, s, l, chunkname));
		}


		private static int LuaBLoadFile (LuaState L) {
		  CharPtr fname = LuaLOptString(L, 1, null);
		  return LoadAux(L, LuaLLoadFile(L, fname));
		}


		/*
		** Reader for generic `load' function: `lua_load' uses the
		** stack for internal stuff, so the reader cannot change the
		** stack top. Instead, it keeps its resulting string in a
		** reserved slot inside the stack.
		*/
		private static CharPtr GenericReader (LuaState L, object ud, out uint size) {
		  //(void)ud;  /* to avoid warnings */
		  LuaLCheckStack(L, 2, "too many nested functions");
		  LuaPushValue(L, 1);  /* get function */
		  LuaCall(L, 0, 1);  /* call it */
		  if (LuaIsNil(L, -1)) {
			size = 0;
			return null;
		  }
		  else if (LuaIsString(L, -1) != 0)
		  {
			  LuaReplace(L, 3);  /* save string in a reserved stack slot */
			  return LuaToLString(L, 3, out size);
		  }
		  else
		  {
			  size = 0;
			  LuaLError(L, "reader function must return a string");
		  }
		  return null;  /* to avoid warnings */
		}


		private static int LuaBLoad (LuaState L) {
		  int status;
		  CharPtr cname = LuaLOptString(L, 2, "=(load)");
		  LuaLCheckType(L, 1, LUA_TFUNCTION);
		  LuaSetTop(L, 3);  /* function, eventual name, plus one reserved slot */
		  status = LuaLoad(L, GenericReader, null, cname);
		  return LoadAux(L, status);
		}


		private static int LuaBDoFile (LuaState L) {
		  CharPtr fname = LuaLOptString(L, 1, null);
		  int n = LuaGetTop(L);
		  if (LuaLLoadFile(L, fname) != 0) LuaError(L);
		  LuaCall(L, 0, LUA_MULTRET);
		  return LuaGetTop(L) - n;
		}


		private static int LuaBAssert (LuaState L) {
		  LuaLCheckAny(L, 1);
		  if (LuaToBoolean(L, 1)==0)
			return LuaLError(L, "%s", LuaLOptString(L, 2, "assertion failed!"));
		  return LuaGetTop(L);
		}


		private static int LuaBUnpack (LuaState L) {
		  int i, e, n;
		  LuaLCheckType(L, 1, LUA_TTABLE);
		  i = LuaLOptInt(L, 2, 1);
		  e = LuaLOptInteger(L, LuaLCheckInt, 3, LuaLGetN(L, 1));
		  if (i > e) return 0;  /* empty range */
		  n = e - i + 1;  /* number of elements */
		  if (n <= 0 || (LuaCheckStack(L, n)==0))  /* n <= 0 means arith. overflow */
			return LuaLError(L, "too many results to unpack");
		  LuaRawGetI(L, 1, i);  /* push arg[i] (avoiding overflow problems) */
		  while (i++ < e)  /* push arg[i + 1...e] */
			LuaRawGetI(L, 1, i);
		  return n;
		}


		private static int LuaBSelect (LuaState L) {
		  int n = LuaGetTop(L);
		  if (LuaType(L, 1) == LUA_TSTRING && LuaToString(L, 1)[0] == '#') {
			LuaPushInteger(L, n-1);
			return 1;
		  }
		  else {
			int i = LuaLCheckInt(L, 1);
			if (i < 0) i = n + i;
			else if (i > n) i = n;
			LuaLArgCheck(L, 1 <= i, 1, "index out of range");
			return n - i;
		  }
		}


		private static int LuaBPCall (LuaState L) {
		  int status;
		  LuaLCheckAny(L, 1);
		  status = LuaPCall(L, LuaGetTop(L) - 1, LUA_MULTRET, 0);
		  LuaPushBoolean(L, (status == 0) ? 1 : 0);
		  LuaInsert(L, 1);
		  return LuaGetTop(L);  /* return status + all results */
		}


		private static int LuaBXPCall (LuaState L) {
		  int status;
		  LuaLCheckAny(L, 2);
		  LuaSetTop(L, 2);
		  LuaInsert(L, 1);  /* put error function under function to be called */
		  status = LuaPCall(L, 0, LUA_MULTRET, 1);
		  LuaPushBoolean(L, (status == 0) ? 1 : 0);
		  LuaReplace(L, 1);
		  return LuaGetTop(L);  /* return status + all results */
		}


		private static int LuaBToString (LuaState L) {
		  LuaLCheckAny(L, 1);
		  if (LuaLCallMeta(L, 1, "__tostring") != 0)  /* is there a metafield? */
			return 1;  /* use its value */
		  switch (LuaType(L, 1)) {
			case LUA_TNUMBER:
			  LuaPushString(L, LuaToString(L, 1));
			  break;
			case LUA_TSTRING:
			  LuaPushValue(L, 1);
			  break;
			case LUA_TBOOLEAN:
			  LuaPushString(L, (LuaToBoolean(L, 1) != 0 ? "true" : "false"));
			  break;
			case LUA_TNIL:
			  LuaPushLiteral(L, "nil");
			  break;
			default:
			  LuaPushFString(L, "%s: %p", LuaLTypeName(L, 1), LuaToPointer(L, 1));
			  break;
		  }
		  return 1;
		}


		private static int LuaBNewProxy (LuaState L) {
		  LuaSetTop(L, 1);
		  LuaNewUserData(L, 0);  /* create proxy */
		  if (LuaToBoolean(L, 1) == 0)
			return 1;  /* no metatable */
		  else if (LuaIsBoolean(L, 1)) {
			LuaNewTable(L);  /* create a new metatable `m' ... */
			LuaPushValue(L, -1);  /* ... and mark `m' as a valid metatable */
			LuaPushBoolean(L, 1);
			LuaRawSet(L, LuaUpValueIndex(1));  /* weaktable[m] = true */
		  }
		  else {
			int validproxy = 0;  /* to check if weaktable[metatable(u)] == true */
			if (LuaGetMetatable(L, 1) != 0) {
			  LuaRawGet(L, LuaUpValueIndex(1));
			  validproxy = LuaToBoolean(L, -1);
			  LuaPop(L, 1);  /* remove value */
			}
			LuaLArgCheck(L, validproxy!=0, 1, "boolean or proxy expected");
			LuaGetMetatable(L, 1);  /* metatable is valid; get it */
		  }
		  LuaSetMetatable(L, 2);
		  return 1;
		}


		private readonly static LuaLReg[] base_funcs = {
		  new LuaLReg("assert", LuaBAssert),
		  new LuaLReg("collectgarbage", LuaBCollectGarbage),
		  new LuaLReg("dofile", LuaBDoFile),
		  new LuaLReg("error", LuaBError),
		  new LuaLReg("gcinfo", LuaBGGInfo),
		  new LuaLReg("getfenv", LuaBGetFEnv),
		  new LuaLReg("getmetatable", LuaBGetMetatable),
		  new LuaLReg("loadfile", LuaBLoadFile),
		  new LuaLReg("load", LuaBLoad),
		  new LuaLReg("loadstring", LuaBLoadString),
		  new LuaLReg("next", LuaBNext),
		  new LuaLReg("pcall", LuaBPCall),
		  new LuaLReg("print", LuaBPrint),
		  new LuaLReg("rawequal", LuaBRawEqual),
		  new LuaLReg("rawget", LuaBRawGet),
		  new LuaLReg("rawset", LuaBRawSet),
		  new LuaLReg("select", LuaBSelect),
		  new LuaLReg("setfenv", LuaBSetFEnv),
		  new LuaLReg("setmetatable", LuaBSetMetatable),
		  new LuaLReg("tonumber", LuaBToNumber),
		  new LuaLReg("tostring", LuaBToString),
		  new LuaLReg("type", LuaBType),
		  new LuaLReg("unpack", LuaBUnpack),
		  new LuaLReg("xpcall", LuaBXPCall),
		  new LuaLReg(null, null)
		};


		/*
		** {======================================================
		** Coroutine library
		** =======================================================
		*/

		public const int CO_RUN		= 0;	/* running */
		public const int CO_SUS		= 1;	/* suspended */
		public const int CO_NOR		= 2;	/* 'normal' (it resumed another coroutine) */
		public const int CO_DEAD	= 3;

		private static readonly string[] statnames =
			{"running", "suspended", "normal", "dead"};

		private static int costatus (LuaState L, LuaState co) {
		  if (L == co) return CO_RUN;
		  switch (LuaStatus(co)) {
			case LUA_YIELD:
			  return CO_SUS;
			case 0: {
			  LuaDebug ar = new LuaDebug();
			  if (LuaGetStack(co, 0,ref ar) > 0)  /* does it have frames? */
				return CO_NOR;  /* it is running */
			  else if (LuaGetTop(co) == 0)
				  return CO_DEAD;
			  else
				return CO_SUS;  /* initial state */
			}
			default:  /* some error occured */
			  return CO_DEAD;
		  }
		}


		private static int LuaBCosStatus (LuaState L) {
		  LuaState co = LuaToThread(L, 1);
		  LuaLArgCheck(L, co!=null, 1, "coroutine expected");
		  LuaPushString(L, statnames[costatus(L, co)]);
		  return 1;
		}


		private static int AuxResume (LuaState L, LuaState co, int narg) {
		  int status = costatus(L, co);
		  if (LuaCheckStack(co, narg)==0)
			LuaLError(L, "too many arguments to resume");
		  if (status != CO_SUS) {
			LuaPushFString(L, "cannot resume %s coroutine", statnames[status]);
			return -1;  /* error flag */
		  }
		  LuaXMove(L, co, narg);
		  LuaSetLevel(L, co);
		  status = LuaResume(co, narg);
		  if (status == 0 || status == LUA_YIELD) {
			int nres = LuaGetTop(co);
			if (LuaCheckStack(L, nres + 1)==0)
			  LuaLError(L, "too many results to resume");
			LuaXMove(co, L, nres);  /* move yielded values */
			return nres;
		  }
		  else {
			LuaXMove(co, L, 1);  /* move error message */
			return -1;  /* error flag */
		  }
		}


		private static int LuaBCorResume (LuaState L) {
		  LuaState co = LuaToThread(L, 1);
		  int r;
		  LuaLArgCheck(L, co!=null, 1, "coroutine expected");
		  r = AuxResume(L, co, LuaGetTop(L) - 1);
		  if (r < 0) {
			LuaPushBoolean(L, 0);
			LuaInsert(L, -2);
			return 2;  /* return false + error message */
		  }
		  else {
			LuaPushBoolean(L, 1);
			LuaInsert(L, -(r + 1));
			return r + 1;  /* return true + `resume' returns */
		  }
		}


		private static int LuaBAuxWrap (LuaState L) {
		  LuaState co = LuaToThread(L, LuaUpValueIndex(1));
		  int r = AuxResume(L, co, LuaGetTop(L));
		  if (r < 0) {
			if (LuaIsString(L, -1) != 0) {  /* error object is a string? */
			  LuaLWhere(L, 1);  /* add extra info */
			  LuaInsert(L, -2);
			  LuaConcat(L, 2);
			}
			LuaError(L);  /* propagate error */
		  }
		  return r;
		}


		private static int LuaBCoCreate (LuaState L) {
		  LuaState NL = LuaNewThread(L);
		  LuaLArgCheck(L, LuaIsFunction(L, 1) && !LuaIsCFunction(L, 1), 1,
			"Lua function expected");
		  LuaPushValue(L, 1);  /* move function to top */
		  LuaXMove(L, NL, 1);  /* move function from L to NL */
		  return 1;
		}


		private static int LuaBCoWrap (LuaState L) {
		  LuaBCoCreate(L);
		  LuaPushCClosure(L, LuaBAuxWrap, 1);
		  return 1;
		}


		private static int LuaBYield (LuaState L) {
		  return LuaYield(L, LuaGetTop(L));
		}


		private static int LuaBCoRunning (LuaState L) {
		  if (LuaPushThread(L) != 0)
			LuaPushNil(L);  /* main thread is not a coroutine */
		  return 1;
		}


		private readonly static LuaLReg[] co_funcs = {
		  new LuaLReg("create", LuaBCoCreate),
		  new LuaLReg("resume", LuaBCorResume),
		  new LuaLReg("running", LuaBCoRunning),
		  new LuaLReg("status", LuaBCosStatus),
		  new LuaLReg("wrap", LuaBCoWrap),
		  new LuaLReg("yield", LuaBYield),
		  new LuaLReg(null, null)
		};

		/* }====================================================== */


		private static void AuxOpen (LuaState L, CharPtr name,
							 LuaNativeFunction f, LuaNativeFunction u) {
		  LuaPushCFunction(L, u);
		  LuaPushCClosure(L, f, 1);
		  LuaSetField(L, -2, name);
		}


		private static void BaseOpen (LuaState L) {
		  /* set global _G */
		  LuaPushValue(L, LUA_GLOBALSINDEX);
		  LuaSetGlobal(L, "_G");
		  /* open lib into global table */
		  LuaLRegister(L, "_G", base_funcs);
		  LuaPushLiteral(L, LUA_VERSION);
		  LuaSetGlobal(L, "_VERSION");  /* set global _VERSION */
		  /* `ipairs' and `pairs' need auxiliary functions as upvalues */
		  AuxOpen(L, "ipairs", LuaBIPairs, CheckPairsAux);
		  AuxOpen(L, "pairs", LuaBPairs, LuaBNext);
		  /* `newproxy' needs a weaktable as upvalue */
		  LuaCreateTable(L, 0, 1);  /* new table `w' */
		  LuaPushValue(L, -1);  /* `w' will be its own metatable */
		  LuaSetMetatable(L, -2);
		  LuaPushLiteral(L, "kv");
		  LuaSetField(L, -2, "__mode");  /* metatable(w).__mode = "kv" */
		  LuaPushCClosure(L, LuaBNewProxy, 1);
		  LuaSetGlobal(L, "newproxy");  /* set global `newproxy' */
		  /** KopiLua Hack - Add L state to registry to get inside coroutine ***/
		  LuaPushThread (L);
		  LuaSetField (L, LUA_REGISTRYINDEX, "main_state");
		}


		public static int LuaOpenBase (LuaState L) {
		  BaseOpen(L);
		  LuaLRegister(L, LUA_COLIBNAME, co_funcs);
		  return 2;
		}

	}
}

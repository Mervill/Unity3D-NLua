/*
** $Id: lapi.c,v 2.55.1.5 2008/07/04 18:41:18 roberto Exp $
** Lua API
** See Copyright Notice in lua.h
*/

using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace KopiLua
{
	using lu_mem = System.UInt32;
	using TValue = Lua.LuaTypeValue;
	using StkId = Lua.LuaTypeValue;
	using lua_Integer = System.Int32;
	using lua_Number = System.Double;
	using ptrdiff_t = System.Int32;
	using ZIO = Lua.Zio;

	public partial class Lua
	{
		public const string LuaIdent =
		  "$Lua: " + LUA_RELEASE + " " + LUA_COPYRIGHT + " $\n" +
		  "$Authors: " + LUA_AUTHORS + " $\n" +
		  "$URL: www.lua.org $\n";

		public static void CheckNElements(LuaState L, int n)
		{
			ApiCheck(L, n <= L.top - L.base_);
		}

		public static void CheckValidIndex(LuaState L, StkId i)
		{
			ApiCheck(L, i != LuaONilObject);
		}

		public static void IncrementTop(LuaState L)
		{
			ApiCheck(L, L.top < L.ci.top);
			StkId.Inc(ref L.top);
		}



		static TValue Index2Address (LuaState L, int idx) {
		  if (idx > 0) {
			TValue o = L.base_ + (idx - 1);
			ApiCheck(L, idx <= L.ci.top - L.base_);
			if (o >= L.top) return LuaONilObject;
			else return o;
		  }
		  else if (idx > LUA_REGISTRYINDEX) {
			ApiCheck(L, idx != 0 && -idx <= L.top - L.base_);
			return L.top + idx;
		  }
		  else switch (idx) {  /* pseudo-indices */
			case LUA_REGISTRYINDEX: return Registry(L);
			case LUA_ENVIRONINDEX: {
			  Closure func = CurrFunc(L);
			  SetHValue(L, L.env, func.c.env);
			  return L.env;
			}
			case LUA_GLOBALSINDEX: return Gt(L);
			default: {
			  Closure func = CurrFunc(L);
			  idx = LUA_GLOBALSINDEX - idx;
			  return (idx <= func.c.nupvalues)
						? func.c.upvalue[idx-1]
						: (TValue)LuaONilObject;
			}
		  }
		}


		private static Table GetCurrentEnv (LuaState L) {
		  if (L.ci == L.base_ci[0])  /* no enclosing function? */
			return HValue(Gt(L));  /* use global table as environment */
		  else {
			Closure func = CurrFunc(L);
			return func.c.env;
		  }
		}


		public static void LuaAPushObject (LuaState L, TValue o) {
		  SetObj2S(L, L.top, o);
		  IncrementTop(L);
		}


		public static int LuaCheckStack (LuaState L, int size) {
		  int res = 1;
		  LuaLock(L);
		  if (size > LUAI_MAXCSTACK || (L.top - L.base_ + size) > LUAI_MAXCSTACK)
			res = 0;  /* stack overflow */
		  else if (size > 0) {
			LuaDCheckStack(L, size);
			if (L.ci.top < L.top + size)
			  L.ci.top = L.top + size;
		  }
		  LuaUnlock(L);
		  return res;
		}


		public static void LuaXMove (LuaState from, LuaState to, int n) {
		  int i;
		  if (from == to) return;
		  LuaLock(to);
		  CheckNElements(from, n);
		  ApiCheck(from, G(from) == G(to));
		  ApiCheck(from, to.ci.top - to.top >= n);
		  from.top -= n;
		  for (i = 0; i < n; i++) {
			SetObj2S(to, StkId.Inc(ref to.top), from.top + i);
		  }
		  LuaUnlock(to);
		}


		public static void LuaSetLevel (LuaState from, LuaState to) {
		  to.nCcalls = from.nCcalls;
		}


		public static LuaNativeFunction LuaAtPanic (LuaState L, LuaNativeFunction panicf) {
		  LuaNativeFunction old;
		  LuaLock(L);
		  old = G(L).panic;
		  G(L).panic = panicf;
		  LuaUnlock(L);
		  return old;
		}


		public static LuaState LuaNewThread (LuaState L) {
		  LuaState L1;
		  LuaLock(L);
		  LuaCCheckGC(L);
		  L1 = luaE_newthread(L);
		  SetTTHValue(L, L.top, L1);
		  IncrementTop(L);
		  LuaUnlock(L);
		  luai_userstatethread(L, L1);
		  return L1;
		}



		/*
		** basic stack manipulation
		*/


		public static int LuaGetTop (LuaState L) {
		  return CastInt(L.top - L.base_);
		}


		public static void LuaSetTop (LuaState L, int idx) {
		  LuaLock(L);
		  if (idx >= 0) {
			ApiCheck(L, idx <= L.stack_last - L.base_);
			while (L.top < L.base_ + idx)
			  SetNilValue(StkId.Inc(ref L.top));
			L.top = L.base_ + idx;
		  }
		  else {
			ApiCheck(L, -(idx+1) <= (L.top - L.base_));
			L.top += idx+1;  /* `subtract' index (index is negative) */
		  }
		  LuaUnlock(L);
		}


		public static void LuaRemove (LuaState L, int idx) {
		  StkId p;
		  LuaLock(L);
		  p = Index2Address(L, idx);
		  CheckValidIndex(L, p);
		  while ((p=p[1]) < L.top) SetObj2S(L, p-1, p);
		  StkId.Dec(ref L.top);
		  LuaUnlock(L);
		}


		public static void LuaInsert (LuaState L, int idx) {
		  StkId p;
		  StkId q;
		  LuaLock(L);
		  p = Index2Address(L, idx);
		  CheckValidIndex(L, p);
		  for (q = L.top; q>p; StkId.Dec(ref q)) SetObj2S(L, q, q-1);
		  SetObj2S(L, p, L.top);
		  LuaUnlock(L);
		}


		public static void LuaReplace (LuaState L, int idx) {
		  StkId o;
		  LuaLock(L);
		  /* explicit test for incompatible code */
		  if (idx == LUA_ENVIRONINDEX && L.ci == L.base_ci[0])
			LuaGRunError(L, "no calling environment");
		  CheckNElements(L, 1);
		  o = Index2Address(L, idx);
		  CheckValidIndex(L, o);
		  if (idx == LUA_ENVIRONINDEX) {
			Closure func = CurrFunc(L);
			ApiCheck(L, TTIsTable(L.top - 1)); 
			func.c.env = HValue(L.top - 1);
			LuaCBarrier(L, func, L.top - 1);
		  }
		  else {
			SetObj(L, o, L.top - 1);
			if (idx < LUA_GLOBALSINDEX)  /* function upvalue? */
			  LuaCBarrier(L, CurrFunc(L), L.top - 1);
		  }
		  StkId.Dec(ref L.top);
		  LuaUnlock(L);
		}


		public static void LuaPushValue (LuaState L, int idx) {
		  LuaLock(L);
		  SetObj2S(L, L.top, Index2Address(L, idx));
		  IncrementTop(L);
		  LuaUnlock(L);
		}



		/*
		** access functions (stack . C)
		*/


		public static int LuaType (LuaState L, int idx) {
		  StkId o = Index2Address(L, idx);
		  return (o == LuaONilObject) ? LUA_TNONE : TType(o);
		}


		public static CharPtr LuaTypeName (LuaState L, int t) {
		  //UNUSED(L);
		  return (t == LUA_TNONE) ? "no value" : luaT_typenames[t];
		}


		public static bool LuaIsCFunction (LuaState L, int idx) {
		  StkId o = Index2Address(L, idx);
		  return IsCFunction(o);
		}


		public static int LuaIsNumber (LuaState L, int idx) {
		  TValue n = new LuaTypeValue();
		  TValue o = Index2Address(L, idx);
		  return tonumber(ref o, n);
		}


		public static int LuaIsString (LuaState L, int idx) {
		  int t = LuaType(L, idx);
		  return (t == LUA_TSTRING || t == LUA_TNUMBER) ? 1 : 0;
		}


		public static int LuaIsUserData (LuaState L, int idx) {
		  TValue o = Index2Address(L, idx);
		  return (TTIsUserData(o) || TTIsLightUserData(o)) ? 1 : 0;
		}


		public static int LuaRawEqual (LuaState L, int index1, int index2) {
		  StkId o1 = Index2Address(L, index1);
		  StkId o2 = Index2Address(L, index2);
		  return (o1 == LuaONilObject || o2 == LuaONilObject) ? 0
				 : LuaORawEqualObj(o1, o2);
		}


		public static int LuaEqual (LuaState L, int index1, int index2) {
		  StkId o1, o2;
		  int i;
		  LuaLock(L);  /* may call tag method */
		  o1 = Index2Address(L, index1);
		  o2 = Index2Address(L, index2);
		  i = (o1 == LuaONilObject || o2 == LuaONilObject) ? 0 : equalobj(L, o1, o2);
		  LuaUnlock(L);
		  return i;
		}


		public static int LuaLessThan (LuaState L, int index1, int index2) {
		  StkId o1, o2;
		  int i;
		  LuaLock(L);  /* may call tag method */
		  o1 = Index2Address(L, index1);
		  o2 = Index2Address(L, index2);
		  i = (o1 == LuaONilObject || o2 == LuaONilObject) ? 0
			   : luaV_lessthan(L, o1, o2);
		  LuaUnlock(L);
		  return i;
		}



		public static lua_Number LuaToNumber (LuaState L, int idx) {
		  TValue n = new LuaTypeValue();
		  TValue o = Index2Address(L, idx);
		  if (tonumber(ref o, n) != 0)
			return NValue(o);
		  else
			return 0;
		}


		public static lua_Integer LuaToInteger (LuaState L, int idx) {
		  TValue n = new LuaTypeValue();
		  TValue o = Index2Address(L, idx);
		  if (tonumber(ref o, n) != 0) {
			lua_Integer res;
			lua_Number num = NValue(o);
			lua_number2integer(out res, num);
			return res;
		  }
		  else
			return 0;
		}


		public static int LuaToBoolean (LuaState L, int idx) {
		  TValue o = Index2Address(L, idx);
		  return (LIsFalse(o) == 0) ? 1 : 0;
		}

		[CLSCompliantAttribute(false)]
		public static CharPtr LuaToLString (LuaState L, int idx, out uint len) {
		  StkId o = Index2Address(L, idx);
		  if (!TTIsString(o)) {
			LuaLock(L);  /* `luaV_tostring' may create a new string */
			if (luaV_tostring(L, o)==0) {  /* conversion failed? */
			  len = 0;
			  LuaUnlock(L);
			  return null;
			}
			LuaCCheckGC(L);
			o = Index2Address(L, idx);  /* previous call may reallocate the stack */
			LuaUnlock(L);
		  }
		  len = TSValue(o).len;
		  return SValue(o);
		}

		[CLSCompliantAttribute(false)]
		public static uint LuaObjectLen (LuaState L, int idx) {
		  StkId o = Index2Address(L, idx);
		  switch (TType(o)) {
			case LUA_TSTRING: return TSValue(o).len;
			case LUA_TUSERDATA: return UValue(o).len;
			case LUA_TTABLE: return (uint)luaH_getn(HValue(o));
			case LUA_TNUMBER: {
			  uint l;
			  LuaLock(L);  /* `luaV_tostring' may create a new string */
			  l = (luaV_tostring(L, o) != 0 ? TSValue(o).len : 0);
			  LuaUnlock(L);
			  return l;
			}
			default: return 0;
		  }
		}


		public static LuaNativeFunction LuaToCFunction (LuaState L, int idx) {
		  StkId o = Index2Address(L, idx);
		  return (!IsCFunction(o)) ? null : CLValue(o).c.f;
		}


		public static object LuaToUserData (LuaState L, int idx) {
		  StkId o = Index2Address(L, idx);
		  switch (TType(o)) {
			case LUA_TUSERDATA: return (RawUValue(o).user_data);
			case LUA_TLIGHTUSERDATA: return PValue(o);
			default: return null;
		  }
		}

		public static LuaState LuaToThread (LuaState L, int idx) {
		  StkId o = Index2Address(L, idx);
		  return (!TTIsThread(o)) ? null : THValue(o);
		}


		public static object LuaToPointer (LuaState L, int idx) {
		  StkId o = Index2Address(L, idx);
		  switch (TType(o)) {
			case LUA_TTABLE: return HValue(o);
			case LUA_TFUNCTION: return CLValue(o);
			case LUA_TTHREAD: return THValue(o);
			case LUA_TUSERDATA:
			case LUA_TLIGHTUSERDATA:
			  return LuaToUserData(L, idx);
			default: return null;
		  }
		}



		/*
		** push functions (C . stack)
		*/


		public static void LuaPushNil (LuaState L) {
		  LuaLock(L);
		  SetNilValue(L.top);
		  IncrementTop(L);
		  LuaUnlock(L);
		}


		public static void LuaPushNumber (LuaState L, lua_Number n) {
		  LuaLock(L);
		  SetNValue(L.top, n);
		  IncrementTop(L);
		  LuaUnlock(L);
		}


		public static void LuaPushInteger (LuaState L, lua_Integer n) {
		  LuaLock(L);
		  SetNValue(L.top, CastNum(n));
		  IncrementTop(L);
		  LuaUnlock(L);
		}
		

		private static void LuaPushLString (LuaState L, CharPtr s, uint len) {
		  LuaLock(L);
		  LuaCCheckGC(L);
		  SetSValue2S(L, L.top, luaS_newlstr(L, s, len));
		  IncrementTop(L);
		  LuaUnlock(L);
		}


		public static void LuaPushString (LuaState L, CharPtr s) {
		  if (s == null)
			LuaPushNil(L);
		  else
			LuaPushLString(L, s, (uint)strlen(s));
		}


		public static CharPtr LuaPushVFString (LuaState L, CharPtr fmt,
											  object[] argp) {
		  CharPtr ret;
		  LuaLock(L);
		  LuaCCheckGC(L);
		  ret = LuaOPushVFString(L, fmt, argp);
		  LuaUnlock(L);
		  return ret;
		}


		public static CharPtr LuaPushFString (LuaState L, CharPtr fmt) {
			CharPtr ret;
			LuaLock(L);
			LuaCCheckGC(L);
			ret = LuaOPushVFString(L, fmt, null);
			LuaUnlock(L);
			return ret;
		}

		public static CharPtr LuaPushFString(LuaState L, CharPtr fmt, params object[] p)
		{
			  CharPtr ret;
			  LuaLock(L);
			  LuaCCheckGC(L);
			  ret = LuaOPushVFString(L, fmt, p);
			  LuaUnlock(L);
			  return ret;
		}

		public static void LuaPushCClosure (LuaState L, LuaNativeFunction fn, int n) {
		  Closure cl;
		  LuaLock(L);
		  LuaCCheckGC(L);
		  CheckNElements(L, n);
		  cl = LuaFNewCclosure(L, n, GetCurrentEnv(L));
		  cl.c.f = fn;
		  L.top -= n;
		  while (n-- != 0)
			SetObj2N(L, cl.c.upvalue[n], L.top+n);
		  SetCLValue(L, L.top, cl);
		  LuaAssert(IsWhite(obj2gco(cl)));
		  IncrementTop(L);
		  LuaUnlock(L);
		}


		public static void LuaPushBoolean (LuaState L, int b) {
		  LuaLock(L);
		  SetBValue(L.top, (b != 0) ? 1 : 0);  /* ensure that true is 1 */
		  IncrementTop(L);
		  LuaUnlock(L);
		}


		public static void LuaPushLightUserData (LuaState L, object p) {
		  LuaLock(L);
		  SetPValue(L.top, p);
		  IncrementTop(L);
		  LuaUnlock(L);
		}


		public static int LuaPushThread (LuaState L) {
		  LuaLock(L);
		  SetTTHValue(L, L.top, L);
		  IncrementTop(L);
		  LuaUnlock(L);
		  return (G(L).mainthread == L) ? 1 : 0;
		}



		/*
		** get functions (Lua . stack)
		*/


		public static void LuaGetTable (LuaState L, int idx) {
		  StkId t;
		  LuaLock(L);
		  t = Index2Address(L, idx);
		  CheckValidIndex(L, t);
		  luaV_gettable(L, t, L.top - 1, L.top - 1);
		  LuaUnlock(L);
		}

		public static void LuaGetField (LuaState L, int idx, CharPtr k) {
		  StkId t;
		  TValue key = new LuaTypeValue();
		  LuaLock(L);
		  t = Index2Address(L, idx);
		  CheckValidIndex(L, t);
		  SetSValue(L, key, luaS_new(L, k));
		  luaV_gettable(L, t, key, L.top);
		  IncrementTop(L);
		  LuaUnlock(L);
		}


		public static void LuaRawGet (LuaState L, int idx) {
		  StkId t;
		  LuaLock(L);
		  t = Index2Address(L, idx);
		  ApiCheck(L, TTIsTable(t));
		  SetObj2S(L, L.top - 1, luaH_get(HValue(t), L.top - 1));
		  LuaUnlock(L);
		}


		public static void LuaRawGetI (LuaState L, int idx, int n) {
		  StkId o;
		  LuaLock(L);
		  o = Index2Address(L, idx);
		  ApiCheck(L, TTIsTable(o));
		  SetObj2S(L, L.top, luaH_getnum(HValue(o), n));
		  IncrementTop(L);
		  LuaUnlock(L);
		}


		public static void LuaCreateTable (LuaState L, int narray, int nrec) {
		  LuaLock(L);
		  LuaCCheckGC(L);
		  SetHValue(L, L.top, luaH_new(L, narray, nrec));
		  IncrementTop(L);
		  LuaUnlock(L);
		}


		public static int LuaGetMetatable (LuaState L, int objindex) {
		  TValue obj;
		  Table mt = null;
		  int res;
		  LuaLock(L);
		  obj = Index2Address(L, objindex);
		  switch (TType(obj)) {
			case LUA_TTABLE:
			  mt = HValue(obj).metatable;
			  break;
			case LUA_TUSERDATA:
			  mt = UValue(obj).metatable;
			  break;
			default:
			  mt = G(L).mt[TType(obj)];
			  break;
		  }
		  if (mt == null)
			res = 0;
		  else {
			SetHValue(L, L.top, mt);
			IncrementTop(L);
			res = 1;
		  }
		  LuaUnlock(L);
		  return res;
		}


		public static void LuaGetFEnv (LuaState L, int idx) {
		  StkId o;
		  LuaLock(L);
		  o = Index2Address(L, idx);
		  CheckValidIndex(L, o);
		  switch (TType(o)) {
			case LUA_TFUNCTION:
			  SetHValue(L, L.top, CLValue(o).c.env);
			  break;
			case LUA_TUSERDATA:
			  SetHValue(L, L.top, UValue(o).env);
			  break;
			case LUA_TTHREAD:
			  SetObj2S(L, L.top,  Gt(THValue(o)));
			  break;
			default:
			  SetNilValue(L.top);
			  break;
		  }
		  IncrementTop(L);
		  LuaUnlock(L);
		}


		/*
		** set functions (stack . Lua)
		*/


		public static void LuaSetTable (LuaState L, int idx) {
		  StkId t;
		  LuaLock(L);
		  CheckNElements(L, 2);
		  t = Index2Address(L, idx);
		  CheckValidIndex(L, t);
		  luaV_settable(L, t, L.top - 2, L.top - 1);
		  L.top -= 2;  /* pop index and value */
		  LuaUnlock(L);
		}


		public static void LuaSetField (LuaState L, int idx, CharPtr k) {
		  StkId t;
		  TValue key = new LuaTypeValue();			
		  LuaLock(L);
		  CheckNElements(L, 1);
		  t = Index2Address(L, idx);
		  CheckValidIndex(L, t);
		  SetSValue(L, key, luaS_new(L, k));
		  luaV_settable(L, t, key, L.top - 1);
		  StkId.Dec(ref L.top);  /* pop value */
		  LuaUnlock(L);
		}


		public static void LuaRawSet (LuaState L, int idx) {
		  StkId t;
		  LuaLock(L);
		  CheckNElements(L, 2);
		  t = Index2Address(L, idx);
		  ApiCheck(L, TTIsTable(t));
		  SetObj2T(L, luaH_set(L, HValue(t), L.top-2), L.top-1);
		  LuaCBarrierT(L, HValue(t), L.top-1);
		  L.top -= 2;
		  LuaUnlock(L);
		}


		public static void LuaRawSetI (LuaState L, int idx, int n) {
		  StkId o;
		  LuaLock(L);
		  CheckNElements(L, 1);
		  o = Index2Address(L, idx);
		  ApiCheck(L, TTIsTable(o));
		  SetObj2T(L, luaH_setnum(L, HValue(o), n), L.top-1);
		  LuaCBarrierT(L, HValue(o), L.top-1);
		  StkId.Dec(ref L.top);
		  LuaUnlock(L);
		}


		public static int LuaSetMetatable (LuaState L, int objindex) {
		  TValue obj;
		  Table mt;
		  LuaLock(L);
		  CheckNElements(L, 1);
		  obj = Index2Address(L, objindex);
		  CheckValidIndex(L, obj);
		  if (TTIsNil(L.top - 1))
			  mt = null;
		  else {
			ApiCheck(L, TTIsTable(L.top - 1));
			mt = HValue(L.top - 1);
		  }
		  switch (TType(obj)) {
			case LUA_TTABLE: {
			  HValue(obj).metatable = mt;
			  if (mt != null)
				LuaCObjBarrierT(L, HValue(obj), mt);
			  break;
			}
			case LUA_TUSERDATA: {
			  UValue(obj).metatable = mt;
			  if (mt != null)
				LuaCObjBarrier(L, RawUValue(obj), mt);
			  break;
			}
			default: {
			  G(L).mt[TType(obj)] = mt;
			  break;
			}
		  }
		  StkId.Dec(ref L.top);
		  LuaUnlock(L);
		  return 1;
		}


		public static int LuaSetFEnv (LuaState L, int idx) {
		  StkId o;
		  int res = 1;
		  LuaLock(L);
		  CheckNElements(L, 1);
		  o = Index2Address(L, idx);
		  CheckValidIndex(L, o);
		  ApiCheck(L, TTIsTable(L.top - 1));
		  switch (TType(o)) {
			case LUA_TFUNCTION:
			  CLValue(o).c.env = HValue(L.top - 1);
			  break;
			case LUA_TUSERDATA:
			  UValue(o).env = HValue(L.top - 1);
			  break;
			case LUA_TTHREAD:
			  SetHValue(L, Gt(THValue(o)), HValue(L.top - 1));
			  break;
			default:
			  res = 0;
			  break;
		  }
		  if (res != 0) LuaCObjBarrier(L, GCValue(o), HValue(L.top - 1));
		  StkId.Dec(ref L.top);
		  LuaUnlock(L);
		  return res;
		}


		/*
		** `load' and `call' functions (run Lua code)
		*/


		public static void AdjustResults(LuaState L, int nres) {
			if (nres == LUA_MULTRET && L.top >= L.ci.top)
				L.ci.top = L.top;
		}


		public static void CheckResults(LuaState L, int na, int nr) {
			ApiCheck(L, (nr) == LUA_MULTRET || (L.ci.top - L.top >= (nr) - (na)));
		}
			

		public static void LuaCall (LuaState L, int nargs, int nresults) {
		  StkId func;
		  LuaLock(L);
		  CheckNElements(L, nargs+1);
		  CheckResults(L, nargs, nresults);
		  func = L.top - (nargs+1);
		  LuaDCall(L, func, nresults);
		  AdjustResults(L, nresults);
		  LuaUnlock(L);
		}



		/*
		** Execute a protected call.
		*/
		public class CallS {  /* data to `f_call' */
		  public StkId func;
			public int nresults;
		};


		static void FunctionCall (LuaState L, object ud) {
		  CallS c = ud as CallS;
		  LuaDCall(L, c.func, c.nresults);
		}



		public static int LuaPCall (LuaState L, int nargs, int nresults, int errfunc) {
		  CallS c = new CallS();
		  int status;
		  ptrdiff_t func;
		  LuaLock(L);
		  CheckNElements(L, nargs+1);
		  CheckResults(L, nargs, nresults);
		  if (errfunc == 0)
			func = 0;
		  else {
			StkId o = Index2Address(L, errfunc);
			CheckValidIndex(L, o);
			func = SaveStack(L, o);
		  }
		  c.func = L.top - (nargs+1);  /* function to be called */
		  c.nresults = nresults;
		  status = LuaDPCall(L, FunctionCall, c, SaveStack(L, c.func), func);
		  AdjustResults(L, nresults);
		  LuaUnlock(L);
		  return status;
		}


		/*
		** Execute a protected C call.
		*/
		public class CCallS {  /* data to `f_Ccall' */
		  public LuaNativeFunction func;
		  public object ud;
		};


		static void FunctionCCall (LuaState L, object ud) {
		  CCallS c = ud as CCallS;
		  Closure cl;
		  cl = LuaFNewCclosure(L, 0, GetCurrentEnv(L));
		  cl.c.f = c.func;
		  SetCLValue(L, L.top, cl);  /* push function */
		  IncrementTop(L);
		  SetPValue(L.top, c.ud);  /* push only argument */
		  IncrementTop(L);
		  LuaDCall(L, L.top - 2, 0);
		}


		public static int LuaCPCall (LuaState L, LuaNativeFunction func, object ud) {
		  CCallS c = new CCallS();
		  int status;
		  LuaLock(L);
		  c.func = func;
		  c.ud = ud;
		  status = LuaDPCall(L, FunctionCCall, c, SaveStack(L, L.top), 0);
		  LuaUnlock(L);
		  return status;
		}

		[CLSCompliantAttribute(false)]
		public static int LuaLoad (LuaState L, lua_Reader reader, object data,
							  CharPtr chunkname) {
		  ZIO z = new ZIO();
		  int status;
		  LuaLock(L);
		  if (chunkname == null) chunkname = "?";
		  luaZ_init(L, z, reader, data);
		  status = LuaDProtectedParser(L, z, chunkname);
		  LuaUnlock(L);
		  return status;
		}

		[CLSCompliantAttribute(false)]
		public static int LuaDump (LuaState L, lua_Writer writer, object data) {
		  int status;
		  TValue o;
		  LuaLock(L);
		  CheckNElements(L, 1);
		  o = L.top - 1;
		  if (IsLfunction(o))
			status = LuaUDump(L, CLValue(o).l.p, writer, data, 0);
		  else
			status = 1;
		  LuaUnlock(L);
		  return status;
		}


		public static int  LuaStatus (LuaState L) {
		  return L.status;
		}


		/*
		** Garbage-collection function
		*/

		public static int LuaGC (LuaState L, int what, int data) {
		  int res = 0;
		  GlobalState g;
		  LuaLock(L);
		  g = G(L);
		  switch (what) {
			case LUA_GCSTOP: {
			  g.GCthreshold = MAXLUMEM;
			  break;
			}
			case LUA_GCRESTART: {
			  g.GCthreshold = g.totalbytes;
			  break;
			}
			case LUA_GCCOLLECT: {
			  LuaCFullGC(L);
			  break;
			}
			case LUA_GCCOUNT: {
			  /* GC values are expressed in Kbytes: #bytes/2^10 */
			  res = CastInt(g.totalbytes >> 10);
			  break;
			}
			case LUA_GCCOUNTB: {
			  res = CastInt(g.totalbytes & 0x3ff);
			  break;
			}
			case LUA_GCSTEP: {
			  lu_mem a = ((lu_mem)data << 10);
			  if (a <= g.totalbytes)
				g.GCthreshold = (uint)(g.totalbytes - a);
			  else
				g.GCthreshold = 0;
			  while (g.GCthreshold <= g.totalbytes) {
				LuaCStep(L);
				if (g.gcstate == GCSpause) {  /* end of cycle? */
				  res = 1;  /* signal it */
				  break;
				}
			  }
			  break;
			}
			case LUA_GCSETPAUSE: {
			  res = g.gcpause;
			  g.gcpause = data;
			  break;
			}
			case LUA_GCSETSTEPMUL: {
			  res = g.gcstepmul;
			  g.gcstepmul = data;
			  break;
			}
			default:
				res = -1;  /* invalid option */
				break;
		  }
		  LuaUnlock(L);
		  return res;
		}



		/*
		** miscellaneous functions
		*/


		public static int LuaError (LuaState L) {
		  LuaLock(L);
		  CheckNElements(L, 1);
		  LuaGErrorMsg(L);
		  LuaUnlock(L);
		  return 0;  /* to avoid warnings */
		}


		public static int LuaNext (LuaState L, int idx) {
		  StkId t;
		  int more;
		  LuaLock(L);
		  t = Index2Address(L, idx);
		  ApiCheck(L, TTIsTable(t));
		  more = luaH_next(L, HValue(t), L.top - 1);
		  if (more != 0) {
			IncrementTop(L);
		  }
		  else  /* no more elements */
			StkId.Dec(ref L.top);  /* remove key */
		  LuaUnlock(L);
		  return more;
		}


		public static void LuaConcat (LuaState L, int n) {
		  LuaLock(L);
		  CheckNElements(L, n);
		  if (n >= 2) {
			LuaCCheckGC(L);
			luaV_concat(L, n, CastInt(L.top - L.base_) - 1);
			L.top -= (n-1);
		  }
		  else if (n == 0) {  /* push empty string */
			SetSValue2S(L, L.top, luaS_newlstr(L, "", 0));
			IncrementTop(L);
		  }
		  /* else n == 1; nothing to do */
		  LuaUnlock(L);
		}


		public static lua_Alloc LuaGetAllocF (LuaState L, ref object ud) {
		  lua_Alloc f;
		  LuaLock(L);
		  if (ud != null) ud = G(L).ud;
		  f = G(L).frealloc;
		  LuaUnlock(L);
		  return f;
		}


		public static void LuaSetAllocF (LuaState L, lua_Alloc f, object ud) {
		  LuaLock(L);
		  G(L).ud = ud;
		  G(L).frealloc = f;
		  LuaUnlock(L);
		}

		[CLSCompliantAttribute(false)]
		public static object LuaNewUserData(LuaState L, uint size)
		{
			Udata u;
			LuaLock(L);
			LuaCCheckGC(L);
			u = luaS_newudata(L, size, GetCurrentEnv(L));
			SetUValue(L, L.top, u);
			IncrementTop(L);
			LuaUnlock(L);
			return u.user_data;
		}

		// this one is used internally only
		internal static object LuaNewUserData(LuaState L, Type t)
		{
			Udata u;
			LuaLock(L);
			LuaCCheckGC(L);
			u = luaS_newudata(L, t, GetCurrentEnv(L));
			SetUValue(L, L.top, u);
			IncrementTop(L);
			LuaUnlock(L);
			return u.user_data;
		}

		static CharPtr AuxUpValue (StkId fi, int n, ref TValue val) {
		  Closure f;
		  if (!TTIsFunction(fi)) return null;
		  f = CLValue(fi);
		  if (f.c.isC != 0) {
			if (!(1 <= n && n <= f.c.nupvalues)) return null;
			val = f.c.upvalue[n-1];
			return "";
		  }
		  else {
			Proto p = f.l.p;
			if (!(1 <= n && n <= p.sizeupvalues)) return null;
			val = f.l.upvals[n-1].v;
			return GetStr(p.upvalues[n-1]);
		  }
		}


		public static CharPtr LuaGetUpValue (LuaState L, int funcindex, int n) {
		  CharPtr name;
		  TValue val = new LuaTypeValue();
		  LuaLock(L);
		  name = AuxUpValue(Index2Address(L, funcindex), n, ref val);
		  if (name != null) {
			SetObj2S(L, L.top, val);
			IncrementTop(L);
		  }
		  LuaUnlock(L);
		  return name;
		}


		public static CharPtr LuaSetUpValue (LuaState L, int funcindex, int n) {
		  CharPtr name;
		  TValue val = new LuaTypeValue();
		  StkId fi;
		  LuaLock(L);
		  fi = Index2Address(L, funcindex);
		  CheckNElements(L, 1);
		  name = AuxUpValue(fi, n, ref val);
		  if (name != null) {
			StkId.Dec(ref L.top);
			SetObj(L, val, L.top);
			LuaCBarrier(L, CLValue(fi), L.top);
		  }
		  LuaUnlock(L);
		  return name;
		}

	}
}

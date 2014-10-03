/*
** $Id: lfunc.c,v 2.12.1.2 2007/12/28 14:58:43 roberto Exp $
** Auxiliary functions to manipulate prototypes and closures
** See Copyright Notice in lua.h
*/

using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace KopiLua
{
	using TValue = Lua.LuaTypeValue;
	using StkId = Lua.LuaTypeValue;
	using Instruction = System.UInt32;

	public partial class Lua
	{

		public static int SizeCclosure(int n) {
			return GetUnmanagedSize(typeof(CClosure)) + GetUnmanagedSize(typeof(TValue)) * (n - 1);
		}

		public static int SizeLclosure(int n) {
			return GetUnmanagedSize(typeof(LClosure)) + GetUnmanagedSize(typeof(TValue)) * (n - 1);
		}

		public static Closure LuaFNewCclosure (LuaState L, int nelems, Table e) {
		  //Closure c = (Closure)luaM_malloc(L, sizeCclosure(nelems));	
		  Closure c = LuaMNew<Closure>(L);
		  AddTotalBytes(L, SizeCclosure(nelems));
		  LuaCLink(L, obj2gco(c), LUA_TFUNCTION);
		  c.c.isC = 1;
		  c.c.env = e;
		  c.c.nupvalues = CastByte(nelems);
		  c.c.upvalue = new TValue[nelems];
		  for (int i = 0; i < nelems; i++)
			  c.c.upvalue[i] = new LuaTypeValue();
		  return c;
		}


		public static Closure LuaFNewLClosure (LuaState L, int nelems, Table e) {
		  //Closure c = (Closure)luaM_malloc(L, sizeLclosure(nelems));
		  Closure c = LuaMNew<Closure>(L);
		  AddTotalBytes(L, SizeLclosure(nelems));
		  LuaCLink(L, obj2gco(c), LUA_TFUNCTION);
		  c.l.isC = 0;
		  c.l.env = e;
		  c.l.nupvalues = CastByte(nelems);
		  c.l.upvals = new UpVal[nelems];
		  for (int i = 0; i < nelems; i++)
			  c.l.upvals[i] = new UpVal();
		  while (nelems-- > 0) c.l.upvals[nelems] = null;
		  return c;
		}


		public static UpVal LuaFNewUpVal (LuaState L) {
		  UpVal uv = LuaMNew<UpVal>(L);
		  LuaCLink(L, obj2gco(uv), LUATUPVAL);
		  uv.v = uv.u.value;
		  SetNilValue(uv.v);
		  return uv;
		}

		public static UpVal LuaFindUpVal (LuaState L, StkId level) {
		  GlobalState g = G(L);
		  GCObjectRef pp = new OpenValRef(L);
		  UpVal p;
		  UpVal uv;
		  while (pp.get() != null && (p = ngcotouv(pp.get())).v >= level) {
			LuaAssert(p.v != p.u.value);
			if (p.v == level) {  /* found a corresponding upvalue? */
			  if (IsDead(g, obj2gco(p)))  /* is it dead? */
				ChangeWhite(obj2gco(p));  /* ressurect it */
			  return p;
			}
			pp = new NextRef(p);
		  }
		  uv = LuaMNew<UpVal>(L);  /* not found: create a new one */
		  uv.tt = LUATUPVAL;
		  uv.marked = LuaCWhite(g);
		  uv.v = level;  /* current value lives in the stack */
		  uv.next = pp.get();  /* chain it in the proper position */
		  pp.set( obj2gco(uv) );
		  uv.u.l.prev = g.uvhead;  /* double link it in `uvhead' list */
		  uv.u.l.next = g.uvhead.u.l.next;
		  uv.u.l.next.u.l.prev = uv;
		  g.uvhead.u.l.next = uv;
		  LuaAssert(uv.u.l.next.u.l.prev == uv && uv.u.l.prev.u.l.next == uv);
		  return uv;
		}


		private static void UnlinkUpVal (UpVal uv) {
		  LuaAssert(uv.u.l.next.u.l.prev == uv && uv.u.l.prev.u.l.next == uv);
		  uv.u.l.next.u.l.prev = uv.u.l.prev;  /* remove from `uvhead' list */
		  uv.u.l.prev.u.l.next = uv.u.l.next;
		}


		public static void LuaFreeUpVal (LuaState L, UpVal uv) {
		  if (uv.v != uv.u.value)  /* is it open? */
			UnlinkUpVal(uv);  /* remove from open list */
		  LuaMFree(L, uv);  /* free upvalue */
		}


		public static void LuaFClose (LuaState L, StkId level) {
		  UpVal uv;
		  GlobalState g = G(L);
		  while (L.openupval != null && (uv = ngcotouv(L.openupval)).v >= level) {
			GCObject o = obj2gco(uv);
			LuaAssert(!IsBlack(o) && uv.v != uv.u.value);
			L.openupval = uv.next;  /* remove from `open' list */
			if (IsDead(g, o))
			  LuaFreeUpVal(L, uv);  /* free upvalue */
			else {
			  UnlinkUpVal(uv);
			  SetObj(L, uv.u.value, uv.v);
			  uv.v = uv.u.value;  /* now current value lives here */
			  LuaCLinkUpVal(L, uv);  /* link upvalue into `gcroot' list */
			}
		  }
		}


		public static Proto LuaFNewProto (LuaState L) {
		  Proto f = LuaMNew<Proto>(L);
		  LuaCLink(L, obj2gco(f), LUATPROTO);
		  f.k = null;
		  f.sizek = 0;
		  f.p = null;
		  f.sizep = 0;
		  f.code = null;
		  f.sizecode = 0;
		  f.sizelineinfo = 0;
		  f.sizeupvalues = 0;
		  f.nups = 0;
		  f.upvalues = null;
		  f.numparams = 0;
		  f.is_vararg = 0;
		  f.maxstacksize = 0;
		  f.lineinfo = null;
		  f.sizelocvars = 0;
		  f.locvars = null;
		  f.linedefined = 0;
		  f.lastlinedefined = 0;
		  f.source = null;
		  return f;
		}

		public static void LuaFFreeProto (LuaState L, Proto f) {
		  LuaMFreeArray<Instruction>(L, f.code);
		  LuaMFreeArray<Proto>(L, f.p);
		  LuaMFreeArray<TValue>(L, f.k);
		  LuaMFreeArray<Int32>(L, f.lineinfo);
		  LuaMFreeArray<LocVar>(L, f.locvars);
		  LuaMFreeArray<TString>(L, f.upvalues);
		  LuaMFree(L, f);
		}

		// we have a gc, so nothing to do
		public static void LuaFFreeClosure (LuaState L, Closure c) {
		  int size = (c.c.isC != 0) ? SizeCclosure(c.c.nupvalues) :
								  SizeLclosure(c.l.nupvalues);
		  //luaM_freemem(L, c, size);
		  SubtractTotalBytes(L, size);
		}


		/*
		** Look for n-th local variable at line `line' in function `func'.
		** Returns null if not found.
		*/
		public static CharPtr LuaFGetLocalName (Proto f, int local_number, int pc) {
		  int i;
		  for (i = 0; i<f.sizelocvars && f.locvars[i].startpc <= pc; i++) {
			if (pc < f.locvars[i].endpc) {  /* is variable active? */
			  local_number--;
			  if (local_number == 0)
				return GetStr(f.locvars[i].varname);
			}
		  }
		  return null;  /* not found */
		}

	}
}

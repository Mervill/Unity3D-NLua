/*
** $Id: lvm.c,v 2.63.1.3 2007/12/28 15:32:23 roberto Exp $
** Lua virtual machine
** See Copyright Notice in lua.h
*/

using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace KopiLua
{
	using TValue = Lua.LuaTypeValue;
	using StkId = Lua.LuaTypeValue;
	using lua_Number = System.Double;
	using lu_byte = System.Byte;
	using ptrdiff_t = System.Int32;
	using Instruction = System.UInt32;

	public partial class Lua
	{
		[CLSCompliantAttribute(false)]
		public static int tostring(LuaState L, StkId o) {
			return ((TType(o) == LUA_TSTRING) || (luaV_tostring(L, o) != 0)) ? 1 : 0;
		}

		public static int tonumber(ref StkId o, TValue n) {
			return ((TType(o) == LUA_TNUMBER || (((o) = luaV_tonumber(o, n)) != null))) ? 1 : 0;
		}

		public static int equalobj(LuaState L, TValue o1, TValue o2) {
			return ((TType(o1) == TType(o2)) && (luaV_equalval(L, o1, o2) != 0)) ? 1 : 0;
		}


		/* limit for table tag-method chains (to avoid loops) */
		public const int MAXTAGLOOP	= 100;


		public static TValue luaV_tonumber (TValue obj, TValue n) {
		  lua_Number num;
		  if (TTIsNumber(obj)) return obj;
		  if (TTIsString(obj) && (LuaOStr2d(SValue(obj), out num)!=0)) {
			SetNValue(n, num);
			return n;
		  }
		  else
			return null;
		}


		public static int luaV_tostring (LuaState L, StkId obj) {
		  if (!TTIsNumber(obj))
			return 0;
		  else {
			lua_Number n = NValue(obj);
			CharPtr s = lua_number2str(n);
			SetSValue2S(L, obj, luaS_new(L, s));
			return 1;
		  }
		}


		private static void traceexec (LuaState L, InstructionPtr pc) {
		  lu_byte mask = L.hookmask;
		  InstructionPtr oldpc = InstructionPtr.Assign(L.savedpc);
		  L.savedpc = InstructionPtr.Assign(pc);
		  if (((mask & LUA_MASKCOUNT) != 0) && (L.hookcount == 0)) {
			ResetHookCount(L);
			LuaDCallHook(L, LUA_HOOKCOUNT, -1);
		  }
		  if ((mask & LUA_MASKLINE) != 0) {
			Proto p = CIFunc(L.ci).l.p;
			int npc = PCRel(pc, p);
			int newline = GetLine(p, npc);
			/* call linehook when enter a new function, when jump back (loop),
			   or when enter a new line */
			if (npc == 0 || pc <= oldpc || newline != GetLine(p, PCRel(oldpc, p)))
			  LuaDCallHook(L, LUA_HOOKLINE, newline);
		  }
		}


		private static void callTMres (LuaState L, StkId res, TValue f,
								TValue p1, TValue p2) {
		  ptrdiff_t result = SaveStack(L, res);
		  SetObj2S(L, L.top, f);  /* push function */
		  SetObj2S(L, L.top+1, p1);  /* 1st argument */
		  SetObj2S(L, L.top+2, p2);  /* 2nd argument */
		  LuaDCheckStack(L, 3);
		  L.top += 3;
		  LuaDCall(L, L.top-3, 1);
		  res = RestoreStack(L, result);
		  StkId.Dec(ref L.top);
		  SetObj2S(L, res, L.top);
		}



		private static void callTM (LuaState L, TValue f, TValue p1,
							TValue p2, TValue p3) {
		  SetObj2S(L, L.top, f);  /* push function */
		  SetObj2S(L, L.top + 1, p1);  /* 1st argument */
		  SetObj2S(L, L.top + 2, p2);  /* 2nd argument */
		  SetObj2S(L, L.top + 3, p3);  /* 3th argument */
		  LuaDCheckStack(L, 4);
		  L.top += 4;
		  LuaDCall(L, L.top - 4, 0);
		}


		public static void luaV_gettable (LuaState L, TValue t, TValue key, StkId val) {
		  int loop;
		  for (loop = 0; loop < MAXTAGLOOP; loop++) {
			TValue tm;
			if (TTIsTable(t)) {  /* `t' is a table? */
			  Table h = HValue(t);
			  TValue res = luaH_get(h, key); /* do a primitive get */
			  if (!TTIsNil(res) ||  /* result is no nil? */
				  (tm = fasttm(L, h.metatable, TMS.TM_INDEX)) == null) { /* or no TM? */
				SetObj2S(L, val, res);
				return;
			  }
			  /* else will try the tag method */
			}
			else if (TTIsNil(tm = luaT_gettmbyobj(L, t, TMS.TM_INDEX)))
			  LuaGTypeError(L, t, "index");
			if (TTIsFunction(tm)) {
			  callTMres(L, val, tm, t, key);
			  return;
			}
			t = tm;  /* else repeat with `tm' */ 
		  }
		  LuaGRunError(L, "loop in gettable");
		}

		public static void luaV_settable (LuaState L, TValue t, TValue key, StkId val) {
		  int loop;
		  TValue temp = new LuaTypeValue();
		  for (loop = 0; loop < MAXTAGLOOP; loop++) {
			TValue tm;
			if (TTIsTable(t)) {  /* `t' is a table? */
			  Table h = HValue(t);
			  TValue oldval = luaH_set(L, h, key); /* do a primitive set */
			  if (!TTIsNil(oldval) ||  /* result is no nil? */
				  (tm = fasttm(L, h.metatable, TMS.TM_NEWINDEX)) == null) { /* or no TM? */
				SetObj2T(L, oldval, val);
			    h.flags = 0;
				LuaCBarrierT(L, h, val);
				return;
			  }
			  /* else will try the tag method */
			}
			else if (TTIsNil(tm = luaT_gettmbyobj(L, t, TMS.TM_NEWINDEX)))
			  LuaGTypeError(L, t, "index");
			if (TTIsFunction(tm)) {
			  callTM(L, tm, t, key, val);
			  return;
			}
			/* else repeat with `tm' */
			SetObj (L, temp, tm); /* avoid pointing inside table (may rehash) */
			t = temp;
		  }
		  LuaGRunError(L, "loop in settable");
		}


		private static int call_binTM (LuaState L, TValue p1, TValue p2,
							   StkId res, TMS event_) {
		  TValue tm = luaT_gettmbyobj(L, p1, event_);  /* try first operand */
		  if (TTIsNil(tm))
			tm = luaT_gettmbyobj(L, p2, event_);  /* try second operand */
		  if (TTIsNil(tm)) return 0;
		  callTMres(L, res, tm, p1, p2);
		  return 1;
		}


		private static TValue get_compTM (LuaState L, Table mt1, Table mt2,
										  TMS event_) {
		  TValue tm1 = fasttm(L, mt1, event_);
		  TValue tm2;
		  if (tm1 == null) return null;  /* no metamethod */
		  if (mt1 == mt2) return tm1;  /* same metatables => same metamethods */
		  tm2 = fasttm(L, mt2, event_);
		  if (tm2 == null) return null;  /* no metamethod */
		  if (LuaORawEqualObj(tm1, tm2) != 0)  /* same metamethods? */
			return tm1;
		return null;
		}


		private static int call_orderTM (LuaState L, TValue p1, TValue p2,
								 TMS event_) {
		  TValue tm1 = luaT_gettmbyobj(L, p1, event_);
		  TValue tm2;
		  if (TTIsNil(tm1)) return -1;  /* no metamethod? */
		  tm2 = luaT_gettmbyobj(L, p2, event_);
		  if (LuaORawEqualObj(tm1, tm2)==0)  /* different metamethods? */
			return -1;
		  callTMres(L, L.top, tm1, p1, p2);
		  return LIsFalse(L.top) == 0 ? 1 : 0;
		}


		private static int l_strcmp (TString ls, TString rs) {
		  CharPtr l = GetStr(ls);
		  uint ll = ls.tsv.len;
		  CharPtr r = GetStr(rs);
		  uint lr = rs.tsv.len;
		  for (;;) {
		    //int temp = strcoll(l, r);
		      int temp = String.Compare(l.ToString(), r.ToString());
		    if (temp != 0) return temp;
		    else {  /* strings are equal up to a `\0' */
		      uint len = (uint)l.ToString().Length;  /* index of first `\0' in both strings */
		      if (len == lr)  /* r is finished? */
		        return (len == ll) ? 0 : 1;
		      else if (len == ll)  /* l is finished? */
		        return -1;  /* l is smaller than r (because r is not finished) */
		      /* both strings longer than `len'; go on comparing (after the `\0') */
		      len++;
		      l += len; ll -= len; r += len; lr -= len;
		    }
		  }
		}


		public static int luaV_lessthan (LuaState L, TValue l, TValue r) {
		  int res;
		  if (TType(l) != TType(r))
			return LuaGOrderError(L, l, r);
		  else if (TTIsNumber(l))
			return luai_numlt(NValue(l), NValue(r)) ? 1 : 0;
		  else if (TTIsString(l))
			  return (l_strcmp(RawTSValue(l), RawTSValue(r)) < 0) ? 1 : 0;
		  else if ((res = call_orderTM(L, l, r, TMS.TM_LT)) != -1)
			return res;
		  return LuaGOrderError(L, l, r);
		}


		private static int lessequal (LuaState L, TValue l, TValue r) {
		  int res;
		  if (TType(l) != TType(r))
			return LuaGOrderError(L, l, r);
		  else if (TTIsNumber(l))
			return luai_numle(NValue(l), NValue(r)) ? 1 : 0;
		  else if (TTIsString(l))
			  return (l_strcmp(RawTSValue(l), RawTSValue(r)) <= 0) ? 1 : 0;
		  else if ((res = call_orderTM(L, l, r, TMS.TM_LE)) != -1)  /* first try `le' */
			return res;
		  else if ((res = call_orderTM(L, r, l, TMS.TM_LT)) != -1)  /* else try `lt' */
			return (res == 0) ? 1 : 0;
		  return LuaGOrderError(L, l, r);
		}

		static CharPtr mybuff = null;

		public static int luaV_equalval (LuaState L, TValue t1, TValue t2) {
		  TValue tm = null;
		  LuaAssert(TType(t1) == TType(t2));
		  switch (TType(t1)) {
			case LUA_TNIL: return 1;
			case LUA_TNUMBER: return luai_numeq(NValue(t1), NValue(t2)) ? 1 : 0;
			case LUA_TBOOLEAN: return (BValue(t1) == BValue(t2)) ? 1 : 0;  /* true must be 1 !! */
			case LUA_TLIGHTUSERDATA: return (PValue(t1) == PValue(t2)) ? 1 : 0;
			case LUA_TUSERDATA: {
			  if (UValue(t1) == UValue(t2)) return 1;
			  tm = get_compTM(L, UValue(t1).metatable, UValue(t2).metatable,
								 TMS.TM_EQ);
			  break;  /* will try TM */
			}
			case LUA_TTABLE: {
			  if (HValue(t1) == HValue(t2)) return 1;
			  tm = get_compTM(L, HValue(t1).metatable, HValue(t2).metatable, TMS.TM_EQ);
			  break;  /* will try TM */
			}
			default: return (GCValue(t1) == GCValue(t2)) ? 1 : 0;
		  }
		  if (tm == null) return 0;  /* no TM? */
		  callTMres(L, L.top, tm, t1, t2);  /* call TM */
		  return LIsFalse(L.top) == 0 ? 1 : 0;
		}


		public static void luaV_concat (LuaState L, int total, int last) {
		  do {
			StkId top = L.base_ + last + 1;
			int n = 2;  /* number of elements handled in this pass (at least 2) */
			if (!(TTIsString(top-2) || TTIsNumber(top-2)) || (tostring(L, top-1)==0)) {
			  if (call_binTM(L, top-2, top-1, top-2, TMS.TM_CONCAT)==0)
				LuaGConcatError(L, top-2, top-1);
			} else if (TSValue(top-1).len == 0)  /* second op is empty? */
			  tostring(L, top - 2);  /* result is first op (as string) */
			else {
			  /* at least two string values; get as many as possible */
			  uint tl = TSValue(top-1).len;
			  CharPtr buffer;
			  int i;
			  /* collect total length */
			  for (n = 1; n < total && (tostring(L, top-n-1)!=0); n++) {
				uint l = TSValue(top-n-1).len;
				if (l >= MAXSIZET - tl) LuaGRunError(L, "string length overflow");
				tl += l;
			  }
			  buffer = luaZ_openspace(L, G(L).buff, tl);
			  if (mybuff == null)
				  mybuff = buffer;
			  tl = 0;
			  for (i=n; i>0; i--) {  /* concat all strings */
				uint l = TSValue(top-i).len;
				memcpy(buffer.chars, (int)tl, SValue(top-i).chars, (int)l);
				tl += l;
			  }
			  SetSValue2S(L, top-n, luaS_newlstr(L, buffer, tl));
			}
			total -= n-1;  /* got `n' strings to create 1 new */
			last -= n-1;
		  } while (total > 1);  /* repeat until only 1 result left */
		}


		public static void Arith (LuaState L, StkId ra, TValue rb,
						   TValue rc, TMS op) {
		  TValue tempb = new LuaTypeValue(), tempc = new LuaTypeValue();
		  TValue b, c;
		  if ((b = luaV_tonumber(rb, tempb)) != null &&
			  (c = luaV_tonumber(rc, tempc)) != null) {
			lua_Number nb = NValue(b), nc = NValue(c);
			switch (op) {
			  case TMS.TM_ADD: SetNValue(ra, luai_numadd(nb, nc)); break;
			  case TMS.TM_SUB: SetNValue(ra, luai_numsub(nb, nc)); break;
			  case TMS.TM_MUL: SetNValue(ra, luai_nummul(nb, nc)); break;
			  case TMS.TM_DIV: SetNValue(ra, luai_numdiv(nb, nc)); break;
			  case TMS.TM_MOD: SetNValue(ra, luai_nummod(nb, nc)); break;
			  case TMS.TM_POW: SetNValue(ra, luai_numpow(nb, nc)); break;
			  case TMS.TM_UNM: SetNValue(ra, luai_numunm(nb)); break;
			  default: LuaAssert(false); break;
			}
		  }
		  else if (call_binTM(L, rb, rc, ra, op) == 0)
			LuaGArithError(L, rb, rc);
		}



		/*
		** some macros for common tasks in `luaV_execute'
		*/

		public static void runtime_check(LuaState L, bool c)	{ Debug.Assert(c); }

		//#define RA(i)	(base+GETARG_A(i))
		/* to be used after possible stack reallocation */
		//#define RB(i)	check_exp(getBMode(GET_OPCODE(i)) == OpArgMask.OpArgR, base+GETARG_B(i))
		//#define RC(i)	check_exp(getCMode(GET_OPCODE(i)) == OpArgMask.OpArgR, base+GETARG_C(i))
		//#define RKB(i)	check_exp(getBMode(GET_OPCODE(i)) == OpArgMask.OpArgK, \
			//ISK(GETARG_B(i)) ? k+INDEXK(GETARG_B(i)) : base+GETARG_B(i))
		//#define RKC(i)	check_exp(getCMode(GET_OPCODE(i)) == OpArgMask.OpArgK, \
		//	ISK(GETARG_C(i)) ? k+INDEXK(GETARG_C(i)) : base+GETARG_C(i))
		//#define KBx(i)	check_exp(getBMode(GET_OPCODE(i)) == OpArgMask.OpArgK, k+GETARG_Bx(i))

		// todo: implement proper checks, as above
		internal static TValue RA(LuaState L, StkId base_, Instruction i) { return base_ + GETARG_A(i); }
		internal static TValue RB(LuaState L, StkId base_, Instruction i) { return base_ + GETARG_B(i); }
		internal static TValue RC(LuaState L, StkId base_, Instruction i) { return base_ + GETARG_C(i); }
		internal static TValue RKB(LuaState L, StkId base_, Instruction i, TValue[] k) { return ISK(GETARG_B(i)) != 0 ? k[INDEXK(GETARG_B(i))] : base_ + GETARG_B(i); }
		internal static TValue RKC(LuaState L, StkId base_, Instruction i, TValue[] k) { return ISK(GETARG_C(i)) != 0 ? k[INDEXK(GETARG_C(i))] : base_ + GETARG_C(i); }
		internal static TValue KBx(LuaState L, Instruction i, TValue[] k) { return k[GETARG_Bx(i)]; }


		public static void dojump(LuaState L, InstructionPtr pc, int i) { pc.pc += i; LuaIThreadYield(L); }


		//#define Protect(x)	{ L.savedpc = pc; {x;}; base = L.base_; }

		[CLSCompliantAttribute(false)]
		public static void arith_op(LuaState L, op_delegate op, TMS tm, StkId base_, Instruction i, TValue[] k, StkId ra, InstructionPtr pc) {
				TValue rb = RKB(L, base_, i, k);
				TValue rc = RKC(L, base_, i, k);
				if (TTIsNumber(rb) && TTIsNumber(rc))
				{
					lua_Number nb = NValue(rb), nc = NValue(rc);
					SetNValue(ra, op(nb, nc));
				}
				else
				{
					//Protect(
					L.savedpc = InstructionPtr.Assign(pc);
					Arith(L, ra, rb, rc, tm);
					base_ = L.base_;
					//);
				}
		      }

		internal static void Dump(int pc, Instruction i)
		{
			int A = GETARG_A(i);
			int B = GETARG_B(i);
			int C = GETARG_C(i);
			int Bx = GETARG_Bx(i);
			int sBx = GETARG_sBx(i);
			if ((sBx & 0x100) != 0)
				sBx = - (sBx & 0xff);

			Console.Write("{0,5} ({1,10}): ", pc, i);
			Console.Write("{0,-10}\t", luaP_opnames[(int)GET_OPCODE(i)]);
			switch (GET_OPCODE(i))
			{
				case OpCode.OP_CLOSE:
					Console.Write("{0}", A);
					break;

				case OpCode.OP_MOVE:
				case OpCode.OP_LOADNIL:
				case OpCode.OP_GETUPVAL:
				case OpCode.OP_SETUPVAL:
				case OpCode.OP_UNM:
				case OpCode.OP_NOT:
				case OpCode.OP_RETURN:
					Console.Write("{0}, {1}", A, B);
					break;

				case OpCode.OP_LOADBOOL:
				case OpCode.OP_GETTABLE:
				case OpCode.OP_SETTABLE:
				case OpCode.OP_NEWTABLE:
				case OpCode.OP_SELF:
				case OpCode.OP_ADD:
				case OpCode.OP_SUB:
				case OpCode.OP_MUL:
				case OpCode.OP_DIV:
				case OpCode.OP_POW:
				case OpCode.OP_CONCAT:
				case OpCode.OP_EQ:
				case OpCode.OP_LT:
				case OpCode.OP_LE:
				case OpCode.OP_TEST:
				case OpCode.OP_CALL:
				case OpCode.OP_TAILCALL:
					Console.Write("{0}, {1}, {2}", A, B, C);
					break;

				case OpCode.OP_LOADK:					
					Console.Write("{0}, {1}", A, Bx);
					break;

				case OpCode.OP_GETGLOBAL:
				case OpCode.OP_SETGLOBAL:
				case OpCode.OP_SETLIST:
				case OpCode.OP_CLOSURE:
					Console.Write("{0}, {1}", A, Bx);
					break;

				case OpCode.OP_TFORLOOP:
					Console.Write("{0}, {1}", A, C);
					break;

				case OpCode.OP_JMP:
				case OpCode.OP_FORLOOP:
				case OpCode.OP_FORPREP:
					Console.Write("{0}, {1}", A, sBx);
					break;
			}
			Console.WriteLine();

		}

		public static void luaV_execute (LuaState L, int nexeccalls) {
		  LClosure cl;
		  StkId base_;
		  TValue[] k;
		  /*const*/ InstructionPtr pc;
		 reentry:  /* entry point */
		  LuaAssert(IsLua(L.ci));		  
		  pc = InstructionPtr.Assign(L.savedpc);		  
		  cl = CLValue(L.ci.func).l;
		  base_ = L.base_;
		  k = cl.p.k;
		  /* main loop of interpreter */
		  for (;;) {
			/*const*/ Instruction i = InstructionPtr.inc(ref pc)[0];
			StkId ra;
			if ( ((L.hookmask & (LUA_MASKLINE | LUA_MASKCOUNT)) != 0) &&
				(((--L.hookcount) == 0) || ((L.hookmask & LUA_MASKLINE) != 0))) {
			  traceexec(L, pc);
			  if (L.status == LUA_YIELD) {  /* did hook yield? */
				L.savedpc = new InstructionPtr(pc.codes, pc.pc - 1);
				return;
			  }
			  base_ = L.base_;
			}
			/* warning!! several calls may realloc the stack and invalidate `ra' */
			ra = RA(L, base_, i);
			LuaAssert(base_ == L.base_ && L.base_ == L.ci.base_);
			LuaAssert(base_ <= L.top && ((L.top - L.stack) <= L.stacksize));
			LuaAssert(L.top == L.ci.top || (LuaGCheckOpenOp(i)!=0));
			//Dump(pc.pc, i);			
			switch (GET_OPCODE(i)) {
			  case OpCode.OP_MOVE: {
				SetObj2S(L, ra, RB(L, base_, i));
				continue;
			  }
			  case OpCode.OP_LOADK: {
				SetObj2S(L, ra, KBx(L, i, k));
				continue;
			  }
			  case OpCode.OP_LOADBOOL: {
				SetBValue(ra, GETARG_B(i));
				if (GETARG_C(i) != 0) InstructionPtr.inc(ref pc);  /* skip next instruction (if C) */
				continue;
			  }
			  case OpCode.OP_LOADNIL: {
				TValue rb = RB(L, base_, i);
				do {
					SetNilValue(StkId.Dec(ref rb));
				} while (rb >= ra);
				continue;
			  }
			  case OpCode.OP_GETUPVAL: {
				int b = GETARG_B(i);
				SetObj2S(L, ra, cl.upvals[b].v);
				continue;
			  }
			  case OpCode.OP_GETGLOBAL: {
				TValue g = new LuaTypeValue();
				TValue rb = KBx(L, i, k);
				SetHValue(L, g, cl.env);
				LuaAssert(TTIsString(rb));
				//Protect(
				  L.savedpc = InstructionPtr.Assign(pc);
				  luaV_gettable(L, g, rb, ra);
				  base_ = L.base_;
				  //);
				  L.savedpc = InstructionPtr.Assign(pc);
				continue;
			  }
			  case OpCode.OP_GETTABLE: {
				//Protect(
				  L.savedpc = InstructionPtr.Assign(pc);
				  luaV_gettable(L, RB(L, base_, i), RKC(L, base_, i, k), ra);
				  base_ = L.base_;
				  //);
				L.savedpc = InstructionPtr.Assign(pc);
				continue;
			  }
			  case OpCode.OP_SETGLOBAL: {
				TValue g = new LuaTypeValue();
				SetHValue(L, g, cl.env);
				LuaAssert(TTIsString(KBx(L, i, k)));
				//Protect(
				  L.savedpc = InstructionPtr.Assign(pc);
				  luaV_settable(L, g, KBx(L, i, k), ra);
				  base_ = L.base_;
				  //);
				L.savedpc = InstructionPtr.Assign(pc);
				continue;
			  }
			  case OpCode.OP_SETUPVAL: {
				UpVal uv = cl.upvals[GETARG_B(i)];
				SetObj(L, uv.v, ra);
				LuaCBarrier(L, uv, ra);
				continue;
			  }
			  case OpCode.OP_SETTABLE: {
				//Protect(
				  L.savedpc = InstructionPtr.Assign(pc);
				  luaV_settable(L, ra, RKB(L, base_, i, k), RKC(L, base_, i, k));
				  base_ = L.base_;
				  //);
				L.savedpc = InstructionPtr.Assign(pc);
				continue;
			  }
			  case OpCode.OP_NEWTABLE: {
				int b = GETARG_B(i);
				int c = GETARG_C(i);
				SetHValue(L, ra, luaH_new(L, LuaOFBInt(b), LuaOFBInt(c)));
				//Protect(
				  L.savedpc = InstructionPtr.Assign(pc);
				  LuaCCheckGC(L);
				  base_ = L.base_;
				  //);
				L.savedpc = InstructionPtr.Assign(pc);
				continue;
			  }
			  case OpCode.OP_SELF: {
				StkId rb = RB(L, base_, i);
				SetObj2S(L, ra + 1, rb);
				//Protect(
				  L.savedpc = InstructionPtr.Assign(pc);
				  luaV_gettable(L, rb, RKC(L, base_, i, k), ra);
				  base_ = L.base_;
				  //);
				L.savedpc = InstructionPtr.Assign(pc);
				continue;
			  }
			  case OpCode.OP_ADD: {
				arith_op(L, luai_numadd, TMS.TM_ADD, base_, i, k, ra, pc);
				continue;
			  }
			  case OpCode.OP_SUB: {
				arith_op(L, luai_numsub, TMS.TM_SUB, base_, i, k, ra, pc);
				continue;
			  }
			  case OpCode.OP_MUL: {
				arith_op(L, luai_nummul, TMS.TM_MUL, base_, i, k, ra, pc);
				continue;
			  }
			  case OpCode.OP_DIV: {
				arith_op(L, luai_numdiv, TMS.TM_DIV, base_, i, k, ra, pc);
				continue;
			  }
			  case OpCode.OP_MOD: {
				arith_op(L, luai_nummod, TMS.TM_MOD, base_, i, k, ra, pc);
				continue;
			  }
			  case OpCode.OP_POW: {
				arith_op(L, luai_numpow, TMS.TM_POW, base_, i, k, ra, pc);
				continue;
			  }
			  case OpCode.OP_UNM: {
				TValue rb = RB(L, base_, i);
				if (TTIsNumber(rb)) {
				  lua_Number nb = NValue(rb);
				  SetNValue(ra, luai_numunm(nb));
				}
				else {
				  //Protect(
					L.savedpc = InstructionPtr.Assign(pc);
					Arith(L, ra, rb, rb, TMS.TM_UNM);
					base_ = L.base_;
					//);
				  L.savedpc = InstructionPtr.Assign(pc);
				}
				continue;
			  }
			  case OpCode.OP_NOT: {
				int res = LIsFalse(RB(L, base_, i)) == 0 ? 0 : 1;  /* next assignment may change this value */
				SetBValue(ra, res);
				continue;
			  }
			  case OpCode.OP_LEN: {
				TValue rb = RB(L, base_, i);
				switch (TType(rb)) {
				  case LUA_TTABLE: {
					SetNValue(ra, (lua_Number)luaH_getn(HValue(rb)));
					break;
				  }
				  case LUA_TSTRING: {
					SetNValue(ra, (lua_Number)TSValue(rb).len);
					break;
				  }
				  default: {  /* try metamethod */
					//Protect(
					  L.savedpc = InstructionPtr.Assign(pc);
					  if (call_binTM(L, rb, LuaONilObject, ra, TMS.TM_LEN) == 0)
						LuaGTypeError(L, rb, "get length of");
					  base_ = L.base_;
					//)
					  break;
				  }
				}
				continue;
			  }
			  case OpCode.OP_CONCAT: {
				int b = GETARG_B(i);
				int c = GETARG_C(i);
				//Protect(
				  L.savedpc = InstructionPtr.Assign(pc);
				  luaV_concat(L, c-b+1, c); LuaCCheckGC(L);
				  base_ = L.base_;
				  //);
				SetObj2S(L, RA(L, base_, i), base_ + b);
				continue;
			  }
			  case OpCode.OP_JMP: {
				dojump(L, pc, GETARG_sBx(i));
				continue;
			  }
			  case OpCode.OP_EQ: {
				TValue rb = RKB(L, base_, i, k);
				TValue rc = RKC(L, base_, i, k);
				//Protect(
				  L.savedpc = InstructionPtr.Assign(pc);
				  if (equalobj(L, rb, rc) == GETARG_A(i))
					dojump(L, pc, GETARG_sBx(pc[0]));
				  base_ = L.base_;
				//);
				InstructionPtr.inc(ref pc);
				continue;
			  }
			  case OpCode.OP_LT: {
				//Protect(
				  L.savedpc = InstructionPtr.Assign(pc);
				  if (luaV_lessthan(L, RKB(L, base_, i, k), RKC(L, base_, i, k)) == GETARG_A(i))
					dojump(L, pc, GETARG_sBx(pc[0]));
				  base_ = L.base_;
				//);
				InstructionPtr.inc(ref pc);
				continue;
			  }
			  case OpCode.OP_LE: {
				//Protect(
				  L.savedpc = InstructionPtr.Assign(pc);
				  if (lessequal(L, RKB(L, base_, i, k), RKC(L, base_, i, k)) == GETARG_A(i))
					dojump(L, pc, GETARG_sBx(pc[0]));
				  base_ = L.base_;
				//);
				InstructionPtr.inc(ref pc);
				continue;
			  }
			  case OpCode.OP_TEST: {
				if (LIsFalse(ra) != GETARG_C(i))
				  dojump(L, pc, GETARG_sBx(pc[0]));
				InstructionPtr.inc(ref pc);
				continue;
			  }
			  case OpCode.OP_TESTSET: {
				TValue rb = RB(L, base_, i);
				if (LIsFalse(rb) != GETARG_C(i)) {
				  SetObj2S(L, ra, rb);
				  dojump(L, pc, GETARG_sBx(pc[0]));
				}
				InstructionPtr.inc(ref pc);
				continue;
			  }
			  case OpCode.OP_CALL: {
				int b = GETARG_B(i);
				int nresults = GETARG_C(i) - 1;
				if (b != 0) L.top = ra + b;  /* else previous instruction set top */
				L.savedpc = InstructionPtr.Assign(pc);
				switch (LuaDPreCall(L, ra, nresults)) {
				  case PCRLUA: {
					nexeccalls++;
					goto reentry;  /* restart luaV_execute over new Lua function */
				  }
				  case PCRC: {
					/* it was a C function (`precall' called it); adjust results */
					if (nresults >= 0) L.top = L.ci.top;
					base_ = L.base_;
					continue;
				  }
				  default: {
					return;  /* yield */
				  }
				}
			  }
			  case OpCode.OP_TAILCALL: {
				int b = GETARG_B(i);
				if (b != 0) L.top = ra + b;  /* else previous instruction set top */
				L.savedpc = InstructionPtr.Assign(pc);
				LuaAssert(GETARG_C(i) - 1 == LUA_MULTRET);
				switch (LuaDPreCall(L, ra, LUA_MULTRET)) {
				  case PCRLUA: {
					/* tail call: put new frame in place of previous one */
					CallInfo ci = L.ci - 1;  /* previous frame */
					int aux;
					StkId func = ci.func;
					StkId pfunc = (ci+1).func;  /* previous function index */
					if (L.openupval != null) LuaFClose(L, ci.base_);
					L.base_ = ci.base_ = ci.func + (ci[1].base_ - pfunc);
					for (aux = 0; pfunc+aux < L.top; aux++)  /* move frame down */
					  SetObj2S(L, func+aux, pfunc+aux);
					ci.top = L.top = func+aux;  /* correct top */
					LuaAssert(L.top == L.base_ + CLValue(func).l.p.maxstacksize);
					ci.savedpc = InstructionPtr.Assign(L.savedpc);
					ci.tailcalls++;  /* one more call lost */
					CallInfo.Dec(ref L.ci);  /* remove new frame */
					goto reentry;
				  }
				  case PCRC: {  /* it was a C function (`precall' called it) */
					base_ = L.base_;
					continue;
				  }
				  default: {
					return;  /* yield */
				  }
				}
			  }
			  case OpCode.OP_RETURN: {
				int b = GETARG_B(i);
				if (b != 0) L.top = ra+b-1;
				if (L.openupval != null) LuaFClose(L, base_);
				L.savedpc = InstructionPtr.Assign(pc);
				b = LuaDPosCall(L, ra);
				if (--nexeccalls == 0)  /* was previous function running `here'? */
				  return;  /* no: return */
				else {  /* yes: continue its execution */
				  if (b != 0) L.top = L.ci.top;
				  LuaAssert(IsLua(L.ci));
				  LuaAssert(GET_OPCODE(L.ci.savedpc[-1]) == OpCode.OP_CALL);
				  goto reentry;
				}
			  }
			  case OpCode.OP_FORLOOP: {
				lua_Number step = NValue(ra+2);
				lua_Number idx = luai_numadd(NValue(ra), step); /* increment index */
				lua_Number limit = NValue(ra+1);
				if (luai_numlt(0, step) ? luai_numle(idx, limit)
										: luai_numle(limit, idx)) {
				  dojump(L, pc, GETARG_sBx(i));  /* jump back */
				  SetNValue(ra, idx);  /* update internal index... */
				  SetNValue(ra+3, idx);  /* ...and external index */
				}
				continue;
			  }
			  case OpCode.OP_FORPREP: {
				TValue init = ra;
				TValue plimit = ra+1;
				TValue pstep = ra+2;
				L.savedpc = InstructionPtr.Assign(pc);  /* next steps may throw errors */
				if (tonumber(ref init, ra) == 0)
				  LuaGRunError(L, LUA_QL("for") + " initial value must be a number");
				else if (tonumber(ref plimit, ra+1)  == 0)
				  LuaGRunError(L, LUA_QL("for") + " limit must be a number");
				else if (tonumber(ref pstep, ra+2)  == 0)
				  LuaGRunError(L, LUA_QL("for") + " step must be a number");
				SetNValue(ra, luai_numsub(NValue(ra), NValue(pstep)));
				dojump(L, pc, GETARG_sBx(i));
				continue;
			  }
			  case OpCode.OP_TFORLOOP: {
				StkId cb = ra + 3;  /* call base */
				SetObj2S(L, cb+2, ra+2);
				SetObj2S(L, cb+1, ra+1);
				SetObj2S(L, cb, ra);
				L.top = cb+3;  /* func. + 2 args (state and index) */
				//Protect(
					L.savedpc = InstructionPtr.Assign(pc);
					LuaDCall(L, cb, GETARG_C(i));
					base_ = L.base_;
				  //);
				L.top = L.ci.top;
				cb = RA(L, base_, i) + 3;  /* previous call may change the stack */
				if (!TTIsNil(cb)) {  /* continue loop? */
				  SetObj2S(L, cb-1, cb);  /* save control variable */
				  dojump(L, pc, GETARG_sBx(pc[0]));  /* jump back */
				}
				InstructionPtr.inc(ref pc);
				continue;
			  }
			  case OpCode.OP_SETLIST: {
				int n = GETARG_B(i);
				int c = GETARG_C(i);
				int last;
				Table h;
				if (n == 0) {
				  n = CastInt(L.top - ra) - 1;
				  L.top = L.ci.top;
				}
				if (c == 0)
				{
					c = CastInt(pc[0]);
					InstructionPtr.inc(ref pc);
				}
				runtime_check(L, TTIsTable(ra));
				h = HValue(ra);
				last = ((c-1)*LFIELDS_PER_FLUSH) + n;
				if (last > h.sizearray)  /* needs more space? */
				  luaH_resizearray(L, h, last);  /* pre-alloc it at once */
				for (; n > 0; n--) {
				  TValue val = ra+n;
				  SetObj2T(L, luaH_setnum(L, h, last--), val);
				  LuaCBarrierT(L, h, val);
				}
				continue;
			  }
			  case OpCode.OP_CLOSE: {
				LuaFClose(L, ra);
				continue;
			  }
			  case OpCode.OP_CLOSURE: {
				Proto p;
				Closure ncl;
				int nup, j;
				p = cl.p.p[GETARG_Bx(i)];
				nup = p.nups;
				ncl = LuaFNewLClosure(L, nup, cl.env);
				ncl.l.p = p;
				for (j=0; j<nup; j++, InstructionPtr.inc(ref pc)) {
				  if (GET_OPCODE(pc[0]) == OpCode.OP_GETUPVAL)
					ncl.l.upvals[j] = cl.upvals[GETARG_B(pc[0])];
				  else {
					LuaAssert(GET_OPCODE(pc[0]) == OpCode.OP_MOVE);
					ncl.l.upvals[j] = LuaFindUpVal(L, base_ + GETARG_B(pc[0]));
				  }
				}
				SetCLValue(L, ra, ncl);
				//Protect(
				  L.savedpc = InstructionPtr.Assign(pc);
					LuaCCheckGC(L);
				  base_ = L.base_;
				  //);
				continue;
			  }
			  case OpCode.OP_VARARG: {
				int b = GETARG_B(i) - 1;
				int j;
				CallInfo ci = L.ci;
				int n = CastInt(ci.base_ - ci.func) - cl.p.numparams - 1;
				if (b == LUA_MULTRET) {
				  //Protect(
					L.savedpc = InstructionPtr.Assign(pc);
					  LuaDCheckStack(L, n);
					base_ = L.base_;
					//);
				  ra = RA(L, base_, i);  /* previous call may change the stack */
				  b = n;
				  L.top = ra + n;
				}
				for (j = 0; j < b; j++) {
				  if (j < n) {
					SetObj2S(L, ra + j, ci.base_ - n + j);
				  }
				  else {
					SetNilValue(ra + j);
				  }
				}
				continue;
			  }
			}
		  }
		}

	}
}

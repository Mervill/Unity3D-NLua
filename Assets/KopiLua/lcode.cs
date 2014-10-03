/*
** $Id: lcode.c,v 2.25.1.5 2011/01/31 14:53:16 roberto Exp $
** Code generator for Lua
** See Copyright Notice in lua.h
*/

using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace KopiLua
{
	using TValue = Lua.LuaTypeValue;
	using LuaNumberType = System.Double;
	using Instruction = System.UInt32;

	public class InstructionPtr
	{
#if !UNITY_3D
		[CLSCompliantAttribute(false)]
#endif
		public Instruction[] codes;
		public int pc;

		public InstructionPtr() { this.codes = null; ; this.pc = -1; }
#if !UNITY_3D
		[CLSCompliantAttribute(false)]
#endif
		public InstructionPtr(Instruction[] codes, int pc) {
			this.codes = codes; this.pc = pc; }
		public static InstructionPtr Assign(InstructionPtr ptr)
		{
			if (ptr == null) return null;
			return new InstructionPtr(ptr.codes, ptr.pc);
		}
#if !UNITY_3D
		[CLSCompliantAttribute(false)]
#endif
		public Instruction this[int index]
		{
			get { return this.codes[pc + index]; }
			set { this.codes[pc + index] = value; }
		}
		public static InstructionPtr inc(ref InstructionPtr ptr)
		{
			InstructionPtr result = new InstructionPtr(ptr.codes, ptr.pc);
			ptr.pc++;
			return result;
		}
		public static InstructionPtr dec(ref InstructionPtr ptr)
		{
			InstructionPtr result = new InstructionPtr(ptr.codes, ptr.pc);
			ptr.pc--;
			return result;
		}
		public static bool operator <(InstructionPtr p1, InstructionPtr p2)
		{
			Debug.Assert(p1.codes == p2.codes);
			return p1.pc < p2.pc;
		}
		public static bool operator >(InstructionPtr p1, InstructionPtr p2)
		{
			Debug.Assert(p1.codes == p2.codes);
			return p1.pc > p2.pc;
		}
		public static bool operator <=(InstructionPtr p1, InstructionPtr p2)
		{
			Debug.Assert(p1.codes == p2.codes);
			return p1.pc < p2.pc;
		}
		public static bool operator >=(InstructionPtr p1, InstructionPtr p2)
		{
			Debug.Assert(p1.codes == p2.codes);
			return p1.pc > p2.pc;
		}
	};

	public partial class Lua
	{
		/*
		** Marks the end of a patch list. It is an invalid value both as an absolute
		** address, and as a list link (would link an element to itself).
		*/
		public const int NO_JUMP = (-1);


		/*
		** grep "ORDER OPR" if you change these enums
		*/
		public enum BinOpr {
		  OPR_ADD, OPR_SUB, OPR_MUL, OPR_DIV, OPR_MOD, OPR_POW,
		  OPR_CONCAT,
		  OPR_NE, OPR_EQ,
		  OPR_LT, OPR_LE, OPR_GT, OPR_GE,
		  OPR_AND, OPR_OR,
		  OPR_NOBINOPR
		};


		public enum UnOpr { OPR_MINUS, OPR_NOT, OPR_LEN, OPR_NOUNOPR };


		public static InstructionPtr GetCode(FuncState fs, expdesc e)	{return new InstructionPtr(fs.f.code, e.u.s.info);}

		public static int LuaKCodeAsBx(FuncState fs, OpCode o, int A, int sBx)	{return LuaKCodeABx(fs,o,A,sBx+MAXARG_sBx);}

		public static void LuaKSetMultRet(FuncState fs, expdesc e)	{LuaKSetReturns(fs, e, LUA_MULTRET);}

		public static bool HasJumps(expdesc e)	{return e.t != e.f;}


		private static int IsNumeral(expdesc e) {
		  return (e.k == expkind.VKNUM && e.t == NO_JUMP && e.f == NO_JUMP) ? 1 : 0;
		}


		public static void LuaKNil (FuncState fs, int from, int n) {
		  InstructionPtr previous;
		  if (fs.pc > fs.lasttarget) {  /* no jumps to current position? */
			if (fs.pc == 0) {  /* function start? */
			  if (from >= fs.nactvar)
				return;  /* positions are already clean */
			}
			else {
			  previous = new InstructionPtr(fs.f.code, fs.pc-1);
			  if (GET_OPCODE(previous) == OpCode.OP_LOADNIL) {
				int pfrom = GETARG_A(previous);
				int pto = GETARG_B(previous);
				if (pfrom <= from && from <= pto+1) {  /* can connect both? */
				  if (from+n-1 > pto)
					SETARG_B(previous, from+n-1);
				  return;
				}
			  }
			}
		  }
		  LuaKCodeABC(fs, OpCode.OP_LOADNIL, from, from + n - 1, 0);  /* else no optimization */
		}


		public static int LuaKJump (FuncState fs) {
		  int jpc = fs.jpc;  /* save list of jumps to here */
		  int j;
		  fs.jpc = NO_JUMP;
		  j = LuaKCodeAsBx(fs, OpCode.OP_JMP, 0, NO_JUMP);
		  LuaKConcat(fs, ref j, jpc);  /* keep them on hold */
		  return j;
		}


		public static void LuaKRet (FuncState fs, int first, int nret) {
			LuaKCodeABC(fs, OpCode.OP_RETURN, first, nret + 1, 0);
		}


		private static int CondJump (FuncState fs, OpCode op, int A, int B, int C) {
		  LuaKCodeABC(fs, op, A, B, C);
		  return LuaKJump(fs);
		}


		private static void FixJump (FuncState fs, int pc, int dest) {
		  InstructionPtr jmp = new InstructionPtr(fs.f.code, pc);
		  int offset = dest-(pc+1);
		  LuaAssert(dest != NO_JUMP);
		  if (Math.Abs(offset) > MAXARG_sBx)
			LuaXSyntaxError(fs.ls, "control structure too long");
		  SETARG_sBx(jmp, offset);
		}


		/*
		** returns current `pc' and marks it as a jump target (to avoid wrong
		** optimizations with consecutive instructions not in the same basic block).
		*/
		public static int LuaKGetLabel (FuncState fs) {
		  fs.lasttarget = fs.pc;
		  return fs.pc;
		}


		private static int GetJump (FuncState fs, int pc) {
		  int offset = GETARG_sBx(fs.f.code[pc]);
		  if (offset == NO_JUMP)  /* point to itself represents end of list */
			return NO_JUMP;  /* end of list */
		  else
			return (pc+1)+offset;  /* turn offset into absolute position */
		}

		private static InstructionPtr GetJumpControl (FuncState fs, int pc) {
		  InstructionPtr pi = new InstructionPtr(fs.f.code, pc);
		  if (pc >= 1 && (testTMode(GET_OPCODE(pi[-1]))!=0))
			return new InstructionPtr(pi.codes, pi.pc-1);
		  else
			return new InstructionPtr(pi.codes, pi.pc);
		}


		/*
		** check whether list has any jump that do not produce a value
		** (or produce an inverted value)
		*/
		private static int NeedValue (FuncState fs, int list) {
		  for (; list != NO_JUMP; list = GetJump(fs, list)) {
			InstructionPtr i = GetJumpControl(fs, list);
			if (GET_OPCODE(i[0]) != OpCode.OP_TESTSET) return 1;
		  }
		  return 0;  /* not found */
		}


		private static int PatchTestReg (FuncState fs, int node, int reg) {
		  InstructionPtr i = GetJumpControl(fs, node);
		  if (GET_OPCODE(i[0]) != OpCode.OP_TESTSET)
			return 0;  /* cannot patch other instructions */
		if (reg != NO_REG && reg != GETARG_B(i[0]))
			SETARG_A(i, reg);
		  else  /* no register to put value or register already has the value */
			i[0] = (uint)CREATE_ABC(OpCode.OP_TEST, GETARG_B(i[0]), 0, GETARG_C(i[0]));

		  return 1;
		}


		private static void RemoveValues (FuncState fs, int list) {
		  for (; list != NO_JUMP; list = GetJump(fs, list))
			  PatchTestReg(fs, list, NO_REG);
		}


		private static void PatchListAux (FuncState fs, int list, int vtarget, int reg,
								  int dtarget) {
		  while (list != NO_JUMP) {
			int next = GetJump(fs, list);
			if (PatchTestReg(fs, list, reg) != 0)
			  FixJump(fs, list, vtarget);
			else
			  FixJump(fs, list, dtarget);  /* jump to default target */
			list = next;
		  }
		}


		private static void DischargeJPC (FuncState fs) {
		  PatchListAux(fs, fs.jpc, fs.pc, NO_REG, fs.pc);
		  fs.jpc = NO_JUMP;
		}


		public static void LuaKPatchList (FuncState fs, int list, int target) {
		  if (target == fs.pc)
			LuaKPatchToHere(fs, list);
		  else {
			LuaAssert(target < fs.pc);
			PatchListAux(fs, list, target, NO_REG, target);
		  }
		}


		public static void LuaKPatchToHere (FuncState fs, int list) {
		  LuaKGetLabel(fs);
		  LuaKConcat(fs, ref fs.jpc, list);
		}


		public static void LuaKConcat(FuncState fs, ref int l1, int l2)
		{
		  if (l2 == NO_JUMP) return;
		  else if (l1 == NO_JUMP)
			l1 = l2;
		  else {
			int list = l1;
			int next;
			while ((next = GetJump(fs, list)) != NO_JUMP)  /* find last element */
			  list = next;
			FixJump(fs, list, l2);
		  }
		}


		public static void LuaKCheckStack (FuncState fs, int n) {
		  int newstack = fs.freereg + n;
		  if (newstack > fs.f.maxstacksize) {
			if (newstack >= MAXSTACK)
			  LuaXSyntaxError(fs.ls, "function or expression too complex");
			fs.f.maxstacksize = CastByte(newstack);
		  }
		}


		public static void LuaKReserveRegs (FuncState fs, int n) {
		  LuaKCheckStack(fs, n);
		  fs.freereg += n;
		}


		private static void FreeReg (FuncState fs, int reg) {
		  if ((ISK(reg)==0) && reg >= fs.nactvar) {
			fs.freereg--;
			LuaAssert(reg == fs.freereg);
		  }
		}


		private static void FreeExp (FuncState fs, expdesc e) {
		  if (e.k == expkind.VNONRELOC)
			FreeReg(fs, e.u.s.info);
		}


		private static int AddK (FuncState fs, TValue k, TValue v) {
		  LuaState L = fs.L;
		  TValue idx = luaH_set(L, fs.h, k);
		  Proto f = fs.f;
		  int oldsize = f.sizek;
		  if (TTIsNumber(idx)) {
			LuaAssert(LuaORawEqualObj(fs.f.k[CastInt(NValue(idx))], v));
			return CastInt(NValue(idx));
		  }
		  else {  /* constant not found; create a new entry */
			SetNValue(idx, CastNum(fs.nk));
			LuaMGrowVector(L, ref f.k, fs.nk, ref f.sizek,
							MAXARG_Bx, "constant table overflow");
			while (oldsize < f.sizek) SetNilValue(f.k[oldsize++]);
			SetObj(L, f.k[fs.nk], v);
			LuaCBarrier(L, f, v);
			return fs.nk++;
		  }
		}


		public static int LuaKStringK (FuncState fs, TString s) {
		  TValue o = new LuaTypeValue();
		  SetSValue(fs.L, o, s);
		  return AddK(fs, o, o);
		}


		public static int LuaKNumberK (FuncState fs, LuaNumberType r) {
		  TValue o = new LuaTypeValue();
		  SetNValue(o, r);
		  return AddK(fs, o, o);
		}


		private static int BoolValueK (FuncState fs, int b) {
		  TValue o = new LuaTypeValue();
		  SetBValue(o, b);
		  return AddK(fs, o, o);
		}


		private static int NilK (FuncState fs) {
		  TValue k = new LuaTypeValue(), v = new LuaTypeValue();
		  SetNilValue(v);
		  /* cannot use nil as key; instead use table itself to represent nil */
		  SetHValue(fs.L, k, fs.h);
		  return AddK(fs, k, v);
		}


		public static void LuaKSetReturns (FuncState fs, expdesc e, int nresults) {
		  if (e.k == expkind.VCALL) {  /* expression is an open function call? */
			SETARG_C(GetCode(fs, e), nresults+1);
		  }
		  else if (e.k == expkind.VVARARG) {
			SETARG_B(GetCode(fs, e), nresults+1);
			SETARG_A(GetCode(fs, e), fs.freereg);
			LuaKReserveRegs(fs, 1);
		  }
		}


		public static void LuaKSetOneRet (FuncState fs, expdesc e) {
		  if (e.k == expkind.VCALL) {  /* expression is an open function call? */
			e.k = expkind.VNONRELOC;
			e.u.s.info = GETARG_A(GetCode(fs, e));
		  }
		  else if (e.k == expkind.VVARARG) {
			SETARG_B(GetCode(fs, e), 2);
			e.k = expkind.VRELOCABLE;  /* can relocate its simple result */
		  }
		}


		public static void LuaKDischargeVars (FuncState fs, expdesc e) {
		  switch (e.k) {
			case expkind.VLOCAL: {
			  e.k = expkind.VNONRELOC;
			  break;
			}
			case expkind.VUPVAL: {
			  e.u.s.info = LuaKCodeABC(fs, OpCode.OP_GETUPVAL, 0, e.u.s.info, 0);
			  e.k = expkind.VRELOCABLE;
			  break;
			}
			case expkind.VGLOBAL: {
				e.u.s.info = LuaKCodeABx(fs, OpCode.OP_GETGLOBAL, 0, e.u.s.info);
			  e.k = expkind.VRELOCABLE;
			  break;
			}
			case expkind.VINDEXED: {
			  FreeReg(fs, e.u.s.aux);
			  FreeReg(fs, e.u.s.info);
			  e.u.s.info = LuaKCodeABC(fs, OpCode.OP_GETTABLE, 0, e.u.s.info, e.u.s.aux);
			  e.k = expkind.VRELOCABLE;
			  break;
			}
			case expkind.VVARARG:
			case expkind.VCALL: {
			  LuaKSetOneRet(fs, e);
			  break;
			}
			default: break;  /* there is one value available (somewhere) */
		  }
		}


		private static int CodeLabel (FuncState fs, int A, int b, int jump) {
		  LuaKGetLabel(fs);  /* those instructions may be jump targets */
		  return LuaKCodeABC(fs, OpCode.OP_LOADBOOL, A, b, jump);
		}


		private static void Discharge2Reg (FuncState fs, expdesc e, int reg) {
		  LuaKDischargeVars(fs, e);
		  switch (e.k) {
			case expkind.VNIL: {
			  LuaKNil(fs, reg, 1);
			  break;
			}
			case expkind.VFALSE:  case expkind.VTRUE: {
				LuaKCodeABC(fs, OpCode.OP_LOADBOOL, reg, (e.k == expkind.VTRUE) ? 1 : 0, 0);
			  break;
			}
			case expkind.VK: {
			  LuaKCodeABx(fs, OpCode.OP_LOADK, reg, e.u.s.info);
			  break;
			}
			case expkind.VKNUM: {
			  LuaKCodeABx(fs, OpCode.OP_LOADK, reg, LuaKNumberK(fs, e.u.nval));
			  break;
			}
			case expkind.VRELOCABLE: {
			  InstructionPtr pc = GetCode(fs, e);
			  SETARG_A(pc, reg);
			  break;
			}
			case expkind.VNONRELOC: {
			  if (reg != e.u.s.info)
				LuaKCodeABC(fs, OpCode.OP_MOVE, reg, e.u.s.info, 0);
			  break;
			}
			default: {
			  LuaAssert(e.k == expkind.VVOID || e.k == expkind.VJMP);
			  return;  /* nothing to do... */
			}
		  }
		  e.u.s.info = reg;
		  e.k = expkind.VNONRELOC;
		}


		private static void Discharge2AnyReg (FuncState fs, expdesc e) {
		  if (e.k != expkind.VNONRELOC) {
			LuaKReserveRegs(fs, 1);
			Discharge2Reg(fs, e, fs.freereg-1);
		  }
		}


		private static void Exp2Reg (FuncState fs, expdesc e, int reg) {
		  Discharge2Reg(fs, e, reg);
		  if (e.k == expkind.VJMP)
			LuaKConcat(fs, ref e.t, e.u.s.info);  /* put this jump in `t' list */
		  if (HasJumps(e)) {
			int final;  /* position after whole expression */
			int p_f = NO_JUMP;  /* position of an eventual LOAD false */
			int p_t = NO_JUMP;  /* position of an eventual LOAD true */
			if (NeedValue(fs, e.t)!=0 || NeedValue(fs, e.f)!=0) {
			  int fj = (e.k == expkind.VJMP) ? NO_JUMP : LuaKJump(fs);
			  p_f = CodeLabel(fs, reg, 0, 1);
			  p_t = CodeLabel(fs, reg, 1, 0);
			  LuaKPatchToHere(fs, fj);
			}
			final = LuaKGetLabel(fs);
			PatchListAux(fs, e.f, final, reg, p_f);
			PatchListAux(fs, e.t, final, reg, p_t);
		  }
		  e.f = e.t = NO_JUMP;
		  e.u.s.info = reg;
		  e.k = expkind.VNONRELOC;
		}


		public static void LuaKExp2NextReg (FuncState fs, expdesc e) {
		  LuaKDischargeVars(fs, e);
		  FreeExp(fs, e);
		  LuaKReserveRegs(fs, 1);
		  Exp2Reg(fs, e, fs.freereg - 1);
		}


		public static int LuaKExp2AnyReg (FuncState fs, expdesc e) {
		  LuaKDischargeVars(fs, e);
		  if (e.k == expkind.VNONRELOC) {
			if (!HasJumps(e)) return e.u.s.info;  /* exp is already in a register */
			if (e.u.s.info >= fs.nactvar) {  /* reg. is not a local? */
			  Exp2Reg(fs, e, e.u.s.info);  /* put value on it */
			  return e.u.s.info;
			}
		  }
		  LuaKExp2NextReg(fs, e);  /* default */
		  return e.u.s.info;
		}


		public static void LuaKExp2Val (FuncState fs, expdesc e) {
		  if (HasJumps(e))
			LuaKExp2AnyReg(fs, e);
		  else
			LuaKDischargeVars(fs, e);
		}


		public static int LuaKExp2RK (FuncState fs, expdesc e) {
		  LuaKExp2Val(fs, e);
		  switch (e.k) {
			case expkind.VKNUM:
			case expkind.VTRUE:
			case expkind.VFALSE:
			case expkind.VNIL: {
			  if (fs.nk <= MAXINDEXRK) {  /* constant fit in RK operand? */
				e.u.s.info = (e.k == expkind.VNIL)  ? NilK(fs) :
							  (e.k == expkind.VKNUM) ? LuaKNumberK(fs, e.u.nval) :
							  BoolValueK(fs, (e.k == expkind.VTRUE) ? 1 : 0);
				e.k = expkind.VK;
				return RKASK(e.u.s.info);
			  }
			  else break;
			}
			case expkind.VK: {
			  if (e.u.s.info <= MAXINDEXRK)  /* constant fit in argC? */
				return RKASK(e.u.s.info);
			  else break;
			}
			default: break;
		  }
		  /* not a constant in the right range: put it in a register */
		  return LuaKExp2AnyReg(fs, e);
		}


		public static void LuaKStoreVar (FuncState fs, expdesc var, expdesc ex) {
		  switch (var.k) {
			case expkind.VLOCAL: {
			  FreeExp(fs, ex);
			  Exp2Reg(fs, ex, var.u.s.info);
			  return;
			}
			case expkind.VUPVAL: {
			  int e = LuaKExp2AnyReg(fs, ex);
			  LuaKCodeABC(fs, OpCode.OP_SETUPVAL, e, var.u.s.info, 0);
			  break;
			}
			case expkind.VGLOBAL: {
			  int e = LuaKExp2AnyReg(fs, ex);
			  LuaKCodeABx(fs, OpCode.OP_SETGLOBAL, e, var.u.s.info);
			  break;
			}
			case expkind.VINDEXED: {
			  int e = LuaKExp2RK(fs, ex);
			  LuaKCodeABC(fs, OpCode.OP_SETTABLE, var.u.s.info, var.u.s.aux, e);
			  break;
			}
			default: {
			  LuaAssert(0);  /* invalid var kind to store */
			  break;
			}
		  }
		  FreeExp(fs, ex);
		}


		public static void LuaKSelf (FuncState fs, expdesc e, expdesc key) {
		  int func;
		  LuaKExp2AnyReg(fs, e);
		  FreeExp(fs, e);
		  func = fs.freereg;
		  LuaKReserveRegs(fs, 2);
		  LuaKCodeABC(fs, OpCode.OP_SELF, func, e.u.s.info, LuaKExp2RK(fs, key));
		  FreeExp(fs, key);
		  e.u.s.info = func;
		  e.k = expkind.VNONRELOC;
		}


		private static void InvertJump (FuncState fs, expdesc e) {
		  InstructionPtr pc = GetJumpControl(fs, e.u.s.info);
		  LuaAssert(testTMode(GET_OPCODE(pc[0])) != 0 && GET_OPCODE(pc[0]) != OpCode.OP_TESTSET &&
												   GET_OPCODE(pc[0]) != OpCode.OP_TEST);
		  SETARG_A(pc, (GETARG_A(pc[0]) == 0) ? 1 : 0);
		}


		private static int JumpOnCond (FuncState fs, expdesc e, int cond) {
		  if (e.k == expkind.VRELOCABLE) {
			InstructionPtr ie = GetCode(fs, e);
			if (GET_OPCODE(ie) == OpCode.OP_NOT) {
			  fs.pc--;  /* remove previous OpCode.OP_NOT */
			  return CondJump(fs, OpCode.OP_TEST, GETARG_B(ie), 0, (cond==0) ? 1 : 0);
			}
			/* else go through */
		  }
		  Discharge2AnyReg(fs, e);
		  FreeExp(fs, e);
		  return CondJump(fs, OpCode.OP_TESTSET, NO_REG, e.u.s.info, cond);
		}


		public static void LuaKGoIfTrue (FuncState fs, expdesc e) {
		  int pc;  /* pc of last jump */
		  LuaKDischargeVars(fs, e);
		  switch (e.k) {
			case expkind.VK: case expkind.VKNUM: case expkind.VTRUE: {
			  pc = NO_JUMP;  /* always true; do nothing */
			  break;
			}
			case expkind.VJMP: {
			  InvertJump(fs, e);
			  pc = e.u.s.info;
			  break;
			}
			default: {
			  pc = JumpOnCond(fs, e, 0);
			  break;
			}
		  }
		  LuaKConcat(fs, ref e.f, pc);  /* insert last jump in `f' list */
		  LuaKPatchToHere(fs, e.t);
		  e.t = NO_JUMP;
		}


		private static void LuaKGoIFalse (FuncState fs, expdesc e) {
		  int pc;  /* pc of last jump */
		  LuaKDischargeVars(fs, e);
		  switch (e.k) {
			case expkind.VNIL: case expkind.VFALSE: {
			  pc = NO_JUMP;  /* always false; do nothing */
			  break;
			}
			case expkind.VJMP: {
			  pc = e.u.s.info;
			  break;
			}
			default: {
			  pc = JumpOnCond(fs, e, 1);
			  break;
			}
		  }
		  LuaKConcat(fs, ref e.t, pc);  /* insert last jump in `t' list */
		  LuaKPatchToHere(fs, e.f);
		  e.f = NO_JUMP;
		}


		private static void CodeNot (FuncState fs, expdesc e) {
		  LuaKDischargeVars(fs, e);
		  switch (e.k) {
			case expkind.VNIL: case expkind.VFALSE: {
				e.k = expkind.VTRUE;
			  break;
			}
			case expkind.VK: case expkind.VKNUM: case expkind.VTRUE: {
			  e.k = expkind.VFALSE;
			  break;
			}
			case expkind.VJMP: {
			  InvertJump(fs, e);
			  break;
			}
			case expkind.VRELOCABLE:
			case expkind.VNONRELOC: {
			  Discharge2AnyReg(fs, e);
			  FreeExp(fs, e);
			  e.u.s.info = LuaKCodeABC(fs, OpCode.OP_NOT, 0, e.u.s.info, 0);
			  e.k = expkind.VRELOCABLE;
			  break;
			}
			default: {
			  LuaAssert(0);  /* cannot happen */
			  break;
			}
		  }
		  /* interchange true and false lists */
		  { int temp = e.f; e.f = e.t; e.t = temp; }
		  RemoveValues(fs, e.f);
		  RemoveValues(fs, e.t);
		}


		public static void LuaKIndexed (FuncState fs, expdesc t, expdesc k) {
		  t.u.s.aux = LuaKExp2RK(fs, k);
		  t.k = expkind.VINDEXED;
		}


		private static int ConstFolding (OpCode op, expdesc e1, expdesc e2) {
		  LuaNumberType v1, v2, r;
		  if ((IsNumeral(e1)==0) || (IsNumeral(e2)==0)) return 0;
		  v1 = e1.u.nval;
		  v2 = e2.u.nval;
		  switch (op) {
			case OpCode.OP_ADD: r = luai_numadd(v1, v2); break;
			case OpCode.OP_SUB: r = luai_numsub(v1, v2); break;
			case OpCode.OP_MUL: r = luai_nummul(v1, v2); break;
			case OpCode.OP_DIV:
			  if (v2 == 0) return 0;  /* do not attempt to divide by 0 */
			  r = luai_numdiv(v1, v2); break;
			case OpCode.OP_MOD:
			  if (v2 == 0) return 0;  /* do not attempt to divide by 0 */
			  r = luai_nummod(v1, v2); break;
			case OpCode.OP_POW: r = luai_numpow(v1, v2); break;
			case OpCode.OP_UNM: r = luai_numunm(v1); break;
			case OpCode.OP_LEN: return 0;  /* no constant folding for 'len' */
			default: LuaAssert(0); r = 0; break;
		  }
		  if (luai_numisnan(r)) return 0;  /* do not attempt to produce NaN */
		  e1.u.nval = r;
		  return 1;
		}


		private static void CodeArith (FuncState fs, OpCode op, expdesc e1, expdesc e2) {
		  if (ConstFolding(op, e1, e2) != 0)
			return;
		  else {
			int o2 = (op != OpCode.OP_UNM && op != OpCode.OP_LEN) ? LuaKExp2RK(fs, e2) : 0;
			int o1 = LuaKExp2RK(fs, e1);
			if (o1 > o2) {
			  FreeExp(fs, e1);
			  FreeExp(fs, e2);
			}
			else {
			  FreeExp(fs, e2);
			  FreeExp(fs, e1);
			}
			e1.u.s.info = LuaKCodeABC(fs, op, 0, o1, o2);
			e1.k = expkind.VRELOCABLE;
		  }
		}


		private static void CodeComp (FuncState fs, OpCode op, int cond, expdesc e1,
																  expdesc e2) {
		  int o1 = LuaKExp2RK(fs, e1);
		  int o2 = LuaKExp2RK(fs, e2);
		  FreeExp(fs, e2);
		  FreeExp(fs, e1);
		  if (cond == 0 && op != OpCode.OP_EQ) {
			int temp;  /* exchange args to replace by `<' or `<=' */
			temp = o1; o1 = o2; o2 = temp;  /* o1 <==> o2 */
			cond = 1;
		  }
		  e1.u.s.info = CondJump(fs, op, cond, o1, o2);
		  e1.k = expkind.VJMP;
		}


		public static void LuaKPrefix (FuncState fs, UnOpr op, expdesc e) {
		  expdesc e2 = new expdesc();
		  e2.t = e2.f = NO_JUMP; e2.k = expkind.VKNUM; e2.u.nval = 0;
		  switch (op) {
			case UnOpr.OPR_MINUS: {
			  if (IsNumeral(e)==0)
				LuaKExp2AnyReg(fs, e);  /* cannot operate on non-numeric constants */
			  CodeArith(fs, OpCode.OP_UNM, e, e2);
			  break;
			}
			case UnOpr.OPR_NOT: CodeNot(fs, e); break;
			case UnOpr.OPR_LEN: {
			  LuaKExp2AnyReg(fs, e);  /* cannot operate on constants */
			  CodeArith(fs, OpCode.OP_LEN, e, e2);
			  break;
			}
			default: LuaAssert(0); break;
		  }
		}


		public static void LuaKInfix (FuncState fs, BinOpr op, expdesc v) {
		  switch (op) {
			case BinOpr.OPR_AND: {
			  LuaKGoIfTrue(fs, v);
			  break;
			}
			case BinOpr.OPR_OR: {
			  LuaKGoIFalse(fs, v);
			  break;
			}
			case BinOpr.OPR_CONCAT: {
			  LuaKExp2NextReg(fs, v);  /* operand must be on the `stack' */
			  break;
			}
			case BinOpr.OPR_ADD: case BinOpr.OPR_SUB: case BinOpr.OPR_MUL: case BinOpr.OPR_DIV:
			case BinOpr.OPR_MOD: case BinOpr.OPR_POW: {
			  if ((IsNumeral(v)==0)) LuaKExp2RK(fs, v);
			  break;
			}
			default: {
			  LuaKExp2RK(fs, v);
			  break;
			}
		  }
		}


		public static void LuaKPosFix (FuncState fs, BinOpr op, expdesc e1, expdesc e2) {
		  switch (op) {
			case BinOpr.OPR_AND: {
			  LuaAssert(e1.t == NO_JUMP);  /* list must be closed */
			  LuaKDischargeVars(fs, e2);
			  LuaKConcat(fs, ref e2.f, e1.f);
			  e1.Copy(e2);
			  break;
			}
			case BinOpr.OPR_OR: {
			  LuaAssert(e1.f == NO_JUMP);  /* list must be closed */
			  LuaKDischargeVars(fs, e2);
			  LuaKConcat(fs, ref e2.t, e1.t);
			  e1.Copy(e2);
			  break;
			}
			case BinOpr.OPR_CONCAT: {
			  LuaKExp2Val(fs, e2);
			  if (e2.k == expkind.VRELOCABLE && GET_OPCODE(GetCode(fs, e2)) == OpCode.OP_CONCAT) {
				LuaAssert(e1.u.s.info == GETARG_B(GetCode(fs, e2))-1);
				FreeExp(fs, e1);
				SETARG_B(GetCode(fs, e2), e1.u.s.info);
				e1.k = expkind.VRELOCABLE; e1.u.s.info = e2.u.s.info;
			  }
			  else {
				LuaKExp2NextReg(fs, e2);  /* operand must be on the 'stack' */
				CodeArith(fs, OpCode.OP_CONCAT, e1, e2);
			  }
			  break;
			}
			case BinOpr.OPR_ADD: CodeArith(fs, OpCode.OP_ADD, e1, e2); break;
			case BinOpr.OPR_SUB: CodeArith(fs, OpCode.OP_SUB, e1, e2); break;
			case BinOpr.OPR_MUL: CodeArith(fs, OpCode.OP_MUL, e1, e2); break;
			case BinOpr.OPR_DIV: CodeArith(fs, OpCode.OP_DIV, e1, e2); break;
			case BinOpr.OPR_MOD: CodeArith(fs, OpCode.OP_MOD, e1, e2); break;
			case BinOpr.OPR_POW: CodeArith(fs, OpCode.OP_POW, e1, e2); break;
			case BinOpr.OPR_EQ: CodeComp(fs, OpCode.OP_EQ, 1, e1, e2); break;
			case BinOpr.OPR_NE: CodeComp(fs, OpCode.OP_EQ, 0, e1, e2); break;
			case BinOpr.OPR_LT: CodeComp(fs, OpCode.OP_LT, 1, e1, e2); break;
			case BinOpr.OPR_LE: CodeComp(fs, OpCode.OP_LE, 1, e1, e2); break;
			case BinOpr.OPR_GT: CodeComp(fs, OpCode.OP_LT, 0, e1, e2); break;
			case BinOpr.OPR_GE: CodeComp(fs, OpCode.OP_LE, 0, e1, e2); break;
			default: LuaAssert(0); break;
		  }
		}


		public static void LuaKFixLine (FuncState fs, int line) {
		  fs.f.lineinfo[fs.pc - 1] = line;
		}


		private static int LuaKCode (FuncState fs, int i, int line) {			
		  Proto f = fs.f;
		  DischargeJPC(fs);  /* `pc' will change */
		  /* put new instruction in code array */
		  LuaMGrowVector(fs.L, ref f.code, fs.pc, ref f.sizecode,
						  MAXINT, "code size overflow");
		  f.code[fs.pc] = (uint)i;
		  /* save corresponding line information */
		  LuaMGrowVector(fs.L, ref f.lineinfo, fs.pc, ref f.sizelineinfo,
						  MAXINT, "code size overflow");
		  f.lineinfo[fs.pc] = line;		  
		  return fs.pc++;
		}


		public static int LuaKCodeABC (FuncState fs, OpCode o, int a, int b, int c) {
		  LuaAssert(getOpMode(o) == OpMode.iABC);
		  LuaAssert(getBMode(o) != OpArgMask.OpArgN || b == 0);
		  LuaAssert(getCMode(o) != OpArgMask.OpArgN || c == 0);
		  return LuaKCode(fs, CREATE_ABC(o, a, b, c), fs.ls.lastline);
		}


		public static int LuaKCodeABx (FuncState fs, OpCode o, int a, int bc) {			
		  LuaAssert(getOpMode(o) == OpMode.iABx || getOpMode(o) == OpMode.iAsBx);
		  LuaAssert(getCMode(o) == OpArgMask.OpArgN);
		  return LuaKCode(fs, CREATE_ABx(o, a, bc), fs.ls.lastline);
		}

		public static void LuaKSetList (FuncState fs, int base_, int nelems, int tostore) {
		  int c =  (nelems - 1)/LFIELDS_PER_FLUSH + 1;
		  int b = (tostore == LUA_MULTRET) ? 0 : tostore;
		  LuaAssert(tostore != 0);
		  if (c <= MAXARG_C)
			LuaKCodeABC(fs, OpCode.OP_SETLIST, base_, b, c);
		  else {
			  LuaKCodeABC(fs, OpCode.OP_SETLIST, base_, b, 0);
			LuaKCode(fs, c, fs.ls.lastline);
		  }
		  fs.freereg = base_ + 1;  /* free registers with list values */
		}

	}
}

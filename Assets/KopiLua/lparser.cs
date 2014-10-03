/*
** $Id: lparser.c,v 2.42.1.3 2007/12/28 15:32:23 roberto Exp $
** Lua Parser
** See Copyright Notice in lua.h
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace KopiLua
{
	using lu_byte = System.Byte;
	using lua_Number = System.Double;
	using ZIO = Lua.Zio;

	public partial class Lua
	{
		/*
		** Expression descriptor
		*/

		public enum expkind {
		  VVOID,	/* no value */
		  VNIL,
		  VTRUE,
		  VFALSE,
		  VK,		/* info = index of constant in `k' */
		  VKNUM,	/* nval = numerical value */
		  VLOCAL,	/* info = local register */
		  VUPVAL,       /* info = index of upvalue in `upvalues' */
		  VGLOBAL,	/* info = index of table; aux = index of global name in `k' */
		  VINDEXED,	/* info = table register; aux = index register (or `k') */
		  VJMP,		/* info = instruction pc */
		  VRELOCABLE,	/* info = instruction pc */
		  VNONRELOC,	/* info = result register */
		  VCALL,	/* info = instruction pc */
		  VVARARG	/* info = instruction pc */
		};

	

		public class expdesc {

			public void Copy(expdesc e)
			{
				this.k = e.k;
				this.u.Copy(e.u);
				this.t = e.t;
				this.f = e.f;
			}

			public expkind k;

#if !UNITY_3D
			[CLSCompliantAttribute(false)]
#endif
			public class _u
			{
				public void Copy(_u u)
				{
					this.s.Copy(u.s);
					this.nval = u.nval;
				}

#if !UNITY_3D
				[CLSCompliantAttribute(false)]
#endif
				public class _s
				{
					public void Copy(_s s)
					{
						this.info = s.info;
						this.aux = s.aux;
					}
					public int info, aux;
				};
			    public _s s = new _s();
				public lua_Number nval;
			};

#if !UNITY_3D
			[CLSCompliantAttribute(false)]
#endif
			public _u u = new _u();

		  public int t;  /* patch list of `exit when true' */
		  public int f;  /* patch list of `exit when false' */
		};


		public class upvaldesc {
		  public lu_byte k;
		  public lu_byte info;
		};


		/* state needed to generate code for a given function */
		public class FuncState {
		  public FuncState()
		  {
			  for (int i=0; i<this.upvalues.Length; i++)
				  this.upvalues[i] = new upvaldesc();
		  }

		  public Proto f;  /* current function header */
		  public Table h;  /* table to find (and reuse) elements in `k' */
		  public FuncState prev;  /* enclosing function */
		  public LexState ls;  /* lexical state */
		  public LuaState L;  /* copy of the Lua state */
		  public BlockCnt bl;  /* chain of current blocks */
		  public int pc;  /* next position to code (equivalent to `ncode') */
		  public int lasttarget;   /* `pc' of last `jump target' */
		  public int jpc;  /* list of pending jumps to `pc' */
		  public int freereg;  /* first free register */
		  public int nk;  /* number of elements in `k' */
		  public int np;  /* number of elements in `p' */
		  public short nlocvars;  /* number of elements in `locvars' */
		  public lu_byte nactvar;  /* number of active local variables */
		  public upvaldesc[] upvalues = new upvaldesc[LUAI_MAXUPVALUES];  /* upvalues */
#if !UNITY_3D
          [CLSCompliantAttribute(false)]
#endif
		  public ushort[] actvar = new ushort[LUAI_MAXVARS];  /* declared-variable stack */
		};


		public static int hasmultret(expkind k)		{return ((k) == expkind.VCALL || (k) == expkind.VVARARG) ? 1 : 0;}

		public static LocVar getlocvar(FuncState fs, int i)	{return fs.f.locvars[fs.actvar[i]];}

		public static void luaY_checklimit(FuncState fs, int v, int l, CharPtr m) { if ((v) > (l)) errorlimit(fs, l, m); }


		/*
		** nodes for block list (list of active blocks)
		*/
		public class BlockCnt {
		  public BlockCnt previous;  /* chain */
		  public int breaklist;  /* list of jumps out of this loop */
		  public lu_byte nactvar;  /* # active locals outside the breakable structure */
		  public lu_byte upval;  /* true if some variable in the block is an upvalue */
		  public lu_byte isbreakable;  /* true if `block' is a loop */
		};



		private static void anchor_token (LexState ls) {
		  if (ls.t.token == (int)RESERVED.TK_NAME || ls.t.token == (int)RESERVED.TK_STRING) {
			TString ts = ls.t.seminfo.ts;
			LuaXNewString(ls, GetStr(ts), ts.tsv.len);
		  }
		}


		private static void error_expected (LexState ls, int token) {
		  LuaXSyntaxError(ls,
			  LuaOPushFString(ls.L, LUA_QS + " expected", LuaXTokenToString(ls, token)));
		}


		private static void errorlimit (FuncState fs, int limit, CharPtr what) {
		  CharPtr msg = (fs.f.linedefined == 0) ?
			LuaOPushFString(fs.L, "main function has more than %d %s", limit, what) :
			LuaOPushFString(fs.L, "function at line %d has more than %d %s",
									fs.f.linedefined, limit, what);
		  LuaXLexError(fs.ls, msg, 0);
		}


		private static int testnext (LexState ls, int c) {
		  if (ls.t.token == c) {
			LuaXNext(ls);
			return 1;
		  }
		  else return 0;
		}


		private static void check (LexState ls, int c) {
		  if (ls.t.token != c)
			error_expected(ls, c);
		}

		private static void checknext (LexState ls, int c) {
		  check(ls, c);
		  LuaXNext(ls);
		}


		public static void check_condition(LexState ls, bool c, CharPtr msg)	{
			if (!(c)) LuaXSyntaxError(ls, msg);
		}

		private static void check_match (LexState ls, int what, int who, int where) {
		  if (testnext(ls, what)==0) {
			if (where == ls.linenumber)
			  error_expected(ls, what);
			else {
			  LuaXSyntaxError(ls, LuaOPushFString(ls.L,
					 LUA_QS + " expected (to close " + LUA_QS + " at line %d)",
					  LuaXTokenToString(ls, what), LuaXTokenToString(ls, who), where));
			}
		  }
		}

		private static TString str_checkname (LexState ls) {
		  TString ts;
		  check(ls, (int)RESERVED.TK_NAME);
		  ts = ls.t.seminfo.ts;
		  LuaXNext(ls);
		  return ts;
		}


		private static void init_exp (expdesc e, expkind k, int i) {
		  e.f = e.t = NO_JUMP;
		  e.k = k;
		  e.u.s.info = i;
		}


		private static void codestring (LexState ls, expdesc e, TString s) {
			init_exp(e, expkind.VK, LuaKStringK(ls.fs, s));
		}


		private static void checkname(LexState ls, expdesc e) {
		  codestring(ls, e, str_checkname(ls));
		}


		private static int registerlocalvar (LexState ls, TString varname) {
		  FuncState fs = ls.fs;
		  Proto f = fs.f;
		  int oldsize = f.sizelocvars;
		  LuaMGrowVector(ls.L, ref f.locvars, fs.nlocvars, ref f.sizelocvars,
						  (int)SHRT_MAX, "too many local variables");
		  while (oldsize < f.sizelocvars) f.locvars[oldsize++].varname = null;
		  f.locvars[fs.nlocvars].varname = varname;
		  LuaCObjBarrier(ls.L, f, varname);
		  return fs.nlocvars++;
		}


		public static void new_localvarliteral(LexState ls, CharPtr v, int n) {
			new_localvar(ls, LuaXNewString(ls, "" + v, (uint)(v.chars.Length - 1)), n);
		}


		private static void new_localvar (LexState ls, TString name, int n) {
		  FuncState fs = ls.fs;
		  luaY_checklimit(fs, fs.nactvar+n+1, LUAI_MAXVARS, "local variables");
		  fs.actvar[fs.nactvar+n] = (ushort)registerlocalvar(ls, name);
		}


		private static void adjustlocalvars (LexState ls, int nvars) {
		  FuncState fs = ls.fs;
		  fs.nactvar = CastByte(fs.nactvar + nvars);
		  for (; nvars!=0; nvars--) {
			getlocvar(fs, fs.nactvar - nvars).startpc = fs.pc;
		  }
		}


		private static void removevars (LexState ls, int tolevel) {
		  FuncState fs = ls.fs;
		  while (fs.nactvar > tolevel)
			getlocvar(fs, --fs.nactvar).endpc = fs.pc;
		}


		private static int indexupvalue (FuncState fs, TString name, expdesc v) {
		  int i;
		  Proto f = fs.f;
		  int oldsize = f.sizeupvalues;
		  for (i=0; i<f.nups; i++) {
			if ((int)fs.upvalues[i].k == (int)v.k && fs.upvalues[i].info == v.u.s.info) {
			  LuaAssert(f.upvalues[i] == name);
			  return i;
			}
		  }
		  /* new one */
		  luaY_checklimit(fs, f.nups + 1, LUAI_MAXUPVALUES, "upvalues");
		  LuaMGrowVector(fs.L, ref f.upvalues, f.nups, ref f.sizeupvalues, MAXINT, "");
		  while (oldsize < f.sizeupvalues) f.upvalues[oldsize++] = null;
		  f.upvalues[f.nups] = name;
		  LuaCObjBarrier(fs.L, f, name);
		  LuaAssert(v.k == expkind.VLOCAL || v.k == expkind.VUPVAL);
		  fs.upvalues[f.nups].k = CastByte(v.k);
		  fs.upvalues[f.nups].info = CastByte(v.u.s.info);
		  return f.nups++;
		}


		private static int searchvar (FuncState fs, TString n) {
		  int i;
		  for (i=fs.nactvar-1; i >= 0; i--) {
			if (n == getlocvar(fs, i).varname)
			  return i;
		  }
		  return -1;  /* not found */
		}


		private static void markupval (FuncState fs, int level) {
		  BlockCnt bl = fs.bl;
		  while ((bl!=null) && bl.nactvar > level) bl = bl.previous;
		  if (bl != null) bl.upval = 1;
		}


		private static expkind singlevaraux(FuncState fs, TString n, expdesc var, int base_)
		{
		  if (fs == null) {  /* no more levels? */
			init_exp(var, expkind.VGLOBAL, NO_REG);  /* default is global variable */
			return expkind.VGLOBAL;
		  }
		  else {
			int v = searchvar(fs, n);  /* look up at current level */
			if (v >= 0) {
			  init_exp(var, expkind.VLOCAL, v);
			  if (base_==0)
				markupval(fs, v);  /* local will be used as an upval */
			  return expkind.VLOCAL;
			}
			else {  /* not found at current level; try upper one */
			  if (singlevaraux(fs.prev, n, var, 0) == expkind.VGLOBAL)
				  return expkind.VGLOBAL;
			  var.u.s.info = indexupvalue(fs, n, var);  /* else was LOCAL or UPVAL */
			  var.k = expkind.VUPVAL;  /* upvalue in this level */
			  return expkind.VUPVAL;
			}
		  }
		}


		private static void singlevar (LexState ls, expdesc var) {
		  TString varname = str_checkname(ls);
		  FuncState fs = ls.fs;
		  if (singlevaraux(fs, varname, var, 1) == expkind.VGLOBAL)
			var.u.s.info = LuaKStringK(fs, varname);  /* info points to global name */
		}


		private static void adjust_assign (LexState ls, int nvars, int nexps, expdesc e) {
		  FuncState fs = ls.fs;
		  int extra = nvars - nexps;
		  if (hasmultret(e.k) != 0) {
			extra++;  /* includes call itself */
			if (extra < 0) extra = 0;
			LuaKSetReturns(fs, e, extra);  /* last exp. provides the difference */
			if (extra > 1) LuaKReserveRegs(fs, extra-1);
		  }
		  else {
			if (e.k != expkind.VVOID) LuaKExp2NextReg(fs, e);  /* close last expression */
			if (extra > 0) {
			  int reg = fs.freereg;
			  LuaKReserveRegs(fs, extra);
			  LuaKNil(fs, reg, extra);
			}
		  }
		}


		private static void enterlevel (LexState ls) {
		  if (++ls.L.nCcalls > LUAI_MAXCCALLS)
			LuaXLexError(ls, "chunk has too many syntax levels", 0);
		}


		private static void leavelevel(LexState ls) { ls.L.nCcalls--; }


		private static void enterblock (FuncState fs, BlockCnt bl, lu_byte isbreakable) {
		  bl.breaklist = NO_JUMP;
		  bl.isbreakable = isbreakable;
		  bl.nactvar = fs.nactvar;
		  bl.upval = 0;
		  bl.previous = fs.bl;
		  fs.bl = bl;
		  LuaAssert(fs.freereg == fs.nactvar);
		}


		private static void leaveblock (FuncState fs) {
		  BlockCnt bl = fs.bl;
		  fs.bl = bl.previous;
		  removevars(fs.ls, bl.nactvar);
		  if (bl.upval != 0)
			LuaKCodeABC(fs, OpCode.OP_CLOSE, bl.nactvar, 0, 0);
		  /* a block either controls scope or breaks (never both) */
		  LuaAssert((bl.isbreakable==0) || (bl.upval==0));
		  LuaAssert(bl.nactvar == fs.nactvar);
		  fs.freereg = fs.nactvar;  /* free registers */
		  LuaKPatchToHere(fs, bl.breaklist);
		}


		private static void pushclosure (LexState ls, FuncState func, expdesc v) {
		  FuncState fs = ls.fs;
		  Proto f = fs.f;
		  int oldsize = f.sizep;
		  int i;
		  LuaMGrowVector(ls.L, ref f.p, fs.np, ref f.sizep, 
						  MAXARG_Bx, "constant table overflow");
		  while (oldsize < f.sizep) f.p[oldsize++] = null;
		  f.p[fs.np++] = func.f;
		  LuaCObjBarrier(ls.L, f, func.f);
		  init_exp(v, expkind.VRELOCABLE, LuaKCodeABx(fs, OpCode.OP_CLOSURE, 0, fs.np - 1));
		  for (i=0; i<func.f.nups; i++) {
			OpCode o = ((int)func.upvalues[i].k == (int)expkind.VLOCAL) ? OpCode.OP_MOVE : OpCode.OP_GETUPVAL;
			LuaKCodeABC(fs, o, 0, func.upvalues[i].info, 0);
		  }
		}


		private static void open_func (LexState ls, FuncState fs) {
		  LuaState L = ls.L;
		  Proto f = LuaFNewProto(L);
		  fs.f = f;
		  fs.prev = ls.fs;  /* linked list of funcstates */
		  fs.ls = ls;
		  fs.L = L;
		  ls.fs = fs;
		  fs.pc = 0;
		  fs.lasttarget = -1;
		  fs.jpc = NO_JUMP;
		  fs.freereg = 0;
		  fs.nk = 0;
		  fs.np = 0;
		  fs.nlocvars = 0;
		  fs.nactvar = 0;
		  fs.bl = null;
		  f.source = ls.source;
		  f.maxstacksize = 2;  /* registers 0/1 are always valid */
		  fs.h = luaH_new(L, 0, 0);
		  /* anchor table of constants and prototype (to avoid being collected) */
		  SetHValue2S(L, L.top, fs.h);
		  IncrTop(L);
		  SetPTValue2S(L, L.top, f);
		  IncrTop(L);
		}

		private static void close_func (LexState ls) {
		  LuaState L = ls.L;
		  FuncState fs = ls.fs;
		  Proto f = fs.f;
		  removevars(ls, 0);
		  LuaKRet(fs, 0, 0);  /* final return */
		  LuaMReallocVector(L, ref f.code, f.sizecode, fs.pc/*, typeof(Instruction)*/);
		  f.sizecode = fs.pc;
		  LuaMReallocVector(L, ref f.lineinfo, f.sizelineinfo, fs.pc/*, typeof(int)*/);
		  f.sizelineinfo = fs.pc;
		  LuaMReallocVector(L, ref f.k, f.sizek, fs.nk/*, TValue*/);
		  f.sizek = fs.nk;
		  LuaMReallocVector(L, ref f.p, f.sizep, fs.np/*, Proto*/);		  
		  f.sizep = fs.np;
		  for (int i = 0; i < f.p.Length; i++)
		  {
			  f.p[i].protos = f.p;
			  f.p[i].index = i;
		  }
		  LuaMReallocVector(L, ref f.locvars, f.sizelocvars, fs.nlocvars/*, LocVar*/);
		  f.sizelocvars = fs.nlocvars;
		  LuaMReallocVector(L, ref f.upvalues, f.sizeupvalues, f.nups/*, TString*/);
		  f.sizeupvalues = f.nups;
		  LuaAssert(LuaGCheckCode(f));
		  LuaAssert(fs.bl == null);
		  ls.fs = fs.prev;
		  /* last token read was anchored in defunct function; must reanchor it */
		  if (fs!=null) anchor_token(ls);
		  L.top -= 2;  /* remove table and prototype from the stack */
		}


		public static Proto luaY_parser (LuaState L, ZIO z, Mbuffer buff, CharPtr name) {
		  LexState lexstate = new LexState();
		  FuncState funcstate = new FuncState();
		  lexstate.buff = buff;
		  LuaXSetInput(L, lexstate, z, luaS_new(L, name));
		  open_func(lexstate, funcstate);
		  funcstate.f.is_vararg = VARARG_ISVARARG;  /* main func. is always vararg */
		  LuaXNext(lexstate);  /* read first token */
 		  System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
		  chunk(lexstate);
		  check(lexstate, (int)RESERVED.TK_EOS);
		  close_func(lexstate);
		  LuaAssert(funcstate.prev == null);
		  LuaAssert(funcstate.f.nups == 0);
		  LuaAssert(lexstate.fs == null);
		  return funcstate.f;
		}



		/*============================================================*/
		/* GRAMMAR RULES */
		/*============================================================*/


		private static void field (LexState ls, expdesc v) {
		  /* field . ['.' | ':'] NAME */
		  FuncState fs = ls.fs;
		  expdesc key = new expdesc();
		  LuaKExp2AnyReg(fs, v);
		  LuaXNext(ls);  /* skip the dot or colon */
		  checkname(ls, key);
		  LuaKIndexed(fs, v, key);
		}


		private static void yindex (LexState ls, expdesc v) {
		  /* index . '[' expr ']' */
		  LuaXNext(ls);  /* skip the '[' */
		  expr(ls, v);
		  LuaKExp2Val(ls.fs, v);
		  checknext(ls, ']');
		}


		/*
		** {======================================================================
		** Rules for Constructors
		** =======================================================================
		*/


		public class ConsControl {
		  public expdesc v = new expdesc();  /* last list item read */
		  public expdesc t;  /* table descriptor */
		  public int nh;  /* total number of `record' elements */
		  public int na;  /* total number of array elements */
		  public int tostore;  /* number of array elements pending to be stored */
		};


		private static void recfield (LexState ls, ConsControl cc) {
		  /* recfield . (NAME | `['exp1`]') = exp1 */
		  FuncState fs = ls.fs;
		  int reg = ls.fs.freereg;
		  expdesc key = new expdesc(), val = new expdesc();
		  int rkkey;
		  if (ls.t.token == (int)RESERVED.TK_NAME) {
			luaY_checklimit(fs, cc.nh, MAXINT, "items in a constructor");
			checkname(ls, key);
		  }
		  else  /* ls.t.token == '[' */
			yindex(ls, key);
		  cc.nh++;
		  checknext(ls, '=');
		  rkkey = LuaKExp2RK(fs, key);
		  expr(ls, val);
		  LuaKCodeABC(fs, OpCode.OP_SETTABLE, cc.t.u.s.info, rkkey, LuaKExp2RK(fs, val));
		  fs.freereg = reg;  /* free registers */
		}


		private static void closelistfield (FuncState fs, ConsControl cc) {
		  if (cc.v.k == expkind.VVOID) return;  /* there is no list item */
		  LuaKExp2NextReg(fs, cc.v);
		  cc.v.k = expkind.VVOID;
		  if (cc.tostore == LFIELDS_PER_FLUSH) {
			LuaKSetList(fs, cc.t.u.s.info, cc.na, cc.tostore);  /* flush */
			cc.tostore = 0;  /* no more items pending */
		  }
		}


		private static void lastlistfield (FuncState fs, ConsControl cc) {
		  if (cc.tostore == 0) return;
		  if (hasmultret(cc.v.k) != 0) {
			LuaKSetMultRet(fs, cc.v);
			LuaKSetList(fs, cc.t.u.s.info, cc.na, LUA_MULTRET);
			cc.na--;  /* do not count last expression (unknown number of elements) */
		  }
		  else {
			if (cc.v.k != expkind.VVOID)
			  LuaKExp2NextReg(fs, cc.v);
			LuaKSetList(fs, cc.t.u.s.info, cc.na, cc.tostore);
		  }
		}


		private static void listfield (LexState ls, ConsControl cc) {
		  expr(ls, cc.v);
		  luaY_checklimit(ls.fs, cc.na, MAXINT, "items in a constructor");
		  cc.na++;
		  cc.tostore++;
		}


		private static void constructor (LexState ls, expdesc t) {
		  /* constructor . ?? */
		  FuncState fs = ls.fs;
		  int line = ls.linenumber;
		  int pc = LuaKCodeABC(fs, OpCode.OP_NEWTABLE, 0, 0, 0);
		  ConsControl cc = new ConsControl();
		  cc.na = cc.nh = cc.tostore = 0;
		  cc.t = t;
		  init_exp(t, expkind.VRELOCABLE, pc);
		  init_exp(cc.v, expkind.VVOID, 0);  /* no value (yet) */
		  LuaKExp2NextReg(ls.fs, t);  /* fix it at stack top (for gc) */
		  checknext(ls, '{');
		  do {
			LuaAssert(cc.v.k == expkind.VVOID || cc.tostore > 0);
			if (ls.t.token == '}') break;
			closelistfield(fs, cc);
			switch(ls.t.token) {
			  case (int)RESERVED.TK_NAME: {  /* may be listfields or recfields */
				LuaXLookAhead(ls);
				if (ls.lookahead.token != '=')  /* expression? */
				  listfield(ls, cc);
				else
				  recfield(ls, cc);
				break;
			  }
			  case '[': {  /* constructor_item . recfield */
				recfield(ls, cc);
				break;
			  }
			  default: {  /* constructor_part . listfield */
				listfield(ls, cc);
				break;
			  }
			}
		  } while ((testnext(ls, ',')!=0) || (testnext(ls, ';')!=0));
		  check_match(ls, '}', '{', line);
		  lastlistfield(fs, cc);
		  SETARG_B(new InstructionPtr(fs.f.code, pc), LuaOInt2FB((uint)cc.na)); /* set initial array size */
		  SETARG_C(new InstructionPtr(fs.f.code, pc), LuaOInt2FB((uint)cc.nh));  /* set initial table size */
		}

		/* }====================================================================== */



		private static void parlist (LexState ls) {
		  /* parlist . [ param { `,' param } ] */
		  FuncState fs = ls.fs;
		  Proto f = fs.f;
		  int nparams = 0;
		  f.is_vararg = 0;
		  if (ls.t.token != ')') {  /* is `parlist' not empty? */
			do {
			  switch (ls.t.token) {
				case (int)RESERVED.TK_NAME: {  /* param . NAME */
				  new_localvar(ls, str_checkname(ls), nparams++);
				  break;
				}
				case (int)RESERVED.TK_DOTS: {  /* param . `...' */
				  LuaXNext(ls);
		#if LUA_COMPAT_VARARG
		          /* use `arg' as default name */
		          new_localvarliteral(ls, "arg", nparams++);
		          f.is_vararg = VARARG_HASARG | VARARG_NEEDSARG;
		#endif
				  f.is_vararg |= VARARG_ISVARARG;
				  break;
				}
				default: LuaXSyntaxError(ls, "<name> or " + LUA_QL("...") + " expected"); break;
			  }
			} while ((f.is_vararg==0) && (testnext(ls, ',')!=0));
		  }
		  adjustlocalvars(ls, nparams);
		  f.numparams = CastByte(fs.nactvar - (f.is_vararg & VARARG_HASARG));
		  LuaKReserveRegs(fs, fs.nactvar);  /* reserve register for parameters */
		}


		private static void body (LexState ls, expdesc e, int needself, int line) {
		  /* body .  `(' parlist `)' chunk END */
		  FuncState new_fs = new FuncState();
		  open_func(ls, new_fs);
		  new_fs.f.linedefined = line;
		  checknext(ls, '(');
		  if (needself != 0) {
			new_localvarliteral(ls, "self", 0);
			adjustlocalvars(ls, 1);
		  }
		  parlist(ls);
		  checknext(ls, ')');
		  chunk(ls);
		  new_fs.f.lastlinedefined = ls.linenumber;
		  check_match(ls, (int)RESERVED.TK_END, (int)RESERVED.TK_FUNCTION, line);
		  close_func(ls);
		  pushclosure(ls, new_fs, e);
		}


		private static int explist1 (LexState ls, expdesc v) {
		  /* explist1 . expr { `,' expr } */
		  int n = 1;  /* at least one expression */
		  expr(ls, v);
		  while (testnext(ls, ',') != 0) {
			LuaKExp2NextReg(ls.fs, v);
			expr(ls, v);
			n++;
		  }
		  return n;
		}


		private static void funcargs (LexState ls, expdesc f) {
		  FuncState fs = ls.fs;
		  expdesc args = new expdesc();
		  int base_, nparams;
		  int line = ls.linenumber;
		  switch (ls.t.token) {
			case '(': {  /* funcargs . `(' [ explist1 ] `)' */
			  if (line != ls.lastline)
				LuaXSyntaxError(ls,"ambiguous syntax (function call x new statement)");
			  LuaXNext(ls);
			  if (ls.t.token == ')')  /* arg list is empty? */
				args.k = expkind.VVOID;
			  else {
				explist1(ls, args);
				LuaKSetMultRet(fs, args);
			  }
			  check_match(ls, ')', '(', line);
			  break;
			}
			case '{': {  /* funcargs . constructor */
			  constructor(ls, args);
			  break;
			}
			case (int)RESERVED.TK_STRING: {  /* funcargs . STRING */
			  codestring(ls, args, ls.t.seminfo.ts);
			  LuaXNext(ls);  /* must use `seminfo' before `next' */
			  break;
			}
			default: {
			  LuaXSyntaxError(ls, "function arguments expected");
			  return;
			}
		  }
		  LuaAssert(f.k == expkind.VNONRELOC);
		  base_ = f.u.s.info;  /* base_ register for call */
		  if (hasmultret(args.k) != 0)
			nparams = LUA_MULTRET;  /* open call */
		  else {
			if (args.k != expkind.VVOID)
			  LuaKExp2NextReg(fs, args);  /* close last argument */
			nparams = fs.freereg - (base_+1);
		  }
		  init_exp(f, expkind.VCALL, LuaKCodeABC(fs, OpCode.OP_CALL, base_, nparams + 1, 2));
		  LuaKFixLine(fs, line);
		  fs.freereg = base_+1;  /* call remove function and arguments and leaves
									(unless changed) one result */
		}




		/*
		** {======================================================================
		** Expression parsing
		** =======================================================================
		*/


		private static void prefixexp (LexState ls, expdesc v) {
		  /* prefixexp . NAME | '(' expr ')' */
		  switch (ls.t.token) {
			case '(': {
			  int line = ls.linenumber;
			  LuaXNext(ls);
			  expr(ls, v);
			  check_match(ls, ')', '(', line);
			  LuaKDischargeVars(ls.fs, v);
			  return;
			}
			case (int)RESERVED.TK_NAME: {
			  singlevar(ls, v);
			  return;
			}
			default: {
			  LuaXSyntaxError(ls, "unexpected symbol");
			  return;
			}
		  }
		}

		private static void primaryexp (LexState ls, expdesc v) {
		  /* primaryexp .
				prefixexp { `.' NAME | `[' exp `]' | `:' NAME funcargs | funcargs } */
		  FuncState fs = ls.fs;
		  prefixexp(ls, v);
		  for (;;) {
			switch (ls.t.token) {
			  case '.': {  /* field */
				field(ls, v);
				break;
			  }
			  case '[': {  /* `[' exp1 `]' */
				expdesc key = new expdesc();
				LuaKExp2AnyReg(fs, v);
				yindex(ls, key);
				LuaKIndexed(fs, v, key);
				break;
			  }
			  case ':': {  /* `:' NAME funcargs */
				expdesc key = new expdesc();
				LuaXNext(ls);
				checkname(ls, key);
				LuaKSelf(fs, v, key);
				funcargs(ls, v);
				break;
			  }
			  case '(': case (int)RESERVED.TK_STRING: case '{': {  /* funcargs */
				LuaKExp2NextReg(fs, v);
				funcargs(ls, v);
				break;
			  }
			  default: return;
			}
		  }
		}


		private static void simpleexp (LexState ls, expdesc v) {
		  /* simpleexp . NUMBER | STRING | NIL | true | false | ... |
						  constructor | FUNCTION body | primaryexp */
		  switch (ls.t.token) {
			case (int)RESERVED.TK_NUMBER: {
			  init_exp(v, expkind.VKNUM, 0);
			  v.u.nval = ls.t.seminfo.r;
			  break;
			}
			case (int)RESERVED.TK_STRING: {
			  codestring(ls, v, ls.t.seminfo.ts);
			  break;
			}
			case (int)RESERVED.TK_NIL: {
			  init_exp(v, expkind.VNIL, 0);
			  break;
			}
			case (int)RESERVED.TK_TRUE: {
			  init_exp(v, expkind.VTRUE, 0);
			  break;
			}
			case (int)RESERVED.TK_FALSE: {
			  init_exp(v, expkind.VFALSE, 0);
			  break;
			}
			case (int)RESERVED.TK_DOTS: {  /* vararg */
			  FuncState fs = ls.fs;
			  check_condition(ls, fs.f.is_vararg!=0,
							  "cannot use " + LUA_QL("...") + " outside a vararg function");
			  fs.f.is_vararg &= unchecked((lu_byte)(~VARARG_NEEDSARG));  /* don't need 'arg' */
			  init_exp(v, expkind.VVARARG, LuaKCodeABC(fs, OpCode.OP_VARARG, 0, 1, 0));
			  break;
			}
			case '{': {  /* constructor */
			  constructor(ls, v);
			  return;
			}
			case (int)RESERVED.TK_FUNCTION: {
			  LuaXNext(ls);
			  body(ls, v, 0, ls.linenumber);
			  return;
			}
			default: {
			  primaryexp(ls, v);
			  return;
			}
		  }
		  LuaXNext(ls);
		}


		private static UnOpr getunopr (int op) {
		  switch (op) {
			case (int)RESERVED.TK_NOT: return UnOpr.OPR_NOT;
			case '-': return UnOpr.OPR_MINUS;
			case '#': return UnOpr.OPR_LEN;
			default: return UnOpr.OPR_NOUNOPR;
		  }
		}


		private static BinOpr getbinopr (int op) {
		  switch (op) {
			case '+': return BinOpr.OPR_ADD;
			case '-': return BinOpr.OPR_SUB;
			case '*': return BinOpr.OPR_MUL;
			case '/': return BinOpr.OPR_DIV;
			case '%': return BinOpr.OPR_MOD;
			case '^': return BinOpr.OPR_POW;
			case (int)RESERVED.TK_CONCAT: return BinOpr.OPR_CONCAT;
			case (int)RESERVED.TK_NE: return BinOpr.OPR_NE;
			case (int)RESERVED.TK_EQ: return BinOpr.OPR_EQ;
			case '<': return BinOpr.OPR_LT;
			case (int)RESERVED.TK_LE: return BinOpr.OPR_LE;
			case '>': return BinOpr.OPR_GT;
			case (int)RESERVED.TK_GE: return BinOpr.OPR_GE;
			case (int)RESERVED.TK_AND: return BinOpr.OPR_AND;
			case (int)RESERVED.TK_OR: return BinOpr.OPR_OR;
			default: return BinOpr.OPR_NOBINOPR;
		  }
		}


		private class priority_ {
			public priority_(lu_byte left, lu_byte right)
			{
				this.left = left;
				this.right = right;
			}

			public lu_byte left;  /* left priority for each binary operator */
			public lu_byte right; /* right priority */
		} 

		private static priority_[] priority = {  /* ORDER OPR */

			new priority_(6, 6),
			new priority_(6, 6),
			new priority_(7, 7),
			new priority_(7, 7),
			new priority_(7, 7),				/* `+' `-' `/' `%' */

			new priority_(10, 9),
			new priority_(5, 4),				/* power and concat (right associative) */

			new priority_(3, 3),
			new priority_(3, 3),				/* equality and inequality */

			new priority_(3, 3),
			new priority_(3, 3),
			new priority_(3, 3),
			new priority_(3, 3),				/* order */

			new priority_(2, 2),
			new priority_(1, 1)					/* logical (and/or) */
		};

		public const int UNARY_PRIORITY	= 8;  /* priority for unary operators */


		/*
		** subexpr . (simpleexp | unop subexpr) { binop subexpr }
		** where `binop' is any binary operator with a priority higher than `limit'
		*/
		private static BinOpr subexpr (LexState ls, expdesc v, uint limit) {
		  BinOpr op = new BinOpr();
		  UnOpr uop = new UnOpr();
		  enterlevel(ls);
		  uop = getunopr(ls.t.token);
		  if (uop != UnOpr.OPR_NOUNOPR) {
			LuaXNext(ls);
			subexpr(ls, v, UNARY_PRIORITY);
			LuaKPrefix(ls.fs, uop, v);
		  }
		  else simpleexp(ls, v);
		  /* expand while operators have priorities higher than `limit' */
		  op = getbinopr(ls.t.token);
		  while (op != BinOpr.OPR_NOBINOPR && priority[(int)op].left > limit)
		  {
			expdesc v2 = new expdesc();
			BinOpr nextop;
			LuaXNext(ls);
			LuaKInfix(ls.fs, op, v);
			/* read sub-expression with higher priority */
			nextop = subexpr(ls, v2, priority[(int)op].right);
			LuaKPosFix(ls.fs, op, v, v2);
			op = nextop;
		  }
		  leavelevel(ls);
		  return op;  /* return first untreated operator */
		}


		private static void expr (LexState ls, expdesc v) {
		  subexpr(ls, v, 0);
		}

		/* }==================================================================== */



		/*
		** {======================================================================
		** Rules for Statements
		** =======================================================================
		*/


		private static int block_follow (int token) {
		  switch (token) {
			case (int)RESERVED.TK_ELSE: case (int)RESERVED.TK_ELSEIF: case (int)RESERVED.TK_END:
			case (int)RESERVED.TK_UNTIL: case (int)RESERVED.TK_EOS:
			  return 1;
			default: return 0;
		  }
		}


		private static void block (LexState ls) {
		  /* block . chunk */
		  FuncState fs = ls.fs;
		  BlockCnt bl = new BlockCnt();
		  enterblock(fs, bl, 0);
		  chunk(ls);
		  LuaAssert(bl.breaklist == NO_JUMP);
		  leaveblock(fs);
		}


		/*
		** structure to chain all variables in the left-hand side of an
		** assignment
		*/
		public class LHS_assign {
		  public LHS_assign prev;
		  public expdesc v = new expdesc();  /* variable (global, local, upvalue, or indexed) */
		};


		/*
		** check whether, in an assignment to a local variable, the local variable
		** is needed in a previous assignment (to a table). If so, save original
		** local value in a safe place and use this safe copy in the previous
		** assignment.
		*/
		private static void check_conflict (LexState ls, LHS_assign lh, expdesc v) {
		  FuncState fs = ls.fs;
		  int extra = fs.freereg;  /* eventual position to save local variable */
		  int conflict = 0;
		  for (; lh!=null; lh = lh.prev) {
			if (lh.v.k == expkind.VINDEXED) {
			  if (lh.v.u.s.info == v.u.s.info) {  /* conflict? */
				conflict = 1;
				lh.v.u.s.info = extra;  /* previous assignment will use safe copy */
			  }
			  if (lh.v.u.s.aux == v.u.s.info) {  /* conflict? */
				conflict = 1;
				lh.v.u.s.aux = extra;  /* previous assignment will use safe copy */
			  }
			}
		  }
		  if (conflict != 0) {
			LuaKCodeABC(fs, OpCode.OP_MOVE, fs.freereg, v.u.s.info, 0);  /* make copy */
			LuaKReserveRegs(fs, 1);
		  }
		}


		private static void assignment (LexState ls, LHS_assign lh, int nvars) {
		  expdesc e = new expdesc();
		  check_condition(ls, expkind.VLOCAL <= lh.v.k && lh.v.k <= expkind.VINDEXED,
							  "syntax error");
		  if (testnext(ls, ',') != 0) {  /* assignment . `,' primaryexp assignment */
			LHS_assign nv = new LHS_assign();
			nv.prev = lh;
			primaryexp(ls, nv.v);
			if (nv.v.k == expkind.VLOCAL)
			  check_conflict(ls, lh, nv.v);
			luaY_checklimit(ls.fs, nvars, LUAI_MAXCCALLS - ls.L.nCcalls,
							"variables in assignment");
			assignment(ls, nv, nvars+1);
		  }
		  else {  /* assignment . `=' explist1 */
			int nexps;
			checknext(ls, '=');
			nexps = explist1(ls, e);
			if (nexps != nvars) {
			  adjust_assign(ls, nvars, nexps, e);
			  if (nexps > nvars)
				ls.fs.freereg -= nexps - nvars;  /* remove extra values */
			}
			else {
			  LuaKSetOneRet(ls.fs, e);  /* close last expression */
			  LuaKStoreVar(ls.fs, lh.v, e);
			  return;  /* avoid default */
			}
		  }
		  init_exp(e, expkind.VNONRELOC, ls.fs.freereg - 1);  /* default assignment */
		  LuaKStoreVar(ls.fs, lh.v, e);
		}


		private static int cond (LexState ls) {
		  /* cond . exp */
		  expdesc v = new expdesc();
		  expr(ls, v);  /* read condition */
		  if (v.k == expkind.VNIL) v.k = expkind.VFALSE;  /* `falses' are all equal here */
		  LuaKGoIfTrue(ls.fs, v);
		  return v.f;
		}


		private static void breakstat (LexState ls) {
		  FuncState fs = ls.fs;
		  BlockCnt bl = fs.bl;
		  int upval = 0;
		  while ((bl!=null) && (bl.isbreakable==0)) {
			upval |= bl.upval;
			bl = bl.previous;
		  }
		  if (bl==null)
			LuaXSyntaxError(ls, "no loop to break");
		  if (upval != 0)
			LuaKCodeABC(fs, OpCode.OP_CLOSE, bl.nactvar, 0, 0);
		  LuaKConcat(fs, ref bl.breaklist, LuaKJump(fs));
		}


		private static void whilestat (LexState ls, int line) {
		  /* whilestat . WHILE cond DO block END */
		  FuncState fs = ls.fs;
		  int whileinit;
		  int condexit;
		  BlockCnt bl = new BlockCnt();
		  LuaXNext(ls);  /* skip WHILE */
		  whileinit = LuaKGetLabel(fs);
		  condexit = cond(ls);
		  enterblock(fs, bl, 1);
		  checknext(ls, (int)RESERVED.TK_DO);
		  block(ls);
		  LuaKPatchList(fs, LuaKJump(fs), whileinit);
		  check_match(ls, (int)RESERVED.TK_END, (int)RESERVED.TK_WHILE, line);
		  leaveblock(fs);
		  LuaKPatchToHere(fs, condexit);  /* false conditions finish the loop */
		}


		private static void repeatstat (LexState ls, int line) {
		  /* repeatstat . REPEAT block UNTIL cond */
		  int condexit;
		  FuncState fs = ls.fs;
		  int repeat_init = LuaKGetLabel(fs);
		  BlockCnt bl1 = new BlockCnt(), bl2 = new BlockCnt();
		  enterblock(fs, bl1, 1);  /* loop block */
		  enterblock(fs, bl2, 0);  /* scope block */
		  LuaXNext(ls);  /* skip REPEAT */
		  chunk(ls);
		  check_match(ls, (int)RESERVED.TK_UNTIL, (int)RESERVED.TK_REPEAT, line);
		  condexit = cond(ls);  /* read condition (inside scope block) */
		  if (bl2.upval==0) {  /* no upvalues? */
			leaveblock(fs);  /* finish scope */
			LuaKPatchList(ls.fs, condexit, repeat_init);  /* close the loop */
		  }
		  else {  /* complete semantics when there are upvalues */
			breakstat(ls);  /* if condition then break */
			LuaKPatchToHere(ls.fs, condexit);  /* else... */
			leaveblock(fs);  /* finish scope... */
			LuaKPatchList(ls.fs, LuaKJump(fs), repeat_init);  /* and repeat */
		  }
		  leaveblock(fs);  /* finish loop */
		}


		private static int exp1 (LexState ls) {
		  expdesc e = new expdesc();
		  int k;
		  expr(ls, e);
		  k = (int)e.k;
		  LuaKExp2NextReg(ls.fs, e);
		  return k;
		}


		private static void forbody (LexState ls, int base_, int line, int nvars, int isnum) {
		  /* forbody . DO block */
		  BlockCnt bl = new BlockCnt();
		  FuncState fs = ls.fs;
		  int prep, endfor;
		  adjustlocalvars(ls, 3);  /* control variables */
		  checknext(ls, (int)RESERVED.TK_DO);
		  prep = (isnum != 0) ? LuaKCodeAsBx(fs, OpCode.OP_FORPREP, base_, NO_JUMP) : LuaKJump(fs);
		  enterblock(fs, bl, 0);  /* scope for declared variables */
		  adjustlocalvars(ls, nvars);
		  LuaKReserveRegs(fs, nvars);
		  block(ls);
		  leaveblock(fs);  /* end of scope for declared variables */
		  LuaKPatchToHere(fs, prep);
		  endfor = (isnum!=0) ? LuaKCodeAsBx(fs, OpCode.OP_FORLOOP, base_, NO_JUMP) :
							 LuaKCodeABC(fs, OpCode.OP_TFORLOOP, base_, 0, nvars);
		  LuaKFixLine(fs, line);  /* pretend that `OP_FOR' starts the loop */
		  LuaKPatchList(fs, ((isnum!=0) ? endfor : LuaKJump(fs)), prep + 1);
		}


		private static void fornum (LexState ls, TString varname, int line) {
		  /* fornum . NAME = exp1,exp1[,exp1] forbody */
		  FuncState fs = ls.fs;
		  int base_ = fs.freereg;
		  new_localvarliteral(ls, "(for index)", 0);
		  new_localvarliteral(ls, "(for limit)", 1);
		  new_localvarliteral(ls, "(for step)", 2);
		  new_localvar(ls, varname, 3);
		  checknext(ls, '=');
		  exp1(ls);  /* initial value */
		  checknext(ls, ',');
		  exp1(ls);  /* limit */
		  if (testnext(ls, ',') != 0)
			exp1(ls);  /* optional step */
		  else {  /* default step = 1 */
			LuaKCodeABx(fs, OpCode.OP_LOADK, fs.freereg, LuaKNumberK(fs, 1));
			LuaKReserveRegs(fs, 1);
		  }
		  forbody(ls, base_, line, 1, 1);
		}


		private static void forlist (LexState ls, TString indexname) {
		  /* forlist . NAME {,NAME} IN explist1 forbody */
		  FuncState fs = ls.fs;
		  expdesc e = new expdesc();
		  int nvars = 0;
		  int line;
		  int base_ = fs.freereg;
		  /* create control variables */
		  new_localvarliteral(ls, "(for generator)", nvars++);
		  new_localvarliteral(ls, "(for state)", nvars++);
		  new_localvarliteral(ls, "(for control)", nvars++);
		  /* create declared variables */
		  new_localvar(ls, indexname, nvars++);
		  while (testnext(ls, ',') != 0)
			new_localvar(ls, str_checkname(ls), nvars++);
		  checknext(ls, (int)RESERVED.TK_IN);
		  line = ls.linenumber;
		  adjust_assign(ls, 3, explist1(ls, e), e);
		  LuaKCheckStack(fs, 3);  /* extra space to call generator */
		  forbody(ls, base_, line, nvars - 3, 0);
		}


		private static void forstat (LexState ls, int line) {
		  /* forstat . FOR (fornum | forlist) END */
		  FuncState fs = ls.fs;
		  TString varname;
		  BlockCnt bl = new BlockCnt();
		  enterblock(fs, bl, 1);  /* scope for loop and control variables */
		  LuaXNext(ls);  /* skip `for' */
		  varname = str_checkname(ls);  /* first variable name */
		  switch (ls.t.token) {
			case '=': fornum(ls, varname, line); break;
			case ',':
			case (int)RESERVED.TK_IN:
				forlist(ls, varname);
				break;
			default: LuaXSyntaxError(ls, LUA_QL("=") + " or " + LUA_QL("in") + " expected"); break;
		  }
		  check_match(ls, (int)RESERVED.TK_END, (int)RESERVED.TK_FOR, line);
		  leaveblock(fs);  /* loop scope (`break' jumps to this point) */
		}


		private static int test_then_block (LexState ls) {
		  /* test_then_block . [IF | ELSEIF] cond THEN block */
		  int condexit;
		  LuaXNext(ls);  /* skip IF or ELSEIF */
		  condexit = cond(ls);
		  checknext(ls, (int)RESERVED.TK_THEN);
		  block(ls);  /* `then' part */
		  return condexit;
		}


		private static void ifstat (LexState ls, int line) {
		  /* ifstat . IF cond THEN block {ELSEIF cond THEN block} [ELSE block] END */
		  FuncState fs = ls.fs;
		  int flist;
		  int escapelist = NO_JUMP;
		  flist = test_then_block(ls);  /* IF cond THEN block */
		  while (ls.t.token == (int)RESERVED.TK_ELSEIF) {
			LuaKConcat(fs, ref escapelist, LuaKJump(fs));
			LuaKPatchToHere(fs, flist);
			flist = test_then_block(ls);  /* ELSEIF cond THEN block */
		  }
		  if (ls.t.token == (int)RESERVED.TK_ELSE) {
			LuaKConcat(fs, ref escapelist, LuaKJump(fs));
			LuaKPatchToHere(fs, flist);
			LuaXNext(ls);  /* skip ELSE (after patch, for correct line info) */
			block(ls);  /* `else' part */
		  }
		  else
			LuaKConcat(fs, ref escapelist, flist);
		  LuaKPatchToHere(fs, escapelist);
		  check_match(ls, (int)RESERVED.TK_END, (int)RESERVED.TK_IF, line);
		}


		private static void localfunc (LexState ls) {
		  expdesc v = new expdesc(), b = new expdesc();
		  FuncState fs = ls.fs;
		  new_localvar(ls, str_checkname(ls), 0);
		  init_exp(v, expkind.VLOCAL, fs.freereg);
		  LuaKReserveRegs(fs, 1);
		  adjustlocalvars(ls, 1);
		  body(ls, b, 0, ls.linenumber);
		  LuaKStoreVar(fs, v, b);
		  /* debug information will only see the variable after this point! */
		  getlocvar(fs, fs.nactvar - 1).startpc = fs.pc;
		}


		private static void localstat (LexState ls) {
		  /* stat . LOCAL NAME {`,' NAME} [`=' explist1] */
		  int nvars = 0;
		  int nexps;
		  expdesc e = new expdesc();
		  do {
			new_localvar(ls, str_checkname(ls), nvars++);
		  } while (testnext(ls, ',') != 0);
		  if (testnext(ls, '=') != 0)
			nexps = explist1(ls, e);
		  else {
			e.k = expkind.VVOID;
			nexps = 0;
		  }
		  adjust_assign(ls, nvars, nexps, e);
		  adjustlocalvars(ls, nvars);
		}


		private static int funcname (LexState ls, expdesc v) {
		  /* funcname . NAME {field} [`:' NAME] */
		  int needself = 0;
		  singlevar(ls, v);
		  while (ls.t.token == '.')
			field(ls, v);
		  if (ls.t.token == ':') {
			needself = 1;
			field(ls, v);
		  }
		  return needself;
		}


		private static void funcstat (LexState ls, int line) {
		  /* funcstat . FUNCTION funcname body */
		  int needself;
		  expdesc v = new expdesc(), b = new expdesc();
		  LuaXNext(ls);  /* skip FUNCTION */
		  needself = funcname(ls, v);
		  body(ls, b, needself, line);
		  LuaKStoreVar(ls.fs, v, b);
		  LuaKFixLine(ls.fs, line);  /* definition `happens' in the first line */
		}


		private static void exprstat (LexState ls) {
		  /* stat . func | assignment */
		  FuncState fs = ls.fs;
		  LHS_assign v = new LHS_assign();
		  primaryexp(ls, v.v);
		  if (v.v.k == expkind.VCALL)  /* stat . func */
			SETARG_C(GetCode(fs, v.v), 1);  /* call statement uses no results */
		  else {  /* stat . assignment */
			v.prev = null;
			assignment(ls, v, 1);
		  }
		}


		private static void retstat (LexState ls) {
		  /* stat . RETURN explist */
		  FuncState fs = ls.fs;
		  expdesc e = new expdesc();
		  int first, nret;  /* registers with returned values */
		  LuaXNext(ls);  /* skip RETURN */
		  if ((block_follow(ls.t.token)!=0) || ls.t.token == ';')
			first = nret = 0;  /* return no values */
		  else {
			nret = explist1(ls, e);  /* optional return values */
			if (hasmultret(e.k) != 0) {
			  LuaKSetMultRet(fs, e);
			  if (e.k == expkind.VCALL && nret == 1) {  /* tail call? */
				SET_OPCODE(GetCode(fs,e), OpCode.OP_TAILCALL);
				LuaAssert(GETARG_A(GetCode(fs,e)) == fs.nactvar);
			  }
			  first = fs.nactvar;
			  nret = LUA_MULTRET;  /* return all values */
			}
			else {
			  if (nret == 1)  /* only one single value? */
				first = LuaKExp2AnyReg(fs, e);
			  else {
				LuaKExp2NextReg(fs, e);  /* values must go to the `stack' */
				first = fs.nactvar;  /* return all `active' values */
				LuaAssert(nret == fs.freereg - first);
			  }
			}
		  }
		  LuaKRet(fs, first, nret);
		}


		private static int statement (LexState ls) {
		  int line = ls.linenumber;  /* may be needed for error messages */
		  switch (ls.t.token) {
			case (int)RESERVED.TK_IF: {  /* stat . ifstat */
			  ifstat(ls, line);
			  return 0;
			}
			case (int)RESERVED.TK_WHILE: {  /* stat . whilestat */
			  whilestat(ls, line);
			  return 0;
			}
			case (int)RESERVED.TK_DO: {  /* stat . DO block END */
			  LuaXNext(ls);  /* skip DO */
			  block(ls);
			  check_match(ls, (int)RESERVED.TK_END, (int)RESERVED.TK_DO, line);
			  return 0;
			}
			case (int)RESERVED.TK_FOR: {  /* stat . forstat */
			  forstat(ls, line);
			  return 0;
			}
			case (int)RESERVED.TK_REPEAT: {  /* stat . repeatstat */
			  repeatstat(ls, line);
			  return 0;
			}
			case (int)RESERVED.TK_FUNCTION: {
			  funcstat(ls, line);  /* stat . funcstat */
			  return 0;
			}
			case (int)RESERVED.TK_LOCAL: {  /* stat . localstat */
			  LuaXNext(ls);  /* skip LOCAL */
			  if (testnext(ls, (int)RESERVED.TK_FUNCTION) != 0)  /* local function? */
				localfunc(ls);
			  else
				localstat(ls);
			  return 0;
			}
			case (int)RESERVED.TK_RETURN: {  /* stat . retstat */
			  retstat(ls);
			  return 1;  /* must be last statement */
			}
			case (int)RESERVED.TK_BREAK: {  /* stat . breakstat */
			  LuaXNext(ls);  /* skip BREAK */
			  breakstat(ls);
			  return 1;  /* must be last statement */
			}
			default: {
			  exprstat(ls);
			  return 0;  /* to avoid warnings */
			}
		  }
		}


		private static void chunk (LexState ls) {
		  /* chunk . { stat [`;'] } */
		  int islast = 0;
		  enterlevel(ls);
		  while ((islast==0) && (block_follow(ls.t.token)==0)) {
			islast = statement(ls);
			testnext(ls, ';');
			LuaAssert(ls.fs.f.maxstacksize >= ls.fs.freereg &&
					   ls.fs.freereg >= ls.fs.nactvar);
			ls.fs.freereg = ls.fs.nactvar;  /* free registers */
		  }
		  leavelevel(ls);
		}

		/* }====================================================================== */

	}
}
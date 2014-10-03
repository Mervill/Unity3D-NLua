/*
** $Id: lobject.c,v 2.22.1.1 2007/12/27 13:02:25 roberto Exp $
** Some generic functions over Lua objects
** See Copyright Notice in lua.h
*/

using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace KopiLua
{
	using StkId = Lua.LuaTypeValue;
	using LuaByteType = System.Byte;
	using LuaNumberType = System.Double;
	using l_uacNumber = System.Double;
	using Instruction = System.UInt32;

	public partial class Lua
	{
		/* tags for values visible from Lua */
		public const int LASTTAG	= LUA_TTHREAD;

		public const int NUMTAGS	= (LASTTAG+1);


		/*
		** Extra tags for non-values
		*/
		public const int LUATPROTO	= (LASTTAG+1);
		public const int LUATUPVAL	= (LASTTAG+2);
		public const int LUATDEADKEY	= (LASTTAG+3);

		public interface ArrayElement
		{
			void SetIndex(int index);
			void SetArray(object array);
		}


		/*
		** Common Header for all collectable objects (in macro form, to be
		** included in other objects)
		*/
		public class CommonHeader
		{
			public GCObject next;
			public LuaByteType tt;
			public LuaByteType marked;
		}


		/*
		** Common header in struct form
		*/
		public class GCheader : CommonHeader {
		};




		/*
		** Union of all Lua values (in c# we use virtual data members and boxing)
		*/
		public class Value
		{

			// in the original code Value is a struct, so all assignments in the code
			// need to be replaced with a call to Copy. as it turns out, there are only
			// a couple. the vast majority of references to Value are the instance that
			// appears in the Lua.LuaTypeValue class, so if you make that a virtual data member and
			// omit the set accessor then you'll get a compiler error if anything tries
			// to set it.
			public void Copy(Value copy)
			{
				this.p = copy.p;
			}

			public GCObject gc
			{
				get {return (GCObject)this.p;}
				set {this.p = value;}
			}
			public object p;
			public LuaNumberType n
			{
				get { return (LuaNumberType)this.p; }
				set { this.p = (object)value; }
			}
			public int b
			{
				get { return (int)this.p; }
				set { this.p = (object)value; }
			}
		};


		/*
		** Tagged Values
		*/

		//#define TValuefields	Value value; int tt

		public class LuaTypeValue : ArrayElement
		{
			private LuaTypeValue[] values = null;
			private int index = -1;

			public void SetIndex(int index)
			{
				this.index = index;
			}

			public void SetArray(object array)
			{
				this.values = (LuaTypeValue[])array;
				Debug.Assert(this.values != null);
			}

			public LuaTypeValue this[int offset]
			{
				get { return this.values[this.index + offset]; }
			}

#if !UNITY_3D
			[CLSCompliantAttribute(false)]
#endif
			public LuaTypeValue this[uint offset]
			{
				get { return this.values[this.index + (int)offset]; }
			}

			public static LuaTypeValue operator +(LuaTypeValue value, int offset)
			{
				return value.values[value.index + offset];
			}

			public static LuaTypeValue operator +(int offset, LuaTypeValue value)
			{
				return value.values[value.index + offset];
			}

			public static LuaTypeValue operator -(LuaTypeValue value, int offset)
			{
				return value.values[value.index - offset];
			}

			public static int operator -(LuaTypeValue value, LuaTypeValue[] array)
			{
				Debug.Assert(value.values == array);
				return value.index;
			}

			public static int operator -(LuaTypeValue a, LuaTypeValue b)
			{
				Debug.Assert(a.values == b.values);
				return a.index - b.index;
			}
			
			public static bool operator <(LuaTypeValue a, LuaTypeValue b)
			{
				Debug.Assert(a.values == b.values);
				return a.index < b.index;
			}

			public static bool operator <=(LuaTypeValue a, LuaTypeValue b)
			{
				Debug.Assert(a.values == b.values);
				return a.index <= b.index;
			}

			public static bool operator >(LuaTypeValue a, LuaTypeValue b)
			{
				Debug.Assert(a.values == b.values);
				return a.index > b.index;
			}

			public static bool operator >=(LuaTypeValue a, LuaTypeValue b)
			{
				Debug.Assert(a.values == b.values);
				return a.index >= b.index;
			}
			
			public static LuaTypeValue Inc(ref LuaTypeValue value)
			{
				value = value[1];
				return value[-1];
			}

			public static LuaTypeValue Dec(ref LuaTypeValue value)
			{
				value = value[-1];
				return value[1];
			}

			public static implicit operator int(LuaTypeValue value)
			{
				return value.index;
			}

			public LuaTypeValue()
			{
			}

			public LuaTypeValue(LuaTypeValue copy)
			{
				this.values = copy.values;
				this.index = copy.index;
				this.value.Copy(copy.value);
				this.tt = copy.tt;
			}

			public LuaTypeValue(Value value, int tt)
			{
			    this.values = null;
			    this.index = 0;
			    this.value.Copy(value);
			    this.tt = tt;
			}

		  public Value value = new Value();
		  public int tt;

          public override string ToString()
          {
              string typename = null;
              string val = null;
              switch (tt)
              {
                  case LUA_TNIL: typename = "LUA_TNIL"; val = string.Empty;  break;
                  case LUA_TNUMBER: typename = "LUA_TNUMBER"; val = value.n.ToString(); break;
                  case LUA_TSTRING: typename = "LUA_TSTRING"; val = value.gc.ts.ToString(); break;
                  case LUA_TTABLE: typename = "LUA_TTABLE"; break;
                  case LUA_TFUNCTION: typename = "LUA_TFUNCTION"; break;
                  case LUA_TBOOLEAN: typename = "LUA_TBOOLEAN"; break;
                  case LUA_TUSERDATA: typename = "LUA_TUSERDATA"; break;
                  case LUA_TTHREAD: typename = "LUA_TTHREAD"; break;
                  case LUA_TLIGHTUSERDATA: typename = "LUA_TLIGHTUSERDATA"; break;
                  default: typename = "unknown"; break;
              }
              return string.Format("Lua.LuaTypeValue<{0}>({1})", typename, val);
          }
        };

		/* Macros to test type */
		internal static bool TTIsNil(Lua.LuaTypeValue o) { return (TType(o) == LUA_TNIL); }
		internal static bool TTIsNumber(Lua.LuaTypeValue o)	{return (TType(o) == LUA_TNUMBER);}
		internal static bool TTIsString(Lua.LuaTypeValue o)	{return (TType(o) == LUA_TSTRING);}
		internal static bool TTIsTable(Lua.LuaTypeValue o)	{return (TType(o) == LUA_TTABLE);}
		internal static bool TTIsFunction(Lua.LuaTypeValue o)	{return (TType(o) == LUA_TFUNCTION);}
		internal static bool TTIsBoolean(Lua.LuaTypeValue o) { return (TType(o) == LUA_TBOOLEAN); }
		internal static bool TTIsUserData(Lua.LuaTypeValue o) { return (TType(o) == LUA_TUSERDATA); }
		internal static bool TTIsThread(Lua.LuaTypeValue o)	{return (TType(o) == LUA_TTHREAD);}
		internal static bool TTIsLightUserData(Lua.LuaTypeValue o) { return (TType(o) == LUA_TLIGHTUSERDATA); }

		/* Macros to access values */
#if DEBUG
		internal static int TType(Lua.LuaTypeValue o) { return o.tt; }
		internal static int TType(CommonHeader o) { return o.tt; }
		internal static GCObject GCValue(Lua.LuaTypeValue o) { return (GCObject)CheckExp(IsCollectable(o), o.value.gc); }
		internal static object PValue(Lua.LuaTypeValue o) { return (object)CheckExp(TTIsLightUserData(o), o.value.p); }
		internal static LuaNumberType NValue(Lua.LuaTypeValue o) { return (LuaNumberType)CheckExp(TTIsNumber(o), o.value.n); }
		internal static TString RawTSValue(Lua.LuaTypeValue o) { return (TString)CheckExp(TTIsString(o), o.value.gc.ts); }
		internal static TStringTSV TSValue(Lua.LuaTypeValue o) { return RawTSValue(o).tsv; }
		internal static Udata RawUValue(Lua.LuaTypeValue o) { return (Udata)CheckExp(TTIsUserData(o), o.value.gc.u); }
		internal static UdataUV UValue(Lua.LuaTypeValue o) { return RawUValue(o).uv; }
		internal static Closure CLValue(Lua.LuaTypeValue o) { return (Closure)CheckExp(TTIsFunction(o), o.value.gc.cl); }
		internal static Table HValue(Lua.LuaTypeValue o) { return (Table)CheckExp(TTIsTable(o), o.value.gc.h); }
		internal static int BValue(Lua.LuaTypeValue o) { return (int)CheckExp(TTIsBoolean(o), o.value.b); }
		internal static LuaState THValue(Lua.LuaTypeValue o) { return (LuaState)CheckExp(TTIsThread(o), o.value.gc.th); }
#else
		internal static int TType(Lua.LuaTypeValue o) { return o.tt; }
		internal static int TType(CommonHeader o) { return o.tt; }
		internal static GCObject GCValue(Lua.LuaTypeValue o) { return o.value.gc; }
		internal static object PValue(Lua.LuaTypeValue o) { return o.value.p; }
		internal static LuaNumberType NValue(Lua.LuaTypeValue o) { return o.value.n; }
		internal static TString RawTSValue(Lua.LuaTypeValue o) { return o.value.gc.ts; }
		internal static TStringTSV TSValue(Lua.LuaTypeValue o) { return RawTSValue(o).tsv; }
		internal static Udata RawUValue(Lua.LuaTypeValue o) { return o.value.gc.u; }
		internal static UdataUV UValue(Lua.LuaTypeValue o) { return RawUValue(o).uv; }
		internal static Closure CLValue(Lua.LuaTypeValue o) { return o.value.gc.cl; }
		internal static Table HValue(Lua.LuaTypeValue o) { return o.value.gc.h; }
		internal static int BValue(Lua.LuaTypeValue o) { return o.value.b; }
		internal static LuaState THValue(Lua.LuaTypeValue o) { return (LuaState)CheckExp(TTIsThread(o), o.value.gc.th); }
#endif

		public static int LIsFalse(Lua.LuaTypeValue o) { return ((TTIsNil(o) || (TTIsBoolean(o) && BValue(o) == 0))) ? 1 : 0; }

		/*
		** for internal debug only
		*/
		[Conditional("DEBUG")]
		internal static void CheckConsistency(Lua.LuaTypeValue obj)
		{
			LuaAssert(!IsCollectable(obj) || (TType(obj) == (obj).value.gc.gch.tt));
		}

		[Conditional("DEBUG")]
		internal static void CheckLiveness(GlobalState g, Lua.LuaTypeValue obj)
		{
			LuaAssert(!IsCollectable(obj) ||
			((TType(obj) == obj.value.gc.gch.tt) && !IsDead(g, obj.value.gc)));
		}
		
		/* Macros to set values */
		internal static void SetNilValue(Lua.LuaTypeValue obj) {
			obj.tt=LUA_TNIL;
		}

		internal static void SetNValue(Lua.LuaTypeValue obj, LuaNumberType x) {
			obj.value.n = x;
			obj.tt = LUA_TNUMBER;
		}

		internal static void SetPValue( Lua.LuaTypeValue obj, object x) {
			obj.value.p = x;
			obj.tt = LUA_TLIGHTUSERDATA;
		}

		internal static void SetBValue(Lua.LuaTypeValue obj, int x) {
			obj.value.b = x;
			obj.tt = LUA_TBOOLEAN;
		}

		internal static void SetSValue(LuaState L, Lua.LuaTypeValue obj, GCObject x) {
			obj.value.gc = x;
			obj.tt = LUA_TSTRING;
			CheckLiveness(G(L), obj);
		}

		internal static void SetUValue(LuaState L, Lua.LuaTypeValue obj, GCObject x) {
			obj.value.gc = x;
			obj.tt = LUA_TUSERDATA;
			CheckLiveness(G(L), obj);
		}

		internal static void SetTTHValue(LuaState L, Lua.LuaTypeValue obj, GCObject x) {
			obj.value.gc = x;
			obj.tt = LUA_TTHREAD;
			CheckLiveness(G(L), obj);
		}

		internal static void SetCLValue(LuaState L, Lua.LuaTypeValue obj, Closure x) {
			obj.value.gc = x;
			obj.tt = LUA_TFUNCTION;
			CheckLiveness(G(L), obj);
		}

		internal static void SetHValue(LuaState L, Lua.LuaTypeValue obj, Table x) {
			obj.value.gc = x;
			obj.tt = LUA_TTABLE;
			CheckLiveness(G(L), obj);
		}

		internal static void SetPTValue(LuaState L, Lua.LuaTypeValue obj, Proto x) {
			obj.value.gc = x;
			obj.tt = LUATPROTO;
			CheckLiveness(G(L), obj);
		}

		internal static void SetObj(LuaState L, Lua.LuaTypeValue obj1, Lua.LuaTypeValue obj2) {
			obj1.value.Copy(obj2.value);
			obj1.tt = obj2.tt;
			CheckLiveness(G(L), obj1);
		}


		/*
		** different types of sets, according to destination
		*/

		/* from stack to (same) stack */
		//#define setobjs2s	setobj
		internal static void SetObjs2S(LuaState L, Lua.LuaTypeValue obj, Lua.LuaTypeValue x) { SetObj(L, obj, x); }
		///* to stack (not from same stack) */
		
		//#define setobj2s	setobj
		internal static void SetObj2S(LuaState L, Lua.LuaTypeValue obj, Lua.LuaTypeValue x) { SetObj(L, obj, x); }

		//#define setsvalue2s	setsvalue
		internal static void SetSValue2S(LuaState L, Lua.LuaTypeValue obj, TString x) { SetSValue(L, obj, x); }

		//#define sethvalue2s	sethvalue
		internal static void SetHValue2S(LuaState L, Lua.LuaTypeValue obj, Table x) { SetHValue(L, obj, x); }

		//#define setptvalue2s	setptvalue
		internal static void SetPTValue2S(LuaState L, Lua.LuaTypeValue obj, Proto x) { SetPTValue(L, obj, x); }

		///* from table to same table */
		//#define setobjt2t	setobj
		internal static void SetObjT2T(LuaState L, Lua.LuaTypeValue obj, Lua.LuaTypeValue x) { SetObj(L, obj, x); }

		///* to table */
		//#define setobj2t	setobj
		internal static void SetObj2T(LuaState L, Lua.LuaTypeValue obj, Lua.LuaTypeValue x) { SetObj(L, obj, x); }

		///* to new object */
		//#define setobj2n	setobj
		internal static void SetObj2N(LuaState L, Lua.LuaTypeValue obj, Lua.LuaTypeValue x) { SetObj(L, obj, x); }

		//#define setsvalue2n	setsvalue
		internal static void SetSValue2N(LuaState L, Lua.LuaTypeValue obj, TString x) { SetSValue(L, obj, x); }

		internal static void SetTType(Lua.LuaTypeValue obj, int tt) { obj.tt = tt; }


		internal static bool IsCollectable(Lua.LuaTypeValue o) { return (TType(o) >= LUA_TSTRING); }



		//typedef Lua.LuaTypeValue *StkId;  /* index to stack elements */
		
		/*
		** String headers for string table
		*/
		public class TStringTSV : GCObject
		{
			public LuaByteType reserved;
#if !UNITY_3D
			[CLSCompliantAttribute(false)]
#endif
			public uint hash;
#if !UNITY_3D
			[CLSCompliantAttribute(false)]
#endif
			public uint len;
		};
		public class TString : TStringTSV {
			//public L_Umaxalign dummy;  /* ensures maximum alignment for strings */			
			public TStringTSV tsv { get { return this; } }

			public TString()
			{
			}
			public TString(CharPtr str) { this.str = str; }

			public CharPtr str;

			public override string ToString() { return str.ToString(); } // for debugging
		};

		public static CharPtr GetStr(TString ts) { return ts.str; }
		public static CharPtr SValue(StkId o) { return GetStr(RawTSValue(o)); }

		public class UdataUV : GCObject
		{
			public Table metatable;
			public Table env;
#if !UNITY_3D
			[CLSCompliantAttribute(false)]
#endif
			public uint len;
		};

		public class Udata : UdataUV
		{
			public Udata() { this.uv = this; }

			public new UdataUV uv;

			//public L_Umaxalign dummy;  /* ensures maximum alignment for `local' udata */

			// in the original C code this was allocated alongside the structure memory. it would probably
			// be possible to still do that by allocating memory and pinning it down, but we can do the
			// same thing just as easily by allocating a seperate byte array for it instead.
			public object user_data;
		};




		/*
		** Function Prototypes
		*/
		public class Proto : GCObject {

		  public Proto[] protos = null;
		  public int index = 0;
		  public Proto this[int offset] {get { return this.protos[this.index + offset]; }}

		  public Lua.LuaTypeValue[] k;  /* constants used by the function */
#if !UNITY_3D
			[CLSCompliantAttribute(false)]
#endif
		  public Instruction[] code;
		  public new Proto[] p;  /* functions defined inside the function */
		  public int[] lineinfo;  /* map from opcodes to source lines */
		  public LocVar[] locvars;  /* information about local variables */
		  public TString[] upvalues;  /* upvalue names */
		  public TString  source;
		  public int sizeupvalues;
		  public int sizek;  /* size of `k' */
		  public int sizecode;
		  public int sizelineinfo;
		  public int sizep;  /* size of `p' */
		  public int sizelocvars;
		  public int linedefined;
		  public int lastlinedefined;
		  public GCObject gclist;
		  public LuaByteType nups;  /* number of upvalues */
		  public LuaByteType numparams;
		  public LuaByteType is_vararg;
		  public LuaByteType maxstacksize;
		};


		/* masks for new-style vararg */
		public const int VARARG_HASARG			= 1;
		public const int VARARG_ISVARARG		= 2;
		public const int VARARG_NEEDSARG		= 4;

		public class LocVar {
		  public TString varname;
		  public int startpc;  /* first point where variable is active */
		  public int endpc;    /* first point where variable is dead */
		};



		/*
		** Upvalues
		*/

		public class UpVal : GCObject {
		  public Lua.LuaTypeValue v;  /* points to stack or to its own value */
#if !UNITY_3D
			[CLSCompliantAttribute(false)]
#endif
			public class Uinternal {
				public Lua.LuaTypeValue value = new LuaTypeValue();  /* the value (when closed) */
#if !UNITY_3D
				[CLSCompliantAttribute(false)]
#endif
				public class _l {  /* double linked list (when open) */
				  public UpVal prev;
				  public UpVal next;
				};

				public _l l = new _l();
		  }
#if !UNITY_3D
			[CLSCompliantAttribute(false)]
#endif
			public new Uinternal u = new Uinternal();
		};


		/*
		** Closures
		*/

		public class ClosureHeader : GCObject {
			public LuaByteType isC;
			public LuaByteType nupvalues;
			public GCObject gclist;
			public Table env;
		};

		public class ClosureType {

			ClosureHeader header;

			public static implicit operator ClosureHeader(ClosureType ctype) {return ctype.header;}
			public ClosureType(ClosureHeader header) {this.header = header;}

			public LuaByteType isC { get { return header.isC; } set { header.isC = value; } }
			public LuaByteType nupvalues { get { return header.nupvalues; } set { header.nupvalues = value; } }
			public GCObject gclist { get { return header.gclist; } set { header.gclist = value; } }
			public Table env { get { return header.env; } set { header.env = value; } }
		}

		public class CClosure : ClosureType {
			public CClosure(ClosureHeader header) : base(header) { }
			public LuaNativeFunction f;
			public Lua.LuaTypeValue[] upvalue;
		};


		public class LClosure : ClosureType {
			public LClosure(ClosureHeader header) : base(header) { }
			public Proto p;
			public UpVal[] upvals;
		};

		public class Closure : ClosureHeader
		{
		  public Closure()
		  {
			  c = new CClosure(this);
			  l = new LClosure(this);
		  }

		  public CClosure c;
		  public LClosure l;
		};


		public static bool IsCFunction(Lua.LuaTypeValue o) { return ((TType(o) == LUA_TFUNCTION) && (CLValue(o).c.isC != 0)); }
		public static bool IsLfunction(Lua.LuaTypeValue o) { return ((TType(o) == LUA_TFUNCTION) && (CLValue(o).c.isC==0)); }


		/*
		** Tables
		*/

		public class TKeyNK : Lua.LuaTypeValue
		{
			public TKeyNK() { }
			public TKeyNK(Value value, int tt, Node next) : base(value, tt)
			{
			    this.next = next;
			}
			public Node next;  /* for chaining */
		};

		public class TKey {
			public TKey()
			{
				this.nk = new TKeyNK();
			}
			public TKey(TKey copy)
			{
				this.nk = new TKeyNK(copy.nk.value, copy.nk.tt, copy.nk.next);
			}
			public TKey(Value value, int tt, Node next)
			{
			    this.nk = new TKeyNK(value, tt, next);
			}

			public TKeyNK nk = new TKeyNK();
			public Lua.LuaTypeValue tvk { get { return this.nk; } }
		};


		public class Node : ArrayElement
		{
			private Node[] values = null;
			private int index = -1;

			public void SetIndex(int index)
			{
				this.index = index;
			}

			public void SetArray(object array)
			{
				this.values = (Node[])array;
				Debug.Assert(this.values != null);
			}

			public Node()
			{
				this.i_val = new LuaTypeValue();
				this.i_key = new TKey();
			}

			public Node(Node copy)
			{
				this.values = copy.values;
				this.index = copy.index;
				this.i_val = new LuaTypeValue(copy.i_val);
				this.i_key = new TKey(copy.i_key);
			}

			public Node(Lua.LuaTypeValue i_val, TKey i_key)
			{
				this.values = new Node[] { this };
				this.index = 0;
				this.i_val = i_val;
				this.i_key = i_key;
			}

			public Lua.LuaTypeValue i_val;
			public TKey i_key;

#if !UNITY_3D
			[CLSCompliantAttribute(false)]
#endif
			public Node this[uint offset]
			{
				get { return this.values[this.index + (int)offset]; }
			}

			public Node this[int offset]
			{
				get { return this.values[this.index + offset]; }
			}

			public static int operator -(Node n1, Node n2)
			{
				Debug.Assert(n1.values == n2.values);
				return n1.index - n2.index;
			}

			public static Node Inc(ref Node node)
			{
				node = node[1];
				return node[-1];
			}

			public static Node Dec(ref Node node)
			{
				node = node[-1];
				return node[1];
			}

			public static bool operator >(Node n1, Node n2) { Debug.Assert(n1.values == n2.values); return n1.index > n2.index; }
			public static bool operator >=(Node n1, Node n2) { Debug.Assert(n1.values == n2.values); return n1.index >= n2.index; }
			public static bool operator <(Node n1, Node n2) { Debug.Assert(n1.values == n2.values); return n1.index < n2.index; }
			public static bool operator <=(Node n1, Node n2) { Debug.Assert(n1.values == n2.values); return n1.index <= n2.index; }
			public static bool operator ==(Node n1, Node n2)
			{
				object o1 = n1 as Node;
				object o2 = n2 as Node;
				if ((o1 == null) && (o2 == null)) return true;
				if (o1 == null) return false;
				if (o2 == null) return false;
				if (n1.values != n2.values) return false;
				return n1.index == n2.index;
			}
			public static bool operator !=(Node n1, Node n2) { return !(n1==n2); }

			public override bool Equals(object o) {return this == (Node)o;}
			public override int GetHashCode() {return 0;}
		};


		public class Table : GCObject {
		  public LuaByteType flags;  /* 1<<p means tagmethod(p) is not present */ 
		  public LuaByteType lsizenode;  /* log2 of size of `node' array */
		  public Table metatable;
		  public Lua.LuaTypeValue[] array;  /* array part */
		  public Node[] node;
		  public int lastfree;  /* any free position is before this position */
		  public GCObject gclist;
		  public int sizearray;  /* size of `array' array */
		};



		/*
		** `module' operation for hashing (size is always a power of 2)
		*/
		//#define lmod(s,size) \
		//    (check_exp((size&(size-1))==0, (cast(int, (s) & ((size)-1)))))


		internal static int TwoTo(int x) { return 1 << x; }
		internal static int SizeNode(Table t) { return TwoTo(t.lsizenode); }

		public static Lua.LuaTypeValue LuaONilObjectX = new LuaTypeValue(new Value(), LUA_TNIL);
		public static Lua.LuaTypeValue LuaONilObject = LuaONilObjectX;

		public static int CeilLog2(int x)	{return LuaOLog2((uint)(x-1)) + 1;}
	


		/*
		** converts an integer to a "floating point byte", represented as
		** (eeeeexxx), where the real value is (1xxx) * 2^(eeeee - 1) if
		** eeeee != 0 and (xxx) otherwise.
		*/
#if !UNITY_3D
		[CLSCompliantAttribute(false)]
#endif
		public static int LuaOInt2FB (uint x) {
		  int e = 0;  /* expoent */
		  while (x >= 16) {
			x = (x+1) >> 1;
			e++;
		  }
		  if (x < 8) return (int)x;
		  else return ((e+1) << 3) | (CastInt(x) - 8);
		}


		/* converts back */
		public static int LuaOFBInt (int x) {
		  int e = (x >> 3) & 31;
		  if (e == 0) return x;
		  else return ((x & 7)+8) << (e - 1);
		}


		private readonly static LuaByteType[] log2 = {
			0,1,2,2,3,3,3,3,4,4,4,4,4,4,4,4,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,
			6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,
			7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,
			7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,
			8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,
			8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,
			8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,
			8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8
		  };

#if !UNITY_3D
		[CLSCompliantAttribute(false)]
#endif
		public static int LuaOLog2 (uint x) {
		  int l = -1;
		  while (x >= 256) { l += 8; x >>= 8; }
		  return l + log2[x];

		}


		public static int LuaORawEqualObj (Lua.LuaTypeValue t1, Lua.LuaTypeValue t2) {
		  if (TType(t1) != TType(t2)) return 0;
		  else switch (TType(t1)) {
			case LUA_TNIL:
			  return 1;
			case LUA_TNUMBER:
			  return luai_numeq(NValue(t1), NValue(t2)) ? 1 : 0;
			case LUA_TBOOLEAN:
			  return BValue(t1) == BValue(t2) ? 1 : 0;  /* boolean true must be 1....but not in C# !! */
			case LUA_TLIGHTUSERDATA:
				return PValue(t1) == PValue(t2) ? 1 : 0;
			default:
			  LuaAssert(IsCollectable(t1));
			  return GCValue(t1) == GCValue(t2) ? 1 : 0;
		  }
		}

		public static int LuaOStr2d (CharPtr s, out LuaNumberType result) {
		  CharPtr endptr;
		  result = lua_str2number(s, out endptr);
		  if (endptr == s) return 0;  /* conversion failed */
		  if (endptr[0] == 'x' || endptr[0] == 'X')  /* maybe an hexadecimal constant? */
			result = CastNum(strtoul(s, out endptr, 16));
		  if (endptr[0] == '\0') return 1;  /* most common case */
		  while (isspace(endptr[0])) endptr = endptr.next();
		  if (endptr[0] != '\0') return 0;  /* invalid trailing characters? */
		  return 1;
		}



		private static void PushStr (LuaState L, CharPtr str) {
		  SetSValue2S(L, L.top, luaS_new(L, str));
		  IncrTop(L);
		}


		/* this function handles only `%d', `%c', %f, %p, and `%s' formats */
		public static CharPtr LuaOPushVFString (LuaState L, CharPtr fmt, params object[] argp) {
		  int parm_index = 0;
		  int n = 1;
		  PushStr(L, "");
		  for (;;) {
		    CharPtr e = strchr(fmt, '%');
		    if (e == null) break;
		    SetSValue2S(L, L.top, luaS_newlstr(L, fmt, (uint)(e-fmt)));
		    IncrTop(L);
		    switch (e[1]) {
		      case 's': {
				  object o = argp[parm_index++];
				  CharPtr s = o as CharPtr;
				  if (s == null)
					  s = (string)o;
				  if (s == null) s = "(null)";
		          PushStr(L, s);
		          break;
		      }
		      case 'c': {
		        CharPtr buff = new char[2];
		        buff[0] = (char)(int)argp[parm_index++];
		        buff[1] = '\0';
		        PushStr(L, buff);
		        break;
		      }
		      case 'd': {
		        SetNValue(L.top, (int)argp[parm_index++]);
		        IncrTop(L);
		        break;
		      }
		      case 'f': {
		        SetNValue(L.top, (l_uacNumber)argp[parm_index++]);
		        IncrTop(L);
		        break;
		      }
		      case 'p': {
		        //CharPtr buff = new char[4*sizeof(void *) + 8]; /* should be enough space for a `%p' */
				CharPtr buff = new char[32];
				sprintf(buff, "0x%08x", argp[parm_index++].GetHashCode());
		        PushStr(L, buff);
		        break;
		      }
		      case '%': {
		        PushStr(L, "%");
		        break;
		      }
		      default: {
		        CharPtr buff = new char[3];
		        buff[0] = '%';
		        buff[1] = e[1];
		        buff[2] = '\0';
		        PushStr(L, buff);
		        break;
		      }
		    }
		    n += 2;
		    fmt = e+2;
		  }
		  PushStr(L, fmt);
		  luaV_concat(L, n+1, CastInt(L.top - L.base_) - 1);
		  L.top -= n;
		  return SValue(L.top - 1);
		}

		public static CharPtr LuaOPushFString(LuaState L, CharPtr fmt, params object[] args)
		{
			return LuaOPushVFString(L, fmt, args);
		}

#if !UNITY_3D
		[CLSCompliantAttribute(false)]
#endif
		public static void LuaOChunkID (CharPtr out_, CharPtr source, uint bufflen) {
			//out_ = "";
		  if (source[0] == '=') {
		    strncpy(out_, source+1, (int)bufflen);  /* remove first char */
		    out_[bufflen-1] = '\0';  /* ensures null termination */
		  }
		  else {  /* out = "source", or "...source" */
		    if (source[0] == '@') {
		      uint l;
		      source = source.next();  /* skip the `@' */
		      bufflen -= (uint)(" '...' ".Length + 1);
		      l = (uint)strlen(source);
		      strcpy(out_, "");
		      if (l > bufflen) {
		        source += (l-bufflen);  /* get last part of file name */
		        strcat(out_, "...");
		      }
		      strcat(out_, source);
		    }
		    else {  /* out = [string "string"] */
		      uint len = strcspn(source, "\n\r");  /* stop at first newline */
		      bufflen -= (uint)(" [string \"...\"] ".Length + 1);
		      if (len > bufflen) len = bufflen;
		      strcpy(out_, "[string \"");
		      if (source[len] != '\0') {  /* must truncate? */
		        strncat(out_, source, (int)len);
		        strcat(out_, "...");
		      }
		      else
		        strcat(out_, source);
		      strcat(out_, "\"]");
		    }
		  }
		}

	}
}

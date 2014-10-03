using System;

namespace KopiLua
{
	using lu_byte = System.Byte;
	using lu_int32 = System.Int32;
	using lu_mem = System.UInt32;
	using TValue = Lua.LuaTypeValue;
	using StkId = Lua.LuaTypeValue;
	using ptrdiff_t = System.Int32;
	using Instruction = System.UInt32;
	/*
		** `per thread' state
		*/
	public class LuaState : Lua.GCObject {

		public lu_byte status;
		public StkId top;  /* first free slot in the stack */
		public StkId base_;  /* base of current function */
		public Lua.GlobalState l_G;
		public Lua.CallInfo ci;  /* call info for current function */
		public InstructionPtr savedpc = new InstructionPtr();  /* `savedpc' of current function */
		public StkId stack_last;  /* last free slot in the stack */
		public StkId[] stack;  /* stack base */
		public Lua.CallInfo end_ci;  /* points after end of ci array*/
		public Lua.CallInfo[] base_ci;  /* array of CallInfo's */
		public int stacksize;
		public int size_ci;  /* size of array `base_ci' */
#if !UNITY_3D
		[CLSCompliantAttribute(false)]
#endif
		public ushort nCcalls;  /* number of nested C calls */
#if !UNITY_3D
		[CLSCompliantAttribute(false)]
#endif
		public ushort baseCcalls;  /* nested C calls when resuming coroutine */
		public lu_byte hookmask;
		public lu_byte allowhook;
		public int basehookcount;
		public int hookcount;
		public LuaHook hook;
		public TValue l_gt = new Lua.LuaTypeValue();  /* table of globals */
		public TValue env = new Lua.LuaTypeValue();  /* temporary place for environments */
		public Lua.GCObject openupval;  /* list of open upvalues in this stack */
		public Lua.GCObject gclist;
		public Lua.LuaLongJmp errorJmp;  /* current error recover point */
		public ptrdiff_t errfunc;  /* current error handling function (stack index) */
	}
}

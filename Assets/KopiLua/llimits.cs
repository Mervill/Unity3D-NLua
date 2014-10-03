//#define lua_assert

/*
** $Id: llimits.h,v 1.69.1.1 2007/12/27 13:02:25 roberto Exp $
** Limits, basic types, and some other `installation-dependent' definitions
** See Copyright Notice in lua.h
*/

using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace KopiLua
{
	using lu_int32 = System.UInt32;
	using lu_mem = System.UInt32;
	using l_mem = System.Int32;
	using lu_byte = System.Byte;
	using l_uacNumber = System.Double;
	using lua_Number = System.Double;
	using Instruction = System.UInt32;

	public partial class Lua
	{

		//typedef LUAI_UINT32 lu_int32;

		//typedef LUAI_UMEM lu_mem;

		//typedef LUAI_MEM l_mem;



		/* chars used as small naturals (so that `char' is reserved for characters) */
		//typedef unsigned char lu_byte;

		[CLSCompliantAttribute(false)]
		public const uint MAXSIZET	= uint.MaxValue - 2;
		[CLSCompliantAttribute(false)]
		public const lu_mem MAXLUMEM	= lu_mem.MaxValue - 2;


		public const int MAXINT = (Int32.MaxValue - 2);  /* maximum value of an int (-2 for safety) */

		/*
		** conversion of pointer to integer
		** this is for hashing only; there is no problem if the integer
		** cannot hold the whole pointer value
		*/
		//#define IntPoint(p)  ((uint)(lu_mem)(p))



		/* type to ensure maximum alignment */
		//typedef LUAI_USER_ALIGNMENT_T L_Umaxalign;


		/* result of a `usual argument conversion' over lua_Number */
		//typedef LUAI_UACNUMBER l_uacNumber;


		/* internal assertions for in-house debugging */

#if lua_assert

		[Conditional("DEBUG")]
		public static void LuaAssert(bool c) {Debug.Assert(c);}

		[Conditional("DEBUG")]
		public static void LuaAssert(int c) { Debug.Assert(c != 0); }

		internal static object CheckExp(bool c, object e)		{LuaAssert(c); return e;}
		public static object CheckExp(int c, object e) { LuaAssert(c != 0); return e; }

#else

		[Conditional("DEBUG")]
		public static void LuaAssert (bool c) { }

		[Conditional("DEBUG")]
		public static void LuaAssert (int c) { }

		public static object CheckExp (bool c, object e) { return e; }
		public static object CheckExp (int c, object e) { return e; }

#endif

		[Conditional("DEBUG")]
		internal static void ApiCheck(object o, bool e) { LuaAssert(e); }
		internal static void ApiCheck(object o, int e) { LuaAssert(e != 0); }

		//#define UNUSED(x)	((void)(x))	/* to avoid warnings */


		internal static lu_byte CastByte(int i) { return (lu_byte)i; }
		internal static lu_byte CastByte(long i) { return (lu_byte)(int)i; }
		internal static lu_byte CastByte(bool i) { return i ? (lu_byte)1 : (lu_byte)0; }
		internal static lu_byte CastByte(lua_Number i) { return (lu_byte)i; }
		internal static lu_byte CastByte(object i) { return (lu_byte)(int)(i); }

		internal static int CastInt(int i) { return (int)i; }
		internal static int CastInt(uint i) { return (int)i; }
		internal static int CastInt(long i) { return (int)(int)i; }
		internal static int CastInt(ulong i) { return (int)(int)i; }
		internal static int CastInt(bool i) { return i ? (int)1 : (int)0; }
		internal static int CastInt(lua_Number i) { return (int)i; }
		internal static int CastInt(object i) { Debug.Assert(false, "Can't convert int."); return Convert.ToInt32(i); }

		internal static lua_Number CastNum(int i) { return (lua_Number)i; }
		internal static lua_Number CastNum(uint i) { return (lua_Number)i; }
		internal static lua_Number CastNum(long i) { return (lua_Number)i; }
		internal static lua_Number CastNum(ulong i) { return (lua_Number)i; }
		internal static lua_Number CastNum(bool i) { return i ? (lua_Number)1 : (lua_Number)0; }
		internal static lua_Number CastNum(object i) { Debug.Assert(false, "Can't convert number."); return Convert.ToSingle(i); }

		/*
		** type for virtual-machine instructions
		** must be an unsigned with (at least) 4 bytes (see details in lopcodes.h)
		*/
		//typedef lu_int32 Instruction;



		/* maximum stack for a Lua function */
		public const int MAXSTACK	= 250;



		/* minimum size for the string table (must be power of 2) */
		public const int MINSTRTABSIZE	= 32;


		/* minimum size for string buffer */
		public const int LUAMINBUFFER	= 32;


		#if !lua_lock
		public static void LuaLock(LuaState L) { }
		public static void LuaUnlock(LuaState L) { }
		#endif
		

		#if !luai_threadyield
		public static void LuaIThreadYield(LuaState L)     {LuaUnlock(L); LuaLock(L);}
		#endif


		/*
		** macro to control inclusion of some hard tests on stack reallocation
		*/ 
		//#ifndef HARDSTACKTESTS
		//#define condhardstacktests(x)	((void)0)
		//#else
		//#define condhardstacktests(x)	x
		//#endif

	}
}

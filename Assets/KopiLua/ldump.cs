/*
** $Id: ldump.c,v 2.8.1.1 2007/12/27 13:02:25 roberto Exp $
** save precompiled Lua chunks
** See Copyright Notice in lua.h
*/

using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Runtime.Serialization;


namespace KopiLua
{
	using LuaNumberType = System.Double;
	using TValue = Lua.LuaTypeValue;

	public partial class Lua
	{
		public class DumpState {
		 public LuaState L;
#if !UNITY_3D
		 [CLSCompliantAttribute(false)]
#endif
		 public lua_Writer writer;
		 public object data;
		 public int strip;
		 public int status;
		};

		public static void DumpMem(object b, DumpState D)
		{
#if XBOX
			// todo: implement this - mjf
			Debug.Assert(false);
#endif

#if SILVERLIGHT
			// No support for Marshal.SizeOf in Silverlight, so we
			// have to manually set the size. Size values are from
			// Lua's 5.1 spec.

			// No support for Marshal.StructureToPtr in Silverlight,
			// let's use BitConverter instead!
			
			int size = 0;
			byte[] bytes;
			Type t = b.GetType();
			if (t.Equals(typeof(UInt32)))
			{
				size = 4;
				bytes = BitConverter.GetBytes((uint)b);
			}
			else if (t.Equals(typeof(Int32)))
			{
				size = 4;
				bytes = BitConverter.GetBytes((int)b);
			}
			else if (t.Equals(typeof(Char)))
			{
				size = 1;
				bytes = new byte[1] { BitConverter.GetBytes((char)b)[0] };
			}
			else if (t.Equals(typeof(Byte)))
			{
				size = 1;
				bytes = new byte[1] { (byte)b };
				//bytes = BitConverter.GetBytes((byte)b);
			}
			else if (t.Equals(typeof(Double)))
			{
				size = 8;
				bytes = BitConverter.GetBytes((double)b);
			}
			else
			{
				throw new NotImplementedException("Invalid type: " + t.FullName);
			}

#else
			int size = Marshal.SizeOf(b);
			IntPtr ptr = Marshal.AllocHGlobal(size);
			Marshal.StructureToPtr(b, ptr, false);
			byte[] bytes = new byte[size];
			Marshal.Copy(ptr, bytes, 0, size);
#endif
			char[] ch = new char[bytes.Length];
			for (int i = 0; i < bytes.Length; i++)
				ch[i] = (char)bytes[i];
			CharPtr str = ch;
			DumpBlock(str, (uint)str.chars.Length, D);

#if !SILVERLIGHT
			Marshal.Release(ptr); 
#endif
		}

		public static void DumpMem(object b, int n, DumpState D)
		{
			Array array = b as Array;
			Debug.Assert(array.Length == n);
			for (int i = 0; i < n; i++)
				DumpMem(array.GetValue(i), D);
		}

		public static void DumpVar(object x, DumpState D)
		{
			DumpMem(x, D);
		}

		private static void DumpBlock(CharPtr b, uint size, DumpState D)
		{
		 if (D.status==0)
		 {
		  LuaUnlock(D.L);
		  D.status=D.writer(D.L,b,size,D.data);
		  LuaLock(D.L);
		 }
		}

		private static void DumpChar(int y, DumpState D)
		{
		 char x=(char)y;
		 DumpVar(x,D);
		}

		private static void DumpInt(int x, DumpState D)
		{
		 DumpVar(x,D);
		}

		private static void DumpNumber(LuaNumberType x, DumpState D)
		{
		 DumpVar(x,D);
		}

		static void DumpVector(object b, int n, DumpState D)
		{
		 DumpInt(n,D);
		 DumpMem(b, n, D);
		}

		private static void DumpString(TString s, DumpState D)
		{
		 if (s==null || GetStr(s)==null)
		 {
		  uint size=0;
		  DumpVar(size,D);
		 }
		 else
		 {
		  uint size=s.tsv.len+1;		/* include trailing '\0' */
		  DumpVar(size,D);
		  DumpBlock(GetStr(s),size,D);
		 }
		}

		private static void DumpCode(Proto f,DumpState D)
		{
			DumpVector(f.code, f.sizecode, D);
		}

		private static void DumpConstants(Proto f, DumpState D)
		{
		 int i,n=f.sizek;
		 DumpInt(n,D);
		 for (i=0; i<n; i++)
		 {
		  /*const*/ TValue o=f.k[i];
		  DumpChar(TType(o),D);
		  switch (TType(o))
		  {
		   case LUA_TNIL:
			break;
		   case LUA_TBOOLEAN:
			DumpChar(BValue(o),D);
			break;
		   case LUA_TNUMBER:
			DumpNumber(NValue(o),D);
			break;
		   case LUA_TSTRING:
			DumpString(RawTSValue(o),D);
			break;
		   default:
			LuaAssert(0);			/* cannot happen */
			break;
		  }
		 }
		 n=f.sizep;
		 DumpInt(n,D);
		 for (i=0; i<n; i++) DumpFunction(f.p[i],f.source,D);
		}

		private static void DumpDebug(Proto f, DumpState D)
		{
		 int i,n;
		 n= (D.strip != 0) ? 0 : f.sizelineinfo;
		 DumpVector(f.lineinfo, n, D);
		 n= (D.strip != 0) ? 0 : f.sizelocvars;
		 DumpInt(n,D);
		 for (i=0; i<n; i++)
		 {
		  DumpString(f.locvars[i].varname,D);
		  DumpInt(f.locvars[i].startpc,D);
		  DumpInt(f.locvars[i].endpc,D);
		 }
		 n= (D.strip != 0) ? 0 : f.sizeupvalues;
		 DumpInt(n,D);
		 for (i=0; i<n; i++) DumpString(f.upvalues[i],D);
		}

		private static void DumpFunction(Proto f, TString p, DumpState D)
		{
		 DumpString( ((f.source==p) || (D.strip!=0)) ? null : f.source, D);
		 DumpInt(f.linedefined,D);
		 DumpInt(f.lastlinedefined,D);
		 DumpChar(f.nups,D);
		 DumpChar(f.numparams,D);
		 DumpChar(f.is_vararg,D);
		 DumpChar(f.maxstacksize,D);
		 DumpCode(f,D);
		 DumpConstants(f,D);
		 DumpDebug(f,D);
		}

		private static void DumpHeader(DumpState D)
		{
		 CharPtr h = new char[LUAC_HEADERSIZE];
		 luaU_header(h);
		 DumpBlock(h,LUAC_HEADERSIZE,D);
		}

		/*
		** dump Lua function as precompiled chunk
		*/
#if !UNITY_3D
		[CLSCompliantAttribute(false)]
#endif
		public static int LuaUDump (LuaState L, Proto f, lua_Writer w, object data, int strip)
		{
		 DumpState D = new DumpState();
		 D.L=L;
		 D.writer=w;
		 D.data=data;
		 D.strip=strip;
		 D.status=0;
		 DumpHeader(D);
		 DumpFunction(f,null,D);
		 return D.status;
		}
	}
}

using System;

namespace KopiLua
{
	public partial class Lua
	{
		private static object tag = 0;

		public static void LuaPushStdCallCFunction (LuaState luaState, LuaNativeFunction function)
		{
			LuaPushCFunction (luaState, function);
		}

		public static bool LuaLCheckMetatable (LuaState luaState, int index)
		{
			bool retVal = false;
			
			if (LuaGetMetatable (luaState, index) != 0) {
				LuaPushLightUserData (luaState, tag);
				LuaRawGet (luaState, -2);
				retVal = !LuaIsNil (luaState, -1);
				LuaSetTop (luaState, -3);
			}
			
			return retVal;
		}

		public static LuaTag LuaNetGetTag ()
		{
			return new LuaTag (tag);
		}

		public static void LuaPushLightUserData (LuaState L, LuaTag p)
		{
			LuaPushLightUserData (L, p.Tag);
		}

		// Starting with 5.1 the auxlib version of checkudata throws an exception if the type isn't right
		// Instead, we want to run our own version that checks the type and just returns null for failure
		private static object CheckUserDataRaw (LuaState L, int ud, string tname)
		{
			object p = LuaToUserData (L, ud);
			
			if (p != null) {
				/* value is a userdata? */
				if (LuaGetMetatable (L, ud) != 0) { 
					bool isEqual;
					
					/* does it have a metatable? */
					LuaGetField (L, LUA_REGISTRYINDEX, tname);  /* get correct metatable */
					
					isEqual = LuaRawEqual (L, -1, -2) != 0;
					
					// NASTY - we need our own version of the lua_pop macro
					// lua_pop(L, 2);  /* remove both metatables */
					LuaSetTop (L, -(2) - 1);
					
					if (isEqual)	/* does it have the correct mt? */
						return p;
				}
			}
			
			return null;
		}


		public static int LuaNetCheckUData (LuaState luaState, int ud, string tname)
		{
			object udata = CheckUserDataRaw (luaState, ud, tname);
			return udata != null ? FourBytesToInt (udata as byte[]) : -1;
		}

		public static int LuaNetToNetObject (LuaState luaState, int index)
		{
			byte[] udata;
			
			if (LuaType (luaState, index) == LUA_TUSERDATA) {
				if (LuaLCheckMetatable (luaState, index)) {
					udata = LuaToUserData (luaState, index) as byte[];
					if (udata != null)
						return FourBytesToInt (udata);
				}
				
				udata = CheckUserDataRaw (luaState, index, "luaNet_class") as byte[];
				if (udata != null)
					return FourBytesToInt (udata);
				
				udata = CheckUserDataRaw (luaState, index, "luaNet_searchbase") as byte[];
				if (udata != null)
					return FourBytesToInt (udata);
				
				udata = CheckUserDataRaw (luaState, index, "luaNet_function") as byte[];
				if (udata != null)
					return FourBytesToInt (udata);
			}
			
			return -1;
		}

		public static void LuaNetNewUData (LuaState luaState, int val)
		{
			var userdata = LuaNewUserData (luaState, sizeof(int)) as byte[];
			IntToFourBytes (val, userdata);
		}

		public static int LuaNetRawNetObj (LuaState luaState, int obj)
		{
			byte[] bytes = LuaToUserData (luaState, obj) as byte[];
			if (bytes == null)
				return -1;
			return FourBytesToInt (bytes);
		}

		private static int FourBytesToInt (byte [] bytes)
		{
			return bytes [0] + (bytes [1] << 8) + (bytes [2] << 16) + (bytes [3] << 24);
		}

		private static void IntToFourBytes (int val, byte [] bytes)
		{
			// gfoot: is this really a good idea?
			bytes [0] = (byte)val;
			bytes [1] = (byte)(val >> 8);
			bytes [2] = (byte)(val >> 16);
			bytes [3] = (byte)(val >> 24);
		}

		/* Compatibility functions to allow NLua work with Lua 5.1.5 and Lua 5.2.2 with the same dll interface.
		 * KopiLua methods to match KeraLua API */ 

		public static int LuaNetRegistryIndex ()
		{
			return LUA_REGISTRYINDEX;
		}

		public static void LuaNetPushGlobalTable (LuaState L) 
		{
			LuaPushValue (L, LUA_GLOBALSINDEX);
		}

		public static void LuaNetPopGlobalTable (LuaState L)
		{
			LuaReplace (L, LUA_GLOBALSINDEX);
		}

		public static void LuaNetSetGlobal (LuaState L, string name)
		{
			LuaSetGlobal (L, name);
		}

		public static void LuaNetGetGlobal (LuaState L, string name)
		{
			LuaGetGlobal (L, name);
		}
		
		public static int LuaNetPCall (LuaState L, int nargs, int nresults, int errfunc)
		{
			return LuaPCall (L, nargs, nresults, errfunc);
		}

#if !UNITY_3D
		[CLSCompliantAttribute(false)]
#endif
		public static int LuaNetLoadBuffer (LuaState L, string buff, uint sz, string name)
		{
			if (sz == 0)
				sz = (uint) strlen (buff);
			return LuaLLoadBuffer (L, buff, sz, name);
		}

#if !UNITY_3D
		[CLSCompliantAttribute(false)]
#endif
		public static int LuaNetLoadBuffer (LuaState L, byte [] buff, uint sz, string name)
		{
			return LuaLLoadBuffer (L, buff, sz, name);
		}

		public static int LuaNetLoadFile (LuaState L, string file)
		{
			return LuaLLoadFile (L, file);
		}

		public static double LuaNetToNumber (LuaState L, int idx)
		{
			return LuaToNumber (L, idx);
		}

		public static int LuaNetEqual (LuaState L, int idx1, int idx2)
		{
			return LuaEqual (L, idx1, idx2);
		}

#if !UNITY_3D
		[CLSCompliantAttribute(false)]
#endif
		public static void LuaNetPushLString (LuaState L, string s, uint len)
		{
			LuaPushLString (L, s, len);
		}

		public static int LuaNetIsStringStrict (LuaState L, int idx)
		{
			int t = LuaType (L, idx);
			return (t == LUA_TSTRING) ? 1 : 0;
		}

		public static LuaState LuaNetGetMainState (LuaState L1)
		{
			LuaGetField (L1, LUA_REGISTRYINDEX, "main_state");
			LuaState main = LuaToThread (L1, -1);
			LuaPop (L1, 1);
			return main;
		}
	}
}


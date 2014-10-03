using System;

namespace KopiLua
{
	public class LuaDebug {
		public int event_;
		public CharPtr name;	/* (n) */
		public CharPtr namewhat;	/* (n) `global', `local', `field', `method' */
		public CharPtr what;	/* (S) `Lua', `C', `main', `tail' */
		public CharPtr source;	/* (S) */
		public int currentline;	/* (l) */
		public int nups;		/* (u) number of upvalues */
		public int linedefined;	/* (S) */
		public int lastlinedefined;	/* (S) */
		public CharPtr short_src = new char[Lua.LUA_IDSIZE]; /* (S) */
		/* private part */
		public int i_ci;  /* active function */
		public string shortsrc
		{
			get { return short_src.ToString (); }
		}
	};
}


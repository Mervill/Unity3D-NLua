using System;

namespace KopiLua
{
	public struct LuaTag
	{
		public LuaTag (object tag): this ()
		{
			this.Tag = tag;
		}

		public object Tag { get; set; }
	}
}


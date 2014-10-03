using System;
using System.Diagnostics;

namespace KopiLua
{
	public class CharPtr
	{
		public char[] chars;
		public int index;

		public char this[int offset]
		{
			get { return chars[index + offset]; }
			set { chars[index + offset] = value; }
		}

#if !UNITY_3D
		[CLSCompliantAttribute(false)]
#endif
		public char this[uint offset]
		{
			get { return chars[index + offset]; }
			set { chars[index + offset] = value; }
		}
		public char this[long offset]
		{
			get { return chars[index + (int)offset]; }
			set { chars[index + (int)offset] = value; }
		}

		public static implicit operator CharPtr(string str) { return new CharPtr(str); }
		public static implicit operator CharPtr(char[] chars) { return new CharPtr(chars); }
		public static implicit operator CharPtr(byte[] bytes) { return new CharPtr(bytes); }

		public CharPtr()
		{
			this.chars = null;
			this.index = 0;
		}

		public CharPtr(string str)
		{
			this.chars = (str + '\0').ToCharArray();
			this.index = 0;
		}

		public CharPtr(CharPtr ptr)
		{
			this.chars = ptr.chars;
			this.index = ptr.index;
		}

		public CharPtr(CharPtr ptr, int index)
		{
			this.chars = ptr.chars;
			this.index = index;
		}

		public CharPtr(char[] chars)
		{
			this.chars = chars;
			this.index = 0;
		}

		public CharPtr(char[] chars, int index)
		{
			this.chars = chars;
			this.index = index;
		}

		public CharPtr(byte[] bytes)
		{
			this.chars = new char[bytes.Length];
			for (int i = 0; i < bytes.Length; i++)
			{
				this.chars[i] = (char)bytes[i];
			}

			this.index = 0;
		}

		public CharPtr(IntPtr ptr)
		{
			this.chars = new char[0];
			this.index = 0;
		}

		public static CharPtr operator +(CharPtr ptr, int offset) {return new CharPtr(ptr.chars, ptr.index+offset);}
		public static CharPtr operator -(CharPtr ptr, int offset) {return new CharPtr(ptr.chars, ptr.index-offset);}
#if !UNITY_3D
		[CLSCompliantAttribute(false)]
#endif
		public static CharPtr operator +(CharPtr ptr, uint offset) { return new CharPtr(ptr.chars, ptr.index + (int)offset); }
#if !UNITY_3D
		[CLSCompliantAttribute(false)]
#endif
		public static CharPtr operator -(CharPtr ptr, uint offset) { return new CharPtr(ptr.chars, ptr.index - (int)offset); }

		public void inc() { this.index++; }
		public void dec() { this.index--; }
		public CharPtr next() { return new CharPtr(this.chars, this.index + 1); }
		public CharPtr prev() { return new CharPtr(this.chars, this.index - 1); }
		public CharPtr add(int ofs) { return new CharPtr(this.chars, this.index + ofs); }
		public CharPtr sub(int ofs) { return new CharPtr(this.chars, this.index - ofs); }

		public static bool operator ==(CharPtr ptr, char ch) { return ptr[0] == ch; }
		public static bool operator ==(char ch, CharPtr ptr) { return ptr[0] == ch; }
		public static bool operator !=(CharPtr ptr, char ch) { return ptr[0] != ch; }
		public static bool operator !=(char ch, CharPtr ptr) { return ptr[0] != ch; }

		public static CharPtr operator +(CharPtr ptr1, CharPtr ptr2)
		{
			string result = "";
			for (int i = 0; ptr1[i] != '\0'; i++)
				result += ptr1[i];
			for (int i = 0; ptr2[i] != '\0'; i++)
				result += ptr2[i];
			return new CharPtr(result);
		}
		public static int operator -(CharPtr ptr1, CharPtr ptr2) {
			Debug.Assert(ptr1.chars == ptr2.chars); return ptr1.index - ptr2.index; }
		public static bool operator <(CharPtr ptr1, CharPtr ptr2) {
			Debug.Assert(ptr1.chars == ptr2.chars); return ptr1.index < ptr2.index; }
		public static bool operator <=(CharPtr ptr1, CharPtr ptr2) {
			Debug.Assert(ptr1.chars == ptr2.chars); return ptr1.index <= ptr2.index; }
		public static bool operator >(CharPtr ptr1, CharPtr ptr2) {
			Debug.Assert(ptr1.chars == ptr2.chars); return ptr1.index > ptr2.index; }
		public static bool operator >=(CharPtr ptr1, CharPtr ptr2) {
			Debug.Assert(ptr1.chars == ptr2.chars); return ptr1.index >= ptr2.index; }
		public static bool operator ==(CharPtr ptr1, CharPtr ptr2) {
			object o1 = ptr1 as CharPtr;
			object o2 = ptr2 as CharPtr;
			if ((o1 == null) && (o2 == null)) return true;
			if (o1 == null) return false;
			if (o2 == null) return false;
			return (ptr1.chars == ptr2.chars) && (ptr1.index == ptr2.index); }
		public static bool operator !=(CharPtr ptr1, CharPtr ptr2) {return !(ptr1 == ptr2); }

		public override bool Equals(object o)
		{
			return this == (o as CharPtr);
		}

		public override int GetHashCode()
		{
			return 0;
		}
		public override string ToString()
		{
			System.Text.StringBuilder result = new System.Text.StringBuilder ();
			for (int i = index; (i<chars.Length) && (chars[i] != '\0'); i++)
				result.Append (chars [i]);

			return result.ToString();
		}

		public string ToString (int length)
		{
			System.Text.StringBuilder result = new System.Text.StringBuilder ();
			for (int i = index; (i < chars.Length) && i < length; i++)
				result.Append (chars [i]);
			return result.ToString();
		}
	}

}


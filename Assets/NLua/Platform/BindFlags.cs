using System;

#if WINDOWS_PHONE
namespace System.Reflection
{
	public enum BindingFlags
	{
		Instance = 4,
		Static = 8,
		Public = 16,
	}
}
#endif
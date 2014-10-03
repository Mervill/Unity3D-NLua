/*
** $Id: loslib.c,v 1.19.1.3 2008/01/18 16:38:18 roberto Exp $
** Standard Operating System library
** See Copyright Notice in lua.h
*/

using System;
using System.Threading;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace KopiLua
{
	using TValue = Lua.LuaTypeValue;
	using StkId = Lua.LuaTypeValue;
	using lua_Integer = System.Int32;
	using lua_Number = System.Double;

	public partial class Lua
	{
		private static int OSPushResult (LuaState L, int i, CharPtr filename) {
		  int en = errno();  /* calls to Lua API may change this value */
		  if (i != 0) {
			LuaPushBoolean(L, 1);
			return 1;
		  }
		  else {
			LuaPushNil(L);
			LuaPushFString(L, "%s: %s", filename, strerror(en));
			LuaPushInteger(L, en);
			return 3;
		  }
		}


		private static int OSExecute (LuaState L) {
#if XBOX || SILVERLIGHT
			LuaLError(L, "os_execute not supported on XBox360");
#else
			CharPtr param = LuaLOptString(L, 1, null);
			if (param == null) {
				LuaPushInteger (L, 1);
				return 1;
			}
			CharPtr strCmdLine = "/C regenresx " + LuaLOptString(L, 1, null);
			System.Diagnostics.Process proc = new System.Diagnostics.Process();
			proc.EnableRaisingEvents=false;
			proc.StartInfo.FileName = "CMD.exe";
			proc.StartInfo.Arguments = strCmdLine.ToString();
			proc.Start();
			proc.WaitForExit();
			LuaPushInteger(L, proc.ExitCode);
#endif
			return 1;
		}


		private static int OSRemove (LuaState L) {
		  CharPtr filename = LuaLCheckString(L, 1);
		  int result = 1;
		  try {File.Delete(filename.ToString());} catch {result = 0;}
		  return OSPushResult(L, result, filename);
		}


		private static int OSRename (LuaState L) {
			CharPtr fromname = LuaLCheckString(L, 1);
		  CharPtr toname = LuaLCheckString(L, 2);
		  int result;
		  try
		  {
			  File.Move(fromname.ToString(), toname.ToString());
			  result = 0;
		  }
		  catch
		  {
			  result = 1; // todo: this should be a proper error code
		  }
		  return OSPushResult(L, result, fromname);
		}


		private static int OSTmpName (LuaState L) {
#if XBOX
			LuaLError(L, "os_tmpname not supported on Xbox360");
#else
		  LuaPushString(L, Path.GetTempFileName());
#endif
		  return 1;
		}


		private static int OSGetEnv (LuaState L) {
		  LuaPushString(L, getenv(LuaLCheckString(L, 1)));  /* if null push nil */
		  return 1;
		}


		private static int OSClock (LuaState L) {
		  long ticks = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
		  LuaPushNumber(L, ((lua_Number)ticks)/(lua_Number)1000);
		  return 1;
		}


		/*
		** {======================================================
		** Time/Date operations
		** { year=%Y, month=%m, day=%d, hour=%H, min=%M, sec=%S,
		**   wday=%w+1, yday=%j, isdst=? }
		** =======================================================
		*/

		private static void SetField (LuaState L, CharPtr key, int value) {
		  LuaPushInteger(L, value);
		  LuaSetField(L, -2, key);
		}

		private static void SetBoolField (LuaState L, CharPtr key, int value) {
		  if (value < 0)  /* undefined? */
			return;  /* does not set field */
		  LuaPushBoolean(L, value);
		  LuaSetField(L, -2, key);
		}

		private static int GetBoolField (LuaState L, CharPtr key) {
		  int res;
		  LuaGetField(L, -1, key);
		  res = LuaIsNil(L, -1) ? -1 : LuaToBoolean(L, -1);
		  LuaPop(L, 1);
		  return res;
		}

		private static int GetField (LuaState L, CharPtr key, int d) {
		  int res;
		  LuaGetField(L, -1, key);
		  if (LuaIsNumber(L, -1) != 0)
			res = (int)LuaToInteger(L, -1);
		  else {
			if (d < 0)
			  return LuaLError(L, "field " + LUA_QS + " missing in date table", key);
			res = d;
		  }
		  LuaPop(L, 1);
		  return res;
		}


		private static int OSDate (LuaState L) {
		  CharPtr s = LuaLOptString(L, 1, "%c");
		  DateTime stm;
		  if (s[0] == '!') {  /* UTC? */
			stm = DateTime.UtcNow;
			s.inc();  /* skip `!' */
		  }
		  else
			  stm = DateTime.Now;
		  if (strcmp(s, "*t") == 0) {
			LuaCreateTable(L, 0, 9);  /* 9 = number of fields */
			SetField(L, "sec", stm.Second);
			SetField(L, "min", stm.Minute);
			SetField(L, "hour", stm.Hour);
			SetField(L, "day", stm.Day);
			SetField(L, "month", stm.Month);
			SetField(L, "year", stm.Year);
			SetField(L, "wday", (int)stm.DayOfWeek);
			SetField(L, "yday", stm.DayOfYear);
			SetBoolField(L, "isdst", stm.IsDaylightSavingTime() ? 1 : 0);
		  }
		  else {
			CharPtr cc = new char[3];
			LuaLBuffer b = new LuaLBuffer();
			cc[0] = '%'; cc[2] = '\0';
			LuaLBuffInit(L, b);
			for (; s[0] != 0; s.inc()) {
			  if (s[0] != '%' || s[1] == '\0')  /* no conversion specifier? */
			    LuaLAddChar(b, s[0]);
			  else {
			    uint reslen;
			    CharPtr buff = new char[200];  /* should be big enough for any conversion result */
			    s.inc();
			    cc[1] = s[0];
			    reslen = strftime(buff, (uint)buff.chars.Length, cc, stm);
			    buff.index = 0;
			    LuaLAddLString(b, buff, reslen);
			  }
			}
			LuaLPushResult(b);
		  }
			return 1;
		}

		#region strftime c# implementation
		
		// This strftime implementation has been made following the
		// Sanos OS open-source strftime.c implementation at
		// http://www.jbox.dk/sanos/source/lib/strftime.c.html
		
		private static uint strftime(CharPtr s, uint maxsize, CharPtr format, DateTime t)
		{
			int sIndex = s.index;

			CharPtr p = StrFTimeFmt((format as object) == null ? "%c" : format, t, s, s.add((int)maxsize));
			if (p == s + maxsize) return 0;
			p[0] = '\0';

			return (uint)Math.Abs(s.index - sIndex);
		}

		private static CharPtr StrFTimeFmt(CharPtr baseFormat, DateTime t, CharPtr pt, CharPtr ptlim)
		{
			CharPtr format = new CharPtr(baseFormat);

			for (; format[0] != 0; format.inc())
			{

				if (format == '%')
				{

					format.inc();

					if (format == 'E')
					{
						format.inc(); // Alternate Era is ignored
					}
					else if (format == 'O')
					{
						format.inc(); // Alternate numeric symbols is ignored
					}

					switch (format[0])
					{
						case '\0':
							format.dec();
							break;

						case 'A': // Full day of week
							//pt = _add((t->tm_wday < 0 || t->tm_wday > 6) ? "?" : _days[t->tm_wday], pt, ptlim);
							pt = StrFTimeAdd(t.ToString("dddd"), pt, ptlim);
							continue;

						case 'a': // Abbreviated day of week
							//pt = _add((t->tm_wday < 0 || t->tm_wday > 6) ? "?" : _days_abbrev[t->tm_wday], pt, ptlim);
							pt = StrFTimeAdd(t.ToString("ddd"), pt, ptlim);
							continue;

						case 'B': // Full month name
							//pt = _add((t->tm_mon < 0 || t->tm_mon > 11) ? "?" : _months[t->tm_mon], pt, ptlim);
							pt = StrFTimeAdd(t.ToString("MMMM"), pt, ptlim);
							continue;

						case 'b': // Abbreviated month name
						case 'h': // Abbreviated month name
							//pt = _add((t->tm_mon < 0 || t->tm_mon > 11) ? "?" : _months_abbrev[t->tm_mon], pt, ptlim);
							pt = StrFTimeAdd(t.ToString("MMM"), pt, ptlim);
							continue;

						case 'C': // First two digits of year (a.k.a. Year divided by 100 and truncated to integer (00-99))
							//pt = _conv((t->tm_year + TM_YEAR_BASE) / 100, "%02d", pt, ptlim);
							pt = StrFTimeAdd(t.ToString("yyyy").Substring(0, 2), pt, ptlim);
							continue;

						case 'c': // Abbreviated date/time representation (e.g. Thu Aug 23 14:55:02 2001)
							pt = StrFTimeFmt("%a %b %e %H:%M:%S %Y", t, pt, ptlim);
							continue;

						case 'D': // Short MM/DD/YY date
							pt = StrFTimeFmt("%m/%d/%y", t, pt, ptlim);
							continue;

						case 'd': // Day of the month, zero-padded (01-31)
							//pt = _conv(t->tm_mday, "%02d", pt, ptlim);
							pt = StrFTimeAdd(t.ToString("dd"), pt, ptlim);
							continue;

						case 'e': // Day of the month, space-padded ( 1-31)
							//pt = _conv(t->tm_mday, "%2d", pt, ptlim);
							pt = StrFTimeAdd(t.Day.ToString().PadLeft(2, ' '), pt, ptlim);
							continue;

						case 'F': // Short YYYY-MM-DD date
							pt = StrFTimeFmt("%Y-%m-%d", t, pt, ptlim);
							continue;

						case 'H': // Hour in 24h format (00-23)
							//pt = _conv(t->tm_hour, "%02d", pt, ptlim);
							pt = StrFTimeAdd(t.ToString("HH"), pt, ptlim);
							continue;

						case 'I': // Hour in 12h format (01-12)
							//pt = _conv((t->tm_hour % 12) ? (t->tm_hour % 12) : 12, "%02d", pt, ptlim);
							pt = StrFTimeAdd(t.ToString("hh"), pt, ptlim);
							continue;

						case 'j': // Day of the year (001-366)
							pt = StrFTimeAdd(t.DayOfYear.ToString().PadLeft(3, ' '), pt, ptlim);
							continue;

						case 'k': // (Non-standard) // Hours in 24h format, space-padded ( 1-23)
							//pt = _conv(t->tm_hour, "%2d", pt, ptlim);
							pt = StrFTimeAdd(t.ToString("%H").PadLeft(2, ' '), pt, ptlim);
							continue;

						case 'l': // (Non-standard) // Hours in 12h format, space-padded ( 1-12)
							//pt = _conv((t->tm_hour % 12) ? (t->tm_hour % 12) : 12, "%2d", pt, ptlim);
							pt = StrFTimeAdd(t.ToString("%h").PadLeft(2, ' '), pt, ptlim);
							continue;

						case 'M': // Minute (00-59)
							//pt = _conv(t->tm_min, "%02d", pt, ptlim);
							pt = StrFTimeAdd(t.ToString("mm"), pt, ptlim);
							continue;

						case 'm': // Month as a decimal number (01-12)
							//pt = _conv(t->tm_mon + 1, "%02d", pt, ptlim);
							pt = StrFTimeAdd(t.ToString("MM"), pt, ptlim);
							continue;

						case 'n': // New-line character.
							pt = StrFTimeAdd(Environment.NewLine, pt, ptlim);
							continue;

						case 'p': // AM or PM designation (locale dependent).
							//pt = _add((t->tm_hour >= 12) ? "pm" : "am", pt, ptlim);
							pt = StrFTimeAdd(t.ToString("tt"), pt, ptlim);
							continue;

						case 'R': // 24-hour HH:MM time, equivalent to %H:%M
							pt = StrFTimeFmt("%H:%M", t, pt, ptlim);
							continue;

						case 'r': // 12-hour clock time (locale dependent).
							pt = StrFTimeFmt("%I:%M:%S %p", t, pt, ptlim);
							continue;

						case 'S': // Second ((00-59)
							//pt = _conv(t->tm_sec, "%02d", pt, ptlim);
							pt = StrFTimeAdd(t.ToString("ss"), pt, ptlim);
							continue;

						case 'T': // ISO 8601 time format (HH:MM:SS), equivalent to %H:%M:%S
							pt = StrFTimeFmt("%H:%M:%S", t, pt, ptlim);
							continue;

						case 't': // Horizontal-tab character
							pt = StrFTimeAdd("\t", pt, ptlim);
							continue;

						case 'U': // Week number with the first Sunday as the first day of week one (00-53)
							//pt = _conv((t->tm_yday + 7 - t->tm_wday) / 7, "%02d", pt, ptlim);
							pt = StrFTimeAdd(System.Globalization.CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(t, System.Globalization.CalendarWeekRule.FirstFullWeek, DayOfWeek.Sunday).ToString(), pt, ptlim);
							continue;

						case 'u': // ISO 8601 weekday as number with Monday as 1 (1-7) (locale independant).
							//pt = _conv((t->tm_wday == 0) ? 7 : t->tm_wday, "%d", pt, ptlim);
							pt = StrFTimeAdd(t.DayOfWeek == DayOfWeek.Sunday ? "7" : ((int)t.DayOfWeek).ToString(), pt, ptlim);
							continue;

						case 'G':   // ISO 8601 year (four digits)
						case 'g':  // ISO 8601 year (two digits)
						case 'V':   // ISO 8601 week number
							// See http://stackoverflow.com/questions/11154673/get-the-correct-week-number-of-a-given-date
							DateTime isoTime = t;
							DayOfWeek day = System.Globalization.CultureInfo.InvariantCulture.Calendar.GetDayOfWeek(isoTime);
							if (day >= DayOfWeek.Monday && day <= DayOfWeek.Wednesday)
							{
								isoTime = isoTime.AddDays(3);
							}

							if (format[0] == 'V') // ISO 8601 week number
							{
								int isoWeek = System.Globalization.CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(isoTime, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
								pt = StrFTimeAdd(isoWeek.ToString(), pt, ptlim);
							}
							else
							{
								string isoYear = System.Globalization.CultureInfo.InvariantCulture.Calendar.GetYear(isoTime).ToString(); // ISO 8601 year (four digits)

								if (format[0] == 'g') // ISO 8601 year (two digits)
								{
									isoYear = isoYear.Substring(isoYear.Length - 2, 2);
								}
								pt = StrFTimeAdd(isoYear, pt, ptlim);
							}

							continue;

						case 'W': // Week number with the first Monday as the first day of week one (00-53)
							//pt = _conv((t->tm_yday + 7 - (t->tm_wday ? (t->tm_wday - 1) : 6)) / 7, "%02d", pt, ptlim);
							pt = StrFTimeAdd(System.Globalization.CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(t, System.Globalization.CalendarWeekRule.FirstFullWeek, DayOfWeek.Monday).ToString(), pt, ptlim);
							continue;

						case 'w': // Weekday as a decimal number with Sunday as 0 (0-6)
							//pt = _conv(t->tm_wday, "%d", pt, ptlim);
							pt = StrFTimeAdd(((int)t.DayOfWeek).ToString(), pt, ptlim);
							continue;

						case 'X': // Long time representation (locale dependent)
							//pt = _fmt("%H:%M:%S", t, pt, ptlim); // fails to comply with spec!
							pt = StrFTimeAdd(t.ToString("%T"), pt, ptlim);
							continue;

						case 'x': // Short date representation (locale dependent)
							//pt = _fmt("%m/%d/%y", t, pt, ptlim); // fails to comply with spec!
							pt = StrFTimeAdd(t.ToString("%d"), pt, ptlim);
							continue;

						case 'y': // Last two digits of year (00-99)
							//pt = _conv((t->tm_year + TM_YEAR_BASE) % 100, "%02d", pt, ptlim);
							pt = StrFTimeAdd(t.ToString("yy"), pt, ptlim);
							continue;

						case 'Y': // Full year (all digits)
							//pt = _conv(t->tm_year + TM_YEAR_BASE, "%04d", pt, ptlim);
							pt = StrFTimeAdd(t.Year.ToString(), pt, ptlim);
							continue;

						case 'Z': // Timezone name or abbreviation (locale dependent) or nothing if unavailable (e.g. CDT)
							pt = StrFTimeAdd(TimeZoneInfo.Local.StandardName, pt, ptlim);
							continue;

						case 'z': // ISO 8601 offset from UTC in timezone (+/-hhmm), or nothing if unavailable
							TimeSpan ts = TimeZoneInfo.Local.GetUtcOffset(t);
							string offset = (ts.Ticks < 0 ? "-" : "+") + ts.TotalHours.ToString("#00") + ts.Minutes.ToString("00");
							pt = StrFTimeAdd(offset, pt, ptlim);
							continue;

						case '%': // Add '%'
							pt = StrFTimeAdd("%", pt, ptlim);
							continue;

						default:
							break;
					}
				}

				if (pt == ptlim) break;

				pt[0] = format[0];
				pt.inc();
			}

			return pt;
		}

		private static CharPtr StrFTimeAdd(CharPtr str, CharPtr pt, CharPtr ptlim)
		{
			pt[0] = str[0];
			str = str.next();

			while (pt < ptlim && pt[0] != 0)
			{
				pt.inc();

				pt[0] = str[0];
				str = str.next();
			}
			return pt;
		} 
		#endregion

		private static int OSTime (LuaState L) {
		  DateTime t;
		  if (LuaIsNoneOrNil(L, 1))  /* called without args? */
			t = DateTime.Now;  /* get current time */
		  else {
			LuaLCheckType(L, 1, LUA_TTABLE);
			LuaSetTop(L, 1);  /* make sure table is at the top */
			int sec = GetField(L, "sec", 0);
			int min = GetField(L, "min", 0);
			int hour = GetField(L, "hour", 12);
			int day = GetField(L, "day", -1);
			int month = GetField(L, "month", -1) - 1;
			int year = GetField(L, "year", -1) - 1900;
			/*int isdst = */GetBoolField(L, "isdst");	// todo: implement this - mjf
			t = new DateTime(year, month, day, hour, min, sec);
		  }
		  LuaPushNumber(L, t.Ticks);
		  return 1;
		}


		private static int OSDiffTime (LuaState L) {
		  long ticks = (long)LuaLCheckNumber(L, 1) - (long)LuaLOptNumber(L, 2, 0);
		  LuaPushNumber(L, ticks/TimeSpan.TicksPerSecond);
		  return 1;
		}

		/* }====================================================== */

		// locale not supported yet
		private static int OSSetLocale (LuaState L) {		  
		  /*
		  static string[] cat = {LC_ALL, LC_COLLATE, LC_CTYPE, LC_MONETARY,
							  LC_NUMERIC, LC_TIME};
		  static string[] catnames[] = {"all", "collate", "ctype", "monetary",
			 "numeric", "time", null};
		  CharPtr l = luaL_optstring(L, 1, null);
		  int op = luaL_checkoption(L, 2, "all", catnames);
		  lua_pushstring(L, setlocale(cat[op], l));
		  */
		  CharPtr l = LuaLOptString(L, 1, null);
		  LuaPushString(L, "C");
		  return (l.ToString() == "C") ? 1 : 0;
		}


		private static int OSExit (LuaState L) {
#if XBOX
			LuaLError(L, "os_exit not supported on XBox360");
#else
#if SILVERLIGHT
            throw new SystemException();
#else
			Environment.Exit(EXIT_SUCCESS);
#endif
#endif
			return 0;
		}

		private readonly static LuaLReg[] syslib = {
		  new LuaLReg("clock",     OSClock),
		  new LuaLReg("date",      OSDate),
		  new LuaLReg("difftime",  OSDiffTime),
		  new LuaLReg("execute",   OSExecute),
		  new LuaLReg("exit",      OSExit),
		  new LuaLReg("getenv",    OSGetEnv),
		  new LuaLReg("remove",    OSRemove),
		  new LuaLReg("rename",    OSRename),
		  new LuaLReg("setlocale", OSSetLocale),
		  new LuaLReg("time",      OSTime),
		  new LuaLReg("tmpname",   OSTmpName),
		  new LuaLReg(null, null)
		};

		/* }====================================================== */



		public static int LuaOpenOS (LuaState L) {
		  LuaLRegister(L, LUA_OSLIBNAME, syslib);
		  return 1;
		}

	}
}

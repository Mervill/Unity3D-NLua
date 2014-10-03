/*
** $Id: liolib.c,v 2.73.1.3 2008/01/18 17:47:43 roberto Exp $
** Standard I/O (and system) library
** See Copyright Notice in lua.h
*/

using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace KopiLua
{
	using LuaNumberType = System.Double;
	using LuaIntegerType = System.Int32;

	public class FilePtr
	{
		public Stream file;
	}

	public partial class Lua
	{
		public const int IOINPUT	= 1;
		public const int IOOUTPUT	= 2;

		private static readonly string[] fnames = { "input", "output" };


		private static int PushResult (LuaState L, int i, CharPtr filename) {
		  int en = errno();  /* calls to Lua API may change this value */
		  if (i != 0) {
			LuaPushBoolean(L, 1);
			return 1;
		  }
		  else {
			LuaPushNil(L);
			if (filename != null)
				LuaPushFString(L, "%s: %s", filename, strerror(en));
			else
				LuaPushFString(L, "%s", strerror(en));
			LuaPushInteger(L, en);
			return 3;
		  }
		}


		private static void FileError (LuaState L, int arg, CharPtr filename) {
		  LuaPushFString(L, "%s: %s", filename, strerror(errno()));
		  LuaLArgError(L, arg, LuaToString(L, -1));
		}


		public static FilePtr ToFilePointer(LuaState L) { return (FilePtr)LuaLCheckUData(L, 1, LUA_FILEHANDLE); }


		private static int GetIOType (LuaState L) {
		  object ud;
		  LuaLCheckAny(L, 1);
		  ud = LuaToUserData(L, 1);
		  LuaGetField(L, LUA_REGISTRYINDEX, LUA_FILEHANDLE);
		  if (ud == null || (LuaGetMetatable(L, 1)==0) || (LuaRawEqual(L, -2, -1)==0))
			LuaPushNil(L);  /* not a file */
		  else if ( (ud as FilePtr).file == null)
			LuaPushLiteral(L, "closed file");
		  else
			LuaPushLiteral(L, "file");
		  return 1;
		}


		private static Stream ToFile (LuaState L) {
		  FilePtr f = ToFilePointer(L);
		  if (f.file == null)
			LuaLError(L, "attempt to use a closed file");
		  return f.file;
		}



		/*
		** When creating file files, always creates a `closed' file file
		** before opening the actual file; so, if there is a memory error, the
		** file is not left opened.
		*/
		private static FilePtr NewFile (LuaState L) {

		  FilePtr pf = (FilePtr)LuaNewUserData(L, typeof(FilePtr));
		  pf.file = null;  /* file file is currently `closed' */
		  LuaLGetMetatable(L, LUA_FILEHANDLE);
		  LuaSetMetatable(L, -2);
		  return pf;
		}


		/*
		** function to (not) close the standard files stdin, stdout, and stderr
		*/
		private static int IoNoClose (LuaState L) {
		  LuaPushNil(L);
		  LuaPushLiteral(L, "cannot close standard file");
		  return 2;
		}


		/*
		** function to close 'popen' files
		*/
		private static int IoPClose (LuaState L) {
		  FilePtr p = ToFilePointer(L);
		  int ok = (LuaPClose(L, p.file) == 0) ? 1 : 0;
		  p.file = null;
		  return PushResult(L, ok, null);
		}


		/*
		** function to close regular files
		*/
		private static int IoFClose (LuaState L) {
		  FilePtr p = ToFilePointer(L);
		  int ok = (fclose(p.file) == 0) ? 1 : 0;
		  p.file = null;
		  return PushResult(L, ok, null);
		}


		private static int AuxClose (LuaState L) {
		  LuaGetFEnv(L, 1);
		  LuaGetField(L, -1, "__close");
		  return (LuaToCFunction(L, -1))(L);
		}


		private static int IoClose (LuaState L) {
		  if (LuaIsNone(L, 1))
			LuaRawGetI(L, LUA_ENVIRONINDEX, IOOUTPUT);
		  ToFile(L);  /* make sure argument is a file */
		  return AuxClose(L);
		}


		private static int IoGC (LuaState L) {
		  Stream f = ToFilePointer(L).file;
		  /* ignore closed files */
		  if (f != null)
			AuxClose(L);
		  return 0;
		}


		private static int IoToString (LuaState L) {
		  Stream f = ToFilePointer(L).file;
		  if (f == null)
			LuaPushLiteral(L, "file (closed)");
		  else
			LuaPushFString(L, "file (%p)", f);
		  return 1;
		}


		private static int IoOpen (LuaState L) {
		  CharPtr filename = LuaLCheckString(L, 1);
		  CharPtr mode = LuaLOptString(L, 2, "r");
		  FilePtr pf = NewFile(L);
		  pf.file = fopen(filename, mode);
		  return (pf.file == null) ? PushResult(L, 0, filename) : 1;
		}


		/*
		** this function has a separated environment, which defines the
		** correct __close for 'popen' files
		*/
		private static int IoPopen (LuaState L) {
		  CharPtr filename = LuaLCheckString(L, 1);
		  CharPtr mode = LuaLOptString(L, 2, "r");
		  FilePtr pf = NewFile(L);
		  pf.file = LuaPopen(L, filename, mode);
		  return (pf.file == null) ? PushResult(L, 0, filename) : 1;
		}


		private static int IoTmpFile (LuaState L) {
		  FilePtr pf = NewFile(L);
#if XBOX
			LuaLError(L, "io_tmpfile not supported on Xbox360");
#else
		  pf.file = tmpfile();
#endif
		  return (pf.file == null) ? PushResult(L, 0, null) : 1;
		}


		private static Stream GetIOFile (LuaState L, int findex) {
		  Stream f;
		  LuaRawGetI(L, LUA_ENVIRONINDEX, findex);
		  f = (LuaToUserData(L, -1) as FilePtr).file;
		  if (f == null)
			LuaLError(L, "standard %s file is closed", fnames[findex - 1]);
		  return f;
		}


		private static int GIOFile (LuaState L, int f, CharPtr mode) {
		  if (!LuaIsNoneOrNil(L, 1)) {
			CharPtr filename = LuaToString(L, 1);
			if (filename != null) {
			  FilePtr pf = NewFile(L);
			  pf.file = fopen(filename, mode);
			  if (pf.file == null)
				FileError(L, 1, filename);
			}
			else {
			  ToFile(L);  /* check that it's a valid file file */
			  LuaPushValue(L, 1);
			}
			LuaRawSetI(L, LUA_ENVIRONINDEX, f);
		  }
		  /* return current value */
		  LuaRawGetI(L, LUA_ENVIRONINDEX, f);
		  return 1;
		}


		private static int IoInput (LuaState L) {
		  return GIOFile(L, IOINPUT, "r");
		}


		private static int IoOutput (LuaState L) {
		  return GIOFile(L, IOOUTPUT, "w");
		}

		private static void AuxLines (LuaState L, int idx, int toclose) {
		  LuaPushValue(L, idx);
		  LuaPushBoolean(L, toclose);  /* close/not close file when finished */
		  LuaPushCClosure(L, IoReadLine, 2);
		}


		private static int FLines (LuaState L) {
		  ToFile(L);  /* check that it's a valid file file */
		  AuxLines(L, 1, 0);
		  return 1;
		}


		private static int IoLines (LuaState L) {
		  if (LuaIsNoneOrNil(L, 1)) {  /* no arguments? */
			/* will iterate over default input */
			LuaRawGetI(L, LUA_ENVIRONINDEX, IOINPUT);
			return FLines(L);
		  }
		  else {
			CharPtr filename = LuaLCheckString(L, 1);
			FilePtr pf = NewFile(L);
			pf.file = fopen(filename, "r");
			if (pf.file == null)
			  FileError(L, 1, filename);
			AuxLines(L, LuaGetTop(L), 1);
			return 1;
		  }
		}


		/*
		** {======================================================
		** READ
		** =======================================================
		*/


		private static int ReadNumber (LuaState L, Stream f) {
		  //LuaNumberType d;
			object[] parms = { (object)(double)0.0 };
			if (fscanf (f, LUA_NUMBER_SCAN, parms) == 1) {
				LuaPushNumber (L, (double)parms [0]);
				return 1;
			} 
			else {
				LuaPushNil(L);  /* "result" to be removed */
				return 0;  /* read fails */
			}
		}


		private static int TestEof (LuaState L, Stream f) {
		  int c = getc(f);
		  ungetc(c, f);
		  LuaPushLString(L, null, 0);
		  return (c != EOF) ? 1 : 0;
		}


		private static int ReadLine (LuaState L, Stream f) {
		  LuaLBuffer b = new LuaLBuffer();
		  LuaLBuffInit(L, b);
		  for (;;) {
			uint l;
			CharPtr p = LuaLPrepBuffer(b);
			if (fgets(p, f) == null) {  /* eof? */
			  LuaLPushResult(b);  /* close buffer */
				return (LuaObjectLen(L, -1) > 0) ? 1 : 0;  /* check whether read something */
			}
			l = (uint)strlen(p);
			if (l == 0 || p[l-1] != '\n')
			  LuaLAddSize(b, (int)l);
			else {
			  LuaLAddSize(b, (int)(l - 1));  /* do not include `eol' */
			  LuaLPushResult(b);  /* close buffer */
			  return 1;  /* read at least an `eol' */
			}
		  }
		}


		private static int ReadChars (LuaState L, Stream f, uint n) {
		  uint rlen;  /* how much to read */
		  uint nr;  /* number of chars actually read */
		  LuaLBuffer b = new LuaLBuffer();
		  LuaLBuffInit(L, b);
		  rlen = LUAL_BUFFERSIZE;  /* try to read that much each time */
		  do {
			CharPtr p = LuaLPrepBuffer(b);
			if (rlen > n) rlen = n;  /* cannot read more than asked */
			nr = (uint)fread(p, GetUnmanagedSize(typeof(char)), (int)rlen, f);
			LuaLAddSize(b, (int)nr);
			n -= nr;  /* still have to read `n' chars */
		  } while (n > 0 && nr == rlen);  /* until end of count or eof */
		  LuaLPushResult(b);  /* close buffer */
		  return (n == 0 || LuaObjectLen(L, -1) > 0) ? 1 : 0;
		}


		private static int GRead (LuaState L, Stream f, int first) {
		  int nargs = LuaGetTop(L) - 1;
		  int success;
		  int n;
		  clearerr(f);
		  if (nargs == 0) {  /* no arguments? */
			success = ReadLine(L, f);
			n = first+1;  /* to return 1 result */
		  }
		  else {  /* ensure stack space for all results and for auxlib's buffer */
			LuaLCheckStack(L, nargs+LUA_MINSTACK, "too many arguments");
			success = 1;
			for (n = first; (nargs-- != 0) && (success!=0); n++) {
			  if (LuaType(L, n) == LUA_TNUMBER) {
				uint l = (uint)LuaToInteger(L, n);
				success = (l == 0) ? TestEof(L, f) : ReadChars(L, f, l);
			  }
			  else {
				CharPtr p = LuaToString(L, n);
				LuaLArgCheck(L, (p!=null) && (p[0] == '*'), n, "invalid option");
				switch (p[1]) {
				  case 'n':  /* number */
					success = ReadNumber(L, f);
					break;
				  case 'l':  /* line */
					success = ReadLine(L, f);
					break;
				  case 'a':  /* file */
					ReadChars(L, f, ~((uint)0));  /* read MAX_uint chars */
					success = 1; /* always success */
					break;
				  default:
					return LuaLArgError(L, n, "invalid format");
				}
			  }
			}
		  }
		  if (ferror(f)!=0)
			return PushResult(L, 0, null);
		  if (success==0) {
			LuaPop(L, 1);  /* remove last result */
			LuaPushNil(L);  /* push nil instead */
		  }
		  return n - first;
		}


		private static int IoRead (LuaState L) {
		  return GRead(L, GetIOFile(L, IOINPUT), 1);
		}


		private static int FRead (LuaState L) {
		  return GRead(L, ToFile(L), 2);
		}


		private static int IoReadLine (LuaState L) {
		  Stream f = (LuaToUserData(L, LuaUpValueIndex(1)) as FilePtr).file;
		  int sucess;
		  if (f == null)  /* file is already closed? */
			LuaLError(L, "file is already closed");
		  sucess = ReadLine(L, f);
		  if (ferror(f)!=0)
			return LuaLError(L, "%s", strerror(errno()));
		  if (sucess != 0) return 1;
		  else {  /* EOF */
			if (LuaToBoolean(L, LuaUpValueIndex(2)) != 0) {  /* generator created file? */
			  LuaSetTop(L, 0);
			  LuaPushValue(L, LuaUpValueIndex(1));
			  AuxClose(L);  /* close it */
			}
			return 0;
		  }
		}

		/* }====================================================== */


		private static int GWrite (LuaState L, Stream f, int arg) {
		  int nargs = LuaGetTop(L) - 1;
		  int status = 1;
		  for (; (nargs--) != 0; arg++) {
			if (LuaType(L, arg) == LUA_TNUMBER) {
			  /* optimization: could be done exactly as for strings */
			  status = ((status!=0) &&
				  (fprintf(f, LUA_NUMBER_FMT, LuaToNumber(L, arg)) > 0)) ? 1 : 0;
			}
			else {
			  uint l;
			  CharPtr s = LuaLCheckLString(L, arg, out l);
			  status = ((status!=0) && (fwrite(s, GetUnmanagedSize(typeof(char)), (int)l, f) == l)) ? 1 : 0;
			}
		  }
		  return PushResult(L, status, null);
		}


		private static int IoWrite (LuaState L) {
		  return GWrite(L, GetIOFile(L, IOOUTPUT), 1);
		}


		private static int FWrite (LuaState L) {
		  return GWrite(L, ToFile(L), 2);
		}

		

		private static int FSeek (LuaState L) {
		  int[] mode = { SEEK_SET, SEEK_CUR, SEEK_END };
		  CharPtr[] modenames = { "set", "cur", "end", null };
		  Stream f = ToFile(L);
		  int op = LuaLCheckOption(L, 2, "cur", modenames);
		  long offset = LuaLOptLong(L, 3, 0);
		  op = fseek(f, offset, mode[op]);
		  if (op != 0)
			return PushResult(L, 0, null);  /* error */
		  else {
			LuaPushInteger(L, ftell(f));
			return 1;
		  }
		}

		private static int FSetVBuf (LuaState L) {
		  CharPtr[] modenames = { "no", "full", "line", null };
		  int[] mode = { _IONBF, _IOFBF, _IOLBF };
		  Stream f = ToFile(L);
		  int op = LuaLCheckOption(L, 2, null, modenames);
		  LuaIntegerType sz = LuaLOptInteger(L, 3, LUAL_BUFFERSIZE);
		  int res = setvbuf(f, null, mode[op], (uint)sz);
		  return PushResult(L, (res == 0) ? 1 : 0, null);
		}



		private static int IoFlush (LuaState L) {
			int result = 1;
			try {GetIOFile(L, IOOUTPUT).Flush();} catch {result = 0;}
		  return PushResult(L, result, null);
		}


		private static int FFlush (LuaState L) {
			int result = 1;
			try {ToFile(L).Flush();} catch {result = 0;}
			return PushResult(L, result, null);
		}


		private readonly static LuaLReg[] iolib = {
		  new LuaLReg("close", IoClose),
		  new LuaLReg("flush", IoFlush),
		  new LuaLReg("input", IoInput),
		  new LuaLReg("lines", IoLines),
		  new LuaLReg("open", IoOpen),
		  new LuaLReg("output", IoOutput),
		  new LuaLReg("popen", IoPopen),
		  new LuaLReg("read", IoRead),
		  new LuaLReg("tmpfile", IoTmpFile),
		  new LuaLReg("type", GetIOType),
		  new LuaLReg("write", IoWrite),
		  new LuaLReg(null, null)
		};


		private readonly static LuaLReg[] flib = {
		  new LuaLReg("close", IoClose),
		  new LuaLReg("flush", FFlush),
		  new LuaLReg("lines", FLines),
		  new LuaLReg("read", FRead),
		  new LuaLReg("seek", FSeek),
		  new LuaLReg("setvbuf", FSetVBuf),
		  new LuaLReg("write", FWrite),
		  new LuaLReg("__gc", IoGC),
		  new LuaLReg("__tostring", IoToString),
		  new LuaLReg(null, null)
		};


		private static void CreateMeta (LuaState L) {
		  LuaLNewMetatable(L, LUA_FILEHANDLE);  /* create metatable for file files */
		  LuaPushValue(L, -1);  /* push metatable */
		  LuaSetField(L, -2, "__index");  /* metatable.__index = metatable */
		  LuaLRegister(L, null, flib);  /* file methods */
		}


		private static void CreateStdFile (LuaState L, Stream f, int k, CharPtr fname) {
		  NewFile(L).file = f;
		  if (k > 0) {
			LuaPushValue(L, -1);
			LuaRawSetI(L, LUA_ENVIRONINDEX, k);
		  }
		  LuaPushValue(L, -2);  /* copy environment */
		  LuaSetFEnv(L, -2);  /* set it */
		  LuaSetField(L, -3, fname);
		}


		private static void NewFEnv (LuaState L, LuaNativeFunction cls) {
		  LuaCreateTable(L, 0, 1);
		  LuaPushCFunction(L, cls);
		  LuaSetField(L, -2, "__close");
		}


		public static int LuaOpenIo (LuaState L) {
		  CreateMeta(L);
		  /* create (private) environment (with fields IO_INPUT, IO_OUTPUT, __close) */
		  NewFEnv(L, IoFClose);
		  LuaReplace(L, LUA_ENVIRONINDEX);
		  /* open library */
		  LuaLRegister(L, LUA_IOLIBNAME, iolib);
		  /* create (and set) default files */
		  NewFEnv(L, IoNoClose);  /* close function for default files */
		  CreateStdFile(L, stdin, IOINPUT, "stdin");
		  CreateStdFile(L, stdout, IOOUTPUT, "stdout");
		  CreateStdFile(L, stderr, 0, "stderr");
		  LuaPop(L, 1);  /* pop environment for default files */
		  LuaGetField(L, -1, "popen");
		  NewFEnv(L, IoPClose);  /* create environment for 'popen' */
		  LuaSetFEnv(L, -2);  /* set fenv for 'popen' */
		  LuaPop(L, 1);  /* pop 'popen' */
		  return 1;
		}

	}
}

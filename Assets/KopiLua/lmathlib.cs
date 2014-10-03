/*
** $Id: lmathlib.c,v 1.67.1.1 2007/12/27 13:02:25 roberto Exp $
** Standard mathematical library
** See Copyright Notice in lua.h
*/

using System;
using System.Collections.Generic;
using System.Text;

namespace KopiLua
{
	using LuaNumberType = System.Double;

	public partial class Lua
	{
		public const double PI = 3.14159265358979323846;
		public const double RADIANS_PER_DEGREE = PI / 180.0;



		private static int MathAbs (LuaState L) {
		  LuaPushNumber(L, Math.Abs(LuaLCheckNumber(L, 1)));
		  return 1;
		}

		private static int MathSin (LuaState L) {
		  LuaPushNumber(L, Math.Sin(LuaLCheckNumber(L, 1)));
		  return 1;
		}

		private static int MathSinH (LuaState L) {
		  LuaPushNumber(L, Math.Sinh(LuaLCheckNumber(L, 1)));
		  return 1;
		}

		private static int MathCos (LuaState L) {
		  LuaPushNumber(L, Math.Cos(LuaLCheckNumber(L, 1)));
		  return 1;
		}

		private static int MathCosH (LuaState L) {
		  LuaPushNumber(L, Math.Cosh(LuaLCheckNumber(L, 1)));
		  return 1;
		}

		private static int MathTan (LuaState L) {
		  LuaPushNumber(L, Math.Tan(LuaLCheckNumber(L, 1)));
		  return 1;
		}

		private static int MathTanH (LuaState L) {
		  LuaPushNumber(L, Math.Tanh(LuaLCheckNumber(L, 1)));
		  return 1;
		}

		private static int MathASin (LuaState L) {
		  LuaPushNumber(L, Math.Asin(LuaLCheckNumber(L, 1)));
		  return 1;
		}

		private static int MathACos (LuaState L) {
		  LuaPushNumber(L, Math.Acos(LuaLCheckNumber(L, 1)));
		  return 1;
		}

		private static int MathATan (LuaState L) {
		  LuaPushNumber(L, Math.Atan(LuaLCheckNumber(L, 1)));
		  return 1;
		}

		private static int MathATan2 (LuaState L) {
		  LuaPushNumber(L, Math.Atan2(LuaLCheckNumber(L, 1), LuaLCheckNumber(L, 2)));
		  return 1;
		}

		private static int MathCeil (LuaState L) {
		  LuaPushNumber(L, Math.Ceiling(LuaLCheckNumber(L, 1)));
		  return 1;
		}

		private static int MathFloor (LuaState L) {
		  LuaPushNumber(L, Math.Floor(LuaLCheckNumber(L, 1)));
		  return 1;
		}

		private static int MathFMod (LuaState L) {
		  LuaPushNumber(L, fmod(LuaLCheckNumber(L, 1), LuaLCheckNumber(L, 2)));
		  return 1;
		}

		private static int MathModF (LuaState L) {
		  double ip;
		  double fp = modf(LuaLCheckNumber(L, 1), out ip);
		  LuaPushNumber(L, ip);
		  LuaPushNumber(L, fp);
		  return 2;
		}

		private static int MathSqrt (LuaState L) {
		  LuaPushNumber(L, Math.Sqrt(LuaLCheckNumber(L, 1)));
		  return 1;
		}

		private static int MathPow (LuaState L) {
		  LuaPushNumber(L, Math.Pow(LuaLCheckNumber(L, 1), LuaLCheckNumber(L, 2)));
		  return 1;
		}

		private static int MathLog (LuaState L) {
		  LuaPushNumber(L, Math.Log(LuaLCheckNumber(L, 1)));
		  return 1;
		}

		private static int MathLog10 (LuaState L) {
		  LuaPushNumber(L, Math.Log10(LuaLCheckNumber(L, 1)));
		  return 1;
		}

		private static int MathExp (LuaState L) {
		  LuaPushNumber(L, Math.Exp(LuaLCheckNumber(L, 1)));
		  return 1;
		}

		private static int MathDeg (LuaState L) {
		  LuaPushNumber(L, LuaLCheckNumber(L, 1)/RADIANS_PER_DEGREE);
		  return 1;
		}

		private static int MathRad (LuaState L) {
		  LuaPushNumber(L, LuaLCheckNumber(L, 1)*RADIANS_PER_DEGREE);
		  return 1;
		}

		private static int MathFRExp (LuaState L) {
		  int e;
		  LuaPushNumber(L, frexp(LuaLCheckNumber(L, 1), out e));
		  LuaPushInteger(L, e);
		  return 2;
		}

		private static int MathLDExp (LuaState L) {
		  LuaPushNumber(L, ldexp(LuaLCheckNumber(L, 1), LuaLCheckInt(L, 2)));
		  return 1;
		}



		private static int MahMin (LuaState L) {
		  int n = LuaGetTop(L);  /* number of arguments */
		  LuaNumberType dmin = LuaLCheckNumber(L, 1);
		  int i;
		  for (i=2; i<=n; i++) {
			LuaNumberType d = LuaLCheckNumber(L, i);
			if (d < dmin)
			  dmin = d;
		  }
		  LuaPushNumber(L, dmin);
		  return 1;
		}


		private static int MathMax (LuaState L) {
		  int n = LuaGetTop(L);  /* number of arguments */
		  LuaNumberType dmax = LuaLCheckNumber(L, 1);
		  int i;
		  for (i=2; i<=n; i++) {
			LuaNumberType d = LuaLCheckNumber(L, i);
			if (d > dmax)
			  dmax = d;
		  }
		  LuaPushNumber(L, dmax);
		  return 1;
		}

		private static Random rng = new Random();

		private static int MathRandom (LuaState L) {
		  /* the `%' avoids the (rare) case of r==1, and is needed also because on
			 some systems (SunOS!) `rand()' may return a value larger than RAND_MAX */
		  //LuaNumberType r = (LuaNumberType)(rng.Next()%RAND_MAX) / (LuaNumberType)RAND_MAX;
			LuaNumberType r = (LuaNumberType)rng.NextDouble();
		  switch (LuaGetTop(L)) {  /* check number of arguments */
			case 0: {  /* no arguments */
			  LuaPushNumber(L, r);  /* Number between 0 and 1 */
			  break;
			}
			case 1: {  /* only upper limit */
			  int u = LuaLCheckInt(L, 1);
			  LuaLArgCheck(L, 1<=u, 1, "interval is empty");
			  LuaPushNumber(L, Math.Floor(r*u)+1);  /* int between 1 and `u' */
			  break;
			}
			case 2: {  /* lower and upper limits */
			  int l = LuaLCheckInt(L, 1);
			  int u = LuaLCheckInt(L, 2);
			  LuaLArgCheck(L, l<=u, 2, "interval is empty");
			  LuaPushNumber(L, Math.Floor(r * (u - l + 1)) + l);  /* int between `l' and `u' */
			  break;
			}
			default: return LuaLError(L, "wrong number of arguments");
		  }
		  return 1;
		}


		private static int MathRandomSeed (LuaState L) {
		  //srand(luaL_checkint(L, 1));
			rng = new Random(LuaLCheckInt(L, 1));
		  return 0;
		}


		private readonly static LuaLReg[] mathlib = {
		  new LuaLReg("abs",   MathAbs),
		  new LuaLReg("acos",  MathACos),
		  new LuaLReg("asin",  MathASin),
		  new LuaLReg("atan2", MathATan2),
		  new LuaLReg("atan",  MathATan),
		  new LuaLReg("ceil",  MathCeil),
		  new LuaLReg("cosh",   MathCosH),
		  new LuaLReg("cos",   MathCos),
		  new LuaLReg("deg",   MathDeg),
		  new LuaLReg("exp",   MathExp),
		  new LuaLReg("floor", MathFloor),
		  new LuaLReg("fmod",   MathFMod),
		  new LuaLReg("frexp", MathFRExp),
		  new LuaLReg("ldexp", MathLDExp),
		  new LuaLReg("log10", MathLog10),
		  new LuaLReg("log",   MathLog),
		  new LuaLReg("max",   MathMax),
		  new LuaLReg("min",   MahMin),
		  new LuaLReg("modf",   MathModF),
		  new LuaLReg("pow",   MathPow),
		  new LuaLReg("rad",   MathRad),
		  new LuaLReg("random",     MathRandom),
		  new LuaLReg("randomseed", MathRandomSeed),
		  new LuaLReg("sinh",   MathSinH),
		  new LuaLReg("sin",   MathSin),
		  new LuaLReg("sqrt",  MathSqrt),
		  new LuaLReg("tanh",   MathTanH),
		  new LuaLReg("tan",   MathTan),
		  new LuaLReg(null, null)
		};


		/*
		** Open math library
		*/
		public static int LuaOpenMath (LuaState L) {
		  LuaLRegister(L, LUA_MATHLIBNAME, mathlib);
		  LuaPushNumber(L, PI);
		  LuaSetField(L, -2, "pi");
		  LuaPushNumber(L, HUGE_VAL);
		  LuaSetField(L, -2, "huge");
		#if LUA_COMPAT_MOD
		  LuaGetField(L, -1, "fmod");
		  LuaSetField(L, -2, "mod");
		#endif
		  return 1;
		}

	}
}

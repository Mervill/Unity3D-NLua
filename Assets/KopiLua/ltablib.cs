/*
** $Id: ltablib.c,v 1.38.1.3 2008/02/14 16:46:58 roberto Exp $
** Library for Table Manipulation
** See Copyright Notice in lua.h
*/

using System;
using System.Collections.Generic;
using System.Text;

namespace KopiLua
{
	using lua_Number = System.Double;

	public partial class Lua
	{
		private static int aux_getn(LuaState L, int n)	{LuaLCheckType(L, n, LUA_TTABLE); return LuaLGetN(L, n);}

		private static int foreachi (LuaState L) {
		  int i;
		  int n = aux_getn(L, 1);
		  LuaLCheckType(L, 2, LUA_TFUNCTION);
		  for (i=1; i <= n; i++) {
			LuaPushValue(L, 2);  /* function */
			LuaPushInteger(L, i);  /* 1st argument */
			LuaRawGetI(L, 1, i);  /* 2nd argument */
			LuaCall(L, 2, 1);
			if (!LuaIsNil(L, -1))
			  return 1;
			LuaPop(L, 1);  /* remove nil result */
		  }
		  return 0;
		}


		private static int _foreach (LuaState L) {
		  LuaLCheckType(L, 1, LUA_TTABLE);
		  LuaLCheckType(L, 2, LUA_TFUNCTION);
		  LuaPushNil(L);  /* first key */
		  while (LuaNext(L, 1) != 0) {
			LuaPushValue(L, 2);  /* function */
			LuaPushValue(L, -3);  /* key */
			LuaPushValue(L, -3);  /* value */
			LuaCall(L, 2, 1);
			if (!LuaIsNil(L, -1))
			  return 1;
			LuaPop(L, 2);  /* remove value and result */
		  }
		  return 0;
		}


		private static int maxn (LuaState L) {
		  lua_Number max = 0;
		  LuaLCheckType(L, 1, LUA_TTABLE);
		  LuaPushNil(L);  /* first key */
		  while (LuaNext(L, 1) != 0) {
			LuaPop(L, 1);  /* remove value */
			if (LuaType(L, -1) == LUA_TNUMBER) {
			  lua_Number v = LuaToNumber(L, -1);
			  if (v > max) max = v;
			}
		  }
		  LuaPushNumber(L, max);
		  return 1;
		}


		private static int getn (LuaState L) {
		  LuaPushInteger(L, aux_getn(L, 1));
		  return 1;
		}


		private static int setn (LuaState L) {
		  LuaLCheckType(L, 1, LUA_TTABLE);
		//#ifndef luaL_setn
		  //luaL_setn(L, 1, luaL_checkint(L, 2));
		//#else
		  LuaLError(L, LUA_QL("setn") + " is obsolete");
		//#endif
		  LuaPushValue(L, 1);
		  return 1;
		}


		private static int tinsert (LuaState L) {
		  int e = aux_getn(L, 1) + 1;  /* first empty element */
		  int pos;  /* where to insert new element */
		  switch (LuaGetTop(L)) {
			case 2: {  /* called with only 2 arguments */
			  pos = e;  /* insert new element at the end */
			  break;
			}
			case 3: {
			  int i;
			  pos = LuaLCheckInt(L, 2);  /* 2nd argument is the position */
			  if (pos > e) e = pos;  /* `grow' array if necessary */
			  for (i = e; i > pos; i--) {  /* move up elements */
				LuaRawGetI(L, 1, i-1);
				LuaRawSetI(L, 1, i);  /* t[i] = t[i-1] */
			  }
			  break;
			}
			default: {
			  return LuaLError(L, "wrong number of arguments to " + LUA_QL("insert"));
			}
		  }
		  LuaLSetN(L, 1, e);  /* new size */
		  LuaRawSetI(L, 1, pos);  /* t[pos] = v */
		  return 0;
		}


		private static int tremove (LuaState L) {
		  int e = aux_getn(L, 1);
		  int pos = LuaLOptInt(L, 2, e);
		  if (!(1 <= pos && pos <= e))  /* position is outside bounds? */
		   return 0;  /* nothing to remove */
		  LuaLSetN(L, 1, e - 1);  /* t.n = n-1 */
		  LuaRawGetI(L, 1, pos);  /* result = t[pos] */
		  for ( ;pos<e; pos++) {
			LuaRawGetI(L, 1, pos+1);
			LuaRawSetI(L, 1, pos);  /* t[pos] = t[pos+1] */
		  }
		  LuaPushNil(L);
		  LuaRawSetI(L, 1, e);  /* t[e] = nil */
		  return 1;
		}


		private static void addfield (LuaState L, LuaLBuffer b, int i) {
		  LuaRawGetI(L, 1, i);
		  if (LuaIsString(L, -1)==0)
			LuaLError(L, "invalid value (%s) at index %d in table for " +
						  LUA_QL("concat"), LuaLTypeName(L, -1), i);
			LuaLAddValue(b);
		}


		private static int tconcat (LuaState L) {
		  LuaLBuffer b = new LuaLBuffer();
		  uint lsep;
		  int i, last;
		  CharPtr sep = LuaLOptLString(L, 2, "", out lsep);
		  LuaLCheckType(L, 1, LUA_TTABLE);
		  i = LuaLOptInt(L, 3, 1);
		  last = LuaLOptInteger(L, LuaLCheckInt, 4, LuaLGetN(L, 1));
		  LuaLBuffInit(L, b);
		  for (; i < last; i++) {
			addfield(L, b, i);
			LuaLAddLString(b, sep, lsep);
		  }
		  if (i == last)  /* add last value (if interval was not empty) */
			addfield(L, b, i);
		  LuaLPushResult(b);
		  return 1;
		}



		/*
		** {======================================================
		** Quicksort
		** (based on `Algorithms in MODULA-3', Robert Sedgewick;
		**  Addison-Wesley, 1993.)
		*/


		private static void set2 (LuaState L, int i, int j) {
		  LuaRawSetI(L, 1, i);
		  LuaRawSetI(L, 1, j);
		}

		private static int sort_comp (LuaState L, int a, int b) {
		  if (!LuaIsNil(L, 2)) {  /* function? */
			int res;
			LuaPushValue(L, 2);
			LuaPushValue(L, a-1);  /* -1 to compensate function */
			LuaPushValue(L, b-2);  /* -2 to compensate function and `a' */
			LuaCall(L, 2, 1);
			res = LuaToBoolean(L, -1);
			LuaPop(L, 1);
			return res;
		  }
		  else  /* a < b? */
			return LuaLessThan(L, a, b);
		}

		private static int auxsort_loop1(LuaState L, ref int i)
		{
			LuaRawGetI(L, 1, ++i);
			return sort_comp(L, -1, -2);
		}

		private static int auxsort_loop2(LuaState L, ref int j)
		{
			LuaRawGetI(L, 1, --j);
			return sort_comp(L, -3, -1);
		}

		private static void auxsort (LuaState L, int l, int u) {
		  while (l < u) {  /* for tail recursion */
			int i, j;
			/* sort elements a[l], a[(l+u)/2] and a[u] */
			LuaRawGetI(L, 1, l);
			LuaRawGetI(L, 1, u);
			if (sort_comp(L, -1, -2) != 0)  /* a[u] < a[l]? */
			  set2(L, l, u);  /* swap a[l] - a[u] */
			else
			  LuaPop(L, 2);
			if (u-l == 1) break;  /* only 2 elements */
			i = (l+u)/2;
			LuaRawGetI(L, 1, i);
			LuaRawGetI(L, 1, l);
			if (sort_comp(L, -2, -1) != 0)  /* a[i]<a[l]? */
			  set2(L, i, l);
			else {
			  LuaPop(L, 1);  /* remove a[l] */
			  LuaRawGetI(L, 1, u);
			  if (sort_comp(L, -1, -2) != 0)  /* a[u]<a[i]? */
				set2(L, i, u);
			  else
				LuaPop(L, 2);
			}
			if (u-l == 2) break;  /* only 3 elements */
			LuaRawGetI(L, 1, i);  /* Pivot */
			LuaPushValue(L, -1);
			LuaRawGetI(L, 1, u-1);
			set2(L, i, u-1);
			/* a[l] <= P == a[u-1] <= a[u], only need to sort from l+1 to u-2 */
			i = l; j = u-1;
			for (;;) {  /* invariant: a[l..i] <= P <= a[j..u] */
			  /* repeat ++i until a[i] >= P */
			  while (auxsort_loop1(L, ref i) != 0) {
				if (i>u) LuaLError(L, "invalid order function for sorting");
				LuaPop(L, 1);  /* remove a[i] */
			  }
			  /* repeat --j until a[j] <= P */
			  while (auxsort_loop2(L, ref j) != 0) {
				if (j<l) LuaLError(L, "invalid order function for sorting");
				LuaPop(L, 1);  /* remove a[j] */
			  }
			  if (j<i) {
				LuaPop(L, 3);  /* pop pivot, a[i], a[j] */
				break;
			  }
			  set2(L, i, j);
			}
			LuaRawGetI(L, 1, u-1);
			LuaRawGetI(L, 1, i);
			set2(L, u-1, i);  /* swap pivot (a[u-1]) with a[i] */
			/* a[l..i-1] <= a[i] == P <= a[i+1..u] */
			/* adjust so that smaller half is in [j..i] and larger one in [l..u] */
			if (i-l < u-i) {
			  j=l; i=i-1; l=i+2;
			}
			else {
			  j=i+1; i=u; u=j-2;
			}
			auxsort(L, j, i);  /* call recursively the smaller one */
		  }  /* repeat the routine for the larger one */
		}

		private static int sort (LuaState L) {
		  int n = aux_getn(L, 1);
		  LuaLCheckStack(L, 40, "");  /* assume array is smaller than 2^40 */
		  if (!LuaIsNoneOrNil(L, 2))  /* is there a 2nd argument? */
			LuaLCheckType(L, 2, LUA_TFUNCTION);
		  LuaSetTop(L, 2);  /* make sure there is two arguments */
		  auxsort(L, 1, n);
		  return 0;
		}

		/* }====================================================== */


		private readonly static LuaLReg[] tab_funcs = {
		  new LuaLReg("concat", tconcat),
		  new LuaLReg("foreach", _foreach),
		  new LuaLReg("foreachi", foreachi),
		  new LuaLReg("getn", getn),
		  new LuaLReg("maxn", maxn),
		  new LuaLReg("insert", tinsert),
		  new LuaLReg("remove", tremove),
		  new LuaLReg("setn", setn),
		  new LuaLReg("sort", sort),
		  new LuaLReg(null, null)
		};


		public static int luaopen_table (LuaState L) {
		  LuaLRegister(L, LUA_TABLIBNAME, tab_funcs);
		  return 1;
		}

	}
}

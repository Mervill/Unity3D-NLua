Unity3D-NLua
=======

Everything you need to get started using Lua in **Unity3D v5**. Indie & Pro compatible. 

This template project implements [NLua](https://github.com/NLua/NLua) which
drives [KeraLua](https://github.com/NLua/KeraLua), a C VM wrapper. Both projects
are licensed under the MIT. This project is licensed under the Boost Software Licence.

**Unity 5 Update**

With the release of Unity 5, native dlls are now allowed under the Indie licnence.
Therefore the previous support for [KopiLua](https://github.com/NLua/KopiLua) has been **depreciated**.

## Scripting Symbols

NLua requires some scripting symbols to be defined:

`UNITY_3D` - Suppress warnings about CLSC Attributes.

`USE_KERALUA` - KeraLua is an interop wrapper to the [NLua fork](https://github.com/NLua/lua)
of the original C VM (The DLL should be placed in a `Plugins\` folder in your project). NLua
can also interface with KopiLua (USE_KOPILUA), a pure C# implementation of lua, however it is 
not supported by this project.

`LUA_CORE` & `CATCH_EXCEPTIONS` - Required by the VM.

Your **Scripting Define Symbols** list should end up looking like:

```
UNITY_3D; USE_KERALUA; LUA_CORE; CATCH_EXCEPTIONS
```

You may also notice other symbols used throughout NLua, none of these have
been tested for compatibility.

## FAQ

 **How do I Instantiate new objects?**

See the SpawnSphere example.

**How do I run C# coroutines?**

See [this comment](https://github.com/NLua/NLua/issues/110#issuecomment-59874806) for details, essentially though you either have to call lua functions indriectily, or roll your own coroutine manager (not hugely difficult). Direct support for coroutines may be included in future releases. 

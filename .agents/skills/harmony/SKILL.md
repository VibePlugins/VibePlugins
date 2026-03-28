# Harmony Runtime Patching Guide

## Overview

**Harmony** is a .NET library for patching, replacing, and decorating existing methods at runtime — without modifying the original assembly. It works by dynamically generating replacement methods via IL manipulation and redirecting calls through detours.

Harmony is used extensively in modding communities (Unturned, RimWorld, Cities: Skylines, BepInEx) to intercept game methods that don't expose events or delegates.

**Reference source:** `references/harmony/` (submodule)
**Documentation:** `references/harmony/Documentation/articles/`
**NuGet:** `Lib.Harmony` version 2.x

---

## Core Concepts

### Harmony Instance

Every mod creates a single `Harmony` instance with a unique reverse-domain ID:

```csharp
using HarmonyLib;

var harmony = new Harmony("com.yourname.yourmod");

// Patch all annotated classes in the assembly
harmony.PatchAll(Assembly.GetExecutingAssembly());

// Unpatch everything this instance applied
harmony.UnpatchAll("com.yourname.yourmod");
```

**Important:** Always pass your own ID to `UnpatchAll()`. Calling `UnpatchAll()` with no argument removes ALL patches from ALL mods.

### Patch Types

Harmony supports six patch types, each serving a different purpose:

| Patch Type | When It Runs | Can Skip Original? | Can Modify Result? | Access to IL? |
|---|---|---|---|---|
| **Prefix** | Before original | Yes (return `false`) | Yes (`__result`) | No |
| **Postfix** | After original | No | Yes (`ref __result`) | No |
| **Transpiler** | At patch time (IL rewrite) | N/A | N/A | Yes |
| **Finalizer** | Always (like `finally`) | No | Yes | No |
| **Reverse Patch** | Copies original into your stub | N/A | N/A | Optional |

---

## Patch Type Details

### Prefix

Runs **before** the original method. Can inspect/modify arguments, skip the original, or set the return value.

```csharp
[HarmonyPatch(typeof(SomeClass), nameof(SomeClass.SomeMethod))]
static class SomeMethodPatch
{
    [HarmonyPrefix]
    static bool Prefix(SomeClass __instance, int damage, ref bool __result)
    {
        // Modify arguments before original runs
        // Return false to SKIP the original method entirely
        // Return true to let the original run

        if (damage > 1000)
        {
            __result = false; // Set return value when skipping
            return false;     // Skip original
        }

        return true; // Let original run
    }
}
```

**Rules:**
- Return type `bool` controls whether the original executes (`true` = run, `false` = skip).
- Return type `void` always lets the original run (side-effect only prefix).
- When skipping, set `__result` to provide the return value.
- Multiple prefixes: if any returns `false`, the original is skipped, but all prefixes still execute unless they are also "effect-having" (non-void, non-ref-less).

### Postfix

Runs **after** the original method. Can read/modify the return value and inspect final argument state.

```csharp
[HarmonyPatch(typeof(SomeClass), nameof(SomeClass.Calculate))]
static class CalculatePatch
{
    [HarmonyPostfix]
    static void Postfix(ref int __result, int input)
    {
        // Modify the return value
        __result = __result * 2;
    }
}
```

**Pass-through postfix pattern** — when the return type matches the first parameter:

```csharp
[HarmonyPostfix]
static IEnumerable<Item> Postfix(IEnumerable<Item> __result)
{
    // Filter or transform the original result
    foreach (var item in __result)
    {
        if (item.IsValid)
            yield return item;
    }
}
```

### Transpiler

Rewrites the IL instructions of the original method **at patch time**. The most powerful but most complex patch type.

```csharp
[HarmonyPatch(typeof(SomeClass), nameof(SomeClass.Process))]
static class ProcessPatch
{
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator,        // optional
        MethodBase original)           // optional
    {
        var codes = new List<CodeInstruction>(instructions);

        for (int i = 0; i < codes.Count; i++)
        {
            // Replace a constant value
            if (codes[i].opcode == OpCodes.Ldc_I4 && (int)codes[i].operand == 100)
            {
                codes[i].operand = 200;
            }

            // Replace a method call
            if (codes[i].Calls(AccessTools.Method(typeof(OldClass), "OldMethod")))
            {
                codes[i].operand = AccessTools.Method(typeof(NewClass), "NewMethod");
            }
        }

        return codes;
    }
}
```

**CodeMatcher** — pattern-based IL editing (preferred for complex transpilers):

```csharp
[HarmonyTranspiler]
static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
{
    return new CodeMatcher(instructions)
        // Find a specific pattern
        .MatchStartForward(
            new CodeMatch(OpCodes.Ldarg_0),
            new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(SomeClass), "GetValue")),
            new CodeMatch(OpCodes.Stloc_0)
        )
        // Advance past the match
        .Advance(2)
        // Insert new instructions after the match
        .InsertAndAdvance(
            new CodeInstruction(OpCodes.Ldloc_0),
            new CodeInstruction(OpCodes.Ldc_I4_2),
            new CodeInstruction(OpCodes.Mul),
            new CodeInstruction(OpCodes.Stloc_0)
        )
        .InstructionEnumeration();
}
```

**When to use transpilers:**
- You need to change behavior *inside* a method (not just before/after).
- You need to modify a specific conditional branch or loop.
- You need to inject code at a precise IL offset.
- Prefix/Postfix can't achieve the desired behavior.

**Transpiler guidelines:**
- Always work with `List<CodeInstruction>` for random access.
- Preserve labels and exception blocks — use `Clone()` or `MoveLabelsFrom()`.
- Use `CodeMatcher` for pattern-based edits instead of fragile index math.
- Test thoroughly — IL errors cause `InvalidProgramException` at runtime.

### Finalizer

Wraps the method in a try/finally (or try/catch/finally). Guaranteed to execute even if the original or other patches throw.

```csharp
[HarmonyPatch(typeof(SomeClass), nameof(SomeClass.Risky))]
static class RiskyPatch
{
    [HarmonyFinalizer]
    static Exception Finalizer(Exception __exception)
    {
        if (__exception != null)
        {
            Logger.LogError($"Caught: {__exception.Message}");
            return null; // Suppress the exception (return null = swallow)
        }
        return null;
    }
}
```

**Return types:**
- `void` — observe only, exception propagates normally.
- `Exception` — return `null` to suppress, return a new exception to remap.

### Reverse Patch

Copies the original (unpatched) method into a stub you control. Useful when you need to call the original from within a prefix that skips it.

```csharp
[HarmonyPatch(typeof(SomeClass), nameof(SomeClass.Calculate))]
static class CalculatePatch
{
    [HarmonyReversePatch]
    [HarmonyPatch(typeof(SomeClass), nameof(SomeClass.Calculate))]
    static int OriginalCalculate(SomeClass instance, int input)
    {
        // Stub — Harmony replaces this body with the original IL
        throw new NotImplementedException("Harmony reverse patch stub");
    }

    [HarmonyPrefix]
    static bool Prefix(SomeClass __instance, int input, ref int __result)
    {
        if (input < 0)
        {
            // Call the original unpatched method
            __result = OriginalCalculate(__instance, Math.Abs(input));
            return false;
        }
        return true;
    }
}
```

**Types:**
- `HarmonyReversePatchType.Original` — exact copy of original IL.
- `HarmonyReversePatchType.Snapshot` — copy with all current transpilers applied.

---

## Parameter Injection

Harmony injects special values into patch methods via naming conventions:

### Instance & Result

| Parameter | Type | Description |
|---|---|---|
| `__instance` | Declaring type | `this` reference (instance methods only) |
| `__result` | Return type | Return value. Use `ref` in postfix to modify. |
| `__resultRef` | `ref RefResult<T>` | For methods returning by `ref` |

### State Passing (Prefix to Postfix)

```csharp
[HarmonyPrefix]
static void Prefix(out Stopwatch __state)
{
    __state = Stopwatch.StartNew(); // Initialize state
}

[HarmonyPostfix]
static void Postfix(Stopwatch __state)
{
    __state.Stop();
    Logger.Log($"Method took {__state.ElapsedMilliseconds}ms");
}
```

`__state` can be any type. It is shared between prefix and postfix **within the same patch class**.

### Arguments

```csharp
// By name (must match original parameter name exactly)
static void Prefix(int damage, string playerName) { }

// By index (zero-based)
static void Prefix(int __0, string __1) { }

// By reference (to modify the argument before original runs)
static void Prefix(ref int damage) { }

// All arguments as array
static void Prefix(object[] __args)
{
    __args[0] = 999; // Modify first argument
}
```

### Private Field Access

Use triple underscore prefix + field name:

```csharp
// Read a private field
static void Postfix(float ___health) { }

// Write a private field
static void Prefix(ref float ___health)
{
    ___health = 100f;
}
```

### Metadata

| Parameter | Type | Description |
|---|---|---|
| `__originalMethod` | `MethodBase` | The method being patched (for conditional logic) |
| `__runOriginal` | `bool` | Whether the original will/did run |

---

## Targeting Methods

### Annotation-Based Targeting

```csharp
// Simple method
[HarmonyPatch(typeof(Player), nameof(Player.TakeDamage))]

// Overloaded method (specify argument types)
[HarmonyPatch(typeof(Player), nameof(Player.TakeDamage), new Type[] { typeof(int), typeof(bool) })]

// Property getter
[HarmonyPatch(typeof(Player), nameof(Player.Health), MethodType.Getter)]

// Property setter
[HarmonyPatch(typeof(Player), nameof(Player.Health), MethodType.Setter)]

// Constructor
[HarmonyPatch(typeof(Player), MethodType.Constructor)]

// Constructor with specific signature
[HarmonyPatch(typeof(Player), MethodType.Constructor, new Type[] { typeof(string), typeof(int) })]

// Static constructor
[HarmonyPatch(typeof(Player), MethodType.StaticConstructor)]

// Enumerator MoveNext (patch the state machine)
[HarmonyPatch(typeof(SomeClass), nameof(SomeClass.GetItems), MethodType.Enumerator)]

// Async MoveNext
[HarmonyPatch(typeof(SomeClass), nameof(SomeClass.FetchData), MethodType.Async)]
```

### Stacked Annotations

Combine multiple attributes on the class and method levels:

```csharp
[HarmonyPatch(typeof(Player))]
static class PlayerPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(Player.TakeDamage))]
    static bool TakeDamagePrefix(ref int amount) { return true; }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(Player.Heal))]
    static void HealPostfix(int __result) { }
}
```

### Dynamic Targeting

Use `[HarmonyTargetMethod]` or `[HarmonyTargetMethods]` for runtime method resolution:

```csharp
[HarmonyPatch]
static class DynamicPatch
{
    [HarmonyTargetMethod]
    static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(SomeInternalClass), "HiddenMethod");
    }

    [HarmonyPrefix]
    static void Prefix() { }
}

// Multiple targets
[HarmonyPatch]
static class MultiPatch
{
    [HarmonyTargetMethods]
    static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(ClassA), "Method1");
        yield return AccessTools.Method(typeof(ClassB), "Method2");
    }

    [HarmonyPrefix]
    static void Prefix(MethodBase __originalMethod)
    {
        Logger.Log($"Intercepted: {__originalMethod.Name}");
    }
}
```

### Conditional Patching

Use `[HarmonyPrepare]` to conditionally apply patches:

```csharp
[HarmonyPatch(typeof(SomeClass), "SomeMethod")]
static class ConditionalPatch
{
    [HarmonyPrepare]
    static bool Prepare()
    {
        // Return false to skip this patch entirely
        return MyPlugin.Instance.Configuration.Instance.EnableFeature;
    }

    [HarmonyPrefix]
    static void Prefix() { }
}
```

---

## Priority & Ordering

When multiple mods patch the same method, execution order matters.

### Priority Constants

```csharp
Priority.Last           = 0     // Execute last
Priority.VeryLow        = 100
Priority.Low            = 200
Priority.LowerThanNormal = 300
Priority.Normal         = 400   // Default
Priority.HigherThanNormal = 500
Priority.High           = 600
Priority.VeryHigh       = 700
Priority.First          = 800   // Execute first
```

**For Prefixes:** Higher priority = runs earlier.
**For Postfixes:** Higher priority = runs later (closer to original).

### Ordering Attributes

```csharp
[HarmonyPatch(typeof(Player), nameof(Player.TakeDamage))]
static class DamagePatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.High)]
    [HarmonyBefore("com.other.mod")]
    [HarmonyAfter("com.base.mod")]
    static bool Prefix(ref int damage) { return true; }
}
```

### Categories

Group patches for selective application:

```csharp
[HarmonyPatchCategory("combat")]
[HarmonyPatch(typeof(Player), nameof(Player.TakeDamage))]
static class CombatPatch { }

// Apply only specific categories
harmony.PatchCategory("combat");
harmony.UnpatchCategory("combat");
```

---

## Utility Classes

### AccessTools

Reflection helper that searches all binding flags by default:

```csharp
using HarmonyLib;

// Get private/internal members without BindingFlags
MethodInfo method = AccessTools.Method(typeof(SomeClass), "PrivateMethod");
MethodInfo method = AccessTools.Method(typeof(SomeClass), "Overloaded", new[] { typeof(int) });
FieldInfo field = AccessTools.Field(typeof(SomeClass), "privateField");
PropertyInfo prop = AccessTools.Property(typeof(SomeClass), "InternalProp");
ConstructorInfo ctor = AccessTools.Constructor(typeof(SomeClass), new[] { typeof(string) });
Type nested = AccessTools.Inner(typeof(SomeClass), "NestedPrivateClass");

// Create delegates
var del = AccessTools.MethodDelegate<Action<int>>(method);

// Get all methods/fields matching criteria
var allMethods = AccessTools.GetDeclaredMethods(typeof(SomeClass));
var allFields = AccessTools.GetDeclaredFields(typeof(SomeClass));
```

### Traverse

Fluent reflection API with null safety:

```csharp
using HarmonyLib;

// Read a private field
float health = Traverse.Create(playerInstance).Field("_health").GetValue<float>();

// Set a private field
Traverse.Create(playerInstance).Field("_health").SetValue(100f);

// Call a private method
var result = Traverse.Create(playerInstance)
    .Method("InternalCalculate", new object[] { 42 })
    .GetValue<int>();

// Chain through nested objects
string name = Traverse.Create(instance)
    .Field("_manager")
    .Property("CurrentPlayer")
    .Field("_name")
    .GetValue<string>();
```

### FileLog (Debugging)

```csharp
// Writes to harmony.log.txt on Desktop
FileLog.Log("Debug message");

// Or enable debug on specific patches
[HarmonyDebug]
[HarmonyPatch(typeof(SomeClass), "SomeMethod")]
static class DebugPatch { }
```

Set `Harmony.DEBUG = true` to log all patching activity globally.

---

## IL Manipulation Reference

### Common OpCodes for Transpilers

| OpCode | Stack Effect | Description |
|---|---|---|
| `Ldarg_0` | push | Load `this` (or first arg in static) |
| `Ldarg_1..3` | push | Load argument 1-3 |
| `Ldloc_0..3` | push | Load local variable 0-3 |
| `Stloc_0..3` | pop | Store to local variable 0-3 |
| `Ldc_I4` | push | Load int constant |
| `Ldc_R4` | push | Load float constant |
| `Ldstr` | push | Load string literal |
| `Ldfld` | pop,push | Load instance field |
| `Stfld` | pop,pop | Store to instance field |
| `Ldsfld` | push | Load static field |
| `Call` | varies | Call method |
| `Callvirt` | varies | Call virtual method |
| `Ret` | pop | Return |
| `Brfalse` / `Brtrue` | pop | Conditional branch |
| `Br` | — | Unconditional branch |
| `Ceq` / `Clt` / `Cgt` | pop,pop,push | Comparison |
| `Add` / `Sub` / `Mul` / `Div` | pop,pop,push | Arithmetic |

### CodeInstruction Helpers

```csharp
// Factory methods (preferred over manual OpCode construction)
CodeInstruction.Call(() => SomeClass.StaticMethod(default(int)))
CodeInstruction.Call(typeof(SomeClass), "Method", new[] { typeof(int) })
CodeInstruction.LoadField(typeof(SomeClass), "fieldName")
CodeInstruction.StoreField(typeof(SomeClass), "fieldName")
CodeInstruction.LoadLocal(0)
CodeInstruction.StoreLocal(0)
CodeInstruction.LoadArgument(1)

// Check helpers
instruction.Is(OpCodes.Call, someMethodInfo)
instruction.Calls(someMethodInfo)
instruction.IsLdarg()
instruction.IsStloc()
instruction.IsLdloc()
```

### Practical Transpiler: Injecting a Method Call

```csharp
[HarmonyTranspiler]
static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
{
    var targetMethod = AccessTools.Method(typeof(Player), "ApplyDamage");
    var hookMethod = AccessTools.Method(typeof(MyHooks), nameof(MyHooks.OnBeforeDamage));
    bool injected = false;

    foreach (var instruction in instructions)
    {
        // Inject our hook right before the call to ApplyDamage
        if (!injected && instruction.Calls(targetMethod))
        {
            // At this point the stack has the arguments for ApplyDamage
            // Duplicate the damage value on the stack for our hook
            yield return new CodeInstruction(OpCodes.Dup);
            yield return new CodeInstruction(OpCodes.Call, hookMethod);
            injected = true;
        }
        yield return instruction;
    }

    if (!injected)
        Logger.LogWarning("Failed to find injection point for OnBeforeDamage!");
}
```

### Practical Transpiler: Changing a Conditional

```csharp
// Original: if (health > 0) { ... }
// Patched:  if (health > -1) { ... }  (allow zero health to pass)

[HarmonyTranspiler]
static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
{
    return new CodeMatcher(instructions)
        .MatchStartForward(
            new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Player), "health")),
            new CodeMatch(OpCodes.Ldc_I4_0),
            new CodeMatch(OpCodes.Ble)    // branch if less-or-equal
        )
        .Advance(1) // Move to the Ldc_I4_0
        .SetOperandAndAdvance(-1) // Change 0 to -1
        .InstructionEnumeration();
}
```

---

## Manual Patching API

For cases where annotations are inconvenient (dynamic targets, runtime decisions):

```csharp
var harmony = new Harmony("com.yourname.mod");

var original = AccessTools.Method(typeof(TargetClass), "TargetMethod");
var prefix = new HarmonyMethod(AccessTools.Method(typeof(MyPatches), nameof(MyPatches.MyPrefix)));
var postfix = new HarmonyMethod(AccessTools.Method(typeof(MyPatches), nameof(MyPatches.MyPostfix)));

// Apply patches
harmony.Patch(original, prefix: prefix, postfix: postfix);

// With priority
prefix.priority = Priority.High;
prefix.before = new[] { "com.other.mod" };

// Unpatch specific method
harmony.Unpatch(original, HarmonyPatchType.Prefix, "com.yourname.mod");
```

---

## Common Patterns

### Guard Patch (Cancel an Action)

```csharp
[HarmonyPatch(typeof(Player), nameof(Player.DropItem))]
static class PreventItemDropPatch
{
    [HarmonyPrefix]
    static bool Prefix(Player __instance, Item item)
    {
        if (IsProtectedItem(item))
        {
            // Don't drop protected items
            return false; // Skip original
        }
        return true;
    }
}
```

### Modify Return Value

```csharp
[HarmonyPatch(typeof(Shop), nameof(Shop.GetPrice))]
static class DiscountPatch
{
    [HarmonyPostfix]
    static void Postfix(ref float __result, Item item)
    {
        if (IsOnSale(item))
            __result *= 0.5f; // 50% discount
    }
}
```

### Wrap with Try/Catch

```csharp
[HarmonyPatch(typeof(ThirdPartyLib), "UnstableMethod")]
static class SafetyPatch
{
    [HarmonyFinalizer]
    static Exception Finalizer(Exception __exception)
    {
        if (__exception != null)
        {
            Logger.LogError($"Suppressed crash: {__exception}");
            return null; // Swallow exception
        }
        return null;
    }
}
```

### Timing / Profiling

```csharp
[HarmonyPatch(typeof(World), nameof(World.Tick))]
static class ProfilingPatch
{
    [HarmonyPrefix]
    static void Prefix(out Stopwatch __state)
    {
        __state = Stopwatch.StartNew();
    }

    [HarmonyPostfix]
    static void Postfix(Stopwatch __state)
    {
        __state.Stop();
        if (__state.ElapsedMilliseconds > 16)
            Logger.LogWarning($"World.Tick took {__state.ElapsedMilliseconds}ms");
    }
}
```

### Redirect Method Call (Transpiler)

```csharp
[HarmonyPatch(typeof(SomeClass), "SomeMethod")]
static class RedirectPatch
{
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchStartForward(
                new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(OldHelper), "Calculate"))
            )
            .SetOperandAndAdvance(AccessTools.Method(typeof(NewHelper), "Calculate"))
            .InstructionEnumeration();
    }
}
```

---

## Edge Cases & Pitfalls

1. **Patching generic methods:** You must patch the specific closed generic (`Method<int>`) not the open definition. Use `AccessTools.Method(type, name).MakeGenericMethod(typeof(int))`.

2. **Patching structs:** `__instance` is a copy for value types. Use `ref __instance` (Harmony 2.x) or transpiler to modify struct state.

3. **Inlined methods:** The JIT may inline small methods before Harmony can patch them. Add `[MethodImpl(MethodImplOptions.NoInlining)]` to your test targets, or patch the caller instead.

4. **Multiple prefixes returning false:** All prefixes execute regardless — returning `false` only skips the *original*, not other prefixes.

5. **Static constructors:** Can only be transpiler-patched; prefix/postfix may not work reliably.

6. **Thread safety:** Patch methods can be called from any thread. Use locks or concurrent collections if your patch maintains state.

7. **Unpatching order:** Always unpatch with your specific Harmony ID. Never call `UnpatchAll()` without an ID — it removes every mod's patches.

8. **Transpiler fragility:** IL patterns change between game versions. Always log a warning if your `CodeMatcher` fails to find its target rather than silently doing nothing.

---

## Debugging Patches

```csharp
// Enable global debug logging (writes to Desktop/harmony.log.txt)
Harmony.DEBUG = true;

// Per-patch debug
[HarmonyDebug]
[HarmonyPatch(typeof(SomeClass), "SomeMethod")]
static class MyPatch { }

// Query patch state at runtime
var patches = Harmony.GetPatchInfo(AccessTools.Method(typeof(SomeClass), "SomeMethod"));
if (patches != null)
{
    foreach (var prefix in patches.Prefixes)
        Logger.Log($"Prefix: {prefix.owner} priority={prefix.priority}");
    foreach (var postfix in patches.Postfixes)
        Logger.Log($"Postfix: {postfix.owner} priority={postfix.priority}");
}

// List all patched methods
foreach (var method in harmony.GetPatchedMethods())
    Logger.Log($"Patched: {method.DeclaringType?.Name}.{method.Name}");
```

# BifyLang (BoomifyCS) — AI Agent Architecture & Codegen Rules

## 1. STOP-AND-ASK PROTOCOL
Before writing or modifying code for a new language feature, ABI/memory layout change, or multi-file architectural refactoring:
1. **HALT** and output a short Architectural Proposal (max 10–15 lines) specifying:
    - Which existing classes/files will be touched and what new classes will be created.
    - How LLVM IR types, GEP indices, or VTable/ARC layouts are affected (if at all).
2. **WAIT** for user approval before generating implementation code.
3. Skip this step for bug fixes, local cleanups, or single-method changes.

## 2. MODULARITY & RESPONSIBILITY CONSTRAINTS
- **File & Method Size:** Keep new and refactored `CodeGen/` files under **200 lines** and methods under **40 lines**. Extract complex LLVM IR sequences into focused private methods or helper structs.
- **Single Responsibility in CodeGen:**
    - `DeclarationDispatcher`, `StatementDispatcher`, and `ExpressionDispatcher` are strictly for **routing** AST nodes. Never emit raw `LLVMBuilder` instructions inside dispatchers.
    - Complex mechanics MUST live in isolated helpers (`BranchScope`, `BranchEmitter`, `LoopEmitter`, `TypeCaster`, `HeapControl`).
    - Use `IDisposable` scopes (`using (...)`) for lexical scopes (`_ctx.EnterScope()`), loop contexts (`_ctx.EnterLoop()`), temporary ARC scopes (`_ctx.HeapControl.BeginTempScope()`), and branch blocks (`BranchScope`).

## 3. LLVM IR (OPAQUE POINTERS) & MEMORY LAYOUT INVARIANTS
Target **LLVM 20+ / 23+ with Opaque Pointers (`ptr`)**. Typed pointers (`i8*`, `T*`) do not exist in modern LLVM IR:
- **Opaque Pointer APIs:**
    - All pointers in LLVM IR have type `ptr`. Never emit pointer-to-pointer `BuildBitCast` instructions (they are no-ops with opaque pointers).
    - Always use explicit-type LLVMSharp builder APIs: `BuildLoad2`, `BuildGEP2`, `BuildStructGEP2`, and `BuildCall2`, passing the explicit pointee/struct/function `LLVMTypeRef`.
- **Object Layout (Class Struct):**
    - Index `0`: `vptr` (Pointer to VTable, `ptr`)
    - Index `1`: `strong_count` (ARC strong reference counter, `i32`)
    - Index `2`: `weak_count` (ARC weak reference counter, `i32`)
    - Index `3..N`: Parent fields in exact inheritance order
    - Index `N+1..M`: Derived fields
- **No Field Shadowing:** Derived classes cannot redeclare parent field names (`ClassLayoutBuilder` reports duplicate fields with `DiagnosticKind.Name`).
- **VTable Layout & Method Dispatch:**
    - **VTable Slot `0`:** Always reserved for the synthesized or user-defined destructor (`__dtor`), invoked by the C runtime (`runtime.c`) when `strong_count` reaches 0.
    - **Virtual / Override Call (`method.VTableIndex >= 0`):** Load VTable `ptr` from struct slot `0` (`BuildStructGEP2`) -> GEP slot `method.VTableIndex` (`BuildGEP2`) -> load function `ptr` -> indirect `BuildCall2`.
    - **Non-Virtual or `base` Call (`method.VTableIndex == -1` or `base.Foo()`):** Direct static `BuildCall2` to the mangled function symbol (`NameMangler.GenerateMethodName`), passing `this` (`ptr`) as argument `0`. Never route non-virtual or `base` calls through the VTable.
- **BasicBlock & Alloca Hygiene:**
    - Use `builder.BranchIfUnterminated(target)` or `block.IsTerminated()` (`BasicBlockExtensions`) before emitting trailing branches. Never emit instructions into a terminated basic block.
    - Always allocate local variables in the function's entry block via `_ctx.BuildEntryAlloca(type, name)` (never call `Builder.BuildAlloca` directly inside loop or branch blocks).
- **ARC & Object Lifetime (`HeapControl` & `Scope`):**
    - Use `_ctx.HeapControl.BuildArcStoreInit` for initial variable/field writes (consumes tracked temporaries without extra retain) and `BuildArcAssign` for reassignments (releases the old reference first).
    - Wrap expressions that may produce temporary `ClassType` values (`new`, calls, weak upgrades) in `using (_ctx.HeapControl.BeginTempScope())`.
    - Before emitting early exits (`return`, `break`, `continue`), release active lexical scopes via `_ctx.ReleaseScopesUpTo(...)` (retaining/exempting the return value prior to local cleanup).
- **Modules & Name Mangling (`NameMangler` & `EnterModule`):**
    - Never hardcode or manually concatenate symbol names—always use `NameMangler` (`GenerateClassName`, `GenerateMethodName`, `GenerateFunctionName`, `GenerateVtable`). Only the entry point `main.main` remains unmangled as `main`.
    - Wrap cross-module type/function declarations and emission in `using (_ctx.EnterModule(...))`.

## 4. C# & CODEGEN CONVENTIONS
- **Base Class & Primary Constructors:** CodeGen handlers, dispatchers, and emitters inherit from `CodeGenBase(ctx)`: `public class FooGen(CodeGenContext ctx) : CodeGenBase(ctx)`. Always reference `_ctx` (not `ctx`) in method bodies to avoid duplicate primary-constructor captures (`CS9124` / `CS9107`).
- **Naming Conventions (`CodeGen/`):**
    - File names and file-scoped namespaces (`namespace ...;`) MUST match the primary type declared inside.
    - CodeGen handler classes use the `*Gen` suffix (`IfGen`, `BinaryOpGen`, `UnaryOpGen`), and primary emission methods follow `Emit<NodeName>` (`EmitIf`, `EmitWhile`, `EmitFor`, `EmitSwitch`, `EmitReturn`, `EmitVariableDecl`).
    - Prefer unabbreviated domain terms for services and models (`ParameterResolver`, `TypeResolver`, `MemberResolver`) and consistent `Op` / `VTable` casing (`BinaryOpGen`, `UnaryOpGen`, `VTableBuilder`).
- **Error Handling:** Never use legacy `Traceback.Instance` or `BifyError.Throw()`. All errors go through the shared `DiagnosticBag` pipeline described in Section 5.
- **Test Verification:** Always run `dotnet test` after modifying `Lexer/`, `Ast/`, or `CodeGen/` to verify lexer/parser recovery, LLVM IR structure, and native E2E execution (`runtime.o`). Assert failures via `DiagnosticBag` and `DiagnosticKind` (`BifyAssert.CompileError`, `BifyAssert.ParseError`, `BifyAssert.ActionThrows`).

## 5. DIAGNOSTICS, COORDINATES & POISON RECOVERY
- **Unified `DiagnosticBag` Pipeline:** Every compilation shares one `DiagnosticBag` across `CompilationHelper.ParseToAst` -> `MyLexer` / `AstTree` -> `CodeGenContext` (and across imports via `ModulePoolCollector`). Never emit or verify LLVM IR or link binaries when `Diagnostics.HasErrors` is true; print diagnostics once via `ErrorFormatter.PrintDiagnostics`.
- **`Error`, `Fail`, and `AbortPoison` (`AstBuilder`, `CodeGenBase`, `CodeGenContext`):**
    - `Error(msg, kind, target)` records a `Diagnostic` and **continues** in the same method (used for independent checks such as duplicate fields/methods, `break`/`continue` outside loops, or missing return paths).
    - `Fail(msg, kind, target)` / `Fail<T>(...)` (`[DoesNotReturn]`) records a `Diagnostic` and throws `BifyCompilationException(diagnostic)` to unwind to the nearest recovery boundary.
    - `AbortPoison()` / `AbortPoison<T>()` (`[DoesNotReturn]`) throws `new BifyCompilationException()` (`IsPoisonAbort == true`) **without** recording a duplicate diagnostic when an already-poisoned token, AST node, or symbol is encountered.
- **Exact Source Coordinates (`SourceLocation`):**
    - `MyLexer` tracks `_lineStartOffset` on `\n` and computes 1-based columns via `(tokenStart - _lineStartOffset) + 1`.
    - `AstBuilder` propagates `Token.Line` and `Token.Column` onto `AstNode` (`LineNumber`, `ColumnNumber`), and `CodeGenContext.CreateLocation` populates `SourceLocation.ColumnNumber` from `AstNode` (`ErrorFormatter` prints `Column` when `> 0`).
- **Multi-Stage Poison & Recovery Boundaries:**
    - **Lexer (`TokenType.POISON`, `Token.IsPoison`):** Record `DiagnosticKind.Syntax`, emit a poison token (`TokenType.POISON`, or `TokenType.STRING` with `IsPoison = true` on unclosed strings while rewinding `\n`), and continue lexing.
    - **AST (`AstPoison`, `AstNode.IsPoison`):** Catch `BifyCompilationException` at line/statement/declaration boundaries (`AstTree.ParseTokens`, `VariableDeclarationHandler`, `FunctionHandler` which still parses function bodies, `ClassHandler`), emit `AstPoison` or set `IsPoison = true`, and continue parsing.
    - **CodeGen (`Symbol.IsPoison`, `ClassField.IsPoison`, `ClassMethod.IsPoison`):** Guard `VariableDeclGen`, `ModuleLayoutBuilder`, `FunctionGen`, `ClassMemberCollector`, `Scope.ReleaseLocals` (`DefinePoison` skips ARC cleanup), and dispatchers against `AstPoison` / `.IsPoison`; call `AbortPoison` when referencing poisoned symbols or members to suppress cascading errors (`undeclared identifier '...'`). Isolate failures at `StatementDispatcher.EmitStatementSafely`, `DeclarationDispatcher.EmitModule`, and `ClassGen.EmitClass`.
- **Error Message Style (Zig-Style):**
    - Terse, single-clause, all lowercase start, no trailing `.`, `!`, or `\r\n`, and no contractions (`cannot`, not `Can't`).
    - Wrap identifiers, types, tokens, operators, and flags in `'single quotes'` (`'...'`).
    - Prefer standard phrasing: `undeclared identifier '...'`, `unknown type '...'`, `no member '...' in '...'`, `duplicate ... '...'`, `expected ..., found ...`, `cannot ...`, `conflicting flags '...' and '...'`.

## 6. COMMIT MESSAGE CONVENTIONS
- Use Conventional Commits format: `<type>(<scope>): <description>` (or `<type>: <description>` when no single scope applies).
- Common types: `feat`, `fix`, `refactor`, `test`, `docs`, `chore`. Common scopes: `lexer`, `ast`, `codegen`, `runtime`, `tests`, `docs`.
- Write the description in lowercase imperative mood, without a trailing period. For multi-part commits, list one `<type>(<scope>): <description>` per line.
- Example:
  ```
  feat(ast): add Rust-like declarations (mut, val, func)
  fix(codegen): correct type inheritance from variable values and mutable variable recognition
  ```
using System;
using System.Diagnostics;
using System.IO;
using BifyLang.Ast;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.Exceptions;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen;

[Flags]
public enum CompilerFlags
{
    NONE = 0,
    EXECUTE = 1 << 0
}

public class BifyCompiler
{
    static BifyCompiler()
    {
        LLVM.InitializeAllTargetInfos();
        LLVM.InitializeAllTargets();
        LLVM.InitializeAllTargetMCs();
        LLVM.InitializeAllAsmParsers();
        LLVM.InitializeAllAsmPrinters();
    }

    public static unsafe bool VerifyModule(LLVMModuleRef module, out string? error)
    {
        error = null;
        sbyte* message = null;

        if (LLVM.VerifyModule(module, LLVMVerifierFailureAction.LLVMReturnStatusAction, &message) != 0)
        {
            if (message != null)
            {
                error = new string(message);
                LLVM.DisposeMessage(message);
            }
            return false;
        }

        return true;
    }

    public void Compile(AstModule moduleNode, string filePath, string outputFilePath, CompilerFlags flags, DiagnosticBag? initialDiagnostics = null)
    {
        var ctx = new CodeGenContext(initialDiagnostics);
        try
        {

            var module = ctx.GenerateModule(moduleNode);
            if (ctx.Diagnostics.HasErrors)
            {
                ErrorFormatter.PrintDiagnostics(ctx.Diagnostics);
                return;
            }

            LLVMValueRef userMain = FindUserMain(ctx, module);
            if (userMain.Name == "main")
                userMain.Name = "__bify_user_main";
            EmitEntryPointWrapper(ctx, module, userMain);

            if (!VerifyModule(module, out var error))
            {
                Console.Error.WriteLine($"LLVM module verification failed:\n{error}");
                Console.Error.WriteLine(module.ToString());
                throw new InvalidOperationException("Module verification failed.");
            }

            EmitOutput(outputFilePath, flags, module);
        }
        catch (BifyCompilationException)
        {
            ErrorFormatter.PrintDiagnostics(ctx.Diagnostics);
        }
        catch (Exception)
        {
            if (ctx.Diagnostics.HasErrors)
            {
                ErrorFormatter.PrintDiagnostics(ctx.Diagnostics);
            }
            throw;
        }
    }

    private void EmitOutput(string outputFilePath, CompilerFlags flags, LLVMModuleRef module)
    {
        string fileExtension = Path.GetExtension(outputFilePath);
        switch (fileExtension)
        {
            case ".o":
                GenerateObjectFile(outputFilePath, module);
                break;
            case ".out" or "":
                GenerateExecutable(outputFilePath, flags, module);
                break;
            case ".ir":
                File.WriteAllText(outputFilePath, module.ToString());
                break;
            default:
                throw new ArgumentException($"Invalid output file extension: '{fileExtension}'");
        }
    }

    private static LLVMValueRef FindUserMain(CodeGenContext ctx, LLVMModuleRef module)
    {
        LLVMValueRef userMain = default;

        for (var fn = module.FirstFunction; fn.Handle != IntPtr.Zero; fn = fn.NextFunction)
        {
            var parts = fn.Name.Split('.');

            if (fn.Name == "main" || (parts.Length == 2 && parts[1] == "main"))
            {
                if (userMain.Handle != IntPtr.Zero)
                    ctx.Fail($"duplicate 'main' function '{fn.Name}'", DiagnosticKind.NAME);

                userMain = fn;
            }
        }

        if (userMain.Handle == IntPtr.Zero)
            ctx.Fail("missing 'main' function", DiagnosticKind.NAME);

        return userMain;
    }

    private static unsafe void EmitEntryPointWrapper(
        CodeGenContext ctx,
        LLVMModuleRef module,
        LLVMValueRef userMain)
    {
        LLVMTypeRef intType = BifyType.CreateInt(ctx).Type;
        LLVMTypeRef userFnType = LLVM.GlobalGetValueType(userMain);

        LLVMTypeRef mainWrapperType = LLVMTypeRef.CreateFunction(intType, [], IsVarArg: false);
        LLVMValueRef mainWrapper = module.AddFunction("main", mainWrapperType);

        LLVMBasicBlockRef entry = mainWrapper.AppendBasicBlock("entry");
        ctx.Builder.PositionAtEnd(entry);
        ctx.StaticLifeCycle.EmitCallInitStatics();
        if (userFnType.ReturnType == intType)
        {
            LLVMValueRef[] refs = [];
            LLVMValueRef exitCode = ctx.Builder.BuildCall2(userFnType, userMain, refs, "exit_code");
            ctx.StaticLifeCycle.EmitCallCleanUpStatics();
            ctx.Builder.BuildRet(exitCode);
        }
        else
        {
            ctx.Builder.BuildCall2(userFnType, userMain, []);
            ctx.StaticLifeCycle.EmitCallCleanUpStatics();
            ctx.Builder.BuildRet(LLVMValueRef.CreateConstInt(intType, 0, SignExtend: false));
        }

    }

    public unsafe void GenerateObjectFile(string outputFilePath, LLVMModuleRef module)
    {
        LLVMTargetMachineRef targetMachine = CreateTargetMachine();
        try
        {
            targetMachine.EmitToFile(module, outputFilePath, LLVMCodeGenFileType.LLVMObjectFile);
        }
        finally
        {
            LLVM.DisposeTargetMachine(targetMachine);
        }
    }

    public void GenerateExecutable(string outputFilePath, CompilerFlags flags, LLVMModuleRef module)
    {
        string tempObjFile = Path.Combine(Path.GetTempPath(), $"bify_{Guid.NewGuid():N}.o");

        try
        {
            GenerateObjectFile(tempObjFile, module);

            LinkLibraries(tempObjFile, outputFilePath);
        }
        finally
        {
            if (File.Exists(tempObjFile))
            {
                File.Delete(tempObjFile);
            }
        }

        if (flags.HasFlag(CompilerFlags.EXECUTE))
        {
            RunExecutable(outputFilePath);
        }
    }

    public void LinkLibraries(string objectFilePath, string outputFilePath)
    {
        string runtimeObjPath = ResolveRuntimePath();

        var processInfo = new ProcessStartInfo
        {
            FileName = "clang",
            Arguments = $"\"{objectFilePath}\" \"{runtimeObjPath}\" -o \"{outputFilePath}\"",
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(processInfo)
            ?? throw new InvalidOperationException("Failed to start clang process.");

        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Clang linking failed:\n{error}");
        }

        Console.WriteLine("Successfully compiled.");
    }

    private static void RunExecutable(string executablePath)
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = Path.GetFullPath(executablePath),
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(processInfo)
            ?? throw new InvalidOperationException($"Failed to start executable: {executablePath}");

        process.WaitForExit();
    }

    private static string ResolveRuntimePath()
    {
        string? envPath = Environment.GetEnvironmentVariable("BIFY_RUNTIME_PATH");
        if (!string.IsNullOrWhiteSpace(envPath) && File.Exists(envPath))
        {
            return envPath;
        }

        string defaultPath = Path.Combine(AppContext.BaseDirectory, "runtime.o");
        if (File.Exists(defaultPath))
        {
            return defaultPath;
        }

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, "runtime.o");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Could not find compiled runtime at '{defaultPath}'.");
    }

    private static unsafe LLVMTargetMachineRef CreateTargetMachine()
    {
        sbyte* targetTriple = LLVM.GetDefaultTargetTriple();
        try
        {
            LLVMTarget* target;
            sbyte* error = null;

            if (LLVM.GetTargetFromTriple(targetTriple, &target, &error) != 0)
            {
                string message = new string(error);
                LLVM.DisposeMessage(error);
                throw new InvalidOperationException($"Error selecting target: {message}");
            }

            fixed (byte* cpuPtr = "generic\0"u8)
            fixed (byte* featuresPtr = "\0"u8)
            {
                return LLVM.CreateTargetMachine(
                    target,
                    targetTriple,
                    (sbyte*)cpuPtr,
                    (sbyte*)featuresPtr,
                    LLVMCodeGenOptLevel.LLVMCodeGenLevelDefault,
                    LLVMRelocMode.LLVMRelocPIC,
                    LLVMCodeModel.LLVMCodeModelDefault);
            }
        }
        finally
        {
            LLVM.DisposeMessage(targetTriple);
        }
    }
}

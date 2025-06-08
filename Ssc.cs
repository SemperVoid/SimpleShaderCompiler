using CommandLine;

using Vortice.Dxc;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.ShaderCompiler;

using DxCompiler = Vortice.D3DCompiler.Compiler;
using SpvCompiler = Vortice.ShaderCompiler.Compiler;

namespace Ssc;

public class Ssc
{
    public static void Main(string[] args)
    {
        var p = Parser.Default.ParseArguments<CompileDxbcVerbs, CompileDxilVerbs, CompileSpirvVerbs, 
            DisassembleDxbcVerbs, DisassembleDxilVerbs>(args);

        p.WithParsed<CompileDxbcVerbs>(CompileDxbc);
        p.WithParsed<CompileDxilVerbs>(CompileDxil);
        p.WithParsed<CompileSpirvVerbs>(CompileSpirv);
        p.WithParsed<DisassembleDxbcVerbs>(DisassembleDxbc);
        p.WithParsed<DisassembleDxilVerbs>(DisassembleDxil);
    }

    static void CompileDxbc(CompileDxbcVerbs verbs)
    {
        (verbs.InputFile, verbs.OutputFile) = ValidateParameters((verbs.InputFile, verbs.OutputFile, ""));

        string shaderFileExtension = GetShaderFileExtension(verbs.TargetProfile);
        verbs.EntryPoint ??= "main";

        DxCompiler.CompileFromFile(verbs.InputFile, verbs.EntryPoint, verbs.TargetProfile, out Blob compiledShader, out Blob errorMessages);

        if (errorMessages != null)
        {
            Console.Error.WriteLine(errorMessages.AsString());
            return;
        }
        if (compiledShader.AsBytes().Length == 0)
        {
            Console.Error.WriteLine("Error: Compiled shader is empty");
            return;
        }

        DxCompiler.WriteBlobToFile(compiledShader, verbs.OutputFile + shaderFileExtension, true);
    }

    static void CompileDxil(CompileDxilVerbs verbs)
    {
        (verbs.InputFile, verbs.OutputFile) = ValidateParameters((verbs.InputFile, verbs.OutputFile, ""));

        string shaderFileExtension = GetShaderFileExtension(verbs.TargetProfile);

        using IDxcUtils utils = Dxc.CreateDxcUtils();
        using IDxcCompiler3 compiler = Dxc.CreateDxcCompiler<IDxcCompiler3>();

        string shaderSource = File.ReadAllText(verbs.InputFile);

        using IDxcIncludeHandler includeHandler = utils.CreateDefaultIncludeHandler();

        string[] arguments =
        {
            "-E", verbs.EntryPoint ??= "main",
            "-T", verbs.TargetProfile,
            verbs.Enable16BitTypes == true ? "-enable-16bit-types" : "",
            verbs.LegacyMacroExpansion == true ? "-legacy-macro-expansion" : "",
            verbs.EnableDebug == true ? "-Zi" : "",
            verbs.DisableOptimizations == true ? "-Od" : "",
            verbs.DisableValidation == true ? "-Vd" : "",
            "-HV", verbs.HlslVersion ?? "2021",
            verbs.CompatibilityMode == true ? "-Gec" : "",
            verbs.StrictMode == true ? "-Ges" : "",
            verbs.SupressWarnings == true ? "-no-warnings" : "",
            verbs.WarningsAsErrors == true ? "-WX" : "",
        };

        using IDxcResult result = compiler.Compile(shaderSource, arguments, includeHandler);
        if (result.GetStatus().Failure)
        {
            Console.Error.WriteLine("Compilation error: " + result.GetErrors());
            return;
        }

        using IDxcBlob compiledShader = result.GetOutput(DxcOutKind.Object);
        if (compiledShader.AsBytes().Length == 0)
        {
            Console.Error.WriteLine("Error: Compiled shader is empty");
            return;
        }

        File.WriteAllBytes(verbs.OutputFile + shaderFileExtension, compiledShader.AsBytes());
    }

    static void CompileSpirv(CompileSpirvVerbs verbs)
    {
        (verbs.InputFile, verbs.OutputFile) = ValidateParameters((verbs.InputFile, verbs.OutputFile, "spv"));

        using SpvCompiler compiler = new();
        CompilerOptions options = new()
        {
            SourceLanguage = ParseEnumOrDefault<SourceLanguage>(verbs.SourceLanguage, null),
            OptimizationLevel = ParseEnumOrDefault<OptimizationLevel>(verbs.OptimizationLevel, null) ?? OptimizationLevel.Performance,
            InvertY = verbs.InvertY,
            GeneratedDebug = verbs.GenerateDebug, 
            SuppressWarnings = verbs.SuppressWarnings,
            WarningsAsErrors = verbs.WarningsAsErrors,
            ShaderStage = ParseEnumOrDefault<ShaderKind>(verbs.ShaderStage, null),
            TargetSpv = ParseEnumOrDefault<SpirVVersion>(verbs.SpirvVersion, null)
        };

        CompileResult result = compiler.Compile(verbs.InputFile, options);
        
        if (result.Status != CompilationStatus.Success)
        {
            Console.Error.WriteLine(result.ErrorMessage);
            return;
        }
        if (result.Bytecode.Length == 0)
        {
            Console.Error.WriteLine("Error: Compiled shader is empty");
            return;
        }

        File.WriteAllBytes(verbs.OutputFile, result.Bytecode);
    }

    static void DisassembleDxbc(DisassembleDxbcVerbs verbs)
    {
        (verbs.InputFile, verbs.OutputFile) = ValidateParameters((verbs.InputFile, verbs.OutputFile, "asm"));

        using Blob compiledShader = DxCompiler.ReadFileToBlob(verbs.InputFile);

        using Blob disassembledShader = DxCompiler.Disassemble(compiledShader.BufferPointer, compiledShader.BufferSize, DisasmFlags.None, "");

        if (disassembledShader.AsBytes().Length == 0)
        {
            Console.Error.WriteLine("Error: Disassembled shader is empty");
            return;
        }

        DxCompiler.WriteBlobToFile(disassembledShader, verbs.OutputFile, true);
    }

    static void DisassembleDxil(DisassembleDxilVerbs verbs)
    {
        (verbs.InputFile, verbs.OutputFile) = ValidateParameters((verbs.InputFile, verbs.OutputFile, "ir"));

        using IDxcCompiler3 compiler = Dxc.CreateDxcCompiler<IDxcCompiler3>();
        using IDxcUtils utils = Dxc.CreateDxcUtils();

        using IDxcBlob compiledShader = utils.LoadFile(verbs.InputFile, null);

        DxcBuffer buffer = new()
        {
            Ptr = compiledShader.BufferPointer,
            Size = (uint)compiledShader.AsBytes().Length,
            Encoding = 0
        };

        using IDxcResult result = compiler.Disassemble<IDxcResult>(buffer);
        if (result.GetStatus().Failure)
        {
            IDxcBlob errors = result.GetOutput(DxcOutKind.Errors);
            Console.Error.WriteLine(errors.ToString());
            return;
        }

        using IDxcBlob disassembledShader = result.GetOutput(DxcOutKind.Disassembly);
        if (disassembledShader.AsBytes().Length == 0)
        {
            Console.Error.WriteLine("Error: Disassembled shader is empty");
            return;
        }

        File.WriteAllBytes(verbs.OutputFile, disassembledShader.AsBytes());
    }

    private static string GetShaderFileExtension(string targetProfile)
    {
        var shaderMap = new Dictionary<string, string>
        {
            { "vs_", "vso" },
            { "ps_", "pso" },
            { "gs_", "gso" },
            { "cs_", "cso" },
            { "hs_", "hso" },
            { "ds_", "dso" },
            { "fx_", "fxo" },
            { "as_", "aso" },
            { "ms_", "mso" }
        };

        string extension = shaderMap.FirstOrDefault(x => targetProfile.StartsWith(x.Key)).Value
                   ?? throw new InvalidOperationException("Invalid target profile");

        return extension;
    }

    private static (string, string) ValidateParameters((string, string?, string) parameters)
    {
        if (!File.Exists(parameters.Item1))
        {
            throw new FileNotFoundException($"File {parameters.Item1} does not exist");
        }

        if (string.IsNullOrEmpty(parameters.Item2))
        {
            string inputFileName = Path.GetFileNameWithoutExtension(parameters.Item1);
            parameters.Item2 = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(parameters.Item1)), $"{inputFileName}.{parameters.Item3}");
        }

        return (parameters.Item1, parameters.Item2);
    }

    private static TEnum? ParseEnumOrDefault<TEnum>(string value, TEnum? defaultValue = null) where TEnum : struct, Enum
    {
        if (!string.IsNullOrWhiteSpace(value) && Enum.TryParse<TEnum>(value, ignoreCase: true, out var result))
        {
            return result;
        }
        return defaultValue;
    }
}

[Verb("comp-dxbc", HelpText = "Compiles an HLSL shader to DXBC format.")]
public class CompileDxbcVerbs
{
    [Option('i', "input", Required = true, HelpText = "Input file. Should be a HLSL shader.")]
    public string InputFile { get; set; }

    [Option('t', "target", Required = true, HelpText = "Target profile. The format should be 'shaderPipeline_shaderModelMajorVersion_shaderModelMinorVersion'.")]
    public string TargetProfile { get; set; }

    [Option('o', "output", HelpText = "Optional. Output DXBC file. Defaults to <input_filename>.bc")]
    public string? OutputFile { get; set; }

    [Option('e', "entry", HelpText = "Optional. Custom entry point.")]
    public string? EntryPoint { get; set; }
}

[Verb("comp-dxil", HelpText = "Compiles an HLSL shader to DXIL format.")]
public class CompileDxilVerbs
{
    [Option('i', "input", Required = true, HelpText = "Input file. Should be an HLSL shader.")]
    public string InputFile { get; set; }

    [Option('t', "target", Required = true, HelpText = "Target profile. The format should be 'shaderPipeline_shaderModelMajorVersion_shaderModelMinorVersion'.")]
    public string TargetProfile { get; set; }

    [Option('e', "entry", HelpText = "Optional. Custom entry point. Defaults to 'main'")]
    public string? EntryPoint { get; set; }

    [Option('v', "version", HelpText = "Optional. HLSL version. Expected values: 2016, 2017, 2018, 2021. Defaults to '2021'.")]
    public string? HlslVersion { get; set; }

    [Option('c', "compat", HelpText = "Optional. Enable backward compatibility mode. Boolean parameter.")]
    public bool? CompatibilityMode { get; set; }

    [Option('s', "strict", HelpText = "Optional. Enable strict mode. Boolean parameter.")]
    public bool? StrictMode { get; set; }

    [Option('w', "supwarn", HelpText = "Optional. Supress warnings. Boolean parameter.")]
    public bool? SupressWarnings { get; set; }

    [Option('W', "warnerr", HelpText = "Optional. Treat warnings as errors. Boolean parameter.")]
    public bool? WarningsAsErrors { get; set; }

    [Option('O', "disopt", HelpText = "Optional. Disable optimizations. Boolean parameter.")]
    public bool? DisableOptimizations { get; set; }

    [Option('V', "disval", HelpText = "Optional. Disable validation. Boolean parameter.")]
    public bool? DisableValidation { get; set; }

    [Option('b', "16bit", HelpText = "Optional. Enable 16 bit types. Boolean parameter.")]
    public bool? Enable16BitTypes { get; set; }

    [Option('m', "macroexp", HelpText = "Optional. Expand the operands before performing token-pasting operation (fxc behavior). Boolean parameter.")]
    public bool? LegacyMacroExpansion { get; set; }

    [Option('d', "debug", HelpText = "Optional. Enable debug information. Boolean parameter.")]
    public bool? EnableDebug { get; set; }

    [Option('o', "output", HelpText = "Optional. Output DXIL file. Defaults to <input_filename>.ll")]
    public string? OutputFile { get; set; }
}

[Verb("comp-spv", HelpText = "Compiles an OpenGL or HLSL shader to SPIR-V format.")]
public class CompileSpirvVerbs
{
    [Option('i', "input", Required = true, HelpText = "Input file. Should be an OpenGL or HLSL shader.")]
    public string InputFile { get; set; }

    [Option('l', "lang", HelpText = "Optional. Shading language used in the source file. Expected values: GLSL, HLSL.")]
    public string? SourceLanguage { get; set; }

    [Option('O', "level", HelpText = "Optional. Level of optimization used in the compilation process. Expected values: zero, size, performance. Defaults to 'performance'.")]
    public string? OptimizationLevel { get; set; }

    [Option('y', "invert", HelpText = "Optional. Invert Y Axis. Boolean parameter.")]
    public bool? InvertY { get; set; }

    [Option('d', "debug", HelpText = "Optional. Generate debug information. Boolean parameter. ")]
    public bool? GenerateDebug { get; set; }

    [Option('w', "supwarn", HelpText = "Optional. Supress compilation related warnings. Boolean parameter. ")]
    public bool? SuppressWarnings { get; set; }

    [Option('e', "warnerr", HelpText = "Optional. Expose compilation related warnings as errors. Boolean parameter. ")]
    public bool? WarningsAsErrors { get; set; }

    [Option('s', "stage", HelpText = "Optional. Shader type. Expected values: VertexShader, FragmentShader, ComputeShader, GeometryShader, TessControlShader, TessEvaluationShader, GLSL_VertexShader, GLSL_FragmentShader, GLSL_ComputeShader, GLSL_GeometryShader, GLSL_TessControlShader, GLSL_TessEvaluationShader, GLSL_InferFromSource, GLSL_DefaultVertexShader, GLSL_DefaultFragmentShader, GLSL_DefaultComputeShader, GLSL_DefaultGeometryShader, GLSL_DefaultTessControlShader, GLSL_DefaultTessEvaluationShader, SPIRVAssembly, RaygenShader, AnyHitShader, ClosestHitShader, MissShader, IntersectionShader, CallableShader, GLSL_RaygenShader, GLSL_AnyHitShader, GLSL_ClosestHitShader, GLSL_MissShader, GLSL_IntersectionShader, GLSL_CallableShader, shaderc_glsl_default_raygen_shader, GLSL_DefaultAnyHitShader, GLSL_DefaultClosestHitShader, GLSL_DefaultMissShader, GLSL_DefaultIntersectionShader, GLSL_DefaultCallableShader, TaskShader, MeshShader, GLSL_TaskShader, GLSL_MeshShader, GLSL_DefaultTaskShader, GLSL_DefaultMeshShader.")]
    public string? ShaderStage { get; set; }

    [Option('v', "version", HelpText = "Optional. Target SPIR-V version. Expected values: Version_1_0, Version_1_1, Version_1_2, Version_1_3, Version_1_4, Version_1_5, Version_1_6.")]
    public string? SpirvVersion { get; set; }

    [Option('o', "output", HelpText = "Optional. Output SPIR-V file. Defaults to <input_filename>.spv")]
    public string? OutputFile { get; set; }
}

[Verb("disasm-dxbc", HelpText = "Disassembles DXBC bytecode to x86 assembly code.")]
public class DisassembleDxbcVerbs
{
    [Option('i', "input", Required = true, HelpText = "Input file. Should be a DXBC compiled shader.")]
    public string InputFile { get; set; }

    [Option('o', "output", HelpText = "Optional. Output x86-Asm file. Defaults to <input_filename>.asm")]
    public string? OutputFile { get; set; }

    //[Option('f', "flags", HelpText = "Optional. Flags affecting the behavior of D3DDisassemble. Defaults to 0 (none). For more info, check D3DCompiler docs.")]
    //public uint? Flags { get; set; }
}

[Verb("disasm-dxil", HelpText = "Disassembles DXIL bytecode to LLVM IR code.")]
public class DisassembleDxilVerbs
{
    [Option('i', "input", Required = true, HelpText = "Input file. Should be a DXIL compiled shader.")]
    public string InputFile { get; set; }

    [Option('o', "output", HelpText = "Optional. Output LLVM IR file. Defaults to <input_filename>.ir")]
    public string? OutputFile { get; set; }
}
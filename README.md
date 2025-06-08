# SimpleShaderCompiler

SSC is a command-line shader compiler for DXBC, DXIL, and SPIR-V formats. Its purpose is to simplify shader compilation for modding projects or other works that aren’t heavily focused on shading.

This tool is made with C# (targeting .NET 9) and is powered by [Vortice.Windows](https://github.com/amerkoleci/Vortice.Windows) and [Vortice.Vulkan](https://github.com/amerkoleci/Vortice.Vulkan). What makes this tool useful is how easy it is to build, modify, and use compared to official tools (like DXC and SPIRV-Cross). While it may provide fewer options, it’s more lightweight and doesn’t require setting up an LLVM environment.

## Usage

```
ssc.exe <option> -arg1 param -arg2
```

## Options

```
comp-dxbc     Compiles a HLSL shader to DXBC format.
comp-dxil     Compiles a HLSL shader to DXIL format.
comp-spv      Compiles an OpenGL or HLSL shader to SPIR-V format.
disasm-dxbc   Disassembles DXBC bytecode to x86 assembly code.
disasm-dxil   Disassembles DXIL bytecode to LLVM IR code.
help          Display more information on a specific command.
version       Display version information.
```

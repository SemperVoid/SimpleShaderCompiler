# SimpleShaderCompiler

SSC is a command line DXBC/DXIL/SPIR-V shader compiler. Its purpose is to simplify the shader compilation for modding projects or other non heavily shading focused works.

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

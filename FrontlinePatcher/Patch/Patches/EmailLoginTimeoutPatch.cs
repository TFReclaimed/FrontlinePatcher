using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Spectre.Console;

namespace FrontlinePatcher.Patch.Patches;

public class EmailLoginTimeoutPatch : Patch
{
    public override string Name => "Fix Email Login Timing Out";

    public override bool Apply(ModuleDefMD module)
    {
        var gameShellType = module.Find("GameShell", true);
        if (gameShellType is null)
        {
            AnsiConsole.MarkupLine("[red]  GameShell type not found![/]");
            return false;
        }

        var npResultType = module.Find("NPA.NPResult", true);
        if (npResultType is null)
        {
            AnsiConsole.MarkupLine("[red]  NPA.NPResult type not found![/]");
            return false;
        }

        var npResultSig = npResultType.ToTypeSig();

        // void OnResult(NPA.NPResult)
        var targetSig = MethodSig.CreateInstance(
            module.CorLibTypes.Void,
            npResultSig
        );

        var targetMethod = gameShellType.FindMethod("OnResult", targetSig);
        if (targetMethod is null)
        {
            AnsiConsole.MarkupLine("[red]  GameShell.OnResult method not found![/]");
            return false;
        }

        AnsiConsole.WriteLine($"  OnResult method found! {targetMethod.FullName}");

        // Modify method body
        var body = targetMethod.Body;
        var instructions = body.Instructions;

        body.KeepOldMaxStack = false;

        // 9997 -> 9993
        instructions[10].Operand = 0x2709;

        // Add switch cases
        var switchInstruction = instructions[12];
        var switchOperands = (Instruction[]) switchInstruction.Operand;
        var operand = switchOperands[0];
        var newOperands = new Instruction[switchOperands.Length + 4];
        Array.Copy(switchOperands, newOperands, switchOperands.Length);
        for (var i = 0; i < 4; i++)
        {
            newOperands[switchOperands.Length + i] = operand;
        }

        switchInstruction.Operand = newOperands;

        AnsiConsole.MarkupLine("  Successfully modified method body!");
        return true;
    }
}
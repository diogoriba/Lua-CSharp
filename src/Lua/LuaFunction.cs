using System.Runtime.CompilerServices;
using Lua.Runtime;

namespace Lua;

public class LuaFunction(string name, Func<LuaFunctionExecutionContext, Memory<LuaValue>, CancellationToken, ValueTask<int>> func)
{
    public string Name { get; } = name;
    internal Func<LuaFunctionExecutionContext, Memory<LuaValue>, CancellationToken, ValueTask<int>> Func { get; } = func;

    public LuaFunction(Func<LuaFunctionExecutionContext, Memory<LuaValue>, CancellationToken, ValueTask<int>> func) : this("anonymous", func)
    {
    }

    public async ValueTask<int> InvokeAsync(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken cancellationToken)
    {
        var frame = new CallStackFrame
        {
            Base = context.FrameBase,
            VariableArgumentCount = this is LuaClosure closure ? Math.Max(context.ArgumentCount - closure.Proto.ParameterCount, 0) : 0,
            Function = this,
        };

        context.Thread.PushCallStackFrame(frame);


        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (context.Thread.CallOrReturnHookMask.Value != 0 && !context.Thread.IsInHook)
            {
                var result1 = await LuaVirtualMachine.ExecuteCallHook(context, buffer, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return result1;
            }

            var result2 = await Func(context, buffer, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return result2;
        }
        finally
        {
            context.Thread.PopCallStackFrame();
        }
    }
}
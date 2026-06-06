using Ferbento.Core.Math;
using Ferbento.Scripting.Api;
using Ferbento.Scripting.Debugger;
using GDShrapt.Reader;

namespace Ferbento.Scripting.Interpreter;

public sealed class ScriptInterpreter
{
    private Environment _environment = new();
    private readonly Pico8Context _context;

    public Environment CurrentEnvironment => _environment;
    public Environment Globals => _environment;

    public ScriptInterpreter(Pico8Context context) => _context = context;

    public void SetGlobals(Environment env) => _environment = env;

    public void LoadScript(GDClassDeclaration script)
    {
        foreach (var method in script.Methods)
            RegisterFunction(method);
    }

    private void RegisterFunction(GDMethodDeclaration method)
    {
        var name = method.Identifier.Sequence;
        var parameters = method.Parameters?
            .Select(p => p.Identifier.Sequence)
            .ToList() ?? [];
        var fn = new UserFunction
        {
            Name = name,
            Parameters = parameters,
            Body = method.Statements
        };
        _environment.Define(name, ScriptValue.Func(fn));
    }

    public ScriptValue CallFunction(string name, ScriptValue[]? args = null)
    {
        var fn = _environment.Get(name);
        if (fn.Type == ScriptValueType.Nil)
            return ScriptValue.Nil;
        return CallValue(fn, args ?? []);
    }

    public bool HasFunction(string name)
    {
        var v = _environment.Get(name);
        return v.Type is ScriptValueType.Function or ScriptValueType.NativeFunction;
    }

    public void ResumeCoroutine(Coroutine co)
    {
        if (co.Body == null || co.Status == CoroutineStatus.Dead) return;
        var prev = _environment;
        _environment = co.Environment;
        co.Status = CoroutineStatus.Running;
        try
        {
            var statements = co.Body.ToList();
            for (; co.StatementIndex < statements.Count; co.StatementIndex++)
            {
                _context.CheckCpuBudget();
                ExecuteStatement(statements[co.StatementIndex]);
            }
            co.Status = CoroutineStatus.Dead;
        }
        catch (YieldSignal)
        {
            co.StatementIndex++;
            co.Status = CoroutineStatus.Suspended;
        }
        catch (ReturnSignal)
        {
            co.Status = CoroutineStatus.Dead;
        }
        finally
        {
            _environment = prev;
        }
    }

    public ScriptValue ExecuteBlock(GDStatementsList? block)
    {
        if (block == null) return ScriptValue.Nil;
        foreach (var stmt in block)
        {
            _context.TickCpu();
            _context.CheckCpuBudget();
            var result = ExecuteStatement(stmt);
            if (result != ScriptValue.Nil) return result;
        }
        return ScriptValue.Nil;
    }

    public ScriptValue ExecuteStatement(GDStatement stmt)
    {
        var bp = _context.Breakpoints;
        if (bp.BreakpointLines.Count > 0 || bp.IsPaused || bp.StepOver || bp.StepInto || bp.StepOut)
        {
            if (bp.CheckBreakpoint(stmt.StartLine, _context.CallDepth))
                bp.WaitWhilePaused();
        }

        switch (stmt)
        {
            case GDVariableDeclarationStatement varDecl:
                var init = varDecl.Initializer != null ? Evaluate(varDecl.Initializer) : ScriptValue.Nil;
                _environment.Define(varDecl.Identifier.Sequence, init);
                return ScriptValue.Nil;

            case GDExpressionStatement exprStmt:
                if (exprStmt.Expression is GDReturnExpression retExpr)
                    throw new ReturnSignal(retExpr.Expression != null ? Evaluate(retExpr.Expression) : ScriptValue.Nil);
                return Evaluate(exprStmt.Expression);

            case GDIfStatement ifStmt:
                return ExecuteIf(ifStmt);

            case GDWhileStatement whileStmt:
                while (Evaluate(whileStmt.Condition).IsTruthy())
                {
                    ExecuteBlock(whileStmt.Statements);
                    _context.TickCpu();
                    _context.CheckCpuBudget();
                }
                return ScriptValue.Nil;

            case GDForStatement forStmt:
                return ExecuteFor(forStmt);

            default:
                return ScriptValue.Nil;
        }
    }

    private ScriptValue ExecuteIf(GDIfStatement ifStmt)
    {
        if (Evaluate(ifStmt.IfBranch.Condition).IsTruthy())
            return ExecuteBlock(ifStmt.IfBranch.Statements);

        if (ifStmt.ElifBranchesList != null)
        {
            foreach (var elif in ifStmt.ElifBranchesList)
            {
                if (Evaluate(elif.Condition).IsTruthy())
                    return ExecuteBlock(elif.Statements);
            }
        }

        if (ifStmt.ElseBranch != null)
            return ExecuteBlock(ifStmt.ElseBranch.Statements);

        return ScriptValue.Nil;
    }

    private ScriptValue ExecuteFor(GDForStatement forStmt)
    {
        var collection = Evaluate(forStmt.Collection);
        var varName = forStmt.Variable.Sequence;

        if (collection.Type == ScriptValueType.Table)
        {
            var table = collection.AsTable();
            foreach (var key in table.ArrayKeys)
            {
                _environment.Set(varName, table.Get(key));
                ExecuteBlock(forStmt.Statements);
                _context.TickCpu();
            }
            return ScriptValue.Nil;
        }

        if (collection.Type == ScriptValueType.String)
        {
            var str = collection.AsString();
            for (var i = 0; i < str.Length; i++)
            {
                _environment.Set(varName, ScriptValue.Str(str[i].ToString()));
                ExecuteBlock(forStmt.Statements);
                _context.TickCpu();
            }
        }

        return ScriptValue.Nil;
    }

    public ScriptValue Evaluate(GDExpression? expr)
    {
        if (expr == null) return ScriptValue.Nil;

        _context.TickCpu();

        switch (expr)
        {
            case GDNumberExpression num:
                return ScriptValue.Number(ReadNumber(num.Number));

            case GDStringExpression str:
                return ScriptValue.Str(ExtractString(str.String));

            case GDBoolExpression boo:
                return boo.Value == true ? ScriptValue.True : ScriptValue.False;

            case GDIdentifierExpression id:
                return _environment.Get(id.Identifier.Sequence);

            case GDDualOperatorExpression bin:
                return EvaluateBinary(bin);

            case GDSingleOperatorExpression unary:
                return EvaluateUnaryOp(unary);

            case GDCallExpression call:
                return EvaluateCall(call);

            case GDMemberOperatorExpression member:
                return EvaluateMember(member);

            case GDIndexerExpression indexer:
                return EvaluateIndex(indexer);

            case GDArrayInitializerExpression arr:
                return EvaluateArray(arr);

            case GDDictionaryInitializerExpression dict:
                return EvaluateDictionary(dict);

            case GDReturnExpression ret:
                var val = ret.Expression != null ? Evaluate(ret.Expression) : ScriptValue.Nil;
                throw new ReturnSignal(val);

            case GDYieldExpression:
                throw YieldSignal.Instance;

            default:
                return ScriptValue.Nil;
        }
    }

    private static string ExtractString(GDStringNode? node)
    {
        if (node == null) return "";
        return node.ToString().Trim('"', '\'');
    }

    private ScriptValue EvaluateBinary(GDDualOperatorExpression bin)
    {
        var left = Evaluate(bin.LeftExpression);
        var right = Evaluate(bin.RightExpression);

        return bin.OperatorType switch
        {
            GDDualOperatorType.Addition => Add(left, right),
            GDDualOperatorType.Subtraction => ScriptValue.Number(left.AsNumber() - right.AsNumber()),
            GDDualOperatorType.Multiply => ScriptValue.Number(left.AsNumber() * right.AsNumber()),
            GDDualOperatorType.Division => right.AsNumber().Raw == 0
                ? ScriptValue.Number(0)
                : ScriptValue.Number(left.AsNumber() / right.AsNumber()),
            GDDualOperatorType.Mod => ScriptValue.Number(left.AsNumber() % right.AsNumber()),
            GDDualOperatorType.Equal => Bool(ValuesEqual(left, right)),
            GDDualOperatorType.NotEqual => Bool(!ValuesEqual(left, right)),
            GDDualOperatorType.LessThan => Bool(left.AsNumber().Raw < right.AsNumber().Raw),
            GDDualOperatorType.MoreThan => Bool(left.AsNumber().Raw > right.AsNumber().Raw),
            GDDualOperatorType.LessThanOrEqual => Bool(left.AsNumber().Raw <= right.AsNumber().Raw),
            GDDualOperatorType.MoreThanOrEqual => Bool(left.AsNumber().Raw >= right.AsNumber().Raw),
            GDDualOperatorType.And or GDDualOperatorType.And2 =>
                Bool(left.IsTruthy() && right.IsTruthy()),
            GDDualOperatorType.Or or GDDualOperatorType.Or2 =>
                Bool(left.IsTruthy() || right.IsTruthy()),
            GDDualOperatorType.Assignment => Assign(bin.LeftExpression, right),
            GDDualOperatorType.BitwiseAnd => ScriptValue.Number(FixedPoint.Band(left.AsInt(), right.AsInt())),
            GDDualOperatorType.BitwiseOr => ScriptValue.Number(FixedPoint.Bor(left.AsInt(), right.AsInt())),
            GDDualOperatorType.Xor => ScriptValue.Number(FixedPoint.Bxor(left.AsInt(), right.AsInt())),
            GDDualOperatorType.BitShiftLeft => ScriptValue.Number(FixedPoint.Shl(left.AsInt(), right.AsInt())),
            GDDualOperatorType.BitShiftRight => ScriptValue.Number(FixedPoint.Shr(left.AsInt(), right.AsInt())),
            _ => ScriptValue.Nil
        };
    }

    private ScriptValue Add(ScriptValue left, ScriptValue right)
    {
        if (left.Type == ScriptValueType.String || right.Type == ScriptValueType.String)
        {
            var result = left.AsString() + right.AsString();
            _context.ValidateStringLength(result);
            return ScriptValue.Str(result);
        }
        return ScriptValue.Number(left.AsNumber() + right.AsNumber());
    }

    private ScriptValue Assign(GDExpression? target, ScriptValue value)
    {
        if (target is GDIdentifierExpression id)
        {
            _environment.Set(id.Identifier.Sequence, value);
            return value;
        }
        if (target is GDIndexerExpression idx)
        {
            var table = Evaluate(idx.CallerExpression);
            var key = Evaluate(idx.InnerExpression);
            if (table.Type == ScriptValueType.Table)
            {
                if (key.Type == ScriptValueType.Number)
                    table.AsTable().Set(key.AsInt(), value);
                else
                    table.AsTable().Set(key.AsString(), value);
            }
            return value;
        }
        return value;
    }

    private ScriptValue EvaluateUnaryOp(GDSingleOperatorExpression unary)
    {
        var val = Evaluate(unary.TargetExpression);
        return unary.OperatorType switch
        {
            GDSingleOperatorType.Not or GDSingleOperatorType.Not2 => Bool(!val.IsTruthy()),
            GDSingleOperatorType.Negate => ScriptValue.Number(-val.AsNumber()),
            GDSingleOperatorType.BitwiseNegate => ScriptValue.Number(FixedPoint.Bnot(val.AsInt())),
            _ => val
        };
    }

    private ScriptValue EvaluateCall(GDCallExpression call)
    {
        var callee = Evaluate(call.CallerExpression);
        var args = call.Parameters?
            .Select(Evaluate)
            .ToArray() ?? [];
        return CallValue(callee, args);
    }

    private ScriptValue CallValue(ScriptValue callee, ScriptValue[] args)
    {
        if (_context.CallDepth >= _context.MaxCallDepth)
            throw new InvalidOperationException("Call depth limit exceeded.");

        _context.CallDepth++;
        try
        {
            if (callee.Type == ScriptValueType.NativeFunction)
            {
                _context.TickCpu();
                _context.CheckCpuBudget();
                return callee.AsNative()(args);
            }

            if (callee.Type != ScriptValueType.Function)
                return ScriptValue.Nil;

            var fn = callee.AsFunction();
            var env = fn.Closure?.Child() ?? _environment.Child();
            for (var i = 0; i < fn.Parameters.Count; i++)
                env.Define(fn.Parameters[i], i < args.Length ? args[i] : ScriptValue.Nil);

            var prev = _environment;
            _environment = env;
            try
            {
                return ExecuteBlock(fn.Body);
            }
            catch (ReturnSignal ret)
            {
                return ret.Value;
            }
            finally
            {
                _environment = prev;
            }
        }
        finally
        {
            _context.CallDepth--;
        }
    }

    private ScriptValue EvaluateMember(GDMemberOperatorExpression member)
    {
        var obj = Evaluate(member.CallerExpression);
        var name = member.Identifier.Sequence;
        if (obj.Type == ScriptValueType.Table)
            return obj.AsTable().Get(name);
        return ScriptValue.Nil;
    }

    private ScriptValue EvaluateIndex(GDIndexerExpression indexer)
    {
        var obj = Evaluate(indexer.CallerExpression);
        var key = Evaluate(indexer.InnerExpression);
        if (obj.Type != ScriptValueType.Table) return ScriptValue.Nil;
        var table = obj.AsTable();
        return key.Type == ScriptValueType.Number
            ? table.Get(key.AsInt())
            : table.Get(key.AsString());
    }

    private ScriptValue EvaluateArray(GDArrayInitializerExpression arr)
    {
        var table = new ScriptTable();
        if (arr.Values != null)
        {
            var i = 1;
            foreach (var el in arr.Values)
            {
                table.Set(i++, Evaluate(el));
            }
        }
        return ScriptValue.Table(table);
    }

    private ScriptValue EvaluateDictionary(GDDictionaryInitializerExpression dict)
    {
        var table = new ScriptTable();
        if (dict.KeyValues != null)
        {
            foreach (var kv in dict.KeyValues)
            {
                var key = Evaluate(kv.Key);
                var val = Evaluate(kv.Value);
                if (key.Type == ScriptValueType.String)
                    table.Set(key.AsString(), val);
                else
                    table.Set(key.AsInt(), val);
            }
        }
        return ScriptValue.Table(table);
    }

    private static bool ValuesEqual(ScriptValue a, ScriptValue b)
    {
        if (a.Type != b.Type) return false;
        return a.Type switch
        {
            ScriptValueType.Nil => true,
            ScriptValueType.Number => a.AsNumber().Raw == b.AsNumber().Raw,
            ScriptValueType.String => a.AsString() == b.AsString(),
            ScriptValueType.Bool => a.AsBool() == b.AsBool(),
            _ => ReferenceEquals(a.Data, b.Data)
        };
    }

    private static double ReadNumber(GDNumber number)
    {
        try { return number.ValueDouble; }
        catch { return number.ValueInt64; }
    }

    private static ScriptValue Bool(bool b) => b ? ScriptValue.True : ScriptValue.False;
}

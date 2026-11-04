using System.Text;
using Ferbento.Scripting.Api;
using Ferbento.Scripting.Compiler.Analysis;
using Ferbento.Scripting.Compiler.Diagnostics;
using Ferbento.Scripting.Compiler.Ir;
using Ferbento.Scripting.Compiler.Parsing;

namespace Ferbento.Scripting.Tests.Compiler;

public class StaticAnalyzerTests
{
    // A small fixed API, so the tests do not depend on what the runtime registers today.
    private static readonly AnalysisOptions Options = new()
    {
        Api = new ApiAvailability(
            new HashSet<string> { "print", "cls", "spr", "oval", "btn" },
            new HashSet<string> { "cls", "spr", "btn" }),
    };

    private static (ParseResult Parse, AnalysisResult Analysis) Analyze(string source)
    {
        var parse = LuaParser.Parse(source);
        Assert.True(parse.Diagnostics.IsEmpty, string.Join("\n", parse.Diagnostics));
        return (parse, StaticAnalyzer.Analyze(parse, Options));
    }

    private static List<string> Codes(string source) => [.. Analyze(source).Analysis.Diagnostics.Select(d => d.Code)];

    private static Diagnostic Single(string source, string code)
    {
        var diagnostics = Analyze(source).Analysis.Diagnostics;
        Assert.True(diagnostics.Count(d => d.Code == code) == 1, string.Join("\n", diagnostics));
        return diagnostics.First(d => d.Code == code);
    }

    private static void NoDiagnostics(string source)
    {
        var diagnostics = Analyze(source).Analysis.Diagnostics;
        Assert.True(diagnostics.IsEmpty, string.Join("\n", diagnostics));
    }

    /// <summary>All name occurrences in source order (depth-first over the IR).</summary>
    private static List<IrName> Names(IrNode node)
    {
        var names = new List<IrName>();
        Walk(node);
        return names;

        void Walk(object? value)
        {
            switch (value)
            {
                case IrName name:
                    names.Add(name);
                    break;
                case IrNode child:
                    foreach (var property in child.GetType().GetProperties())
                    {
                        if (property.Name is "EqualityContract" or "Span") continue;
                        Walk(property.GetValue(child));
                    }
                    break;
                case System.Collections.IEnumerable items and not string:
                    foreach (var item in items) Walk(item);
                    break;
            }
        }
    }

    private static List<NameResolution> Resolutions(string source, string name)
    {
        var (parse, analysis) = Analyze(source);
        return [.. Names(parse.Chunk).Where(n => n.Name == name).Select(analysis.Resolve)];
    }

    // ---- name resolution ----

    [Fact]
    public void Resolve_LocalsAndGlobals()
    {
        var (parse, analysis) = Analyze("local a = 1\nb = a");
        var names = Names(parse.Chunk);
        Assert.Equal(NameKind.Global, analysis.Resolve(names.Single(n => n.Name == "b")).Kind);
        var a = analysis.Resolve(names.Single(n => n.Name == "a"));
        Assert.Equal(NameKind.Local, a.Kind);
        Assert.Same(analysis.GetDeclaredLocals(parse.Chunk.Body[0])[0], a.Variable);
        Assert.Contains("b", analysis.AssignedGlobals);
    }

    [Fact]
    public void Resolve_ShadowingCreatesDistinctVariables()
    {
        var x = Resolutions("local x = 1\ndo local x = 2 y = x end\nz = x", "x");
        Assert.Equal(2, x.Count);
        Assert.Equal(1, x[0].Variable!.Index);
        Assert.Equal(0, x[1].Variable!.Index);
    }

    [Fact]
    public void Resolve_LocalInitializerSeesTheOuterName()
    {
        var x = Resolutions("x = 1\nlocal x = x", "x");
        Assert.Equal([NameKind.Global, NameKind.Global], x.Select(r => r.Kind));
    }

    [Fact]
    public void Resolve_Upvalue_FromEnclosingFunction()
    {
        var (parse, analysis) = Analyze("local n = 0\nfunction inc() n += 1 end");
        var function = Assert.IsType<IrFunctionStmt>(parse.Chunk.Body[1]);
        var n = analysis.Resolve(Names(function.Function).Single(name => name.Name == "n"));
        Assert.Equal(NameKind.Upvalue, n.Kind);
        Assert.Equal(0, n.UpvalueIndex);
        Assert.True(n.Variable!.IsCaptured);

        var upvalue = Assert.Single(analysis.GetFunction(function.Function).Upvalues);
        Assert.True(upvalue.FromParentLocal);
        Assert.Same(n.Variable, upvalue.Variable);
    }

    [Fact]
    public void Resolve_Upvalue_ThroughTwoFunctions()
    {
        var (parse, analysis) = Analyze("local n\nfunction a()\n  return function() return n end\nend");
        var outer = Assert.IsType<IrFunctionStmt>(parse.Chunk.Body[1]).Function;
        var inner = Assert.IsType<IrFunction>(Assert.Single(Assert.IsType<IrReturn>(outer.Body[0]).Values));

        var middle = Assert.Single(analysis.GetFunction(outer).Upvalues);
        Assert.True(middle.FromParentLocal);
        var innermost = Assert.Single(analysis.GetFunction(inner).Upvalues);
        Assert.False(innermost.FromParentLocal);
        Assert.Equal(0, innermost.ParentUpvalueIndex);
        Assert.Same(middle.Variable, innermost.Variable);
    }

    [Fact]
    public void Resolve_LocalFunctionSeesItself()
    {
        var f = Resolutions("local function f() return f() end", "f");
        Assert.Equal(NameKind.Upvalue, Assert.Single(f).Kind);
    }

    [Fact]
    public void Resolve_LoopVariablesAreScopedToTheBody()
    {
        Assert.Equal([NameKind.Local, NameKind.Global], Resolutions("for i = 1, 3 do x = i end\ny = i", "i").Select(r => r.Kind));
        Assert.Equal([NameKind.Local], Resolutions("for k in all(t) do x = k end", "k").Select(r => r.Kind));
    }

    [Fact]
    public void Resolve_UntilSeesTheBodyLocals()
    {
        Assert.Equal(NameKind.Local, Assert.Single(Resolutions("repeat local done = btn(4) until done", "done")).Kind);
    }

    [Fact]
    public void Resolve_MethodSelf_AndFunctionName()
    {
        var (parse, analysis) = Analyze("obj = {}\nfunction obj:m() return self end");
        var statement = Assert.IsType<IrFunctionStmt>(parse.Chunk.Body[1]);
        Assert.Equal(["self"], analysis.GetDeclaredLocals(statement.Function).Select(v => v.Name));
        Assert.Equal(NameKind.Local, analysis.Resolve(Names(statement.Function).Single()).Kind);
        Assert.Equal(NameKind.Global, analysis.ResolveFunctionName(statement).Kind);
    }

    [Fact]
    public void Resolve_UserEnv_TurnsFreeNamesIntoItsFields()
    {
        var (parse, analysis) = Analyze("local _ENV = {}\nx = 1");
        var x = analysis.Resolve(Names(parse.Chunk).Single(n => n.Name == "x"));
        Assert.Equal(NameKind.Global, x.Kind);
        Assert.Equal(NameKind.Local, x.Environment!.Kind);
        Assert.DoesNotContain("x", analysis.AssignedGlobals);

        Assert.Equal(NameKind.Environment, Assert.Single(Resolutions("t = _ENV", "_ENV")).Kind);
    }

    [Fact]
    public void Resolve_IsByNodeIdentity_NotByRecordEquality()
    {
        // Two equal records (same name, no span) in different places resolve differently.
        var global = new IrName("x");
        var local = new IrName("x");
        Assert.Equal(global, local);
        var chunk = new IrChunk([
            new IrAssign([new IrName("y")], [global]),
            new IrLocal(["x"], []),
            new IrAssign([new IrName("y")], [local]),
        ]);
        var analysis = StaticAnalyzer.Analyze(chunk, Options);
        Assert.Equal(NameKind.Global, analysis.Resolve(global).Kind);
        Assert.Equal(NameKind.Local, analysis.Resolve(local).Kind);
    }

    [Fact]
    public void Functions_GlobalsAndHiddenForSlots()
    {
        var (parse, analysis) = Analyze("function f() for i = 1, 2 do x = i end end");
        var info = analysis.GetFunction(Assert.IsType<IrFunctionStmt>(parse.Chunk.Body[0]).Function);
        Assert.Equal(4, info.MaxActiveLocals);
        Assert.True(info.UsesGlobals);
        Assert.True(analysis.MainChunk.UsesGlobals);
        Assert.True(analysis.MainChunk.IsVararg);
        Assert.False(info.IsVararg);
    }

    // ---- errors ----

    [Fact]
    public void Break_OutsideLoop()
    {
        Assert.Equal(1, Single("break", "FB0201").Span.Line);
        Single("while true do\n  local f = function() break end\nend", "FB0201");
        NoDiagnostics("while true do if btn(0) then break end end");
        NoDiagnostics("for i = 1, 2 do break end\nrepeat break until false");
    }

    [Fact]
    public void Goto_WithoutVisibleLabel()
    {
        Assert.Contains("'nowhere'", Single("goto nowhere", "FB0202").Message);
        Single("::top::\nlocal f = function() goto top end", "FB0202");
        Single("do ::inner:: end\ngoto inner", "FB0202");
        NoDiagnostics("goto done\n::done::");
        NoDiagnostics("::top::\nif btn(0) then goto top end");
    }

    [Fact]
    public void Goto_IntoTheScopeOfALocal()
    {
        var error = Single("goto skip\nlocal x = 1\n::skip::\ncls(x)", "FB0203");
        Assert.Equal("o 'goto skip' pula para dentro do escopo do local 'x'", error.Message);
        Assert.Equal(1, error.Span.Line);
        // In 'repeat', a label before 'until' is still inside the body's scope.
        Single("repeat\n  goto c\n  local x = 1\n  ::c::\nuntil x", "FB0203");
    }

    [Fact]
    public void Goto_ContinueIdiom_AndBackwardJumps_AreFine()
    {
        NoDiagnostics("for i = 1, 3 do\n  if i == 2 then goto continue end\n  local y = i\n  cls(y)\n  ::continue::\nend");
        NoDiagnostics("do\n  goto last\n  local x = 1\n  cls(x)\n  ::last::\n  ::really_last::\nend");
        NoDiagnostics("::top::\nlocal x = 1\nif x then goto top end");
    }

    [Fact]
    public void Label_DuplicateInTheSameBlock()
    {
        var error = Single("::a::\ncls()\n::a::", "FB0204");
        Assert.Contains("linha 1", error.Message);
        Assert.Equal(3, error.Span.Line);
        NoDiagnostics("::a::\ndo ::a:: end");
        NoDiagnostics("function f() ::a:: end\nfunction g() ::a:: end");
    }

    [Fact]
    public void Vararg_OutsideVarargFunction()
    {
        Single("function f() return ... end", "FB0205");
        Single("function f(...) return function() return ... end end", "FB0205");
        NoDiagnostics("function f(a, ...) return ... end");
        NoDiagnostics("local args = {...}");
    }

    private static string ManyLocals(int count)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < count; i++) sb.Append($"local v{i} = {i}\n");
        return sb.ToString();
    }

    [Fact]
    public void Locals_Limit()
    {
        Single(ManyLocals(201), "FB0206");
        NoDiagnostics(ManyLocals(200));
        // Each 'for' takes 3 hidden slots plus its variable.
        Single(ManyLocals(197) + "for i = 1, 2 do end", "FB0206");
        NoDiagnostics(ManyLocals(196) + "for i = 1, 2 do end");
        // Locals that left scope do not count.
        NoDiagnostics("do\n" + ManyLocals(150) + "end\n" + ManyLocals(150));
    }

    /// <summary>A function that reads <paramref name="count"/> locals of two enclosing levels as upvalues.</summary>
    private static string ManyUpvalues(int count, bool readsGlobal)
    {
        var sb = new StringBuilder();
        var outer = Math.Min(count, 150);
        for (var i = 0; i < outer; i++) sb.Append($"local a{i} = {i}\n");
        sb.Append("function wrap()\n");
        for (var i = 0; i < count - outer; i++) sb.Append($"  local b{i} = {i}\n");
        sb.Append("  return function()\n    local s = 0\n");
        for (var i = 0; i < outer; i++) sb.Append($"    s += a{i}\n");
        for (var i = 0; i < count - outer; i++) sb.Append($"    s += b{i}\n");
        if (readsGlobal) sb.Append("    cls(s)\n");
        sb.Append("    return s\n  end\nend\n");
        return sb.ToString();
    }

    [Fact]
    public void Upvalues_Limit_CountsTheImplicitEnv()
    {
        NoDiagnostics(ManyUpvalues(255, readsGlobal: false));
        NoDiagnostics(ManyUpvalues(254, readsGlobal: true));
        Single(ManyUpvalues(256, readsGlobal: false), "FB0207");
        Single(ManyUpvalues(255, readsGlobal: true), "FB0207");
    }

    // ---- warnings ----

    [Fact]
    public void UndefinedGlobal_WithSuggestion()
    {
        var warning = Single("player = {}\nplyer.x = 1", "FB0250");
        Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
        Assert.Equal("a global 'plyer' é lida, mas nunca recebe valor no programa; você quis dizer 'player'?", warning.Message);
        Assert.Equal((2, 1), (warning.Span.Line, warning.Span.Column));

        Assert.Equal("a global 'zzz' é lida, mas nunca recebe valor no programa", Single("cls(zzz)", "FB0250").Message);
        Assert.Contains("'spr'", Single("sprr(1)", "FB0250").Message);
    }

    [Fact]
    public void UndefinedGlobal_AssignedAnywhere_IsFine()
    {
        NoDiagnostics("function _init() score = 0 end\nfunction _draw() cls(score) end");
        NoDiagnostics("function helper() end\nhelper()");
        NoDiagnostics("x += 1");
        NoDiagnostics("local _ENV = {}\ncls = y");
    }

    [Fact]
    public void UnimplementedApiFunction()
    {
        Assert.Equal("'oval' é da API do PICO-8, mas ainda não existe no Ferbento", Single("oval(1, 2, 3, 4)", "FB0251").Message);
        Single("?\"hi\"", "FB0251");
        NoDiagnostics("spr(1, 0, 0)");
        NoDiagnostics("function oval() end\noval()");
    }

    [Fact]
    public void NumberWrapsAround()
    {
        Assert.Equal("o número 40000 não cabe no 16.16 e vira -25536", Single("x = 40000", "FB0252").Message);
        Single("x = 32768.5", "FB0252");
        NoDiagnostics("x = 0xffff\ny = 0b1111111111111111\nz = -32768\nw = 32767.99");
    }

    [Fact]
    public void Ignore_TrailingComment_SilencesThatLine()
    {
        NoDiagnostics("x = y -- fb-ignore: FB0250");
        NoDiagnostics("x = y // fb-ignore: FB0250");
        NoDiagnostics("x = y -- fb-ignore");
        Assert.Equal(["FB0250"], Codes("x = y -- fb-ignore: FB0251"));
        Assert.Equal(["FB0250"], Codes("x = y -- fb-ignore: FB0250\nz = w"));
    }

    [Fact]
    public void Ignore_CommentOnItsOwnLine_SilencesTheNextLine()
    {
        NoDiagnostics("-- fb-ignore: FB0251, FB0250\noval(y)");
        NoDiagnostics("--[[ fb-ignore:\nFB0252 ]]\nx = 40000");
        Assert.Equal(["FB0250"], Codes("-- fb-ignore: FB0250\ncls()\nx = y"));
    }

    [Fact]
    public void Ignore_DoesNotSilenceErrors()
    {
        Assert.Equal(["FB0201"], Codes("break -- fb-ignore"));
    }

    // ---- the real API list ----

    [Fact]
    public void Pico8Catalog_LoadsAndHasNoDuplicates()
    {
        var names = Pico8ApiCatalog.Functions.Select(f => f.Name).ToList();
        Assert.True(names.Count > 100);
        Assert.Equal(names.Count, names.Distinct().Count());
        Assert.All(Pico8ApiCatalog.Functions, f => Assert.StartsWith(f.Name + "(", f.Signature));
    }

    [Fact]
    public void Pico8Availability_ComesFromTheCatalogAndTheRuntime()
    {
        var api = ApiAvailability.Pico8;
        Assert.Contains("spr", api.Implemented);
        Assert.Contains("oval", api.Known);
        Assert.DoesNotContain("oval", api.Implemented);
        Assert.True(api.Implemented.IsSubsetOf(api.Known));
    }

    [Theory]
    [MemberData(nameof(RoundTripPrograms.All), MemberType = typeof(RoundTripPrograms))]
    public void ExamplePrograms_HaveNoErrors(string name, string source)
    {
        var analysis = StaticAnalyzer.Analyze(LuaParser.Parse(source, name));
        Assert.False(analysis.HasErrors, string.Join("\n", analysis.Diagnostics));
    }

    // ---- comments collected by the lexer (needed by fb-ignore) ----

    [Fact]
    public void Comments_AreCollectedWithTheirPosition()
    {
        var comments = LuaParser.Parse("x = 1 -- trailing\n-- own line\n// slashes\n--[[ long\ncomment ]] y = 2").Comments;
        Assert.Equal([" trailing", " own line", " slashes", " long\ncomment "], comments.Select(c => c.Text));
        Assert.Equal([false, true, true, true], comments.Select(c => c.OnOwnLine));
        Assert.Equal([1, 2, 3, 4], comments.Select(c => c.Span.Line));
    }

    [Fact]
    public void Comments_InsideStringsAreNotComments()
    {
        Assert.Empty(LuaParser.Parse("x = \"-- not a comment\" .. [[// nor this]]").Comments);
    }
}

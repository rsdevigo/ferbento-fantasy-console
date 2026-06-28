using System.Text;
using Ferbento.Cartridge;
using Ferbento.Core.Chips;
using Ferbento.Profiles.Models;
using Ferbento.Scripting.Interpreter;

namespace Ferbento.Scripting.Api;

public static class SystemApi
{
    public static void Register(Interpreter.Environment env, Pico8Context ctx)
    {
        env.Define("stat", ScriptValue.Native(args =>
        {
            var n = args.Length > 0 ? args[0].AsInt() : 0;
            return n switch
            {
                0 => ScriptValue.Number(ctx.Bus.Memory.Memory.Size / 32),
                1 => ScriptValue.Number(System.Math.Min(100, ctx.CpuSteps / 100)),
                >= 16 and <= 19 => ScriptValue.Number(
                    ctx.Bus.Sound.Engine.Channels[n - 16].IsPlaying ? 1 : 0),
                >= 20 and <= 23 => ScriptValue.Number(
                    ctx.Bus.Sound.Engine.Channels[n - 20].CurrentNoteIndex),
                >= 46 and <= 49 => ScriptValue.Number(
                    ctx.Bus.Sound.Engine.StatSfxOnChannel(n - 46)),
                >= 50 and <= 53 => ScriptValue.Number(
                    ctx.Bus.Sound.Engine.StatNoteOnChannel(n - 50)),
                54 => ScriptValue.Number(ctx.Bus.Sound.Engine.StatCurrentPattern()),
                55 => ScriptValue.Number(ctx.Bus.Sound.Engine.StatTotalPatterns()),
                56 => ScriptValue.Number(ctx.Bus.Sound.Engine.StatTicksOnPattern()),
                57 => ScriptValue.Number(ctx.Bus.Sound.Engine.StatMusicPlaying() ? 1 : 0),
                _ => ScriptValue.Number(0)
            };
        }));

        env.Define("time", ScriptValue.Native(_ => ScriptValue.Number(ctx.Time())));

        env.Define("type", ScriptValue.Native(args =>
        {
            if (args.Length == 0) return ScriptValue.Str("nil");
            return ScriptValue.Str(args[0].Type switch
            {
                ScriptValueType.Nil => "nil",
                ScriptValueType.Number => "number",
                ScriptValueType.String => "string",
                ScriptValueType.Table => "table",
                ScriptValueType.Function or ScriptValueType.NativeFunction => "function",
                ScriptValueType.Coroutine => "thread",
                ScriptValueType.Bool => "boolean",
                _ => "unknown"
            });
        }));

        env.Define("printh", ScriptValue.Native(args =>
        {
            var msg = args.Length > 0 ? args[0].AsString() : "";
            ctx.PrintHandler?.Invoke(msg);
            return ScriptValue.Nil;
        }));

        env.Define("run", ScriptValue.Native(_ =>
        {
            ctx.RebootRequested = true;
            return ScriptValue.Nil;
        }));

        env.Define("reboot", ScriptValue.Native(_ =>
        {
            ctx.RebootRequested = true;
            return ScriptValue.Nil;
        }));

        env.Define("extcmd", ScriptValue.Native(args =>
        {
            if (args.Length == 0) return ScriptValue.Nil;
            var cmd = args[0].AsString();
            ctx.ExtCommandHandler?.Invoke(cmd);
            return ScriptValue.Nil;
        }));

        env.Define("save", ScriptValue.Native(args =>
        {
            var filename = args.Length > 0 ? args[0].AsString() : "";
            if (string.IsNullOrEmpty(filename) || ctx.CurrentSource == null)
                return ScriptValue.Nil;

            try
            {
                var path = ResolvePath(ctx.WorkingDirectory, filename);
                var codeBytes = Encoding.UTF8.GetBytes(ctx.CurrentSource);

                var cartridge = new CartridgeData
                {
                    Title = Path.GetFileNameWithoutExtension(filename),
                    Profile = ctx.Bus.Profile,
                };
                cartridge.Sections.Add(new CartridgeSection
                {
                    Type = CartridgeSectionType.Code,
                    Data = codeBytes,
                });

                CartridgeWriter.WritePngToFile(cartridge, path);
            }
            catch
            {
            }

            return ScriptValue.Nil;
        }));

        env.Define("load", ScriptValue.Native(args =>
        {
            var filename = args.Length > 0 ? args[0].AsString() : "";
            if (string.IsNullOrEmpty(filename))
                return ScriptValue.Nil;

            try
            {
                var path = ResolvePath(ctx.WorkingDirectory, filename);
                var cartridge = CartridgeReader.ReadPngFromFile(path);

                foreach (var section in cartridge.Sections)
                {
                    if (section.Type == CartridgeSectionType.Code)
                    {
                        var source = Encoding.UTF8.GetString(section.Data);
                        if (!string.IsNullOrEmpty(source))
                        {
                            ctx.CurrentSource = source;
                            ctx.RebootRequested = true;
                        }
                        break;
                    }
                }
            }
            catch
            {
            }

            return ScriptValue.Nil;
        }));
    }

    private static string ResolvePath(string? workingDir, string filename)
    {
        if (string.IsNullOrEmpty(workingDir))
            return filename;
        return Path.Combine(workingDir, filename);
    }
}

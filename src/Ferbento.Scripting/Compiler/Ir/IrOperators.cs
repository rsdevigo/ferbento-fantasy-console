namespace Ferbento.Scripting.Compiler.Ir;

/// <summary>Binary operators of PICO-8 Lua.</summary>
public enum IrBinaryOp
{
    Or,
    And,
    Less,
    Greater,
    LessEqual,
    GreaterEqual,
    Equal,
    NotEqual,
    /// <summary><c>|</c></summary>
    BitOr,
    /// <summary><c>^^</c> (PICO-8 xor; <c>^</c> is power)</summary>
    BitXor,
    /// <summary><c>&amp;</c></summary>
    BitAnd,
    /// <summary><c>&lt;&lt;</c></summary>
    ShiftLeft,
    /// <summary><c>&gt;&gt;</c> (arithmetic)</summary>
    ShiftRight,
    /// <summary><c>&gt;&gt;&gt;</c> (logical)</summary>
    LogicalShiftRight,
    /// <summary><c>&lt;&lt;&gt;</c></summary>
    RotateLeft,
    /// <summary><c>&gt;&gt;&lt;</c></summary>
    RotateRight,
    /// <summary><c>..</c></summary>
    Concat,
    Add,
    Subtract,
    Multiply,
    Divide,
    /// <summary><c>\</c> (integer division, <c>flr(a/b)</c>)</summary>
    IntDivide,
    Modulo,
    /// <summary><c>^</c></summary>
    Power,
}

/// <summary>Unary operators of PICO-8 Lua.</summary>
public enum IrUnaryOp
{
    /// <summary><c>-</c></summary>
    Negate,
    /// <summary><c>not</c></summary>
    Not,
    /// <summary><c>#</c></summary>
    Length,
    /// <summary><c>~</c></summary>
    BitNot,
}

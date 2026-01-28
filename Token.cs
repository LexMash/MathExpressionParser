using System;
using static FormulaParser;
using static UnityEngine.Rendering.DebugUI;

public enum TokenType : byte
{
    Number,
    Boolean,
    Identifier,
    Operator,
    Function,
    LeftParenthesis,
    RightParenthesis,
    Comma,
    EndOfExpression,
    Formula,
}

[Serializable]
public readonly struct Token
{
    public static readonly Token LeftParenthesis = new(TokenType.LeftParenthesis, "(");
    public static readonly Token RightParenthesis = new(TokenType.RightParenthesis, ")");
    public static readonly Token Comma = new(TokenType.Comma, ",");
    public static readonly Token True = new(TokenType.Boolean, "true", true);
    public static readonly Token False = new(TokenType.Boolean, "false", false);

    public readonly TokenType Type;
    public readonly OperationType Operation;
    public readonly bool BooleanValue;
    public readonly float NumericValue;
    public readonly string StringValue;

    public Token(TokenType type, float numericValue)
    {
        Type = type;
        Operation = default;
        StringValue = null;
        NumericValue = numericValue;
        BooleanValue = false;
    }

    public Token(TokenType type, string value)
    {
        Type = type;
        Operation = default;
        StringValue = value;
        NumericValue = 0;
        BooleanValue = false;
    }

    public Token(TokenType type, OperationType operation)
    {
        Type = type;
        Operation = operation;
        StringValue = null;
        NumericValue = 0;
        BooleanValue = false;
    }

    public Token(TokenType type, string value, bool booleanValue)
    {
        Type = type;
        Operation = default;
        StringValue = value;
        NumericValue = booleanValue ? 1 : 0;
        BooleanValue = booleanValue;
    }

    public override string ToString() => $"{Type}: {StringValue}";
}

public static class CommonTokens
{
    public static ref readonly Token LeftParenthesis => ref Token.LeftParenthesis;
    public static ref readonly Token RightParenthesis => ref Token.RightParenthesis;
    public static ref readonly Token Comma => ref Token.Comma;
    public static ref readonly Token True => ref Token.True;
    public static ref readonly Token False => ref Token.False;
}

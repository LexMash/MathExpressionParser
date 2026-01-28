using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Pool;

public class FormulaParser : IDisposable
{
    private const char Dot = '.';
    private const char Minus = '-';
    private const char LeftParenthesis = '(';
    private const char RightParenthesis = ')';
    private const char Comma = ',';
    private const string True = "true";
    private const string False = "false";

    // Precomputed data for fast comparison
    private static readonly byte[] precedences = new byte[] { 1, 1, 2, 2, 2, 3, 0, 0 };
    private static readonly bool[] rightAssociative = new bool[] { false, false, false, false, false, true, false, false };

    private static readonly bool[] IsOperatorLookup = CreateOperatorLookup();

    private static bool[] CreateOperatorLookup()
    {
        var lookup = new bool[128]; // For ASCII characters
        lookup['+'] = true;
        lookup['-'] = true;
        lookup['*'] = true;
        lookup['/'] = true;
        lookup['%'] = true;
        lookup['^'] = true;
        lookup['&'] = true;
        lookup['|'] = true;
        return lookup;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsOperatorChar(char c)
    {
        // O(1) access, no branching besides bounds check
        return c < IsOperatorLookup.Length && IsOperatorLookup[c];
    }

    public readonly struct Function
    {
        public readonly Func<float[], float> Method;
        public readonly byte ArgumentsAmount;

        public Function(byte argumentsAmount, Func<float[], float> method)
        {
            ArgumentsAmount = argumentsAmount;
            Method = method;
        }
    }

    public enum OperationType : byte
    {
        Add,        // +
        Subtract,   // -
        Multiply,   // *
        Divide,     // /
        Modulo,     // %
        Power,      // ^
        And,        // &
        Or,         // |
    }

    public readonly struct OperationData
    {
        public readonly byte Precedence;
        public readonly OperationType Type;
        public readonly bool RightAssociative;

        public OperationData(OperationType type, byte precedence, bool rightAssociative)
        {
            Type = type;
            Precedence = precedence;
            RightAssociative = rightAssociative;
        }
    }

    private readonly Dictionary<char, OperationType> operationsMap = new()
    {
        {'+', OperationType.Add },
        {'-', OperationType.Subtract },
        {'*', OperationType.Multiply },
        {'/', OperationType.Divide },
        {'%', OperationType.Modulo },
        {'^', OperationType.Power },
        {'&', OperationType.And },
        {'|', OperationType.Or },
    };

    private readonly Dictionary<int, Func<float>> parameterProviders = new();
    private readonly Dictionary<int, Token[]> formulaCache = new();
    private readonly HashSet<int> formulaNames = new(128);
    private readonly Dictionary<int, float> formulasResultCache = new();
    private readonly ObjectPool<List<Token>> tokenListPool = new(() => new List<Token>(32), list => list.Clear(), actionOnDestroy: list => list.Clear(), collectionCheck: false, defaultCapacity: 4);
    private readonly ObjectPool<float[]> argsCachePool = new(() => new float[16], collectionCheck: false, defaultCapacity: 4);
    private readonly ObjectPool<Stack<float>> stackFloatPool = new(() => new Stack<float>(16), stack => stack.Clear(), actionOnDestroy: stack => stack.Clear(), collectionCheck: false, defaultCapacity: 4);
    private readonly ObjectPool<Stack<Token>> stackTokenPool = new(() => new Stack<Token>(16), stack => stack.Clear(), actionOnDestroy: stack => stack.Clear(), collectionCheck: false, defaultCapacity: 4);

    // Mathematical functions
    private readonly Dictionary<int, Function> functions = new()
    {
        {"sin".GetHashCode(), new (argumentsAmount: 1, method: args => (float)Math.Sin(args[0]))},
        {"cos".GetHashCode(), new (1, args => (float)Math.Cos(args[0]))},
        {"tan".GetHashCode(), new (1, args =>(float) Math.Tan(args[0]))},
        {"sqrt".GetHashCode(), new (1, args => (float)Math.Sqrt(args[0]))},
        {"abs".GetHashCode(), new (1, args => Math.Abs(args[0]))},
        {"min".GetHashCode(), new (2, args => Math.Min(args[0], args[1]))},
        {"max".GetHashCode(), new (2, args => Math.Max(args[0], args[1]))},
        {"pow".GetHashCode(), new (2, args => (float)Mathf.Pow(args[0], args[1]))},
        {"clamp".GetHashCode(), new (3, args => Math.Clamp(args[0], args[1], args[2]))},
        {"lerp".GetHashCode(), new (3, args => args[0] + (args[1] - args[0]) * args[2])},
        {"floor".GetHashCode(), new (1, args => (float)Math.Floor(args[0]))},
        {"ceil".GetHashCode(), new (1, args => (float)Math.Ceiling(args[0]))},
        {"round".GetHashCode(), new (1, args => (float)Math.Round(args[0]))},

        {"and".GetHashCode(), new (2, args => (args[0] != 0 && args[1] != 0) ? 1f : 0f)},
        {"or".GetHashCode(), new (2, args => (args[0] != 0 || args[1] != 0) ? 1f : 0f)},
        {"not".GetHashCode(), new (1, args => (args[0] == 0) ? 1f : 0f)},
        {"xor".GetHashCode(), new (2, args => (args[0] != 0 ^ args[1] != 0) ? 1f : 0f)},

        {"eq".GetHashCode(), new (2, args => Math.Abs(args[0] - args[1]) < 0.00001f ? 1f : 0f)}, //equal
        {"neq".GetHashCode(), new (2, args => Math.Abs(args[0] - args[1]) > 0.00001f ? 1f : 0f)}, //not equal
        {"gt".GetHashCode(), new (2, args => args[0] > args[1] ? 1f : 0f)}, //greater then
        {"lt".GetHashCode(), new (2, args => args[0] < args[1] ? 1f : 0f)}, //less then
        {"gte".GetHashCode(), new (2, args => args[0] >= args[1] ? 1f : 0f)}, //greater or equal
        {"lte".GetHashCode(), new (2, args => args[0] <= args[1] ? 1f : 0f)}, //less or equal
        {"if".GetHashCode(), new (3, args => args[0] != 0 ? args[1] : args[2])},
    };

    public void RegisterParameter(string name, Func<float> provider)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Parameter name cannot be empty");

        parameterProviders[name.GetHashCode()] = provider;
    }

    public float GetParameterValue(string name)
    {
        if (parameterProviders.TryGetValue(name.GetHashCode(), out var func))
            return func();

        throw new ArgumentException("Parameter name is not registered");
    }

    public void RegisterParameter(string name, float value) => RegisterParameter(name, () => value);
    public bool UnregisterParameter(string name) => parameterProviders.Remove(name.GetHashCode());

    public void RegisterFormula(string name, string formula)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Formula name cannot be empty");

        if (string.IsNullOrEmpty(formula))
            throw new ArgumentException("Formula cannot be empty");

        var hash = name.GetHashCode();

        if (formulaNames.Contains(hash))
            throw new ArgumentException("Formula with this name is already registered");

        formulaNames.Add(hash);
        formulaCache[hash] = ConvertToRPN(Tokenize(formula));
    }

    public bool TryUnregisterFormula(string name)
    {
        var hash = name.GetHashCode();

        if (formulaNames.Contains(hash))
        {
            formulaNames.Remove(hash);
            formulaCache.Remove(hash);
            return true;
        }

        return false;
    }

    public float Evaluate(string expression)
    {
        try
        {
            return EvaluateRPN(ConvertToRPN(Tokenize(expression))); ;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error evaluating expression: {expression}\n{ex.Message}");
        }
    }

    public float EvaluateWithCaching(string expression)
    {
        var result = Evaluate(expression);
        formulasResultCache[expression.GetHashCode()] = result;

        return result;
    }

    /// <summary>
    /// Can be used for the name of an expression or formula
    /// </summary>
    /// <param name="nameOrExpression"></param>
    public void RemoveCacheFor(string nameOrExpression)
    {
        var hash = nameOrExpression.GetHashCode();

        if (formulasResultCache.ContainsKey(hash))
            formulasResultCache.Remove(hash);
    }

    public float EvaluateByName(string formulaName)
    {
        var hash = formulaName.GetHashCode();

        if (formulasResultCache.TryGetValue(hash, out var result))
            return result;

        if (formulaCache.TryGetValue(hash, out var rpnTokens))
            return EvaluateRPN(rpnTokens);

        throw new KeyNotFoundException($"Formula '{formulaName}' not found");
    }

    public float EvaluateByNameWithCaching(string formulaName)
    {
        var result = EvaluateByName(formulaName);
        formulasResultCache[formulaName.GetHashCode()] = result;

        return result;
    }

    public void ClearCache()
    {
        formulaCache.Clear();
        formulasResultCache.Clear();
    }

    public void ClearAll()
    {
        parameterProviders.Clear();
        formulaNames.Clear();
        ClearCache();
    }

    public void Dispose()
    {
        tokenListPool.Clear();
        tokenListPool.Dispose();
        argsCachePool.Clear();
        argsCachePool.Dispose();
        operationsMap.Clear();
        functions.Clear();

        ClearAll();
    }

    private List<Token> Tokenize(string expression)
    {
        List<Token> tokens = tokenListPool.Get();
        int position = 0;
        int length = expression.Length;

        while (position < length)
        {
            char current = expression[position];

            if (IsDigit(current) || current == Dot)
            {
                tokens.Add(ReadNumber(expression, ref position));
            }
            else if (char.IsLetter(current))
            {
                tokens.Add(ReadIdentifier(expression, ref position));
            }
            else if (IsOperatorChar(current))
            {
                if (current == Minus &&
                    position + 1 < length &&
                    IsDigit(expression[position + 1]))
                {
                    tokens.Add(ReadNumber(expression, ref position));
                }
                else
                {
                    tokens.Add(new Token(TokenType.Operator, operationsMap[current]));
                    position++;
                }
            }
            else if (current == LeftParenthesis)
            {
                tokens.Add(CommonTokens.LeftParenthesis);
                position++;
            }
            else if (current == RightParenthesis)
            {
                tokens.Add(CommonTokens.RightParenthesis);
                position++;
            }
            else if (current == Comma)
            {
                tokens.Add(CommonTokens.Comma);
                position++;
            }
            else
            {
                position++;
                continue;
            }
        }

        return tokens;
    }

    private Token ReadNumber(string expression, ref int position)
    {
        var startPosition = position;

        if (expression[startPosition] == Minus)
        {
            position++;
        }

        bool hasDecimal = false;
        while (position < expression.Length)
        {
            char current = expression[position];

            if (IsDigit(current))
            {
                position++;
            }
            else if (current == Dot && !hasDecimal)
            {
                hasDecimal = true;
                position++;
            }
            else
            {
                break;
            }
        }

        ReadOnlySpan<char> stringValue = expression.AsSpan(startPosition, position - startPosition);
        float.TryParse(stringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var numericValue);
        return new Token(TokenType.Number, numericValue);
    }

    private Token ReadIdentifier(string expression, ref int position)
    {
        var startPosition = position;
        while (position < expression.Length)
        {
            char current = expression[position];

            if (char.IsLetter(current))
            {
                position++;
            }
            else
            {
                break;
            }
        }

        string value = expression[startPosition..position];

        if (string.Equals(value, True, StringComparison.OrdinalIgnoreCase))
            return CommonTokens.True;

        if (string.Equals(value, False, StringComparison.OrdinalIgnoreCase))
            return CommonTokens.False;

        var hash = value.GetHashCode();

        // Check if it's a function
        if (position < expression.Length &&
            expression[position] == LeftParenthesis &&
            functions.ContainsKey(hash))
        {
            return new Token(TokenType.Function, value);
        }

        if (formulaNames.Contains(hash))
        {
            return new Token(TokenType.Formula, value);
        }

        return new Token(TokenType.Identifier, value);
    }

    private Token[] ConvertToRPN(List<Token> tokens)
    {
        var output = tokenListPool.Get();
        var stack = stackTokenPool.Get();
        var count = tokens.Count;
        for (int i = 0; i < count; i++)
        {
            Token token = tokens[i];
            switch (token.Type)
            {
                case TokenType.Number:
                case TokenType.Identifier:
                case TokenType.Formula:
                case TokenType.Boolean:
                    output.Add(token);
                    break;

                case TokenType.Function:
                case TokenType.LeftParenthesis:
                    stack.Push(token);
                    break;

                case TokenType.Operator:
                    while (stack.Count > 0 && stack.Peek().Type == TokenType.Operator)
                    {
                        OperationType op1Type = token.Operation;
                        OperationType op2Type = stack.Peek().Operation;

                        if (ShouldPopOperator(op1Type, op2Type))
                        {
                            output.Add(stack.Pop());
                        }
                        else
                        {
                            break;
                        }
                    }
                    stack.Push(token);
                    break;

                case TokenType.RightParenthesis:
                    while (stack.Count > 0 && stack.Peek().Type != TokenType.LeftParenthesis)
                    {
                        output.Add(stack.Pop());
                    }

                    if (stack.Count == 0 || stack.Peek().Type != TokenType.LeftParenthesis)
                        throw new Exception("Unbalanced parentheses");

                    stack.Pop(); // Remove left parenthesis

                    if (stack.Count > 0 && stack.Peek().Type == TokenType.Function)
                    {
                        output.Add(stack.Pop());
                    }
                    break;

                case TokenType.Comma:
                    while (stack.Count > 0 && stack.Peek().Type != TokenType.LeftParenthesis)
                    {
                        output.Add(stack.Pop());
                    }
                    break;
            }
        }

        while (stack.Count > 0)
        {
            if (stack.Peek().Type == TokenType.LeftParenthesis)
                throw new Exception("Unbalanced parentheses");

            output.Add(stack.Pop());
        }

        stackTokenPool.Release(stack);
        tokenListPool.Release(tokens);
        Token[] array = output.ToArray();
        tokenListPool.Release(output);
        return array;
    }

    private float EvaluateRPN(Token[] rpnTokens)
    {
        var argsCache = argsCachePool.Get();
        var stack = stackFloatPool.Get();
        for (int i = 0; i < rpnTokens.Length; i++)
        {
            ref Token token = ref rpnTokens[i];
            switch (token.Type)
            {
                case TokenType.Number:
                case TokenType.Boolean:
                    stack.Push(token.NumericValue);
                    break;

                case TokenType.Identifier:
                    if (parameterProviders.TryGetValue(token.StringValue.GetHashCode(), out var provider))
                    {
                        stack.Push(provider());
                    }
                    else
                    {
                        throw new Exception($"Unknown parameter: '{token.StringValue}'");
                    }
                    break;

                case TokenType.Operator:
                    if (stack.Count < 2)
                    {
                        var op = token.Operation;
                        var a = stack.Pop();
                        if (op == OperationType.Subtract)
                            stack.Push(-a);
                        else
                            throw new Exception("Not enough operands for operator");
                    }
                    else
                    {
                        float b = stack.Pop();
                        float a = stack.Pop();
                        var value = PreformOperation(token.Operation, a, b);
                        stack.Push(value);
                    }
                    break;

                case TokenType.Function:
                    if (!functions.TryGetValue(token.StringValue.GetHashCode(), out var func))
                        throw new Exception($"Unknown function: '{token.StringValue}'");

                    // Collect function arguments
                    var count = func.ArgumentsAmount;
                    for (int k = count - 1; k >= 0; k--)
                    {
                        argsCache[k] = stack.Pop();
                    }

                    stack.Push(func.Method(argsCache));
                    break;

                case TokenType.Formula:
                    var res = EvaluateByName(token.StringValue);
                    stack.Push(res);
                    break;

                default:
                    throw new Exception($"Unexpected token: {token.Type}");
            }
        }

        if (stack.Count != 1)
            throw new Exception("Invalid expression");

        argsCachePool.Release(argsCache);
        float result = stack.Pop();
        stackFloatPool.Release(stack);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float PreformOperation(OperationType operation, float a, float b)
    {
        return operation switch
        {
            OperationType.Add => a + b,
            OperationType.Subtract => a - b,
            OperationType.Multiply => a * b,
            OperationType.Divide => b != 0 ? a / b : float.NaN,
            OperationType.Modulo => b != 0 ? a % b : float.NaN,
            OperationType.Power => (float)Math.Pow(a, b),
            OperationType.And => (a != 0 && b != 0) ? 1 : 0,
            OperationType.Or => (a != 0 || b != 0) ? 1 : 0,
            _ => 0f,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ShouldPopOperator(OperationType current, OperationType next)
    {
        int precCurrent = precedences[(int)current];
        int precStack = precedences[(int)next];
        bool isRightAssoc = rightAssociative[(int)current];

        return precCurrent < precStack || (!isRightAssoc && precCurrent == precStack);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsDigit(char c) => c >= '0' && c <= '9';
}

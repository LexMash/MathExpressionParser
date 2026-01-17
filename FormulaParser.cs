using System;
using System.Collections.Generic;
using System.Globalization;
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

    public class Function
    {
        public readonly int ArgumentsAmount;
        public readonly Func<float[], float> Method;

        public Function(int argumentsAmount, Func<float[], float> method)
        {
            ArgumentsAmount = argumentsAmount;
            Method = method;
        }
    }

    public class Operation
    {
        public readonly string Symbol;
        public readonly int Precedence;
        public readonly bool RightAssociative;
        private readonly Func<float, float, float> func;

        public Operation(string symbol, int precedence, bool rightAssociative, Func<float, float, float> func)
        {
            Symbol = symbol;
            Precedence = precedence;
            RightAssociative = rightAssociative;
            this.func = func;
        }

        public float Perform(float a , float b) => func(a, b);
    }

    private readonly Dictionary<string, Func<float>> parameterProviders = new();
    private readonly Dictionary<string, Token[]> formulaCache = new();
    private readonly HashSet<string> formulaNames = new(128);
    private readonly Dictionary<string, float> formulasResultCache = new();
    private readonly ObjectPool<List<Token>> tokenListPool = new(() => new List<Token>(32), list => list.Clear(), actionOnDestroy: list => list.Clear(), collectionCheck: false, defaultCapacity: 4);
    private readonly ObjectPool<float[]> argsCachePool = new(() => new float[16], collectionCheck: false, defaultCapacity: 4);

    // Математические функции
    private static readonly Dictionary<string, Function> functions = new()
    {
        {"sin", new (argumentsAmount: 1, method: args => (float)Math.Sin(args[0]))},
        {"cos", new (1, args => (float)Math.Cos(args[0]))},
        {"tan", new (1, args =>(float) Math.Tan(args[0]))},
        {"sqrt", new (1, args => (float)Math.Sqrt(args[0]))},
        {"abs", new (1, args => Math.Abs(args[0]))},
        {"min", new (2, args => Math.Min(args[0], args[1]))},
        {"max", new (2, args => Math.Max(args[0], args[1]))},
        {"pow", new (2, args => (float)Mathf.Pow(args[0], args[1]))},
        {"clamp", new (3, args => Math.Clamp(args[0], args[1], args[2]))},
        {"lerp", new (3, args => args[0] + (args[1] - args[0]) * args[2])},
        {"floor", new (1, args => (float)Math.Floor(args[0]))},
        {"ceil", new (1, args => (float)Math.Ceiling(args[0]))},
        {"round", new (1, args => (float)Math.Round(args[0]))},

        {"and", new (2, args => (args[0] != 0 && args[1] != 0) ? 1f : 0f)},
        {"or", new (2, args => (args[0] != 0 || args[1] != 0) ? 1f : 0f)},
        {"not", new (1, args => (args[0] == 0) ? 1f : 0f)},
        {"xor", new (2, args => (args[0] != 0 ^ args[1] != 0) ? 1f : 0f)},
    
        {"eq", new (2, args => Math.Abs(args[0] - args[1]) < 0.00001f ? 1f : 0f)}, //equal
        {"neq", new (2, args => Math.Abs(args[0] - args[1]) > 0.00001f ? 1f : 0f)}, //not equal
        {"gt", new (2, args => args[0] > args[1] ? 1f : 0f)}, //greater then
        {"lt", new (2, args => args[0] < args[1] ? 1f : 0f)}, //less then
        {"gte", new (2, args => args[0] >= args[1] ? 1f : 0f)}, //greater or equal
        {"lte", new (2, args => args[0] <= args[1] ? 1f : 0f)}, //less or equal
        {"if", new (3, args => args[0] != 0 ? args[1] : args[2])},
    };

    private static readonly Dictionary<string, Operation> operationsMap = new()
    {
        {"+", new Operation("+", 1, false, (a, b) => a + b) },
        {"-", new Operation("-", 1, false, (a, b) => a - b) },
        {"*", new Operation("*", 2, false, (a, b) => a * b) },
        {"/", new Operation("/", 2, false, (a, b) => b != 0 ? a / b : float.NaN) },
        {"%", new Operation("%", 2, false, (a, b) => b != 0 ? a % b : float.NaN) },
        {"^", new Operation("^", 3, true, (a, b) => (float)Math.Pow(a, b)) },
        {"&", new Operation("&", 0, false, (a, b) => (a != 0 && b != 0) ? 1 : 0) },
        {"|", new Operation("|", 0, false, (a, b) => (a != 0 || b != 0) ? 1 : 0) },
    };

    // Регистрация провайдеров параметров
    public void RegisterParameter(string name, Func<float> provider)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Имя параметра не может быть пустым");

        parameterProviders[name] = provider;
    }

    public void RegisterParameter(string name, float value) => RegisterParameter(name, () => value);
    public bool UnregisterParameter(string name) => parameterProviders.Remove(name);

    // Регистрация формул
    public void RegisterFormula(string name, string formula)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Имя формулы не может быть пустым");

        if (string.IsNullOrEmpty(formula))
            throw new ArgumentException("Формула не может быть пустой");

        // Проверяем валидность формулы
        formulaNames.Add(name);
        formulaCache[name] = ConvertToRPN(Tokenize(formula));
    }

    public bool TryUnregisterFormula(string name)
    {
        if (formulaNames.Contains(name))
        {
            formulaNames.Remove(name);
            formulaCache.Remove(name);
            return true;
        }
        
        return false;
    }

    // Основной метод вычисления
    public float Evaluate(string expression)
    {
        if(formulasResultCache.TryGetValue(expression, out var result))
            return result;

        try
        {
            var tokens = Tokenize(expression);
            return EvaluateRPN(ConvertToRPN(tokens));
        }
        catch (Exception ex)
        {
            throw new Exception($"Ошибка вычисления выражения: {expression}\n{ex.Message}");
        }
    }

    public float EvaluateWithCaching(string expression)
    {
        if(!formulasResultCache.TryGetValue(expression, out var result))
        {
            result = Evaluate(expression);
            formulasResultCache[expression] = result;
        }

        return result;
    }

    /// <summary>
    /// Can be used for the name of an expression or formula
    /// </summary>
    /// <param name="value"></param>
    public void RemoveCacheFor(string value)
    {
        if(formulasResultCache.ContainsKey(value))
            formulasResultCache.Remove(value);
    }

    public float EvaluateByName(string formulaName)
    {
        if (formulasResultCache.TryGetValue(formulaName, out var result))
            return result;

        if (formulaCache.TryGetValue(formulaName, out var rpnTokens))
            return EvaluateRPN(rpnTokens);

        throw new KeyNotFoundException($"Формула '{formulaName}' не найдена");
    }

    public float EvaluateByNameWithCaching(string formulaName)
    {
        if(!formulasResultCache.TryGetValue(formulaName, out var result))
        {
            result = EvaluateByName(formulaName);
            formulasResultCache[formulaName] = result;
        }

        return result;
    }

    public void ClearCache()
    {
        formulaNames.Clear();
        formulaCache.Clear();
    }

    public void ClearAll()
    {
        parameterProviders.Clear();
        formulaCache.Clear();
        formulaNames.Clear();
    }

    public void Dispose()
    {
        tokenListPool.Clear();
        tokenListPool.Dispose();
        ClearAll();
    }

    //private List<Token> Tokenize(string expression)
    public List<Token> Tokenize(string expression)
    {
        List<Token> tokens = tokenListPool.Get();
        int position = 0;
        int length = expression.Length;

        while (position < length)
        {
            char current = expression[position];

            if (char.IsDigit(current) || current == Dot)
            {
                tokens.Add(ReadNumber(expression, ref position));
            }
            else if (char.IsLetter(current))
            {
                tokens.Add(ReadIdentifier(expression, ref position));
            }
            else if (IsOperator(current.ToString()))
            {
                if (current == Minus &&
                    position + 1 < length && 
                    char.IsDigit(expression[position + 1]))
                {
                    tokens.Add(ReadNumber(expression, ref position));
                }
                else
                {
                    tokens.Add(new Token(TokenType.Operator, current.ToString()));
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

            if(char.IsDigit(current))
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

        if (value.Equals(True, StringComparison.OrdinalIgnoreCase))
            return CommonTokens.True;

        if (value.Equals(False, StringComparison.OrdinalIgnoreCase))
            return CommonTokens.False;

        // Проверяем, является ли это функцией
        if (position < expression.Length && 
            expression[position] == LeftParenthesis && 
            functions.ContainsKey(value))
        {
            return new Token(TokenType.Function, value);
        }

        if (formulaNames.Contains(value))
        {
            return new Token(TokenType.Formula, value);
        }

        return new Token(TokenType.Identifier, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsOperator(string op) => operationsMap.ContainsKey(op);

    //private Token[] ConvertToRPN(List<Token> tokens)
    public Token[] ConvertToRPN(List<Token> tokens)
    {
        var output = tokenListPool.Get();
        var stack = new Stack<Token>(32);
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
                        Operation op1 = operationsMap[token.Value];
                        Operation op2 = operationsMap[stack.Peek().Value];
                        int op1Precendence = op1.Precedence;
                        int op2Precendence = op2.Precedence;
                        bool op1RightAssociative = op1.RightAssociative;                  

                        if (op1Precendence < op2Precendence || 
                            !op1RightAssociative && op1Precendence == op2Precendence)
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
                        throw new Exception("Несбалансированные скобки");

                    stack.Pop(); // Убираем левую скобку

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
                throw new Exception("Несбалансированные скобки");

            output.Add(stack.Pop());
        }

        tokenListPool.Release(tokens);
        Token[] array = output.ToArray();
        tokenListPool.Release(output);
        return array;
    }

    //private float EvaluateRPN(Token[] rpnTokens)
    public float EvaluateRPN(Token[] rpnTokens)
    {
        var argsCache = argsCachePool.Get();
        var stack = new Stack<float>(32);
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
                    if (parameterProviders.TryGetValue(token.Value, out var provider))
                    {
                        stack.Push(provider());
                    }
                    else
                    {
                        throw new Exception($"Неизвестный параметр: '{token.Value}'");
                    }
                    break;

                case TokenType.Operator:
                    if (stack.Count < 2)
                    {
                        var op = token.Value;
                        var a = stack.Pop();
                        if (op.Equals("-"))
                            stack.Push(-a);
                        else
                            throw new Exception("Недостаточно операндов для оператора");
                    }
                    else
                    {
                        float b = stack.Pop();
                        float a = stack.Pop();
                        var value = PreformOperation(token.Value, a, b);
                        stack.Push(value);
                    }
                    break;

                case TokenType.Function:
                    if (!functions.TryGetValue(token.Value, out var func))
                        throw new Exception($"Неизвестная функция: '{token.Value}'");

                    // Собираем аргументы функции
                    var count = func.ArgumentsAmount;
                    for (int k = count - 1; k >= 0; k--)
                    {
                        argsCache[k] = stack.Pop();
                    }

                    float result = func.Method(argsCache);
                    stack.Push(result);
                    break;

                case TokenType.Formula:
                    stack.Push(EvaluateByName(token.Value));
                    break;

                default:
                    throw new Exception($"Неожиданный токен: {token.Type}");
            }
        }

        if (stack.Count != 1)
            throw new Exception("Некорректное выражение");

        argsCachePool.Release(argsCache);
        return stack.Pop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float PreformOperation(string op, float a, float b) => operationsMap[op].Perform(a, b);
}
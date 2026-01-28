using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class FormulaParserTests : MonoBehaviour
{
    private FormulaParser parser;
    private int count;

    void Start()
    {
        parser = new FormulaParser();
        RunAllTests();
    }

    void RunAllTests()
    {
        Debug.Log("=== FORMULA PARSER TESTING START ===");

        var stopWatch = Stopwatch.StartNew();

        TestBasicArithmetic();
        TestOperatorPrecedence();
        TestParentheses();
        TestMathFunctions();
        TestComplexExpressions();
        TestEdgeCases();

        // Parameter registration
        parser.RegisterParameter("x", 10);
        parser.RegisterParameter("y", 5);

        // Using logical operations
        TestExpression("gt(x,y) & lt(x,20)", 1, "logic");
        TestExpression("eq(x, y) | gt(x, 0)", 1, "logic");
        TestExpression("if(gt(x, y), 100, 200)", 100, "logic");
        TestExpression("and(gt(x, 5), lt(x, 15))", 1, "logic");
        TestExpression("true & false", 0, "logic");
        TestExpression("true & true", 1, "logic");
        TestExpression("false & false", 0, "logic");
        TestExpression("false & true", 0, "logic");
        TestExpression("false | true", 1, "logic");
        TestExpression("true | true", 1, "logic");
        TestExpression("true | false", 1, "logic");
        TestExpression("false | 1", 1, "logic");
        TestExpression("and(or(eq(x,y),gt(x,0)), false)", 0, "logic");
        stopWatch.Stop();

        Debug.Log($"=== TESTING COMPLETED === TIME {stopWatch.ElapsedMilliseconds}, COUNT {count}");
    }

    void TestBasicArithmetic()
    {
        Debug.Log("\n--- BASIC ARITHMETIC TEST ---");

        // Addition
        TestExpression("2 + 3", 5f, "Addition");
        TestExpression("10 + 20 + 30", 60f, "Multiple addition");

        // Subtraction
        TestExpression("10 - 5", 5f, "Subtraction");
        TestExpression("100 - 20 - 10", 70f, "Multiple subtraction");

        // Multiplication
        TestExpression("5 * 6", 30f, "Multiplication");
        TestExpression("2 * 3 * 4", 24f, "Multiple multiplication");

        // Division
        TestExpression("10 / 2", 5f, "Division");
        TestExpression("100 / 5 / 2", 10f, "Multiple division");
        TestExpression("1 / 3", 1f / 3f, "Fraction division");
        TestExpression("-(2+3)", -5f, "Unary minus before expression");

        parser.RegisterParameter("variable", 5f);
        TestExpression("-variable", -5f, "Minus before identifier");
        TestExpression("sin(-variable)", 0.9589f, 0.0001f, "Unary minus before parameter");

        // Power
        TestExpression("2 ^ 3", 8f, "Power");
        TestExpression("3 ^ 4", 81f, "Power");
        TestExpression("4 ^ 0.5", 2f, "Square root via power");

        // Modulo
        TestExpression("10 % 3", 1f, "Modulo");
        TestExpression("15 % 4", 3f, "Modulo");
        TestExpression("-15 - 15", -30f, "Adding negative numbers");
        TestExpression("15 - -15", 30f, "Subtracting negative from positive");
        TestExpression("15 + -15", 0f, "Adding negative to positive");
        TestExpression("20 + -15", 5f, "Adding negative to positive");
        TestExpression("20.2 + -0.2", 20f, "Adding negative to positive");
    }

    void TestOperatorPrecedence()
    {
        Debug.Log("\n--- OPERATOR PRECEDENCE TEST ---");

        TestExpression("2 + 3 * 4", 14f, "Multiplication before addition");
        TestExpression("3 * 4 + 2", 14f, "Multiplication before addition");
        TestExpression("10 - 4 / 2", 8f, "Division before subtraction");
        TestExpression("2 * 3 ^ 2", 18f, "Power before multiplication");
        TestExpression("2 ^ 3 * 4", 32f, "Power before multiplication");
        TestExpression("10 + 20 % 3", 12f, "Modulo before addition");
    }

    void TestParentheses()
    {
        Debug.Log("\n--- PARENTHESES TEST ---");

        TestExpression("(2 + 3) * 4", 20f, "Parentheses change precedence");
        TestExpression("2 * (3 + 4)", 14f, "Parentheses change precedence");
        TestExpression("(2 + 3) * (4 + 5)", 45f, "Multiple parentheses");
        TestExpression("((2 + 3) * 4) / 2", 10f, "Nested parentheses");
        TestExpression("2 ^ (3 + 1)", 16f, "Parentheses in power");
    }

    void TestMathFunctions()
    {
        Debug.Log("\n--- MATH FUNCTIONS TEST ---");

        // Trigonometry
        TestExpression("sin(0)", 0f, "Sine of 0");
        TestExpression("sin(-5)", 0.9589f, 0.0001f, "Sine of -5");
        TestExpression("sin(3.14159/2)", 1f, 0.0001f, "Sine of π/2");
        TestExpression("cos(0)", 1f, "Cosine of 0");
        TestExpression("cos(3.14159)", -1f, 0.0001f, "Cosine of π");

        // Roots and powers
        TestExpression("sqrt(16)", 4f, "Square root");
        TestExpression("sqrt(2)", 1.414213f, 0.0001f, "Square root of 2");
        TestExpression("pow(2, 3)", 8f, "Power");
        TestExpression("pow(4, 0.5)", 2f, "Square root via pow");

        // Rounding
        TestExpression("floor(3.7)", 3f, "Floor");
        TestExpression("ceil(3.2)", 4f, "Ceiling");
        TestExpression("round(3.5)", 4f, "Round");
        TestExpression("round(3.4)", 3f, "Round");

        // Min/Max
        TestExpression("min(5, 10)", 5f, "Minimum");
        TestExpression("max(5, 10)", 10f, "Maximum");

        // Absolute value
        TestExpression("abs(5)", 5f, "Absolute value of positive");
        TestExpression("abs(-5)", 5f, "Absolute value of negative");

        // Clamp and Lerp
        TestExpression("clamp(15, 10, 20)", 15f, "Clamp within range");
        TestExpression("clamp(5, 10, 20)", 10f, "Clamp below minimum");
        TestExpression("clamp(25, 10, 20)", 20f, "Clamp above maximum");
        TestExpression("lerp(10, 20, 0.5)", 15f, "Lerp midpoint");
        TestExpression("lerp(10, 20, 0)", 10f, "Lerp start");
        TestExpression("lerp(10, 20, 1)", 20f, "Lerp end");
        TestExpression("lerp(10, 20, 0.3)", 13f, "Lerp 30%");
    }

    void TestComplexExpressions()
    {
        Debug.Log("\n--- COMPLEX EXPRESSIONS TEST ---");

        TestExpression("2 + 3 * 4 ^ 2", 50f, "Operator combination");
        TestExpression("(2 + 3) * (4 ^ 2)", 80f, "Parentheses and power");
        TestExpression("sqrt(16) + pow(2, 3)", 12f, "Functions and addition");
        TestExpression("sin(3.14159/2) * cos(0)", 1f, 0.0001f, "Trigonometry");
        TestExpression("min(10, max(5, 15))", 10f, "Nested functions");
        TestExpression("lerp(10, 20, 0.3) + clamp(25, 10, 20)", 33f, "Function combination");
    }

    void TestEdgeCases()
    {
        Debug.Log("\n--- EDGE CASES TEST ---");

        // Division by zero
        TestExpression("5 / 0", float.NaN, "Division by zero");
        TestExpression("0 / 5", 0f, "Zero numerator");

        // Power
        TestExpression("5 ^ 0", 1f, "Any number to power of 0");
        TestExpression("0 ^ 5", 0f, "Zero to positive power");
        TestExpression("0 ^ 0", 1, "Zero to power of zero");

        // Modulo
        TestExpression("5 % 0", float.NaN, "Modulo by zero");
        TestExpression("0 % 5", 0f, "Zero modulo");

        // Function boundary values
        TestExpression("sqrt(0)", 0f, "Square root of zero");
        TestExpression("sqrt(-1)", float.NaN, "Square root of negative");
        TestExpression("abs(0)", 0f, "Absolute value of zero");
    }

    void TestExpression(string expression, float expectedResult, string testName, float tolerance = 0.0001f)
    {
        try
        {
            float actualResult = parser.Evaluate(expression);
            bool isSuccess = Mathf.Abs(actualResult - expectedResult) < tolerance ||
                           (float.IsNaN(actualResult) && float.IsNaN(expectedResult));

            if (isSuccess)
            {
                Debug.Log($"✅ {testName}: {expression} = {actualResult} (expected: {expectedResult})");
            }
            else
            {
                Debug.LogError($"❌ {testName}: {expression} = {actualResult} (expected: {expectedResult})");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"❌ {testName}: Error evaluating '{expression}': {ex.Message}");
        }

        count++;
    }

    void TestExpression(string expression, float expectedResult, float tolerance, string testName)
    {
        TestExpression(expression, expectedResult, testName, tolerance);
    }
}

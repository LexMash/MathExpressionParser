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
        Debug.Log("=== НАЧАЛО ТЕСТИРОВАНИЯ ПАРСЕРА ===");

        var stopWatch = Stopwatch.StartNew();

        TestBasicArithmetic();
        TestOperatorPrecedence();
        TestParentheses();
        TestMathFunctions();
        TestComplexExpressions();
        TestEdgeCases();

        // Регистрация параметров
        parser.RegisterParameter("x", 10);
        parser.RegisterParameter("y", 5);

        // Использование логических операций
        float result1 = parser.Evaluate("gt(x,y) & lt(x,20)"); // Вернет 1 (true)
        float result2 = parser.Evaluate("eq(x, y) | gt(x, 0)"); // Вернет 1 (true)       
        float result3 = parser.Evaluate("if(gt(x, y), 100, 200)"); // Вернет 100
        float result4 = parser.Evaluate("and(gt(x, 5), lt(x, 15))"); // Вернет 1 (true)
        float result5 = parser.Evaluate("true & false"); // Вернет 0 (false)
        float result6 = parser.Evaluate("true & true"); // Вернет 1 (true)
        float result7 = parser.Evaluate("false & false"); // Вернет 0 (false)
        float result8 = parser.Evaluate("false & true"); // Вернет 0 (false)
        float result9 = parser.Evaluate("false | true"); // Вернет 1 (true)
        float result10 = parser.Evaluate("true | true"); // Вернет 1 (true)
        float result11 = parser.Evaluate("true | false"); // Вернет 1 (true)
        float result12 = parser.Evaluate("false | 1"); // Вернет 1 (true)
        float result13 = parser.Evaluate("and(or(eq(x,y),gt(x,0)), false)"); // Вернет 0 (false)

        stopWatch.Stop();

        Debug.Log($"=== ТЕСТИРОВАНИЕ ЗАВЕРШЕНО === ВРЕМЯ {stopWatch.ElapsedMilliseconds}, КОЛ-ВО {count}");
    }

    void TestBasicArithmetic()
    {
        Debug.Log("\n--- ТЕСТ БАЗОВОЙ АРИФМЕТИКИ ---");
        
        // Сложение
        TestExpression("2 + 3", 5f, "Сложение");
        TestExpression("10 + 20 + 30", 60f, "Множественное сложение");
        
        // Вычитание
        TestExpression("10 - 5", 5f, "Вычитание");
        TestExpression("100 - 20 - 10", 70f, "Множественное вычитание");
        
        // Умножение
        TestExpression("5 * 6", 30f, "Умножение");
        TestExpression("2 * 3 * 4", 24f, "Множественное умножение");
        
        // Деление
        TestExpression("10 / 2", 5f, "Деление");
        TestExpression("100 / 5 / 2", 10f, "Множественное деление");
        TestExpression("1 / 3", 1f / 3f, "Деление дробей");
        TestExpression("-(2+3)", -5f, "Унарный минус перед выражением");

        parser.RegisterParameter("variable", 5f);
        TestExpression("-variable", -5f, "Минус перед идентификатором");
        TestExpression("sin(-variable)", 0.9589f, 0.0001f, "Унарный минус перед параметром");

        // Степень
        TestExpression("2 ^ 3", 8f, "Возведение в степень");
        TestExpression("3 ^ 4", 81f, "Возведение в степень");
        TestExpression("4 ^ 0.5", 2f, "Квадратный корень через степень");
        
        // Остаток от деления
        TestExpression("10 % 3", 1f, "Остаток от деления");
        TestExpression("15 % 4", 3f, "Остаток от деления");
        TestExpression("-15 - 15", -30f, "Сложение отрицательных чисел");
        TestExpression("15 - -15", 30f, "Вычитание отрицательного числа из положительного");
        TestExpression("15 + -15", 0f, "Вычитание отрицательного числа из положительного");
        TestExpression("20 + -15", 5f, "Вычитание отрицательного числа из положительного");
        TestExpression("20.2 + -0.2", 20f, "Вычитание отрицательного числа из положительного");
    }

    void TestOperatorPrecedence()
    {
        Debug.Log("\n--- ТЕСТ ПРИОРИТЕТОВ ОПЕРАТОРОВ ---");
        
        TestExpression("2 + 3 * 4", 14f, "Умножение перед сложением");
        TestExpression("3 * 4 + 2", 14f, "Умножение перед сложением");
        TestExpression("10 - 4 / 2", 8f, "Деление перед вычитанием");
        TestExpression("2 * 3 ^ 2", 18f, "Степень перед умножением");
        TestExpression("2 ^ 3 * 4", 32f, "Степень перед умножением");
        TestExpression("10 + 20 % 3", 12f, "Остаток перед сложением");
    }

    void TestParentheses()
    {
        Debug.Log("\n--- ТЕСТ СКОБОК ---");
        
        TestExpression("(2 + 3) * 4", 20f, "Скобки меняют приоритет");
        TestExpression("2 * (3 + 4)", 14f, "Скобки меняют приоритет");
        TestExpression("(2 + 3) * (4 + 5)", 45f, "Множественные скобки");
        TestExpression("((2 + 3) * 4) / 2", 10f, "Вложенные скобки");
        TestExpression("2 ^ (3 + 1)", 16f, "Скобки в степени");
    }

    void TestMathFunctions()
    {
        Debug.Log("\n--- ТЕСТ МАТЕМАТИЧЕСКИХ ФУНКЦИЙ ---");
        
        // Тригонометрия
        TestExpression("sin(0)", 0f, "Синус 0");
        TestExpression("sin(-5)", 0.9589f, 0.0001f, "Синус -5");
        TestExpression("sin(3.14159/2)", 1f, 0.0001f, "Синус π/2");
        TestExpression("cos(0)", 1f, "Косинус 0");
        TestExpression("cos(3.14159)", -1f, 0.0001f, "Косинус π");
        
        // Корни и степени
        TestExpression("sqrt(16)", 4f, "Квадратный корень");
        TestExpression("sqrt(2)", 1.414213f, 0.0001f, "Квадратный корень из 2");
        TestExpression("pow(2, 3)", 8f, "Степень");
        TestExpression("pow(4, 0.5)", 2f, "Квадратный корень через pow");
        
        // Округление
        TestExpression("floor(3.7)", 3f, "Округление вниз");
        TestExpression("ceil(3.2)", 4f, "Округление вверх");
        TestExpression("round(3.5)", 4f, "Округление");
        TestExpression("round(3.4)", 3f, "Округление");
        
        // Минимум/максимум
        TestExpression("min(5, 10)", 5f, "Минимум");
        TestExpression("max(5, 10)", 10f, "Максимум");
        
        // Абсолютное значение
        TestExpression("abs(5)", 5f, "Абсолютное значение положительного");
        TestExpression("abs(-5)", 5f, "Абсолютное значение отрицательного");
        
        // Clamp и Lerp
        TestExpression("clamp(15, 10, 20)", 15f, "Clamp внутри диапазона");
        TestExpression("clamp(5, 10, 20)", 10f, "Clamp снизу");
        TestExpression("clamp(25, 10, 20)", 20f, "Clamp сверху");
        TestExpression("lerp(10, 20, 0.5)", 15f, "Lerp середина");
        TestExpression("lerp(10, 20, 0)", 10f, "Lerp начало");
        TestExpression("lerp(10, 20, 1)", 20f, "Lerp конец");
        TestExpression("lerp(10, 20, 0.3)", 13f, "Lerp 30%");
        Debug.Log(Mathf.Lerp(10, 20, 0.3f));
    }

    void TestComplexExpressions()
    {
        Debug.Log("\n--- ТЕСТ СЛОЖНЫХ ВЫРАЖЕНИЙ ---");
        
        TestExpression("2 + 3 * 4 ^ 2", 50f, "Комбинация операторов");
        TestExpression("(2 + 3) * (4 ^ 2)", 80f, "Скобки и степень");
        TestExpression("sqrt(16) + pow(2, 3)", 12f, "Функции и сложение");
        TestExpression("sin(3.14159/2) * cos(0)", 1f, 0.0001f, "Тригонометрия");
        TestExpression("min(10, max(5, 15))", 10f, "Вложенные функции");
        TestExpression("lerp(10, 20, 0.3) + clamp(25, 10, 20)", 33f, "Комбинация функций");   
    }

    void TestEdgeCases()
    {
        Debug.Log("\n--- ТЕСТ ГРАНИЧНЫХ СЛУЧАЕВ ---");
        
        // Деление на ноль
        TestExpression("5 / 0", float.NaN, "Деление на ноль");
        TestExpression("0 / 5", 0f, "Ноль в числителе");
        
        // Степень
        TestExpression("5 ^ 0", 1f, "Любое число в степени 0");
        TestExpression("0 ^ 5", 0f, "Ноль в положительной степени");
        TestExpression("0 ^ 0", 1, "Ноль в степени ноль");
        
        // Остаток от деления
        TestExpression("5 % 0", float.NaN, "Остаток от деления на ноль");
        TestExpression("0 % 5", 0f, "Ноль по модулю");
        
        // Граничные значения функций
        TestExpression("sqrt(0)", 0f, "Корень из нуля");
        TestExpression("sqrt(-1)", float.NaN, "Корень из отрицательного");
        TestExpression("abs(0)", 0f, "Абсолютное значение нуля");
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
                Debug.Log($"✅ {testName}: {expression} = {actualResult} (ожидалось: {expectedResult})");
            }
            else
            {
                Debug.LogError($"❌ {testName}: {expression} = {actualResult} (ожидалось: {expectedResult})");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"❌ {testName}: Ошибка при вычислении '{expression}': {ex.Message}");
        }

        count++;
    }

    void TestExpression(string expression, float expectedResult, float tolerance, string testName)
    {
        TestExpression(expression, expectedResult, testName, tolerance);
    }
}
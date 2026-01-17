# FormulaParser for Unity

Simple mathematical expression parser with function support, dynamic parameters, and caching for Unity games.

## 🚀 Key Features

- Fast parsing using Reverse Polish Notation (RPN)
- Extensive function library: mathematical, trigonometric, logical
- Dynamic parameters with value providers
- Intelligent caching for optimal performance
- Object pooling to minimize memory allocations
- Formula support with nesting capability

## ⚡ **CRITICAL: Performance Optimization**

For game projects, **proper caching usage is essential**. Incorrect implementation can cause FPS drops!

### Performance Guidelines:

1. **ALWAYS register formulas using `RegisterFormula()` instead of direct `Evaluate()` calls**
2. **Use `EvaluateByNameWithCaching()` for registered formulas**
3. **Limit formula updates to 50 per frame maximum** to avoid performance spikes
4. Cache parsing results and computation results separately

## 📦 Quick Start

```csharp
// Create parser (implements IDisposable)
var parser = new FormulaParser();

// Register parameters
parser.RegisterParameter("health", 100f);
parser.RegisterParameter("damage", 25f);
parser.RegisterParameter("time", () => Time.time);

// Register formula (cached parsing)
parser.RegisterFormula("total_damage", "damage * (1 + sin(time))");

// Evaluate with result caching
float result = parser.EvaluateByNameWithCaching("total_damage");
```

## 📝 API Reference
Parameter Management
``` csharp
// Register static value
parser.RegisterParameter("max_health", 200f);

// Register dynamic provider
parser.RegisterParameter("current_time", () => Time.time);

// Remove parameter
parser.UnregisterParameter("param_name");
Formula Management
csharp
// Register formula (parsing cached)
parser.RegisterFormula("formula_name", "expression");

// Evaluate with caching
float value = parser.EvaluateByNameWithCaching("formula_name");

// Remove formula
parser.TryUnregisterFormula("formula_name");
```

Direct Evaluation
``` csharp
// Direct evaluation (no formula registration)
float result = parser.Evaluate("10 + sin(time) * 5");

// With result caching
float cached = parser.EvaluateWithCaching("10 + sin(time) * 5");
Cache Management
csharp
// Remove specific cache entry
parser.RemoveCacheFor("formula_name");

// Clear all caches
parser.ClearCache();

// Clear everything (parameters, formulas, caches)
parser.ClearAll();
```

Cache Management
```csharp
// Remove specific cache entry
parser.RemoveCacheFor("formula_name");

// Clear all caches
parser.ClearCache();

// Clear everything (parameters, formulas, caches)
parser.ClearAll();
```
## 🔧 Supported Operations
### Arithmetic Operators

"+" (addition)

"-" (subtraction, unary minus)

"*" (multiplication)

"/" (division)

"%" (modulo)

"^" (power)

"&" (logical AND)

"|" (logical OR)

### Mathematical Functions
sin(x), cos(x), tan(x)

sqrt(x), abs(x)

min(a, b), max(a, b), pow(a, b)

clamp(x, min, max), lerp(a, b, t)

floor(x), ceil(x), round(x)

### Logical Functions
and(a, b), or(a, b), not(a), xor(a, b)

eq(a, b), neq(a, b)

gt(a, b), lt(a, b), gte(a, b), lte(a, b)

if(condition, true_value, false_value)

### Constants

true (evaluates to 1)

false (evaluates to 0)

## 🎮 Recommended Usage Pattern

```csharp
public class GameSystem : MonoBehaviour
{
    private FormulaParser parser;
    private readonly HashSet<string> dirtyFormulas = new();
    private const int MAX_UPDATES_PER_FRAME = 50;
    
    void Start()
    {
        parser = new FormulaParser();
        
        // Register all formulas at initialization
        parser.RegisterFormula("player_damage", "base_damage * strength");
        parser.RegisterFormula("enemy_defense", "base_defense * (1 + armor * 0.1)");
        parser.RegisterFormula("final_damage", 
            "max(0, player_damage - enemy_defense)");
    }
    
    void Update()
    {
        // Mark formulas as dirty when parameters change
        if (Input.GetKeyDown(KeyCode.Space))
        {
            dirtyFormulas.Add("player_damage");
            dirtyFormulas.Add("final_damage");
        }
        
        // Process limited number of updates per frame
        UpdateFormulas();
    }
    
    void UpdateFormulas()
    {
        int processed = 0;
        foreach (var formula in dirtyFormulas.ToArray())
        {
            if (processed >= MAX_UPDATES_PER_FRAME)
                break;
                
            // Force cache update
            parser.RemoveCacheFor(formula);
            float value = parser.EvaluateByName(formula);
            
            dirtyFormulas.Remove(formula);
            processed++;
        }
    }
    
    void OnDestroy()
    {
        parser?.Dispose();
    }
}
```
## ⚠️ Common Mistakes to Avoid
❌ DON'T use Evaluate() in Update() for the same expression

❌ DON'T update all formulas every frame

❌ DON'T forget to dispose the parser when done

✅ DO use EvaluateByNameWithCaching() for repeated evaluations

✅ DO batch formula updates (max 50/frame)

✅ DO mark formulas dirty only when inputs change

## 🏗️ Architecture Notes
Tokenization: Converts string to tokens

RPN Conversion: Shunting-yard algorithm

Evaluation: Stack-based RPN evaluation

Caching: Two-level (parsed tokens, computed results)

Pooling: Token lists and argument arrays

## 📊 Performance Tips
Static formulas: Register once, evaluate many times

Dynamic updates: Limit to changed formulas only

Parameter providers: Use delegates for dynamic values

Memory: Parser implements IDisposable - always dispose properly

Frame budget: Stay under 50 formula updates per frame

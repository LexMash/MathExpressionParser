using UnityEngine;

public class FormulaBindingTest : MonoBehaviour
{
    private ExampleDataModel dataModel;
    private FormulaParser formulaParser;
    private FormulaParametersBinder binder;
    public int iterations;

    private void Start()
    {
        dataModel = new ExampleDataModel()
        {
            Strength = 5,
            Endurance = 10,
            Luck = 8,
            IsTested = true,
        };

        formulaParser = new FormulaParser();
        binder = new FormulaParametersBinder(formulaParser);

        var stopWatch = System.Diagnostics.Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            binder.Bind(dataModel);
            formulaParser.ClearAll();
        }

        stopWatch.Stop();

        binder.Bind(dataModel);
        Debug.Log($"TIME {stopWatch.ElapsedMilliseconds} MS");

        formulaParser.RegisterFormula("testFormula", "luck + endurance / luck");
        formulaParser.RegisterFormula("testFormula2", "if(isTested, testFormula, luck)");

        formulaParser.EvaluateByName("testFormula");
        formulaParser.EvaluateByName("testFormula2");
    }

    private void Update()
    {
        for (int i = 0; i < iterations; i++)
        {
            binder.Bind(dataModel);
            formulaParser.ClearAll();
        }
    }

    private void OnDestroy()
    {
        formulaParser?.Dispose();
    }
}

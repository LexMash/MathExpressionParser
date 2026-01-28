using System;
using System.Linq.Expressions;
using System.Reflection;

public class FormulaParametersBinder
{
    private readonly FormulaParser formulaParser;

    public FormulaParametersBinder(FormulaParser formulaParser)
    {
        this.formulaParser = formulaParser;
    }

    public FormulaParametersBinder Bind<T>(T instance) where T : class
    {
        Type type = typeof(T);
        var fields = type.GetFields().AsSpan();

        for (int i = 0; i < fields.Length; i++)
        {
            FieldInfo field = fields[i];

            if (field.GetCustomAttribute(typeof(FormulaParameterBindingAttribute)) is not FormulaParameterBindingAttribute bindAtt)
                continue;

            string bindName = bindAtt.ParameterName;

            ParameterExpression instanceParameter = Expression.Parameter(typeof(T));
            Expression expField = Expression.Field(instanceParameter, field.Name);

            if (field.FieldType != typeof(float) && IsConvertableType(field.FieldType))
            {
                expField = Expression.Convert(expField, typeof(float));
            }
            else
                throw new ArgumentException($"Can't bind type {field.FieldType}");

            Expression<Func<T, float>> lambda = Expression.Lambda<Func<T, float>>(expField, instanceParameter);
            Func<T, float> getterFunc = lambda.Compile();
            formulaParser.RegisterParameter(bindName, getterFunc(instance));
        }

        return this;
    }

    private bool IsConvertableType(Type type)
    {
        return Type.GetTypeCode(type) switch
        {
            TypeCode.Int16 or
            TypeCode.Int32 or
            TypeCode.Int64 or
            TypeCode.Boolean or
            TypeCode.UInt32 or
            TypeCode.Double or
            TypeCode.UInt16 or
            TypeCode.UInt64 or
            TypeCode.Byte or
            TypeCode.SByte or
            TypeCode.Decimal => true,
            _ => false
        };
    }
}

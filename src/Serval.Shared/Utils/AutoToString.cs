namespace Serval.Shared.Utils;

public static class AutoToString
{
    //Helper functions for debugging and error printing data classes

    /// <summary>
    /// Prints the object and its properties using introspection.
    /// </summary>
    /// <param name="o">Object to be printed</param>
    /// <returns>A recursively generated string representation of the object</returns>
    public static string GetGAutoToString(object? o)
    {
        if (!o!.GetType().IsValueType && o.GetType() != typeof(string))
        {
            string soFar = "(" + o!.GetType().Name + ")\n";
            return GetAutoToString(o, soFar, 1);
        }
        return o is null ? "" : o.ToString()!;
    }

    public static string GetAutoToString(object? o, string soFar = "", int tabDepth = 0, int itemIndex = 0)
    {
        if (o!.GetType().IsValueType || o.GetType() == typeof(string))
        {
            var value = o;
            return soFar + (itemIndex > 0 ? "\n" + new string('\t', tabDepth) : " ") + value;
        }
        if (itemIndex > 0)
            soFar += "\n" + new string('\t', tabDepth - 1) + "(" + o.GetType().Name + "@Index" + itemIndex + ")";
        foreach (var property in o.GetType().GetProperties())
        {
            if (property.Name == "Count")
                continue;
            if (property.GetIndexParameters().Count() > 0)
            {
                foreach (var ele in property.GetIndexParameters())
                {
                    try
                    {
                        int index = 0;
                        while (true)
                        {
                            var next_obj = property.GetValue(o, new object[] { index });
                            soFar = GetAutoToString(
                                next_obj,
                                soFar: soFar + "\n" + new string('\t', tabDepth) + (index == 0 ? "[" : ""),
                                tabDepth: tabDepth + 1,
                                itemIndex: ++index
                            );
                        }
                    }
                    catch
                    {
                        soFar += "\n" + new string('\t', tabDepth) + "]";
                    }
                }
            }
            else
            {
                soFar += "\n" + new string('\t', tabDepth) + property.Name + ":";
                soFar = GetAutoToString(property.GetValue(o), soFar: soFar, tabDepth: tabDepth + 1);
            }
        }
        return soFar;
    }
}

namespace Microsoft.Extensions.DependencyInjection;

public static class IServalBuilderExtensions
{
    public static IServalBuilder AddHandlers(this IServalBuilder builder, Assembly assembly)
    {
        foreach (Type type in assembly.GetTypes())
        {
            foreach (Type intf in type.GetInterfaces())
            {
                if (intf.IsGenericType)
                {
                    Type genericType = intf.GetGenericTypeDefinition();
                    if (genericType == typeof(IRequestHandler<,>))
                    {
                        builder.Services.AddScoped(intf, type);
                    }
                    else if (genericType == typeof(IRequestHandler<>))
                    {
                        builder.Services.AddScoped(intf, type);
                    }
                    else if (genericType == typeof(IEventHandler<>))
                    {
                        builder.Services.AddScoped(intf, type);
                    }
                }
            }
        }
        return builder;
    }
}

namespace SIL.DataAccess;

public class DataAccessFieldDefinition<TDocument, TField>(
    Expression<Func<TDocument, TField>> expression,
    string arrayFilterId = ""
) : FieldDefinition<TDocument, TField>
{
    private readonly ExpressionFieldDefinition<TDocument, TField> _internalDef = new(expression);
    private readonly string _arrayFilterId = arrayFilterId;

    public override RenderedFieldDefinition<TField> Render(
        IBsonSerializer<TDocument> documentSerializer,
        IBsonSerializerRegistry serializerRegistry,
        LinqProvider linqProvider
    )
    {
        RenderedFieldDefinition<TField> rendered = _internalDef.Render(
            documentSerializer,
            serializerRegistry,
            linqProvider
        );
        string fieldName = rendered.FieldName.Replace(ArrayPosition.All.ToString(CultureInfo.InvariantCulture), "$[]");
        fieldName = fieldName.Replace(ArrayPosition.FirstMatching.ToString(CultureInfo.InvariantCulture), "$");
        fieldName = fieldName.Replace(
            ArrayPosition.ArrayFilter.ToString(CultureInfo.InvariantCulture),
            $"$[{_arrayFilterId}]"
        );
        if (fieldName != rendered.FieldName)
        {
            return new RenderedFieldDefinition<TField>(
                fieldName,
                rendered.FieldSerializer,
                rendered.ValueSerializer,
                rendered.UnderlyingSerializer
            );
        }
        return rendered;
    }
}

using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace SmartTaskManager.Api.ModelBinding;

/// <summary>
/// Binds enum values from the query string or route by name only (case-insensitive). The default binder
/// also accepts numbers ("status=1") and comma lists that .NET reads as flag combinations
/// ("status=ToDo,InProgress" becomes InProgress); both are rejected here, matching the JSON rules.
/// </summary>
internal sealed class StrictEnumModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        if (context.BindingInfo.BindingSource == BindingSource.Body)
        {
            return null; // bodies are handled by the JSON converter (integers disallowed there too)
        }

        var enumType = Nullable.GetUnderlyingType(context.Metadata.ModelType) ?? context.Metadata.ModelType;
        return enumType.IsEnum ? new StrictEnumModelBinder(enumType) : null;
    }
}

internal sealed class StrictEnumModelBinder(Type enumType) : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var result = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (result == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, result);
        var value = result.FirstValue?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return Task.CompletedTask; // optional filter left empty
        }

        // Enum names are letters only, so this rules out numbers, signs and comma-separated lists.
        if (value.All(char.IsLetter)
            && Enum.TryParse(enumType, value, ignoreCase: true, out var parsed)
            && Enum.IsDefined(enumType, parsed!))
        {
            bindingContext.Result = ModelBindingResult.Success(parsed);
            return Task.CompletedTask;
        }

        bindingContext.ModelState.TryAddModelError(
            bindingContext.ModelName,
            $"The value '{value}' is not valid for {bindingContext.ModelMetadata.Name}. Use one of: {string.Join(", ", Enum.GetNames(enumType))}.");
        return Task.CompletedTask;
    }
}

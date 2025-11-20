using System.ComponentModel.DataAnnotations;

namespace Chatbots.Api.Services;

public static class ValidationExtensions
{
    public static bool TryValidate(object instance, out Dictionary<string, string[]> errors)
    {
        var context = new ValidationContext(instance);
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(instance, context, results, true);

        errors = results
            .SelectMany(result => result.MemberNames.Any()
                ? result.MemberNames.Select(name => new KeyValuePair<string, string>(name, result.ErrorMessage ?? "Invalid"))
                : new[] { new KeyValuePair<string, string>(string.Empty, result.ErrorMessage ?? "Invalid") })
            .GroupBy(kvp => kvp.Key)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Value).ToArray());

        return isValid;
    }

    public static Dictionary<string, string[]> MergeErrors(params Dictionary<string, string[]>[] errorSets)
    {
        var merged = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var errors in errorSets)
        {
            foreach (var kvp in errors)
            {
                if (merged.TryGetValue(kvp.Key, out var existing))
                {
                    merged[kvp.Key] = existing.Concat(kvp.Value).ToArray();
                }
                else
                {
                    merged[kvp.Key] = kvp.Value;
                }
            }
        }

        return merged;
    }
}

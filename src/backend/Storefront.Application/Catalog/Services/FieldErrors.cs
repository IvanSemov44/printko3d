using Storefront.Application.Exceptions;

namespace Storefront.Application.Catalog;

/// <summary>Collects the problems of one request, by field name, and throws them together.</summary>
internal sealed class FieldErrors
{
    private readonly Dictionary<string, List<string>> _errors = [];

    public void Add(string field, string message)
    {
        if (!_errors.TryGetValue(field, out var messages))
        {
            _errors[field] = messages = [];
        }

        messages.Add(message);
    }

    public bool Has(string field) => _errors.ContainsKey(field);

    public void ThrowIfAny(string message)
    {
        if (_errors.Count > 0)
        {
            throw new ValidationFailedException(
                message,
                _errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()));
        }
    }
}

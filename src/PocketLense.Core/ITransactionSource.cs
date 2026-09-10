using PocketLense.Core.Models;

namespace PocketLense.Core;

public interface ITransactionSource
{
    string Name
    {
        get;
    }
    Task<List<Transaction>> ReadAsync(CancellationToken cancellationToken = default);
}

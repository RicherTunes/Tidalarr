using System.Threading.Tasks;
using Tidalarr.Core.Models;

namespace Tidalarr.Infrastructure.Storage;

public interface ITokenStorage
{
    Task SaveTokensAsync(TidalTokens tokens);
    Task<TidalTokens?> LoadTokensAsync();
    Task DeleteTokensAsync();
}


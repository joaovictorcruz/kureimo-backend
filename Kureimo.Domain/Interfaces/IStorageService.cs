using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kureimo.Domain.Interfaces
{
    public interface IStorageService
    {
        Task<string> UploadProfilePicAsync(Stream imageStream, string fileName, Guid userId, CancellationToken ct = default);
        Task<string> UploadSetImageAsync(Stream imageStream, string fileName, string accessToken, CancellationToken ct = default);
    }
}

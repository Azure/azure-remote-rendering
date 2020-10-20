using System.Threading.Tasks;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    public interface IRemoteRenderingStorageAccountData
    {
        string StorageAccountName { get; }
        string DefaultContainer { get; }
        bool ModelPathByUsername { get; }
        AuthenticationType AuthType { get; }
        bool IsValid();
        Task<string> GetAuthData();
    }
}

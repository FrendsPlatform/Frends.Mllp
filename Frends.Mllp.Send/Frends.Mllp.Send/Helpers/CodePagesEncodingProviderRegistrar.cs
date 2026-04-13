using System.Text;

namespace Frends.Mllp.Send.Helpers;

internal static class CodePagesEncodingProviderRegistrar
{
    private static readonly object Lock = new();
    private static bool registered;

    internal static void EnsureRegistered()
    {
        if (registered)
            return;

        lock (Lock)
        {
            if (!registered)
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                registered = true;
            }
        }
    }
}

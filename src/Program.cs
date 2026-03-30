using System.Windows.Forms;
using PbiRestProxy.Dax;
using PbiRestProxy.Discovery;
using PbiRestProxy.Logging;
using PbiRestProxy.Rest;
using PbiRestProxy.Session;
using PbiRestProxy.UI;

namespace PbiRestProxy;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var logStore = new LogStore();
        var sessionService = new AppSessionService(logStore);
        var discoveryService = new PowerBiDiscoveryService(logStore);
        var daxQueryService = new AdomdDaxQueryService(logStore);
        var localRestApiHost = new LocalRestApiHost(logStore, sessionService, daxQueryService);

        logStore.WriteInfo("App", "Started pbi-rest-proxy desktop shell.");
        localRestApiHost.StartAsync().GetAwaiter().GetResult();

        try
        {
            Application.Run(new MainForm(sessionService, logStore, discoveryService, daxQueryService, localRestApiHost));
        }
        finally
        {
            localRestApiHost.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}

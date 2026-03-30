using System.Windows.Forms;
using PbiRestProxy.Dax;
using PbiRestProxy.Discovery;
using PbiRestProxy.Logging;
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

        logStore.WriteInfo("App", "Started pbi-rest-proxy desktop shell.");

        Application.Run(new MainForm(sessionService, logStore, discoveryService, daxQueryService));
    }
}

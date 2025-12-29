using Altium.Controls;
using DXP;
using EasyEDA_LoaderNG;
using System.Runtime.InteropServices;

namespace CSharpPlugin
{
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public class PluginFactory
    {
        public object InvokePluginFactory(IClient client)
        {
            if (!client.ProductInfo().SupportsUIFeature("NoGUI", false))
            {
                IUITheme uiTheme = (client as IUIThemeManager).CurrentUITheme();
                if (uiTheme != null)
                    Style.Init(uiTheme.GetHRID(), uiTheme.GetAttributeDictionary());
                else
                    Style.Init();

            }
            return (object)new EasyEDALoaderNGModule(client);
        }
    }
}
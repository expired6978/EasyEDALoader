using DXP;
using EDP;
using PCB;
using SCH;
using System;
using System.Collections.Generic;

namespace EasyEDA_Loader
{
    public class AltiumApi
    {
        internal static class GlobalVars
        {
            private static IWorkspace workspace = null;
            private static IPCB_ServerInterface pcbServer = null;
            private static ISch_ServerInterface schServer = null;
            private static Dictionary<string, string> documentKindExtensions = new Dictionary<string, string>();

            public static IClient Client
            {
                get => DXP.GlobalVars.Client;
                set => DXP.GlobalVars.Client = value;
            }

            public static IWorkspace Workspace
            {
                get
                {
                    if (workspace == null)
                        workspace = DXP.GlobalVars.DXPWorkSpace as IWorkspace;
                    return workspace;
                }
            }

            public static IPCB_ServerInterface PCBServer
            {
                get
                {
                    if (pcbServer == null)
                    {
                        Client.StartServer("PCB");
                        pcbServer = Client.GetServerModuleByName("PCB") as IPCB_ServerInterface;
                        if (pcbServer == null)
                            throw new Exception("Cannot instantiate PCB server.");
                    }
                    return pcbServer;
                }
            }

            public static ISch_ServerInterface SCHServer
            {
                get
                {
                    if (schServer == null)
                    {
                        Client.StartServer("SCH");
                        schServer = Client.GetServerModuleByName("SCH") as ISch_ServerInterface;
                        if (schServer == null)
                            throw new Exception("Cannot instantiate SCH server.");
                    }
                    return schServer;
                }
            }

            public static IClientAPI_Interface ClientAPI => Client == null ? null : Client.GetClientAPI();

            public static string DocumentKindExtension(string documentKind)
            {
                if (string.IsNullOrEmpty(documentKind))
                    return string.Empty;
                string extensionForDocumentKind;
                if (!documentKindExtensions.TryGetValue(documentKind, out extensionForDocumentKind))
                {
                    extensionForDocumentKind = Client.GetDefaultExtensionForDocumentKind(documentKind);
                    documentKindExtensions.Add(documentKind, extensionForDocumentKind);
                }
                return extensionForDocumentKind;
            }
        }

        public static int MmToCoord(double value)
        {
            return EDP.Utils.MMsToCoord(value);
        }

        public static int MilsToCoord(double value)
        {
            return EDP.Utils.MilsToCoord(value);
        }

        public static string GetActiveProjectPath()
        {
            try
            {
                IClient client = GlobalVars.Client;
                if (client == null) return string.Empty;

                IServerDocumentView currentView = client.GetCurrentView();

                if (currentView != null && currentView.GetOwnerDocument() != null)
                {
                    string fullPath = currentView.GetOwnerDocument().GetFileName();

                    if (!string.IsNullOrEmpty(fullPath))
                    {
                        string baseDir = System.IO.Path.GetDirectoryName(fullPath) ?? string.Empty;

                        if (!string.IsNullOrEmpty(baseDir))
                        {
                            // On définit le chemin du sous-dossier
                            string datasheetDir = System.IO.Path.Combine(baseDir, "Datasheets");

                            // On crée le répertoire s'il n'existe pas
                            if (!System.IO.Directory.Exists(datasheetDir))
                            {
                                System.IO.Directory.CreateDirectory(datasheetDir);
                            }

                            return datasheetDir;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Erreur Dossier Datasheet: " + ex.Message);
            }
            return string.Empty;
        }

    }
}

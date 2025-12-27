using EasyEDA_Loader;
using DXP;
using EDP;
using PCB;
using SCH;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EasyEDA_LoaderNG
{
    internal class Helper
    {

            private static readonly string LogPath = @"C:\Temp\EasyEDA_LoaderNG.log";

            public static void Log(string msg)
            {
                try
                {
                    Directory.CreateDirectory(@"C:\Temp");
                    File.AppendAllText(
                        LogPath,
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {msg}\r\n");
                }
                catch
                {
                    // Jamais bloquer l'extension pour un log
                }
            }

    }
}

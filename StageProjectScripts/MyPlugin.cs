using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;

[assembly: ExtensionApplication(typeof(StageProjectScripts.MyPlugin))]

namespace StageProjectScripts
{
    internal class MyPlugin : IExtensionApplication

    {
        void IExtensionApplication.Initialize()
        {
            Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("Program loaded \n");
        }
        void IExtensionApplication.Terminate()
        {

        }
    }
}
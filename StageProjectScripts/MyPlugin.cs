using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;

[assembly: ExtensionApplication(typeof(StageProjectScripts.MyPlugin))]

namespace StageProjectScripts
{
    internal class MyPlugin : IExtensionApplication

    {
        void IExtensionApplication.Initialize()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc != null)
            {
                SystemObjects.DynamicLinker.LoadModule(
                    "AcMPolygonObj" + Application.Version.Major + ".dbx", false, false);
                doc.Editor.WriteMessage("Program loaded \n");
            }
        }
        void IExtensionApplication.Terminate()
        {

        }
    }
}
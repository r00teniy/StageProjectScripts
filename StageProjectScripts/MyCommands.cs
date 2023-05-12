using Autodesk.AutoCAD.Runtime;

using StageProjectScripts.Forms;
using StageProjectScripts.Functions;

[assembly: CommandClass(typeof(StageProjectScripts.MyCommands))]

namespace StageProjectScripts
{
    internal class MyCommands
    {
        [CommandMethod("StageProjectScripts")]
        static public void StageProjectScripts()
        {
            var MW = new MainWindow();
            MW.Show();
        }
        [CommandMethod("StageProjectChecks")]
        static public void StageProjectChecks()
        {
            var settingsStorage = new SettingsStorage();
            var variables = settingsStorage.ReadSettingsFromXML();
            variables.SavedData = settingsStorage.ReadData();
            var CW = new ChecksWindow(variables);
            CW.Show();
        }
    }
}
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
            var variables = SettingsStorage.ReadSettingsFromXML();
            variables.SavedData = SettingsStorage.ReadData();
            var MW = new MainWindow(variables);
            MW.Show();
        }
        [CommandMethod("StageProjectChecks")]
        static public void StageProjectChecks()
        {
            var variables = SettingsStorage.ReadSettingsFromXML();
            variables.SavedData = SettingsStorage.ReadData();
            var CW = new ChecksWindow(variables);
            CW.Show();
        }
    }
}